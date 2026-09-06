using MAIPT.PM.Api.Data;
using MAIPT.PM.Api.Models;
using MAIPT.PM.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace MAIPT.PM.Api.Endpoints;

public static class ClubDocumentEndpoints
{
    static long U(HttpContext h)=>Convert.ToInt64(h.Items["AuthUserId"]);
    static bool Root(HttpContext h)=>string.Equals(Convert.ToString(h.Items["AuthUserRole"]),"ROOT",StringComparison.OrdinalIgnoreCase);

    static async Task<AppUser?> User(AppDbContext d,HttpContext h)=>
        await d.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==U(h));

    static async Task<bool> CanReadClub(AppDbContext d,HttpContext h,long clubId)=>
        Root(h) ||
        await d.PersonalFcClubs.AnyAsync(x=>x.Id==clubId&&x.OwnerUserId==U(h)) ||
        await d.PersonalFcClubMembers.AnyAsync(x=>x.ClubId==clubId&&x.UserId==U(h)&&x.Status=="ACTIVE");

    static async Task<bool> CanManageClub(AppDbContext d,HttpContext h,long clubId)=>
        Root(h) ||
        await d.PersonalFcClubs.AnyAsync(x=>x.Id==clubId&&x.OwnerUserId==U(h)) ||
        await d.PersonalFcClubMembers.AnyAsync(x=>x.ClubId==clubId&&x.UserId==U(h)&&(x.MemberRole=="ADMIN"||x.MemberRole=="TREASURER"));

    static async Task<bool> CanDownload(AppDbContext d,HttpContext h)
    {
        if(Root(h))return true;
        var u=await User(d,h);
        return u!=null && await RbacService.CanAsync(d,u,"PERSONAL_FC","DOWNLOAD");
    }

    public static void MapClubDocumentEndpoints(this WebApplication app)
    {
        app.MapGet("/api/personal-fc/clubs/{clubId:long}/documents",async(long clubId,HttpContext h,AppDbContext d)=>{
            if(!await CanReadClub(d,h,clubId))return Results.Forbid();
            var canDownload=await CanDownload(d,h);
            var items=await d.Documents.AsNoTracking()
                .Where(x=>x.EntityType=="CLUB"&&x.EntityId==clubId&&x.Status=="ACTIVE")
                .OrderByDescending(x=>x.UploadedAt)
                .Select(x=>new{
                    x.Id,x.Name,x.OriginalFileName,x.Category,x.MimeType,x.FileSize,x.UploadedAt,x.StorageWebUrl,
                    ViewUrl=$"/api/personal-fc/clubs/{clubId}/documents/{x.Id}/view",
                    DownloadUrl=canDownload?$"/api/personal-fc/clubs/{clubId}/documents/{x.Id}/download":null
                }).ToListAsync();
            return Results.Ok(new{canDownload,items});
        });

        app.MapPost("/api/personal-fc/clubs/{clubId:long}/documents/upload",
            async(long clubId,HttpRequest req,HttpContext h,AppDbContext d,M365StorageService m365)=>{
                if(!await CanManageClub(d,h,clubId))return Results.Forbid();
                if(!m365.Enabled)return Results.Json(new{message="M365 document storage is not enabled."},statusCode:503);

                var club=await d.PersonalFcClubs.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==clubId);
                if(club==null)return Results.NotFound();

                var form=await req.ReadFormAsync(h.RequestAborted);
                var file=form.Files.FirstOrDefault();
                if(file==null)return Results.BadRequest(new{message="No file selected."});

                var category=(form["category"].FirstOrDefault()??"CLUB_LIBRARY").Trim().ToUpperInvariant();
                var entityCode=string.IsNullOrWhiteSpace(club.Code)?$"CLUB-{clubId}":club.Code.Trim();
                var folder=M365StorageService.BuildEntityFolder("CLUB",entityCode);

                await using var stream=file.OpenReadStream();
                var stored=await m365.UploadAsync(stream,file.FileName,file.ContentType??"application/octet-stream",folder,h.RequestAborted);

                // Logo and team image are single-current-file categories.
                if(category is "CLUB_LOGO" or "CLUB_TEAM_IMAGE")
                {
                    var old=await d.Documents.Where(x=>x.EntityType=="CLUB"&&x.EntityId==clubId&&x.Category==category&&x.Status=="ACTIVE").ToListAsync();
                    foreach(var x in old){x.Status="DEACTIVATED";x.IsCurrent=false;}
                }

                var doc=new DocumentRecord{
                    OrgUnitId=1,EntityType="CLUB",EntityId=clubId,Category=category,
                    Name=(form["name"].FirstOrDefault()??category).Trim(),
                    OriginalFileName=Path.GetFileName(file.FileName),FilePath="",
                    MimeType=file.ContentType??"application/octet-stream",FileSize=stored.Size,
                    UploadedBy=U(h),UploadedAt=DateTime.UtcNow,Status="ACTIVE",IsCurrent=true,
                    StorageProvider="M365",StorageDriveId=stored.DriveId,StorageItemId=stored.ItemId,
                    StorageWebUrl=stored.WebUrl,StoragePath=stored.Path
                };
                d.Documents.Add(doc);await d.SaveChangesAsync();
                return Results.Created($"/api/personal-fc/clubs/{clubId}/documents/{doc.Id}",new{
                    doc.Id,doc.OriginalFileName,doc.Category,
                    ViewUrl=$"/api/personal-fc/clubs/{clubId}/documents/{doc.Id}/view"
                });
            });

        app.MapGet("/api/personal-fc/clubs/{clubId:long}/documents/{docId:long}/view",
            async(long clubId,long docId,HttpContext h,AppDbContext d,M365StorageService m365,IWebHostEnvironment env)=>{
                if(!await CanReadClub(d,h,clubId))return Results.Forbid();
                var doc=await d.Documents.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==docId&&x.EntityType=="CLUB"&&x.EntityId==clubId&&x.Status=="ACTIVE");
                if(doc==null)return Results.NotFound();

                if(string.Equals(doc.StorageProvider,"M365",StringComparison.OrdinalIgnoreCase))
                {
                    if(string.IsNullOrWhiteSpace(doc.StorageItemId))return Results.NotFound();
                    var f=await m365.DownloadAsync(doc.StorageItemId,doc.OriginalFileName,doc.MimeType,h.RequestAborted);
                    return Results.Stream(f.Stream,f.MimeType,enableRangeProcessing:true);
                }

                var decoded=Uri.UnescapeDataString(doc.FilePath??"");
                var rel=decoded.TrimStart('/').Replace('/',Path.DirectorySeparatorChar);
                var full=Path.GetFullPath(Path.Combine(env.ContentRootPath,rel));
                if(!File.Exists(full))return Results.NotFound();
                return Results.File(full,doc.MimeType??"application/octet-stream",enableRangeProcessing:true);
            });

        app.MapGet("/api/personal-fc/clubs/{clubId:long}/documents/{docId:long}/download",
            async(long clubId,long docId,HttpContext h,AppDbContext d,M365StorageService m365,IWebHostEnvironment env)=>{
                if(!await CanReadClub(d,h,clubId))return Results.Forbid();
                if(!await CanDownload(d,h))return Results.Forbid();

                var doc=await d.Documents.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==docId&&x.EntityType=="CLUB"&&x.EntityId==clubId&&x.Status=="ACTIVE");
                if(doc==null)return Results.NotFound();

                if(string.Equals(doc.StorageProvider,"M365",StringComparison.OrdinalIgnoreCase))
                {
                    if(string.IsNullOrWhiteSpace(doc.StorageItemId))return Results.NotFound();
                    var f=await m365.DownloadAsync(doc.StorageItemId,doc.OriginalFileName,doc.MimeType,h.RequestAborted);
                    return Results.File(f.Stream,f.MimeType,f.FileName,enableRangeProcessing:true);
                }

                var decoded=Uri.UnescapeDataString(doc.FilePath??"");
                var rel=decoded.TrimStart('/').Replace('/',Path.DirectorySeparatorChar);
                var full=Path.GetFullPath(Path.Combine(env.ContentRootPath,rel));
                if(!File.Exists(full))return Results.NotFound();
                return Results.File(full,doc.MimeType??"application/octet-stream",doc.OriginalFileName,enableRangeProcessing:true);
            });

        app.MapDelete("/api/personal-fc/clubs/{clubId:long}/documents/{docId:long}",
            async(long clubId,long docId,HttpContext h,AppDbContext d,M365StorageService m365)=>{
                if(!await CanManageClub(d,h,clubId))return Results.Forbid();
                var doc=await d.Documents.FirstOrDefaultAsync(x=>x.Id==docId&&x.EntityType=="CLUB"&&x.EntityId==clubId);
                if(doc==null)return Results.NotFound();
                if(string.Equals(doc.StorageProvider,"M365",StringComparison.OrdinalIgnoreCase)&&!string.IsNullOrWhiteSpace(doc.StorageItemId))
                    await m365.DeleteAsync(doc.StorageItemId,h.RequestAborted);
                doc.Status="DEACTIVATED";doc.IsCurrent=false;await d.SaveChangesAsync();
                return Results.NoContent();
            });
    }
}
