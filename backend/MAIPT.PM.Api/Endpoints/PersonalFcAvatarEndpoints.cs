using MAIPT.PM.Api.Data;
using MAIPT.PM.Api.Models;
using MAIPT.PM.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace MAIPT.PM.Api.Endpoints;

public static class PersonalFcAvatarEndpoints
{
    static string Safe(string? s)
    {
        var x=(s??"").Trim();
        foreach(var c in Path.GetInvalidFileNameChars()) x=x.Replace(c,'_');
        x=x.Replace('/','_').Replace('\\','_').Replace(':','_');
        return string.IsNullOrWhiteSpace(x) ? "UNKNOWN" : x;
    }

    static async Task<bool> CanEdit(AppDbContext db,HttpContext http)
    {
        if(http.Items["AuthUserId"] is null) return false;
        var uid=Convert.ToInt64(http.Items["AuthUserId"]);
        var user=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid);
        if(user is null) return false;
        return await RbacService.CanAsync(db,user,"PERSONAL_FC","EDIT")
            || await RbacService.CanAsync(db,user,"PERSONAL_FC","UPLOAD")
            || await RbacService.CanAsync(db,user,"PERSONAL_FC","FULL");
    }

    static async Task<IResult> StreamDoc(DocumentRecord d,M365StorageService m365,IWebHostEnvironment env,HttpContext http)
    {
        if(string.Equals(d.StorageProvider,"M365",StringComparison.OrdinalIgnoreCase))
        {
            if(string.IsNullOrWhiteSpace(d.StorageItemId)) return Results.NotFound();
            var f=await m365.DownloadAsync(d.StorageItemId,d.OriginalFileName,d.MimeType,http.RequestAborted);
            return Results.File(f.Stream,f.MimeType,enableRangeProcessing:true);
        }

        var rel=(d.FilePath??"").TrimStart('/').Replace('/',Path.DirectorySeparatorChar);
        var full=Path.GetFullPath(Path.Combine(env.ContentRootPath,rel));
        var root=Path.GetFullPath(Path.Combine(env.ContentRootPath,"storage"))+Path.DirectorySeparatorChar;
        if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!File.Exists(full))return Results.NotFound();
        return Results.File(full,d.MimeType??"application/octet-stream",enableRangeProcessing:true);
    }

    public static void MapPersonalFcAvatarEndpoints(this WebApplication app)
    {
        app.MapPost("/api/personal-fc/clubs/{clubId:long}/members/{memberId:long}/avatar",
            async(long clubId,long memberId,HttpRequest req,HttpContext http,AppDbContext db,M365StorageService m365)=>{
                if(!await CanEdit(db,http))return Results.Forbid();
                if(!m365.Enabled)return Results.Json(new{message="M365/SharePoint storage is not enabled."},statusCode:503);

                var club=await db.PersonalFcClubs.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==clubId);
                var member=await db.PersonalFcClubMembers.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==memberId&&x.ClubId==clubId);
                if(club is null||member is null)return Results.NotFound(new{message="Club/member not found."});

                var form=await req.ReadFormAsync(http.RequestAborted);
                var file=form.Files.FirstOrDefault();
                if(file is null)return Results.BadRequest(new{message="No image selected."});

                var folder=$"PERSONAL_FC/CLUBS/{Safe(club.Code)}/MEMBERS/{Safe(member.MemberCode)}/AVATAR";
                await using var stream=file.OpenReadStream();
                var stored=await m365.UploadAsync(stream,Path.GetFileName(file.FileName),file.ContentType??"application/octet-stream",folder,http.RequestAborted);

                var old=await db.Documents.Where(x=>x.EntityType=="PERSONAL_FC_MEMBER"&&x.EntityId==memberId&&x.Category=="AVATAR"&&x.Status=="ACTIVE").ToListAsync();
                foreach(var x in old){x.Status="DEACTIVATED";x.IsCurrent=false;}

                var uid=Convert.ToInt64(http.Items["AuthUserId"]);
                var d=new DocumentRecord{
                    OrgUnitId=1,EntityType="PERSONAL_FC_MEMBER",EntityId=memberId,Category="AVATAR",
                    Name=$"{member.MemberCode}_AVATAR",OriginalFileName=Path.GetFileName(file.FileName),
                    FilePath="",MimeType=file.ContentType??"application/octet-stream",FileSize=stored.Size,
                    UploadedBy=uid,UploadedAt=DateTime.UtcNow,Status="ACTIVE",IsCurrent=true,
                    StorageProvider="M365",StorageDriveId=stored.DriveId,StorageItemId=stored.ItemId,
                    StorageWebUrl=stored.WebUrl,StoragePath=stored.Path
                };
                db.Documents.Add(d);await db.SaveChangesAsync(http.RequestAborted);
                return Results.Ok(new{d.Id,d.StoragePath,avatarUrl=$"/api/personal-fc/clubs/{clubId}/members/{memberId}/avatar"});
            });

        app.MapGet("/api/personal-fc/clubs/{clubId:long}/members/{memberId:long}/avatar",
            async(long clubId,long memberId,HttpContext http,AppDbContext db,M365StorageService m365,IWebHostEnvironment env)=>{
                var exists=await db.PersonalFcClubMembers.AsNoTracking().AnyAsync(x=>x.Id==memberId&&x.ClubId==clubId);
                if(!exists)return Results.NotFound();
                var d=await db.Documents.AsNoTracking().Where(x=>x.EntityType=="PERSONAL_FC_MEMBER"&&x.EntityId==memberId&&x.Category=="AVATAR"&&x.Status=="ACTIVE").OrderByDescending(x=>x.UploadedAt).FirstOrDefaultAsync();
                return d is null?Results.NotFound():await StreamDoc(d,m365,env,http);
            });

        app.MapPost("/api/personal-fc/tournaments/{tourId:long}/registrations/{regId:long}/avatar",
            async(long tourId,long regId,HttpRequest req,HttpContext http,AppDbContext db,M365StorageService m365)=>{
                if(!await CanEdit(db,http))return Results.Forbid();
                if(!m365.Enabled)return Results.Json(new{message="M365/SharePoint storage is not enabled."},statusCode:503);

                var tour=await db.PersonalFcTournaments.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==tourId);
                var reg=await db.PersonalFcTournamentRegistrations.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==regId&&x.TournamentId==tourId);
                if(tour is null||reg is null)return Results.NotFound(new{message="Tournament participant not found."});

                var form=await req.ReadFormAsync(http.RequestAborted);
                var file=form.Files.FirstOrDefault();
                if(file is null)return Results.BadRequest(new{message="No image selected."});

                var folder=$"PERSONAL_FC/TOURNAMENTS/{Safe(tour.Code)}/MEMBERS/{regId}/AVATAR";
                await using var stream=file.OpenReadStream();
                var stored=await m365.UploadAsync(stream,Path.GetFileName(file.FileName),file.ContentType??"application/octet-stream",folder,http.RequestAborted);

                var old=await db.Documents.Where(x=>x.EntityType=="PERSONAL_FC_TOUR_MEMBER"&&x.EntityId==regId&&x.Category=="AVATAR"&&x.Status=="ACTIVE").ToListAsync();
                foreach(var x in old){x.Status="DEACTIVATED";x.IsCurrent=false;}

                var uid=Convert.ToInt64(http.Items["AuthUserId"]);
                var d=new DocumentRecord{
                    OrgUnitId=1,EntityType="PERSONAL_FC_TOUR_MEMBER",EntityId=regId,Category="AVATAR",
                    Name=$"REG_{regId}_AVATAR",OriginalFileName=Path.GetFileName(file.FileName),
                    FilePath="",MimeType=file.ContentType??"application/octet-stream",FileSize=stored.Size,
                    UploadedBy=uid,UploadedAt=DateTime.UtcNow,Status="ACTIVE",IsCurrent=true,
                    StorageProvider="M365",StorageDriveId=stored.DriveId,StorageItemId=stored.ItemId,
                    StorageWebUrl=stored.WebUrl,StoragePath=stored.Path
                };
                db.Documents.Add(d);await db.SaveChangesAsync(http.RequestAborted);
                return Results.Ok(new{d.Id,d.StoragePath,avatarUrl=$"/api/personal-fc/tournaments/{tourId}/registrations/{regId}/avatar"});
            });

        app.MapGet("/api/personal-fc/tournaments/{tourId:long}/registrations/{regId:long}/avatar",
            async(long tourId,long regId,HttpContext http,AppDbContext db,M365StorageService m365,IWebHostEnvironment env)=>{
                var reg=await db.PersonalFcTournamentRegistrations.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==regId&&x.TournamentId==tourId);
                if(reg is null)return Results.NotFound();

                var own=await db.Documents.AsNoTracking().Where(x=>x.EntityType=="PERSONAL_FC_TOUR_MEMBER"&&x.EntityId==regId&&x.Category=="AVATAR"&&x.Status=="ACTIVE").OrderByDescending(x=>x.UploadedAt).FirstOrDefaultAsync();
                if(own is not null)return await StreamDoc(own,m365,env,http);

                if(reg.MemberId.HasValue)
                {
                    var inherited=await db.Documents.AsNoTracking().Where(x=>x.EntityType=="PERSONAL_FC_MEMBER"&&x.EntityId==reg.MemberId.Value&&x.Category=="AVATAR"&&x.Status=="ACTIVE").OrderByDescending(x=>x.UploadedAt).FirstOrDefaultAsync();
                    if(inherited is not null)return await StreamDoc(inherited,m365,env,http);
                }
                return Results.NotFound();
            });
    }
}

