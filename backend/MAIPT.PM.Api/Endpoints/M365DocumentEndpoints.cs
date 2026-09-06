using MAIPT.PM.Api.Data;
using MAIPT.PM.Api.Models;
using MAIPT.PM.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace MAIPT.PM.Api.Endpoints;

public static class M365DocumentEndpoints
{
    static string CategoryFromPath(string path)
    {
        var parts=(path??"").Split('/',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        var folders=parts.Length>1?parts[..^1]:Array.Empty<string>();
        if(folders.Any(x=>x.Equals("CAPITAL MANAGEMENT",StringComparison.OrdinalIgnoreCase)||x.Equals("CAPITAL MGMT",StringComparison.OrdinalIgnoreCase)))return $"CAPITAL_{folders.LastOrDefault()??"DOCUMENT"}".ToUpperInvariant();
        if(folders.Any(x=>x.Equals("INVESTMENT MANAGEMENT",StringComparison.OrdinalIgnoreCase)||x.Equals("INVEST MGMT",StringComparison.OrdinalIgnoreCase)))return $"INVESTMENT_{folders.LastOrDefault()??"DOCUMENT"}".ToUpperInvariant();
        if(folders.Any(x=>x.Equals("IT Policies & Procedures",StringComparison.OrdinalIgnoreCase)))return "IT POLICIES & PROCEDURES";
        if(folders.Any(x=>x.Equals("PERSONAL FC",StringComparison.OrdinalIgnoreCase)||x.Equals("PERSONAL_FC",StringComparison.OrdinalIgnoreCase)))return $"PERSONAL_FC_{folders.LastOrDefault()??"DOCUMENT"}".ToUpperInvariant();
        if(folders.Any(x=>x.Equals("SYSTEM",StringComparison.OrdinalIgnoreCase)))return $"SYSTEM_{folders.LastOrDefault()??"DOCUMENT"}".ToUpperInvariant();
        if(folders.Any(x=>x.Equals("PROJECTS",StringComparison.OrdinalIgnoreCase)))return "PROJECT_DOCUMENT";
        if(folders.Any(x=>x.Equals("CONTRACTS",StringComparison.OrdinalIgnoreCase)))return "CONTRACT_DOCUMENT";
        if(folders.Any(x=>x.Equals("SUPPLIERS",StringComparison.OrdinalIgnoreCase)))return "SUPPLIER_DOCUMENT";
        if(folders.Any(x=>x.Equals("BUDGET",StringComparison.OrdinalIgnoreCase)))return "BUDGET_DOCUMENT";
        if(folders.Any(x=>x.Equals("KPI",StringComparison.OrdinalIgnoreCase)||x.Equals("PERFORMANCE",StringComparison.OrdinalIgnoreCase)))return "KPI_DOCUMENT";
        if(folders.Any(x=>x.Equals("ASSETS",StringComparison.OrdinalIgnoreCase)))return "ASSET_DOCUMENT";
        if(folders.Any(x=>x.Equals("LICENSES",StringComparison.OrdinalIgnoreCase)))return "LICENSE_DOCUMENT";
        if(folders.Any(x=>x.Equals("HANDOVER",StringComparison.OrdinalIgnoreCase)))return "HANDOVER_DOCUMENT";
        var leaf=folders.LastOrDefault();
        return string.IsNullOrWhiteSpace(leaf)?"GENERAL":leaf.Trim().ToUpperInvariant();
    }

    static string FunctionFromPath(string path)
    {
        var p=(path??"").Replace('\\','/').ToUpperInvariant();
        if(p is "PROJECT" or "PROJECTS")return "PROJECTS";
        if(p is "CONTRACT" or "CONTRACTS")return "CONTRACTS";
        if(p is "SUPPLIER" or "SUPPLIERS")return "SUPPLIERS";
        if(p is "BUDGET")return "BUDGET";
        if(p is "KPI" or "PERFORMANCE")return "KPI";
        if(p is "PERSONAL FC" or "PERSONAL_FC" or "CLUB")return "PERSONAL_FC";
        if(p is "SYSTEM" or "AVATAR" or "USER" or "USERS" or "USER_PROFILE" or "SIGNATURE")return "USERS";
        if(p is "IT_ASSET" or "IT-ASSET" or "ASSET" or "LICENSE" or "LICENSES" or "IT_HANDOVER" or "HANDOVER" or "IT_DOMAIN")return "IT_ASSETS";
        if(p.Contains("CAPITAL MANAGEMENT")||p.Contains("CAPITAL MGMT"))return "CAPITAL";
        if(p.Contains("INVESTMENT MANAGEMENT")||p.Contains("INVEST MGMT"))return "INVESTMENT";
        if(p.Contains("IT ASSETS & SERVICES")||p.Contains("IT POLICIES & PROCEDURES"))return "IT_ASSETS";
        if(p.Contains("PROJECT MANAGEMENT/PROJECTS"))return "PROJECTS";
        if(p.Contains("FINANCIAL CONTROL/CONTRACTS"))return "CONTRACTS";
        if(p.Contains("FINANCIAL CONTROL/BUDGET"))return "BUDGET";
        if(p.Contains("ORGANIZATION/SUPPLIERS"))return "SUPPLIERS";
        if(p.Contains("PERSONAL FC")||p.Contains("PERSONAL_FC")||p.Contains("/CLUB/"))return "PERSONAL_FC";
        if(p.Contains("/SYSTEM/")||p.Contains("/AVATAR/")||p.Contains("USER PROFILE")||p.Contains("USER_PROFILE")||p.Contains("/SIGNATURE/"))return "USERS";
        if(p.Contains("PERFORMANCE")||p.Contains("/KPI/"))return "KPI";
        return "DOCUMENTS";
    }

    public static void MapM365DocumentEndpoints(this WebApplication app)
    {
        app.MapPost("/api/m365-documents/{entityType}/{entityId:long}/upload",
            async (string entityType,long entityId,HttpRequest req,HttpContext http,AppDbContext db,M365StorageService m365) =>
            {
                if(!m365.Enabled)
                    return Results.Json(new { message = "M365 document storage is not enabled." }, statusCode: 503);

                var form = await req.ReadFormAsync(http.RequestAborted);
                var file = form.Files.FirstOrDefault();
                if(file is null) return Results.BadRequest(new { message = "No file selected." });

                var uid = Convert.ToInt64(http.Items["AuthUserId"]);
                var actor=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid,http.RequestAborted);
                if(actor is null) return Results.Unauthorized();
                var permissionModule=FunctionFromPath(entityType);
                if(!await RbacService.CanAsync(db,actor,permissionModule,"UPLOAD"))return Results.Forbid();
                if(entityType.Equals("PROJECT",StringComparison.OrdinalIgnoreCase) &&
                   !await RbacService.IsAllScopeAsync(db,actor,"PROJECTS"))
                {
                    var projectIds=await RbacService.AllowedProjectIdsAsync(db,actor,"PROJECTS");
                    if(!projectIds.Contains(entityId)) return Results.Forbid();
                }

                var effectiveOrgUnitId=actor.OrgUnitId;
                if(entityType.Equals("CONTRACT",StringComparison.OrdinalIgnoreCase)&&entityId>0)
                    effectiveOrgUnitId=await db.Contracts.AsNoTracking().Where(x=>x.Id==entityId).Select(x=>x.OrgUnitId).FirstOrDefaultAsync(http.RequestAborted);
                else if(entityType.Equals("SUPPLIER",StringComparison.OrdinalIgnoreCase)&&entityId>0)
                    effectiveOrgUnitId=await db.Suppliers.AsNoTracking().Where(x=>x.Id==entityId).Select(x=>x.OrgUnitId).FirstOrDefaultAsync(http.RequestAborted);
                else if(entityType.Equals("PROJECT",StringComparison.OrdinalIgnoreCase)&&entityId>0)
                    effectiveOrgUnitId=await db.Projects.AsNoTracking().Where(x=>x.Id==entityId).Select(x=>x.OrgUnitId).FirstOrDefaultAsync(http.RequestAborted);
                if(effectiveOrgUnitId<=0)effectiveOrgUnitId=actor.OrgUnitId;

                var accessProbe=new DocumentRecord{OrgUnitId=effectiveOrgUnitId,EntityType=entityType.ToUpperInvariant(),EntityId=entityId,Category=(form["category"].FirstOrDefault()??$"{entityType.ToUpperInvariant()}_DOCUMENT").Trim().ToUpperInvariant(),UploadedBy=0};
                if(!await RbacService.CanAccessDocumentAsync(db,actor,accessProbe,"UPLOAD"))return Results.Forbid();

                var entityCode = (form["entityCode"].FirstOrDefault() ?? $"{entityType}-{entityId}").Trim();
                var folder = M365StorageService.BuildEntityFolder(entityType, entityCode);

                await using var stream = file.OpenReadStream();
                var stored = await m365.UploadAsync(
                    stream,
                    file.FileName,
                    file.ContentType ?? "application/octet-stream",
                    folder,
                    http.RequestAborted
                );

                var d = new DocumentRecord
                {
                    OrgUnitId = effectiveOrgUnitId,
                    EntityType = entityType.ToUpperInvariant(),
                    EntityId = entityId,
                    Category = (form["category"].FirstOrDefault() ?? $"{entityType.ToUpperInvariant()}_DOCUMENT").Trim().ToUpperInvariant(),
                    Name = (form["name"].FirstOrDefault() ?? entityCode).Trim(),
                    OriginalFileName = Path.GetFileName(file.FileName),
                    FilePath = "",
                    MimeType = file.ContentType ?? "application/octet-stream",
                    FileSize = stored.Size,
                    UploadedBy = uid,
                    UploadedAt = DateTime.UtcNow,
                    Status = "ACTIVE",
                    IsCurrent = true,
                    StorageProvider = "M365",
                    StorageDriveId = stored.DriveId,
                    StorageItemId = stored.ItemId,
                    StorageWebUrl = stored.WebUrl,
                    StoragePath = stored.Path
                };

                db.Documents.Add(d);
                await db.SaveChangesAsync(http.RequestAborted);

                return Results.Created(
                    $"/api/m365-documents/{d.Id}",
                    new { d.Id,d.OriginalFileName,d.FileSize,d.StorageProvider,d.StorageWebUrl,d.StoragePath }
                );
            });

        app.MapPost("/api/m365-documents/sync",
            async (string? folderPath,HttpContext http,AppDbContext db,M365StorageService m365) =>
            {
                if(!m365.Enabled)
                    return Results.Json(new { message = "M365 document storage is not enabled." }, statusCode: 503);

                var uid=Convert.ToInt64(http.Items["AuthUserId"]);
                var actor=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid,http.RequestAborted);
                if(actor is null)return Results.Unauthorized();
                var root=string.Equals(actor.Role,"ROOT",StringComparison.OrdinalIgnoreCase);
                var allowed=root||await RbacService.CanAsync(db,actor,"DOCUMENTS","UPLOAD")||
                    await RbacService.CanAsync(db,actor,"DOCUMENTS","CREATE")||
                    await RbacService.CanAsync(db,actor,"DOCUMENTS","EDIT")||
                    await RbacService.CanAsync(db,actor,"DOCUMENTS","FULL");
                if(!allowed)return Results.Forbid();

                var remote=await m365.ListFilesRecursiveAsync(folderPath,http.RequestAborted);
                var existingRows=await db.Documents
                    .Where(x=>x.StorageProvider=="M365"&&x.StorageItemId!=null)
                    .ToListAsync(http.RequestAborted);
                var existing=existingRows
                    .GroupBy(x=>x.StorageItemId!,StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x=>x.Key,x=>x.OrderByDescending(d=>d.UploadedAt).First(),StringComparer.OrdinalIgnoreCase);
                var uploaders=(await db.Users.AsNoTracking()
                    .Where(x=>x.Email!=null&&x.Email!="")
                    .Select(x=>new{x.Id,x.Email,x.OrgUnitId})
                    .ToListAsync(http.RequestAborted))
                    .GroupBy(x=>x.Email.Trim(),StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x=>x.Key,x=>x.First(),StringComparer.OrdinalIgnoreCase);
                var added=0;var updated=0;

                foreach(var file in remote)
                {
                    if(existing.TryGetValue(file.ItemId,out var doc))
                    {
                        doc.OriginalFileName=file.FileName;
                        doc.Name=Path.GetFileNameWithoutExtension(file.FileName);
                        doc.FileSize=file.Size;
                        doc.MimeType=file.MimeType;
                        doc.StorageDriveId=file.DriveId;
                        doc.StorageWebUrl=file.WebUrl;
                        doc.StoragePath=file.Path;
                        doc.Category=CategoryFromPath(file.Path);
                        doc.UploadedAt=file.LastModifiedAt;
                        doc.Status="ACTIVE";
                        doc.IsCurrent=true;
                        updated++;
                        continue;
                    }

                    var uploader=uploaders.TryGetValue(file.UploadedByEmail.Trim(),out var matched)?matched:null;
                    db.Documents.Add(new DocumentRecord
                    {
                        OrgUnitId=uploader is not null&&uploader.OrgUnitId>0?uploader.OrgUnitId:(actor.OrgUnitId>0?actor.OrgUnitId:1),
                        EntityType=FunctionFromPath(file.Path),EntityId=0,
                        Category=CategoryFromPath(file.Path),
                        Name=Path.GetFileNameWithoutExtension(file.FileName),
                        OriginalFileName=file.FileName,FilePath="",
                        MimeType=file.MimeType,FileSize=file.Size,
                        UploadedBy=uploader?.Id??uid,UploadedAt=file.LastModifiedAt,
                        Status="ACTIVE",IsCurrent=true,StorageProvider="M365",
                        StorageDriveId=file.DriveId,StorageItemId=file.ItemId,
                        StorageWebUrl=file.WebUrl,StoragePath=file.Path
                    });
                    added++;
                }

                await db.SaveChangesAsync(http.RequestAborted);
                return Results.Ok(new{scanned=remote.Count,added,updated,folder=folderPath??"/"});
            });

