using System.Data;
using System.Data.Common;
using MAIPT.PM.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MAIPT.PM.Api.Data;

public sealed record AccessScope(string Mode, string[] Values);
public sealed record AccessSummary(string[] Modules, Dictionary<string,string[]> Permissions, Dictionary<string,AccessScope> Scopes);

public static class RbacService
{
    public static bool IsAdmin(AppUser u)
    {
        // Permanent root administrator.
        if (string.Equals(u.Email, "maipt@maipt.org", StringComparison.OrdinalIgnoreCase))
            return true;

        var role = (u.Role ?? "").Trim().ToUpperInvariant();
        return role is "ADMIN" or "CIO";
    }




    public static readonly string[] Modules = ["DASHBOARD","PROJECTS","TASKS","RISKS","BUDGET","SUPPLIERS","KPI","APPROVALS","REPORTS","EXECUTIVE","DOCUMENTS","ADMINISTRATION"];
    public static readonly string[] Actions = ["VIEW","CREATE","EDIT","DELETE","SUBMIT","APPROVE","REPORT","FULL"];

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
        if(role is "ADMIN" or "CIO") return new(Actions,StringComparer.OrdinalIgnoreCase);
        if(role is "HOD" or "PM" or "PROJECT_MANAGER")
        {
            var mods=new[]{"DASHBOARD","PROJECTS","TASKS","RISKS","BUDGET","SUPPLIERS","KPI","APPROVALS","REPORTS","EXECUTIVE","DOCUMENTS"};
            return mods.Contains(module)?new(Actions.Where(x=>x!="FULL"),StringComparer.OrdinalIgnoreCase):new(StringComparer.OrdinalIgnoreCase);
        }
        if(role=="PROCUREMENT" && module=="SUPPLIERS") return new(["VIEW","CREATE","EDIT","SUBMIT"],StringComparer.OrdinalIgnoreCase);
        return module switch {
            "BUDGET"=>new(["VIEW","CREATE","EDIT","SUBMIT"],StringComparer.OrdinalIgnoreCase),
            "KPI"=>new(["VIEW","CREATE","EDIT","SUBMIT"],StringComparer.OrdinalIgnoreCase),
            "PROJECTS"=>new(["VIEW"],StringComparer.OrdinalIgnoreCase),
            "TASKS"=>new(["VIEW","EDIT"],StringComparer.OrdinalIgnoreCase),
            "DOCUMENTS"=>new(["VIEW","CREATE"],StringComparer.OrdinalIgnoreCase),
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
        if (string.Equals(u.Email, "maipt@maipt.org", StringComparison.OrdinalIgnoreCase))
            return new HashSet<string>(Actions, StringComparer.OrdinalIgnoreCase);

        // ROOT ADMIN BYPASS: PERMISSIONS
        if (string.Equals(u.Email, "maipt@maipt.org", StringComparison.OrdinalIgnoreCase))
            return new HashSet<string>(Actions, StringComparer.OrdinalIgnoreCase);

        var rows=await PermissionRows(db,u.Id,module.ToUpperInvariant());
        if(rows.Count==0)return DefaultPerm(u,module);
        var set=new HashSet<string>(rows.Where(x=>x.Allowed).Select(x=>x.Permission),StringComparer.OrdinalIgnoreCase);
        if(set.Contains("FULL"))foreach(var a in Actions)set.Add(a);
        return set;
    }
    public static async Task<bool> CanAsync(AppDbContext db,AppUser u,string module,string action){var p=await GetPermissionsAsync(db,u,module);return p.Contains("FULL")||p.Contains(action);}
    public static async Task<AccessScope> GetScopeAsync(AppDbContext db,AppUser u,string module)
    {
        // ROOT MAIPT ALL DATA
        if (string.Equals(u.Email, "maipt@maipt.org", StringComparison.OrdinalIgnoreCase))
            return new("ALL", []);

        // ROOT ADMIN BYPASS: DATA SCOPE
        if (string.Equals(u.Email, "maipt@maipt.org", StringComparison.OrdinalIgnoreCase))
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
        var p=(path??"").ToLowerInvariant(); string? m=
            p.StartsWith("/api/access")||p.StartsWith("/api/settings")?"ADMINISTRATION":
            p.StartsWith("/api/budget")?"BUDGET":p.StartsWith("/api/performance")||p.StartsWith("/api/kpi")?"KPI":
            p.StartsWith("/api/approvals")?"APPROVALS":p.StartsWith("/api/dashboard")?"DASHBOARD":
            p.StartsWith("/api/projects")||p.StartsWith("/api/project-members")||p.StartsWith("/api/team/users")?"PROJECTS":
            p.StartsWith("/api/tasks")||p.StartsWith("/api/milestones")?"TASKS":p.StartsWith("/api/risks")||p.StartsWith("/api/issues")||p.StartsWith("/api/change-requests")?"RISKS":
            p.StartsWith("/api/suppliers")||p.StartsWith("/api/contracts")||p.StartsWith("/api/deliverables")?"SUPPLIERS":
            p.StartsWith("/api/reports")?"REPORTS":p.StartsWith("/api/presentation")?"EXECUTIVE":p.StartsWith("/api/documents")?"DOCUMENTS":null;
        if(m==null)return(null,null);var v=(method??"GET").ToUpperInvariant();var a=v=="GET"?"VIEW":v=="DELETE"?"DELETE":v is "PUT" or "PATCH"?"EDIT":p.Contains("approve")||p.Contains("reject")||p.StartsWith("/api/approvals/")?"APPROVE":p.Contains("submit")?"SUBMIT":"CREATE";return(m,a);
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

    public static async Task SaveAsync(AppDbContext db,long uid,Dictionary<string,string[]> perms,Dictionary<string,AccessScope> scopes)
    {
        var c=db.Database.GetDbConnection();var opened=c.State!=ConnectionState.Open;if(opened)await c.OpenAsync();await using var tx=await c.BeginTransactionAsync();try{
            await Exec(c,tx,"DELETE FROM UserModulePermissions WHERE UserId=@u",("@u",uid));await Exec(c,tx,"DELETE FROM UserModuleScopes WHERE UserId=@u",("@u",uid));
            foreach(var m in Modules){var a=perms.TryGetValue(m,out var list)?new HashSet<string>(list??[],StringComparer.OrdinalIgnoreCase):new(StringComparer.OrdinalIgnoreCase);foreach(var x in Actions)await Exec(c,tx,"INSERT INTO UserModulePermissions(UserId,Module,Permission,IsAllowed) VALUES(@u,@m,@p,@a)",("@u",uid),("@m",m),("@p",x),("@a",a.Contains(x)?1:0));if(scopes.TryGetValue(m,out var sc)){var vals=sc.Values?.Length>0?sc.Values:[""];foreach(var v in vals)await Exec(c,tx,"INSERT INTO UserModuleScopes(UserId,Module,ScopeMode,ScopeValue) VALUES(@u,@m,@s,@v)",("@u",uid),("@m",m),("@s",sc.Mode.ToUpperInvariant()),("@v",v??""));}}
            await tx.CommitAsync();}catch{await tx.RollbackAsync();throw;}finally{if(opened)await c.CloseAsync();}
    }

    static async Task<List<(string Permission,bool Allowed)>> PermissionRows(AppDbContext db,long uid,string m){var r=new List<(string,bool)>();var c=db.Database.GetDbConnection();var o=c.State!=ConnectionState.Open;if(o)await c.OpenAsync();try{await using var cmd=c.CreateCommand();cmd.CommandText="SELECT Permission,IsAllowed FROM UserModulePermissions WHERE UserId=@u AND Module=@m";Add(cmd,"@u",uid);Add(cmd,"@m",m);await using var rd=await cmd.ExecuteReaderAsync();while(await rd.ReadAsync())r.Add((Convert.ToString(rd[0])??"",Convert.ToInt32(rd[1])!=0));}finally{if(o)await c.CloseAsync();}return r;}
    static async Task<List<(string Mode,string Value)>> ScopeRows(AppDbContext db,long uid,string m){var r=new List<(string,string)>();var c=db.Database.GetDbConnection();var o=c.State!=ConnectionState.Open;if(o)await c.OpenAsync();try{await using var cmd=c.CreateCommand();cmd.CommandText="SELECT ScopeMode,ScopeValue FROM UserModuleScopes WHERE UserId=@u AND Module=@m";Add(cmd,"@u",uid);Add(cmd,"@m",m);await using var rd=await cmd.ExecuteReaderAsync();while(await rd.ReadAsync())r.Add((Convert.ToString(rd[0])??"OWN",Convert.ToString(rd[1])??""));}finally{if(o)await c.CloseAsync();}return r;}
    static void Add(DbCommand c,string n,object v){var p=c.CreateParameter();p.ParameterName=n;p.Value=v;c.Parameters.Add(p);}static async Task Exec(DbConnection c,DbTransaction tx,string sql,params(string Name,object Value)[] a){await using var cmd=c.CreateCommand();cmd.Transaction=tx;cmd.CommandText=sql;foreach(var x in a)Add(cmd,x.Name,x.Value);await cmd.ExecuteNonQueryAsync();}
}
