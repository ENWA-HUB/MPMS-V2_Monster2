using Microsoft.EntityFrameworkCore;

namespace MAIPT.PM.Api.Data;

public static class M365StorageSchema
{
    public static async Task EnsureAsync(AppDbContext db)
    {
        if(db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'dbo.Documents',N'U') IS NOT NULL
BEGIN
 IF COL_LENGTH(N'dbo.Documents','StorageProvider') IS NULL
   ALTER TABLE dbo.Documents ADD StorageProvider NVARCHAR(30) NOT NULL CONSTRAINT DF_Documents_StorageProvider DEFAULT N'LOCAL';
 IF COL_LENGTH(N'dbo.Documents','StorageDriveId') IS NULL
   ALTER TABLE dbo.Documents ADD StorageDriveId NVARCHAR(500) NULL;
 IF COL_LENGTH(N'dbo.Documents','StorageItemId') IS NULL
   ALTER TABLE dbo.Documents ADD StorageItemId NVARCHAR(500) NULL;
 IF COL_LENGTH(N'dbo.Documents','StorageWebUrl') IS NULL
   ALTER TABLE dbo.Documents ADD StorageWebUrl NVARCHAR(MAX) NULL;
 IF COL_LENGTH(N'dbo.Documents','StoragePath') IS NULL
   ALTER TABLE dbo.Documents ADD StoragePath NVARCHAR(2000) NULL;
END");
        }
    }
}