        app.MapGet("/api/m365-documents/{docId:long}/download",
            async (long docId,AppDbContext db,M365StorageService m365,IWebHostEnvironment env,HttpContext http) =>
            {
                var d = await db.Documents.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == docId && x.Status == "ACTIVE", http.RequestAborted);

                if(d is null) return Results.NotFound();

                var uid=Convert.ToInt64(http.Items["AuthUserId"]);
                var actor=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid,http.RequestAborted);
                if(actor is null) return Results.Unauthorized();
                if(!await RbacService.CanAccessDocumentAsync(db,actor,d,"DOWNLOAD")) return Results.Forbid();

                if(string.Equals(d.StorageProvider,"M365",StringComparison.OrdinalIgnoreCase))
                {
                    if(string.IsNullOrWhiteSpace(d.StorageItemId)) return Results.NotFound();

                    var f = await m365.DownloadAsync(
                        d.StorageItemId,
                        d.OriginalFileName,
                        d.MimeType,
                        http.RequestAborted
                    );

                    return Results.File(f.Stream,f.MimeType,f.FileName,enableRangeProcessing:true);
                }

                var decoded = Uri.UnescapeDataString(d.FilePath ?? "");
                var relative = decoded.TrimStart('/').Replace('/',Path.DirectorySeparatorChar);
                var full = Path.GetFullPath(Path.Combine(env.ContentRootPath,relative));
                var storageRoot = Path.GetFullPath(Path.Combine(env.ContentRootPath,"storage")) + Path.DirectorySeparatorChar;

