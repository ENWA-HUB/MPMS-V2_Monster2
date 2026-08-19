using System.Data;
using System.Data.Common;
using MAIPT.PM.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MAIPT.PM.Api.Data;

public static class AuditSchema
{
    public static async Task EnsureAsync(AppDbContext db)
    {
        var provider = db.Database.ProviderName ?? string.Empty;

        var entities = db.Model.GetEntityTypes()
            .Where(e => typeof(AuditableEntity).IsAssignableFrom(e.ClrType))
            .Select(e => new
            {
                Table = e.GetTableName(),
                Schema = e.GetSchema() ?? "dbo"
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Table))
            .Distinct()
            .ToList();

        if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var e in entities)
            {
                var table = e.Table!;
                var schema = e.Schema;

                var fullName = "[" + EscapeSqlServerIdentifier(schema) + "].[" +
                               EscapeSqlServerIdentifier(table) + "]";
                var objectName = schema + "." + table;

                var sql =
                    "IF OBJECT_ID(N'" + EscapeSqlLiteral(objectName) + "', N'U') IS NOT NULL\n" +
                    "BEGIN\n" +
                    "    IF COL_LENGTH(N'" + EscapeSqlLiteral(objectName) + "', 'CreatedByUserId') IS NULL\n" +
                    "        ALTER TABLE " + fullName + " ADD [CreatedByUserId] BIGINT NULL;\n" +
                    "    IF COL_LENGTH(N'" + EscapeSqlLiteral(objectName) + "', 'UpdatedByUserId') IS NULL\n" +
                    "        ALTER TABLE " + fullName + " ADD [UpdatedByUserId] BIGINT NULL;\n" +
                    "END;";

                await db.Database.ExecuteSqlRawAsync(sql);
            }

            return;
        }

        if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var connection = db.Database.GetDbConnection();
            var openedHere = connection.State != ConnectionState.Open;

            if (openedHere)
                await connection.OpenAsync();

            try
            {
                foreach (var e in entities)
                {
                    var table = e.Table!;
                    if (!await SqliteTableExists(connection, table))
                        continue;

                    var columns = await SqliteColumns(connection, table);
                    var quotedTable = QuoteSqliteIdentifier(table);

                    if (!columns.Contains("CreatedByUserId"))
                    {
                        await Execute(
                            connection,
                            "ALTER TABLE " + quotedTable +
                            " ADD COLUMN \"CreatedByUserId\" INTEGER NULL;");
                    }

                    if (!columns.Contains("UpdatedByUserId"))
                    {
                        await Execute(
                            connection,
                            "ALTER TABLE " + quotedTable +
                            " ADD COLUMN \"UpdatedByUserId\" INTEGER NULL;");
                    }
                }
            }
            finally
            {
                if (openedHere)
                    await connection.CloseAsync();
            }
        }
    }

    private static string EscapeSqlServerIdentifier(string value)
        => value.Replace("]", "]]");

    private static string EscapeSqlLiteral(string value)
        => value.Replace("'", "''");

    private static string QuoteSqliteIdentifier(string value)
        => "\"" + value.Replace("\"", "\"\"") + "\"";

    private static async Task<bool> SqliteTableExists(DbConnection connection, string table)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@name";
        parameter.Value = table;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result) > 0;
    }

    private static async Task<HashSet<string>> SqliteColumns(
        DbConnection connection,
        string table)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(" + QuoteSqliteIdentifier(table) + ");";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = Convert.ToString(reader["name"]);
            if (!string.IsNullOrWhiteSpace(name))
                columns.Add(name);
        }

        return columns;
    }

    private static async Task Execute(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
