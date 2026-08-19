using MAIPT.PM.Api.Data;
using MAIPT.PM.Api.Models;
using Microsoft.EntityFrameworkCore;
namespace MAIPT.PM.Api.Endpoints;
public sealed record AccessUpdateRequest(Dictionary<string,string[]> Permissions,Dictionary<string,AccessScope> Scopes);
public static class AccessControlEndpoints
{
 static async Task<AppUser?> Me(HttpContext h,AppDbContext db){var id=Convert.ToInt64(h.Items["AuthUserId"]);return await db.Users.Include(x=>x.OrgUnit).FirstOrDefaultAsync(x=>x.Id==id);}
 public static void MapAccessControlEndpoints(this WebApplication app)
 {
  app.MapGet("/api/access/me",async(HttpContext h,AppDbContext db)=>{var u=await Me(h,db);return u==null?Results.Unauthorized():Results.Ok(await RbacService.SummaryAsync(db,u));});
  app.MapGet("/api/access/catalog",async(HttpContext h,AppDbContext db)=>{var me=await Me(h,db);if(me==null)return Results.Unauthorized();if(!RbacService.IsAdmin(me))return Results.Forbid();var users=await db.Users.AsNoTracking().Include(x=>x.OrgUnit).OrderBy(x=>x.Name).Select(x=>new{x.Id,x.Name,x.Email,x.Role,x.Department,x.OrgUnitId,OrgUnit=x.OrgUnit==null?null:x.OrgUnit.Name}).ToListAsync();var orgUnits=await db.OrgUnits.AsNoTracking().Where(x=>x.Status=="ACTIVE").OrderBy(x=>x.Name).Select(x=>new{x.Id,x.Code,x.Name}).ToListAsync();return Results.Ok(new{modules=RbacService.Modules,actions=RbacService.Actions,scopeModes=new[]{"OWN","OWN_ORG","SELECTED_ORGS","SELECTED_USERS","ALL"},users,orgUnits});});
  app.MapGet("/api/access/users/{id:long}",async(long id,HttpContext h,AppDbContext db)=>{var me=await Me(h,db);if(me==null)return Results.Unauthorized();if(!RbacService.IsAdmin(me))return Results.Forbid();var u=await db.Users.Include(x=>x.OrgUnit).FirstOrDefaultAsync(x=>x.Id==id);return u==null?Results.NotFound():Results.Ok(await RbacService.SummaryAsync(db,u));});
  app.MapPut("/api/access/users/{id:long}",async(long id,AccessUpdateRequest input,HttpContext h,AppDbContext db)=>{var me=await Me(h,db);if(me==null)return Results.Unauthorized();if(!RbacService.IsAdmin(me))return Results.Forbid();var u=await db.Users.Include(x=>x.OrgUnit).FirstOrDefaultAsync(x=>x.Id==id);if(u==null)return Results.NotFound();await RbacService.SaveAsync(db,id,input.Permissions??new(),input.Scopes??new());return Results.Ok(await RbacService.SummaryAsync(db,u));});
 }
}
