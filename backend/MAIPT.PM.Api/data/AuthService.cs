using System.Security.Cryptography;
using System.Text;
using MAIPT.PM.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MAIPT.PM.Api.Data;

public static class AuthService
{
    public const string CookieName = "mpms_session";
    public static bool IsManagerRole(string? role)
    {
        var r = (role ?? "").ToUpperInvariant();
        return r is "ADMIN" or "CIO" or "HOD" or "PM" or "PROJECT_MANAGER";
    }

    public static string HashPassword(string password, string saltBase64, int iterations)
    {
        var salt = Convert.FromBase64String(saltBase64);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
        return Convert.ToBase64String(hash);
    }

    public static (string Hash, string Salt, int Iterations) CreatePassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(24);
        const int iterations = 120000;
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt), iterations);
    }

    public static bool VerifyPassword(string password, AuthAccount account)
    {
        var supplied = HashPassword(password, account.PasswordSalt, account.PasswordIterations);
        return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(supplied), Convert.FromBase64String(account.PasswordHash));
    }

    public static string CreateToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    public static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    public static async Task EnsureSchemaAsync(AppDbContext db)
    {
        var provider = db.Database.ProviderName ?? "";
        if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            await db.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS "AuthAccounts" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_AuthAccounts" PRIMARY KEY AUTOINCREMENT,
    "UserId" INTEGER NOT NULL,
    "PasswordHash" TEXT NOT NULL,
    "PasswordSalt" TEXT NOT NULL,
    "PasswordIterations" INTEGER NOT NULL DEFAULT 120000,
    "MustChangePassword" INTEGER NOT NULL DEFAULT 1,
    "LastLoginAt" TEXT NULL,
    "IsEnabled" INTEGER NOT NULL DEFAULT 1,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_AuthAccounts_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
)
""");
            await db.Database.ExecuteSqlRawAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_AuthAccounts_UserId" ON "AuthAccounts" ("UserId")""");
            await db.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS "AuthSessions" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_AuthSessions" PRIMARY KEY AUTOINCREMENT,
    "UserId" INTEGER NOT NULL,
    "TokenHash" TEXT NOT NULL,
    "ExpiresAt" TEXT NOT NULL,
    "RevokedAt" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_AuthSessions_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
)
""");
            await db.Database.ExecuteSqlRawAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_AuthSessions_TokenHash" ON "AuthSessions" ("TokenHash")""");
            await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_AuthSessions_UserId_ExpiresAt" ON "AuthSessions" ("UserId","ExpiresAt")""");
        }
        else if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'[AuthAccounts]', N'U') IS NULL
BEGIN
 CREATE TABLE [AuthAccounts](
  [Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,[UserId] BIGINT NOT NULL UNIQUE,
  [PasswordHash] NVARCHAR(255) NOT NULL,[PasswordSalt] NVARCHAR(255) NOT NULL,[PasswordIterations] INT NOT NULL,
  [MustChangePassword] BIT NOT NULL,[LastLoginAt] DATETIME2 NULL,[IsEnabled] BIT NOT NULL,
  [CreatedAt] DATETIME2 NOT NULL,[UpdatedAt] DATETIME2 NOT NULL,
  CONSTRAINT [FK_AuthAccounts_Users_UserId] FOREIGN KEY([UserId]) REFERENCES [Users]([Id]) ON DELETE CASCADE);
END;
IF OBJECT_ID(N'[AuthSessions]', N'U') IS NULL
BEGIN
 CREATE TABLE [AuthSessions](
  [Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,[UserId] BIGINT NOT NULL,
  [TokenHash] NVARCHAR(255) NOT NULL UNIQUE,[ExpiresAt] DATETIME2 NOT NULL,[RevokedAt] DATETIME2 NULL,
  [CreatedAt] DATETIME2 NOT NULL,[UpdatedAt] DATETIME2 NOT NULL,
  CONSTRAINT [FK_AuthSessions_Users_UserId] FOREIGN KEY([UserId]) REFERENCES [Users]([Id]) ON DELETE CASCADE);
 CREATE INDEX [IX_AuthSessions_UserId_ExpiresAt] ON [AuthSessions]([UserId],[ExpiresAt]);
END;
""");
        }
    }

    public static async Task SeedBootstrapAdminAsync(AppDbContext db)
    {
        if (await db.AuthAccounts.AnyAsync()) return;
        var admin = await db.Users.OrderBy(x => x.Id).FirstOrDefaultAsync(x => x.Role == "CIO" || x.Role == "ADMIN")
                    ?? await db.Users.OrderBy(x => x.Id).FirstOrDefaultAsync();
        if (admin is null) return;
        var bootstrapPassword = Environment.GetEnvironmentVariable("MPMS_BOOTSTRAP_PASSWORD") ?? "Admin@2026!";
        var p = CreatePassword(bootstrapPassword);
        db.AuthAccounts.Add(new AuthAccount
        {
            UserId = admin.Id, PasswordHash = p.Hash, PasswordSalt = p.Salt,
            PasswordIterations = p.Iterations, MustChangePassword = true, IsEnabled = true
        });
        await db.SaveChangesAsync();
    }

    public static async Task<(AppUser? User, AuthAccount? Account)> ResolveAsync(HttpContext http, AppDbContext db)
    {
        if (!http.Request.Cookies.TryGetValue(CookieName, out var token) || string.IsNullOrWhiteSpace(token))
            return (null, null);
        var tokenHash = HashToken(token);
        var session = await db.AuthSessions.AsNoTracking().FirstOrDefaultAsync(x => x.TokenHash == tokenHash && x.RevokedAt == null && x.ExpiresAt > DateTime.UtcNow);
        if (session is null) return (null, null);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == session.UserId && x.Status == "ACTIVE");
        var account = await db.AuthAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == session.UserId && x.IsEnabled);
        return (user, account);
    }
}
