using System.Text.Json;
using MAIPT.PM.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MAIPT.PM.Api.Data;

public static class PlanningSeedData
{
    private sealed class BudgetImportRow
    {
        public string SourceSheet { get; set; } = "";
        public int SourceRow { get; set; }
        public int BudgetYear { get; set; }
        public string OrgUnit { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string Vendor { get; set; } = "";
        public long PlannedAmount { get; set; }
        public double? Quantity { get; set; }
        public long? UnitPrice { get; set; }
        public string PlannedMonth { get; set; } = "";
        public string Note { get; set; } = "";
        public Dictionary<string,double> MonthlyPlan { get; set; } = new();
    }

    private sealed class PerformanceImport
    {
        public List<PerformancePeriodImport> Periods { get; set; } = new();
        public List<PerformanceItemImport> Items { get; set; } = new();
    }
    private sealed class PerformancePeriodImport
    {
        public string Key { get; set; } = "";
        public string Period { get; set; } = "";
        public string Level { get; set; } = "";
        public string Employee { get; set; } = "";
        public string Department { get; set; } = "";
        public string SourceSheet { get; set; } = "";
    }
    private sealed class PerformanceItemImport
    {
        public string PeriodKey { get; set; } = "";
        public int SourceRow { get; set; }
        public string Strategy { get; set; } = "";
        public string Function { get; set; } = "";
        public string Plan { get; set; } = "";
        public string Actual { get; set; } = "";
        public double Weight { get; set; }
        public double SelfScore { get; set; }
        public double ManagerScore { get; set; }
        public double HodScore { get; set; }
        public string NextPlan { get; set; } = "";
        public string Note { get; set; } = "";
        public Dictionary<string,double> Allocations { get; set; } = new();
    }

    public static async Task InitializeAsync(AppDbContext db, IWebHostEnvironment env)
    {
        await EnsureSchemaAsync(db);
        await SeedBudgetAsync(db, env);
        await SeedPerformanceAsync(db, env);
    }