                if(!full.StartsWith(storageRoot,StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
                    return Results.NotFound();

                return Results.File(full,d.MimeType ?? "application/octet-stream",d.OriginalFileName,enableRangeProcessing:true);
            });

        app.MapDelete("/api/m365-documents/{docId:long}",
            async (long docId,AppDbContext db,M365StorageService m365,IWebHostEnvironment env,HttpContext http) =>
            {
                var d = await db.Documents.FirstOrDefaultAsync(x => x.Id == docId,http.RequestAborted);
                if(d is null) return Results.NotFound();

                var uid=Convert.ToInt64(http.Items["AuthUserId"]);
                var actor=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid,http.RequestAborted);
                if(actor is null) return Results.Unauthorized();
                if(!await RbacService.CanAccessDocumentAsync(db,actor,d,"DELETE")) return Results.Forbid();

                if(string.Equals(d.StorageProvider,"M365",StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(d.StorageItemId) &&
                    m365.Enabled)
                {
                    await m365.DeleteAsync(d.StorageItemId,http.RequestAborted);
                }
                else
                {
                    try
                    {
                        var decoded = Uri.UnescapeDataString(d.FilePath ?? "");
                        var relative = decoded.TrimStart('/').Replace('/',Path.DirectorySeparatorChar);
                        var full = Path.GetFullPath(Path.Combine(env.ContentRootPath,relative));
                        var storageRoot = Path.GetFullPath(Path.Combine(env.ContentRootPath,"storage")) + Path.DirectorySeparatorChar;

                        if(full.StartsWith(storageRoot,StringComparison.OrdinalIgnoreCase) && File.Exists(full))
                            File.Delete(full);
                    }
                    catch {}
                }

                d.Status = "DEACTIVATED";
                d.IsCurrent = false;
                await db.SaveChangesAsync(http.RequestAborted);
                return Results.NoContent();
            });
    }
}
