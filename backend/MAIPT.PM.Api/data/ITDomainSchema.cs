using Microsoft.EntityFrameworkCore;
namespace MAIPT.PM.Api.Data;
public static class ITDomainSchema
{
 public static async Task EnsureAsync(AppDbContext db)
 {
  if(!db.Database.IsSqlServer()) return;
  await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'dbo.ITDomains',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.ITDomains(
  Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
  OrgUnitId BIGINT NULL,SupplierId BIGINT NULL,ContractId BIGINT NULL,ManagerUserId BIGINT NULL,
  DomainName NVARCHAR(255) NOT NULL,DomainType NVARCHAR(50) NOT NULL DEFAULT N'INTERNATIONAL',
  Purpose NVARCHAR(500) NOT NULL DEFAULT N'',Registrar NVARCHAR(255) NOT NULL DEFAULT N'',
  RegistrarUrl NVARCHAR(1000) NOT NULL DEFAULT N'',LoginEmail NVARCHAR(320) NOT NULL DEFAULT N'',
  LoginPasswordProtected NVARCHAR(MAX) NOT NULL DEFAULT N'',RegistrationDate DATE NULL,ExpiryDate DATE NULL,
  RenewalStatus NVARCHAR(50) NOT NULL DEFAULT N'MANUAL',AutoRenew BIT NOT NULL DEFAULT 0,WhoisPrivacy BIT NOT NULL DEFAULT 0,
  DnsProvider NVARCHAR(255) NOT NULL DEFAULT N'',NameServers NVARCHAR(MAX) NOT NULL DEFAULT N'',
  RegistrantOrganization NVARCHAR(500) NOT NULL DEFAULT N'',RegistrantContact NVARCHAR(500) NOT NULL DEFAULT N'',
  AdminContact NVARCHAR(500) NOT NULL DEFAULT N'',TechnicalContact NVARCHAR(500) NOT NULL DEFAULT N'',
  DeclarationNo NVARCHAR(255) NOT NULL DEFAULT N'',DeclarationDate DATE NULL,
  RecoveryEmail NVARCHAR(320) NOT NULL DEFAULT N'',MfaMethod NVARCHAR(255) NOT NULL DEFAULT N'',
  Status NVARCHAR(50) NOT NULL DEFAULT N'ACTIVE',Notes NVARCHAR(MAX) NOT NULL DEFAULT N'',
  CreatedByUserId BIGINT NULL,UpdatedByUserId BIGINT NULL,
  CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
 );
 CREATE UNIQUE INDEX UX_ITDomains_DomainName ON dbo.ITDomains(DomainName);
 CREATE INDEX IX_ITDomains_ExpiryDate ON dbo.ITDomains(ExpiryDate);
END
""");
 }
}
