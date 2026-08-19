using System.Data;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;

var sqlitePath = "/Users/maipt/Documents/MPMS-V2/backend/MAIPT.PM.Api/data/maipt-pm.db";

Console.Write("SQL Server password: ");
var password = ReadPassword();
Console.WriteLine();

var sqlServer =
    $"Server=tcp:db64317.public.databaseasp.net,1433;" +
    $"Database=db64317;User Id=db64317;Password={password};" +
    $"Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Connect Timeout=60";

var tables = new[]
{
    "OrgUnits",
    "Users",
    "AuthAccounts",
    "AuthSessions",
    "Categories",
    "Portfolios",
    "Projects",
    "ProjectMembers",
    "Milestones",
    "Suppliers",
    "Contracts",
    "Deliverables",
    "BudgetLines",
    "BudgetTransactions",
    "Risks",
    "Issues",
    "ChangeRequests",
    "Documents",
    "KpiCriteria",
    "SupplierEvaluations",
    "KpiScores",
    "ApprovalRequests",
    "ApprovalSteps",
    "Notifications",
    "ActivityLogs",
    "BudgetPlanItems",
    "PerformancePeriods",
    "PerformanceItems",
    "Tasks"
};

await using var sqlite = new SqliteConnection($"Data Source={sqlitePath};Mode=ReadOnly");
await using var sql = new SqlConnection(sqlServer);

await sqlite.OpenAsync();
await sql.OpenAsync();

Console.WriteLine($"SQLite : {sqlitePath}");
Console.WriteLine($"SQL    : {sql.DataSource} / {sql.Database}");
Console.WriteLine();

var sourceTables = await GetSqliteTables(sqlite);
var targetTables = await GetSqlServerTables(sql);

var selected = tables
    .Where(t => sourceTables.Contains(t, StringComparer.OrdinalIgnoreCase)
             && targetTables.Contains(t, StringComparer.OrdinalIgnoreCase))
    .ToList();

Console.WriteLine($"Tables to migrate: {selected.Count}");
foreach (var t in selected) Console.WriteLine($"  - {t}");

Console.WriteLine();
Console.Write("This will REPLACE data in the SQL Server tables above. Type MIGRATE to continue: ");
if (!string.Equals(Console.ReadLine(), "MIGRATE", StringComparison.Ordinal))
{
    Console.WriteLine("Cancelled.");
    return;
}

await using var tx = (SqlTransaction)await sql.BeginTransactionAsync();

try
{
    Console.WriteLine("\n[1/5] Disabling SQL Server constraints...");
    foreach (var table in selected)
        await Exec(sql, tx, $"ALTER TABLE {Q(table)} NOCHECK CONSTRAINT ALL;");

    Console.WriteLine("[2/5] Clearing SQL Server data...");
    foreach (var table in selected.AsEnumerable().Reverse())
        await Exec(sql, tx, $"DELETE FROM {Q(table)};");

    Console.WriteLine("[3/5] Copying rows...");
    var results = new List<(string Table, long Source, long Target)>();

    foreach (var table in selected)
    {
        var sourceCols = await GetSqliteColumns(sqlite, table);
        var targetCols = await GetSqlServerColumns(sql, tx, table);

        var common = sourceCols
            .Where(c => targetCols.ContainsKey(c))
            .ToList();

        if (common.Count == 0)
        {
            Console.WriteLine($"  SKIP {table}: no matching columns.");
            continue;
        }

        var identityCol = await GetIdentityColumn(sql, tx, table);
        var sourceCount = await CountSqlite(sqlite, table);

        if (sourceCount == 0)
        {
            results.Add((table, 0, 0));
            Console.WriteLine($"  {table,-24} 0 rows");
            continue;
        }

        if (identityCol is not null && common.Contains(identityCol, StringComparer.OrdinalIgnoreCase))
            await Exec(sql, tx, $"SET IDENTITY_INSERT {Q(table)} ON;");

        var selectSql = $"SELECT {string.Join(", ", common.Select(QSqlite))} FROM {QSqlite(table)};";
        await using var srcCmd = sqlite.CreateCommand();
        srcCmd.CommandText = selectSql;
        await using var reader = await srcCmd.ExecuteReaderAsync();

        var insertSql =
            $"INSERT INTO {Q(table)} ({string.Join(", ", common.Select(Q))}) " +
            $"VALUES ({string.Join(", ", common.Select((_, i) => $"@p{i}"))});";

        await using var ins = new SqlCommand(insertSql, sql, tx);
        for (var i = 0; i < common.Count; i++)
            ins.Parameters.Add(new SqlParameter($"@p{i}", DBNull.Value));

        long copied = 0;
        while (await reader.ReadAsync())
        {
            for (var i = 0; i < common.Count; i++)
            {
                var raw = reader.IsDBNull(i) ? null : reader.GetValue(i);
                var targetType = targetCols[common[i]];
                ins.Parameters[i].Value = ConvertForSqlServer(raw, targetType) ?? DBNull.Value;
            }

            await ins.ExecuteNonQueryAsync();
            copied++;
        }

        if (identityCol is not null && common.Contains(identityCol, StringComparer.OrdinalIgnoreCase))
        {
            await Exec(sql, tx, $"SET IDENTITY_INSERT {Q(table)} OFF;");
            await Exec(sql, tx,
                $"DECLARE @m BIGINT=(SELECT ISNULL(MAX({Q(identityCol)}),0) FROM {Q(table)}); " +
                $"DBCC CHECKIDENT ({SqlLiteral(table)}, RESEED, @m) WITH NO_INFOMSGS;");
        }

        var targetCount = await CountSqlServer(sql, tx, table);
        results.Add((table, sourceCount, targetCount));

        var ok = sourceCount == targetCount ? "OK" : "MISMATCH";
        Console.WriteLine($"  {table,-24} {sourceCount,6} -> {targetCount,6}  {ok}");
    }

    Console.WriteLine("[4/5] Re-enabling and validating constraints...");
    foreach (var table in selected)
        await Exec(sql, tx, $"ALTER TABLE {Q(table)} WITH CHECK CHECK CONSTRAINT ALL;");

    Console.WriteLine("[5/5] Final verification...");
    var mismatches = results.Where(x => x.Source != x.Target).ToList();
    if (mismatches.Count > 0)
        throw new Exception("Row-count mismatch: " +
            string.Join(", ", mismatches.Select(x => $"{x.Table} {x.Source}!={x.Target}")));

    await tx.CommitAsync();

    Console.WriteLine("\n==========================================");
    Console.WriteLine("MIGRATION COMPLETED SUCCESSFULLY");
    Console.WriteLine("==========================================");
    Console.WriteLine($"Tables : {results.Count}");
    Console.WriteLine($"Rows   : {results.Sum(x => x.Target)}");
    Console.WriteLine();

    foreach (var name in new[] { "Users", "AuthAccounts", "OrgUnits", "Categories", "Projects", "BudgetPlanItems", "PerformancePeriods" })
    {
        var r = results.FirstOrDefault(x => x.Table.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(r.Table))
            Console.WriteLine($"{r.Table,-22}: {r.Target}");
    }
}
catch
{
    await tx.RollbackAsync();
    Console.WriteLine("\nERROR: Migration rolled back. SQL Server data was not partially committed.");
    throw;
}

