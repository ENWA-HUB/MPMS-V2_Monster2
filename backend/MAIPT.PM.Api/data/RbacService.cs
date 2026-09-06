using System.Data;
using System.Data.Common;
using MAIPT.PM.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MAIPT.PM.Api.Data;

public sealed record AccessScope(string Mode, string[] Values);
public sealed record AccessSummary(string[] Modules, Dictionary<string,string[]> Permissions, Dictionary<string,AccessScope> Scopes);

public static class RbacService
{
    public static bool IsRoot(AppUser u)
    {
        return string.Equals((u.Role ?? "").Trim(), "ROOT", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAdmin(AppUser u)
    {
        if (IsRoot(u)) return true;
        var role=(u.Role??"").Trim().ToUpperInvariant();
        return role is "ADMIN" or "CIO";
    }

    public static readonly string[] Modules = ["DASHBOARD","PROJECTS","TASKS","RISKS","BUDGET","SUPPLIERS","KPI","APPROVALS","REPORTS","EXECUTIVE","DOCUMENTS","CAPITAL","INVESTMENT","SETTINGS","ACCESS_CONTROL","USERS","PROJECT_TEAM","IT_ASSETS","CONTRACTS",
        "PERSONAL_FC"];
    public static readonly string[] Actions = ["VIEW","CREATE","EDIT","DELETE","SUBMIT","APPROVE","REPORT","UPLOAD","DOWNLOAD","FULL"];

    public static async Task EnsureSchemaAsync(AppDbContext db)
    {
        if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'[UserModulePermissions]', N'U') IS NULL
BEGIN
 CREATE TABLE [UserModulePermissions]([UserId] BIGINT NOT NULL,[Module] NVARCHAR(64) NOT NULL,[Permission] NVARCHAR(32) NOT NULL,[IsAllowed] BIT NOT NULL,CONSTRAINT [PK_UserModulePermissions] PRIMARY KEY([UserId],[Module],[Permission]),CONSTRAINT [FK_UserModulePermissions_Users] FOREIGN KEY([UserId]) REFERENCES [Users]([Id]) ON DELETE CASCADE);
END;
IF OBJECT_ID(N'[UserModuleScopes]', N'U') IS NULL
BEGIN
 CREATE TABLE [UserModuleScopes]([UserId] BIGINT NOT NULL,[Module] NVARCHAR(64) NOT NULL,[ScopeMode] NVARCHAR(32) NOT NULL,[ScopeValue] NVARCHAR(128) NOT NULL DEFAULT '',CONSTRAINT [PK_UserModuleScopes] PRIMARY KEY([UserId],[Module],[ScopeMode],[ScopeValue]),CONSTRAINT [FK_UserModuleScopes_Users] FOREIGN KEY([UserId]) REFERENCES [Users]([Id]) ON DELETE CASCADE);
END;
""");
        }
        else if (db.Database.IsSqlite())
        {
            await db.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS UserModulePermissions(UserId INTEGER NOT NULL,Module TEXT NOT NULL,Permission TEXT NOT NULL,IsAllowed INTEGER NOT NULL,PRIMARY KEY(UserId,Module,Permission));
CREATE TABLE IF NOT EXISTS UserModuleScopes(UserId INTEGER NOT NULL,Module TEXT NOT NULL,ScopeMode TEXT NOT NULL,ScopeValue TEXT NOT NULL DEFAULT '',PRIMARY KEY(UserId,Module,ScopeMode,ScopeValue));
""");
        }
    }

    static HashSet<string> DefaultPerm(AppUser u,string module)
    {
        var role=(u.Role??"MEMBER").ToUpperInvariant(); module=module.ToUpperInvariant();
        if(role is "ADMIN" or "CIO") return new(Actions.Where(x=>x!="DOWNLOAD"),StringComparer.OrdinalIgnoreCase);
        if(role is "HOD" or "PM" or "PROJECT_MANAGER")
        {
            var mods=new[]{"DASHBOARD","PROJECTS","TASKS","RISKS","BUDGET","SUPPLIERS","KPI","APPROVALS","REPORTS","EXECUTIVE","DOCUMENTS","CAPITAL","INVESTMENT"};
            return mods.Contains(module)?new(Actions.Where(x=>x!="FULL"&&x!="DOWNLOAD"),StringComparer.OrdinalIgnoreCase):new(StringComparer.OrdinalIgnoreCase);
        }
        if(role=="PROCUREMENT" && module=="SUPPLIERS") return new(["VIEW","CREATE","EDIT","SUBMIT"],StringComparer.OrdinalIgnoreCase);
        return module switch {
            "BUDGET"=>new(["VIEW","CREATE","EDIT","SUBMIT"],StringComparer.OrdinalIgnoreCase),
            "KPI"=>new(["VIEW","CREATE","EDIT","SUBMIT"],StringComparer.OrdinalIgnoreCase),
            "PROJECTS"=>new(["VIEW"],StringComparer.OrdinalIgnoreCase),
            "TASKS"=>new(["VIEW","EDIT"],StringComparer.OrdinalIgnoreCase),
            "DOCUMENTS"=>new(["VIEW","UPLOAD"],StringComparer.OrdinalIgnoreCase),
            "CAPITAL"=>new(["VIEW"],StringComparer.OrdinalIgnoreCase),
            "INVESTMENT"=>new(["VIEW"],StringComparer.OrdinalIgnoreCase),
            _=>new(StringComparer.OrdinalIgnoreCase)
        };
    }

    static AccessScope DefaultScope(AppUser u,string module)
    {
        if(IsAdmin(u)) return new("ALL",[]);
        var r=(u.Role??"MEMBER").ToUpperInvariant();
        return r is "HOD" or "PM" or "PROJECT_MANAGER" ? new("OWN_ORG",[]) : new("OWN",[]);
    }

    public static async Task<HashSet<string>> GetPermissionsAsync(AppDbContext db,AppUser u,string module)
    {
        // ROOT MAIPT FULL PERMISSIONS
        if (IsRoot(u))
            return new HashSet<string>(Actions, StringComparer.OrdinalIgnoreCase);

        // ROOT ADMIN BYPASS: PERMISSIONS
        if (IsRoot(u))
            return new HashSet<string>(Actions, StringComparer.OrdinalIgnoreCase);

        var rows=await PermissionRows(db,u.Id,module.ToUpperInvariant());
        if(rows.Count==0)return DefaultPerm(u,module);
        var set=new HashSet<string>(rows.Where(x=>x.Allowed).Select(x=>x.Permission),StringComparer.OrdinalIgnoreCase);
        if(set.Contains("FULL"))foreach(var a in Actions.Where(a=>!a.Equals("DOWNLOAD",StringComparison.OrdinalIgnoreCase)))set.Add(a);
        return set;
    }
    public static async Task<bool> CanAsync(AppDbContext db,AppUser u,string module,string action)
    {
        var p=await GetPermissionsAsync(db,u,module);

        // DOWNLOAD is always explicit and never implied by FULL.
        if(action.Equals("DOWNLOAD",StringComparison.OrdinalIgnoreCase))
            return p.Contains("DOWNLOAD");

        // Backward compatibility for imports/uploads:
        // Existing users may already have CREATE/EDIT but were created before
        // UPLOAD became a separate RBAC action.
        // Therefore an upload/import is allowed by UPLOAD, CREATE, EDIT or FULL.
        if(action.Equals("UPLOAD",StringComparison.OrdinalIgnoreCase))
            return p.Contains("UPLOAD") ||
                   p.Contains("CREATE") ||
                   p.Contains("EDIT") ||
                   p.Contains("FULL");

        return p.Contains("FULL") || p.Contains(action);
    }
    public static async Task<AccessScope> GetScopeAsync(AppDbContext db,AppUser u,string module)
    {
        // ROOT MAIPT ALL DATA
        if (IsRoot(u))
            return new("ALL", []);

        // ROOT ADMIN BYPASS: DATA SCOPE
        if (IsRoot(u))
            return new("ALL", []);

        var rows=await ScopeRows(db,u.Id,module.ToUpperInvariant());
        if(rows.Count==0)return DefaultScope(u,module);
        return new(rows[0].Mode.ToUpperInvariant(),rows.Where(x=>!string.IsNullOrWhiteSpace(x.Value)).Select(x=>x.Value).Distinct().ToArray());
    }
    public static async Task<AccessSummary> SummaryAsync(AppDbContext db,AppUser u)
    {
        var p=new Dictionary<string,string[]>();var s=new Dictionary<string,AccessScope>();var mods=new List<string>();
        foreach(var m in Modules){var x=await GetPermissionsAsync(db,u,m);p[m]=x.OrderBy(v=>v).ToArray();s[m]=await GetScopeAsync(db,u,m);if(x.Contains("VIEW")||x.Contains("FULL"))mods.Add(m);}return new(mods.ToArray(),p,s);
    }

    public static (string? Module,string? Action) Map(string path,string method)
    {
        var p=(path??"").ToLowerInvariant();
        var v=(method??"GET").ToUpperInvariant();

        // BUDGET IMPORT SELF-AUTH V4
        if(p.Equals("/api/excel/budget/import",StringComparison.OrdinalIgnoreCase))
            return (null,null);

        if(p=="/api/access/me") return (null,null);
        if(p=="/api/settings/options") return (null,null);
        // The shared Capital/Investment route resolves its exact module from
        // managementType inside CapitalInvestmentEndpoints.
        if(p.StartsWith("/api/capital-investment")) return (null,null);
        // M365 document endpoints resolve permission from each file's function folder.
        if(p.StartsWith("/api/m365-documents")) return (null,null);
        // Document endpoints resolve access from the file/category function.
        if(p.StartsWith("/api/documents")) return (null,null);

        string? m =
            p.StartsWith("/api/access") ? "ACCESS_CONTROL" :
            p.StartsWith("/api/settings/team-members") ? "USERS" :
            p.StartsWith("/api/contracts") ? "CONTRACTS" :
            p.StartsWith("/api/orgunits") ? "SETTINGS" :
            p.StartsWith("/api/portfolios") ? "PROJECTS" :
            p.StartsWith("/api/activity") ? "ACCESS_CONTROL" :
            p.StartsWith("/api/settings") ? "SETTINGS" :
            p.StartsWith("/api/it-assets") ? "IT_ASSETS" :
            p.StartsWith("/api/team/users") && v!="GET" ? "USERS" :
            p.StartsWith("/api/project-members") && v!="GET" ? "PROJECT_TEAM" :
            p.StartsWith("/api/budget") ? "BUDGET" :
            p.StartsWith("/api/performance") || p.StartsWith("/api/kpi") ? "KPI" :
            p.StartsWith("/api/approvals") ? "APPROVALS" :
            p.StartsWith("/api/dashboard") ? "DASHBOARD" :
            p.StartsWith("/api/projects") ? "PROJECTS" :
            p.StartsWith("/api/tasks") || p.StartsWith("/api/milestones") ? "TASKS" :
            p.StartsWith("/api/risks") || p.StartsWith("/api/issues") || p.StartsWith("/api/change-requests") ? "RISKS" :
            p.StartsWith("/api/suppliers") || p.StartsWith("/api/contracts") || p.StartsWith("/api/deliverables") ? "SUPPLIERS" :
            p.StartsWith("/api/reports") ? "REPORTS" :
            p.StartsWith("/api/presentation") ? "EXECUTIVE" :
            null;

        if(m==null)return(null,null);

        var isDownload =
            v=="GET" && p.Contains("/download");

        var isUpload =
            v=="POST" &&
            (p.Contains("/upload") || p.Contains("/profile-image") || p.Contains("/signature") || p.Contains("/files"));

        var a =
            isDownload ? "DOWNLOAD" :
            isUpload ? "UPLOAD" :
            v=="GET" ? "VIEW" :
            v=="DELETE" ? "DELETE" :
            v is "PUT" or "PATCH" ? "EDIT" :
            p.Contains("approve") || p.Contains("reject") || p.StartsWith("/api/approvals/") ? "APPROVE" :
            p.Contains("submit") ? "SUBMIT" :
            "CREATE";

        return(m,a);
    }

    // Paths that Map() intentionally returns (null,null) for because the endpoint
    // performs its own authorization inline (ownership, per-document scope, per-club
    // role, or explicit RbacService.CanAsync calls). Every other unmapped /api/ path
    // is treated as a configuration error and denied by the authentication gate.
    static readonly string[] InlineAuthorizedPrefixes =
    {
        "/api/auth/",
        "/api/access/me",
        "/api/access/catalog",
        "/api/scope/options",
        "/api/settings/options",
        "/api/notifications",
        "/api/documents",
        "/api/m365-documents",
        "/api/capital-investment",
        "/api/code-merge/",
        "/api/personal-fc/",
        "/api/excel/",
        "/api/ai-help/",
        "/api/users",          // list/profile/image/signature: each handler enforces EmployeeScope / CanAccessEmployee
        "/api/team/users",     // GET self-scopes inline; non-GET is mapped to USERS above
        "/api/project-members" // GET self-scopes inline; non-GET is mapped to PROJECT_TEAM above
    };

    public static bool IsInlineAuthorized(string path)
    {
        var p = (path ?? "").ToLowerInvariant();
        return InlineAuthorizedPrefixes.Any(x => p.StartsWith(x, StringComparison.OrdinalIgnoreCase));
    }

    public static async Task<long[]> AllowedOrgIdsAsync(AppDbContext db,AppUser u,string module)
    {
        var sc=await GetScopeAsync(db,u,module);
        var mode=(sc.Mode??"OWN").ToUpperInvariant();
        if(mode=="ALL") return await db.OrgUnits.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED").Select(x=>x.Id).ToArrayAsync();
        if(mode=="OWN") return [];
        if(mode=="OWN_ORG") return u.OrgUnitId>0 ? [u.OrgUnitId] : [];
        if(mode=="SELECTED_ORGS")
        {
            var vals=sc.Values??[];
            return await db.OrgUnits.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED").Where(x=>vals.Contains(x.Code)||vals.Contains(x.Id.ToString())).Select(x=>x.Id).ToArrayAsync();
        }
        return [];
    }

    public static async Task<long[]> AllowedProjectIdsAsync(AppDbContext db,AppUser u,string module)
    {
        var sc=await GetScopeAsync(db,u,module);
        var mode=(sc.Mode??"OWN").ToUpperInvariant();
        if(mode=="ALL") return await db.Projects.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED").Select(x=>x.Id).ToArrayAsync();
        if(mode=="ASSIGNED_PROJECTS") return await db.ProjectMembers.AsNoTracking().Where(x=>x.UserId==u.Id&&x.IsActive).Select(x=>x.ProjectId).Distinct().ToArrayAsync();
        if(mode=="SELECTED_PROJECTS")
        {
            var vals=sc.Values??[];
            var ids=vals.Select(v=>long.TryParse(v,out var id)?id:0).Where(x=>x>0).ToArray();
            return await db.Projects.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED").Where(x=>ids.Contains(x.Id)||vals.Contains(x.Code)).Select(x=>x.Id).Distinct().ToArrayAsync();
        }
        var orgIds=await AllowedOrgIdsAsync(db,u,module);
        if(orgIds.Length==0) return [];
        var a=db.Projects.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED").Where(x=>orgIds.Contains(x.OrgUnitId)).Select(x=>x.Id);
        var b=db.ProjectOrgUnits.AsNoTracking().Where(x=>orgIds.Contains(x.OrgUnitId)).Select(x=>x.ProjectId);
        return await a.Concat(b).Distinct().ToArrayAsync();
    }

    public static async Task<bool> IsAllScopeAsync(AppDbContext db,AppUser u,string module)
        => (await GetScopeAsync(db,u,module)).Mode.Equals("ALL",StringComparison.OrdinalIgnoreCase);

    public static string DocumentModule(DocumentRecord document)
    {
        var source=$"{document.EntityType} {document.Category} {document.StoragePath}".ToUpperInvariant();
        if(source.Contains("CAPITAL MANAGEMENT")||source.Contains("CAPITAL MGMT")||source.Contains("CAPITAL_DOCUMENT")||document.EntityType=="CAPITAL")return "CAPITAL";
        if(source.Contains("INVESTMENT MANAGEMENT")||source.Contains("INVEST MGMT")||source.Contains("INVESTMENT_DOCUMENT")||document.EntityType=="INVESTMENT")return "INVESTMENT";
        if(source.Contains("IT ASSETS")||source.Contains("IT POLICIES")||source.Contains("LICENSE")||source.Contains("HANDOVER")||document.EntityType=="IT_ASSETS")return "IT_ASSETS";
        if(source.Contains("PERSONAL FC")||source.Contains("PERSONAL_FC")||source.Contains("CLUB"))return "PERSONAL_FC";
        if(source.Contains("SYSTEM")||source.Contains("AVATAR")||source.Contains("USER PROFILE")||source.Contains("USER_PROFILE")||source.Contains("SIGNATURE")||document.EntityType=="USERS")return "USERS";
        if(source.Contains("PROJECT"))return "PROJECTS";
        if(source.Contains("CONTRACT"))return "CONTRACTS";
        if(source.Contains("BUDGET"))return "BUDGET";
        if(source.Contains("SUPPLIER"))return "SUPPLIERS";
        if(source.Contains("KPI")||source.Contains("PERFORMANCE"))return "KPI";
        return "DOCUMENTS";
    }

    public static async Task<bool> CanAccessDocumentAsync(AppDbContext db,AppUser actor,DocumentRecord document,string action="VIEW")
    {
        if(IsRoot(actor))return true;
        var module=DocumentModule(document);
        if(!await CanAsync(db,actor,module,action))return false;
        var scope=await GetScopeAsync(db,actor,module);
        var mode=(scope.Mode??"OWN").ToUpperInvariant();
        if(mode=="ALL")return true;

        if(module=="CONTRACTS"&&document.EntityId>0)
        {
            var parent=await db.Contracts.AsNoTracking()
                .Where(x=>x.Id==document.EntityId&&x.Status!="DEACTIVATED"&&x.Status!="DELETED")
                .Select(x=>new{x.OrgUnitId,x.ProjectId,x.CreatedByUserId}).FirstOrDefaultAsync();
            if(parent is null)return false;
            if(mode=="OWN")return parent.CreatedByUserId==actor.Id||document.CreatedByUserId==actor.Id||document.UploadedBy==actor.Id;
            if(mode=="OWN_ORG")return actor.OrgUnitId>0&&parent.OrgUnitId==actor.OrgUnitId;
            if(mode=="SELECTED_ORGS")return (await AllowedOrgIdsAsync(db,actor,module)).Contains(parent.OrgUnitId);
            if(mode is "ASSIGNED_PROJECTS" or "SELECTED_PROJECTS")return (await AllowedProjectIdsAsync(db,actor,module)).Contains(parent.ProjectId);
            return false;
        }

        if(module=="SUPPLIERS"&&document.EntityId>0)
        {
            var parent=await db.Suppliers.AsNoTracking()
                .Where(x=>x.Id==document.EntityId&&x.Status!="DEACTIVATED"&&x.Status!="DELETED")
                .Select(x=>new{x.OrgUnitId,x.CreatedByUserId}).FirstOrDefaultAsync();
            if(parent is null)return false;
            if(mode=="OWN")return parent.CreatedByUserId==actor.Id||document.CreatedByUserId==actor.Id||document.UploadedBy==actor.Id;
            if(mode=="OWN_ORG")return actor.OrgUnitId>0&&parent.OrgUnitId==actor.OrgUnitId;
            if(mode=="SELECTED_ORGS")return (await AllowedOrgIdsAsync(db,actor,module)).Contains(parent.OrgUnitId);
            return false;
        }

        if(module=="USERS"&&document.EntityId>0)
        {
            IQueryable<AppUser> target=db.Users.AsNoTracking().Where(x=>x.Id==document.EntityId);
            target=await EmployeeScope(db,actor,target);
            return await target.AnyAsync();
        }

        if(module=="PROJECTS"&&document.EntityId>0)return await CanProjectAsync(db,actor,document.EntityId);
        if(mode=="OWN")return document.CreatedByUserId==actor.Id||document.UploadedBy==actor.Id;
        if(mode=="OWN_ORG")return actor.OrgUnitId>0&&document.OrgUnitId==actor.OrgUnitId;
        if(mode=="SELECTED_ORGS")
        {
            var ids=await AllowedOrgIdsAsync(db,actor,module);
            return ids.Contains(document.OrgUnitId);
        }
        return false;
    }

    public static async Task<List<DocumentRecord>> FilterAccessibleDocumentsAsync(AppDbContext db,AppUser actor,List<DocumentRecord> documents)
    {
        if(IsRoot(actor))return documents;
        var result=new List<DocumentRecord>();
        foreach(var document in documents)
            if(await CanAccessDocumentAsync(db,actor,document,"VIEW"))result.Add(document);
        return result;
    }

    public static async Task<IQueryable<BudgetPlanItem>> BudgetScope(AppDbContext db,AppUser u,IQueryable<BudgetPlanItem> q)
    {
        var s=await GetScopeAsync(db,u,"BUDGET");if(s.Mode=="ALL")return q;if(s.Mode=="OWN")return q.Where(x=>x.CreatedByUserId==u.Id);
        var own=await db.OrgUnits.Where(x=>x.Id==u.OrgUnitId).Select(x=>x.Code).FirstOrDefaultAsync();
        if(s.Mode=="OWN_ORG")return q.Where(x=>x.CreatedByUserId==u.Id||x.OrgUnit==own);
        if(s.Mode=="SELECTED_ORGS"){var vals=s.Values;return q.Where(x=>x.CreatedByUserId==u.Id||vals.Contains(x.OrgUnit));}
        if(s.Mode=="SELECTED_USERS"){var ids=s.Values.Select(v=>long.TryParse(v,out var id)?id:0).Where(x=>x>0).ToArray();return q.Where(x=>x.CreatedByUserId==u.Id||(x.CreatedByUserId.HasValue&&ids.Contains(x.CreatedByUserId.Value)));}
        return q.Where(x=>x.CreatedByUserId==u.Id);
    }
    public static async Task<bool> CanBudgetItem(AppDbContext db,AppUser u,long id){IQueryable<BudgetPlanItem> q=db.BudgetPlanItems.AsNoTracking().Where(x=>x.Id==id);q=await BudgetScope(db,u,q);return await q.AnyAsync();}
    public static async Task<bool> CanBudgetOrg(AppDbContext db,AppUser u,string org)
    {
        var s=await GetScopeAsync(db,u,"BUDGET");if(s.Mode=="ALL")return true;var own=await db.OrgUnits.Where(x=>x.Id==u.OrgUnitId).Select(x=>x.Code).FirstOrDefaultAsync();if(string.Equals(org,own,StringComparison.OrdinalIgnoreCase))return true;return s.Mode=="SELECTED_ORGS"&&s.Values.Contains(org,StringComparer.OrdinalIgnoreCase);
    }
    public static async Task<IQueryable<PerformancePeriod>> KpiScope(AppDbContext db,AppUser u,IQueryable<PerformancePeriod> q)
    {
        var s=await GetScopeAsync(db,u,"KPI");if(s.Mode=="ALL")return q;if(s.Mode=="OWN")return q.Where(x=>x.UserId==u.Id);if(s.Mode=="OWN_ORG")return q.Where(x=>x.UserId==u.Id||x.Department==u.Department);
        if(s.Mode=="SELECTED_USERS"){var ids=s.Values.Select(v=>long.TryParse(v,out var id)?id:0).Where(x=>x>0).ToArray();return q.Where(x=>x.UserId==u.Id||(x.UserId.HasValue&&ids.Contains(x.UserId.Value)));}
        if(s.Mode=="SELECTED_ORGS"){var names=await db.OrgUnits.Where(x=>s.Values.Contains(x.Code)).Select(x=>x.Name).ToListAsync();var vals=s.Values.Concat(names).ToArray();return q.Where(x=>x.UserId==u.Id||vals.Contains(x.Department));}return q.Where(x=>x.UserId==u.Id);
    }

    public static async Task<IQueryable<AppUser>> EmployeeScope(AppDbContext db,AppUser actor,IQueryable<AppUser> q)
    {
        if(IsRoot(actor))return q;
        var scope=await GetScopeAsync(db,actor,"USERS");
        var mode=(scope.Mode??"OWN").Trim().ToUpperInvariant();
        if(mode=="ALL")return q;
        if(mode=="OWN")return q.Where(x=>x.Id==actor.Id);
        if(mode=="OWN_ORG")return q.Where(x=>x.Id==actor.Id||(actor.OrgUnitId>0&&x.OrgUnitId==actor.OrgUnitId));
        if(mode=="SELECTED_USERS")
        {
            var ids=(scope.Values??[]).Select(v=>long.TryParse(v,out var id)?id:0).Where(x=>x>0).ToArray();
            return q.Where(x=>x.Id==actor.Id||ids.Contains(x.Id));
        }
        if(mode=="SELECTED_ORGS")
        {
            var ids=await AllowedOrgIdsAsync(db,actor,"USERS");
            return q.Where(x=>x.Id==actor.Id||ids.Contains(x.OrgUnitId));
        }
        return q.Where(x=>x.Id==actor.Id);
    }

    public static async Task<bool> CanAccessEmployeeAsync(AppDbContext db,AppUser actor,long targetUserId)
    {
        if(IsRoot(actor)||actor.Id==targetUserId)return true;
        if(!await CanAsync(db,actor,"USERS","VIEW"))return false;
        IQueryable<AppUser> q=db.Users.AsNoTracking().Where(x=>x.Id==targetUserId);
        q=await EmployeeScope(db,actor,q);
        return await q.AnyAsync();
    }

    public static async Task SaveAsync(AppDbContext db,long uid,Dictionary<string,string[]> perms,Dictionary<string,AccessScope> scopes)
    {
        // Update only modules supplied by the UI; never wipe unrelated modules.
        var c=db.Database.GetDbConnection();
        var opened=c.State!=ConnectionState.Open;
        if(opened)await c.OpenAsync();
        await using var tx=await c.BeginTransactionAsync();
        try
        {
            foreach(var pair in perms)
            {
                var m=(pair.Key??"").Trim().ToUpperInvariant();
                if(m=="TEAM_MEMBERS")m="USERS";
                if(!Modules.Contains(m))continue;
                await Exec(c,tx,"DELETE FROM UserModulePermissions WHERE UserId=@u AND Module=@m",("@u",uid),("@m",m));
                var selected=new HashSet<string>(pair.Value??[],StringComparer.OrdinalIgnoreCase);
                foreach(var action in Actions)
                    await Exec(c,tx,"INSERT INTO UserModulePermissions(UserId,Module,Permission,IsAllowed) VALUES(@u,@m,@p,@a)",("@u",uid),("@m",m),("@p",action),("@a",selected.Contains(action)?1:0));
            }
            foreach(var pair in scopes)
            {
                var m=(pair.Key??"").Trim().ToUpperInvariant();
                if(m=="TEAM_MEMBERS")m="USERS";
                if(!Modules.Contains(m))continue;
                await Exec(c,tx,"DELETE FROM UserModuleScopes WHERE UserId=@u AND Module=@m",("@u",uid),("@m",m));
                var sc=pair.Value;
                var vals=sc.Values?.Length>0?sc.Values:[""];
                foreach(var v in vals)
                    await Exec(c,tx,"INSERT INTO UserModuleScopes(UserId,Module,ScopeMode,ScopeValue) VALUES(@u,@m,@s,@v)",("@u",uid),("@m",m),("@s",(sc.Mode??"OWN").ToUpperInvariant()),("@v",v??""));
            }
            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
        finally { if(opened)await c.CloseAsync(); }
    }

    static async Task<List<(string Permission,bool Allowed)>> PermissionRows(AppDbContext db,long uid,string m){var r=new List<(string,bool)>();var c=db.Database.GetDbConnection();var o=c.State!=ConnectionState.Open;if(o)await c.OpenAsync();try{await using var cmd=c.CreateCommand();cmd.CommandText="SELECT Permission,IsAllowed FROM UserModulePermissions WHERE UserId=@u AND Module=@m";Add(cmd,"@u",uid);Add(cmd,"@m",m);await using var rd=await cmd.ExecuteReaderAsync();while(await rd.ReadAsync())r.Add((Convert.ToString(rd[0])??"",Convert.ToInt32(rd[1])!=0));}finally{if(o)await c.CloseAsync();}return r;}
    static async Task<List<(string Mode,string Value)>> ScopeRows(AppDbContext db,long uid,string m){var r=new List<(string,string)>();var c=db.Database.GetDbConnection();var o=c.State!=ConnectionState.Open;if(o)await c.OpenAsync();try{await using var cmd=c.CreateCommand();cmd.CommandText="SELECT ScopeMode,ScopeValue FROM UserModuleScopes WHERE UserId=@u AND Module=@m";Add(cmd,"@u",uid);Add(cmd,"@m",m);await using var rd=await cmd.ExecuteReaderAsync();while(await rd.ReadAsync())r.Add((Convert.ToString(rd[0])??"OWN",Convert.ToString(rd[1])??""));}finally{if(o)await c.CloseAsync();}return r;}
    static void Add(DbCommand c,string n,object v){var p=c.CreateParameter();p.ParameterName=n;p.Value=v;c.Parameters.Add(p);}static async Task Exec(DbConnection c,DbTransaction tx,string sql,params(string Name,object Value)[] a){await using var cmd=c.CreateCommand();cmd.Transaction=tx;cmd.CommandText=sql;foreach(var x in a)Add(cmd,x.Name,x.Value);await cmd.ExecuteNonQueryAsync();}

    public static async Task<bool> CanModuleOrgAsync(AppDbContext db,AppUser actor,string module,string orgCode)
    {
        if(IsRoot(actor)) return true;
        var scope=await GetScopeAsync(db,actor,module);
        var mode=(scope.Mode??"OWN_ORG").Trim().ToUpperInvariant();
        if(mode=="ALL") return true;
        if(mode=="OWN_ORG")
        {

            var own=await db.OrgUnits.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED").FirstOrDefaultAsync(x=>x.Id==actor.OrgUnitId);
            return own is not null && own.Code.Equals(orgCode,StringComparison.OrdinalIgnoreCase);
        }
        if(mode=="SELECTED_ORGS") return (scope.Values??[]).Contains(orgCode,StringComparer.OrdinalIgnoreCase);
        return false;
    }

    public static async Task<bool> CanKpiUserAsync(AppDbContext db,AppUser actor,long targetUserId)
    {
        if(AuthService.IsManagerRole(actor.Role)) return true;
        var scope=await GetScopeAsync(db,actor,"KPI");
        var mode=(scope.Mode??"OWN").Trim().ToUpperInvariant();
        if(mode=="ALL") return true;
        if(mode=="OWN") return actor.Id==targetUserId;
        var target=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==targetUserId);
        if(target is null) return false;
        if(mode=="OWN_ORG") return target.OrgUnitId==actor.OrgUnitId;
        if(mode=="SELECTED_USERS") return (scope.Values??[]).Contains(targetUserId.ToString());
        if(mode=="SELECTED_ORGS")
        {

            var org=await db.OrgUnits.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED").FirstOrDefaultAsync(x=>x.Id==target.OrgUnitId);
            return org is not null && (scope.Values??[]).Contains(org.Code,StringComparer.OrdinalIgnoreCase);
        }
        if(mode=="OWN_TEAM")
        {
            if(actor.Id==targetUserId) return true;
            var role=(actor.Role??"").Trim().ToUpperInvariant();
            if(role is not ("PM" or "PROJECT_MANAGER" or "HOD" or "CIO" or "ADMIN" or "ROOT")) return false;
            var actorProjects=await db.ProjectMembers.AsNoTracking().Where(x=>x.UserId==actor.Id&&x.IsActive).Select(x=>x.ProjectId).ToListAsync();
            return await db.ProjectMembers.AsNoTracking().AnyAsync(x=>x.UserId==targetUserId&&x.IsActive&&actorProjects.Contains(x.ProjectId));
        }
        return false;
    }

    public static async Task<bool> CanProjectAsync(
        AppDbContext db,
        AppUser actor,
        long projectId)
    {
        if(AuthService.IsManagerRole(actor.Role)) return true;

        var scope=await GetScopeAsync(db,actor,"PROJECTS");
        var mode=(scope.Mode??"ALL").Trim().ToUpperInvariant();

        if(mode=="ALL") return true;

        if(mode=="ASSIGNED_PROJECTS")
            return await db.ProjectMembers.AsNoTracking()
                .AnyAsync(x=>x.UserId==actor.Id && x.ProjectId==projectId && x.IsActive);

        if(mode=="SELECTED_PROJECTS")
            return (scope.Values??[]).Contains(projectId.ToString());

        var projectOrgIds=await db.ProjectOrgUnits.AsNoTracking()
            .Where(x=>x.ProjectId==projectId)
            .Select(x=>x.OrgUnitId)
            .ToListAsync();

        if(projectOrgIds.Count==0)
        {
            var primaryOrg=await db.Projects.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED")
                .Where(x=>x.Id==projectId)
                .Select(x=>x.OrgUnitId)
                .FirstOrDefaultAsync();

            if(primaryOrg>0)
                projectOrgIds.Add(primaryOrg);
        }

        if(mode=="OWN_ORG")
            return projectOrgIds.Contains(actor.OrgUnitId);

        if(mode=="SELECTED_ORGS")
        {
            var selected=scope.Values??[];
            if(!selected.Any()) return false;

            var allowedOrgIds=await db.OrgUnits.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED")
                .Where(x=>selected.Contains(x.Code))
                .Select(x=>x.Id)
                .ToListAsync();

            return projectOrgIds.Any(allowedOrgIds.Contains);
        }

        return false;
    }

    public static async Task<IQueryable<Project>> ApplyProjectScopeAsync(
        AppDbContext db,
        AppUser actor,
        IQueryable<Project> query)
    {
        if(AuthService.IsManagerRole(actor.Role)) return query;

        var scope=await GetScopeAsync(db,actor,"PROJECTS");
        var mode=(scope.Mode??"ALL").Trim().ToUpperInvariant();

        if(mode=="ALL") return query;

        if(mode=="ASSIGNED_PROJECTS")
        {
            var ids=db.ProjectMembers.AsNoTracking()
                .Where(x=>x.UserId==actor.Id && x.IsActive)
                .Select(x=>x.ProjectId);

            return query.Where(x=>x.CreatedByUserId==actor.Id || ids.Contains(x.Id));
        }

        if(mode=="SELECTED_PROJECTS")
        {
            var ids=(scope.Values??[])
                .Select(x=>long.TryParse(x,out var id)?id:0)
                .Where(x=>x>0)
                .ToList();

            return query.Where(x=>x.CreatedByUserId==actor.Id || ids.Contains(x.Id));
        }

        if(mode=="OWN_ORG")
        {
            var orgId=actor.OrgUnitId;
            var relIds=db.ProjectOrgUnits.AsNoTracking()
                .Where(x=>x.OrgUnitId==orgId)
                .Select(x=>x.ProjectId);

            return query.Where(x=>
    x.CreatedByUserId==actor.Id ||
    relIds.Contains(x.Id) ||
    x.OrgUnitId==orgId);
        }

        if(mode=="SELECTED_ORGS")
        {
            var codes=scope.Values??[];
            var orgIds=db.OrgUnits.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED")
                .Where(x=>codes.Contains(x.Code))
                .Select(x=>x.Id);

            var relIds=db.ProjectOrgUnits.AsNoTracking()
                .Where(x=>orgIds.Contains(x.OrgUnitId))
                .Select(x=>x.ProjectId);

            return query.Where(x=>
    x.CreatedByUserId==actor.Id ||
    relIds.Contains(x.Id) ||
    orgIds.Contains(x.OrgUnitId));
        }

        return query.Where(x=>x.CreatedByUserId==actor.Id);
    }

}
