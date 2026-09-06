using MAIPT.PM.Api.Data;
using MAIPT.PM.Api.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

public static class ITDomainEndpoints
{
 public static void MapITDomainEndpoints(this WebApplication app)
 {
  app.MapGet("/api/it-assets/domains/master-options", async(HttpContext http,AppDbContext db)=>{
   var uid=Convert.ToInt64(http.Items["AuthUserId"]);
   var actor=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid);
   if(actor is null)return Results.Unauthorized();

   var canItAssets=
      await RbacService.CanAsync(db,actor,"IT_ASSETS","VIEW")
      || await RbacService.CanAsync(db,actor,"IT_ASSETS","READ")
      || await RbacService.CanAsync(db,actor,"IT_ASSETS","EDIT");
   if(!canItAssets)
      return Results.Json(new{message="You do not have permission to access IT Assets."},statusCode:403);

   var canSuppliers=
      await RbacService.CanAsync(db,actor,"SUPPLIERS","VIEW")
      || await RbacService.CanAsync(db,actor,"SUPPLIERS","READ")
      || await RbacService.CanAsync(db,actor,"SUPPLIERS","EDIT");

   var canContracts=
      await RbacService.CanAsync(db,actor,"CONTRACTS","VIEW")
      || await RbacService.CanAsync(db,actor,"CONTRACTS","READ")
      || await RbacService.CanAsync(db,actor,"CONTRACTS","EDIT");

   var orgUnits=await db.OrgUnits.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED")
      .Where(x=>x.Status!="DEACTIVATED" && x.Status!="INACTIVE")
      .OrderBy(x=>x.Name)
      .Select(x=>new{x.Id,x.Code,x.Name,x.Type,x.Status})
      .ToListAsync();

   var users=await db.Users.AsNoTracking()
      .Where(x=>x.Status=="ACTIVE")
      .OrderBy(x=>x.Name)
      .Select(x=>new{x.Id,x.Name,x.Email,x.JobTitle,x.OrgUnitId,x.Status})
      .ToListAsync();

   var supplierRows=new List<object>();
   if(canSuppliers)
   {
      var supplierData=await db.Suppliers.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED")
         .Where(x=>x.Status!="DEACTIVATED" && x.Status!="INACTIVE")
         .OrderBy(x=>x.Name)
         .Select(x=>new{x.Id,x.Code,x.Name,x.Status})
         .ToListAsync();
      supplierRows=supplierData.Cast<object>().ToList();
   }

   var contracts=new List<object>();
   if(canContracts)
   {
      var q=db.Contracts.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED")
         .Where(x=>x.Status!="DEACTIVATED" && x.Status!="INACTIVE")
         .AsQueryable();

      if(!await RbacService.IsAllScopeAsync(db,actor,"CONTRACTS"))
      {
         var pids=await RbacService.AllowedProjectIdsAsync(db,actor,"CONTRACTS");
         q=q.Where(x=>x.CreatedByUserId==uid || pids.Contains(x.ProjectId));
      }

      var contractRows=await q
         .OrderByDescending(x=>x.Id)
         .Select(x=>new{x.Id,x.ContractNumber,x.Title,x.SupplierId,x.ProjectId,x.Status})
         .ToListAsync();
      contracts=contractRows.Cast<object>().ToList();
   }

   return Results.Ok(new{
      orgUnits,
      suppliers=supplierRows,
      contracts,
      users,
      permissions=new{suppliers=canSuppliers,contracts=canContracts}
   });
  });
  app.MapGet("/api/it-assets/domains/options", async(HttpContext http,AppDbContext db)=>{
   var uid=Convert.ToInt64(http.Items["AuthUserId"]);
   var user=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid);
   if(user is null)return Results.Unauthorized();

   var orgUnits=await db.OrgUnits.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED")
    .Where(x=>x.Status!="DEACTIVATED" && x.Status!="INACTIVE")
    .OrderBy(x=>x.Name).ToListAsync();

   var suppliers=await db.Suppliers.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED")
    .Where(x=>x.Status!="DEACTIVATED" && x.Status!="INACTIVE")
    .OrderBy(x=>x.Name).ToListAsync();

   var contracts=await db.Contracts.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED")
    .Where(x=>x.Status!="DEACTIVATED" && x.Status!="INACTIVE")
    .ToListAsync();

   var users=await db.Users.AsNoTracking()
    .Where(x=>x.Status=="ACTIVE")
    .OrderBy(x=>x.Name).ToListAsync();

   return Results.Ok(new{orgUnits,suppliers,contracts,users});
  });


  app.MapGet("/api/it-assets/domains", async(AppDbContext db)=>{
   await ITDomainSchema.EnsureAsync(db);
   return Results.Ok(await db.ITDomains.AsNoTracking().Where(x=>x.Status!="DEACTIVATED").OrderBy(x=>x.DomainName)
    .Select(x=>new{x.Id,x.OrgUnitId,x.SupplierId,x.ContractId,x.ManagerUserId,x.DomainName,x.DomainType,x.Purpose,x.Registrar,x.RegistrarUrl,x.LoginEmail,
      hasPassword=x.LoginPasswordProtected!="",x.RegistrationDate,x.ExpiryDate,x.RenewalStatus,x.AutoRenew,x.WhoisPrivacy,x.DnsProvider,x.NameServers,
      x.RegistrantOrganization,x.RegistrantContact,x.AdminContact,x.TechnicalContact,x.DeclarationNo,x.DeclarationDate,x.RecoveryEmail,x.MfaMethod,x.Status,x.Notes}).ToListAsync());
  });

  app.MapPost("/api/it-assets/domains", async(ITDomainRequest input,HttpContext http,AppDbContext db,[FromServices] Microsoft.AspNetCore.DataProtection.IDataProtectionProvider dp)=>{
   await ITDomainSchema.EnsureAsync(db);var uid=Convert.ToInt64(http.Items["AuthUserId"]);
   var name=(input.DomainName??"").Trim().ToLowerInvariant();if(name=="")return Results.BadRequest(new{message="Domain name is required."});
   if(await db.ITDomains.AnyAsync(x=>x.DomainName==name&&x.Status!="DEACTIVATED"))return Results.BadRequest(new{message="Domain already exists."});
   var p=dp.CreateProtector("MPMS.ITDomain.Password.v1");
   var x=new ITDomain{DomainName=name,DomainType=input.DomainType??"INTERNATIONAL",Purpose=input.Purpose??"",OrgUnitId=input.OrgUnitId,SupplierId=input.SupplierId,
    ContractId=input.ContractId,ManagerUserId=input.ManagerUserId,Registrar=input.Registrar??"",RegistrarUrl=input.RegistrarUrl??"",LoginEmail=input.LoginEmail??"",
    LoginPasswordProtected=string.IsNullOrWhiteSpace(input.LoginPassword)?"":p.Protect(input.LoginPassword),RegistrationDate=input.RegistrationDate,ExpiryDate=input.ExpiryDate,
    RenewalStatus=input.RenewalStatus??"MANUAL",AutoRenew=input.AutoRenew,WhoisPrivacy=input.WhoisPrivacy,DnsProvider=input.DnsProvider??"",NameServers=input.NameServers??"",
    RegistrantOrganization=input.RegistrantOrganization??"",RegistrantContact=input.RegistrantContact??"",AdminContact=input.AdminContact??"",TechnicalContact=input.TechnicalContact??"",
    DeclarationNo=input.DeclarationNo??"",DeclarationDate=input.DeclarationDate,RecoveryEmail=input.RecoveryEmail??"",MfaMethod=input.MfaMethod??"",Notes=input.Notes??"",Status="ACTIVE",
    CreatedByUserId=uid,UpdatedByUserId=uid,CreatedAt=DateTime.UtcNow,UpdatedAt=DateTime.UtcNow};db.ITDomains.Add(x);await db.SaveChangesAsync();return Results.Created($"/api/it-assets/domains/{x.Id}",new{x.Id});
  });

  app.MapPut("/api/it-assets/domains/{id:long}", async(long id,ITDomainRequest input,HttpContext http,AppDbContext db,[FromServices] Microsoft.AspNetCore.DataProtection.IDataProtectionProvider dp)=>{
   await ITDomainSchema.EnsureAsync(db);var uid=Convert.ToInt64(http.Items["AuthUserId"]);var x=await db.ITDomains.FindAsync(id);if(x is null||x.Status=="DEACTIVATED")return Results.NotFound();
   x.DomainName=(input.DomainName??"").Trim().ToLowerInvariant();x.DomainType=input.DomainType??"INTERNATIONAL";x.Purpose=input.Purpose??"";x.OrgUnitId=input.OrgUnitId;x.SupplierId=input.SupplierId;x.ContractId=input.ContractId;x.ManagerUserId=input.ManagerUserId;
   x.Registrar=input.Registrar??"";x.RegistrarUrl=input.RegistrarUrl??"";x.LoginEmail=input.LoginEmail??"";if(!string.IsNullOrWhiteSpace(input.LoginPassword))x.LoginPasswordProtected=dp.CreateProtector("MPMS.ITDomain.Password.v1").Protect(input.LoginPassword);
   x.RegistrationDate=input.RegistrationDate;x.ExpiryDate=input.ExpiryDate;x.RenewalStatus=input.RenewalStatus??"MANUAL";x.AutoRenew=input.AutoRenew;x.WhoisPrivacy=input.WhoisPrivacy;x.DnsProvider=input.DnsProvider??"";x.NameServers=input.NameServers??"";
   x.RegistrantOrganization=input.RegistrantOrganization??"";x.RegistrantContact=input.RegistrantContact??"";x.AdminContact=input.AdminContact??"";x.TechnicalContact=input.TechnicalContact??"";x.DeclarationNo=input.DeclarationNo??"";x.DeclarationDate=input.DeclarationDate;
   x.RecoveryEmail=input.RecoveryEmail??"";x.MfaMethod=input.MfaMethod??"";x.Notes=input.Notes??"";x.UpdatedByUserId=uid;x.UpdatedAt=DateTime.UtcNow;await db.SaveChangesAsync();return Results.Ok(new{x.Id});
  });

  app.MapDelete("/api/it-assets/domains/{id:long}", async(long id,HttpContext http,AppDbContext db)=>{
   await ITDomainSchema.EnsureAsync(db);var x=await db.ITDomains.FindAsync(id);if(x is null)return Results.NotFound();x.Status="DEACTIVATED";x.UpdatedByUserId=Convert.ToInt64(http.Items["AuthUserId"]);x.UpdatedAt=DateTime.UtcNow;await db.SaveChangesAsync();return Results.Ok(new{x.Id});
  });

  app.MapGet("/api/it-assets/domains/{id:long}/password", async(long id,HttpContext http,AppDbContext db,[FromServices] Microsoft.AspNetCore.DataProtection.IDataProtectionProvider dp)=>{
   await ITDomainSchema.EnsureAsync(db);
   var uid=Convert.ToInt64(http.Items["AuthUserId"]);
   var actor=await db.Users.AsNoTracking().FirstOrDefaultAsync(u=>u.Id==uid);
   if(actor is null)return Results.Unauthorized();
   // Revealing a stored credential requires more than read access to the IT Assets module.
   if(!RbacService.IsRoot(actor)
      && !await RbacService.CanAsync(db,actor,"IT_ASSETS","EDIT")
      && !await RbacService.CanAsync(db,actor,"IT_ASSETS","FULL"))
     return Results.StatusCode(StatusCodes.Status403Forbidden);
   var x=await db.ITDomains.AsNoTracking().FirstOrDefaultAsync(d=>d.Id==id&&d.Status!="DEACTIVATED");if(x is null)return Results.NotFound();
   var pw=string.IsNullOrWhiteSpace(x.LoginPasswordProtected)?"":dp.CreateProtector("MPMS.ITDomain.Password.v1").Unprotect(x.LoginPasswordProtected);
   http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("ITDomain")
     .LogInformation("User {UserId} revealed registrar password for domain {DomainId}",uid,id);
   return Results.Ok(new{password=pw});
  });

  app.MapGet("/api/it-assets/domains/{id:long}/attachments", async(long id,AppDbContext db)=>Results.Ok(await db.Documents.AsNoTracking().Where(x=>x.EntityType=="IT_DOMAIN"&&x.EntityId==id&&x.Status=="ACTIVE").OrderByDescending(x=>x.UploadedAt)
   .Select(x=>new{x.Id,x.OriginalFileName,x.FileSize,x.UploadedAt,downloadUrl=$"/api/it-assets/domains/{id}/attachment/{x.Id}/download"}).ToListAsync()));

  app.MapPost("/api/it-assets/domains/{id:long}/attachment", async(long id,HttpRequest req,HttpContext http,AppDbContext db,IWebHostEnvironment env)=>{
   if(!req.HasFormContentType)return Results.BadRequest();var form=await req.ReadFormAsync();var file=form.Files.FirstOrDefault();if(file is null||file.Length==0)return Results.BadRequest(new{message="File required."});
   var ext=Path.GetExtension(file.FileName).ToLowerInvariant();if(!new[]{".pdf",".doc",".docx",".xls",".xlsx",".jpg",".jpeg",".png",".zip"}.Contains(ext))return Results.BadRequest(new{message="Unsupported file type."});
   var dir=Path.Combine(env.ContentRootPath,"storage","documents","IT_DOMAIN");Directory.CreateDirectory(dir);var name=$"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{ext}";await using(var fs=File.Create(Path.Combine(dir,name)))await file.CopyToAsync(fs);
   var rec=new DocumentRecord{OrgUnitId=1,EntityType="IT_DOMAIN",EntityId=id,Category="IT_DOMAIN",Name="Domain document",OriginalFileName=Path.GetFileName(file.FileName),FilePath=$"storage/documents/IT_DOMAIN/{name}",MimeType=file.ContentType??"application/octet-stream",FileSize=file.Length,UploadedBy=Convert.ToInt64(http.Items["AuthUserId"]),UploadedAt=DateTime.UtcNow,Status="ACTIVE",IsCurrent=true};
   db.Documents.Add(rec);await db.SaveChangesAsync();return Results.Created("",new{rec.Id,rec.OriginalFileName});
  });

  app.MapGet("/api/it-assets/domains/{id:long}/attachment/{docId:long}/download", async(long id,long docId,AppDbContext db,IWebHostEnvironment env)=>{
   var r=await db.Documents.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==docId&&x.EntityType=="IT_DOMAIN"&&x.EntityId==id&&x.Status=="ACTIVE");if(r is null)return Results.NotFound();var full=Path.GetFullPath(Path.Combine(env.ContentRootPath,(r.FilePath??"").TrimStart('/').Replace('/',Path.DirectorySeparatorChar)));return File.Exists(full)?Results.File(full,r.MimeType??"application/octet-stream",r.OriginalFileName):Results.NotFound();
  });
 }
}
public sealed record ITDomainRequest(long? OrgUnitId,long? SupplierId,long? ContractId,long? ManagerUserId,string? DomainName,string? DomainType,string? Purpose,string? Registrar,string? RegistrarUrl,string? LoginEmail,string? LoginPassword,DateOnly? RegistrationDate,DateOnly? ExpiryDate,string? RenewalStatus,bool AutoRenew,bool WhoisPrivacy,string? DnsProvider,string? NameServers,string? RegistrantOrganization,string? RegistrantContact,string? AdminContact,string? TechnicalContact,string? DeclarationNo,DateOnly? DeclarationDate,string? RecoveryEmail,string? MfaMethod,string? Notes);
