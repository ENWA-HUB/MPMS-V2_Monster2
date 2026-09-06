using Microsoft.EntityFrameworkCore;

namespace MAIPT.PM.Api.Data;

public static class ITAssetSchema
{
    public static async Task EnsureAsync(AppDbContext db)
    {
        if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'[ITAssets]', N'U') IS NULL
BEGIN
 CREATE TABLE [ITAssets](
  [Id] BIGINT IDENTITY(1,1) PRIMARY KEY,[AssetCode] NVARCHAR(128) NOT NULL,[Category] NVARCHAR(64) NOT NULL,[AssetName] NVARCHAR(255) NOT NULL,
  [Brand] NVARCHAR(128) NOT NULL DEFAULT '',[Model] NVARCHAR(128) NOT NULL DEFAULT '',[SerialNumber] NVARCHAR(255) NOT NULL DEFAULT '',
  [Specification] NVARCHAR(MAX) NOT NULL DEFAULT '',[SupplierId] BIGINT NULL,[ContractId] BIGINT NULL,[ProjectId] BIGINT NULL,[BudgetLineId] BIGINT NULL,
  [PurchaseDate] DATE NULL,[PurchasePrice] BIGINT NOT NULL DEFAULT 0,[Currency] NVARCHAR(16) NOT NULL DEFAULT 'VND',[WarrantyExpiry] DATE NULL,
  [Location] NVARCHAR(255) NOT NULL DEFAULT '',[Department] NVARCHAR(255) NOT NULL DEFAULT '',[Condition] NVARCHAR(64) NOT NULL DEFAULT 'GOOD',
  [Status] NVARCHAR(64) NOT NULL DEFAULT 'IN_STOCK',[Notes] NVARCHAR(MAX) NOT NULL DEFAULT '',
  [CreatedByUserId] BIGINT NULL,[UpdatedByUserId] BIGINT NULL,[CreatedAt] DATETIME2 NOT NULL,[UpdatedAt] DATETIME2 NOT NULL
 );
 CREATE UNIQUE INDEX [IX_ITAssets_AssetCode] ON [ITAssets]([AssetCode]);
 CREATE INDEX [IX_ITAssets_Status_Category] ON [ITAssets]([Status],[Category]);
END;

IF OBJECT_ID(N'[ITAssetAssignments]', N'U') IS NULL
BEGIN
 CREATE TABLE [ITAssetAssignments](
  [Id] BIGINT IDENTITY(1,1) PRIMARY KEY,[AssetId] BIGINT NOT NULL,[UserId] BIGINT NOT NULL,[AssignmentType] NVARCHAR(32) NOT NULL DEFAULT 'ASSIGN',
  [AssignmentDate] DATE NOT NULL,[ReturnDate] DATE NULL,[FromLocation] NVARCHAR(255) NOT NULL DEFAULT '',[ToLocation] NVARCHAR(255) NOT NULL DEFAULT '',
  [ConditionOut] NVARCHAR(64) NOT NULL DEFAULT 'GOOD',[ConditionIn] NVARCHAR(64) NOT NULL DEFAULT '',[Accessories] NVARCHAR(MAX) NOT NULL DEFAULT '',
  [HandoverNo] NVARCHAR(128) NOT NULL DEFAULT '',[Notes] NVARCHAR(MAX) NOT NULL DEFAULT '',[Status] NVARCHAR(64) NOT NULL DEFAULT 'ACTIVE',
  [CreatedByUserId] BIGINT NULL,[UpdatedByUserId] BIGINT NULL,[CreatedAt] DATETIME2 NOT NULL,[UpdatedAt] DATETIME2 NOT NULL
 );
 CREATE INDEX [IX_ITAssetAssignments_AssetId_Status] ON [ITAssetAssignments]([AssetId],[Status]);
 CREATE INDEX [IX_ITAssetAssignments_UserId_Status] ON [ITAssetAssignments]([UserId],[Status]);
END;

IF OBJECT_ID(N'[ITAssetAssignments]', N'U') IS NOT NULL
BEGIN
 IF COL_LENGTH(N'dbo.ITAssetAssignments','ReceivedByUserId') IS NULL ALTER TABLE [dbo].[ITAssetAssignments] ADD [ReceivedByUserId] BIGINT NULL;
 IF COL_LENGTH(N'dbo.ITAssetAssignments','ReturnReason') IS NULL ALTER TABLE [dbo].[ITAssetAssignments] ADD [ReturnReason] NVARCHAR(255) NOT NULL CONSTRAINT [DF_ITAssetAssignments_ReturnReason] DEFAULT '';
 IF COL_LENGTH(N'dbo.ITAssetAssignments','ReturnLocation') IS NULL ALTER TABLE [dbo].[ITAssetAssignments] ADD [ReturnLocation] NVARCHAR(255) NOT NULL CONSTRAINT [DF_ITAssetAssignments_ReturnLocation] DEFAULT '';
 IF COL_LENGTH(N'dbo.ITAssetAssignments','AccessoriesReturned') IS NULL ALTER TABLE [dbo].[ITAssetAssignments] ADD [AccessoriesReturned] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_ITAssetAssignments_AccessoriesReturned] DEFAULT '';
 IF COL_LENGTH(N'dbo.ITAssetAssignments','MissingItems') IS NULL ALTER TABLE [dbo].[ITAssetAssignments] ADD [MissingItems] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_ITAssetAssignments_MissingItems] DEFAULT '';
