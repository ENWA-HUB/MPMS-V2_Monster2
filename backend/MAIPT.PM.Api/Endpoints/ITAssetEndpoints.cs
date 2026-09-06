using MAIPT.PM.Api.Data;
using MAIPT.PM.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text;

namespace MAIPT.PM.Api.Endpoints;

public static class ITAssetEndpoints
{
    static async Task<(string Mode,long[] OrgIds)> StrictItScopeAsync(AppDbContext db,AppUser actor)
    {
        var sc=await RbacService.GetScopeAsync(db,actor,"IT_ASSETS");
        var mode=(sc.Mode??"OWN").Trim().ToUpperInvariant();
        if(mode=="ALL") return ("ALL",await db.OrgUnits.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED").Select(x=>x.Id).ToArrayAsync());
        if(mode=="OWN") return ("OWN",Array.Empty<long>());
        if(mode=="OWN_ORG") return ("OWN_ORG",actor.OrgUnitId>0?[actor.OrgUnitId]:Array.Empty<long>());
        if(mode=="SELECTED_ORGS")
        {
            var vals=sc.Values??[];
            var ids=await db.OrgUnits.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED")
                .Where(x=>vals.Contains(x.Code)||vals.Contains(x.Id.ToString()))
                .Select(x=>x.Id).ToArrayAsync();
            return ("SELECTED_ORGS",ids);
        }
        return ("OWN",Array.Empty<long>());
    }


    static async Task<AppUser?> Me(HttpContext h, AppDbContext db)
    {
        if(!h.Items.TryGetValue("AuthUserId",out var raw) || raw is null) return null;
        return await db.Users.FirstOrDefaultAsync(x=>x.Id==Convert.ToInt64(raw));
    }

    static async Task<bool> CanUseOrgAsync(AppDbContext db,AppUser actor,long? orgUnitId)
    {
        if(!orgUnitId.HasValue) return true;
        var (mode,orgIds)=await StrictItScopeAsync(db,actor);
        if(mode=="ALL") return true;
        if(mode=="OWN") return actor.OrgUnitId>0 && orgUnitId.Value==actor.OrgUnitId;
        return orgIds.Contains(orgUnitId.Value);
    }

    static async Task<bool> CanAccessAssetAsync(AppDbContext db,AppUser actor,ITAsset asset)
    {
        var (mode,orgIds)=await StrictItScopeAsync(db,actor);
        if(mode=="ALL") return true;
        if(mode=="OWN") return asset.CreatedByUserId==actor.Id;
        return asset.OrgUnitId.HasValue && orgIds.Contains(asset.OrgUnitId.Value);
    }

    static async Task<bool> CanAccessOwnedOrgAsync(AppDbContext db,AppUser actor,long? createdByUserId,long? orgUnitId)
    {
        var (mode,orgIds)=await StrictItScopeAsync(db,actor);
        return mode=="ALL" || createdByUserId==actor.Id ||
               (orgUnitId.HasValue && orgIds.Contains(orgUnitId.Value));
    }

    static async Task<string> NextCode(AppDbContext db, string kind)
    {
        var pfx = kind.ToUpperInvariant() switch
        {
            "ASSET"=>"AST","LICENSE"=>"LIC","SERVICE"=>"SVC","HANDOVER"=>"HO",
            _=>"IT"
        };
        var n = kind.ToUpperInvariant() switch
        {
            "ASSET"=>await db.ITAssets.CountAsync()+1,
            "LICENSE"=>await db.ITLicenses.CountAsync()+1,
            "SERVICE"=>await db.ITServices.CountAsync()+1,
            "HANDOVER"=>await db.ITAssetAssignments.CountAsync()+1,
            _=>1
        };
        return $"{pfx}-{DateTime.Today:yyyy}-{n:0000}";
    }

    public static void MapITAssetEndpoints(this WebApplication app)
    {
        app.MapGet("/api/it-assets/overview", async(HttpContext http,AppDbContext db)=>
        {
            var uid=Convert.ToInt64(http.Items["AuthUserId"]);
            var actor=await db.Users.AsNoTracking().FirstAsync(x=>x.Id==uid);
            var today=DateOnly.FromDateTime(DateTime.Today);

            var allScope=await RbacService.IsAllScopeAsync(db,actor,"IT_ASSETS");
            var orgIds=allScope ? Array.Empty<long>() : await RbacService.AllowedOrgIdsAsync(db,actor,"IT_ASSETS");
            var projectIds=allScope ? Array.Empty<long>() : await RbacService.AllowedProjectIdsAsync(db,actor,"IT_ASSETS");

            var orgUnits=await db.OrgUnits.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED")
                .Where(x=>allScope || orgIds.Contains(x.Id) || x.Id==actor.OrgUnitId)
                .Select(x=>new{x.Id,x.Code,x.Name})
                .OrderBy(x=>x.Name)
                .ToListAsync();

            var userIds=allScope || orgIds.Length==0
                ? Array.Empty<long>()
                : await db.Users.AsNoTracking()
                    .Where(x=>orgIds.Contains(x.OrgUnitId))
                    .Select(x=>x.Id)
                    .ToArrayAsync();

            var assetQ=db.ITAssets.AsNoTracking().Where(x=>x.Status!="DEACTIVATED").AsQueryable();
            if(!allScope)
                assetQ=assetQ.Where(x=>
                    x.CreatedByUserId==uid ||
                    (x.OrgUnitId.HasValue && orgIds.Contains(x.OrgUnitId.Value)) ||
                    (x.CreatedByUserId.HasValue && userIds.Contains(x.CreatedByUserId.Value)) ||
                    (x.ProjectId.HasValue && projectIds.Contains(x.ProjectId.Value)));

            var assets=await assetQ.Select(x=>new{
                x.Id,x.Status,x.PurchasePrice,x.Currency,x.WarrantyExpiry,
                x.Category,x.Location,x.OrgUnitId,x.Department
            }).ToListAsync();

            var assetIds=assets.Select(x=>x.Id).ToArray();

            var licenseQ=db.ITLicenses.AsNoTracking().Where(x=>x.Status!="DEACTIVATED").AsQueryable();
            if(!allScope)
                licenseQ=licenseQ.Where(x=>
                    x.CreatedByUserId==uid ||
                    (x.OrgUnitId.HasValue && orgIds.Contains(x.OrgUnitId.Value)) ||
                    (x.CreatedByUserId.HasValue && userIds.Contains(x.CreatedByUserId.Value)));

            var licenses=await licenseQ.Select(x=>new{
                x.Id,x.ProductName,x.Vendor,x.Quantity,x.AssignedQuantity,
                x.ExpiryDate,x.Cost,x.Currency,x.Status,x.ActivationStatus,x.OrgUnitId
            }).ToListAsync();

            var serviceQ=db.ITServices.AsNoTracking().Where(x=>x.Status!="DEACTIVATED").AsQueryable();
            if(!allScope)
                serviceQ=serviceQ.Where(x=>
                    x.CreatedByUserId==uid ||
                    (x.CreatedByUserId.HasValue && userIds.Contains(x.CreatedByUserId.Value)));

            var services=await serviceQ.Select(x=>new{
                x.Id,x.ServiceName,x.ServiceType,x.Provider,x.Department,
                x.ExpiryDate,x.MonthlyCost,x.AnnualCost,x.Currency,x.Status
            }).ToListAsync();

            var maintQ=db.ITMaintenances.AsNoTracking().Where(x=>x.Status!="DEACTIVATED").AsQueryable();
            if(!allScope)
                maintQ=maintQ.Where(x=>x.CreatedByUserId==uid || assetIds.Contains(x.AssetId));

            var maintenance=await maintQ.Select(x=>new{
                x.Id,x.AssetId,x.Cost,x.Currency,x.Status,x.OpenDate,x.CloseDate
            }).ToListAsync();

            var assignmentQ=db.ITAssetAssignments.AsNoTracking().Where(x=>x.Status!="DEACTIVATED").Where(x=>x.Status=="ACTIVE");
            if(!allScope)
                assignmentQ=assignmentQ.Where(x=>
                    x.CreatedByUserId==uid ||
                    assetIds.Contains(x.AssetId) ||
                    userIds.Contains(x.UserId));

            var assignments=await assignmentQ.Select(x=>new{x.AssetId,x.UserId}).ToListAsync();
            var assignedAssetIds=assignments.Select(x=>x.AssetId).Distinct().ToHashSet();

            var businessUnits=orgUnits.Select(o=>
            {
                var buAssets=assets.Where(x=>x.OrgUnitId==o.Id ||
                    (!x.OrgUnitId.HasValue &&
                     (string.Equals(x.Department,o.Name,StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(x.Department,o.Code,StringComparison.OrdinalIgnoreCase)))).ToList();

                var buAssetIds=buAssets.Select(x=>x.Id).ToHashSet();
                var buLicenses=licenses.Where(x=>x.OrgUnitId==o.Id).ToList();
                var buServices=services.Where(x=>
                    string.Equals(x.Department,o.Name,StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.Department,o.Code,StringComparison.OrdinalIgnoreCase)).ToList();

                var assetCost=buAssets.Sum(x=>x.PurchasePrice);
                var licenseCost=buLicenses.Sum(x=>x.Cost);
                var serviceCost=buServices.Sum(x=>x.AnnualCost);
                var maintenanceCost=maintenance.Where(x=>buAssetIds.Contains(x.AssetId)).Sum(x=>x.Cost);

                return new{
                    id=o.Id,code=o.Code,name=o.Name,
                    assets=buAssets.Count,
                    assignedAssets=buAssets.Count(x=>assignedAssetIds.Contains(x.Id) || x.Status=="ASSIGNED" || x.Status=="IN_USE"),
                    availableAssets=buAssets.Count(x=>x.Status=="IN_STOCK" || x.Status=="AVAILABLE"),
                    assetCost,
                    licenseRecords=buLicenses.Count,
                    licenseSeats=buLicenses.Sum(x=>x.Quantity),
                    assignedLicenseSeats=buLicenses.Sum(x=>x.AssignedQuantity),
                    availableLicenseSeats=buLicenses.Sum(x=>Math.Max(0,x.Quantity-x.AssignedQuantity)),
                    licenseCost,
                    services=buServices.Count,
                    serviceCost,
                    maintenanceCost,
                    totalCost=assetCost+licenseCost+serviceCost+maintenanceCost
                };
            }).OrderByDescending(x=>x.totalCost).ToList();

            var licenseSeats=licenses.Sum(x=>x.Quantity);
            var assignedLicenseSeats=licenses.Sum(x=>x.AssignedQuantity);
            var availableLicenseSeats=licenses.Sum(x=>Math.Max(0,x.Quantity-x.AssignedQuantity));

            var licenseProducts=licenses
                .GroupBy(x=>string.IsNullOrWhiteSpace(x.ProductName)?"Unspecified":x.ProductName)
                .Select(g=>new{
                    name=g.Key,
                    quantity=g.Sum(x=>x.Quantity),
                    assigned=g.Sum(x=>x.AssignedQuantity),
                    available=g.Sum(x=>Math.Max(0,x.Quantity-x.AssignedQuantity)),
                    cost=g.Sum(x=>x.Cost)
                })
                .OrderByDescending(x=>x.quantity)
                .Take(8)
                .ToList();

            var assetValue=assets.Sum(x=>x.PurchasePrice);
            var licenseCost=licenses.Sum(x=>x.Cost);
            var serviceCost=services.Sum(x=>x.AnnualCost);
            var maintenanceCost=maintenance.Sum(x=>x.Cost);
            var totalITCost=assetValue+licenseCost+serviceCost+maintenanceCost;

            return Results.Ok(new{
                totalAssets=assets.Count,
                assigned=assets.Count(x=>assignedAssetIds.Contains(x.Id) || x.Status=="ASSIGNED" || x.Status=="IN_USE"),
                inStock=assets.Count(x=>x.Status=="IN_STOCK"||x.Status=="AVAILABLE"),
                repair=assets.Count(x=>x.Status=="REPAIR"),
                retired=assets.Count(x=>x.Status=="RETIRED"||x.Status=="DISPOSED"),
                assetValue,
                activeAssignments=assignments.Count,
                openMaintenance=maintenance.Count(x=>x.Status!="CLOSED"),
                warrantyExpiring=assets.Count(x=>x.WarrantyExpiry.HasValue&&x.WarrantyExpiry.Value>=today&&x.WarrantyExpiry.Value<=today.AddDays(60)),

                licenseRecords=licenses.Count,
                licenseSeats,
                assignedLicenseSeats,
                availableLicenseSeats,
                activeLicenses=licenses.Count(x=>x.Status=="ACTIVE" || x.ActivationStatus=="ACTIVE" || x.ActivationStatus=="ACTIVATED"),
                licensesExpiring=licenses.Count(x=>x.ExpiryDate.HasValue&&x.ExpiryDate.Value>=today&&x.ExpiryDate.Value<=today.AddDays(60)),
                licensesExpired=licenses.Count(x=>x.ExpiryDate.HasValue&&x.ExpiryDate.Value<today),

                activeServices=services.Count(x=>x.Status=="ACTIVE"),
                servicesExpiring=services.Count(x=>x.ExpiryDate.HasValue&&x.ExpiryDate.Value>=today&&x.ExpiryDate.Value<=today.AddDays(90)),

                licenseCost,
                serviceCost,
                maintenanceCost,
                totalITCost,

                categories=assets.GroupBy(x=>string.IsNullOrWhiteSpace(x.Category)?"Other":x.Category)
                    .Select(g=>new{name=g.Key,value=g.Count()})
                    .OrderByDescending(x=>x.value)
                    .ToList(),

                licenseProducts,
                businessUnits,

                attention=new{
                    warrantyExpiring=assets.Count(x=>x.WarrantyExpiry.HasValue&&x.WarrantyExpiry.Value>=today&&x.WarrantyExpiry.Value<=today.AddDays(60)),
                    licensesExpiring=licenses.Count(x=>x.ExpiryDate.HasValue&&x.ExpiryDate.Value>=today&&x.ExpiryDate.Value<=today.AddDays(60)),
                    licensesExpired=licenses.Count(x=>x.ExpiryDate.HasValue&&x.ExpiryDate.Value<today),
                    openMaintenance=maintenance.Count(x=>x.Status!="CLOSED"),
                    unassignedAssets=assets.Count(x=>!assignedAssetIds.Contains(x.Id) && x.Status!="RETIRED" && x.Status!="DISPOSED"),
                    overAssignedLicenses=licenses.Count(x=>x.AssignedQuantity>x.Quantity)
                }
            });
        });

        app.MapGet("/api/it-assets/options", async(HttpContext http,AppDbContext db)=>
        {
            var uid=Convert.ToInt64(http.Items["AuthUserId"]);
            var actor=await db.Users.AsNoTracking().FirstAsync(x=>x.Id==uid);
            var (mode,orgIds)=await StrictItScopeAsync(db,actor);
            var all=mode=="ALL";

            var uq=db.Users.AsNoTracking().Where(x=>x.Status=="ACTIVE");
            var spq=db.Suppliers.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED").Where(x=>x.Status=="ACTIVE");
            var pq=db.Projects.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED").AsQueryable();

            if(!all)
            {
                uq=uq.Where(x=>x.Id==uid||orgIds.Contains(x.OrgUnitId));
                spq=spq.Where(x=>x.CreatedByUserId==uid||orgIds.Contains(x.OrgUnitId));
                pq=pq.Where(x=>x.CreatedByUserId==uid||orgIds.Contains(x.OrgUnitId)||
                    db.ProjectOrgUnits.Any(po=>po.ProjectId==x.Id&&orgIds.Contains(po.OrgUnitId)));
            }

            var pids=await pq.Select(x=>x.Id).ToArrayAsync();

            return Results.Ok(new{
                users=await uq.OrderBy(x=>x.Name).Select(x=>new{x.Id,x.Name,x.Email,x.Department,x.JobTitle,x.OrgUnitId}).ToListAsync(),
                orgUnits=await db.OrgUnits.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED")
                    .Where(x=>x.Status=="ACTIVE" && (all || orgIds.Contains(x.Id) || x.Id==actor.OrgUnitId))
                    .OrderBy(x=>x.Name)
                    .Select(x=>new{x.Id,x.Code,x.Name,x.Status}).ToListAsync(),
                suppliers=await spq.OrderBy(x=>x.Name).Select(x=>new{x.Id,x.Name,x.Code}).ToListAsync(),
                contracts=await db.Contracts.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED").Where(x=>pids.Contains(x.ProjectId)).OrderByDescending(x=>x.StartDate).Select(x=>new{x.Id,x.ContractNumber,x.Title,x.SupplierId}).ToListAsync(),
                projects=await pq.OrderBy(x=>x.Name).Select(x=>new{x.Id,x.Code,x.Name}).ToListAsync(),
                budgets=await db.BudgetLines.AsNoTracking().Where(x=>pids.Contains(x.ProjectId)).OrderBy(x=>x.Code).Select(x=>new{x.Id,x.Code,x.Description,x.ProjectId}).ToListAsync()
            });
        });

        app.MapGet("/api/it-assets/assets", async(HttpContext http,AppDbContext db)=>
        {
            var uid=Convert.ToInt64(http.Items["AuthUserId"]);
            var actor=await db.Users.AsNoTracking().FirstAsync(x=>x.Id==uid);
            var (mode,orgIds)=await StrictItScopeAsync(db,actor);

            var q=db.ITAssets.AsNoTracking().Where(x=>x.Status!="DEACTIVATED")
                .Include(x=>x.Supplier)
                .Include(x=>x.Contract)
                .Include(x=>x.Project)
                .AsQueryable();

            if(mode=="OWN")
                q=q.Where(x=>x.CreatedByUserId==uid);
            else if(mode!="ALL")
                q=q.Where(x=>x.OrgUnitId.HasValue && orgIds.Contains(x.OrgUnitId.Value));

            return Results.Ok(await q.OrderByDescending(x=>x.UpdatedAt)
                .Select(x=>new{
                    x.Id,x.AssetCode,x.Category,x.AssetName,x.Brand,x.Model,x.SerialNumber,x.Specification,
                    x.SupplierId,x.ContractId,x.ProjectId,x.BudgetLineId,x.OrgUnitId,
                    x.PurchaseDate,x.PurchasePrice,x.Currency,x.WarrantyExpiry,x.Location,x.Department,
                    x.Condition,x.Status,x.Notes,
                    Supplier=x.Supplier!=null?x.Supplier.Name:null,
                    Contract=x.Contract!=null?x.Contract.ContractNumber:null,
                    Project=x.Project!=null?x.Project.Name:null
                }).ToListAsync());
        });

        app.MapPost("/api/it-assets/assets", async(ITAsset input,HttpContext http,AppDbContext db)=>{
            var actor=await Me(http,db);if(actor is null)return Results.Unauthorized();
            if(!await CanUseOrgAsync(db,actor,input.OrgUnitId))return Results.Forbid();
            input.Id=0; input.AssetCode=string.IsNullOrWhiteSpace(input.AssetCode)?await NextCode(db,"ASSET"):input.AssetCode.Trim();
            input.Supplier=null;input.Contract=null;input.Project=null;input.BudgetLine=null;
            db.ITAssets.Add(input);await db.SaveChangesAsync();return Results.Created($"/api/it-assets/assets/{input.Id}",new{input.Id,input.AssetCode});
        });

        app.MapPut("/api/it-assets/assets/{id:long}", async(long id,ITAsset input,HttpContext http,AppDbContext db)=>{
            var x=await db.ITAssets.FindAsync(id);if(x is null)return Results.NotFound();
            var actor=await Me(http,db);if(actor is null)return Results.Unauthorized();
            if(!await CanAccessAssetAsync(db,actor,x) || !await CanUseOrgAsync(db,actor,input.OrgUnitId))return Results.Forbid();
            x.Category=input.Category;x.AssetName=input.AssetName;x.Brand=input.Brand;x.Model=input.Model;x.SerialNumber=input.SerialNumber;x.Specification=input.Specification;
            x.SupplierId=input.SupplierId;x.ContractId=input.ContractId;x.ProjectId=input.ProjectId;x.BudgetLineId=input.BudgetLineId;x.PurchaseDate=input.PurchaseDate;
            x.PurchasePrice=input.PurchasePrice;x.Currency=input.Currency;x.WarrantyExpiry=input.WarrantyExpiry;x.Location=input.Location;x.OrgUnitId=input.OrgUnitId;x.Department=input.Department;
            x.Condition=input.Condition;x.Status=input.Status;x.Notes=input.Notes;await db.SaveChangesAsync();return Results.Ok(new{x.Id});
        });
        app.MapDelete("/api/it-assets/assets/{id:long}", async(long id,HttpContext http,AppDbContext db)=>{
            var x=await db.ITAssets.FindAsync(id); if(x is null)return Results.NotFound(new{message="Asset not found."});
            var actor=await Me(http,db);if(actor is null)return Results.Unauthorized();
            if(!await CanAccessAssetAsync(db,actor,x))return Results.Forbid();
            x.Status="DEACTIVATED"; x.UpdatedAt=DateTime.UtcNow;
            if(http.Items["AuthUserId"] is not null)x.UpdatedByUserId=Convert.ToInt64(http.Items["AuthUserId"]);
            await db.SaveChangesAsync(); return Results.Ok(new{x.Id,x.Status,message="Asset deactivated."});
        });
        app.MapGet("/api/it-assets/assignments", async(HttpContext http,AppDbContext db)=>
        {
            var uid=Convert.ToInt64(http.Items["AuthUserId"]);
            var actor=await db.Users.AsNoTracking().FirstAsync(x=>x.Id==uid);
            var (mode,orgIds)=await StrictItScopeAsync(db,actor);

            var q=db.ITAssetAssignments.AsNoTracking().Where(x=>x.Status!="DEACTIVATED")
                .Include(x=>x.Asset)
                .Include(x=>x.User)
                .AsQueryable();

            if(mode=="OWN")
                q=q.Where(x=>x.CreatedByUserId==uid);
            else if(mode!="ALL")
                q=q.Where(x=>x.Asset!=null &&
                    x.Asset.OrgUnitId.HasValue &&
                    orgIds.Contains(x.Asset.OrgUnitId.Value));

            return Results.Ok(await q.OrderByDescending(x=>x.AssignmentDate)
                .Select(x=>new{
                    x.Id,x.AssetId,
                    AssetCode=x.Asset!.AssetCode,
                    AssetName=x.Asset!.AssetName,
                    AssetOrgUnitId=x.Asset!.OrgUnitId,
                    x.UserId,User=x.User!.Name,
                    x.AssignmentType,x.AssignmentDate,x.ReturnDate,
                    x.FromLocation,x.ToLocation,x.ConditionOut,x.ConditionIn,
                    x.Accessories,x.HandoverNo,x.Notes,x.Status,
                    x.ReceivedByUserId,x.ReturnReason,x.ReturnLocation,
                    x.AccessoriesReturned,x.MissingItems
                }).ToListAsync());
        });

        app.MapPost("/api/it-assets/assignments", async(ITAssetAssignment input,HttpContext http,AppDbContext db)=>{
            var asset=await db.ITAssets.FindAsync(input.AssetId);if(asset is null)return Results.BadRequest(new{message="Asset not found."});
            var actor=await Me(http,db);if(actor is null)return Results.Unauthorized();
            if(!await CanAccessAssetAsync(db,actor,asset))return Results.Forbid();
            if(!await db.Users.AnyAsync(x=>x.Id==input.UserId))return Results.BadRequest(new{message="User not found."});
            if(await db.ITAssetAssignments.AnyAsync(x=>x.AssetId==input.AssetId&&x.Status=="ACTIVE"))
                return Results.BadRequest(new{message="Asset is already assigned."});
            input.Id=0;input.Asset=null;input.User=null;if(string.IsNullOrWhiteSpace(input.HandoverNo))input.HandoverNo=await NextCode(db,"HANDOVER");
            if(input.AssignmentDate==default)input.AssignmentDate=DateOnly.FromDateTime(DateTime.Today);
            input.Status="ACTIVE";db.ITAssetAssignments.Add(input);asset.Status="ASSIGNED";asset.Location=input.ToLocation;
            await db.SaveChangesAsync();return Results.Created($"/api/it-assets/assignments/{input.Id}",new{input.Id,input.HandoverNo});
        });

        app.MapPost("/api/it-assets/assignments/{id:long}/return", async(long id,ITAssetReturnRequest input,HttpContext http,AppDbContext db)=>{
            var x=await db.ITAssetAssignments.Include(a=>a.Asset).FirstOrDefaultAsync(a=>a.Id==id);
            if(x is null)return Results.NotFound();
            var actor=await Me(http,db);if(actor is null)return Results.Unauthorized();
            if(x.Asset is null || !await CanAccessAssetAsync(db,actor,x.Asset))return Results.Forbid();
            if(x.Status!="ACTIVE")return Results.BadRequest(new{message="Assignment already closed."});
            x.ReturnDate=input.ReturnDate??DateOnly.FromDateTime(DateTime.Today);
            x.ConditionIn=string.IsNullOrWhiteSpace(input.ConditionIn)?"GOOD":input.ConditionIn!;
            x.ReceivedByUserId=input.ReceivedByUserId;
            x.ReturnReason=input.ReturnReason?.Trim()??"";
            x.ReturnLocation=input.Location?.Trim()??x.FromLocation;
            x.AccessoriesReturned=input.AccessoriesReturned?.Trim()??"";
            x.MissingItems=input.MissingItems?.Trim()??"";
            x.Notes=input.Notes?.Trim()??x.Notes;
            x.Status="RETURNED";
            if(x.Asset!=null){
                x.Asset.Status=string.IsNullOrWhiteSpace(input.AssetStatus)?"IN_STOCK":input.AssetStatus!;
                x.Asset.Condition=x.ConditionIn;
                x.Asset.Location=x.ReturnLocation;
            }
            await db.SaveChangesAsync();
            return Results.Ok(new{x.Id,x.Status,x.ReturnDate,x.ReceivedByUserId,x.ReturnReason,x.ReturnLocation});
        });

        app.MapGet("/api/it-assets/inventory", async(HttpContext http,AppDbContext db)=>
        {
            var actor=await Me(http,db);if(actor is null)return Results.Unauthorized();
            var (mode,orgIds)=await StrictItScopeAsync(db,actor);
            var q=db.ITAssets.AsNoTracking().Where(x=>x.Status!="DEACTIVATED")
                .Where(x=>x.Status=="IN_STOCK"||x.Status=="AVAILABLE"||x.Status=="REPAIR"||x.Status=="RESERVED");
            if(mode=="OWN")q=q.Where(x=>x.CreatedByUserId==actor.Id);
            else if(mode!="ALL")q=q.Where(x=>x.OrgUnitId.HasValue&&orgIds.Contains(x.OrgUnitId.Value));
            return Results.Ok(await q.OrderBy(x=>x.Status).ThenBy(x=>x.Category).ThenBy(x=>x.AssetCode)
                .Select(x=>new{x.Id,x.AssetCode,x.Category,x.AssetName,x.Brand,x.Model,x.SerialNumber,x.Location,x.Department,x.Condition,x.Status,x.WarrantyExpiry,x.PurchasePrice,x.Currency})
                .ToListAsync());
        });

        app.MapGet("/api/it-assets/assets/{id:long}/history", async(long id,HttpContext http,AppDbContext db)=>{
            var asset=await db.ITAssets.AsNoTracking().Where(x=>x.Status!="DEACTIVATED").FirstOrDefaultAsync(x=>x.Id==id);
            if(asset is null)return Results.NotFound();
            var actor=await Me(http,db);if(actor is null)return Results.Unauthorized();
            if(!await CanAccessAssetAsync(db,actor,asset))return Results.Forbid();
            var users=await db.Users.AsNoTracking().ToDictionaryAsync(x=>x.Id,x=>x.Name);
            var rows=await db.ITAssetAssignments.AsNoTracking().Where(x=>x.Status!="DEACTIVATED").Where(x=>x.AssetId==id)
                .OrderByDescending(x=>x.AssignmentDate).ThenByDescending(x=>x.Id).ToListAsync();
            var ids=rows.Select(x=>x.Id).ToList();
            var docs=await db.Documents.AsNoTracking()
                .Where(x=>x.EntityType=="IT_HANDOVER"&&ids.Contains(x.EntityId)&&x.Status=="ACTIVE")
                .OrderByDescending(x=>x.UploadedAt).ToListAsync();
            return Results.Ok(new{
                asset=new{asset.Id,asset.AssetCode,asset.AssetName,asset.Brand,asset.Model,asset.SerialNumber,asset.Status,asset.Condition,asset.Location},
                history=rows.Select(x=>new{
                    x.Id,x.HandoverNo,x.AssignmentType,x.AssignmentDate,x.ReturnDate,x.UserId,
                    User=users.TryGetValue(x.UserId,out var un)?un:$"User #{x.UserId}",
                    x.ToLocation,x.FromLocation,x.ConditionOut,x.ConditionIn,x.Accessories,x.Status,x.ReceivedByUserId,
                    ReceivedBy=x.ReceivedByUserId.HasValue&&users.TryGetValue(x.ReceivedByUserId.Value,out var rn)?rn:null,
                    x.ReturnReason,x.ReturnLocation,x.AccessoriesReturned,x.MissingItems,x.Notes,
                    attachments=docs.Where(d=>d.EntityId==x.Id).Select(d=>new{
                        d.Id,d.OriginalFileName,d.UploadedAt,d.FileSize,
                        DownloadUrl=$"/api/it-assets/assignments/{x.Id}/attachment/{d.Id}/download"
                    })
                })
            });
        });

        app.MapGet("/api/it-assets/assignments/{id:long}/handover", async(long id,HttpContext http,AppDbContext db)=>{
            var x=await db.ITAssetAssignments.AsNoTracking().Where(x=>x.Status!="DEACTIVATED").Include(a=>a.Asset).Include(a=>a.User).FirstOrDefaultAsync(a=>a.Id==id);
            if(x is null)return Results.NotFound();
            var actor=await Me(http,db);if(actor is null)return Results.Unauthorized();
            if(x.Asset is null || !await CanAccessAssetAsync(db,actor,x.Asset))return Results.Forbid();
            var esc=(string? s)=>WebUtility.HtmlEncode(s??"");
            var html = $@"<!doctype html>
<html><head><meta charset=""utf-8""><title>{esc(x.HandoverNo)}</title>
<style>
body{{font-family:Arial,sans-serif;color:#111;padding:36px;max-width:900px;margin:auto}}
h1{{text-align:center;font-size:22px}}
.meta{{display:grid;grid-template-columns:1fr 1fr;gap:8px 30px;margin:25px 0}}
table{{width:100%;border-collapse:collapse}} td,th{{border:1px solid #999;padding:9px;text-align:left}}
.sign{{display:grid;grid-template-columns:1fr 1fr;text-align:center;margin-top:55px}}
.muted{{color:#666;font-size:12px}} @media print{{button{{display:none}}body{{padding:0}}}}
</style></head><body>
<button onclick=""window.print()"">Print / Save PDF</button>
<h1>IT EQUIPMENT HANDOVER RECORD</h1>
<div class=""meta"">
<div><b>Handover No:</b> {esc(x.HandoverNo)}</div><div><b>Date:</b> {x.AssignmentDate:dd/MM/yyyy}</div>
<div><b>Receiver:</b> {esc(x.User?.Name)}</div><div><b>Department:</b> {esc(x.User?.Department)}</div>
<div><b>Location:</b> {esc(x.ToLocation)}</div><div><b>Status:</b> {esc(x.Status)}</div>
</div>
<table><thead><tr><th>Asset Code</th><th>Device</th><th>Brand / Model</th><th>Serial</th><th>Condition</th></tr></thead>
<tbody><tr><td>{esc(x.Asset?.AssetCode)}</td><td>{esc(x.Asset?.AssetName)}</td><td>{esc(x.Asset?.Brand)} {esc(x.Asset?.Model)}</td><td>{esc(x.Asset?.SerialNumber)}</td><td>{esc(x.ConditionOut)}</td></tr></tbody></table>
<p><b>Accessories:</b> {esc(x.Accessories)}</p><p><b>Notes:</b> {esc(x.Notes)}</p>
<div class=""sign""><div><b>IT / Handover by</b><br><br><br><br>________________________</div><div><b>Receiver</b><br><br><br><br>________________________<br>{esc(x.User?.Name)}</div></div>
<p class=""muted"">Generated by MAIPT Project Management System (MPMS).</p>
</body></html>";
            return Results.Content(html,"text/html",Encoding.UTF8);
        });

        app.MapPost("/api/it-assets/assignments/{id:long}/attachment", async(long id,HttpRequest request,HttpContext http,AppDbContext db,IWebHostEnvironment env)=>{
            var row=await db.ITAssetAssignments.Include(x=>x.Asset).FirstOrDefaultAsync(x=>x.Id==id);if(row is null)return Results.NotFound();
            var actor=await Me(http,db);if(actor is null)return Results.Unauthorized();
            if(row.Asset is null || !await CanAccessAssetAsync(db,actor,row.Asset))return Results.Forbid();
            if(!request.HasFormContentType)return Results.BadRequest(new{message="multipart/form-data required."});
            var form=await request.ReadFormAsync();var file=form.Files.FirstOrDefault();if(file is null||file.Length==0)return Results.BadRequest(new{message="File required."});
            var ext=Path.GetExtension(file.FileName).ToLowerInvariant();if(!new[]{".pdf",".jpg",".jpeg",".png",".doc",".docx"}.Contains(ext))return Results.BadRequest(new{message="PDF/JPG/PNG/DOC/DOCX only."});
            var uid=Convert.ToInt64(http.Items["AuthUserId"]);var dir=Path.Combine(env.ContentRootPath,"storage","documents","IT_HANDOVER");Directory.CreateDirectory(dir);
            var name=$"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{ext}";var target=Path.Combine(dir,name);await using(var fs=File.Create(target))await file.CopyToAsync(fs);
            var rec=new DocumentRecord{OrgUnitId=1,EntityType="IT_HANDOVER",EntityId=id,Category="IT_HANDOVER",Name=row.HandoverNo,OriginalFileName=Path.GetFileName(file.FileName),FilePath=$"storage/documents/IT_HANDOVER/{name}",MimeType=file.ContentType??"application/octet-stream",FileSize=file.Length,UploadedBy=uid,UploadedAt=DateTime.UtcNow,Status="ACTIVE",IsCurrent=true};
            db.Documents.Add(rec);await db.SaveChangesAsync();return Results.Created($"/api/it-assets/assignments/{id}/attachment/{rec.Id}",new{rec.Id,rec.OriginalFileName});
        });

        app.MapGet("/api/it-assets/assignments/{id:long}/attachments", async(long id,HttpContext http,AppDbContext db)=>
        {
            var row=await db.ITAssetAssignments.AsNoTracking().Include(x=>x.Asset).FirstOrDefaultAsync(x=>x.Id==id);
            if(row is null)return Results.NotFound();
            var actor=await Me(http,db);if(actor is null)return Results.Unauthorized();
            if(row.Asset is null || !await CanAccessAssetAsync(db,actor,row.Asset))return Results.Forbid();
            return Results.Ok(await db.Documents.AsNoTracking().Where(x=>x.EntityType=="IT_HANDOVER"&&x.EntityId==id&&x.Status=="ACTIVE").OrderByDescending(x=>x.UploadedAt).Select(x=>new{x.Id,x.OriginalFileName,x.FileSize,x.UploadedAt,DownloadUrl=$"/api/it-assets/assignments/{id}/attachment/{x.Id}/download"}).ToListAsync());
        });

        app.MapGet("/api/it-assets/assignments/{id:long}/attachment/{docId:long}/download", async(long id,long docId,HttpContext http,AppDbContext db,IWebHostEnvironment env)=>{
            var row=await db.ITAssetAssignments.AsNoTracking().Include(x=>x.Asset).FirstOrDefaultAsync(x=>x.Id==id);if(row is null)return Results.NotFound();
            var actor=await Me(http,db);if(actor is null)return Results.Unauthorized();
            if(row.Asset is null || !await CanAccessAssetAsync(db,actor,row.Asset))return Results.Forbid();
            var rec=await db.Documents.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==docId&&x.EntityType=="IT_HANDOVER"&&x.EntityId==id&&x.Status=="ACTIVE");if(rec is null)return Results.NotFound();
            var full=Path.GetFullPath(Path.Combine(env.ContentRootPath,(rec.FilePath??"").TrimStart('/').Replace('/',Path.DirectorySeparatorChar)));
            var root=Path.GetFullPath(Path.Combine(env.ContentRootPath,"storage"))+Path.DirectorySeparatorChar;if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!File.Exists(full))return Results.NotFound();
            return Results.File(full,string.IsNullOrWhiteSpace(rec.MimeType)?"application/octet-stream":rec.MimeType,rec.OriginalFileName,enableRangeProcessing:true);
        });

        app.MapDelete("/api/it-assets/assignments/{id:long}", async(long id,HttpContext http,AppDbContext db)=>{
            var x=await db.ITAssetAssignments.Include(a=>a.Asset).FirstOrDefaultAsync(a=>a.Id==id);
            if(x is null)return Results.NotFound(new{message="Assignment not found."});
            var actor=await Me(http,db);if(actor is null)return Results.Unauthorized();
            if(x.Asset is null || !await CanAccessAssetAsync(db,actor,x.Asset))return Results.Forbid();
            x.Status="DEACTIVATED"; x.UpdatedAt=DateTime.UtcNow;
            if(http.Items["AuthUserId"] is not null)x.UpdatedByUserId=Convert.ToInt64(http.Items["AuthUserId"]);
            if(x.Asset!=null && x.Asset.Status=="ASSIGNED"){
                var other=await db.ITAssetAssignments.AnyAsync(a=>a.AssetId==x.AssetId && a.Id!=id && a.Status=="ACTIVE");
                if(!other)x.Asset.Status="IN_STOCK";
            }
            await db.SaveChangesAsync(); return Results.Ok(new{x.Id,x.Status,message="Assignment deactivated."});
        });

        app.MapGet("/api/it-assets/licenses", async(HttpContext http,AppDbContext db)=>{
            var uid=Convert.ToInt64(http.Items["AuthUserId"]);
            var actor=await db.Users.AsNoTracking().FirstAsync(x=>x.Id==uid);
            var (mode,orgIds)=await StrictItScopeAsync(db,actor);
            var q=db.ITLicenses.AsNoTracking().Where(x=>x.Status!="DEACTIVATED").AsQueryable();
            if(mode!="ALL") q=q.Where(x=>x.CreatedByUserId==uid||(x.OrgUnitId.HasValue&&orgIds.Contains(x.OrgUnitId.Value)));
            return Results.Ok(await q.OrderBy(x=>x.ProductName).ToListAsync());
        });

        app.MapPost("/api/it-assets/licenses", async(ITLicense input,HttpContext http,AppDbContext db)=>{var actor=await Me(http,db);if(actor is null)return Results.Unauthorized();if(!await CanUseOrgAsync(db,actor,input.OrgUnitId))return Results.Forbid();input.Id=0;if(string.IsNullOrWhiteSpace(input.Code))input.Code=await NextCode(db,"LICENSE");input.Supplier=null;input.Contract=null;db.ITLicenses.Add(input);await db.SaveChangesAsync();return Results.Created($"/api/it-assets/licenses/{input.Id}",new{input.Id,input.Code});});
        app.MapPut("/api/it-assets/licenses/{id:long}", async(long id,ITLicense input,HttpContext http,AppDbContext db)=>{var x=await db.ITLicenses.FindAsync(id);if(x is null)return Results.NotFound();var actor=await Me(http,db);if(actor is null)return Results.Unauthorized();if(!await CanAccessOwnedOrgAsync(db,actor,x.CreatedByUserId,x.OrgUnitId)||!await CanUseOrgAsync(db,actor,input.OrgUnitId))return Results.Forbid();x.ProductName=input.ProductName;x.Vendor=input.Vendor;x.LicenseType=input.LicenseType;x.Licensee=input.Licensee;x.LicenseKey=input.LicenseKey;x.OrgUnitId=input.OrgUnitId;x.AssignedUserId=input.AssignedUserId;x.AssignedAssetId=input.AssignedAssetId;x.ActivationStatus=input.ActivationStatus;x.ActivationDate=input.ActivationDate;x.Quantity=input.Quantity;x.AssignedQuantity=input.AssignedQuantity;x.StartDate=input.StartDate;x.ExpiryDate=input.ExpiryDate;x.Cost=input.Cost;x.Currency=input.Currency;x.SupplierId=input.SupplierId;x.ContractId=input.ContractId;x.AutoRenew=input.AutoRenew;x.Status=input.Status;x.Notes=input.Notes;await db.SaveChangesAsync();return Results.Ok(new{x.Id});});
        app.MapDelete("/api/it-assets/licenses/{id:long}", async(long id,HttpContext http,AppDbContext db)=>{
            var x=await db.ITLicenses.FindAsync(id); if(x is null)return Results.NotFound(new{message="License not found."});
            var actor=await Me(http,db);if(actor is null)return Results.Unauthorized();
            if(!await CanAccessOwnedOrgAsync(db,actor,x.CreatedByUserId,x.OrgUnitId))return Results.Forbid();
            x.Status="DEACTIVATED"; x.UpdatedAt=DateTime.UtcNow;
            if(http.Items["AuthUserId"] is not null)x.UpdatedByUserId=Convert.ToInt64(http.Items["AuthUserId"]);
            await db.SaveChangesAsync(); return Results.Ok(new{x.Id,x.Status,message="License deactivated."});
        });
        app.MapGet("/api/it-assets/services", async(HttpContext http,AppDbContext db)=>{
            var uid=Convert.ToInt64(http.Items["AuthUserId"]);
            var actor=await db.Users.AsNoTracking().FirstAsync(x=>x.Id==uid);
            var (mode,orgIds)=await StrictItScopeAsync(db,actor);
            var q=db.ITServices.AsNoTracking().Where(x=>x.Status!="DEACTIVATED").Include(x=>x.Supplier).Include(x=>x.Contract).AsQueryable();
            if(mode!="ALL") q=q.Where(x=>x.CreatedByUserId==uid||(x.OrgUnitId.HasValue&&orgIds.Contains(x.OrgUnitId.Value)));
            return Results.Ok(await q.OrderBy(x=>x.ServiceName).ToListAsync());
        });

        app.MapPost("/api/it-assets/services", async(ITService input,HttpContext http,AppDbContext db)=>{var actor=await Me(http,db);if(actor is null)return Results.Unauthorized();if(!await CanUseOrgAsync(db,actor,input.OrgUnitId))return Results.Forbid();input.Id=0;if(string.IsNullOrWhiteSpace(input.Code))input.Code=await NextCode(db,"SERVICE");input.Supplier=null;input.Contract=null;db.ITServices.Add(input);await db.SaveChangesAsync();return Results.Created($"/api/it-assets/services/{input.Id}",new{input.Id,input.Code});});
        app.MapPut("/api/it-assets/services/{id:long}",async(long id,ITService input,HttpContext http,AppDbContext db)=>{var x=await db.ITServices.FindAsync(id);if(x is null)return Results.NotFound();var actor=await Me(http,db);if(actor is null)return Results.Unauthorized();if(!await CanAccessOwnedOrgAsync(db,actor,x.CreatedByUserId,x.OrgUnitId)||!await CanUseOrgAsync(db,actor,input.OrgUnitId))return Results.Forbid();x.ServiceType=input.ServiceType;x.ServiceName=input.ServiceName;x.Provider=input.Provider;x.Site=input.Site;x.Department=input.Department;x.OrgUnitId=input.OrgUnitId;x.SupplierId=input.SupplierId;x.ContractId=input.ContractId;x.StartDate=input.StartDate;x.ExpiryDate=input.ExpiryDate;x.MonthlyCost=input.MonthlyCost;x.AnnualCost=input.AnnualCost;x.Currency=input.Currency;x.Status=input.Status;x.Notes=input.Notes;await db.SaveChangesAsync();return Results.Ok(new{x.Id});});
        app.MapDelete("/api/it-assets/services/{id:long}", async(long id,HttpContext http,AppDbContext db)=>{
            var x=await db.ITServices.FindAsync(id); if(x is null)return Results.NotFound(new{message="Service not found."});
            var actor=await Me(http,db);if(actor is null)return Results.Unauthorized();
            if(!await CanAccessOwnedOrgAsync(db,actor,x.CreatedByUserId,x.OrgUnitId))return Results.Forbid();
            x.Status="DEACTIVATED"; x.UpdatedAt=DateTime.UtcNow;
            if(http.Items["AuthUserId"] is not null)x.UpdatedByUserId=Convert.ToInt64(http.Items["AuthUserId"]);
            await db.SaveChangesAsync(); return Results.Ok(new{x.Id,x.Status,message="Service deactivated."});
        });
        app.MapGet("/api/it-assets/maintenance", async(HttpContext http,AppDbContext db)=>{
            var uid=Convert.ToInt64(http.Items["AuthUserId"]);
            var actor=await db.Users.AsNoTracking().FirstAsync(x=>x.Id==uid);
            var (mode,orgIds)=await StrictItScopeAsync(db,actor);
            var aq=db.ITAssets.AsNoTracking().Where(x=>x.Status!="DEACTIVATED").AsQueryable();
            if(mode!="ALL") aq=aq.Where(x=>x.CreatedByUserId==uid||(x.OrgUnitId.HasValue&&orgIds.Contains(x.OrgUnitId.Value)));
            var ids=await aq.Select(x=>x.Id).ToArrayAsync();
            var q=db.ITMaintenances.AsNoTracking().Where(x=>x.Status!="DEACTIVATED").Include(x=>x.Asset).AsQueryable();
            if(mode!="ALL") q=q.Where(x=>x.CreatedByUserId==uid||ids.Contains(x.AssetId));
            return Results.Ok(await q.OrderByDescending(x=>x.OpenDate).Select(x=>new{x.Id,x.AssetId,AssetCode=x.Asset!.AssetCode,AssetName=x.Asset!.AssetName,x.Type,x.OpenDate,x.CloseDate,x.Vendor,x.Description,x.Cost,x.Currency,x.Status,x.Notes}).ToListAsync());
        });

        app.MapPost("/api/it-assets/maintenance",async(ITMaintenance input,HttpContext http,AppDbContext db)=>{var asset=await db.ITAssets.FindAsync(input.AssetId);if(asset is null)return Results.BadRequest(new{message="Asset not found."});var actor=await Me(http,db);if(actor is null)return Results.Unauthorized();if(!await CanAccessAssetAsync(db,actor,asset))return Results.Forbid();input.Id=0;input.Asset=null;db.ITMaintenances.Add(input);asset.Status="REPAIR";await db.SaveChangesAsync();return Results.Created($"/api/it-assets/maintenance/{input.Id}",new{input.Id});});
        app.MapPut("/api/it-assets/maintenance/{id:long}",async(long id,ITMaintenance input,HttpContext http,AppDbContext db)=>{var x=await db.ITMaintenances.Include(m=>m.Asset).FirstOrDefaultAsync(m=>m.Id==id);if(x is null)return Results.NotFound();var actor=await Me(http,db);if(actor is null)return Results.Unauthorized();if(x.Asset is null||!await CanAccessAssetAsync(db,actor,x.Asset))return Results.Forbid();x.Type=input.Type;x.OpenDate=input.OpenDate;x.CloseDate=input.CloseDate;x.Vendor=input.Vendor;x.Description=input.Description;x.Cost=input.Cost;x.Currency=input.Currency;x.Status=input.Status;x.Notes=input.Notes;if(x.Status=="CLOSED"&&x.Asset.Status=="REPAIR")x.Asset.Status="IN_STOCK";await db.SaveChangesAsync();return Results.Ok(new{x.Id});});
        app.MapDelete("/api/it-assets/maintenance/{id:long}", async(long id,HttpContext http,AppDbContext db)=>{
            var x=await db.ITMaintenances.Include(m=>m.Asset).FirstOrDefaultAsync(m=>m.Id==id); if(x is null)return Results.NotFound(new{message="Maintenance record not found."});
            var actor=await Me(http,db);if(actor is null)return Results.Unauthorized();
            if(x.Asset is null||!await CanAccessAssetAsync(db,actor,x.Asset))return Results.Forbid();
            x.Status="DEACTIVATED"; x.UpdatedAt=DateTime.UtcNow;
            if(http.Items["AuthUserId"] is not null)x.UpdatedByUserId=Convert.ToInt64(http.Items["AuthUserId"]);
            await db.SaveChangesAsync(); return Results.Ok(new{x.Id,x.Status,message="Maintenance record deactivated."});
        });
    }
}

public sealed record ITAssetReturnRequest(DateOnly? ReturnDate,string? ConditionIn,string? AssetStatus,string? Location,string? Notes,long? ReceivedByUserId,string? ReturnReason,string? AccessoriesReturned,string? MissingItems);
