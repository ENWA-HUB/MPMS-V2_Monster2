using System.Globalization;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using MAIPT.PM.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MAIPT.PM.Api.Data;

public static class ExcelExchangeService
{
    private static readonly string[] AllocationNames =
    [
        "Property", "Group Ltd.", "Bitexco JSC", "The Garden", "BWP", "BHMC", "Power", "Energy",
        "Itel", "Big Capital", "Big Nano", "Minh Quang", "BT Chu Văn An", "BOT Thanh Hóa", "Diamond", "Khác"
    ];

    public static async Task<(byte[] Bytes, string FileName)> ExportKpiAsync(
        AppDbContext db, IWebHostEnvironment env, long periodId, long currentUserId, bool canManage)
    {
        var period = await db.PerformancePeriods.AsNoTracking().FirstOrDefaultAsync(x => x.Id == periodId)
                     ?? throw new InvalidOperationException("Performance period not found.");
        if (!canManage && period.UserId != currentUserId)
            throw new UnauthorizedAccessException("You can export only your own KPI period.");

        var items = await db.PerformanceItems.AsNoTracking()
            .Where(x => x.PerformancePeriodId == periodId).OrderBy(x => x.SourceRow).ThenBy(x => x.Id).ToListAsync();

        var template = Path.Combine(env.ContentRootPath, "Data", "Templates", "UPF_ITJSC.Monthly_KPI.Template.xlsx");
        if (!File.Exists(template)) throw new FileNotFoundException("KPI Excel template is missing.", template);

        using var book = new XLWorkbook(template);
        var ws = book.Worksheets.FirstOrDefault(x => x.Name.Equals("NguyenNhuThanh", StringComparison.OrdinalIgnoreCase))
                 ?? book.Worksheets.FirstOrDefault(x => x.Cell("A2").GetString().Contains("INDIVIDUAL PERFORMANCE", StringComparison.OrdinalIgnoreCase))
                 ?? throw new InvalidOperationException("Individual KPI worksheet was not found in the template.");

        // Keep one styled individual worksheet. This preserves the exact corporate UPF format from the supplied workbook.
        foreach (var other in book.Worksheets.Where(x => x.Position != ws.Position).ToList()) other.Delete();
        ws.Name = SafeSheetName(period.EmployeeName.Length > 0 ? period.EmployeeName : "KPI");

        ws.Cell("A3").Value = period.Period.Length == 4 ? $"Năm/Year: {period.Period}" : $"Tháng/Month: {FormatPeriod(period.Period)}";
        ws.Cell("C5").Value = period.Department;
        ws.Cell("E5").Value = period.EmployeeName;

        var totalRow = FindTotalRow(ws, 9, 80);
        if (totalRow < 10) totalRow = 27;
        var capacity = totalRow - 9;
        if (items.Count > capacity)
            throw new InvalidOperationException($"Template supports {capacity} KPI rows; this period has {items.Count}. Reduce rows or extend the UPF template.");

        // Clear editable values, preserve all formatting/merged cells/borders/column widths.
        ws.Range(9, 1, totalRow - 1, 28).Clear(XLClearOptions.Contents);

        for (var i = 0; i < items.Count; i++)
        {
            var row = 9 + i;
            var item = items[i];
            ws.Cell(row, 1).Value = i + 1;
            ws.Cell(row, 2).Value = item.Function;
            ws.Cell(row, 3).Value = item.Plan;
            ws.Cell(row, 4).Value = item.Actual;
            ws.Cell(row, 5).Value = item.Weight;
            ws.Cell(row, 6).Value = item.SelfScore;
            ws.Cell(row, 7).Value = item.ManagerScore;
            ws.Cell(row, 8).Value = item.HodScore;
            ws.Cell(row, 9).Value = item.Note;

            var allocations = ParseAllocations(item.AllocationsJson);
            double total = 0;
            for (var a = 0; a < AllocationNames.Length; a++)
            {
                var value = allocations.TryGetValue(AllocationNames[a], out var v) ? v : 0;
                ws.Cell(row, 13 + a).Value = value;
                total += value;
            }
            ws.Cell(row, 12).Value = total;
        }

        var first = 9;
        var last = Math.Max(first, first + items.Count - 1);
        ws.Cell(totalRow, 3).Value = "TỔNG/TOTAL";
        ws.Cell(totalRow, 5).FormulaA1 = $"=SUM(E{first}:E{last})";
        ws.Cell(totalRow, 6).FormulaA1 = $"=IFERROR(SUMPRODUCT(E{first}:E{last},F{first}:F{last})/SUM(E{first}:E{last}),0)";
        ws.Cell(totalRow, 7).FormulaA1 = $"=IFERROR(SUMPRODUCT(E{first}:E{last},G{first}:G{last})/SUM(E{first}:E{last}),0)";
        ws.Cell(totalRow, 8).FormulaA1 = $"=IFERROR(SUMPRODUCT(E{first}:E{last},H{first}:H{last})/SUM(E{first}:E{last}),0)";
        ws.Range(first, 5, totalRow, 8).Style.NumberFormat.Format = "0.00";
        ws.Range(first, 12, totalRow - 1, 28).Style.NumberFormat.Format = "0.00%";

        using var ms = new MemoryStream();
        book.SaveAs(ms);
        return (ms.ToArray(), $"KPI_{period.Period}_{SafeFileName(period.EmployeeName)}.xlsx");
    }

