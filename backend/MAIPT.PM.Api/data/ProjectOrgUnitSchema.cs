using Microsoft.EntityFrameworkCore;
namespace MAIPT.PM.Api.Data;
public static class ProjectOrgUnitSchema
{
    public static async Task EnsureAsync(AppDbContext db)
    {
        var provider=db.Database.ProviderName??"";
        if(provider.Contains("SqlServer",StringComparison.OrdinalIgnoreCase))
        {
            await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[dbo].[ProjectOrgUnits]',N'U') IS NULL
BEGIN
 CREATE TABLE [dbo].[ProjectOrgUnits](
  [Id] BIGINT IDENTITY(1,1) PRIMARY KEY,
  [ProjectId] BIGINT NOT NULL,[OrgUnitId] BIGINT NOT NULL,[IsPrimary] BIT NOT NULL DEFAULT(0),
  [CreatedByUserId] BIGINT NULL,[UpdatedByUserId] BIGINT NULL,
  [CreatedAt] DATETIME2 NOT NULL DEFAULT(GETUTCDATE()),[UpdatedAt] DATETIME2 NOT NULL DEFAULT(GETUTCDATE()),
  CONSTRAINT [FK_ProjectOrgUnits_Project] FOREIGN KEY([ProjectId]) REFERENCES [dbo].[Projects]([Id]),
  CONSTRAINT [FK_ProjectOrgUnits_OrgUnit] FOREIGN KEY([OrgUnitId]) REFERENCES [dbo].[OrgUnits]([Id])
 );
 CREATE UNIQUE INDEX [IX_ProjectOrgUnits_ProjectId_OrgUnitId] ON [dbo].[ProjectOrgUnits]([ProjectId],[OrgUnitId]);
 CREATE INDEX [IX_ProjectOrgUnits_OrgUnitId_ProjectId] ON [dbo].[ProjectOrgUnits]([OrgUnitId],[ProjectId]);
END;
INSERT INTO [dbo].[ProjectOrgUnits]([ProjectId],[OrgUnitId],[IsPrimary],[CreatedAt],[UpdatedAt])
SELECT p.[Id],p.[OrgUnitId],1,GETUTCDATE(),GETUTCDATE() FROM [dbo].[Projects] p
WHERE NOT EXISTS(SELECT 1 FROM [dbo].[ProjectOrgUnits] x WHERE x.[ProjectId]=p.[Id] AND x.[OrgUnitId]=p.[OrgUnitId]);
");
        }
        else if(provider.Contains("Sqlite",StringComparison.OrdinalIgnoreCase))
        {
            await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS ProjectOrgUnits(
 Id INTEGER PRIMARY KEY AUTOINCREMENT,ProjectId INTEGER NOT NULL,OrgUnitId INTEGER NOT NULL,IsPrimary INTEGER NOT NULL DEFAULT 0,
 CreatedByUserId INTEGER NULL,UpdatedByUserId INTEGER NULL,CreatedAt TEXT NOT NULL,UpdatedAt TEXT NOT NULL,
 FOREIGN KEY(ProjectId) REFERENCES Projects(Id),FOREIGN KEY(OrgUnitId) REFERENCES OrgUnits(Id));
CREATE UNIQUE INDEX IF NOT EXISTS IX_ProjectOrgUnits_ProjectId_OrgUnitId ON ProjectOrgUnits(ProjectId,OrgUnitId);
CREATE INDEX IF NOT EXISTS IX_ProjectOrgUnits_OrgUnitId_ProjectId ON ProjectOrgUnits(OrgUnitId,ProjectId);
INSERT OR IGNORE INTO ProjectOrgUnits(ProjectId,OrgUnitId,IsPrimary,CreatedAt,UpdatedAt)
SELECT Id,OrgUnitId,1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP FROM Projects;
");
        }
    }
}