static async Task<HashSet<string>> GetSqliteTables(SqliteConnection c)
{
    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    await using var cmd = c.CreateCommand();
    cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync()) set.Add(r.GetString(0));
    return set;
}

static async Task<HashSet<string>> GetSqlServerTables(SqlConnection c)
{
    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    await using var cmd = c.CreateCommand();
    cmd.CommandText = "SELECT name FROM sys.tables WHERE is_ms_shipped=0;";
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync()) set.Add(r.GetString(0));
    return set;
}

static async Task<List<string>> GetSqliteColumns(SqliteConnection c, string table)
{
    var list = new List<string>();
    await using var cmd = c.CreateCommand();
    cmd.CommandText = $"PRAGMA table_info({QSqlite(table)});";
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync()) list.Add(r.GetString(1));
    return list;
}

static async Task<Dictionary<string,string>> GetSqlServerColumns(SqlConnection c, SqlTransaction tx, string table)
{
    var d = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    await using var cmd = new SqlCommand(@"
SELECT c.name, t.name
FROM sys.columns c
JOIN sys.types t ON c.user_type_id=t.user_type_id
JOIN sys.tables tb ON c.object_id=tb.object_id
WHERE tb.name=@table;", c, tx);
    cmd.Parameters.AddWithValue("@table", table);
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync()) d[r.GetString(0)] = r.GetString(1);
    return d;
}

static async Task<string?> GetIdentityColumn(SqlConnection c, SqlTransaction tx, string table)
{
    await using var cmd = new SqlCommand(@"
SELECT c.name
FROM sys.identity_columns c
JOIN sys.tables t ON c.object_id=t.object_id
WHERE t.name=@table;", c, tx);
    cmd.Parameters.AddWithValue("@table", table);
    var v = await cmd.ExecuteScalarAsync();
    return v?.ToString();
}

static async Task<long> CountSqlite(SqliteConnection c, string table)
{
    await using var cmd = c.CreateCommand();
    cmd.CommandText = $"SELECT COUNT(*) FROM {QSqlite(table)};";
    return Convert.ToInt64(await cmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
}

static async Task<long> CountSqlServer(SqlConnection c, SqlTransaction tx, string table)
{
    await using var cmd = new SqlCommand($"SELECT COUNT_BIG(*) FROM {Q(table)};", c, tx);
    return Convert.ToInt64(await cmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
}

static async Task Exec(SqlConnection c, SqlTransaction tx, string sqlText)
{
    await using var cmd = new SqlCommand(sqlText, c, tx);
    cmd.CommandTimeout = 120;
    await cmd.ExecuteNonQueryAsync();
}

static object? ConvertForSqlServer(object? value, string sqlType)
{
    if (value is null || value is DBNull) return null;

    sqlType = sqlType.ToLowerInvariant();

    if (sqlType == "bit")
    {
        if (value is long l) return l != 0;
        if (value is int i) return i != 0;
        if (bool.TryParse(value.ToString(), out var b)) return b;
        if (long.TryParse(value.ToString(), out var n)) return n != 0;
    }

    if (sqlType is "bigint" or "int" or "smallint" or "tinyint")
    {
        if (value is long or int or short or byte) return value;
        if (long.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var n))
            return n;
    }

    if (sqlType is "float" or "real" or "decimal" or "numeric" or "money" or "smallmoney")
    {
        if (value is double or float or decimal) return value;
        if (decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return d;
    }

    if (sqlType is "date" or "datetime" or "datetime2" or "smalldatetime")
    {
        if (value is DateTime dt) return dt;
        if (DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out var parsed))
            return parsed;
    }

    return value;
}

static string Q(string name) => $"[{name.Replace("]", "]]")}]";
static string QSqlite(string name) => $"\"{name.Replace("\"", "\"\"")}\"";
static string SqlLiteral(string value) => "N'" + value.Replace("'", "''") + "'";

static string ReadPassword()
{
    var chars = new List<char>();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) break;
        if (key.Key == ConsoleKey.Backspace)
        {
            if (chars.Count > 0) chars.RemoveAt(chars.Count - 1);
            continue;
        }
        chars.Add(key.KeyChar);
    }
    return new string(chars.ToArray());
}
