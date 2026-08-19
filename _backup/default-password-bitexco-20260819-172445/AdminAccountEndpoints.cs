using System.Security.Cryptography;
using MAIPT.PM.Api.Data;
using MAIPT.PM.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MAIPT.PM.Api.Endpoints;

public sealed record AdminResetPasswordRequest(string? TemporaryPassword);

public static class AdminAccountEndpoints
{
    private const int PasswordIterations = 120000;
    private const int SaltBytes = 24;
    private const int HashBytes = 32;
    private const string DefaultTemporaryPassword = "Bitexco@123";

    private static async Task<AppUser?> CurrentUser(HttpContext http, AppDbContext db)
    {
        if (!http.Items.TryGetValue("AuthUserId", out var value) || value is null)
            return null;

        return await db.Users.FirstOrDefaultAsync(x => x.Id == Convert.ToInt64(value));
    }

    private static (string Hash, string Salt) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            PasswordIterations,
            HashAlgorithmName.SHA256,
            HashBytes);

        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    private static async Task UpsertAccount(AppDbContext db, AppUser user, string password)
    {
        var account = await db.AuthAccounts.FirstOrDefaultAsync(x => x.UserId == user.Id);
        var now = DateTime.UtcNow;
        var pair = HashPassword(password);

        if (account is null)
        {
            account = new AuthAccount
            {
                UserId = user.Id,
                PasswordHash = pair.Hash,
                PasswordSalt = pair.Salt,
                PasswordIterations = PasswordIterations,
                MustChangePassword = true,
                LastLoginAt = null,
                IsEnabled = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.AuthAccounts.Add(account);
        }
        else
        {
            account.PasswordHash = pair.Hash;
            account.PasswordSalt = pair.Salt;
            account.PasswordIterations = PasswordIterations;
            account.MustChangePassword = true;
            account.LastLoginAt = null;
            account.IsEnabled = true;
            account.UpdatedAt = now;
        }
    }

    public static void MapAdminAccountEndpoints(this WebApplication app)
    {
        app.MapPost("/api/access/users/{id:long}/reset-password",
            async (long id, AdminResetPasswordRequest? input, HttpContext http, AppDbContext db) =>
            {
                var admin = await CurrentUser(http, db);
                if (admin is null) return Results.Unauthorized();
                if (!RbacService.IsAdmin(admin)) return Results.Forbid();

                var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id);
                if (user is null) return Results.NotFound(new { message = "User not found." });

                var password = string.IsNullOrWhiteSpace(input?.TemporaryPassword)
                    ? DefaultTemporaryPassword
                    : input!.TemporaryPassword!.Trim();

                if (password.Length < 8)
                    return Results.BadRequest(new { message = "Temporary password must be at least 8 characters." });

                await UpsertAccount(db, user, password);
                await db.SaveChangesAsync();

                return Results.Ok(new
                {
                    user.Id,
                    user.Email,
                    temporaryPassword = password,
                    mustChangePassword = true,
                    isEnabled = true
                });
            });

        app.MapPost("/api/access/accounts/initialize-missing",
            async (HttpContext http, AppDbContext db) =>
            {
                var admin = await CurrentUser(http, db);
                if (admin is null) return Results.Unauthorized();
                if (!RbacService.IsAdmin(admin)) return Results.Forbid();

                var existingUserIds = await db.AuthAccounts.AsNoTracking()
                    .Select(x => x.UserId)
                    .ToListAsync();

                var users = await db.Users
                    .Where(x => !existingUserIds.Contains(x.Id))
                    .OrderBy(x => x.Name)
                    .ToListAsync();

                foreach (var user in users)
                    await UpsertAccount(db, user, DefaultTemporaryPassword);

                await db.SaveChangesAsync();

                return Results.Ok(new
                {
                    created = users.Count,
                    temporaryPassword = DefaultTemporaryPassword,
                    mustChangePassword = true,
                    users = users.Select(x => new { x.Id, x.Name, x.Email })
                });
            });
    }
}
