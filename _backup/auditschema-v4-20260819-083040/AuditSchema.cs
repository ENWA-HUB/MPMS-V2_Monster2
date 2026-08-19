using System.Data.Common;
using MAIPT.PM.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MAIPT.PM.Api.Data;

public static class AuditSchema
{
    public static async Task EnsureAsync(AppDbContext db)
    {
        var provider = db.Database.ProviderName ?? "";
        var entities = db.Model.GetEntityTypes()
            .Where(e => typeof(AuditableEntity).IsAssignableFrom(e.ClrType))
            .Select(e => new { Table = e.GetTableName(), Schema = e.GetSchema() ?? "dbo" })
            .Where(x => !string.IsNullOrWhiteSpace(x.Table))
            .Distinct()
            .ToList();

        if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var e in entities)
            {
                var table = e.Table!.Replace("]", "]]");
                var schema = e.Schema.Replace("]", "]]");
                var full = $"[{schema}].[{table}]";
                var literal = $"{e.Schema}.{e.Table}".Replace("'", "''");
                await db.Database.ExecuteSqlRawAsync($@"
IF OBJECT_ID(N'{literal}', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'{literal}', 'CreatedByUserId') IS NULL
        ALTER TABLE {full} ADD [CreatedByUserId] BIGINT NULL;
    IF COL_LENGTH(N'{literal}', 'UpdatedByUserId') IS NULL
        ALTER TABLE {full} ADD [UpdatedByUserId] BIGINT NULL;
END;");
            }
        }
        else if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var connection = db.Database.GetDbConnection();
            var openedHere = connection.State != System.Data.ConnectionState.Open;
            if (openedHere) await connection.OpenAsync();
            try
            {
                foreach (var e in entities)
                {
                    var table = e.Table!;
                    if (!await TableExists(connection, table)) continue;
                    var cols = await Columns(connection, table);
                    var q = table.Replace(""", """");
                    if (!cols.Contains("CreatedByUserId"))
                        await Exec(connection, $"ALTER TABLE \"{q}\" ADD COLUMN \"CreatedByUserId\" INTEGER NULL;");
                    if (!cols.Contains("UpdatedByUserId"))
                        await Exec(connection, $"ALTER TABLE \"{q}\" ADD COLUMN \"UpdatedByUserId\" INTEGER NULL;");
                }
            }
            finally
            {
                if (openedHere) await connection.CloseAsync();
            }
        }
    }

    static async Task<bool> TableExists(DbConnection c, string table)
    {
        await using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name";
        var p = cmd.CreateParameter(); p.ParameterName = "@name"; p.Value = table; cmd.Parameters.Add(p);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync()) > 0;
    }

    static async Task<HashSet<string>> Columns(DbConnection c, string table)
    {
        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = c.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{table.Replace(""", """")}\")";
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) cols.Add(Convert.ToString(r["name"]) ?? "");
        return cols;
    }

    static async Task Exec(DbConnection c, string sql)
    {
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }
}
