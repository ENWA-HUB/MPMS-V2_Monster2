using MAIPT.PM.Api.Data;
using MAIPT.PM.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MAIPT.PM.Api.Endpoints;

public static class ClubProfileEndpoints
{
    static long U(HttpContext h)=>Convert.ToInt64(h.Items["AuthUserId"]);
    static bool Root(HttpContext h)=>string.Equals(Convert.ToString(h.Items["AuthUserRole"]),"ROOT",StringComparison.OrdinalIgnoreCase);
    static async Task<bool> CanRead(AppDbContext d,HttpContext h,long clubId)=>
        Root(h) ||
        await d.PersonalFcClubs.AnyAsync(x=>x.Id==clubId&&x.OwnerUserId==U(h)) ||
        await d.PersonalFcClubMembers.AnyAsync(x=>x.ClubId==clubId&&x.UserId==U(h)&&x.Status=="ACTIVE");
    static async Task<bool> CanManage(AppDbContext d,HttpContext h,long clubId)=>
        Root(h) ||
        await d.PersonalFcClubs.AnyAsync(x=>x.Id==clubId&&x.OwnerUserId==U(h)) ||
        await d.PersonalFcClubMembers.AnyAsync(x=>x.ClubId==clubId&&x.UserId==U(h)&&(x.MemberRole=="ADMIN"||x.MemberRole=="TREASURER"));

    public static void MapClubProfileEndpoints(this WebApplication app)
    {
        app.MapGet("/api/personal-fc/clubs/{clubId:long}/profile",async(long clubId,HttpContext h,AppDbContext d)=>{
            if(!await CanRead(d,h,clubId))return Results.Forbid();
            var club=await d.PersonalFcClubs.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==clubId);
            if(club==null)return Results.NotFound();
            var profile=await d.PersonalFcClubProfiles.AsNoTracking().FirstOrDefaultAsync(x=>x.ClubId==clubId);
            var totalMembers=await d.PersonalFcClubMembers.AsNoTracking().CountAsync(x=>x.ClubId==clubId);
            var activeMembers=await d.PersonalFcClubMembers.AsNoTracking().CountAsync(x=>x.ClubId==clubId&&x.Status=="ACTIVE");
            var upcomingActivities=await d.PersonalFcClubEvents.AsNoTracking()
                .Where(x=>x.ClubId==clubId&&x.EventDate>=DateOnly.FromDateTime(DateTime.Today)&&x.Status!="COMPLETED")
                .OrderBy(x=>x.EventDate).Take(6).ToListAsync();
            var funds=await d.PersonalFcClubFunds.AsNoTracking().Where(x=>x.ClubId==clubId&&x.Status=="ACTIVE").ToListAsync();
            var tq=d.PersonalFcClubTransactions.AsNoTracking().Where(x=>x.ClubId==clubId);
            var inc=await tq.Where(x=>x.TransactionType=="INCOME").SumAsync(x=>(decimal?)x.Amount)??0;
            var exp=await tq.Where(x=>x.TransactionType=="EXPENSE").SumAsync(x=>(decimal?)x.Amount)??0;
            var fundBalance=funds.Sum(x=>x.OpeningBalance)+inc-exp;
            return Results.Ok(new{club,profile,totalMembers,activeMembers,fundBalance,upcomingActivities});
        });

        app.MapPut("/api/personal-fc/clubs/{clubId:long}/profile",async(long clubId,PersonalFcClubProfile input,HttpContext h,AppDbContext d)=>{
            if(!await CanManage(d,h,clubId))return Results.Forbid();
            if(!await d.PersonalFcClubs.AnyAsync(x=>x.Id==clubId))return Results.NotFound();
            var x=await d.PersonalFcClubProfiles.FirstOrDefaultAsync(p=>p.ClubId==clubId);
            if(x==null){x=new PersonalFcClubProfile{ClubId=clubId};d.PersonalFcClubProfiles.Add(x);}
            x.About=input.About??"";
            x.Mission=input.Mission??"";
            x.Vision=input.Vision??"";
            x.CoreValues=input.CoreValues??"";
            x.FoundedDate=input.FoundedDate;
            x.ContactEmail=input.ContactEmail??"";
            x.ContactPhone=input.ContactPhone??"";
            x.Website=input.Website??"";
            x.SocialLink=input.SocialLink??"";
            x.MainVenue=input.MainVenue??"";
            x.RegulationsSummary=input.RegulationsSummary??"";
            x.ActivitiesSummary=input.ActivitiesSummary??"";
            x.UpdatedAt=DateTime.UtcNow;
            await d.SaveChangesAsync();
            return Results.Ok(x);
        });

        app.MapPost("/api/personal-fc/clubs/{clubId:long}/activate",async(long clubId,HttpContext h,AppDbContext d)=>{
            if(!await CanManage(d,h,clubId))return Results.Forbid();
            var x=await d.PersonalFcClubs.FindAsync(clubId);if(x==null)return Results.NotFound();
            x.Status="ACTIVE";await d.SaveChangesAsync();return Results.Ok(x);
        });

        app.MapPost("/api/personal-fc/clubs/{clubId:long}/deactivate",async(long clubId,HttpContext h,AppDbContext d)=>{
            if(!await CanManage(d,h,clubId))return Results.Forbid();
            var x=await d.PersonalFcClubs.FindAsync(clubId);if(x==null)return Results.NotFound();
            x.Status="INACTIVE";await d.SaveChangesAsync();return Results.Ok(x);
        });

        app.MapDelete("/api/personal-fc/clubs/{clubId:long}",async(long clubId,HttpContext h,AppDbContext d)=>{
            if(!await CanManage(d,h,clubId))return Results.Forbid();
            var x=await d.PersonalFcClubs.FindAsync(clubId);if(x==null)return Results.NotFound();
            x.Status="DELETED";await d.SaveChangesAsync();return Results.NoContent();
        });

        app.MapGet("/api/personal-fc/clubs/{clubId:long}/documents-legacy-disabled",async(long clubId,HttpContext h,AppDbContext d)=>{
            if(!await CanRead(d,h,clubId))return Results.Forbid();
            var rows=await d.Documents.AsNoTracking()
                .Where(x=>x.EntityType=="CLUB"&&x.EntityId==clubId&&x.Status=="ACTIVE")
                .OrderByDescending(x=>x.UploadedAt)
                .Select(x=>new{x.Id,x.Name,x.OriginalFileName,x.Category,x.MimeType,x.FileSize,x.UploadedAt,x.StorageProvider,x.StorageWebUrl,x.StoragePath,
                    DownloadUrl=$"/api/m365-documents/{x.Id}/download"})
                .ToListAsync();
            return Results.Ok(rows);
        });
    }
}
