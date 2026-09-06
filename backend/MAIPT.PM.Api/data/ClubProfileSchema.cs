using Microsoft.EntityFrameworkCore;

namespace MAIPT.PM.Api.Data;

public static class ClubProfileSchema
{
    public static async Task EnsureAsync(AppDbContext db)
    {
        if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'dbo.PersonalFcClubProfiles',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.PersonalFcClubProfiles(
  Id BIGINT IDENTITY PRIMARY KEY,
  ClubId BIGINT NOT NULL,
  About NVARCHAR(MAX) NOT NULL DEFAULT N'',
  Mission NVARCHAR(MAX) NOT NULL DEFAULT N'',
  Vision NVARCHAR(MAX) NOT NULL DEFAULT N'',
  CoreValues NVARCHAR(MAX) NOT NULL DEFAULT N'',
  FoundedDate DATE NULL,
  ContactEmail NVARCHAR(320) NOT NULL DEFAULT N'',
  ContactPhone NVARCHAR(100) NOT NULL DEFAULT N'',
  Website NVARCHAR(1000) NOT NULL DEFAULT N'',
  SocialLink NVARCHAR(1000) NOT NULL DEFAULT N'',
  MainVenue NVARCHAR(1000) NOT NULL DEFAULT N'',
  RegulationsSummary NVARCHAR(MAX) NOT NULL DEFAULT N'',
  ActivitiesSummary NVARCHAR(MAX) NOT NULL DEFAULT N'',
  UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
 );
 CREATE UNIQUE INDEX UX_PersonalFcClubProfiles_ClubId ON dbo.PersonalFcClubProfiles(ClubId);
END
""");
        }
        else
        {
            await db.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS PersonalFcClubProfiles(
 Id INTEGER PRIMARY KEY AUTOINCREMENT,
 ClubId INTEGER NOT NULL UNIQUE,
 About TEXT NOT NULL DEFAULT '',
 Mission TEXT NOT NULL DEFAULT '',
 Vision TEXT NOT NULL DEFAULT '',
 CoreValues TEXT NOT NULL DEFAULT '',
 FoundedDate TEXT NULL,
 ContactEmail TEXT NOT NULL DEFAULT '',
 ContactPhone TEXT NOT NULL DEFAULT '',
 Website TEXT NOT NULL DEFAULT '',
 SocialLink TEXT NOT NULL DEFAULT '',
 MainVenue TEXT NOT NULL DEFAULT '',
 RegulationsSummary TEXT NOT NULL DEFAULT '',
 ActivitiesSummary TEXT NOT NULL DEFAULT '',
 UpdatedAt TEXT NOT NULL
);
""");
        }
    }
}
