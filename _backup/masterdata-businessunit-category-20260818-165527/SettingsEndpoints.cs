using System.Security.Cryptography;
using MAIPT.PM.Api.Data;
using MAIPT.PM.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MAIPT.PM.Api.Endpoints;

public static class SettingsEndpoints
{
    static bool CanManage(HttpContext h) =>
        AuthService.IsManagerRole(Convert.ToString(h.Items["AuthUserRole"]));

    static string NewTemporaryPassword()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(6)) + "!aA1";

    static async Task RevokeSessions(long userId, AppDbContext db)
    {
        var sessions = await db.AuthSessions
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ToListAsync();

        foreach (var s in sessions)
            s.RevokedAt = DateTime.UtcNow;
    }

    public static void MapSettingsEndpoints(this WebApplication app)
    {
        // ---------------- Portfolios ----------------
        app.MapGet("/api/settings/portfolios", async (AppDbContext db) =>
            Results.Ok(await db.Portfolios.AsNoTracking()
                .Include(x => x.OrgUnit)
                .Include(x => x.Owner)
                .OrderBy(x => x.Name)
                .Select(x => new
                {
                    x.Id, x.OrgUnitId, x.Code, x.Name, x.Description, x.OwnerId, x.Status,
                    OrgUnit = x.OrgUnit != null ? x.OrgUnit.Name : null,
                    Owner = x.Owner != null ? x.Owner.Name : null,
                    ProjectCount = db.Projects.Count(p => p.PortfolioId == x.Id)
                }).ToListAsync()));

        app.MapPost("/api/settings/portfolios", async (Portfolio x, HttpContext h, AppDbContext db) =>
        {
            if (!CanManage(h)) return Results.Forbid();
            x.Code = (x.Code ?? "").Trim().ToUpperInvariant();
            x.Name = (x.Name ?? "").Trim();
            if (x.Code == "" || x.Name == "") return Results.BadRequest("Code and Name are required.");
            if (await db.Portfolios.AnyAsync(a => a.Code == x.Code))
                return Results.BadRequest("Portfolio code already exists.");
            db.Portfolios.Add(x);
            await db.SaveChangesAsync();
            return Results.Ok(x);
        });

        app.MapPut("/api/settings/portfolios/{id:long}", async (long id, Portfolio x, HttpContext h, AppDbContext db) =>
        {
            if (!CanManage(h)) return Results.Forbid();
            var r = await db.Portfolios.FindAsync(id);
            if (r is null) return Results.NotFound();
            var code = (x.Code ?? "").Trim().ToUpperInvariant();
            if (await db.Portfolios.AnyAsync(a => a.Id != id && a.Code == code))
                return Results.BadRequest("Portfolio code already exists.");
            r.OrgUnitId=x.OrgUnitId; r.Code=code; r.Name=(x.Name??"").Trim();
            r.Description=x.Description; r.OwnerId=x.OwnerId; r.Status=x.Status;
            await db.SaveChangesAsync();
            return Results.Ok(r);
        });

        app.MapDelete("/api/settings/portfolios/{id:long}", async (long id, HttpContext h, AppDbContext db) =>
        {
            if (!CanManage(h)) return Results.Forbid();
            var r = await db.Portfolios.FindAsync(id);
            if (r is null) return Results.NotFound();
            var n = await db.Projects.CountAsync(x => x.PortfolioId == id);
            if (n > 0) return Results.BadRequest($"Cannot delete: {n} project(s) use this portfolio. Set INACTIVE instead.");
            db.Portfolios.Remove(r);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ---------------- Business Units ----------------
        app.MapGet("/api/settings/org-units", async (AppDbContext db) =>
            Results.Ok(await db.OrgUnits.AsNoTracking().OrderBy(x => x.Name)
                .Select(x => new
                {
                    x.Id, x.Code, x.Name, x.Type, x.ParentId, x.Status,
                    UserCount = db.Users.Count(u => u.OrgUnitId == x.Id),
                    PortfolioCount = db.Portfolios.Count(p => p.OrgUnitId == x.Id),
                    ProjectCount = db.Projects.Count(p => p.OrgUnitId == x.Id)
                }).ToListAsync()));

        app.MapPost("/api/settings/org-units", async (OrgUnit x, HttpContext h, AppDbContext db) =>
        {
            if (!CanManage(h)) return Results.Forbid();
            x.Code=(x.Code??"").Trim().ToUpperInvariant();
            x.Name=(x.Name??"").Trim();
            if (x.Code=="" || x.Name=="") return Results.BadRequest("Code and Name are required.");
            if (await db.OrgUnits.AnyAsync(a => a.Code == x.Code))
                return Results.BadRequest("Business Unit code already exists.");
            db.OrgUnits.Add(x);
            await db.SaveChangesAsync();
            return Results.Ok(x);
        });

        app.MapPut("/api/settings/org-units/{id:long}", async (long id, OrgUnit x, HttpContext h, AppDbContext db) =>
        {
            if (!CanManage(h)) return Results.Forbid();
            var r=await db.OrgUnits.FindAsync(id);
            if (r is null) return Results.NotFound();
            var code=(x.Code??"").Trim().ToUpperInvariant();
            if (await db.OrgUnits.AnyAsync(a=>a.Id!=id && a.Code==code))
                return Results.BadRequest("Business Unit code already exists.");
            r.Code=code; r.Name=(x.Name??"").Trim(); r.Type=x.Type; r.ParentId=x.ParentId; r.Status=x.Status;
            await db.SaveChangesAsync();
            return Results.Ok(r);
        });

        app.MapDelete("/api/settings/org-units/{id:long}", async (long id, HttpContext h, AppDbContext db) =>
        {
            if (!CanManage(h)) return Results.Forbid();
            var r=await db.OrgUnits.FindAsync(id);
            if (r is null) return Results.NotFound();
            var n=await db.Users.CountAsync(x=>x.OrgUnitId==id)
                + await db.Portfolios.CountAsync(x=>x.OrgUnitId==id)
                + await db.Projects.CountAsync(x=>x.OrgUnitId==id);
            if (n>0) return Results.BadRequest("Cannot delete: this Business Unit is in use. Set INACTIVE instead.");
            db.OrgUnits.Remove(r);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ---------------- Team Members ----------------
        app.MapGet("/api/settings/team-members", async (AppDbContext db) =>
            Results.Ok(await db.Users.AsNoTracking()
                .Include(x => x.OrgUnit)
                .OrderBy(x => x.Name)
                .Select(x => new
                {
                    x.Id, x.OrgUnitId, x.Name, x.Email, x.JobTitle, x.Department, x.Role, x.Status,
                    OrgUnit = x.OrgUnit != null ? x.OrgUnit.Name : null,
                    ProjectCount = db.ProjectMembers.Count(p => p.UserId == x.Id),
                    PerformanceCount = db.PerformancePeriods.Count(p => p.UserId == x.Id),
                    HasLogin = db.AuthAccounts.Any(a => a.UserId == x.Id),
                    LoginEnabled = db.AuthAccounts.Where(a => a.UserId == x.Id).Select(a => (bool?)a.IsEnabled).FirstOrDefault() ?? false,
                    MustChangePassword = db.AuthAccounts.Where(a => a.UserId == x.Id).Select(a => (bool?)a.MustChangePassword).FirstOrDefault() ?? false
                }).ToListAsync()));

        app.MapPost("/api/settings/team-members", async (AppUser x, HttpContext h, AppDbContext db) =>
        {
            if (!CanManage(h)) return Results.Forbid();
            x.Name=(x.Name??"").Trim();
            x.Email=(x.Email??"").Trim().ToLowerInvariant();
            if (x.Name=="" || x.Email=="") return Results.BadRequest("Name and Email are required.");
            if (await db.Users.AnyAsync(a => a.Email.ToLower() == x.Email))
                return Results.BadRequest("A Team Member with this email already exists.");
            db.Users.Add(x);
            await db.SaveChangesAsync();
            return Results.Ok(x);
        });

        app.MapPut("/api/settings/team-members/{id:long}", async (long id, AppUser x, HttpContext h, AppDbContext db) =>
        {
            if (!CanManage(h)) return Results.Forbid();
            var r=await db.Users.FindAsync(id);
            if (r is null) return Results.NotFound();
            var email=(x.Email??"").Trim().ToLowerInvariant();
            if (await db.Users.AnyAsync(a=>a.Id!=id && a.Email.ToLower()==email))
                return Results.BadRequest("A Team Member with this email already exists.");
            r.OrgUnitId=x.OrgUnitId; r.Name=(x.Name??"").Trim(); r.Email=email;
            r.JobTitle=x.JobTitle; r.Department=x.Department; r.Role=x.Role; r.Status=x.Status;
            await db.SaveChangesAsync();
            return Results.Ok(r);
        });

        app.MapDelete("/api/settings/team-members/{id:long}", async (long id, HttpContext h, AppDbContext db) =>
        {
            if (!CanManage(h)) return Results.Forbid();
            var r=await db.Users.FindAsync(id);
            if (r is null) return Results.NotFound();

            var projectAssignments=await db.ProjectMembers.CountAsync(x=>x.UserId==id);
            var performance=await db.PerformancePeriods.CountAsync(x=>x.UserId==id);
            var projectOwnership=await db.Projects.CountAsync(x=>x.OwnerId==id || x.SponsorId==id);
            var portfolioOwnership=await db.Portfolios.CountAsync(x=>x.OwnerId==id);

            if (projectAssignments+performance+projectOwnership+portfolioOwnership>0)
                return Results.BadRequest(
                    $"Cannot delete this member. Related data: {projectAssignments} project assignment(s), {performance} performance period(s), {projectOwnership} project owner/sponsor relation(s), {portfolioOwnership} portfolio owner relation(s). Set Status to INACTIVE instead.");

            db.Users.Remove(r);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ---------------- Login management ----------------
        app.MapPost("/api/settings/team-members/{id:long}/login/create", async (long id, HttpContext h, AppDbContext db) =>
        {
            if (!CanManage(h)) return Results.Forbid();
            var user=await db.Users.FindAsync(id);
            if (user is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(user.Email)) return Results.BadRequest("Member email is required before creating a login.");
            if (await db.AuthAccounts.AnyAsync(x=>x.UserId==id))
                return Results.BadRequest("This member already has a login. Use Reset Login.");

            var password=NewTemporaryPassword();
            var p=AuthService.CreatePassword(password);
            db.AuthAccounts.Add(new AuthAccount
            {
                UserId=id,
                PasswordHash=p.Hash,
                PasswordSalt=p.Salt,
                PasswordIterations=p.Iterations,
                MustChangePassword=true,
                IsEnabled=true
            });
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                user.Id, user.Name, user.Email,
                TemporaryPassword=password,
                MustChangePassword=true
            });
        });

        app.MapPost("/api/settings/team-members/{id:long}/login/reset", async (long id, HttpContext h, AppDbContext db) =>
        {
            if (!CanManage(h)) return Results.Forbid();
            var user=await db.Users.FindAsync(id);
            if (user is null) return Results.NotFound();
            var account=await db.AuthAccounts.FirstOrDefaultAsync(x=>x.UserId==id);
            if (account is null) return Results.BadRequest("This member has no login. Use Create Login.");

            var password=NewTemporaryPassword();
            var p=AuthService.CreatePassword(password);
            account.PasswordHash=p.Hash;
            account.PasswordSalt=p.Salt;
            account.PasswordIterations=p.Iterations;
            account.MustChangePassword=true;
            account.IsEnabled=true;
            account.LastLoginAt=null;
            await RevokeSessions(id,db);
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                user.Id, user.Name, user.Email,
                TemporaryPassword=password,
                MustChangePassword=true
            });
        });

        app.MapPost("/api/settings/team-members/{id:long}/login/toggle", async (long id, LoginToggleRequest input, HttpContext h, AppDbContext db) =>
        {
            if (!CanManage(h)) return Results.Forbid();
            var account=await db.AuthAccounts.FirstOrDefaultAsync(x=>x.UserId==id);
            if (account is null) return Results.BadRequest("This member has no login.");
            account.IsEnabled=input.IsEnabled;
            if (!input.IsEnabled) await RevokeSessions(id,db);
            await db.SaveChangesAsync();
            return Results.Ok(new { id, account.IsEnabled });
        });

        // ---------------- Options ----------------
        app.MapGet("/api/settings/options", async (AppDbContext db) =>
            Results.Ok(new
            {
                orgUnits=await db.OrgUnits.AsNoTracking().OrderBy(x=>x.Name)
                    .Select(x=>new{x.Id,x.Code,x.Name,x.Status}).ToListAsync(),
                users=await db.Users.AsNoTracking().OrderBy(x=>x.Name)
                    .Select(x=>new{x.Id,x.Name,x.Email,x.Status}).ToListAsync()
            }));
    }
}

public record LoginToggleRequest(bool IsEnabled);
