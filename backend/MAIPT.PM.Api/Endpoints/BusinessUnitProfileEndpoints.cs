using MAIPT.PM.Api.Models;
using MAIPT.PM.Api.Data;
using Microsoft.EntityFrameworkCore;

public static class BusinessUnitProfileEndpoints
{
    public static void MapBusinessUnitProfileEndpoints(this WebApplication app)
    {
        app.MapGet("/api/orgunits/{orgUnitId:long}/attachments", async (long orgUnitId, HttpContext http, AppDbContext db) =>
        {
            var uid=Convert.ToInt64(http.Items["AuthUserId"]);
            var user=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid);
            if(user is null) return Results.Unauthorized();
            var dir=Path.Combine(app.Environment.ContentRootPath,"storage","orgunits",orgUnitId.ToString());
            if(!Directory.Exists(dir)) return Results.Ok(Array.Empty<object>());
            return Results.Ok(Directory.GetFiles(dir).Select(x=>new {
                name=Path.GetFileName(x), size=new FileInfo(x).Length, modifiedAt=File.GetLastWriteTimeUtc(x)
            }).OrderByDescending(x=>x.modifiedAt));
        });

        app.MapPost("/api/orgunits/{orgUnitId:long}/attachments", async (long orgUnitId, HttpRequest req, HttpContext http, AppDbContext db) =>
        {
            var uid=Convert.ToInt64(http.Items["AuthUserId"]);
            var user=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid);
            if(user is null) return Results.Unauthorized();
            if(!await RbacService.CanAsync(db,user,"SETTINGS","EDIT"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            if(!req.HasFormContentType) return Results.BadRequest(new{message="multipart/form-data required"});
            var form=await req.ReadFormAsync();
            var file=form.Files.FirstOrDefault();
            if(file is null || file.Length==0) return Results.BadRequest(new{message="File required"});
            if(file.Length>20*1024*1024) return Results.BadRequest(new{message="Maximum file size is 20 MB"});
            var ext=Path.GetExtension(file.FileName).ToLowerInvariant();
            var ok=new HashSet<string>{".pdf",".doc",".docx",".xls",".xlsx",".ppt",".pptx",".jpg",".jpeg",".png",".zip"};
            if(!ok.Contains(ext)) return Results.BadRequest(new{message="Unsupported file type"});
            var dir=Path.Combine(app.Environment.ContentRootPath,"storage","orgunits",orgUnitId.ToString());
            Directory.CreateDirectory(dir);
            var safe=Path.GetFileName(file.FileName);
            await using var fs=File.Create(Path.Combine(dir,safe));
            await file.CopyToAsync(fs);
            return Results.Ok(new{name=safe,size=file.Length});
        });

        app.MapDelete("/api/orgunits/{orgUnitId:long}/attachments/{name}", async (long orgUnitId,string name,HttpContext http,AppDbContext db) =>
        {
            var uid=Convert.ToInt64(http.Items["AuthUserId"]);
            var user=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid);
            if(user is null) return Results.Unauthorized();
            if(!await RbacService.CanAsync(db,user,"SETTINGS","EDIT"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            var safe=Path.GetFileName(name);
            var path=Path.Combine(app.Environment.ContentRootPath,"storage","orgunits",orgUnitId.ToString(),safe);
            if(File.Exists(path)) File.Delete(path);
            return Results.NoContent();
        });
    }
}