    private static async Task EnsureSchemaAsync(AppDbContext db)
    {
        // EF Core already creates these tables for SQL Server.
        // The raw SQL below is SQLite-specific.
        if (db.Database.IsSqlServer())
            return;

        await db.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS BudgetPlanItems (
 Id INTEGER PRIMARY KEY AUTOINCREMENT, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL,
 BudgetYear INTEGER NOT NULL, OrgUnit TEXT NOT NULL, SourceSheet TEXT NOT NULL, SourceRow INTEGER NOT NULL,
 ProjectId INTEGER NULL, Name TEXT NOT NULL, Category TEXT NOT NULL, Vendor TEXT NOT NULL,
 PlannedAmount INTEGER NOT NULL, Quantity REAL NULL, UnitPrice INTEGER NULL, PlannedMonth TEXT NOT NULL,
 MonthlyPlanJson TEXT NOT NULL, Note TEXT NOT NULL, Status TEXT NOT NULL,
 FOREIGN KEY(ProjectId) REFERENCES Projects(Id) ON DELETE SET NULL
);
CREATE INDEX IF NOT EXISTS IX_BudgetPlanItems_Year_OrgUnit ON BudgetPlanItems(BudgetYear,OrgUnit);
CREATE INDEX IF NOT EXISTS IX_BudgetPlanItems_ProjectId ON BudgetPlanItems(ProjectId);

CREATE TABLE IF NOT EXISTS PerformancePeriods (
 Id INTEGER PRIMARY KEY AUTOINCREMENT, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL,
 PeriodKey TEXT NOT NULL UNIQUE, Period TEXT NOT NULL, Level TEXT NOT NULL, UserId INTEGER NULL,
 EmployeeName TEXT NOT NULL, Department TEXT NOT NULL, SourceSheet TEXT NOT NULL, Status TEXT NOT NULL,
 IsLocked INTEGER NOT NULL, LockedAt TEXT NULL,
 FOREIGN KEY(UserId) REFERENCES Users(Id) ON DELETE SET NULL
);
CREATE TABLE IF NOT EXISTS PerformanceItems (
 Id INTEGER PRIMARY KEY AUTOINCREMENT, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL,
 PerformancePeriodId INTEGER NOT NULL, ProjectId INTEGER NULL, SourceRow INTEGER NOT NULL,
 Strategy TEXT NOT NULL, Function TEXT NOT NULL, Plan TEXT NOT NULL, Actual TEXT NOT NULL,
 Weight REAL NOT NULL, SelfScore REAL NOT NULL, ManagerScore REAL NOT NULL, HodScore REAL NOT NULL,
 FinalScore REAL NOT NULL, NextPlan TEXT NOT NULL, Note TEXT NOT NULL, AllocationsJson TEXT NOT NULL, Status TEXT NOT NULL,
 FOREIGN KEY(PerformancePeriodId) REFERENCES PerformancePeriods(Id) ON DELETE CASCADE,
 FOREIGN KEY(ProjectId) REFERENCES Projects(Id) ON DELETE SET NULL
);
CREATE INDEX IF NOT EXISTS IX_PerformanceItems_Period_Project ON PerformanceItems(PerformancePeriodId,ProjectId);
""");
    }

    private static async Task SeedBudgetAsync(AppDbContext db, IWebHostEnvironment env)
    {
        if (await db.BudgetPlanItems.AnyAsync()) return;
        var path = Path.Combine(env.ContentRootPath, "Data", "Imports", "budget-plan-2026.json");
        if (!File.Exists(path)) return;
        var rows = JsonSerializer.Deserialize<List<BudgetImportRow>>(await File.ReadAllTextAsync(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        db.BudgetPlanItems.AddRange(rows.Select(r => new BudgetPlanItem
        {
            BudgetYear = r.BudgetYear, OrgUnit = r.OrgUnit, SourceSheet = r.SourceSheet, SourceRow = r.SourceRow,
            Name = r.Name, Category = r.Category, Vendor = r.Vendor, PlannedAmount = r.PlannedAmount,
            Quantity = r.Quantity, UnitPrice = r.UnitPrice, PlannedMonth = r.PlannedMonth,
            MonthlyPlanJson = JsonSerializer.Serialize(r.MonthlyPlan), Note = r.Note, Status = "PLANNED"
        }));
        await db.SaveChangesAsync();
    }

    private static async Task SeedPerformanceAsync(AppDbContext db, IWebHostEnvironment env)
    {
        if (await db.PerformancePeriods.AnyAsync()) return;
        var path = Path.Combine(env.ContentRootPath, "Data", "Imports", "performance-upf.json");
        if (!File.Exists(path)) return;
        var input = JsonSerializer.Deserialize<PerformanceImport>(await File.ReadAllTextAsync(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (input is null) return;

        var org = await db.OrgUnits.OrderBy(x => x.Id).FirstAsync();
        foreach (var person in input.Periods.Select(x => x.Employee).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
        {
            if (!await db.Users.AnyAsync(x => x.Name == person))
            {
                var slug = new string(person.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
                db.Users.Add(new AppUser { OrgUnitId = org.Id, Name = person, Email = $"{slug}@maipt.local", Department = "IT", Role = "MEMBER", Status = "ACTIVE" });
            }
        }
        await db.SaveChangesAsync();

        var periodMap = new Dictionary<string,PerformancePeriod>();
        foreach (var p in input.Periods)
        {
            var user = string.IsNullOrWhiteSpace(p.Employee) ? null : await db.Users.FirstOrDefaultAsync(x => x.Name == p.Employee);
            var row = new PerformancePeriod
            {
                PeriodKey = p.Key, Period = p.Period, Level = p.Level, UserId = user?.Id,
                EmployeeName = p.Employee, Department = p.Department, SourceSheet = p.SourceSheet,
                Status = "OPEN", IsLocked = false
            };
            db.PerformancePeriods.Add(row); periodMap[p.Key] = row;
        }
        await db.SaveChangesAsync();

        foreach (var i in input.Items)
        {
            if (!periodMap.TryGetValue(i.PeriodKey, out var period)) continue;
            var final = i.HodScore > 0 ? i.HodScore : i.ManagerScore > 0 ? i.ManagerScore : i.SelfScore;
            db.PerformanceItems.Add(new PerformanceItem
            {
                PerformancePeriodId = period.Id, SourceRow = i.SourceRow, Strategy = i.Strategy, Function = i.Function,
                Plan = i.Plan, Actual = i.Actual, Weight = i.Weight, SelfScore = i.SelfScore,
                ManagerScore = i.ManagerScore, HodScore = i.HodScore, FinalScore = final,
                NextPlan = i.NextPlan, Note = i.Note, AllocationsJson = JsonSerializer.Serialize(i.Allocations), Status = "OPEN"
            });
        }
        await db.SaveChangesAsync();
    }
}