    public static async Task<object> ImportKpiAsync(
        AppDbContext db, Stream stream, string fileName, long currentUserId, bool canManage, bool dryRun, string mode)
    {
        using var book = new XLWorkbook(stream);
        var parsed = new List<KpiSheet>();
        var warnings = new List<string>();

        foreach (var ws in book.Worksheets)
        {
            if (!LooksLikeIndividualKpi(ws)) continue;
            var name = ws.Cell("E5").GetString().Trim();
            var dept = ws.Cell("C5").GetString().Trim();
            var period = ParsePeriod(ws.Cell("A3").GetString());
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(period))
            {
                warnings.Add($"{ws.Name}: missing employee name or period; skipped.");
                continue;
            }

            var user = await FindUserByNameAsync(db, name);
            if (user is null)
            {
                warnings.Add($"{ws.Name}: member '{name}' does not exist in Project Team; skipped.");
                continue;
            }
            if (!canManage && user.Id != currentUserId)
            {
                warnings.Add($"{ws.Name}: not your KPI sheet; skipped.");
                continue;
            }

            var rows = new List<KpiRow>();
            var totalRow = FindTotalRow(ws, 9, 120);
            if (totalRow < 10) totalRow = Math.Max(10, ws.LastRowUsed()?.RowNumber() ?? 30);
            for (var r = 9; r < totalRow; r++)
            {
                var plan = ws.Cell(r, 3).GetString().Trim();
                if (string.IsNullOrWhiteSpace(plan)) continue;
                var weight = CellDouble(ws.Cell(r, 5), warnings, $"{ws.Name}!E{r}");
                var self = CellDouble(ws.Cell(r, 6), warnings, $"{ws.Name}!F{r}");
                var manager = CellDouble(ws.Cell(r, 7), warnings, $"{ws.Name}!G{r}");
                var hod = CellDouble(ws.Cell(r, 8), warnings, $"{ws.Name}!H{r}");
                if (!canManage) { manager = 0; hod = 0; }

                var allocations = new Dictionary<string, double>();
                for (var a = 0; a < AllocationNames.Length; a++)
                {
                    var v = CellDouble(ws.Cell(r, 13 + a), warnings, $"{ws.Name}!{ws.Cell(r, 13 + a).Address}", false);
                    if (Math.Abs(v) > 0.0000001) allocations[AllocationNames[a]] = v;
                }
                rows.Add(new KpiRow(r, ws.Cell(r, 2).GetString().Trim(), plan, ws.Cell(r, 4).GetString().Trim(), weight,
                    self, manager, hod, ws.Cell(r, 9).GetString().Trim(), allocations));
            }
            parsed.Add(new KpiSheet(ws.Name, user.Id, user.Name, dept.Length > 0 ? dept : user.Department, period, rows));
        }

