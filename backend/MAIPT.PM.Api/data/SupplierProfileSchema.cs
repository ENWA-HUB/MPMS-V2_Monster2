using Microsoft.EntityFrameworkCore;
namespace MAIPT.PM.Api.Data;

public static class SupplierProfileSchema
{
    public static async Task EnsureAsync(AppDbContext db)
    {
        if(!db.Database.IsSqlServer()) return;
        await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'dbo.Suppliers',N'U') IS NOT NULL
BEGIN
 IF COL_LENGTH(N'dbo.Suppliers','CompanyName') IS NULL ALTER TABLE dbo.Suppliers ADD CompanyName NVARCHAR(500) NOT NULL DEFAULT N'';
 IF COL_LENGTH(N'dbo.Suppliers','ShortName') IS NULL ALTER TABLE dbo.Suppliers ADD ShortName NVARCHAR(255) NOT NULL DEFAULT N'';
 IF COL_LENGTH(N'dbo.Suppliers','Website') IS NULL ALTER TABLE dbo.Suppliers ADD Website NVARCHAR(1000) NOT NULL DEFAULT N'';
 IF COL_LENGTH(N'dbo.Suppliers','BusinessRegistrationNo') IS NULL ALTER TABLE dbo.Suppliers ADD BusinessRegistrationNo NVARCHAR(255) NOT NULL DEFAULT N'';
 IF COL_LENGTH(N'dbo.Suppliers','LegalRepresentative') IS NULL ALTER TABLE dbo.Suppliers ADD LegalRepresentative NVARCHAR(500) NOT NULL DEFAULT N'';
 IF COL_LENGTH(N'dbo.Suppliers','RegistrationDate') IS NULL ALTER TABLE dbo.Suppliers ADD RegistrationDate DATE NULL;
 IF COL_LENGTH(N'dbo.Suppliers','Country') IS NULL ALTER TABLE dbo.Suppliers ADD Country NVARCHAR(255) NOT NULL DEFAULT N'Vietnam';
 IF COL_LENGTH(N'dbo.Suppliers','ContactPosition') IS NULL ALTER TABLE dbo.Suppliers ADD ContactPosition NVARCHAR(255) NOT NULL DEFAULT N'';
 IF COL_LENGTH(N'dbo.Suppliers','AlternativePhone') IS NULL ALTER TABLE dbo.Suppliers ADD AlternativePhone NVARCHAR(100) NOT NULL DEFAULT N'';
 IF COL_LENGTH(N'dbo.Suppliers','ProvinceCity') IS NULL ALTER TABLE dbo.Suppliers ADD ProvinceCity NVARCHAR(255) NOT NULL DEFAULT N'';
 IF COL_LENGTH(N'dbo.Suppliers','PaymentTerms') IS NULL ALTER TABLE dbo.Suppliers ADD PaymentTerms NVARCHAR(500) NOT NULL DEFAULT N'';
 IF COL_LENGTH(N'dbo.Suppliers','Currency') IS NULL ALTER TABLE dbo.Suppliers ADD Currency NVARCHAR(20) NOT NULL DEFAULT N'VND';
 IF COL_LENGTH(N'dbo.Suppliers','BankName') IS NULL ALTER TABLE dbo.Suppliers ADD BankName NVARCHAR(500) NOT NULL DEFAULT N'';
 IF COL_LENGTH(N'dbo.Suppliers','BankAccountNo') IS NULL ALTER TABLE dbo.Suppliers ADD BankAccountNo NVARCHAR(255) NOT NULL DEFAULT N'';
 IF COL_LENGTH(N'dbo.Suppliers','BankAccountName') IS NULL ALTER TABLE dbo.Suppliers ADD BankAccountName NVARCHAR(500) NOT NULL DEFAULT N'';
 IF COL_LENGTH(N'dbo.Suppliers','BankBranch') IS NULL ALTER TABLE dbo.Suppliers ADD BankBranch NVARCHAR(500) NOT NULL DEFAULT N'';
 IF COL_LENGTH(N'dbo.Suppliers','InternalOwnerId') IS NULL ALTER TABLE dbo.Suppliers ADD InternalOwnerId BIGINT NULL;
 IF COL_LENGTH(N'dbo.Suppliers','Notes') IS NULL ALTER TABLE dbo.Suppliers ADD Notes NVARCHAR(MAX) NOT NULL DEFAULT N'';
END");
    }
}