END;

IF OBJECT_ID(N'[ITLicenses]', N'U') IS NULL
BEGIN
 CREATE TABLE [ITLicenses](
  [Id] BIGINT IDENTITY(1,1) PRIMARY KEY,[Code] NVARCHAR(128) NOT NULL,[ProductName] NVARCHAR(255) NOT NULL,[Vendor] NVARCHAR(255) NOT NULL DEFAULT '',
  [LicenseType] NVARCHAR(64) NOT NULL DEFAULT 'SUBSCRIPTION',[Quantity] INT NOT NULL DEFAULT 0,[AssignedQuantity] INT NOT NULL DEFAULT 0,
  [StartDate] DATE NULL,[ExpiryDate] DATE NULL,[Cost] BIGINT NOT NULL DEFAULT 0,[Currency] NVARCHAR(16) NOT NULL DEFAULT 'VND',
  [SupplierId] BIGINT NULL,[ContractId] BIGINT NULL,[AutoRenew] BIT NOT NULL DEFAULT 0,[Status] NVARCHAR(64) NOT NULL DEFAULT 'ACTIVE',
  [Notes] NVARCHAR(MAX) NOT NULL DEFAULT '',[CreatedByUserId] BIGINT NULL,[UpdatedByUserId] BIGINT NULL,[CreatedAt] DATETIME2 NOT NULL,[UpdatedAt] DATETIME2 NOT NULL
 );
 CREATE UNIQUE INDEX [IX_ITLicenses_Code] ON [ITLicenses]([Code]);
END;

IF OBJECT_ID(N'[ITServices]', N'U') IS NULL
BEGIN
 CREATE TABLE [ITServices](
  [Id] BIGINT IDENTITY(1,1) PRIMARY KEY,[Code] NVARCHAR(128) NOT NULL,[ServiceType] NVARCHAR(64) NOT NULL,[ServiceName] NVARCHAR(255) NOT NULL,
  [Provider] NVARCHAR(255) NOT NULL DEFAULT '',[Site] NVARCHAR(255) NOT NULL DEFAULT '',[Department] NVARCHAR(255) NOT NULL DEFAULT '',
  [SupplierId] BIGINT NULL,[ContractId] BIGINT NULL,[StartDate] DATE NULL,[ExpiryDate] DATE NULL,[MonthlyCost] BIGINT NOT NULL DEFAULT 0,
  [AnnualCost] BIGINT NOT NULL DEFAULT 0,[Currency] NVARCHAR(16) NOT NULL DEFAULT 'VND',[Status] NVARCHAR(64) NOT NULL DEFAULT 'ACTIVE',
  [Notes] NVARCHAR(MAX) NOT NULL DEFAULT '',[CreatedByUserId] BIGINT NULL,[UpdatedByUserId] BIGINT NULL,[CreatedAt] DATETIME2 NOT NULL,[UpdatedAt] DATETIME2 NOT NULL
 );
 CREATE UNIQUE INDEX [IX_ITServices_Code] ON [ITServices]([Code]);
END;

IF OBJECT_ID(N'[ITMaintenances]', N'U') IS NULL
BEGIN
 CREATE TABLE [ITMaintenances](
  [Id] BIGINT IDENTITY(1,1) PRIMARY KEY,[AssetId] BIGINT NOT NULL,[Type] NVARCHAR(64) NOT NULL,[OpenDate] DATE NULL,[CloseDate] DATE NULL,
  [Vendor] NVARCHAR(255) NOT NULL DEFAULT '',[Description] NVARCHAR(MAX) NOT NULL DEFAULT '',[Cost] BIGINT NOT NULL DEFAULT 0,
  [Currency] NVARCHAR(16) NOT NULL DEFAULT 'VND',[Status] NVARCHAR(64) NOT NULL DEFAULT 'OPEN',[Notes] NVARCHAR(MAX) NOT NULL DEFAULT '',
  [CreatedByUserId] BIGINT NULL,[UpdatedByUserId] BIGINT NULL,[CreatedAt] DATETIME2 NOT NULL,[UpdatedAt] DATETIME2 NOT NULL
 );
 CREATE INDEX [IX_ITMaintenances_AssetId_Status] ON [ITMaintenances]([AssetId],[Status]);
