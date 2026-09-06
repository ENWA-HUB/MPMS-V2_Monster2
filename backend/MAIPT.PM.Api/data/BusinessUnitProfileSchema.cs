using MAIPT.PM.Api.Data;
using Microsoft.EntityFrameworkCore;

public static class BusinessUnitProfileSchema
{
    public static async Task EnsureAsync(AppDbContext db)
    {
        var cols = new Dictionary<string,string>
        {
            ["UnitType"]="NVARCHAR(50) NULL",
            ["ParentOrgUnitId"]="BIGINT NULL",
            ["ShortName"]="NVARCHAR(200) NULL",
            ["InternationalName"]="NVARCHAR(300) NULL",
            ["Description"]="NVARCHAR(MAX) NULL",
            ["LegalType"]="NVARCHAR(200) NULL",
            ["TaxCode"]="NVARCHAR(100) NULL",
            ["TaxIssueDate"]="DATETIME2 NULL",
            ["RegistrationNo"]="NVARCHAR(100) NULL",
            ["RegistrationIssueDate"]="DATETIME2 NULL",
            ["IncorporationDate"]="DATETIME2 NULL",
            ["LegalRepresentative"]="NVARCHAR(250) NULL",
            ["RepresentativeTitle"]="NVARCHAR(200) NULL",
            ["RegisteredAddress"]="NVARCHAR(1000) NULL",
            ["OfficeAddress"]="NVARCHAR(1000) NULL",
            ["Phone"]="NVARCHAR(100) NULL",
            ["Email"]="NVARCHAR(250) NULL",
            ["Website"]="NVARCHAR(500) NULL",
            ["HeadName"]="NVARCHAR(250) NULL",
            ["FinanceContact"]="NVARCHAR(250) NULL",
            ["ITContact"]="NVARCHAR(250) NULL",
            ["HRContact"]="NVARCHAR(250) NULL",
            ["DefaultCurrency"]="NVARCHAR(20) NULL",
            ["FiscalYear"]="NVARCHAR(50) NULL",
            ["CostCenter"]="NVARCHAR(100) NULL",
            ["CompanyCode"]="NVARCHAR(100) NULL"
        };

        if (db.Database.IsSqlServer())
        {
            foreach (var c in cols)
                await db.Database.ExecuteSqlRawAsync(
                    $@"IF COL_LENGTH(N'dbo.OrgUnits',N'{c.Key}') IS NULL ALTER TABLE [dbo].[OrgUnits] ADD [{c.Key}] {c.Value};");
        }
        else if (db.Database.IsSqlite())
        {
            // Mirror every column from `cols` so SQLite dev databases do not drift
            // from the SQL Server schema (missing columns caused runtime 500s).
            foreach (var key in cols.Keys)
            {
                var type = key.EndsWith("Id") ? "INTEGER"
                    : (key.EndsWith("Date") ? "TEXT" : "TEXT");
                try { await db.Database.ExecuteSqlRawAsync($@"ALTER TABLE OrgUnits ADD COLUMN ""{key}"" {type} NULL;"); }
                catch { }
            }
        }
    }
}