        if (dryRun)
            return new { dryRun = true, fileName, sheets = parsed.Select(x => new { x.SheetName, x.EmployeeName, x.Period, itemCount = x.Rows.Count, totalWeight = Math.Round(x.Rows.Sum(r => r.Weight), 4) }), warnings };

        var importedPeriods = 0;
        var importedItems = 0;
        foreach (var sheet in parsed)
        {
            var period = await db.PerformancePeriods.Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.UserId == sheet.UserId && x.Period == sheet.Period && x.Level == "INDIVIDUAL");
            if (period?.IsLocked == true)
            {
                warnings.Add($"{sheet.EmployeeName} {sheet.Period}: period is locked; skipped.");
                continue;
            }
            if (period is null)
            {
                period = new PerformancePeriod
                {
                    PeriodKey = $"EXCEL-{sheet.Period}-{sheet.UserId}-{Guid.NewGuid().ToString("N")[..6]}",
                    Period = sheet.Period, Level = "INDIVIDUAL", UserId = sheet.UserId, EmployeeName = sheet.EmployeeName,
                    Department = sheet.Department, SourceSheet = $"EXCEL:{fileName}/{sheet.SheetName}", Status = "OPEN"
                };
                db.PerformancePeriods.Add(period);
                await db.SaveChangesAsync();
            }
            else if (mode.Equals("replace", StringComparison.OrdinalIgnoreCase))
            {
                db.PerformanceItems.RemoveRange(period.Items);
                period.SourceSheet = $"EXCEL:{fileName}/{sheet.SheetName}";
                period.Department = sheet.Department;
            }