END;
""");
        }
        else
        {
            await db.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS ITAssets(
 Id INTEGER PRIMARY KEY AUTOINCREMENT,AssetCode TEXT NOT NULL UNIQUE,Category TEXT NOT NULL,AssetName TEXT NOT NULL,Brand TEXT NOT NULL DEFAULT '',
 Model TEXT NOT NULL DEFAULT '',SerialNumber TEXT NOT NULL DEFAULT '',Specification TEXT NOT NULL DEFAULT '',SupplierId INTEGER NULL,ContractId INTEGER NULL,
 ProjectId INTEGER NULL,BudgetLineId INTEGER NULL,PurchaseDate TEXT NULL,PurchasePrice INTEGER NOT NULL DEFAULT 0,Currency TEXT NOT NULL DEFAULT 'VND',
 WarrantyExpiry TEXT NULL,Location TEXT NOT NULL DEFAULT '',Department TEXT NOT NULL DEFAULT '',Condition TEXT NOT NULL DEFAULT 'GOOD',
 Status TEXT NOT NULL DEFAULT 'IN_STOCK',Notes TEXT NOT NULL DEFAULT '',CreatedByUserId INTEGER NULL,UpdatedByUserId INTEGER NULL,CreatedAt TEXT NOT NULL,UpdatedAt TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS IX_ITAssets_Status_Category ON ITAssets(Status,Category);

CREATE TABLE IF NOT EXISTS ITAssetAssignments(
 Id INTEGER PRIMARY KEY AUTOINCREMENT,AssetId INTEGER NOT NULL,UserId INTEGER NOT NULL,AssignmentType TEXT NOT NULL DEFAULT 'ASSIGN',
 AssignmentDate TEXT NOT NULL,ReturnDate TEXT NULL,FromLocation TEXT NOT NULL DEFAULT '',ToLocation TEXT NOT NULL DEFAULT '',
 ConditionOut TEXT NOT NULL DEFAULT 'GOOD',ConditionIn TEXT NOT NULL DEFAULT '',Accessories TEXT NOT NULL DEFAULT '',HandoverNo TEXT NOT NULL DEFAULT '',
 Notes TEXT NOT NULL DEFAULT '',Status TEXT NOT NULL DEFAULT 'ACTIVE',CreatedByUserId INTEGER NULL,UpdatedByUserId INTEGER NULL,CreatedAt TEXT NOT NULL,UpdatedAt TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS IX_ITAssetAssignments_AssetId_Status ON ITAssetAssignments(AssetId,Status);
CREATE INDEX IF NOT EXISTS IX_ITAssetAssignments_UserId_Status ON ITAssetAssignments(UserId,Status);

CREATE TABLE IF NOT EXISTS ITLicenses(
 Id INTEGER PRIMARY KEY AUTOINCREMENT,Code TEXT NOT NULL UNIQUE,ProductName TEXT NOT NULL,Vendor TEXT NOT NULL DEFAULT '',LicenseType TEXT NOT NULL DEFAULT 'SUBSCRIPTION',
 Quantity INTEGER NOT NULL DEFAULT 0,AssignedQuantity INTEGER NOT NULL DEFAULT 0,StartDate TEXT NULL,ExpiryDate TEXT NULL,Cost INTEGER NOT NULL DEFAULT 0,
 Currency TEXT NOT NULL DEFAULT 'VND',SupplierId INTEGER NULL,ContractId INTEGER NULL,AutoRenew INTEGER NOT NULL DEFAULT 0,Status TEXT NOT NULL DEFAULT 'ACTIVE',
 Notes TEXT NOT NULL DEFAULT '',CreatedByUserId INTEGER NULL,UpdatedByUserId INTEGER NULL,CreatedAt TEXT NOT NULL,UpdatedAt TEXT NOT NULL);

CREATE TABLE IF NOT EXISTS ITServices(
 Id INTEGER PRIMARY KEY AUTOINCREMENT,Code TEXT NOT NULL UNIQUE,ServiceType TEXT NOT NULL,ServiceName TEXT NOT NULL,Provider TEXT NOT NULL DEFAULT '',
 Site TEXT NOT NULL DEFAULT '',Department TEXT NOT NULL DEFAULT '',SupplierId INTEGER NULL,ContractId INTEGER NULL,StartDate TEXT NULL,ExpiryDate TEXT NULL,
 MonthlyCost INTEGER NOT NULL DEFAULT 0,AnnualCost INTEGER NOT NULL DEFAULT 0,Currency TEXT NOT NULL DEFAULT 'VND',Status TEXT NOT NULL DEFAULT 'ACTIVE',
 Notes TEXT NOT NULL DEFAULT '',CreatedByUserId INTEGER NULL,UpdatedByUserId INTEGER NULL,CreatedAt TEXT NOT NULL,UpdatedAt TEXT NOT NULL);

CREATE TABLE IF NOT EXISTS ITMaintenances(
 Id INTEGER PRIMARY KEY AUTOINCREMENT,AssetId INTEGER NOT NULL,Type TEXT NOT NULL,OpenDate TEXT NULL,CloseDate TEXT NULL,Vendor TEXT NOT NULL DEFAULT '',
 Description TEXT NOT NULL DEFAULT '',Cost INTEGER NOT NULL DEFAULT 0,Currency TEXT NOT NULL DEFAULT 'VND',Status TEXT NOT NULL DEFAULT 'OPEN',
 Notes TEXT NOT NULL DEFAULT '',CreatedByUserId INTEGER NULL,UpdatedByUserId INTEGER NULL,CreatedAt TEXT NOT NULL,UpdatedAt TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS IX_ITMaintenances_AssetId_Status ON ITMaintenances(AssetId,Status);
""");
        }
    

        // IT_FULL_DETAIL_COLUMNS_V1_1
        if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync("IF COL_LENGTH('ITAssets','OrgUnitId') IS NULL ALTER TABLE ITAssets ADD OrgUnitId BIGINT NULL");
            await db.Database.ExecuteSqlRawAsync("IF COL_LENGTH('ITLicenses','AssignedUserId') IS NULL ALTER TABLE ITLicenses ADD AssignedUserId BIGINT NULL");
            await db.Database.ExecuteSqlRawAsync("IF COL_LENGTH('ITLicenses','AssignedAssetId') IS NULL ALTER TABLE ITLicenses ADD AssignedAssetId BIGINT NULL");
        }
        else if (db.Database.IsSqlite())
        {
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE ITAssets ADD COLUMN OrgUnitId INTEGER NULL"); } catch { }
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE ITLicenses ADD COLUMN AssignedUserId INTEGER NULL"); } catch { }
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE ITLicenses ADD COLUMN AssignedAssetId INTEGER NULL"); } catch { }
        }

        // LICENSE_COLUMNS_V1_1
        if (db.Database.IsSqlServer())
        {
        await db.Database.ExecuteSqlRawAsync("IF COL_LENGTH('ITLicenses','Licensee') IS NULL ALTER TABLE ITLicenses ADD Licensee nvarchar(300) NULL");
        await db.Database.ExecuteSqlRawAsync("IF COL_LENGTH('ITLicenses','LicenseKey') IS NULL ALTER TABLE ITLicenses ADD LicenseKey nvarchar(1000) NULL");
        await db.Database.ExecuteSqlRawAsync("IF COL_LENGTH('ITLicenses','LicenseType') IS NULL ALTER TABLE ITLicenses ADD LicenseType nvarchar(100) NULL");
        await db.Database.ExecuteSqlRawAsync("IF COL_LENGTH('ITLicenses','OrgUnitId') IS NULL ALTER TABLE ITLicenses ADD OrgUnitId bigint NULL");
        await db.Database.ExecuteSqlRawAsync("IF COL_LENGTH('ITLicenses','ActivationStatus') IS NULL ALTER TABLE ITLicenses ADD ActivationStatus nvarchar(80) NULL");
        await db.Database.ExecuteSqlRawAsync("IF COL_LENGTH('ITLicenses','ActivationDate') IS NULL ALTER TABLE ITLicenses ADD ActivationDate date NULL");
        }
        else if (db.Database.IsSqlite())
        {
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE ITLicenses ADD COLUMN Licensee TEXT NULL"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE ITLicenses ADD COLUMN LicenseKey TEXT NULL"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE ITLicenses ADD COLUMN LicenseType TEXT NULL"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE ITLicenses ADD COLUMN OrgUnitId INTEGER NULL"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE ITLicenses ADD COLUMN ActivationStatus TEXT NULL"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE ITLicenses ADD COLUMN ActivationDate TEXT NULL"); } catch { }
        }


        // ITASSET_ASSIGNMENT_COLUMNS_V1
        if (db.Database.IsSqlServer())
        {
            // STRICT_IT_BU_SCOPE_V2_1
            await db.Database.ExecuteSqlRawAsync("IF COL_LENGTH('dbo.ITAssets','OrgUnitId') IS NULL ALTER TABLE [dbo].[ITAssets] ADD [OrgUnitId] BIGINT NULL;");
            await db.Database.ExecuteSqlRawAsync("IF COL_LENGTH('dbo.ITServices','OrgUnitId') IS NULL ALTER TABLE [dbo].[ITServices] ADD [OrgUnitId] BIGINT NULL;");

            await db.Database.ExecuteSqlRawAsync("UPDATE a SET a.OrgUnitId=o.Id FROM dbo.ITAssets a JOIN dbo.OrgUnits o ON (o.Code=a.Department OR o.Name=a.Department) WHERE a.OrgUnitId IS NULL AND NULLIF(LTRIM(RTRIM(a.Department)),'') IS NOT NULL;");
            await db.Database.ExecuteSqlRawAsync("UPDATE a SET a.OrgUnitId=p.OrgUnitId FROM dbo.ITAssets a JOIN dbo.Projects p ON p.Id=a.ProjectId WHERE a.OrgUnitId IS NULL AND a.ProjectId IS NOT NULL;");
            await db.Database.ExecuteSqlRawAsync("UPDATE a SET a.OrgUnitId=u.OrgUnitId FROM dbo.ITAssets a JOIN dbo.Users u ON u.Id=a.CreatedByUserId WHERE a.OrgUnitId IS NULL AND a.CreatedByUserId IS NOT NULL;");
            await db.Database.ExecuteSqlRawAsync("UPDATE s SET s.OrgUnitId=o.Id FROM dbo.ITServices s JOIN dbo.OrgUnits o ON (o.Code=s.Department OR o.Name=s.Department) WHERE s.OrgUnitId IS NULL AND NULLIF(LTRIM(RTRIM(s.Department)),'') IS NOT NULL;");
            await db.Database.ExecuteSqlRawAsync("UPDATE s SET s.OrgUnitId=u.OrgUnitId FROM dbo.ITServices s JOIN dbo.Users u ON u.Id=s.CreatedByUserId WHERE s.OrgUnitId IS NULL AND s.CreatedByUserId IS NOT NULL;");
            await db.Database.ExecuteSqlRawAsync("UPDATE l SET l.OrgUnitId=u.OrgUnitId FROM dbo.ITLicenses l JOIN dbo.Users u ON u.Id=l.CreatedByUserId WHERE l.OrgUnitId IS NULL AND l.CreatedByUserId IS NOT NULL;");
        }

        // STRICT_IT_BU_SCOPE_V2
        if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('ITAssets','OrgUnitId') IS NULL ALTER TABLE ITAssets ADD OrgUnitId BIGINT NULL;
