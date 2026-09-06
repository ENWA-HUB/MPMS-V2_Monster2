using Microsoft.EntityFrameworkCore;
namespace MAIPT.PM.Api.Data;
public static class ClubTournamentSchema {
 public static async Task EnsureAsync(AppDbContext db) {
  if(db.Database.IsSqlServer()) await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH('dbo.PersonalFcClubMembers','MemberCode') IS NULL ALTER TABLE dbo.PersonalFcClubMembers ADD MemberCode NVARCHAR(50) NOT NULL DEFAULT N'';
IF COL_LENGTH('dbo.PersonalFcClubMembers','CellPhone') IS NULL ALTER TABLE dbo.PersonalFcClubMembers ADD CellPhone NVARCHAR(50) NOT NULL DEFAULT N'';
IF COL_LENGTH('dbo.PersonalFcClubMembers','Sex') IS NULL ALTER TABLE dbo.PersonalFcClubMembers ADD Sex NVARCHAR(30) NOT NULL DEFAULT N'';
IF COL_LENGTH('dbo.PersonalFcClubMembers','SkillRank') IS NULL ALTER TABLE dbo.PersonalFcClubMembers ADD SkillRank NVARCHAR(50) NOT NULL DEFAULT N'';
IF COL_LENGTH('dbo.PersonalFcClubMembers','BirthDate') IS NULL ALTER TABLE dbo.PersonalFcClubMembers ADD BirthDate DATE NULL;
IF COL_LENGTH('dbo.PersonalFcClubMembers','RegistrationStatus') IS NULL ALTER TABLE dbo.PersonalFcClubMembers ADD RegistrationStatus NVARCHAR(30) NOT NULL DEFAULT N'APPROVED';
IF COL_LENGTH('dbo.PersonalFcClubMembers','MonthlyFeeAmount') IS NULL ALTER TABLE dbo.PersonalFcClubMembers ADD MonthlyFeeAmount DECIMAL(19,2) NOT NULL DEFAULT 0;
IF COL_LENGTH('dbo.PersonalFcClubMembers','RecurringFeeEnabled') IS NULL ALTER TABLE dbo.PersonalFcClubMembers ADD RecurringFeeEnabled BIT NOT NULL DEFAULT 0;
IF COL_LENGTH('dbo.PersonalFcClubMembers','RecurringFeeDay') IS NULL ALTER TABLE dbo.PersonalFcClubMembers ADD RecurringFeeDay INT NOT NULL DEFAULT 1;
IF COL_LENGTH('dbo.PersonalFcClubMembers','Active') IS NULL ALTER TABLE dbo.PersonalFcClubMembers ADD Active BIT NOT NULL DEFAULT 1;
IF COL_LENGTH('dbo.PersonalFcClubMembers','Notes') IS NULL ALTER TABLE dbo.PersonalFcClubMembers ADD Notes NVARCHAR(MAX) NOT NULL DEFAULT N'';

-- CLUB_MEMBER_CODE_UNIQUE_V1
-- Existing duplicate rows are left untouched for manual review. The API blocks new duplicates immediately;
-- the unique index is created automatically once existing duplicates have been resolved.
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'UX_PersonalFcClubMembers_Club_MemberCode' AND object_id=OBJECT_ID(N'dbo.PersonalFcClubMembers'))
AND NOT EXISTS(
 SELECT 1 FROM dbo.PersonalFcClubMembers
 WHERE LTRIM(RTRIM(MemberCode))<>N''
 GROUP BY ClubId,UPPER(LTRIM(RTRIM(MemberCode)))
 HAVING COUNT(*)>1
)
CREATE UNIQUE INDEX UX_PersonalFcClubMembers_Club_MemberCode
ON dbo.PersonalFcClubMembers(ClubId,MemberCode)
WHERE MemberCode<>N'';

IF OBJECT_ID(N'dbo.PersonalFcTournaments',N'U') IS NULL CREATE TABLE dbo.PersonalFcTournaments(Id BIGINT IDENTITY PRIMARY KEY,ClubId BIGINT NOT NULL,Code NVARCHAR(80) NOT NULL,Name NVARCHAR(250) NOT NULL,TournamentDate DATE NULL,Season NVARCHAR(80) NOT NULL DEFAULT N'',Status NVARCHAR(30) NOT NULL DEFAULT N'DRAFT',Format NVARCHAR(50) NOT NULL DEFAULT N'GROUP_ROUND_ROBIN',Venue NVARCHAR(250) NOT NULL DEFAULT N'',Notes NVARCHAR(MAX) NOT NULL DEFAULT N'',CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());
IF OBJECT_ID(N'dbo.PersonalFcTournamentRegistrations',N'U') IS NULL CREATE TABLE dbo.PersonalFcTournamentRegistrations(Id BIGINT IDENTITY PRIMARY KEY,TournamentId BIGINT NOT NULL,MemberId BIGINT NULL,FullName NVARCHAR(200) NOT NULL,Company NVARCHAR(250) NOT NULL DEFAULT N'',Department NVARCHAR(200) NOT NULL DEFAULT N'',Phone NVARCHAR(60) NOT NULL DEFAULT N'',BirthDate DATE NULL,BirthYear INT NULL,Sex NVARCHAR(50) NOT NULL DEFAULT N'',SkillRank NVARCHAR(100) NOT NULL DEFAULT N'',DivisionName NVARCHAR(120) NOT NULL DEFAULT N'',GroupName NVARCHAR(120) NOT NULL DEFAULT N'',ShirtSize NVARCHAR(20) NOT NULL DEFAULT N'',Notes NVARCHAR(MAX) NOT NULL DEFAULT N'',RegistrationStatus NVARCHAR(30) NOT NULL DEFAULT N'REGISTERED',OrderNo INT NULL);
IF OBJECT_ID(N'dbo.PersonalFcTournamentTeams',N'U') IS NULL CREATE TABLE dbo.PersonalFcTournamentTeams(Id BIGINT IDENTITY PRIMARY KEY,TournamentId BIGINT NOT NULL,DivisionName NVARCHAR(120) NOT NULL DEFAULT N'',GroupName NVARCHAR(120) NOT NULL DEFAULT N'',TeamCode NVARCHAR(80) NOT NULL DEFAULT N'',TeamName NVARCHAR(200) NOT NULL DEFAULT N'',Registration1Id BIGINT NULL,Registration2Id BIGINT NULL,Member1Id BIGINT NULL,Member2Id BIGINT NULL,Status NVARCHAR(30) NOT NULL DEFAULT N'ACTIVE');
IF OBJECT_ID(N'dbo.PersonalFcTournamentMatches',N'U') IS NULL CREATE TABLE dbo.PersonalFcTournamentMatches(Id BIGINT IDENTITY PRIMARY KEY,TournamentId BIGINT NOT NULL,DivisionName NVARCHAR(120) NOT NULL DEFAULT N'',GroupName NVARCHAR(120) NOT NULL DEFAULT N'',RoundName NVARCHAR(80) NOT NULL DEFAULT N'GROUP',SequenceNo INT NOT NULL DEFAULT 0,CourtNo NVARCHAR(50) NOT NULL DEFAULT N'',Team1Id BIGINT NULL,Team2Id BIGINT NULL,Score1 INT NULL,Score2 INT NULL,WinnerTeamId BIGINT NULL,Status NVARCHAR(30) NOT NULL DEFAULT N'SCHEDULED',ScheduledAt DATETIME2 NULL,Notes NVARCHAR(MAX) NOT NULL DEFAULT N'');
""");
  else {
   var alters=new[]{
    "ALTER TABLE PersonalFcClubMembers ADD COLUMN MemberCode TEXT NOT NULL DEFAULT ''",
    "ALTER TABLE PersonalFcClubMembers ADD COLUMN CellPhone TEXT NOT NULL DEFAULT ''",
    "ALTER TABLE PersonalFcClubMembers ADD COLUMN Sex TEXT NOT NULL DEFAULT ''",
    "ALTER TABLE PersonalFcClubMembers ADD COLUMN SkillRank TEXT NOT NULL DEFAULT ''",
    "ALTER TABLE PersonalFcClubMembers ADD COLUMN BirthDate TEXT NULL",
    "ALTER TABLE PersonalFcClubMembers ADD COLUMN RegistrationStatus TEXT NOT NULL DEFAULT 'APPROVED'",
    "ALTER TABLE PersonalFcClubMembers ADD COLUMN MonthlyFeeAmount NUMERIC NOT NULL DEFAULT 0",
    "ALTER TABLE PersonalFcClubMembers ADD COLUMN RecurringFeeEnabled INTEGER NOT NULL DEFAULT 0",
    "ALTER TABLE PersonalFcClubMembers ADD COLUMN RecurringFeeDay INTEGER NOT NULL DEFAULT 1",
    "ALTER TABLE PersonalFcClubMembers ADD COLUMN Active INTEGER NOT NULL DEFAULT 1",
    "ALTER TABLE PersonalFcClubMembers ADD COLUMN Notes TEXT NOT NULL DEFAULT ''"};
   foreach(var q in alters){try{await db.Database.ExecuteSqlRawAsync(q);}catch{}}
   try{await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS UX_PersonalFcClubMembers_Club_MemberCode ON PersonalFcClubMembers(ClubId, MemberCode COLLATE NOCASE) WHERE TRIM(MemberCode) <> ''");}catch{}
   await db.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS PersonalFcTournaments(Id INTEGER PRIMARY KEY AUTOINCREMENT,ClubId INTEGER NOT NULL,Code TEXT NOT NULL,Name TEXT NOT NULL,TournamentDate TEXT NULL,Season TEXT NOT NULL DEFAULT '',Status TEXT NOT NULL DEFAULT 'DRAFT',Format TEXT NOT NULL DEFAULT 'GROUP_ROUND_ROBIN',Venue TEXT NOT NULL DEFAULT '',Notes TEXT NOT NULL DEFAULT '',CreatedAt TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS PersonalFcTournamentRegistrations(Id INTEGER PRIMARY KEY AUTOINCREMENT,TournamentId INTEGER NOT NULL,MemberId INTEGER NULL,FullName TEXT NOT NULL,Company TEXT NOT NULL DEFAULT '',Department TEXT NOT NULL DEFAULT '',Phone TEXT NOT NULL DEFAULT '',BirthDate TEXT NULL,BirthYear INTEGER NULL,Sex TEXT NOT NULL DEFAULT '',SkillRank TEXT NOT NULL DEFAULT '',DivisionName TEXT NOT NULL DEFAULT '',GroupName TEXT NOT NULL DEFAULT '',ShirtSize TEXT NOT NULL DEFAULT '',Notes TEXT NOT NULL DEFAULT '',RegistrationStatus TEXT NOT NULL DEFAULT 'REGISTERED',OrderNo INTEGER NULL);
CREATE TABLE IF NOT EXISTS PersonalFcTournamentTeams(Id INTEGER PRIMARY KEY AUTOINCREMENT,TournamentId INTEGER NOT NULL,DivisionName TEXT NOT NULL DEFAULT '',GroupName TEXT NOT NULL DEFAULT '',TeamCode TEXT NOT NULL DEFAULT '',TeamName TEXT NOT NULL DEFAULT '',Registration1Id INTEGER NULL,Registration2Id INTEGER NULL,Member1Id INTEGER NULL,Member2Id INTEGER NULL,Status TEXT NOT NULL DEFAULT 'ACTIVE');
CREATE TABLE IF NOT EXISTS PersonalFcTournamentMatches(Id INTEGER PRIMARY KEY AUTOINCREMENT,TournamentId INTEGER NOT NULL,DivisionName TEXT NOT NULL DEFAULT '',GroupName TEXT NOT NULL DEFAULT '',RoundName TEXT NOT NULL DEFAULT 'GROUP',SequenceNo INTEGER NOT NULL DEFAULT 0,CourtNo TEXT NOT NULL DEFAULT '',Team1Id INTEGER NULL,Team2Id INTEGER NULL,Score1 INTEGER NULL,Score2 INTEGER NULL,WinnerTeamId INTEGER NULL,Status TEXT NOT NULL DEFAULT 'SCHEDULED',ScheduledAt TEXT NULL,Notes TEXT NOT NULL DEFAULT '');
""");
  }
 }
}