            foreach (var row in sheet.Rows)
            {
                var final = row.HodScore > 0 ? row.HodScore : row.ManagerScore > 0 ? row.ManagerScore : row.SelfScore;
                db.PerformanceItems.Add(new PerformanceItem
                {
                    PerformancePeriodId = period.Id, SourceRow = row.SourceRow, Function = row.Function, Plan = row.Plan,
                    Actual = row.Actual, Weight = row.Weight, SelfScore = row.SelfScore, ManagerScore = row.ManagerScore,
                    HodScore = row.HodScore, FinalScore = final, Note = row.Note,
                    AllocationsJson = JsonSerializer.Serialize(row.Allocations), Status = "OPEN"
                });
                importedItems++;
            }
            importedPeriods++;
        }
        await db.SaveChangesAsync();
        return new { dryRun = false, importedPeriods, importedItems, warnings };
    }

    public static async Task<(byte[] Bytes, string FileName)> ExportBudgetAsync(AppDbContext db, int year)
    {
        var items = await db.BudgetPlanItems.AsNoTracking().Include(x => x.Project)
            .Where(x => x.BudgetYear == year).OrderBy(x => x.OrgUnit).ThenBy(x => x.Name).ToListAsync();
        using var book = new XLWorkbook();
        var ws = book.AddWorksheet("Budget Plan");
        var headers = new[] { "Budget Year", "Business Unit", "Project Code", "Plan Item", "Category", "Vendor", "Planned Amount", "Quantity", "Unit Price", "Planned Month", "Status", "Note",
            "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
        var months = headers.Skip(12).ToArray();
        var r = 2;
        foreach (var x in items)
        {
            ws.Cell(r, 1).Value = x.BudgetYear; ws.Cell(r, 2).Value = x.OrgUnit; ws.Cell(r, 3).Value = x.Project?.Code ?? "";
            ws.Cell(r, 4).Value = x.Name; ws.Cell(r, 5).Value = x.Category; ws.Cell(r, 6).Value = x.Vendor;
            ws.Cell(r, 7).Value = x.PlannedAmount; if (x.Quantity.HasValue) ws.Cell(r, 8).Value = x.Quantity.Value; if (x.UnitPrice.HasValue) ws.Cell(r, 9).Value = x.UnitPrice.Value;
            ws.Cell(r, 10).Value = x.PlannedMonth; ws.Cell(r, 11).Value = x.Status; ws.Cell(r, 12).Value = x.Note;
            var mp = ParseAllocations(x.MonthlyPlanJson);
            for (var m = 0; m < months.Length; m++) ws.Cell(r, 13 + m).Value = mp.TryGetValue(months[m], out var v) ? v : 0;
            r++;
        }
        var header = ws.Range(1, 1, 1, headers.Length);
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#082D57"); header.Style.Font.FontColor = XLColor.White; header.Style.Font.Bold = true;
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center; header.Style.Alignment.WrapText = true;
        ws.SheetView.FreezeRows(1); ws.Range(2, 7, Math.Max(2, r - 1), 9).Style.NumberFormat.Format = "#,##0";
        ws.Columns(1, headers.Length).AdjustToContents();
        foreach (var col in ws.Columns(1, headers.Length)) if (col.Width > 40) col.Width = 40;
        if (r > 2) ws.Range(1, 1, r - 1, headers.Length).CreateTable("BudgetPlanTable");

        var ins = book.AddWorksheet("Instructions");
        ins.Cell("A1").Value = "MPMS Annual Budget Plan Excel Exchange"; ins.Cell("A1").Style.Font.Bold = true; ins.Cell("A1").Style.Font.FontSize = 16;
        ins.Cell("A3").Value = "Round-trip format: Export from MPMS, edit values, then Import Excel. Required columns: Budget Year, Business Unit, Plan Item, Planned Amount.";
        ins.Cell("A4").Value = "Project Code is optional; if supplied it must match an existing MPMS project code.";
        ins.Cell("A5").Value = "Import mode MERGE updates an existing item matched by Year + Business Unit + Plan Item; otherwise creates a new item.";
        ins.Column(1).Width = 110; ins.Range("A1:A8").Style.Alignment.WrapText = true;

        using var ms = new MemoryStream(); book.SaveAs(ms);
        return (ms.ToArray(), $"MPMS_Budget_Plan_{year}.xlsx");
    }

    public static async Task<object> ImportBudgetAsync(AppDbContext db, Stream stream, string fileName, bool dryRun, string mode)
    {
        using var book = new XLWorkbook(stream);
        if (!book.TryGetWorksheet("Budget Plan", out var ws))
            return await ImportLegacyBudgetAsync(db, book, fileName, dryRun, mode);

        var warnings = new List<string>();
        var rows = new List<BudgetRow>();
        var last = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= last; r++)
        {
            var name = ws.Cell(r, 4).GetString().Trim();
            var org = ws.Cell(r, 2).GetString().Trim();
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(org)) continue;
            var year = (int)Math.Round(CellDouble(ws.Cell(r, 1), warnings, $"Budget Plan!A{r}"));
            var amount = (long)Math.Round(CellDouble(ws.Cell(r, 7), warnings, $"Budget Plan!G{r}"));
            if (year < 2000 || string.IsNullOrWhiteSpace(org) || string.IsNullOrWhiteSpace(name))
            { warnings.Add($"Budget Plan row {r}: Year, Business Unit and Plan Item are required; skipped."); continue; }
            long? projectId = null;
            var projectCode = ws.Cell(r, 3).GetString().Trim();
            if (projectCode.Length > 0)
            {
                projectId = await db.Projects.Where(x => x.Code == projectCode).Select(x => (long?)x.Id).FirstOrDefaultAsync();
                if (!projectId.HasValue) warnings.Add($"Budget Plan row {r}: project code '{projectCode}' not found; item will be unlinked.");
            }
            var monthly = new Dictionary<string, double>();
            var monthNames = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            for (var m = 0; m < 12; m++) monthly[monthNames[m]] = CellDouble(ws.Cell(r, 13 + m), warnings, $"Budget Plan!{ws.Cell(r, 13 + m).Address}", false);
            rows.Add(new BudgetRow(year, org, projectId, name, ws.Cell(r, 5).GetString().Trim(), ws.Cell(r, 6).GetString().Trim(), amount,
                NullableDouble(ws.Cell(r, 8)), NullableLong(ws.Cell(r, 9)), ws.Cell(r, 10).GetString().Trim(), ws.Cell(r, 11).GetString().Trim(), ws.Cell(r, 12).GetString().Trim(), monthly, r));
        }
        if (dryRun) return new { dryRun = true, format = "MPMS", fileName, itemCount = rows.Count, years = rows.Select(x => x.Year).Distinct().OrderBy(x => x), totalPlanned = rows.Sum(x => x.Amount), warnings };

        var inserted = 0; var updated = 0;
        foreach (var row in rows)
        {
            BudgetPlanItem? item = null;
            if (!mode.Equals("append", StringComparison.OrdinalIgnoreCase))
                item = await db.BudgetPlanItems.FirstOrDefaultAsync(x => x.BudgetYear == row.Year && x.OrgUnit == row.OrgUnit && x.Name == row.Name);
            if (item is null)
            {
                item = new BudgetPlanItem { BudgetYear = row.Year, OrgUnit = row.OrgUnit, Name = row.Name, SourceSheet = $"EXCEL:{fileName}", SourceRow = row.SourceRow };
                db.BudgetPlanItems.Add(item); inserted++;
            }
            else updated++;
            item.ProjectId = row.ProjectId; item.Category = row.Category; item.Vendor = row.Vendor; item.PlannedAmount = row.Amount;
            item.Quantity = row.Quantity; item.UnitPrice = row.UnitPrice; item.PlannedMonth = row.PlannedMonth; item.Status = row.Status.Length > 0 ? row.Status : "PLANNED";
            item.Note = row.Note; item.MonthlyPlanJson = JsonSerializer.Serialize(row.Monthly); item.SourceSheet = $"EXCEL:{fileName}"; item.SourceRow = row.SourceRow;
        }
        await db.SaveChangesAsync();
        return new { dryRun = false, format = "MPMS", inserted, updated, warnings };
    }

    // Backward-compatible parser for the IT Budget 2026 workbook already used to seed MPMS.
    private static async Task<object> ImportLegacyBudgetAsync(AppDbContext db, XLWorkbook book, string fileName, bool dryRun, string mode)
    {
        var warnings = new List<string>();
        var rows = new List<BudgetRow>();
        AddSimpleLegacy(book, rows, "PDD", "PDD", 1, 3, 4, 5, 3, 1_000_000_000d);
        AddSimpleLegacy(book, rows, "DMD", "DMD", 1, 3, 5, 7, 3, 1_000_000_000d);
        AddSimpleLegacy(book, rows, "ACC_FIN", "Finance & Accounting", 2, 3, 1, 4, 3, 1_000_000_000d);
        AddSimpleLegacy(book, rows, "HR.ADM.LEGAL", "HR/Admin/Legal", 2, 5, 1, 6, 4, 1_000_000_000d);

        if (book.TryGetWorksheet("BFT.2026", out var bft))
        {
            var last = bft.LastRowUsed()?.RowNumber() ?? 1;
            for (var r = 1; r <= last; r++)
            {
                if (!bft.Cell(r, 3).IsEmpty() && bft.Cell(r, 11).TryGetValue<double>(out var amount))
                    rows.Add(new BudgetRow(2026, "BFT", null, JoinNonEmpty(bft.Cell(r, 2).GetString(), bft.Cell(r, 3).GetString()), bft.Cell(r, 4).GetString(), "", (long)Math.Round(amount), NullableDouble(bft.Cell(r, 6)), NullableLong(bft.Cell(r, 8)), bft.Cell(r, 12).GetString(), "PLANNED", bft.Cell(r, 13).GetString(), new(), r));
            }
        }
        if (book.TryGetWorksheet("GARDEN", out var garden))
        {
            var last = garden.LastRowUsed()?.RowNumber() ?? 1;
            var mn = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            for (var r = 3; r <= last; r++)
            {
                var name = garden.Cell(r, 1).GetString().Trim(); if (name.Length == 0 || name.StartsWith("A.")) continue;
                var amount = garden.Cell(r, 4).TryGetValue<double>(out var a) ? a : garden.Cell(r, 22).TryGetValue<double>(out var b) ? b : double.NaN;
                if (double.IsNaN(amount)) continue;
                var monthly = new Dictionary<string, double>(); for (var m = 0; m < 12; m++) monthly[mn[m]] = garden.Cell(r, 10 + m).TryGetValue<double>(out var v) ? v : 0;
                rows.Add(new BudgetRow(2026, "The Garden", null, name, "IT Budget", "", (long)Math.Round(amount), NullableDouble(garden.Cell(r, 2)), NullableLong(garden.Cell(r, 3)), garden.Cell(r, 6).GetString(), "PLANNED", garden.Cell(r, 5).GetString(), monthly, r));
            }
        }

        if (rows.Count == 0)
            return new { dryRun, format = "UNKNOWN", fileName, itemCount = 0, warnings = new[] { "No MPMS Budget Plan sheet and no supported legacy budget sheets were found." } };
        if (dryRun) return new { dryRun = true, format = "LEGACY_IT_BUDGET", fileName, itemCount = rows.Count, years = new[] { 2026 }, totalPlanned = rows.Sum(x => x.Amount), warnings };

        var inserted = 0; var updated = 0;
        foreach (var row in rows)
        {
            BudgetPlanItem? item = null;
            if (!mode.Equals("append", StringComparison.OrdinalIgnoreCase)) item = await db.BudgetPlanItems.FirstOrDefaultAsync(x => x.BudgetYear == row.Year && x.OrgUnit == row.OrgUnit && x.Name == row.Name);
            if (item is null) { item = new BudgetPlanItem { BudgetYear = row.Year, OrgUnit = row.OrgUnit, Name = row.Name }; db.BudgetPlanItems.Add(item); inserted++; } else updated++;
            item.Category = row.Category; item.Vendor = row.Vendor; item.PlannedAmount = row.Amount; item.Quantity = row.Quantity; item.UnitPrice = row.UnitPrice;
            item.PlannedMonth = row.PlannedMonth; item.Note = row.Note; item.Status = "PLANNED"; item.MonthlyPlanJson = JsonSerializer.Serialize(row.Monthly);
            item.SourceSheet = $"EXCEL:{fileName}"; item.SourceRow = row.SourceRow;
        }
        await db.SaveChangesAsync();
        return new { dryRun = false, format = "LEGACY_IT_BUDGET", inserted, updated, warnings };
    }

    private static void AddSimpleLegacy(XLWorkbook book, List<BudgetRow> rows, string sheetName, string org, int nameCol, int amountCol, int categoryCol, int vendorCol, int startRow, double multiplier)
    {
        if (!book.TryGetWorksheet(sheetName, out var ws)) return;
        var last = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = startRow; r <= last; r++)
        {
            var name = ws.Cell(r, nameCol).GetString().Trim(); if (name.Length == 0 || name.Contains("TỔNG", StringComparison.OrdinalIgnoreCase) || name.Contains("TOTAL", StringComparison.OrdinalIgnoreCase)) continue;
            if (!ws.Cell(r, amountCol).TryGetValue<double>(out var amount)) continue;
            rows.Add(new BudgetRow(2026, org, null, name, ws.Cell(r, categoryCol).GetString(), ws.Cell(r, vendorCol).GetString(), (long)Math.Round(amount * multiplier), null, null, "", "PLANNED", "", new(), r));
        }
    }

    private static bool LooksLikeIndividualKpi(IXLWorksheet ws) =>
        ws.Cell("A2").GetString().Contains("INDIVIDUAL PERFORMANCE", StringComparison.OrdinalIgnoreCase)
        || (ws.Cell("E5").GetString().Trim().Length > 0 && ws.Cell("C8").GetString().Contains("Plan", StringComparison.OrdinalIgnoreCase));

    private static async Task<AppUser?> FindUserByNameAsync(AppDbContext db, string name)
    {
        var target = Normalize(name);
        var users = await db.Users.AsNoTracking().ToListAsync();
        return users.FirstOrDefault(x => Normalize(x.Name) == target);
    }

    private static string Normalize(string value)
    {
        var form = (value ?? "").Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in form) if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(ch));
        return string.Join(' ', sb.ToString().Normalize(NormalizationForm.FormC).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string ParsePeriod(string raw)
    {
        var nums = System.Text.RegularExpressions.Regex.Matches(raw ?? "", @"\d+").Select(x => int.Parse(x.Value)).ToList();
        if (nums.Count >= 2)
        {
            var month = nums[0] > 12 ? nums[1] : nums[0]; var year = nums[0] > 12 ? nums[0] : nums[1];
            return $"{year:D4}-{month:D2}";
        }
        if (nums.Count == 1) return nums[0] > 1900 ? nums[0].ToString("D4") : $"{DateTime.Today.Year:D4}-{nums[0]:D2}";
        return "";
    }
    private static string FormatPeriod(string period) => period.Length >= 7 ? $"{period[5..7]}/{period[..4]}" : period;
    private static int FindTotalRow(IXLWorksheet ws, int start, int max)
    {
        for (var r = start; r <= max; r++) { var v = ws.Cell(r, 3).GetString(); if (v.Contains("TỔNG", StringComparison.OrdinalIgnoreCase) || v.Contains("TOTAL", StringComparison.OrdinalIgnoreCase)) return r; }
        return -1;
    }
    private static double CellDouble(IXLCell cell, List<string> warnings, string address, bool warn = true)
    {
        if (cell.TryGetValue<double>(out var v)) return v;
        var raw = cell.GetString().Trim(); if (raw.Length == 0) return 0;
        if (raw.EndsWith('%') && double.TryParse(raw.TrimEnd('%'), NumberStyles.Any, CultureInfo.InvariantCulture, out var p)) return p / 100d;
        if (warn) warnings.Add($"{address}: '{raw}' is not numeric; interpreted as 0.");
        return 0;
    }
    private static double? NullableDouble(IXLCell cell) => cell.TryGetValue<double>(out var v) ? v : null;
    private static long? NullableLong(IXLCell cell) => cell.TryGetValue<double>(out var v) ? (long)Math.Round(v) : null;
    private static Dictionary<string, double> ParseAllocations(string json)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, double>>(json ?? "{}") ?? new(); } catch { return new(); }
    }
    private static string SafeSheetName(string s)
    {
        foreach (var c in new[] { ':', '\\', '/', '?', '*', '[', ']' }) s = s.Replace(c, '-');
        return s.Length > 31 ? s[..31] : s;
    }
    private static string SafeFileName(string s) => string.Concat((s ?? "KPI").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
    private static string JoinNonEmpty(params string[] values) => string.Join(" / ", values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));

    private sealed record KpiRow(int SourceRow, string Function, string Plan, string Actual, double Weight, double SelfScore, double ManagerScore, double HodScore, string Note, Dictionary<string, double> Allocations);
    private sealed record KpiSheet(string SheetName, long UserId, string EmployeeName, string Department, string Period, List<KpiRow> Rows);
    private sealed record BudgetRow(int Year, string OrgUnit, long? ProjectId, string Name, string Category, string Vendor, long Amount, double? Quantity, long? UnitPrice, string PlannedMonth, string Status, string Note, Dictionary<string, double> Monthly, int SourceRow);
}