IF COL_LENGTH('ITServices','OrgUnitId') IS NULL ALTER TABLE ITServices ADD OrgUnitId BIGINT NULL;

UPDATE a SET a.OrgUnitId=o.Id
FROM ITAssets a JOIN OrgUnits o ON (o.Code=a.Department OR o.Name=a.Department)
WHERE a.OrgUnitId IS NULL AND NULLIF(LTRIM(RTRIM(a.Department)),'') IS NOT NULL;

UPDATE a SET a.OrgUnitId=p.OrgUnitId
FROM ITAssets a JOIN Projects p ON p.Id=a.ProjectId
WHERE a.OrgUnitId IS NULL AND a.ProjectId IS NOT NULL;

UPDATE a SET a.OrgUnitId=u.OrgUnitId
FROM ITAssets a JOIN Users u ON u.Id=a.CreatedByUserId
WHERE a.OrgUnitId IS NULL AND a.CreatedByUserId IS NOT NULL;

UPDATE s SET s.OrgUnitId=o.Id
FROM ITServices s JOIN OrgUnits o ON (o.Code=s.Department OR o.Name=s.Department)
WHERE s.OrgUnitId IS NULL AND NULLIF(LTRIM(RTRIM(s.Department)),'') IS NOT NULL;

UPDATE s SET s.OrgUnitId=u.OrgUnitId
FROM ITServices s JOIN Users u ON u.Id=s.CreatedByUserId
WHERE s.OrgUnitId IS NULL AND s.CreatedByUserId IS NOT NULL;

UPDATE l SET l.OrgUnitId=u.OrgUnitId
FROM ITLicenses l JOIN Users u ON u.Id=l.CreatedByUserId
WHERE l.OrgUnitId IS NULL AND l.CreatedByUserId IS NOT NULL;
");
        }
}
}
