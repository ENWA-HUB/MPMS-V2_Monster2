using MAIPT.PM.Api.Services;
using ClosedXML.Excel;
using MAIPT.PM.Api.Endpoints;
using MAIPT.PM.Api.Data;
using MAIPT.PM.Api.Models;
using Microsoft.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using System.Threading.RateLimiting;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();

// Trust the reverse proxy (IIS / MonsterASP) so Request.IsHttps and RemoteIpAddress
// reflect the original client, which the secure-cookie and rate-limit logic rely on.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

// Cap request bodies so a single upload cannot exhaust disk / memory.
// Override per-endpoint with [RequestSizeLimit] where a larger file is expected.
const long MaxUploadBytes = 64L * 1024 * 1024;
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = MaxUploadBytes;
    o.ValueLengthLimit = 8 * 1024 * 1024;
});
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = MaxUploadBytes);

// Brute-force protection for the credential endpoint.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", http =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0
            }));
});
builder.Services.AddSingleton<M365StorageService>();
var provider = (builder.Configuration["DB_PROVIDER"] ?? "sqlite").ToLowerInvariant();
var sqlitePath = builder.Configuration.GetConnectionString("Sqlite") ?? "Data Source=data/maipt-pm.db";
var sqlServerConnection = Environment.GetEnvironmentVariable("MPMS_SQLSERVER_CONNECTION_STRING")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__SqlServer");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.AddInterceptors(new AuditSaveChangesInterceptor());
    if (provider == "sqlserver")
    {
        if(string.IsNullOrWhiteSpace(sqlServerConnection))
            throw new InvalidOperationException(
                "SQL Server is selected but no server-side connection string is configured. " +
                "Set MPMS_SQLSERVER_CONNECTION_STRING or ConnectionStrings__SqlServer.");
        options.UseSqlServer(sqlServerConnection);
    }
    else
        options.UseSqlite(sqlitePath);
});

// Frontend and API are served from the same origin (single-port hosting), so CORS
// only needs to admit explicitly configured extra origins. MPMS_CORS_ORIGINS is an
// optional comma-separated allow-list; without it, cross-origin browser calls are refused.
var corsOrigins = (Environment.GetEnvironmentVariable("MPMS_CORS_ORIGINS") ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    if (corsOrigins.Length > 0)
        p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Persist the Data Protection key ring outside the publish folder so encrypted
// values (e.g. IT domain passwords) survive an app-pool recycle or redeploy.
var dpKeys = Environment.GetEnvironmentVariable("MPMS_DATAPROTECTION_KEYS");
if (string.IsNullOrWhiteSpace(dpKeys))
    dpKeys = Path.Combine(builder.Environment.ContentRootPath, "data", "dp-keys");
Directory.CreateDirectory(dpKeys);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dpKeys))
    .SetApplicationName("MPMS");
var app = builder.Build();
app.UseForwardedHeaders();
// CONTRACT_ORGUNIT_SCHEMA_V2
using(var contractBuScope=app.Services.CreateScope())
{
    var db=contractBuScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var pn=db.Database.ProviderName??"";
    if(pn.Contains("SqlServer",StringComparison.OrdinalIgnoreCase))
    {
        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('Contracts','OrgUnitId') IS NULL
BEGIN
    ALTER TABLE Contracts
    ADD OrgUnitId bigint NOT NULL
        CONSTRAINT DF_Contracts_OrgUnitId DEFAULT(0);
END
");
    }
    else if(pn.Contains("Sqlite",StringComparison.OrdinalIgnoreCase))
    {
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Contracts ADD COLUMN OrgUnitId INTEGER NOT NULL DEFAULT 0"); } catch { }
        await db.Database.ExecuteSqlRawAsync(@"
UPDATE Contracts
SET OrgUnitId=(SELECT p.OrgUnitId FROM Projects p WHERE p.Id=Contracts.ProjectId)
WHERE IFNULL(OrgUnitId,0)=0
AND EXISTS(SELECT 1 FROM Projects p WHERE p.Id=Contracts.ProjectId);");
    }
}




// Opt-in once the deployment is confirmed to terminate TLS (avoids redirect loops
// / broken sites on a plain-HTTP host). Set MPMS_REQUIRE_HTTPS=true in production.
if (string.Equals(Environment.GetEnvironmentVariable("MPMS_REQUIRE_HTTPS"), "true", StringComparison.OrdinalIgnoreCase))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Baseline security headers on every response.
app.Use(async (context, next) =>
{
    var h = context.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    h["Cross-Origin-Opener-Policy"] = "same-origin";
    await next();
});

app.UseRateLimiter();

app.UseDefaultFiles();
app.UseStaticFiles();
var storagePath = Path.Combine(app.Environment.ContentRootPath, "storage");
Directory.CreateDirectory(storagePath);
Directory.CreateDirectory(Path.Combine(storagePath, "documents"));
// SECURITY: /storage is not publicly exposed. Files require authenticated download API.
Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "data"));
app.UseCors();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await AuditSchema.EnsureAsync(db);
    await RbacService.EnsureSchemaAsync(db);
    await ITAssetSchema.EnsureAsync(db);
    await PersonalFcSchema.EnsureAsync(db);
    // PROJECT_CODE_BACKFILL_V1
    // Older Projects may have been created before automatic PRJ codes existed.
    var blankProjects = await db.Projects
        .Where(x => x.Code == null || x.Code == "")
        .OrderBy(x => x.Id)
        .ToListAsync();

    if (blankProjects.Count > 0)
    {
        var existingCodes = await db.Projects
            .Where(x => x.Code != null && x.Code.StartsWith("PRJ-"))
            .Select(x => x.Code)
            .ToListAsync();

        var maxProjectNo = 0;
        foreach (var code in existingCodes)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                code ?? "",
                @"^PRJ-(\d+)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success &&
                int.TryParse(match.Groups[1].Value, out var n) &&
                n > maxProjectNo)
                maxProjectNo = n;
        }

        foreach (var project in blankProjects)
        {
            maxProjectNo++;
            project.Code = $"PRJ-{maxProjectNo:0000}";
            project.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        Console.WriteLine($"PROJECT_CODE_BACKFILL_V1: assigned codes to {blankProjects.Count} project(s).");
    }

    await ProjectOrgUnitSchema.EnsureAsync(db);
    // BusinessUnitProfileSchema was never wired into startup, so the OrgUnits
    // company-profile columns (CompanyCode, TaxCode, ...) were missing on any DB
    // that had not been patched by hand. It is idempotent and provider-aware.
    await BusinessUnitProfileSchema.EnsureAsync(db);
    await SeedData.InitializeAsync(db);
    await PlanningSeedData.InitializeAsync(db, app.Environment);
    await AuthService.EnsureSchemaAsync(db);
    await AuthService.SeedBootstrapAdminAsync(db);
}

// KPI_ITEM_PREFIX_CLEANUP_V1
// Remove prefixes that an earlier UI version incorrectly stored on KPI items.
using (var kpiPrefixCleanupScope = app.Services.CreateScope())
{
    var db = kpiPrefixCleanupScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var affected = await db.PerformanceItems
        .Where(x => x.Plan.StartsWith("[UPF][") || x.Plan.StartsWith("[Project][") || x.Plan.StartsWith("[PRJ]["))
        .ToListAsync();
    foreach (var item in affected)
        item.Plan = System.Text.RegularExpressions.Regex.Replace(
            item.Plan ?? "",
            @"^\[(?:UPF|Project|PRJ)\]\[[^\]]+\]\s*",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    if (affected.Count > 0)
        await db.SaveChangesAsync();

    var prefixedPeriods=await db.PerformancePeriods
        .Where(x=>x.Level=="DEPARTMENT"&&x.EmployeeName.StartsWith("[UPF]["))
        .ToListAsync();
    if(prefixedPeriods.Count>0)
    {
        var unitCodes=await db.OrgUnits.AsNoTracking().ToDictionaryAsync(x=>x.Name,x=>x.Code);
        var userIds=prefixedPeriods.Where(x=>x.UserId.HasValue).Select(x=>x.UserId!.Value).Distinct().ToArray();
        var userNames=await db.Users.AsNoTracking().Where(x=>userIds.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,x=>x.Name);
        foreach(var period in prefixedPeriods)
        {
            var baseName=period.UserId.HasValue&&userNames.TryGetValue(period.UserId.Value,out var userName)
                ? userName
                : System.Text.RegularExpressions.Regex.Replace(period.EmployeeName??"",@"^\[(?:UPF|Project|PRJ)\]\[[^\]]+\]\s*","",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var unitCode=unitCodes.TryGetValue(period.Department??"",out var code)?code:period.Department;
            period.EmployeeName=KpiPeriodEmployeeName(baseName,period.Level,unitCode);
        }
        await db.SaveChangesAsync();
    }
}


// MPMS API authentication gate.
// Static app assets and login/health endpoints remain public; all other API calls require a valid session.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    var isApi = path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);
    var isPublicApi = path.Equals("/api/health", StringComparison.OrdinalIgnoreCase) ||
                      path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase);
    if (!isApi || isPublicApi)
    {
        await next();
        return;
    }

    using var scope = context.RequestServices.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var auth = await AuthService.ResolveAsync(context, db);
    if (auth.User is null || auth.Account is null)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { message = "Authentication required." });
        return;
    }

    context.Items["AuthUserId"] = auth.User.Id;
    context.Items["AuthUserRole"] = auth.User.Role;
    context.Items["AuthUserName"] = auth.User.Name;
    context.Items["MustChangePassword"] = auth.Account.MustChangePassword;

    // MPMS_SELF_PROFILE_RBAC_V1
    // Every authenticated user may view/update only their own HR profile,
    // profile image and signature. Other users still require USERS permissions.
    var selfProfileAccess=false;
    var selfProfileMatch=System.Text.RegularExpressions.Regex.Match(
        path,
        @"^/api/users/(?<id>\d+)/(profile|profile-image|signature)(/view)?$",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    if(selfProfileMatch.Success
       && long.TryParse(selfProfileMatch.Groups["id"].Value,out var selfProfileUserId)
       && selfProfileUserId==auth.User.Id)
    {
        selfProfileAccess=true;
    }

    // ROOT_SUPERADMIN_V1
    // The ROOT role is the system super administrator and bypasses functional RBAC
    // plus data-scope checks enforced by this middleware. Identity is the role only,
    // never a display name / email value that a user-management screen can set.
    var isRoot = RbacService.IsRoot(auth.User);

    var mapped = RbacService.Map(path, context.Request.Method);
    if (!isRoot && !selfProfileAccess)
    {
        if (mapped.Module is not null && mapped.Action is not null)
        {
            if (!await RbacService.CanAsync(db, auth.User, mapped.Module, mapped.Action))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { message = "You do not have permission for this function." });
                return;
            }
        }
        else if (!RbacService.IsInlineAuthorized(path))
        {
            // Fail closed: an /api/ path that is neither mapped to a permission nor on
            // the inline-authorized list is a routing gap, not a public endpoint.
            context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("Rbac")
                .LogWarning("Denied unauthorized API path {Method} {Path}", context.Request.Method, path);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { message = "You do not have permission for this function." });
            return;
        }
    }

    if (auth.Account.MustChangePassword &&
        !path.Equals("/api/auth/me", StringComparison.OrdinalIgnoreCase) &&
        !path.Equals("/api/auth/logout", StringComparison.OrdinalIgnoreCase) &&
        !path.Equals("/api/auth/change-password", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { message = "Password change required.", code = "PASSWORD_CHANGE_REQUIRED" });
        return;
    }
    AuditActor.UserId = Convert.ToInt64(context.Items["AuthUserId"]);
    try
    {
        await next();
    }
    finally
    {
        AuditActor.UserId = null;
    }
});


// Master data upgrade: Category table for existing SQLite databases.
using (var categoryScope = app.Services.CreateScope())
{
    var categoryDb = categoryScope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (categoryDb.Database.IsSqlServer())
    {
        await categoryDb.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[Categories]', N'U') IS NULL
            BEGIN
                CREATE TABLE [Categories] (
                    [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Categories] PRIMARY KEY,
                    [Code] NVARCHAR(450) NOT NULL,
                    [Name] NVARCHAR(MAX) NOT NULL,
                    [Scope] NVARCHAR(450) NOT NULL DEFAULT 'GENERAL',
                    [Description] NVARCHAR(MAX) NOT NULL DEFAULT '',
                    [Status] NVARCHAR(MAX) NOT NULL DEFAULT 'ACTIVE',
                    [CreatedAt] DATETIME2 NOT NULL,
                    [UpdatedAt] DATETIME2 NOT NULL
                );
                CREATE UNIQUE INDEX [IX_Categories_Scope_Code]
                    ON [Categories] ([Scope], [Code]);
            END;
            """);
    }
    else
    {
        await categoryDb.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS Categories (" +
            "Id INTEGER NOT NULL CONSTRAINT PK_Categories PRIMARY KEY AUTOINCREMENT," +
            "Code TEXT NOT NULL," +
            "Name TEXT NOT NULL," +
            "Scope TEXT NOT NULL DEFAULT 'GENERAL'," +
            "Description TEXT NOT NULL DEFAULT ''," +
            "Status TEXT NOT NULL DEFAULT 'ACTIVE'," +
            "CreatedAt TEXT NOT NULL," +
            "UpdatedAt TEXT NOT NULL);");
    }
    if (!categoryDb.Database.IsSqlServer())
    {
        await categoryDb.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_Categories_Scope_Code ON Categories (Scope, Code);");
    }
}


static string? ResolveDocumentPhysicalPath(IWebHostEnvironment env, string? storedPath)
{
    if (string.IsNullOrWhiteSpace(storedPath)) return null;

    var decoded = Uri.UnescapeDataString(storedPath).Trim();
    decoded = decoded.Replace('\\','/').TrimStart('/');

    var relative = decoded;
    if (relative.StartsWith("storage/", StringComparison.OrdinalIgnoreCase))
        relative = relative["storage/".Length..];

    var configuredRoot = Environment.GetEnvironmentVariable("MPMS_STORAGE_ROOT");
    var storageRoot = string.IsNullOrWhiteSpace(configuredRoot)
        ? Path.Combine(env.ContentRootPath, "storage")
        : Path.GetFullPath(configuredRoot);

    var full = Path.GetFullPath(Path.Combine(
        storageRoot,
        relative.Replace('/', Path.DirectorySeparatorChar)));

    var rootFull = Path.GetFullPath(storageRoot).TrimEnd(Path.DirectorySeparatorChar)
                   + Path.DirectorySeparatorChar;

    if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        return null;

    return full;
}


static async Task<string> NextCodeAsync(AppDbContext db,string tableName,string prefix)
{
    if (!System.Text.RegularExpressions.Regex.IsMatch(tableName,"^[A-Za-z0-9_]+$"))
        throw new InvalidOperationException("Invalid table name.");

    var conn=db.Database.GetDbConnection();
    if(conn.State!=System.Data.ConnectionState.Open) await conn.OpenAsync();
    await using var cmd=conn.CreateCommand();
    cmd.CommandText=$"SELECT Code FROM [{tableName}] WHERE Code LIKE @pfx";
    var pp=cmd.CreateParameter(); pp.ParameterName="@pfx"; pp.Value=prefix+"-%"; cmd.Parameters.Add(pp);
    var max=0;
    await using var r=await cmd.ExecuteReaderAsync();
    while(await r.ReadAsync())
    {
        var code=Convert.ToString(r.GetValue(0))??"";
        var m=System.Text.RegularExpressions.Regex.Match(code,"^"+System.Text.RegularExpressions.Regex.Escape(prefix)+@"-(\d+)$",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if(m.Success && int.TryParse(m.Groups[1].Value,out var n) && n>max) max=n;
    }
    return $"{prefix}-{max+1:0000}";
}


var mergeEntityConfig = new Dictionary<string,(string Table,string PermissionModule,string DocumentEntityType)>(StringComparer.OrdinalIgnoreCase)
{
    ["PROJECTS"]  = ("Projects",    "PROJECTS",  "PROJECT"),
    ["TASKS"]     = ("Tasks",       "TASKS",     "TASK"),
    ["RISKS"]     = ("Risks",       "RISKS",     "RISK"),
    ["ISSUES"]    = ("Issues",      "RISKS",     "ISSUE"),
    ["BUDGETS"]   = ("BudgetLines", "BUDGET",    "BUDGET"),
    ["SUPPLIERS"] = ("Suppliers",   "SUPPLIERS", "SUPPLIER"),
    ["CONTRACTS"] = ("Contracts",   "SUPPLIERS", "CONTRACT")
};



// ---------- Users HR Profile schema ----------
using (var hrScope = app.Services.CreateScope())
{
    var hrDb = hrScope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (hrDb.Database.IsSqlServer())
    await hrDb.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
BEGIN
 IF COL_LENGTH(N'dbo.Users','EmployeeCode') IS NULL ALTER TABLE [dbo].[Users] ADD [EmployeeCode] NVARCHAR(64) NULL;
 IF COL_LENGTH(N'dbo.Users','Phone') IS NULL ALTER TABLE [dbo].[Users] ADD [Phone] NVARCHAR(64) NULL;
 IF COL_LENGTH(N'dbo.Users','PersonalEmail') IS NULL ALTER TABLE [dbo].[Users] ADD [PersonalEmail] NVARCHAR(255) NULL;
 IF COL_LENGTH(N'dbo.Users','DateOfBirth') IS NULL ALTER TABLE [dbo].[Users] ADD [DateOfBirth] DATETIME2 NULL;
 IF COL_LENGTH(N'dbo.Users','Gender') IS NULL ALTER TABLE [dbo].[Users] ADD [Gender] NVARCHAR(32) NULL;
 IF COL_LENGTH(N'dbo.Users','Address') IS NULL ALTER TABLE [dbo].[Users] ADD [Address] NVARCHAR(1000) NULL;
 IF COL_LENGTH(N'dbo.Users','JoinDate') IS NULL ALTER TABLE [dbo].[Users] ADD [JoinDate] DATETIME2 NULL;
 IF COL_LENGTH(N'dbo.Users','EmploymentType') IS NULL ALTER TABLE [dbo].[Users] ADD [EmploymentType] NVARCHAR(64) NULL;
 IF COL_LENGTH(N'dbo.Users','ManagerUserId') IS NULL ALTER TABLE [dbo].[Users] ADD [ManagerUserId] BIGINT NULL;
 IF COL_LENGTH(N'dbo.Users','EmergencyContactName') IS NULL ALTER TABLE [dbo].[Users] ADD [EmergencyContactName] NVARCHAR(255) NULL;
 IF COL_LENGTH(N'dbo.Users','EmergencyContactPhone') IS NULL ALTER TABLE [dbo].[Users] ADD [EmergencyContactPhone] NVARCHAR(64) NULL;
 IF COL_LENGTH(N'dbo.Users','Notes') IS NULL ALTER TABLE [dbo].[Users] ADD [Notes] NVARCHAR(MAX) NULL;
 IF COL_LENGTH(N'dbo.Users','ProfileImagePath') IS NULL ALTER TABLE [dbo].[Users] ADD [ProfileImagePath] NVARCHAR(1000) NULL;
 IF COL_LENGTH(N'dbo.Users','SignaturePath') IS NULL ALTER TABLE [dbo].[Users] ADD [SignaturePath] NVARCHAR(1000) NULL;
END;
");
    else if (hrDb.Database.IsSqlite())
    {
        foreach (var col in new[]
        {
            "EmployeeCode TEXT NULL", "Phone TEXT NULL", "PersonalEmail TEXT NULL", "DateOfBirth TEXT NULL",
            "Gender TEXT NULL", "Address TEXT NULL", "JoinDate TEXT NULL", "EmploymentType TEXT NULL",
            "ManagerUserId INTEGER NULL", "EmergencyContactName TEXT NULL", "EmergencyContactPhone TEXT NULL",
            "Notes TEXT NULL", "ProfileImagePath TEXT NULL", "SignaturePath TEXT NULL"
        })
        {
            try { await hrDb.Database.ExecuteSqlRawAsync("ALTER TABLE Users ADD COLUMN " + col); } catch { /* column already present */ }
        }
    }
}


// PROJECT_NOTIFICATION_SCOPE_V2
static async Task AddProjectScopedNotifications(
    AppDbContext db,
    long projectId,
    string type,
    string severity,
    string title,
    string message,
    string entityType,
    long? entityId = null)
{
    var users = await db.Users.AsNoTracking()
        .Where(x => x.Status == "ACTIVE")
        .ToListAsync();

    var recipientIds = new HashSet<long>();

    foreach (var user in users)
    {
        try
        {
            if (await RbacService.IsAllScopeAsync(db, user, "PROJECTS"))
            {
                recipientIds.Add(user.Id);
                continue;
            }

            var projectIds = await RbacService.AllowedProjectIdsAsync(db, user, "PROJECTS");
            if (projectIds.Contains(projectId))
                recipientIds.Add(user.Id);
        }
        catch
        {
            // If a user has no valid PROJECTS scope, do not notify that user.
        }
    }

    foreach (var uid in recipientIds)
    {
        db.Notifications.Add(new NotificationRecord
        {
            UserId = uid,
            ProjectId = projectId,
            Type = type,
            Severity = severity,
            Title = title,
            Message = message,
            EntityType = entityType,
            EntityId = entityId,
            IsRead = false
        });
    }
}

app.MapGet("/api/code-merge/entities", async (HttpContext http,AppDbContext db) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);
    var user=await db.Users.FirstOrDefaultAsync(x=>x.Id==uid);
    if(user is null) return Results.Unauthorized();
    if(!(db.Database.ProviderName??"").Contains("SqlServer",StringComparison.OrdinalIgnoreCase)) return Results.BadRequest(new{message="SQL Server required."});
    var conn=db.Database.GetDbConnection(); if(conn.State!=System.Data.ConnectionState.Open) await conn.OpenAsync();
    var result=new List<object>();
    foreach(var kv in mergeEntityConfig)
    {
        await using var c=conn.CreateCommand();
        c.CommandText="SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME=@t AND COLUMN_NAME='Code'";
        var p1=c.CreateParameter();p1.ParameterName="@t";p1.Value=kv.Value.Table;c.Parameters.Add(p1);
        if(Convert.ToInt32(await c.ExecuteScalarAsync())==0) continue;
        if(await RbacService.CanAsync(db,user,kv.Value.PermissionModule,"EDIT") && await RbacService.CanAsync(db,user,kv.Value.PermissionModule,"DELETE"))
            result.Add(new{entity=kv.Key,table=kv.Value.Table});
    }
    return Results.Ok(result);
});


app.MapGet("/api/code-merge/options/{entity}", async (string entity, HttpContext http, AppDbContext db) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);
    var user=await db.Users.FirstOrDefaultAsync(x=>x.Id==uid);
    if(user is null) return Results.Unauthorized();

    entity=(entity??"").Trim().ToUpperInvariant();
    if(!mergeEntityConfig.TryGetValue(entity,out var cfg))
        return Results.BadRequest(new{message="Unsupported entity."});

    if(!await RbacService.CanAsync(db,user,cfg.PermissionModule,"EDIT") ||
       !await RbacService.CanAsync(db,user,cfg.PermissionModule,"DELETE"))
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    var conn=db.Database.GetDbConnection();
    if(conn.State!=System.Data.ConnectionState.Open) await conn.OpenAsync();

    await using var cmd=conn.CreateCommand();
    cmd.CommandText=$"SELECT Id, Code FROM [{cfg.Table}] WHERE Code IS NOT NULL AND LTRIM(RTRIM(Code)) <> '' ORDER BY Code";

    var rows=new List<object>();
    await using var reader=await cmd.ExecuteReaderAsync();
    while(await reader.ReadAsync())
        rows.Add(new{id=Convert.ToInt64(reader.GetValue(0)),code=Convert.ToString(reader.GetValue(1))??""});

    return Results.Ok(rows);
});

app.MapPost("/api/code-merge/preview", async (UniversalCodeMergeRequest req,HttpContext http,AppDbContext db) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]); var user=await db.Users.FirstOrDefaultAsync(x=>x.Id==uid); if(user is null)return Results.Unauthorized();
    var entity=(req.Entity??"").Trim().ToUpperInvariant(); if(!mergeEntityConfig.TryGetValue(entity,out var cfg))return Results.BadRequest(new{message="Unsupported entity."});
    if(!await RbacService.CanAsync(db,user,cfg.PermissionModule,"EDIT")||!await RbacService.CanAsync(db,user,cfg.PermissionModule,"DELETE"))return Results.StatusCode(403);
    var sc=(req.SourceCode??"").Trim();var tc=(req.TargetCode??"").Trim(); if(sc.Length==0||tc.Length==0||sc.Equals(tc,StringComparison.OrdinalIgnoreCase))return Results.BadRequest(new{message="Invalid source/target."});
    var conn=db.Database.GetDbConnection();if(conn.State!=System.Data.ConnectionState.Open)await conn.OpenAsync();
    async Task<long?> FindId(string code){await using var c=conn.CreateCommand();c.CommandText=$"SELECT TOP 1 Id FROM [{cfg.Table}] WHERE LOWER(Code)=LOWER(@c)";var p2=c.CreateParameter();p2.ParameterName="@c";p2.Value=code;c.Parameters.Add(p2);var raw=await c.ExecuteScalarAsync();return raw is null||raw==DBNull.Value?null:Convert.ToInt64(raw);}
    var sid=await FindId(sc);var tid=await FindId(tc);if(sid is null)return Results.NotFound(new{message=$"Source '{sc}' not found."});if(tid is null)return Results.NotFound(new{message=$"Target '{tc}' not found."});
    var refs=new List<object>();
    await using(var d=conn.CreateCommand()){
        d.CommandText="SELECT OBJECT_SCHEMA_NAME(fkc.parent_object_id),OBJECT_NAME(fkc.parent_object_id),pc.name FROM sys.foreign_key_columns fkc JOIN sys.columns pc ON pc.object_id=fkc.parent_object_id AND pc.column_id=fkc.parent_column_id WHERE fkc.referenced_object_id=OBJECT_ID(@tbl) AND COL_NAME(fkc.referenced_object_id,fkc.referenced_column_id)='Id'";
        var pt=d.CreateParameter();pt.ParameterName="@tbl";pt.Value=cfg.Table;d.Parameters.Add(pt);var found=new List<(string Schema,string Table,string Column)>();await using(var r=await d.ExecuteReaderAsync())while(await r.ReadAsync())found.Add((r.GetString(0),r.GetString(1),r.GetString(2)));
        foreach(var f in found){await using var c=conn.CreateCommand();c.CommandText=$"SELECT COUNT_BIG(*) FROM [{f.Schema.Replace("]","]]")}].[{f.Table.Replace("]","]]")} ] WHERE [{f.Column.Replace("]","]]")}]=@id".Replace("] ]","]] ").Replace("]] ","]]");var p3=c.CreateParameter();p3.ParameterName="@id";p3.Value=sid.Value;c.Parameters.Add(p3);var count=Convert.ToInt64(await c.ExecuteScalarAsync());refs.Add(new{schema=f.Schema,table=f.Table,column=f.Column,rows=count});}
    }
    var docs=await db.Documents.AsNoTracking().LongCountAsync(x=>x.EntityType==cfg.DocumentEntityType&&x.EntityId==sid.Value);
    return Results.Ok(new{entity,source=new{id=sid,code=sc},target=new{id=tid,code=tc},references=refs,documents=docs});
});

app.MapPost("/api/code-merge/execute", async (UniversalCodeMergeRequest req,HttpContext http,AppDbContext db) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);var user=await db.Users.FirstOrDefaultAsync(x=>x.Id==uid);if(user is null)return Results.Unauthorized();
    var entity=(req.Entity??"").Trim().ToUpperInvariant();if(!mergeEntityConfig.TryGetValue(entity,out var cfg))return Results.BadRequest(new{message="Unsupported entity."});
    if(!await RbacService.CanAsync(db,user,cfg.PermissionModule,"EDIT")||!await RbacService.CanAsync(db,user,cfg.PermissionModule,"DELETE"))return Results.StatusCode(403);
    var sc=(req.SourceCode??"").Trim();var tc=(req.TargetCode??"").Trim();if(sc.Length==0||tc.Length==0||sc.Equals(tc,StringComparison.OrdinalIgnoreCase))return Results.BadRequest(new{message="Invalid source/target."});
    await using var tx=await db.Database.BeginTransactionAsync();
    try{
        var conn=db.Database.GetDbConnection();var dbtx=tx.GetDbTransaction();if(conn.State!=System.Data.ConnectionState.Open)await conn.OpenAsync();
        async Task<long?> FindId(string code){await using var c=conn.CreateCommand();c.Transaction=dbtx;c.CommandText=$"SELECT TOP 1 Id FROM [{cfg.Table}] WHERE LOWER(Code)=LOWER(@c)";var p2=c.CreateParameter();p2.ParameterName="@c";p2.Value=code;c.Parameters.Add(p2);var raw=await c.ExecuteScalarAsync();return raw is null||raw==DBNull.Value?null:Convert.ToInt64(raw);}
        var sid=await FindId(sc);var tid=await FindId(tc);if(sid is null||tid is null)throw new InvalidOperationException("Source or target not found.");
        var found=new List<(string Schema,string Table,string Column)>();await using(var d=conn.CreateCommand()){d.Transaction=dbtx;d.CommandText="SELECT OBJECT_SCHEMA_NAME(fkc.parent_object_id),OBJECT_NAME(fkc.parent_object_id),pc.name FROM sys.foreign_key_columns fkc JOIN sys.columns pc ON pc.object_id=fkc.parent_object_id AND pc.column_id=fkc.parent_column_id WHERE fkc.referenced_object_id=OBJECT_ID(@tbl) AND COL_NAME(fkc.referenced_object_id,fkc.referenced_column_id)='Id'";var pt=d.CreateParameter();pt.ParameterName="@tbl";pt.Value=cfg.Table;d.Parameters.Add(pt);await using var r=await d.ExecuteReaderAsync();while(await r.ReadAsync())found.Add((r.GetString(0),r.GetString(1),r.GetString(2)));}
        var moved=new List<object>();
        foreach(var f in found){await using var c=conn.CreateCommand();c.Transaction=dbtx;c.CommandText=$"UPDATE [{f.Schema.Replace("]","]]")}].[{f.Table.Replace("]","]]")} ] SET [{f.Column.Replace("]","]]")}]=@t WHERE [{f.Column.Replace("]","]]")}]=@s".Replace("] ]","]] ").Replace("]] ","]]");var ps=c.CreateParameter();ps.ParameterName="@s";ps.Value=sid.Value;c.Parameters.Add(ps);var pt=c.CreateParameter();pt.ParameterName="@t";pt.Value=tid.Value;c.Parameters.Add(pt);var rows=await c.ExecuteNonQueryAsync();moved.Add(new{table=f.Table,column=f.Column,rows});}
        var docs=await db.Documents.Where(x=>x.EntityType==cfg.DocumentEntityType&&x.EntityId==sid.Value).ToListAsync();foreach(var doc in docs)doc.EntityId=tid.Value;await db.SaveChangesAsync();
        await using(var del=conn.CreateCommand()){del.Transaction=dbtx;del.CommandText=$"DELETE FROM [{cfg.Table}] WHERE Id=@id";var pd=del.CreateParameter();pd.ParameterName="@id";pd.Value=sid.Value;del.Parameters.Add(pd);if(await del.ExecuteNonQueryAsync()!=1)throw new InvalidOperationException("Source delete failed.");}
        await tx.CommitAsync();return Results.Ok(new{message=$"{entity}: '{sc}' merged into '{tc}'.",movedReferences=moved,movedDocuments=docs.Count,sourceDeleted=true});
    }catch(Exception ex){await tx.RollbackAsync();return Results.Conflict(new{message="Merge rolled back. No partial changes committed.",detail=ex.Message});}
});

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", database = provider, utc = DateTime.UtcNow }));

// ---------- Authentication endpoints ----------
app.MapPost("/api/auth/login", async (LoginRequest input, HttpContext http, AppDbContext db) =>
{
    var email = (input.Email ?? "").Trim().ToLowerInvariant();
    var user = await db.Users.FirstOrDefaultAsync(x => x.Email.ToLower() == email && x.Status == "ACTIVE");
    var account = user is null
        ? null
        : await db.AuthAccounts.FirstOrDefaultAsync(x => x.UserId == user.Id && x.IsEnabled);

    if (user is null || account is null || !AuthService.VerifyPassword(input.Password ?? "", account))
    {
        if (account is null) AuthService.DummyVerify(input.Password ?? "");
        return Results.Unauthorized();
    }

    var raw = AuthService.CreateToken();
    db.AuthSessions.Add(new AuthSession
    {
        UserId = user.Id,
        TokenHash = AuthService.HashToken(raw),
        ExpiresAt = DateTime.UtcNow.AddDays(7)
    });

    account.LastLoginAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    http.Response.Cookies.Append(AuthService.CookieName, raw, new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        // Now accurate behind the reverse proxy thanks to UseForwardedHeaders; also
        // forced on when the deployment declares HTTPS via MPMS_REQUIRE_HTTPS.
        Secure = http.Request.IsHttps ||
                 string.Equals(Environment.GetEnvironmentVariable("MPMS_REQUIRE_HTTPS"), "true", StringComparison.OrdinalIgnoreCase),
        Expires = DateTimeOffset.UtcNow.AddDays(7),
        Path = "/"
    });

    return Results.Ok(new
    {
        user.Id,
        user.Name,
        user.Email,
        user.JobTitle,
        user.Department,
        user.Role,
        account.MustChangePassword
    });
}).RequireRateLimiting("login");

app.MapGet("/api/auth/me", async (HttpContext http, AppDbContext db) =>
{
    var auth = await AuthService.ResolveAsync(http, db);
    if (auth.User is null || auth.Account is null) return Results.Unauthorized();

    var u = auth.User;
    return Results.Ok(new
    {
        u.Id,
        u.Name,
        u.Email,
        u.JobTitle,
        u.Department,
        u.Role,
        auth.Account.MustChangePassword
    });
});

app.MapGet("/api/auth/avatar", async (HttpContext http, AppDbContext db, IWebHostEnvironment env) =>
{
    // Prevent browser/proxy from showing previous user's avatar after logout/login.
    http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
    http.Response.Headers.Pragma = "no-cache";
    http.Response.Headers.Expires = "0";

    var uid=Convert.ToInt64(http.Items["AuthUserId"]);
    var u=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid);
    if(u is null || string.IsNullOrWhiteSpace(u.ProfileImagePath)) return Results.NotFound();

    var raw=u.ProfileImagePath!.Trim().Replace('\\','/');
    string? full=null;

    if(Path.IsPathRooted(raw) && File.Exists(raw))
        full=raw;

    var storageRoot=Environment.GetEnvironmentVariable("MPMS_STORAGE_ROOT");
    if(string.IsNullOrWhiteSpace(storageRoot))
        storageRoot=Path.Combine(env.ContentRootPath,"storage");

    var relative=raw.StartsWith("storage/",StringComparison.OrdinalIgnoreCase)
        ? raw["storage/".Length..]
        : raw.TrimStart('/');

    var candidate=Path.GetFullPath(Path.Combine(storageRoot!,relative.Replace('/',Path.DirectorySeparatorChar)));
    if(File.Exists(candidate))
        full=candidate;

    if(full is null)
    {
        var fallback=Path.GetFullPath(Path.Combine(env.ContentRootPath,raw.TrimStart('/').Replace('/',Path.DirectorySeparatorChar)));
        if(File.Exists(fallback)) full=fallback;
    }

    if(full is null) return Results.NotFound();

    var mime=Path.GetExtension(full).ToLowerInvariant() switch
    {
        ".png"=>"image/png",
        ".webp"=>"image/webp",
        ".gif"=>"image/gif",
        _=>"image/jpeg"
    };
    return Results.File(full,mime,enableRangeProcessing:true);
});

app.MapPost("/api/auth/logout", async (HttpContext http, AppDbContext db) =>
{
    if (http.Request.Cookies.TryGetValue(AuthService.CookieName, out var token))
    {
        var tokenHash = AuthService.HashToken(token);
        var session = await db.AuthSessions
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash && x.RevokedAt == null);

        if (session is not null)
        {
            session.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    http.Response.Cookies.Delete(AuthService.CookieName, new CookieOptions { Path = "/" });
    return Results.Ok(new { ok = true });
});

app.MapPost("/api/auth/reset-password", async (HttpContext http, AppDbContext db) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);
    var user=await db.Users.FirstOrDefaultAsync(x=>x.Id==uid);
    var account=await db.AuthAccounts.FirstOrDefaultAsync(x=>x.UserId==uid && x.IsEnabled);
    if(user is null || account is null) return Results.NotFound(new{message="Account not found."});
    if(string.IsNullOrWhiteSpace(user.Email)) return Results.BadRequest(new{message="User email is missing."});

    var host=Environment.GetEnvironmentVariable("MPMS_SMTP_HOST");
    var portText=Environment.GetEnvironmentVariable("MPMS_SMTP_PORT");
    var smtpUser=Environment.GetEnvironmentVariable("MPMS_SMTP_USER");
    var smtpPass=Environment.GetEnvironmentVariable("MPMS_SMTP_PASSWORD");
    var from=Environment.GetEnvironmentVariable("MPMS_SMTP_FROM") ?? smtpUser;
    var sslText=Environment.GetEnvironmentVariable("MPMS_SMTP_SSL");
    if(string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
        return Results.Problem("SMTP is not configured.",statusCode:503);

    var temporaryPassword=AuthService.CreateTemporaryPassword();

    try
    {
        using var msg=new System.Net.Mail.MailMessage();
        msg.From=new System.Net.Mail.MailAddress(from!,"MPMS");
        msg.To.Add(user.Email);
        msg.Subject="MPMS password reset";
        msg.Body=$"Hello {user.Name},\n\nYour MPMS temporary password is:\n\n{temporaryPassword}\n\nPlease sign in and change this password immediately.\n\nMPMS";
        using var smtp=new System.Net.Mail.SmtpClient(host,int.TryParse(portText,out var smtpPort)?smtpPort:587);
        smtp.EnableSsl=!string.Equals(sslText,"false",StringComparison.OrdinalIgnoreCase);
        if(!string.IsNullOrWhiteSpace(smtpUser)) smtp.Credentials=new System.Net.NetworkCredential(smtpUser,smtpPass);
        await smtp.SendMailAsync(msg);
    }
    catch(Exception ex) { return Results.Problem("Unable to send reset email. "+ex.Message,statusCode:502); }

    var p=AuthService.CreatePassword(temporaryPassword);
    account.PasswordHash=p.Hash; account.PasswordSalt=p.Salt; account.PasswordIterations=p.Iterations;
    account.MustChangePassword=true; account.UpdatedAt=DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(new{message=$"A temporary password has been sent to {user.Email}."});
});

app.MapPost("/api/auth/change-password", async (ChangePasswordRequest input, HttpContext http, AppDbContext db) =>
{
    var auth = await AuthService.ResolveAsync(http, db);
    if (auth.User is null || auth.Account is null) return Results.Unauthorized();

    var account = await db.AuthAccounts.FirstAsync(x => x.Id == auth.Account.Id);

    if (!AuthService.VerifyPassword(input.CurrentPassword ?? "", account))
        return Results.BadRequest(new { message = "Current password is incorrect." });

    var strengthError = AuthService.ValidatePasswordStrength(input.NewPassword);
    if (strengthError is not null)
        return Results.BadRequest(new { message = strengthError });

    if (AuthService.VerifyPassword(input.NewPassword!, account))
        return Results.BadRequest(new { message = "New password must be different from the current password." });

    var p = AuthService.CreatePassword(input.NewPassword!);
    account.PasswordHash = p.Hash;
    account.PasswordSalt = p.Salt;
    account.PasswordIterations = p.Iterations;
    account.MustChangePassword = false;
    account.UpdatedAt = DateTime.UtcNow;

    // Invalidate every other session for this user; keep the current one alive.
    var currentTokenHash = http.Request.Cookies.TryGetValue(AuthService.CookieName, out var ct)
        ? AuthService.HashToken(ct) : null;
    await db.AuthSessions
        .Where(x => x.UserId == auth.User.Id && x.RevokedAt == null && x.TokenHash != currentTokenHash)
        .ExecuteUpdateAsync(x => x.SetProperty(v => v.RevokedAt, DateTime.UtcNow));

    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true });
});

app.MapPost("/api/team/users/{id:long}/reset-login", async (long id, ResetPasswordRequest input, HttpContext http, AppDbContext db) =>
{
    var role = Convert.ToString(http.Items["AuthUserRole"]);
    if (!AuthService.IsManagerRole(role))
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    var user = await db.Users.FindAsync(id);
    if (user is null) return Results.NotFound();

    // Prevent lateral / vertical takeover: a caller cannot reset the credentials of an
    // account at or above their own privilege rank (only ROOT can reset a peer/ROOT).
    var callerId = Convert.ToInt64(http.Items["AuthUserId"]);
    if (!string.Equals(role, "ROOT", StringComparison.OrdinalIgnoreCase)
        && callerId != id
        && AuthService.RoleRank(user.Role) >= AuthService.RoleRank(role))
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    // The temporary password is always server-generated; it is never accepted from the
    // request so a manager cannot set a known password on someone else's account.
    var temporaryPassword = AuthService.CreateTemporaryPassword();

    var p = AuthService.CreatePassword(temporaryPassword);

    var account = await db.AuthAccounts.FirstOrDefaultAsync(x => x.UserId == id);
    if (account is null)
    {
        account = new AuthAccount { UserId = id };
        db.AuthAccounts.Add(account);
    }

    account.PasswordHash = p.Hash;
    account.PasswordSalt = p.Salt;
    account.PasswordIterations = p.Iterations;
    account.MustChangePassword = true;
    account.IsEnabled = true;

    await db.AuthSessions
        .Where(x => x.UserId == id && x.RevokedAt == null)
        .ExecuteUpdateAsync(x => x.SetProperty(v => v.RevokedAt, DateTime.UtcNow));

    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        user.Id,
        user.Name,
        user.Email,
        temporaryPassword,
        mustChangePassword = true
    });
});


app.MapGet("/api/dashboard", async (AppDbContext db) =>
{
    var projects = await db.Projects.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED").ToListAsync();
    var budget = await db.BudgetLines.AsNoTracking().ToListAsync();
    var risks = await db.Risks.AsNoTracking().Where(x => x.Status != "CLOSED").ToListAsync();
    var issues = await db.Issues.AsNoTracking().Where(x => x.Status != "CLOSED").ToListAsync();
    var tasks = await db.Tasks.AsNoTracking().Where(x => x.Status != "DONE" && x.Status != "CANCELLED").ToListAsync();
    var contracts = await db.Contracts.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED").ToListAsync();
    var evaluations = await db.SupplierEvaluations.AsNoTracking().ToListAsync();
    var today = DateOnly.FromDateTime(DateTime.Today);

    return Results.Ok(new
    {
        totalProjects = projects.Count,
        activeProjects = projects.Count(x => x.Status == "ACTIVE"),
        greenProjects = projects.Count(x => x.HealthStatus == "GREEN"),
        amberProjects = projects.Count(x => x.HealthStatus == "AMBER"),
        redProjects = projects.Count(x => x.HealthStatus == "RED"),
        portfolioProgress = projects.Count == 0 ? 0 : Math.Round(projects.Average(x => x.ProgressPct), 1),
        revisedBudget = budget.Sum(x => x.RevisedAmount),
        actualCost = budget.Sum(x => x.ActualAmount),
        forecastCost = budget.Sum(x => x.ForecastAmount),
        budgetVariance = budget.Sum(x => x.RevisedAmount - x.ActualAmount),
        openRisks = risks.Count,
        criticalRisks = risks.Count(x => x.SeverityScore >= 17),
        openIssues = issues.Count,
        overdueTasks = tasks.Count(x => x.DueDate.HasValue && x.DueDate.Value < today),
        activeContracts = contracts.Count(x => x.Status == "ACTIVE"),
        contractsExpiringSoon = contracts.Count(x => x.EndDate.HasValue && x.EndDate.Value >= today && x.EndDate.Value <= today.AddDays(60)),
        supplierAverageKpi = evaluations.Count == 0 ? 0 : Math.Round(evaluations.Average(x => x.OverallScore), 2),
        suppliersBelowThreshold = evaluations.Count(x => x.OverallScore < 3.0)
    });
});



// ---------- Executive Dashboard ----------
app.MapGet("/api/dashboard/executive", async (int? budgetYear, string? performancePeriod, AppDbContext db) =>
{
    var today = DateOnly.FromDateTime(DateTime.Today);
    var currentPeriod = string.IsNullOrWhiteSpace(performancePeriod)
        ? $"{today.Year:D4}-{today.Month:D2}"
        : performancePeriod;

    var projects = await db.Projects.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED").Include(x => x.Portfolio).Include(x => x.Owner).ToListAsync();
    var activeProjects = projects.Where(x => x.Status == "ACTIVE").ToList();
    var risks = await db.Risks.AsNoTracking().Include(x => x.Project).Where(x => x.Status != "CLOSED").ToListAsync();
    var issues = await db.Issues.AsNoTracking().Include(x => x.Project).Where(x => x.Status != "CLOSED" && x.Status != "RESOLVED").ToListAsync();
    var tasks = await db.Tasks.AsNoTracking().Include(x => x.Project).Where(x => x.Status != "DONE" && x.Status != "CANCELLED").ToListAsync();
    var milestones = await db.Milestones.AsNoTracking().Include(x => x.Project)
        .Where(x => x.Status != "COMPLETED" && x.Status != "CANCELLED").OrderBy(x => x.DueDate).ToListAsync();

    var availableBudgetYears = await db.BudgetPlanItems.AsNoTracking()
        .Select(x => x.BudgetYear).Distinct().OrderByDescending(x => x).ToListAsync();
    var selectedBudgetYear = budgetYear ?? availableBudgetYears.FirstOrDefault();
    if (selectedBudgetYear == 0) selectedBudgetYear = today.Year;

    var annualBudget = await db.BudgetPlanItems.AsNoTracking()
        .Where(x => x.BudgetYear == selectedBudgetYear).ToListAsync();
    var budgetLines = await db.BudgetLines.AsNoTracking().ToListAsync();

    var supplierEvals = await db.SupplierEvaluations.AsNoTracking().ToListAsync();
    var performancePeriods = await db.PerformancePeriods.AsNoTracking()
        .Where(x => x.Period == currentPeriod).ToListAsync();
    var perfIds = performancePeriods.Select(x => x.Id).ToList();
    var performanceItems = await db.PerformanceItems.AsNoTracking()
        .Where(x => perfIds.Contains(x.PerformancePeriodId)).ToListAsync();

    var projectMembers = await db.ProjectMembers.AsNoTracking()
        .Where(x => x.IsActive).ToListAsync();

    var totalAnnualPlan = annualBudget.Sum(x => x.PlannedAmount);
    var linkedAnnualPlan = annualBudget.Where(x => x.ProjectId.HasValue).Sum(x => x.PlannedAmount);
    var actualCost = budgetLines.Sum(x => x.ActualAmount);
    var revisedBudget = budgetLines.Sum(x => x.RevisedAmount);
    var forecastCost = budgetLines.Sum(x => x.ForecastAmount);

    double WeightedScore(IEnumerable<PerformanceItem> rows)
    {
        var list = rows.ToList();
        var weight = list.Sum(x => x.Weight);
        return weight <= 0 ? 0 : list.Sum(x => x.FinalScore * x.Weight) / weight;
    }

    var teamScores = performancePeriods.Select(p =>
    {
        var rows = performanceItems.Where(x => x.PerformancePeriodId == p.Id);
        return new
        {
            p.Id, p.EmployeeName, p.Department, p.Level, p.IsLocked,
            score = Math.Round(WeightedScore(rows), 2),
            weight = Math.Round(rows.Sum(x => x.Weight), 2),
            items = rows.Count()
        };
    }).OrderByDescending(x => x.score).ToList();

    var avgTeamScore = teamScores.Count == 0 ? 0 : Math.Round(teamScores.Average(x => x.score), 2);

    var projectHealth = projects
        .OrderBy(x => x.HealthStatus == "RED" ? 0 : x.HealthStatus == "AMBER" ? 1 : 2)
        .ThenByDescending(x => x.Priority == "CRITICAL" ? 4 : x.Priority == "HIGH" ? 3 : x.Priority == "MEDIUM" ? 2 : 1)
        .Take(10)
        .Select(x => new
        {
            x.Id, x.Code, x.Name, x.Status, x.Priority, x.ProgressPct, x.HealthStatus,
            portfolio = x.Portfolio != null ? x.Portfolio.Name : "Unassigned",
            owner = x.Owner != null ? x.Owner.Name : "Unassigned",
            teamSize = projectMembers.Count(m => m.ProjectId == x.Id)
        }).ToList();

    var riskWatch = risks.OrderByDescending(x => x.SeverityScore).Take(6).Select(x => new
    {
        x.Id, x.Code, x.Title, x.Category, x.SeverityScore, x.Status,
        project = x.Project != null ? x.Project.Name : "Unknown"
    }).ToList();

    var issueWatch = issues
        .OrderByDescending(x => x.Severity == "CRITICAL" ? 4 : x.Severity == "HIGH" ? 3 : x.Severity == "MEDIUM" ? 2 : 1)
        .Take(6).Select(x => new
        {
            x.Id, x.Code, x.Title, x.Severity, x.Status, x.TargetResolutionDate,
            project = x.Project != null ? x.Project.Name : "Unknown"
        }).ToList();

    var nextMilestones = milestones
        .Where(x => !x.DueDate.HasValue || x.DueDate.Value >= today.AddDays(-30))
        .Take(6).Select(x => new
        {
            x.Id, x.Name, x.DueDate, x.Status,
            overdue = x.DueDate.HasValue && x.DueDate.Value < today,
            project = x.Project != null ? x.Project.Name : "Unknown"
        }).ToList();

    var annualBudgetByUnit = annualBudget.GroupBy(x => x.OrgUnit)
        .Select(g => new { name = g.Key, amount = g.Sum(x => x.PlannedAmount), items = g.Count() })
        .OrderByDescending(x => x.amount).Take(8).ToList();

    var projectStatus = projects.GroupBy(x => x.Status)
        .Select(g => new { name = g.Key, value = g.Count() }).OrderByDescending(x => x.value).ToList();

    var health = new[]
    {
        new { name = "Green", value = projects.Count(x => x.HealthStatus == "GREEN") },
        new { name = "Amber", value = projects.Count(x => x.HealthStatus == "AMBER") },
        new { name = "Red", value = projects.Count(x => x.HealthStatus == "RED") }
    };

    var attention = new List<object>();
    if (risks.Count(x => x.SeverityScore >= 17) > 0)
        attention.Add(new { type = "RISK", severity = "CRITICAL", title = $"{risks.Count(x => x.SeverityScore >= 17)} critical risk(s)", detail = "Immediate mitigation review required." });
    var overdueTasks = tasks.Count(x => x.DueDate.HasValue && x.DueDate.Value < today);
    if (overdueTasks > 0)
        attention.Add(new { type = "TASK", severity = "HIGH", title = $"{overdueTasks} overdue task(s)", detail = "Delivery dates require recovery action." });
    var overdueMilestones = milestones.Count(x => x.DueDate.HasValue && x.DueDate.Value < today);
    if (overdueMilestones > 0)
        attention.Add(new { type = "MILESTONE", severity = "HIGH", title = $"{overdueMilestones} overdue milestone(s)", detail = "Review project schedule and dependencies." });
    var unlinkedBudget = annualBudget.Count(x => !x.ProjectId.HasValue);
    if (unlinkedBudget > 0)
        attention.Add(new { type = "BUDGET", severity = "MEDIUM", title = $"{unlinkedBudget} budget item(s) not mapped", detail = "Link annual budget plan to projects for full portfolio visibility." });
    var unlockedPeriods = performancePeriods.Count(x => !x.IsLocked);
    if (unlockedPeriods > 0)
        attention.Add(new { type = "KPI", severity = "MEDIUM", title = $"{unlockedPeriods} open performance period(s)", detail = $"Complete evaluation and lock {currentPeriod}." });

    return Results.Ok(new
    {
        generatedAt = DateTime.UtcNow,
        selectedBudgetYear,
        availableBudgetYears,
        performancePeriod = currentPeriod,
        summary = new
        {
            totalProjects = projects.Count,
            activeProjects = activeProjects.Count,
            portfolioProgress = projects.Count == 0 ? 0 : Math.Round(projects.Average(x => x.ProgressPct), 1),
            greenProjects = projects.Count(x => x.HealthStatus == "GREEN"),
            amberProjects = projects.Count(x => x.HealthStatus == "AMBER"),
            redProjects = projects.Count(x => x.HealthStatus == "RED"),
            annualBudgetPlan = totalAnnualPlan,
            annualBudgetLinked = linkedAnnualPlan,
            revisedBudget,
            actualCost,
            forecastCost,
            openRisks = risks.Count,
            criticalRisks = risks.Count(x => x.SeverityScore >= 17),
            openIssues = issues.Count,
            overdueTasks,
            overdueMilestones,
            supplierScore = supplierEvals.Count == 0 ? 0 : Math.Round(supplierEvals.Average(x => x.OverallScore), 2),
            teamPerformanceScore = avgTeamScore,
            teamPeriods = performancePeriods.Count,
            projectTeamMembers = projectMembers.Select(x => x.UserId).Distinct().Count()
        },
        projectHealth,
        riskWatch,
        issueWatch,
        nextMilestones,
        annualBudgetByUnit,
        projectStatus,
        health,
        teamScores,
        attention
    });
});


app.MapGet("/api/projects/overview", async (AppDbContext db) =>
{
    var projects = await db.Projects
        .AsNoTracking()
        .Include(x => x.Portfolio)
        .ToListAsync();

    var today = DateOnly.FromDateTime(DateTime.Today);
    var next6Months = today.AddMonths(6);

    var milestones = await db.Milestones
        .AsNoTracking()
        .Include(x => x.Project)
        .Where(x =>
            x.DueDate.HasValue &&
            x.DueDate.Value >= today &&
            x.DueDate.Value <= next6Months &&
            x.Status != "COMPLETED" &&
            x.Status != "CANCELLED")
        .OrderBy(x => x.DueDate)
        .ThenBy(x => x.Id)
        .Select(x => new
        {
            x.Id,
            x.Name,
            Project = x.Project != null ? x.Project.Name : "Unknown",
            x.DueDate,
            x.Status,
            x.WeightPct
        })
        .ToListAsync();

    var portfolios = projects
        .GroupBy(x => x.Portfolio != null ? x.Portfolio.Name : "Unassigned")
        .Select(g => new { name = g.Key, value = g.Count() })
        .OrderByDescending(x => x.value)
        .ToList();

    return Results.Ok(new
    {
        totalProjects = projects.Count,
        activeProjects = projects.Count(x => x.Status == "ACTIVE"),
        completedProjects = projects.Count(x => x.Status == "COMPLETED"),
        onHoldProjects = projects.Count(x => x.Status == "ON_HOLD"),
        greenProjects = projects.Count(x => x.HealthStatus == "GREEN"),
        amberProjects = projects.Count(x => x.HealthStatus == "AMBER"),
        redProjects = projects.Count(x => x.HealthStatus == "RED"),
        portfolios,
        milestones
    });
});

app.MapGet("/api/projects", async (HttpContext http, AppDbContext db) =>
{
    var userId=Convert.ToInt64(http.Items["AuthUserId"]);
    var actor=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==userId);
    if(actor is null) return Results.Unauthorized();

    IQueryable<Project> scoped=db.Projects.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED");
    scoped=await RbacService.ApplyProjectScopeAsync(db,actor,scoped);
    var scopedIds=await scoped.Select(x=>x.Id).ToListAsync();
    var memberIds=await db.ProjectMembers.AsNoTracking()
        .Where(x=>x.UserId==userId && x.IsActive)
        .Select(x=>x.ProjectId).ToListAsync();
    var visibleIds=scopedIds.Concat(memberIds).Distinct().ToList();

    var q=db.Projects.AsNoTracking().Where(x=>x.Status!="DEACTIVATED")
        .Include(x=>x.OrgUnit).Include(x=>x.Portfolio).Include(x=>x.Owner).Include(x=>x.Sponsor)
        .Where(x=>x.CreatedByUserId==userId || visibleIds.Contains(x.Id));

    return Results.Ok(await q.OrderByDescending(x=>x.UpdatedAt).Select(x=>new
    {
        x.Id,x.OrgUnitId,x.PortfolioId,x.Code,x.Name,x.Description,x.OwnerId,x.SponsorId,
        x.Status,x.Priority,x.StartDate,x.BaselineEndDate,x.EndDate,x.BudgetAmount,x.Currency,
        x.ProgressPct,x.HealthScore,x.HealthStatus,x.CreatedByUserId,x.CreatedAt,x.UpdatedAt,
        OrgUnit=x.OrgUnit!=null?x.OrgUnit.Name:null,
        Portfolio=x.Portfolio!=null?x.Portfolio.Name:null,
        Owner=x.Owner!=null?x.Owner.Name:null,
        Sponsor=x.Sponsor!=null?x.Sponsor.Name:null,
        CreatedByName=x.CreatedByUserId.HasValue
          ? (db.Users.Where(u=>u.Id==x.CreatedByUserId.Value).Select(u=>u.Name).FirstOrDefault() ?? "System Created")
          : "System Created"
    }).ToListAsync());
});

app.MapGet("/api/projects/form-options", async (HttpContext http, AppDbContext db) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);
    var user=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid);
    if(user is null)return Results.Unauthorized();

    var allScope=await RbacService.IsAllScopeAsync(db,user,"PROJECTS");
    var allowedOrgIds=allScope
        ? Array.Empty<long>()
        : await RbacService.AllowedOrgIdsAsync(db,user,"PROJECTS");

    if(!allScope && allowedOrgIds.Length==0 && user.OrgUnitId>0)
        allowedOrgIds=new[]{user.OrgUnitId};

    var orgQ=db.OrgUnits.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED").Where(x=>x.Status=="ACTIVE");
    if(!allScope)
        orgQ=orgQ.Where(x=>allowedOrgIds.Contains(x.Id));

    var orgUnits=await orgQ
        .OrderByDescending(x=>x.Id==user.OrgUnitId)
        .ThenBy(x=>x.Name)
        .Select(x=>new{x.Id,x.Code,x.Name})
        .ToListAsync();

    var portfolios=await db.Portfolios
        .AsNoTracking()
        .Where(x=>x.Status=="ACTIVE")
        .OrderBy(x=>x.Name)
        .Select(x=>new{x.Id,x.Code,x.Name,x.Status})
        .ToListAsync();

    var users=await db.Users
        .AsNoTracking()
        .Include(x=>x.OrgUnit)
        .Where(x=>x.Status=="ACTIVE")
        .OrderBy(x=>x.OrgUnit==null||x.OrgUnit.Code==null||x.OrgUnit.Code=="").ThenBy(x=>x.OrgUnit==null?"":x.OrgUnit.Code)
        .ThenBy(x=>x.Department==null||x.Department=="").ThenBy(x=>x.Department)
        .ThenBy(x=>x.JobTitle==null||x.JobTitle=="").ThenBy(x=>x.JobTitle)
        .ThenBy(x=>x.JoinDate==null).ThenBy(x=>x.JoinDate).ThenBy(x=>x.Name)
        .Select(x=>new{x.Id,x.Name,x.Email,x.EmployeeCode,x.JobTitle,x.Department,x.JoinDate,x.Role,x.OrgUnitId,OrgUnit=x.OrgUnit==null?null:x.OrgUnit.Code,BusinessUnit=x.OrgUnit==null?null:x.OrgUnit.Name})
        .ToListAsync();

    return Results.Ok(new{
        currentOrgUnitId=user.OrgUnitId,
        allOrgUnits=allScope,
        orgUnits,
        portfolios,
        users
    });
});

app.MapPost("/api/projects", async (Project input, HttpContext http, AppDbContext db) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);
    var user=await db.Users.Include(x=>x.OrgUnit).FirstAsync(x=>x.Id==uid);
    if(!await RbacService.CanAsync(db,user,"PROJECTS","CREATE")) return Results.StatusCode(StatusCodes.Status403Forbidden);
    var allScope=await RbacService.IsAllScopeAsync(db,user,"PROJECTS");
    var allowedOrgIds=allScope?Array.Empty<long>():await RbacService.AllowedOrgIdsAsync(db,user,"PROJECTS");
    if(!allScope)
    {
        if(allowedOrgIds.Length==0 && user.OrgUnitId>0) allowedOrgIds=new[]{user.OrgUnitId};
        if(allowedOrgIds.Length==0) return Results.BadRequest(new{message="Your account has no Business Unit scope."});
        if(allowedOrgIds.Length==1) input.OrgUnitId=allowedOrgIds[0];
        else if(input.OrgUnitId<=0 || !allowedOrgIds.Contains(input.OrgUnitId))
            input.OrgUnitId=user.OrgUnitId>0&&allowedOrgIds.Contains(user.OrgUnitId)?user.OrgUnitId:allowedOrgIds[0];
    }
    input.Id=0;
    input.Code=await NextCodeAsync(db,"Projects","PRJ");
    input.CreatedByUserId=user.Id;
    db.Projects.Add(input);
    await db.SaveChangesAsync();
    // PROJECT_CREATOR_AUTO_ACCESS_V1
    if(!await db.ProjectMembers.AnyAsync(x=>x.ProjectId==input.Id && x.UserId==user.Id))
    {
        db.ProjectMembers.Add(new ProjectMember { ProjectId=input.Id, UserId=user.Id });
        await db.SaveChangesAsync();
    }

    if(input.OrgUnitId>0 && !await db.ProjectOrgUnits.AnyAsync(x=>x.ProjectId==input.Id&&x.OrgUnitId==input.OrgUnitId))
    {
        db.ProjectOrgUnits.Add(new ProjectOrgUnit{ProjectId=input.Id,OrgUnitId=input.OrgUnitId,IsPrimary=true});
        await db.SaveChangesAsync();
    }
    return Results.Created($"/api/projects/{input.Id}",input);
});
app.MapPut("/api/projects/{id:long}", async (long id, Project input, HttpContext http, AppDbContext db) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);
    var actor=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid);
    if(actor is null) return Results.Unauthorized();
    if(!await RbacService.CanAsync(db,actor,"PROJECTS","EDIT")) return Results.StatusCode(403);
    var row=await db.Projects.FindAsync(id); if(row is null) return Results.NotFound();
    var hasAccess=row.CreatedByUserId==uid
      || await db.ProjectMembers.AsNoTracking().AnyAsync(x=>x.ProjectId==id&&x.UserId==uid&&x.IsActive)
      || await RbacService.IsAllScopeAsync(db,actor,"PROJECTS");
    if(!hasAccess){IQueryable<Project> sq=db.Projects.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED").Where(x=>x.Id==id);sq=await RbacService.ApplyProjectScopeAsync(db,actor,sq);hasAccess=await sq.AnyAsync();}
    if(!hasAccess) return Results.StatusCode(403);
    if(string.IsNullOrWhiteSpace(row.Code)) row.Code=await NextCodeAsync(db,"Projects","PRJ");
    row.Name=input.Name;row.Description=input.Description;row.PortfolioId=input.PortfolioId;row.OrgUnitId=input.OrgUnitId;
    row.OwnerId=input.OwnerId;row.SponsorId=input.SponsorId;row.Status=input.Status;row.Priority=input.Priority;
    row.ProgressPct=input.ProgressPct;row.HealthScore=input.HealthScore;row.HealthStatus=input.HealthStatus;
    row.StartDate=input.StartDate;row.BaselineEndDate=input.BaselineEndDate;row.EndDate=input.EndDate;
    row.BudgetAmount=input.BudgetAmount;row.Currency=input.Currency;row.UpdatedAt=DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(new{row.Id,row.Code,row.Name,row.Description,row.PortfolioId,row.OrgUnitId,row.OwnerId,row.SponsorId,row.Status,row.Priority,row.ProgressPct,row.HealthScore,row.HealthStatus,row.StartDate,row.BaselineEndDate,row.EndDate,row.BudgetAmount,row.Currency,row.CreatedByUserId,row.CreatedAt,row.UpdatedAt});
});

app.MapGet("/api/portfolios", async (AppDbContext db) => Results.Ok(await db.Portfolios.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED").Select(x => new { x.Id, x.Code, x.Name, x.Description, x.Status }).ToListAsync()));

app.MapGet("/api/users/{id:long}/profile", async (long id, HttpContext http, AppDbContext db) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);
    var current=await db.Users.FirstOrDefaultAsync(x=>x.Id==uid);
    if(current is null) return Results.Unauthorized();
    if(!await RbacService.CanAccessEmployeeAsync(db,current,id))return Results.Forbid();
    var u=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id);
    if(u is null) return Results.NotFound(new{message="User not found."});
    return Results.Ok(new{u.Id,u.Name,u.Email,u.JobTitle,u.Department,u.Role,u.Status,u.OrgUnitId,u.EmployeeCode,u.Phone,u.PersonalEmail,u.DateOfBirth,u.Gender,u.Address,u.JoinDate,u.EmploymentType,u.ManagerUserId,u.EmergencyContactName,u.EmergencyContactPhone,u.Notes,u.ProfileImagePath,u.SignaturePath});
});

app.MapPut("/api/users/{id:long}/profile", async (long id, UserHrProfileRequest input, HttpContext http, AppDbContext db) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);
    var current=await db.Users.FirstOrDefaultAsync(x=>x.Id==uid);
    if(current is null) return Results.Unauthorized();
    if(uid!=id && !await RbacService.CanAsync(db,current,"USERS","EDIT")) return Results.StatusCode(StatusCodes.Status403Forbidden);
    var u=await db.Users.FirstOrDefaultAsync(x=>x.Id==id);
    if(u is null) return Results.NotFound(new{message="User not found."});
    u.EmployeeCode=input.EmployeeCode?.Trim(); u.Phone=input.Phone?.Trim(); u.PersonalEmail=input.PersonalEmail?.Trim();
    u.DateOfBirth=input.DateOfBirth; u.Gender=input.Gender?.Trim(); u.Address=input.Address?.Trim(); u.JoinDate=input.JoinDate;
    u.EmploymentType=input.EmploymentType?.Trim(); u.ManagerUserId=input.ManagerUserId; u.EmergencyContactName=input.EmergencyContactName?.Trim();
    u.EmergencyContactPhone=input.EmergencyContactPhone?.Trim(); u.Notes=input.Notes?.Trim(); u.UpdatedAt=DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(new{message="User profile updated."});
});

app.MapGet("/api/users/{id:long}/profile-image/view", async (long id,HttpContext http,AppDbContext db,IWebHostEnvironment env) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);var current=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid,http.RequestAborted);
    if(current is null)return Results.Unauthorized();
    if(!await RbacService.CanAccessEmployeeAsync(db,current,id))return Results.Forbid();
    var u=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id);
    if(u is null) return Results.NotFound();

    static IResult FallbackAvatar(string? name)
    {
        var initial = string.IsNullOrWhiteSpace(name) ? "?" : name.Trim().Substring(0,1).ToUpperInvariant();
        var safeInitial = System.Net.WebUtility.HtmlEncode(initial);
        var svg = $"<svg xmlns='http://www.w3.org/2000/svg' width='96' height='96' viewBox='0 0 96 96'>" +
                  $"<rect width='96' height='96' rx='48' fill='#EEF3F8'/>" +
                  $"<circle cx='48' cy='48' r='47' fill='none' stroke='#D9E3EC' stroke-width='2'/>" +
                  $"<text x='48' y='57' text-anchor='middle' font-family='Arial,Helvetica,sans-serif' font-size='34' font-weight='700' fill='#49627A'>{safeInitial}</text>" +
                  "</svg>";
        return Results.Text(svg, "image/svg+xml; charset=utf-8");
    }

    if(string.IsNullOrWhiteSpace(u.ProfileImagePath))
        return FallbackAvatar(u.Name);

    var root=Environment.GetEnvironmentVariable("MPMS_STORAGE_ROOT");
    if(string.IsNullOrWhiteSpace(root)) root=Path.Combine(env.ContentRootPath,"storage");

    var rel=u.ProfileImagePath.Replace('\\','/').TrimStart('/');
    if(rel.StartsWith("storage/",StringComparison.OrdinalIgnoreCase))
        rel=rel["storage/".Length..];

    var full=Path.GetFullPath(Path.Combine(root,rel.Replace('/',Path.DirectorySeparatorChar)));
    var safeRoot=Path.GetFullPath(root)+Path.DirectorySeparatorChar;

    if(!full.StartsWith(safeRoot,StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
        return FallbackAvatar(u.Name);

    var ext=Path.GetExtension(full).ToLowerInvariant();
    var mime=ext switch {
        ".png"=>"image/png",
        ".webp"=>"image/webp",
        ".jpeg"=>"image/jpeg",
        ".jpg"=>"image/jpeg",
        ".gif"=>"image/gif",
        _=>"application/octet-stream"
    };
    return Results.File(full,mime,enableRangeProcessing:true);
});

app.MapPost("/api/users/{id:long}/profile-image", async (long id, HttpRequest request, HttpContext http, AppDbContext db, IWebHostEnvironment env) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]); var current=await db.Users.FirstOrDefaultAsync(x=>x.Id==uid);
    if(current is null) return Results.Unauthorized();
    if(uid!=id && !await RbacService.CanAsync(db,current,"USERS","EDIT")) return Results.StatusCode(StatusCodes.Status403Forbidden);
    var u=await db.Users.FirstOrDefaultAsync(x=>x.Id==id); if(u is null) return Results.NotFound();
    var form=await request.ReadFormAsync(); var file=form.Files.FirstOrDefault();
    if(file is null||file.Length==0) return Results.BadRequest(new{message="Image required."});
    var ext=Path.GetExtension(file.FileName).ToLowerInvariant();
    if(!new[]{".jpg",".jpeg",".png",".webp"}.Contains(ext)) return Results.BadRequest(new{message="Only JPG/JPEG/PNG/WEBP allowed."});
    var root=Environment.GetEnvironmentVariable("MPMS_STORAGE_ROOT"); if(string.IsNullOrWhiteSpace(root)) root=Path.Combine(env.ContentRootPath,"storage");
    var dir=Path.Combine(root,"users",id.ToString(),"profile"); Directory.CreateDirectory(dir);
    var name=$"profile-{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}";
    await using(var fs=File.Create(Path.Combine(dir,name))) await file.CopyToAsync(fs);
    u.ProfileImagePath=$"storage/users/{id}/profile/{name}"; u.UpdatedAt=DateTime.UtcNow; await db.SaveChangesAsync();
    return Results.Ok(new{path=u.ProfileImagePath});
});

app.MapGet("/api/users/{id:long}/signature/view", async (long id, HttpContext http, AppDbContext db, IWebHostEnvironment env) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);
    var current=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid);
    if(current is null) return Results.Unauthorized();
    if(!await RbacService.CanAccessEmployeeAsync(db,current,id))return Results.Forbid();

    var u=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id);
    if(u is null) return Results.NotFound(new{message="User not found."});
    if(string.IsNullOrWhiteSpace(u.SignaturePath)) return Results.NotFound(new{message="File not found."});

    var raw=u.SignaturePath!.Trim().Replace('\\','/');
    string? full=null;

    if(Path.IsPathRooted(raw) && File.Exists(raw))
        full=raw;

    var storageRoot=Environment.GetEnvironmentVariable("MPMS_STORAGE_ROOT");
    if(string.IsNullOrWhiteSpace(storageRoot))
        storageRoot=Path.Combine(env.ContentRootPath,"storage");

    var relative=raw.StartsWith("storage/",StringComparison.OrdinalIgnoreCase)
        ? raw["storage/".Length..]
        : raw.TrimStart('/');

    var candidate=Path.GetFullPath(Path.Combine(storageRoot!,relative.Replace('/',Path.DirectorySeparatorChar)));
    if(File.Exists(candidate))
        full=candidate;

    if(full is null)
    {
        var fallback=Path.GetFullPath(Path.Combine(env.ContentRootPath,raw.TrimStart('/').Replace('/',Path.DirectorySeparatorChar)));
        if(File.Exists(fallback)) full=fallback;
    }

    if(full is null) return Results.NotFound(new{message="File not found.",path=raw});

    var mime=Path.GetExtension(full).ToLowerInvariant() switch
    {
        ".png"=>"image/png",
        ".webp"=>"image/webp",
        ".gif"=>"image/gif",
        _=>"image/jpeg"
    };
    return Results.File(full,mime,enableRangeProcessing:true);
});

app.MapPost("/api/users/{id:long}/signature", async (long id, HttpRequest request, HttpContext http, AppDbContext db, IWebHostEnvironment env) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]); var current=await db.Users.FirstOrDefaultAsync(x=>x.Id==uid);
    if(current is null) return Results.Unauthorized();
    if(uid!=id && !await RbacService.CanAsync(db,current,"USERS","EDIT")) return Results.StatusCode(StatusCodes.Status403Forbidden);
    var u=await db.Users.FirstOrDefaultAsync(x=>x.Id==id); if(u is null) return Results.NotFound();
    var form=await request.ReadFormAsync(); var file=form.Files.FirstOrDefault();
    if(file is null||file.Length==0) return Results.BadRequest(new{message="Signature required."});
    var ext=Path.GetExtension(file.FileName).ToLowerInvariant();
    if(!new[]{".jpg",".jpeg",".png",".webp"}.Contains(ext)) return Results.BadRequest(new{message="Only JPG/JPEG/PNG/WEBP allowed."});
    var root=Environment.GetEnvironmentVariable("MPMS_STORAGE_ROOT"); if(string.IsNullOrWhiteSpace(root)) root=Path.Combine(env.ContentRootPath,"storage");
    var dir=Path.Combine(root,"users",id.ToString(),"signature"); Directory.CreateDirectory(dir);
    var name=$"signature-{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}";
    await using(var fs=File.Create(Path.Combine(dir,name))) await file.CopyToAsync(fs);
    u.SignaturePath=$"storage/users/{id}/signature/{name}"; u.UpdatedAt=DateTime.UtcNow; await db.SaveChangesAsync();
    return Results.Ok(new{path=u.SignaturePath});
});

app.MapGet("/api/users", async (HttpContext http,AppDbContext db) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);var actor=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid);if(actor is null)return Results.Unauthorized();
    IQueryable<AppUser> usersQuery=db.Users.AsNoTracking();usersQuery=await RbacService.EmployeeScope(db,actor,usersQuery);
    return Results.Ok(await usersQuery.Include(x=>x.OrgUnit)
    .OrderBy(x=>x.OrgUnit==null||x.OrgUnit.Code==null||x.OrgUnit.Code=="").ThenBy(x=>x.OrgUnit==null?"":x.OrgUnit.Code)
    .ThenBy(x=>x.Department==null||x.Department=="").ThenBy(x=>x.Department)
    .ThenBy(x=>x.JobTitle==null||x.JobTitle=="").ThenBy(x=>x.JobTitle)
    .ThenBy(x=>x.JoinDate==null).ThenBy(x=>x.JoinDate).ThenBy(x=>x.Name)
    .Select(x=>new {x.Id,x.Name,x.Email,x.EmployeeCode,x.Role,x.Department,x.JobTitle,x.JoinDate,x.OrgUnitId,OrgUnit=x.OrgUnit==null?null:x.OrgUnit.Code,BusinessUnit=x.OrgUnit==null?null:x.OrgUnit.Name}).ToListAsync());
});


// ---------- R7.1 Team Directory & Project Membership ----------
app.MapGet("/api/team/users", async (HttpContext http, AppDbContext db) =>
{
    var userId = Convert.ToInt64(http.Items["AuthUserId"]);
    var actor=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==userId);if(actor is null)return Results.Unauthorized();
    var canManage = AuthService.IsManagerRole(Convert.ToString(http.Items["AuthUserRole"]))||await RbacService.CanAsync(db,actor,"USERS","VIEW")||await RbacService.CanAsync(db,actor,"USERS","FULL");
    IQueryable<AppUser> q = db.Users.AsNoTracking();
    q=canManage?await RbacService.EmployeeScope(db,actor,q):q.Where(x=>x.Id==userId);
    return Results.Ok(await q.Include(x=>x.OrgUnit)
        .OrderBy(x=>x.OrgUnit==null||x.OrgUnit.Code==null||x.OrgUnit.Code=="").ThenBy(x=>x.OrgUnit==null?"":x.OrgUnit.Code)
        .ThenBy(x=>x.Department==null||x.Department=="").ThenBy(x=>x.Department)
        .ThenBy(x=>x.JobTitle==null||x.JobTitle=="").ThenBy(x=>x.JobTitle)
        .ThenBy(x=>x.JoinDate==null).ThenBy(x=>x.JoinDate).ThenBy(x=>x.Name)
        .Select(x => new {x.Id,x.OrgUnitId,x.Name,x.Email,x.EmployeeCode,x.JobTitle,x.Department,x.JoinDate,x.Role,x.Status,OrgUnit=x.OrgUnit==null?null:x.OrgUnit.Code,BusinessUnit=x.OrgUnit==null?null:x.OrgUnit.Name}).ToListAsync());
});

app.MapPost("/api/team/users", async (AppUser input, HttpContext http, AppDbContext db) =>
{
    var callerRole = Convert.ToString(http.Items["AuthUserRole"]);
    if (!AuthService.IsManagerRole(callerRole)) return Results.StatusCode(StatusCodes.Status403Forbidden);
    if (string.IsNullOrWhiteSpace(input.Name)) return Results.BadRequest(new { message = "Name is required." });
    if (string.IsNullOrWhiteSpace(input.Email)) input.Email = $"user{DateTime.UtcNow.Ticks}@local.maipt";
    if (await db.Users.AnyAsync(x => x.Email == input.Email)) return Results.BadRequest(new { message = "Email already exists." });

    var assignedRole = (input.Role ?? "MEMBER").Trim().ToUpperInvariant();
    if (!string.Equals(callerRole, "ROOT", StringComparison.OrdinalIgnoreCase)
        && AuthService.RoleRank(assignedRole) >= AuthService.RoleRank(callerRole))
        return Results.BadRequest(new { message = "You cannot assign a role at or above your own." });

    // Bind only fields the create form owns; never trust client-supplied audit/identity fields.
    var created = new AppUser
    {
        Name = input.Name.Trim(),
        Email = input.Email.Trim(),
        JobTitle = input.JobTitle,
        Department = input.Department,
        Role = assignedRole,
        Status = string.IsNullOrWhiteSpace(input.Status) ? "ACTIVE" : input.Status,
        OrgUnitId = input.OrgUnitId == 0 ? 1 : input.OrgUnitId
    };
    db.Users.Add(created); await db.SaveChangesAsync();
    return Results.Created($"/api/team/users/{created.Id}", new { created.Id });
});

app.MapPut("/api/team/users/{id:long}", async (long id, AppUser input, HttpContext http, AppDbContext db) =>
{
    var callerRole = Convert.ToString(http.Items["AuthUserRole"]);
    if (!AuthService.IsManagerRole(callerRole)) return Results.StatusCode(StatusCodes.Status403Forbidden);
    var x = await db.Users.FindAsync(id); if (x is null) return Results.NotFound();

    var callerId = Convert.ToInt64(http.Items["AuthUserId"]);
    var assignedRole = (input.Role ?? x.Role ?? "MEMBER").Trim().ToUpperInvariant();
    if (!string.Equals(callerRole, "ROOT", StringComparison.OrdinalIgnoreCase))
    {
        if (callerId != id && AuthService.RoleRank(x.Role) >= AuthService.RoleRank(callerRole))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (AuthService.RoleRank(assignedRole) >= AuthService.RoleRank(callerRole))
            return Results.BadRequest(new { message = "You cannot assign a role at or above your own." });
    }

    x.Name = input.Name; x.Email = input.Email; x.JobTitle = input.JobTitle; x.Department = input.Department;
    x.Role = assignedRole; x.Status = input.Status; x.OrgUnitId = input.OrgUnitId == 0 ? x.OrgUnitId : input.OrgUnitId;
    await db.SaveChangesAsync(); return Results.Ok(new { x.Id });
});

app.MapDelete("/api/team/users/{id:long}", async (long id, HttpContext http, AppDbContext db) =>
{
    if (!AuthService.IsManagerRole(Convert.ToString(http.Items["AuthUserRole"]))) return Results.StatusCode(StatusCodes.Status403Forbidden);
    var x = await db.Users.FindAsync(id); if (x is null) return Results.NotFound();
    var referenced = await db.ProjectMembers.AnyAsync(m => m.UserId == id) ||
                     await db.Tasks.AnyAsync(t => t.AssigneeId == id) ||
                     await db.PerformancePeriods.AnyAsync(p => p.UserId == id);
    if (referenced)
    {
        x.Status = "INACTIVE"; await db.SaveChangesAsync();
        return Results.Ok(new { x.Id, deleted = false, status = "INACTIVE", message = "User is referenced and was deactivated instead of deleted." });
    }
    db.Users.Remove(x); await db.SaveChangesAsync(); return Results.NoContent();
});

app.MapGet("/api/project-members", async (long? projectId, HttpContext http, AppDbContext db) =>
{
    var authUserId = Convert.ToInt64(http.Items["AuthUserId"]);
    var canManage = AuthService.IsManagerRole(Convert.ToString(http.Items["AuthUserRole"]));
    var q = db.ProjectMembers.AsNoTracking().Include(x => x.Project).Include(x => x.User).AsQueryable();
    if (!canManage) q = q.Where(x => x.UserId == authUserId);
    if (projectId.HasValue) q = q.Where(x => x.ProjectId == projectId.Value);
    return Results.Ok(await q.OrderBy(x => x.Project!.Name).ThenBy(x => x.User!.Name).Select(x => new {
        x.Id, x.ProjectId, Project = x.Project!.Name, x.UserId, User = x.User!.Name,
        x.ProjectRole, x.AllocationPct, x.StartDate, x.EndDate, x.IsActive
    }).ToListAsync());
});

app.MapGet("/api/projects/{id:long}/org-units", async (long id, AppDbContext db) =>
{
    if(!await db.Projects.AnyAsync(x=>x.Id==id)) return Results.NotFound();
    var rows=await db.ProjectOrgUnits.AsNoTracking().Where(x=>x.ProjectId==id)
        .Join(db.OrgUnits.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED"),x=>x.OrgUnitId,o=>o.Id,(x,o)=>new{o.Id,o.Code,o.Name,x.IsPrimary})
        .OrderByDescending(x=>x.IsPrimary).ThenBy(x=>x.Code).ToListAsync();
    return Results.Ok(rows);
});
app.MapPut("/api/projects/{id:long}/org-units", async (long id, ProjectOrgUnitsRequest input, HttpContext http, AppDbContext db) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);var actor=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid);
    if(actor is null)return Results.Unauthorized();if(!await RbacService.CanAsync(db,actor,"PROJECTS","EDIT"))return Results.StatusCode(403);
    var project=await db.Projects.FindAsync(id);if(project is null)return Results.NotFound();
    var hasAccess=project.CreatedByUserId==uid||await db.ProjectMembers.AsNoTracking().AnyAsync(x=>x.ProjectId==id&&x.UserId==uid&&x.IsActive)||await RbacService.IsAllScopeAsync(db,actor,"PROJECTS");
    if(!hasAccess){IQueryable<Project> sq=db.Projects.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED").Where(x=>x.Id==id);sq=await RbacService.ApplyProjectScopeAsync(db,actor,sq);hasAccess=await sq.AnyAsync();}
    if(!hasAccess)return Results.StatusCode(403);
    var ids=(input.OrgUnitIds??[]).Where(x=>x>0).Distinct().ToList();if(ids.Count==0)return Results.BadRequest(new{message="Select at least one Business Unit."});
    if(!await RbacService.IsAllScopeAsync(db,actor,"PROJECTS")){var allowed=await RbacService.AllowedOrgIdsAsync(db,actor,"PROJECTS");if(allowed.Length==0&&actor.OrgUnitId>0)allowed=new[]{actor.OrgUnitId};if(ids.Any(x=>!allowed.Contains(x)))return Results.StatusCode(403);}
    var valid=await db.OrgUnits.Where(x=>ids.Contains(x.Id)).Select(x=>x.Id).ToListAsync();if(valid.Count!=ids.Count)return Results.BadRequest(new{message="One or more Business Units do not exist."});
    var primary=input.PrimaryOrgUnitId>0&&ids.Contains(input.PrimaryOrgUnitId)?input.PrimaryOrgUnitId:ids[0];
    var old=await db.ProjectOrgUnits.Where(x=>x.ProjectId==id).ToListAsync();db.ProjectOrgUnits.RemoveRange(old);
    foreach(var orgId in ids)db.ProjectOrgUnits.Add(new ProjectOrgUnit{ProjectId=id,OrgUnitId=orgId,IsPrimary=orgId==primary});
    project.OrgUnitId=primary;project.UpdatedAt=DateTime.UtcNow;await db.SaveChangesAsync();
    return Results.Ok(new{projectId=id,orgUnitIds=ids,primaryOrgUnitId=primary});
});

app.MapPost("/api/project-members/batch", async (ProjectMemberBatchRequest input, HttpContext http, AppDbContext db) =>
{
    // PROJECT_MEMBER_BATCH_PERMISSION_V3
    // Authorization is handled by global RBAC: PROJECT_TEAM + CREATE.

    // PROJECT_MEMBER_BATCH_RBAC_V2
    // Functional authorization is handled by global RBAC as PROJECT_TEAM.

    if(!await db.Users.AnyAsync(x=>x.Id==input.UserId))return Results.BadRequest(new{message="User not found."});
    var projectIds=(input.ProjectIds??[]).Where(x=>x>0).Distinct().ToList();
    if(projectIds.Count==0)return Results.BadRequest(new{message="Select at least one Project."});
    var valid=await db.Projects.Where(x=>projectIds.Contains(x.Id)).Select(x=>x.Id).ToListAsync();
    if(valid.Count!=projectIds.Count)return Results.BadRequest(new{message="One or more Projects do not exist."});
    int created=0,updated=0;
    foreach(var projectId in projectIds){
      var ex=await db.ProjectMembers.FirstOrDefaultAsync(x=>x.ProjectId==projectId&&x.UserId==input.UserId);
      if(ex is null){db.ProjectMembers.Add(new ProjectMember{ProjectId=projectId,UserId=input.UserId,ProjectRole=input.ProjectRole,AllocationPct=input.AllocationPct,StartDate=input.StartDate,EndDate=input.EndDate,IsActive=input.IsActive});created++;}
      else{ex.ProjectRole=input.ProjectRole;ex.AllocationPct=input.AllocationPct;ex.StartDate=input.StartDate;ex.EndDate=input.EndDate;ex.IsActive=input.IsActive;updated++;}
    }
    await db.SaveChangesAsync(); return Results.Ok(new{selected=projectIds.Count,created,updated});
});

app.MapPost("/api/project-members", async (ProjectMember input, HttpContext http, AppDbContext db) =>
{
    if (!await db.Projects.AnyAsync(x => x.Id == input.ProjectId)) return Results.BadRequest(new { message = "Project not found." });
    if (!await db.Users.AnyAsync(x => x.Id == input.UserId)) return Results.BadRequest(new { message = "User not found." });
    var existing = await db.ProjectMembers.FirstOrDefaultAsync(x => x.ProjectId == input.ProjectId && x.UserId == input.UserId);
    if (existing != null)
    {
        existing.ProjectRole = input.ProjectRole; existing.AllocationPct = input.AllocationPct; existing.StartDate = input.StartDate; existing.EndDate = input.EndDate; existing.IsActive = input.IsActive;
        await db.SaveChangesAsync(); return Results.Ok(new { existing.Id });
    }
    input.Id = 0; input.Project = null; input.User = null; db.ProjectMembers.Add(input); await db.SaveChangesAsync();
    return Results.Created($"/api/project-members/{input.Id}", new { input.Id });
});

app.MapPut("/api/project-members/{id:long}", async (long id, ProjectMember input, HttpContext http, AppDbContext db) =>
{
    var x = await db.ProjectMembers.FindAsync(id); if (x is null) return Results.NotFound();
    x.ProjectRole = input.ProjectRole; x.AllocationPct = input.AllocationPct; x.StartDate = input.StartDate; x.EndDate = input.EndDate; x.IsActive = input.IsActive;
    await db.SaveChangesAsync(); return Results.Ok(new { x.Id });
});

app.MapDelete("/api/project-members/{id:long}", async (long id, HttpContext http, AppDbContext db) =>
{
    var x = await db.ProjectMembers.FindAsync(id); if (x is null) return Results.NotFound();
    db.ProjectMembers.Remove(x); await db.SaveChangesAsync(); return Results.NoContent();
});

app.MapGet("/api/tasks", async (HttpContext http, AppDbContext db) =>
{
    var userId=Convert.ToInt64(http.Items["AuthUserId"]); var actor=await db.Users.FirstAsync(x=>x.Id==userId);
    var q=db.Tasks.AsNoTracking().Include(x=>x.Project).Include(x=>x.Assignee).AsQueryable();
    if(!await RbacService.IsAllScopeAsync(db,actor,"TASKS")){var pids=await RbacService.AllowedProjectIdsAsync(db,actor,"TASKS");q=q.Where(x=>x.CreatedByUserId==userId||pids.Contains(x.ProjectId));}
    return Results.Ok(await q.Select(x => new { x.Id,x.ProjectId,x.ParentTaskId,x.MilestoneId,x.AssigneeId,x.Code,x.Name,x.Description,x.Status,x.Priority,x.StartDate,x.DueDate,x.CompletedDate,x.ProgressPct,x.EstimatedHours,x.ActualHours,x.SortOrder,Project=x.Project!.Name,Assignee=x.Assignee!=null?x.Assignee.Name:null }).ToListAsync());
});
app.MapGet("/api/milestones", async (HttpContext http, AppDbContext db) =>
{
    var userId=Convert.ToInt64(http.Items["AuthUserId"]); var actor=await db.Users.FirstAsync(x=>x.Id==userId);
    var q=db.Milestones.AsNoTracking().Include(x=>x.Project).AsQueryable();
    if(!await RbacService.IsAllScopeAsync(db,actor,"TASKS")){var pids=await RbacService.AllowedProjectIdsAsync(db,actor,"TASKS");q=q.Where(x=>x.CreatedByUserId==userId||pids.Contains(x.ProjectId));}
    return Results.Ok(await q.Select(x => new { x.Id,x.ProjectId,x.Name,x.Description,x.Status,x.DueDate,x.ActualDate,x.WeightPct,Project=x.Project!.Name }).ToListAsync());
});
app.MapGet("/api/risks", async (HttpContext http,AppDbContext db) => {var uid=Convert.ToInt64(http.Items["AuthUserId"]);var actor=await db.Users.FirstAsync(x=>x.Id==uid);var q=db.Risks.AsNoTracking().Include(x=>x.Project).AsQueryable();if(!await RbacService.IsAllScopeAsync(db,actor,"RISKS")){var pids=await RbacService.AllowedProjectIdsAsync(db,actor,"RISKS");q=q.Where(x=>x.CreatedByUserId==uid||pids.Contains(x.ProjectId));}return Results.Ok(await q.Select(x => new { x.Id,x.ProjectId,x.Code,x.Title,x.Description,x.Category,x.Probability,x.Impact,x.SeverityScore,x.OwnerId,x.ResponseStrategy,x.MitigationPlan,x.ContingencyPlan,x.TargetDate,x.Status,Project=x.Project!.Name }).ToListAsync());});
app.MapGet("/api/issues", async (HttpContext http,AppDbContext db) => {var uid=Convert.ToInt64(http.Items["AuthUserId"]);var actor=await db.Users.FirstAsync(x=>x.Id==uid);var q=db.Issues.AsNoTracking().Include(x=>x.Project).AsQueryable();if(!await RbacService.IsAllScopeAsync(db,actor,"RISKS")){var pids=await RbacService.AllowedProjectIdsAsync(db,actor,"RISKS");q=q.Where(x=>x.CreatedByUserId==uid||pids.Contains(x.ProjectId));}return Results.Ok(await q.Select(x => new { x.Id,x.ProjectId,x.RiskId,x.Code,x.Title,x.Description,x.Category,x.Severity,x.Status,x.ReportedBy,x.AssignedTo,x.ReportedDate,x.TargetResolutionDate,x.ResolvedDate,x.Resolution,Project=x.Project!.Name }).ToListAsync());});

app.MapGet("/api/tasks/{id:long}", async (long id, AppDbContext db) =>
{
    var x = await db.Tasks.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Id,x.ProjectId,x.ParentTaskId,x.MilestoneId,x.AssigneeId,x.Code,x.Name,x.Description,x.Status,x.Priority,x.StartDate,x.DueDate,x.CompletedDate,x.ProgressPct,x.EstimatedHours,x.ActualHours,x.SortOrder }).FirstOrDefaultAsync();
    return x is null ? Results.NotFound() : Results.Ok(x);
});

app.MapGet("/api/milestones/{id:long}", async (long id, AppDbContext db) =>
{
    var x = await db.Milestones.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Id,x.ProjectId,x.Name,x.Description,x.DueDate,x.ActualDate,x.Status,x.WeightPct }).FirstOrDefaultAsync();
    return x is null ? Results.NotFound() : Results.Ok(x);
});

app.MapGet("/api/risks/{id:long}", async (long id, AppDbContext db) =>
{
    var x = await db.Risks.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Id,x.ProjectId,x.Code,x.Title,x.Description,x.Category,x.Probability,x.Impact,x.SeverityScore,x.OwnerId,x.ResponseStrategy,x.MitigationPlan,x.ContingencyPlan,x.TargetDate,x.Status }).FirstOrDefaultAsync();
    return x is null ? Results.NotFound() : Results.Ok(x);
});

app.MapGet("/api/issues/{id:long}", async (long id, AppDbContext db) =>
{
    var x = await db.Issues.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Id,x.ProjectId,x.RiskId,x.Code,x.Title,x.Description,x.Category,x.Severity,x.Status,x.ReportedBy,x.AssignedTo,x.ReportedDate,x.TargetResolutionDate,x.ResolvedDate,x.Resolution }).FirstOrDefaultAsync();
    return x is null ? Results.NotFound() : Results.Ok(x);
});

app.MapGet("/api/budgets/{id:long}", async (long id, AppDbContext db) =>
{
    var x = await db.BudgetLines.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Id,x.ProjectId,x.Code,x.Category,x.Description,x.BaselineAmount,x.RevisedAmount,x.CommittedAmount,x.ActualAmount,x.ForecastAmount,x.Currency }).FirstOrDefaultAsync();
    return x is null ? Results.NotFound() : Results.Ok(x);
});

app.MapGet("/api/suppliers/{id:long}", async (long id, AppDbContext db) =>
{
    var x = await db.Suppliers.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED").Where(x => x.Id == id).Select(x => new { x.Id,x.OrgUnitId,x.Code,x.Name,x.TaxCode,x.Category,x.ContactName,x.Email,x.Phone,x.Address,x.Status,x.Rating,x.CompanyName,x.ShortName,x.Website,x.BusinessRegistrationNo,x.LegalRepresentative,x.RegistrationDate,x.Country,x.ContactPosition,x.AlternativePhone,x.ProvinceCity,x.PaymentTerms,x.Currency,x.BankName,x.BankAccountNo,x.BankAccountName,x.BankBranch,x.InternalOwnerId,x.Notes }).FirstOrDefaultAsync();
    return x is null ? Results.NotFound() : Results.Ok(x);
});

app.MapPost("/api/tasks", async (ProjectTask input, AppDbContext db) => { input.Id=0; input.Code=await NextCodeAsync(db,"Tasks","TSK"); input.Project=null; input.Assignee=null; input.Milestone=null; input.ParentTask=null; db.Tasks.Add(input); await db.SaveChangesAsync(); return Results.Created($"/api/tasks/{input.Id}", new { input.Id,input.Code }); });
app.MapPost("/api/milestones", async (Milestone input, AppDbContext db) => { input.Id=0; input.Project=null; db.Milestones.Add(input); await db.SaveChangesAsync();
    await AddProjectScopedNotifications(
        db,
        input.ProjectId,
        "MILESTONE",
        "INFO",
        $"Milestone: {input.Name}",
        $"A project milestone was created/updated for the project.",
        "MILESTONE",
        input.Id);
    await db.SaveChangesAsync(); return Results.Created($"/api/milestones/{input.Id}", new { input.Id }); });
app.MapPost("/api/risks", async (Risk input, AppDbContext db) => { input.Id=0; input.Code=await NextCodeAsync(db,"Risks","RSK"); input.Project=null; input.Owner=null; input.SeverityScore=input.Probability*input.Impact; db.Risks.Add(input); await db.SaveChangesAsync();
    await AddProjectScopedNotifications(
        db,
        input.ProjectId,
        "RISK",
        "WARNING",
        $"Risk: {input.Code} - {input.Title}",
        $"Project risk {input.Code} was created/registered.",
        "RISK",
        input.Id);
    await db.SaveChangesAsync(); return Results.Created($"/api/risks/{input.Id}", new { input.Id,input.Code }); });
app.MapPost("/api/issues", async (Issue input, AppDbContext db) => { input.Id=0; input.Code=await NextCodeAsync(db,"Issues","ISS"); input.Project=null; input.Risk=null; db.Issues.Add(input); await db.SaveChangesAsync(); return Results.Created($"/api/issues/{input.Id}", new { input.Id,input.Code }); });

app.MapGet("/api/budgets", async (AppDbContext db) => Results.Ok(await db.BudgetLines.AsNoTracking().Include(x=>x.Project).Select(x => new { x.Id, x.Code, x.Category, x.Description, x.BaselineAmount, x.RevisedAmount, x.CommittedAmount, x.ActualAmount, x.ForecastAmount, x.Currency, Project=x.Project!.Name }).ToListAsync()));

app.MapPost("/api/budgets", async (BudgetLine input, AppDbContext db) =>
{
    input.Id=0;
    input.Code=await NextCodeAsync(db,"BudgetLines","BUD");
    if (input.ProjectId <= 0)
        return Results.BadRequest(new { message = "Project is required." });
    if (!await db.Projects.AnyAsync(x => x.Id == input.ProjectId))
        return Results.BadRequest(new { message = "Project does not exist." });
    input.Id = 0;
    input.Project = null;
    if (input.RevisedAmount == 0) input.RevisedAmount = input.BaselineAmount;
    if (input.ForecastAmount == 0) input.ForecastAmount = input.RevisedAmount;
    db.BudgetLines.Add(input);
    await db.SaveChangesAsync();
    return Results.Created($"/api/budgets/{input.Id}", new { input.Id });
});

app.MapGet("/api/budget/overview", async (AppDbContext db) =>
{
    var lines = await db.BudgetLines.AsNoTracking().Include(x => x.Project).ToListAsync();
    var txns = await db.BudgetTransactions.AsNoTracking()
        .Where(x => x.Status == "POSTED" && x.Type == "ACTUAL" && x.TxnDate.HasValue)
        .ToListAsync();

    var totalBudget = lines.Sum(x => x.RevisedAmount);
    var baseline = lines.Sum(x => x.BaselineAmount);
    var totalActual = lines.Sum(x => x.ActualAmount);
    var committed = lines.Sum(x => x.CommittedAmount);
    var forecast = lines.Sum(x => x.ForecastAmount);
    var remaining = totalBudget - totalActual;
    var utilization = totalBudget == 0 ? 0d : totalActual * 100d / totalBudget;
    var budgetChange = baseline == 0 ? 0d : (totalBudget - baseline) * 100d / baseline;

    var today = DateTime.UtcNow.Date;
    var thisMonthStart = new DateOnly(today.Year, today.Month, 1);
    var prevMonthDate = today.AddMonths(-1);
    var prevMonthStart = new DateOnly(prevMonthDate.Year, prevMonthDate.Month, 1);
    var prevMonthEnd = thisMonthStart.AddDays(-1);
    var thisMonthActual = txns.Where(x => x.TxnDate!.Value >= thisMonthStart).Sum(x => x.Amount);
    var prevMonthActual = txns.Where(x => x.TxnDate!.Value >= prevMonthStart && x.TxnDate.Value <= prevMonthEnd).Sum(x => x.Amount);
    var spendChange = prevMonthActual == 0 ? (thisMonthActual > 0 ? 100d : 0d) : (thisMonthActual - prevMonthActual) * 100d / prevMonthActual;

    var trend = new List<object>();
    long runningActual = 0;
    for (var i = 5; i >= 0; i--)
    {
        var d = today.AddMonths(-i);
        var monthStart = new DateOnly(d.Year, d.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        runningActual += txns.Where(x => x.TxnDate!.Value >= monthStart && x.TxnDate.Value <= monthEnd).Sum(x => x.Amount);
        var elapsed = 6 - i;
        var planned = (long)Math.Round(totalBudget * (elapsed / 6d));
        trend.Add(new { month = d.ToString("MMM"), budget = planned, actual = runningActual });
    }

    var projectRows = lines.Select(x =>
    {
        var rowRemaining = x.RevisedAmount - x.ActualAmount;
        var rowUtil = x.RevisedAmount == 0 ? 0d : x.ActualAmount * 100d / x.RevisedAmount;
        var status = rowUtil > 100 ? "Over Budget" : x.ForecastAmount > x.RevisedAmount ? "At Risk" : "On Track";
        return new {
            id = x.Id,
            projectId = x.ProjectId,
            project = x.Project != null ? x.Project.Name : "Unknown",
            code = x.Code,
            category = x.Category,
            description = x.Description,
            currency = x.Currency,
            baselineAmount = x.BaselineAmount,
            totalBudget = x.RevisedAmount,
            committed = x.CommittedAmount,
            spent = x.ActualAmount,
            remaining = rowRemaining,
            forecast = x.ForecastAmount,
            utilizationPct = rowUtil,
            status,
            trend = x.ForecastAmount <= x.RevisedAmount ? "UP" : "DOWN"
        };
    }).OrderByDescending(x => x.totalBudget).ToList();

    return Results.Ok(new {
        totalBudget,
        totalActual,
        remaining,
        utilizationPct = utilization,
        baselineAmount = baseline,
        committedAmount = committed,
        forecastAmount = forecast,
        budgetVariance = totalBudget - totalActual,
        forecastVariance = totalBudget - forecast,
        budgetChangePct = budgetChange,
        spendChangePct = spendChange,
        trend,
        projects = projectRows
    });
});



app.MapGet("/api/suppliers/overview", async (AppDbContext db) =>
{
    var suppliers = await db.Suppliers.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED").Include(x=>x.OrgUnit).OrderBy(x => x.Name).ToListAsync();
    var contracts = await db.Contracts.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED").ToListAsync();
    var evaluations = await db.SupplierEvaluations.AsNoTracking().Include(x => x.Scores).ToListAsync();

    double To100(double score) => score <= 5.0 ? score * 20.0 : score;

    var ranking = suppliers.Select(s =>
    {
        var supplierEvals = evaluations.Where(e => e.SupplierId == s.Id).OrderByDescending(e => e.PeriodEnd).ToList();
        var latest = supplierEvals.FirstOrDefault();
        var previous = supplierEvals.Skip(1).FirstOrDefault();
        var latest100 = latest == null ? 0 : To100(latest.OverallScore);
        var previous100 = previous == null ? latest100 : To100(previous.OverallScore);
        var supplierContracts = contracts.Where(c => c.SupplierId == s.Id).ToList();
        var currency = supplierContracts.Select(c => c.Currency).FirstOrDefault() ?? "VND";
        return new
        {
            id = s.Id,
            code = s.Code,
            name = s.Name,
            orgUnitId = s.OrgUnitId,
            orgUnit = s.OrgUnit != null ? s.OrgUnit.Name : null,
            taxCode = s.TaxCode,
            category = s.Category,
            contactName = s.ContactName,
            email = s.Email,
            phone = s.Phone,
            address = s.Address,
            status = s.Status,
            rating = s.Rating,
            contractValue = supplierContracts.Sum(c => c.Value),
            currency,
            kpiScore = Math.Round(latest100, 1),
            trend = Math.Round(latest100 - previous100, 1)
        };
    }).OrderByDescending(x => x.kpiScore).ThenByDescending(x => x.rating).ToList();

    var ranked = ranking.Select((x, i) => new
    {
        x.id, x.code, x.name, x.category, x.contactName, x.email, x.status, x.rating,
        x.contractValue, x.currency, x.kpiScore, x.trend, rank = i + 1
    }).ToList();

    var topSupplierIds = ranked.Where(x => x.kpiScore > 0).Take(3).Select(x => x.id).ToList();
    var topNames = ranked.Where(x => topSupplierIds.Contains(x.id)).Select(x => x.name).ToList();
    var criteria = evaluations
        .Where(e => topSupplierIds.Contains(e.SupplierId))
        .SelectMany(e => e.Scores)
        .Select(s => s.CriteriaNameSnapshot)
        .Where(n => !string.IsNullOrWhiteSpace(n))
        .Distinct()
        .Take(7)
        .ToList();

    var radarData = criteria.Select(c =>
    {
        var values = new Dictionary<string, object> { ["criterion"] = c };
        foreach (var supplierId in topSupplierIds)
        {
            var supplier = ranked.First(x => x.id == supplierId);
            var latestEval = evaluations.Where(e => e.SupplierId == supplierId).OrderByDescending(e => e.PeriodEnd).FirstOrDefault();
            var score = latestEval?.Scores.FirstOrDefault(s => s.CriteriaNameSnapshot == c)?.Score ?? 0;
            values[supplier.name] = Math.Round(To100(score), 1);
        }
        return values;
    }).ToList();

    return Results.Ok(new
    {
        suppliers = ranked,
        radar = new { suppliers = topNames, data = radarData }
    });
});

app.MapGet("/api/suppliers", async (HttpContext http,AppDbContext db) => {var uid=Convert.ToInt64(http.Items["AuthUserId"]);var actor=await db.Users.FirstAsync(x=>x.Id==uid);var q=db.Suppliers.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED").AsQueryable();if(!await RbacService.IsAllScopeAsync(db,actor,"SUPPLIERS")){var orgs=await RbacService.AllowedOrgIdsAsync(db,actor,"SUPPLIERS");q=q.Where(x=>x.CreatedByUserId==uid||(orgs.Contains(x.OrgUnitId)));}return Results.Ok(await q.OrderBy(x=>x.Name).ToListAsync());});
app.MapPost("/api/suppliers", async (Supplier input, AppDbContext db) => { await SupplierProfileSchema.EnsureAsync(db); input.Id=0; input.Code=await NextCodeAsync(db,"Suppliers","SUP"); db.Suppliers.Add(input); await db.SaveChangesAsync(); return Results.Created($"/api/suppliers/{input.Id}", input); });

// ---------- Supplier Profile Attachments ----------
app.MapGet("/api/suppliers/{id:long}/profile-files", async (long id,HttpContext http,AppDbContext db) =>
{
    if (!await db.Suppliers.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED").AnyAsync(x => x.Id == id)) return Results.NotFound(new { message = "Supplier not found." });
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);var actor=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid,http.RequestAborted);
    if(actor is null)return Results.Unauthorized();
    var candidates = await db.Documents.AsNoTracking()
        .Where(x => x.EntityType == "SUPPLIER" && x.EntityId == id && x.Category == "SUPPLIER_PROFILE" && x.Status == "ACTIVE")
        .OrderByDescending(x => x.UploadedAt)
        .ToListAsync(http.RequestAborted);
    var permitted=await RbacService.FilterAccessibleDocumentsAsync(db,actor,candidates);
    var rows=permitted.Select(x => new { x.Id, x.OriginalFileName, x.FileSize, x.MimeType, x.UploadedAt, DownloadUrl = $"/api/suppliers/{id}/profile-files/{x.Id}/download" }).ToList();
    return Results.Ok(rows);
});

app.MapPost("/api/suppliers/{id:long}/profile-files", async (long id, HttpRequest request, HttpContext http, AppDbContext db, IWebHostEnvironment env) =>
{
    var supplier = await db.Suppliers.FirstOrDefaultAsync(x => x.Id == id);
    if (supplier is null) return Results.NotFound(new { message = "Supplier not found." });
    var uid = Convert.ToInt64(http.Items["AuthUserId"]);
    var user = await db.Users.FirstOrDefaultAsync(x => x.Id == uid);
    if (user is null) return Results.Unauthorized();
    var accessProbe=new DocumentRecord{OrgUnitId=supplier.OrgUnitId,EntityType="SUPPLIER",EntityId=supplier.Id,Category="SUPPLIER_PROFILE",UploadedBy=0};
    if(!await RbacService.CanAccessDocumentAsync(db,user,accessProbe,"UPLOAD"))return Results.Forbid();
    if (!request.HasFormContentType) return Results.BadRequest(new { message = "multipart/form-data required." });
    var form = await request.ReadFormAsync(); var file = form.Files.FirstOrDefault();
    if (file is null || file.Length == 0) return Results.BadRequest(new { message = "File is required." });
    const long maxBytes = 20L * 1024L * 1024L;
    if (file.Length > maxBytes) return Results.BadRequest(new { message = "Maximum file size is 20 MB." });
    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf",".doc",".docx",".xls",".xlsx",".ppt",".pptx",".jpg",".jpeg",".png",".zip" };
    if (!allowed.Contains(ext)) return Results.BadRequest(new { message = "Unsupported file type." });
    var safeOriginal = Path.GetFileName(file.FileName);
    var safeSupplier = string.Concat((supplier.Code ?? id.ToString()).Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
    if (string.IsNullOrWhiteSpace(safeSupplier)) safeSupplier = id.ToString();
    var dir = Path.Combine(env.ContentRootPath,"storage","documents","SUPPLIER_PROFILE",safeSupplier); Directory.CreateDirectory(dir);
    var storedName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{ext}"; var target = Path.Combine(dir,storedName);
    await using (var stream = File.Create(target)) await file.CopyToAsync(stream);
    var relative = Path.GetRelativePath(env.ContentRootPath,target).Replace("\\","/");
    var rec = new DocumentRecord { OrgUnitId=supplier.OrgUnitId, EntityType="SUPPLIER", EntityId=supplier.Id, Category="SUPPLIER_PROFILE", Name=Path.GetFileNameWithoutExtension(safeOriginal), OriginalFileName=safeOriginal, FilePath=relative, MimeType=string.IsNullOrWhiteSpace(file.ContentType)?"application/octet-stream":file.ContentType, FileSize=file.Length, Version=1, UploadedBy=uid, UploadedAt=DateTime.UtcNow, IsCurrent=true, Status="ACTIVE" };
    db.Documents.Add(rec); await db.SaveChangesAsync();
    return Results.Created($"/api/suppliers/{id}/profile-files/{rec.Id}", new { rec.Id, rec.OriginalFileName, rec.FileSize, rec.MimeType, rec.UploadedAt, DownloadUrl=$"/api/suppliers/{id}/profile-files/{rec.Id}/download" });
});

app.MapGet("/api/suppliers/{id:long}/profile-files/{docId:long}/download", async (long id, long docId, HttpContext http, AppDbContext db, IWebHostEnvironment env) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]); var user=await db.Users.FirstOrDefaultAsync(x=>x.Id==uid);
    if(user is null) return Results.Unauthorized();
    var rec=await db.Documents.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==docId&&x.EntityType=="SUPPLIER"&&x.EntityId==id&&x.Category=="SUPPLIER_PROFILE"&&x.Status=="ACTIVE");
    if(rec is null) return Results.NotFound();
    if(!await RbacService.CanAccessDocumentAsync(db,user,rec,"DOWNLOAD"))return Results.Forbid();
    var decodedPath=Uri.UnescapeDataString(rec.FilePath??""); var relative=decodedPath.TrimStart('/').Replace('/',Path.DirectorySeparatorChar); var full=Path.GetFullPath(Path.Combine(env.ContentRootPath,relative));
    var storageRoot=Path.GetFullPath(Path.Combine(env.ContentRootPath,"storage"))+Path.DirectorySeparatorChar;
    if(!full.StartsWith(storageRoot,StringComparison.OrdinalIgnoreCase)) return Results.BadRequest(new { message="Invalid document path." });
    if(!File.Exists(full)) return Results.NotFound(new { message="File not found." });
    return Results.File(full,string.IsNullOrWhiteSpace(rec.MimeType)?"application/octet-stream":rec.MimeType,rec.OriginalFileName??Path.GetFileName(full),enableRangeProcessing:true);
});

app.MapDelete("/api/suppliers/{id:long}/profile-files/{docId:long}", async (long id,long docId,HttpContext http,AppDbContext db,IWebHostEnvironment env) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]); var user=await db.Users.FirstOrDefaultAsync(x=>x.Id==uid);
    if(user is null) return Results.Unauthorized();
    var rec=await db.Documents.FirstOrDefaultAsync(x=>x.Id==docId&&x.EntityType=="SUPPLIER"&&x.EntityId==id&&x.Category=="SUPPLIER_PROFILE");
    if(rec is null) return Results.NotFound();
    if(!await RbacService.CanAccessDocumentAsync(db,user,rec,"DELETE"))return Results.Forbid();
    try{var decodedPath=Uri.UnescapeDataString(rec.FilePath??"");var relative=decodedPath.TrimStart('/').Replace('/',Path.DirectorySeparatorChar);var full=Path.GetFullPath(Path.Combine(env.ContentRootPath,relative));var storageRoot=Path.GetFullPath(Path.Combine(env.ContentRootPath,"storage"))+Path.DirectorySeparatorChar;if(full.StartsWith(storageRoot,StringComparison.OrdinalIgnoreCase)&&File.Exists(full))File.Delete(full);}catch{}
    db.Documents.Remove(rec); await db.SaveChangesAsync(); return Results.NoContent();
});

app.MapGet("/api/contracts", async (HttpContext http,AppDbContext db) => {var uid=Convert.ToInt64(http.Items["AuthUserId"]);var actor=await db.Users.FirstAsync(x=>x.Id==uid);var q=db.Contracts.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED").Include(x=>x.Project).Include(x=>x.Supplier).AsQueryable();if(!await RbacService.IsAllScopeAsync(db,actor,"CONTRACTS")){var pids=await RbacService.AllowedProjectIdsAsync(db,actor,"CONTRACTS");q=q.Where(x=>x.CreatedByUserId==uid||pids.Contains(x.ProjectId));}return Results.Ok(await q.OrderByDescending(x=>x.UpdatedAt).Select(x=>new{x.Id,x.OrgUnitId,x.ProjectId,x.SupplierId,x.ContractNumber,x.Title,x.Description,x.ContractType,x.Value,x.Currency,x.SignedDate,x.StartDate,x.EndDate,x.OwnerId,x.Status,Project=x.Project!=null?x.Project.Name:null,Supplier=x.Supplier!=null?x.Supplier.Name:null,Owner=db.Users.Where(u=>u.Id==x.OwnerId).Select(u=>u.Name).FirstOrDefault(),BusinessUnit=db.OrgUnits.Where(o=>o.Id==x.OrgUnitId).Select(o=>o.Name).FirstOrDefault()}).ToListAsync());});

app.MapGet("/api/contracts/options",async(AppDbContext db)=>{
    var orgUnits=await db.OrgUnits.AsNoTracking()
        .OrderBy(x=>x.Name)
        .Select(x=>new{x.Id,x.Code,x.Name,x.Status})
        .ToListAsync();

    var projects=await db.Projects.AsNoTracking()
        .Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED")
        .OrderBy(x=>x.Name)
        .Select(x=>new{x.Id,x.Code,x.Name,x.OrgUnitId})
        .ToListAsync();

    var suppliers=await db.Suppliers.AsNoTracking()
        .Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED" && x.Status!="INACTIVE")
        .OrderBy(x=>x.Name)
        .Select(x=>new{x.Id,x.Code,x.Name})
        .ToListAsync();

    var users=await db.Users.AsNoTracking()
        .Where(x=>x.Status=="ACTIVE")
        .OrderBy(x=>x.Name)
        .Select(x=>new{x.Id,x.Name,x.Email,x.Department})
        .ToListAsync();

    return Results.Ok(new{orgUnits,projects,suppliers,users});
});
app.MapPost("/api/contracts", async (ContractUpsertRequest i,AppDbContext db)=>{if(i.ProjectId<=0||i.SupplierId<=0||string.IsNullOrWhiteSpace(i.ContractNumber)||string.IsNullOrWhiteSpace(i.Title))return Results.BadRequest(new{message="Required fields missing."});if(await db.Contracts.AnyAsync(x=>x.ContractNumber==i.ContractNumber.Trim()))return Results.BadRequest(new{message="Contract number already exists."});var x=new Contract{OrgUnitId=i.OrgUnitId,ProjectId=i.ProjectId,SupplierId=i.SupplierId,ContractNumber=i.ContractNumber.Trim(),Title=i.Title.Trim(),Description=i.Description,ContractType=i.ContractType,Value=i.Value,Currency=i.Currency,SignedDate=i.SignedDate,StartDate=i.StartDate,EndDate=i.EndDate,OwnerId=i.OwnerId,Status=i.Status};db.Contracts.Add(x);await db.SaveChangesAsync();return Results.Created($"/api/contracts/{x.Id}",new{x.Id});});
app.MapPut("/api/contracts/{id:long}", async (long id,ContractUpsertRequest i,AppDbContext db)=>{var x=await db.Contracts.FindAsync(id);if(x is null)return Results.NotFound();if(await db.Contracts.AnyAsync(c=>c.Id!=id&&c.ContractNumber==i.ContractNumber.Trim()))return Results.BadRequest(new{message="Contract number already exists."});x.OrgUnitId=i.OrgUnitId;x.ProjectId=i.ProjectId;x.SupplierId=i.SupplierId;x.ContractNumber=i.ContractNumber.Trim();x.Title=i.Title.Trim();x.Description=i.Description;x.ContractType=i.ContractType;x.Value=i.Value;x.Currency=i.Currency;x.SignedDate=i.SignedDate;x.StartDate=i.StartDate;x.EndDate=i.EndDate;x.OwnerId=i.OwnerId;x.Status=i.Status;await db.SaveChangesAsync();return Results.Ok(new{x.Id});});
app.MapDelete("/api/contracts/{id:long}", async (long id,AppDbContext db)=>{var x=await db.Contracts.FindAsync(id);if(x is null)return Results.NotFound();if(await db.Deliverables.AnyAsync(d=>d.ContractId==id)||await db.SupplierEvaluations.AnyAsync(e=>e.ContractId==id))return Results.BadRequest(new{message="Contract is referenced. Close it instead of deleting."});db.Contracts.Remove(x);await db.SaveChangesAsync();return Results.NoContent();});
app.MapGet("/api/contracts/{id:long}/files",async(long id,HttpContext http,AppDbContext db)=>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);var actor=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid,http.RequestAborted);
    if(actor is null)return Results.Unauthorized();
    var candidates=await db.Documents.AsNoTracking().Where(x=>x.EntityType=="CONTRACT"&&x.EntityId==id&&x.Status=="ACTIVE").OrderByDescending(x=>x.UploadedAt).ToListAsync(http.RequestAborted);
    var docs=await RbacService.FilterAccessibleDocumentsAsync(db,actor,candidates);
    return Results.Ok(docs.Select(x=>new{x.Id,x.OriginalFileName,x.FileSize,x.UploadedAt,DownloadUrl=$"/api/contracts/{id}/files/{x.Id}/download"}));
});
app.MapPost("/api/contracts/{id:long}/files",async(long id,HttpRequest req,HttpContext h,AppDbContext db,M365StorageService m365)=>{
    var c=await db.Contracts.FindAsync(id);if(c is null)return Results.NotFound();
    var uid=Convert.ToInt64(h.Items["AuthUserId"]);var actor=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid,h.RequestAborted);
    if(actor is null)return Results.Unauthorized();
    var accessProbe=new DocumentRecord{OrgUnitId=c.OrgUnitId,EntityType="CONTRACT",EntityId=c.Id,Category="CONTRACT_DOCUMENT",UploadedBy=0};
    if(!await RbacService.CanAccessDocumentAsync(db,actor,accessProbe,"UPLOAD"))return Results.Forbid();
    if(!m365.Enabled)return Results.Json(new{message="M365/SharePoint storage is not enabled."},statusCode:503);
    var form=await req.ReadFormAsync(h.RequestAborted);var f=form.Files.FirstOrDefault();if(f is null)return Results.BadRequest();
    var ext=Path.GetExtension(f.FileName).ToLowerInvariant();
    if(!new[]{".pdf",".doc",".docx",".xls",".xlsx",".jpg",".jpeg",".png"}.Contains(ext))return Results.BadRequest(new{message="Unsupported file."});
    await using var stream=f.OpenReadStream();
    var stored=await m365.UploadAsync(stream,f.FileName,f.ContentType??"application/octet-stream",M365StorageService.BuildEntityFolder("CONTRACT",c.ContractNumber),h.RequestAborted);
    var contractOrgUnitId=c.OrgUnitId>0?c.OrgUnitId:actor.OrgUnitId;
    var d=new DocumentRecord{OrgUnitId=contractOrgUnitId,EntityType="CONTRACT",EntityId=id,Category="CONTRACT_DOCUMENT",Name=c.ContractNumber,OriginalFileName=Path.GetFileName(f.FileName),FilePath="",MimeType=f.ContentType??"application/octet-stream",FileSize=stored.Size,UploadedBy=uid,UploadedAt=DateTime.UtcNow,Status="ACTIVE",IsCurrent=true,StorageProvider="M365",StorageDriveId=stored.DriveId,StorageItemId=stored.ItemId,StorageWebUrl=stored.WebUrl,StoragePath=stored.Path};
    db.Documents.Add(d);await db.SaveChangesAsync();return Results.Created($"/api/contracts/{id}/files/{d.Id}",new{d.Id,d.StorageWebUrl,d.StoragePath});
});

app.MapGet("/api/contracts/{id:long}/files/{docId:long}/download",async(long id,long docId,HttpContext http,AppDbContext db,IWebHostEnvironment env,M365StorageService m365)=>
{
    var d=await db.Documents.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==docId&&x.EntityType=="CONTRACT"&&x.EntityId==id&&x.Status=="ACTIVE",http.RequestAborted);if(d is null)return Results.NotFound();
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);var actor=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid,http.RequestAborted);if(actor is null)return Results.Unauthorized();
    if(!await RbacService.CanAccessDocumentAsync(db,actor,d,"DOWNLOAD"))return Results.Forbid();
    if(string.Equals(d.StorageProvider,"M365",StringComparison.OrdinalIgnoreCase)){if(string.IsNullOrWhiteSpace(d.StorageItemId))return Results.NotFound();var remote=await m365.DownloadAsync(d.StorageItemId,d.OriginalFileName,d.MimeType,http.RequestAborted);return Results.File(remote.Stream,remote.MimeType,remote.FileName,enableRangeProcessing:true);}
    var full=Path.GetFullPath(Path.Combine(env.ContentRootPath,(d.FilePath??"").TrimStart('/').Replace('/',Path.DirectorySeparatorChar)));var root=Path.GetFullPath(Path.Combine(env.ContentRootPath,"storage"))+Path.DirectorySeparatorChar;if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!File.Exists(full))return Results.NotFound();return Results.File(full,d.MimeType??"application/octet-stream",d.OriginalFileName,enableRangeProcessing:true);
});
app.MapDelete("/api/contracts/{id:long}/files/{docId:long}",async(long id,long docId,HttpContext http,AppDbContext db,IWebHostEnvironment env,M365StorageService m365)=>
{
    var d=await db.Documents.FirstOrDefaultAsync(x=>x.Id==docId&&x.EntityType=="CONTRACT"&&x.EntityId==id,http.RequestAborted);if(d is null)return Results.NotFound();
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);var actor=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid,http.RequestAborted);if(actor is null)return Results.Unauthorized();
    if(!await RbacService.CanAccessDocumentAsync(db,actor,d,"DELETE"))return Results.Forbid();
    if(string.Equals(d.StorageProvider,"M365",StringComparison.OrdinalIgnoreCase)&&!string.IsNullOrWhiteSpace(d.StorageItemId)&&m365.Enabled)await m365.DeleteAsync(d.StorageItemId,http.RequestAborted);else try{var full=Path.GetFullPath(Path.Combine(env.ContentRootPath,(d.FilePath??"").TrimStart('/').Replace('/',Path.DirectorySeparatorChar)));var root=Path.GetFullPath(Path.Combine(env.ContentRootPath,"storage"))+Path.DirectorySeparatorChar;if(full.StartsWith(root,StringComparison.OrdinalIgnoreCase)&&File.Exists(full))File.Delete(full);}catch{}
    d.Status="DEACTIVATED";d.IsCurrent=false;await db.SaveChangesAsync(http.RequestAborted);return Results.NoContent();
});

app.MapGet("/api/deliverables", async (AppDbContext db) => Results.Ok(await db.Deliverables.AsNoTracking().Include(x=>x.Contract).Select(x => new { x.Id, x.Name, x.DueDate, x.Status, x.CompletionPct, Contract=x.Contract!.ContractNumber }).ToListAsync()));

app.MapGet("/api/kpi/criteria", async (AppDbContext db) => Results.Ok(await db.KpiCriteria.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.Id).ToListAsync()));
app.MapGet("/api/kpi/evaluations", async (AppDbContext db) => Results.Ok(await db.SupplierEvaluations.AsNoTracking().Include(x=>x.Supplier).Include(x=>x.Project).Include(x=>x.Scores).Select(x => new { x.Id, Supplier=x.Supplier!.Name, Project=x.Project!.Name, x.PeriodType, x.PeriodStart, x.PeriodEnd, x.OverallScore, x.Rating, x.Status, Scores=x.Scores.Select(s=>new { s.CriteriaNameSnapshot, s.WeightPctSnapshot, s.Score, s.WeightedScore, s.Comment }) }).ToListAsync()));


app.MapPost("/api/kpi/evaluations", async (SupplierEvaluationCreateRequest input, AppDbContext db) =>
{
    var criteria=await db.KpiCriteria.Where(x=>x.IsActive).ToListAsync();
    if(!criteria.Any()) return Results.BadRequest(new {message="No active KPI criteria."});
    var scoreMap=input.Scores.ToDictionary(x=>x.CriteriaId);
    var e=new SupplierEvaluation{SupplierId=input.SupplierId,ProjectId=input.ProjectId,ContractId=input.ContractId,PeriodType=input.PeriodType,PeriodStart=input.PeriodStart,PeriodEnd=input.PeriodEnd,EvaluatorId=input.EvaluatorId,Status=input.Status,SubmittedAt=input.Status=="DRAFT"?null:DateTime.UtcNow};
    foreach(var c in criteria){var raw=scoreMap.TryGetValue(c.Id,out var s)?Math.Clamp(s.Score,1,5):1;e.Scores.Add(new KpiScore{CriteriaId=c.Id,CriteriaNameSnapshot=c.Name,WeightPctSnapshot=c.WeightPct,Score=raw,WeightedScore=raw*c.WeightPct/100.0,Comment=scoreMap.TryGetValue(c.Id,out var si)?si.Comment:""});}
    e.OverallScore=e.Scores.Sum(x=>x.WeightedScore);
    e.Rating=e.OverallScore>=4.5?"EXCELLENT":e.OverallScore>=4.0?"VERY_GOOD":e.OverallScore>=3.0?"ACCEPTABLE":e.OverallScore>=2.0?"NEEDS_IMPROVEMENT":"POOR";
    db.SupplierEvaluations.Add(e); await db.SaveChangesAsync(); return Results.Created($"/api/kpi/evaluations/{e.Id}",new {e.Id,e.OverallScore,e.Rating});
});

app.MapGet("/api/approvals", async (HttpContext http,AppDbContext db) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);
    var actor=await db.Users.AsNoTracking().FirstAsync(x=>x.Id==uid);

    var q=db.ApprovalRequests.AsNoTracking()
        .Include(x=>x.Steps).ThenInclude(x=>x.Approver)
        .AsQueryable();

    var canSeeAll=await RbacService.IsAllScopeAsync(db,actor,"APPROVALS");
    if(!canSeeAll)
        q=q.Where(x=>x.RequestedBy==uid || x.Steps.Any(s=>s.ApproverId==uid));

    return Results.Ok(await q
        .OrderByDescending(x=>x.RequestedAt)
        .Select(x=>new {
            x.Id,x.ProjectId,x.EntityType,x.EntityId,x.WorkflowType,x.Status,
            x.RequestedAt,x.RequestedBy,
            CanAct=x.Status=="PENDING" && x.Steps.Any(s=>s.ApproverId==uid&&s.Status=="PENDING"),
            Steps=x.Steps.OrderBy(s=>s.StepNo).Select(s=>new {
                s.Id,s.StepNo,s.Status,s.Comment,s.ApproverId,
                Approver=s.Approver!=null?s.Approver.Name:""
            })
        })
        .ToListAsync());
});
app.MapPost("/api/approvals/{id:long}/{decision}", async (long id,string decision,HttpContext http,AppDbContext db)=>{
 var uid=Convert.ToInt64(http.Items["AuthUserId"]);
 var actor=await db.Users.AsNoTracking().FirstAsync(x=>x.Id==uid);
 var req=await db.ApprovalRequests.Include(x=>x.Steps).FirstOrDefaultAsync(x=>x.Id==id);
 if(req is null)return Results.NotFound();
 var canAdminAll=await RbacService.IsAllScopeAsync(db,actor,"APPROVALS");
 if(!canAdminAll && !req.Steps.Any(x=>x.ApproverId==uid&&x.Status=="PENDING"))
     return Results.Forbid();var d=decision.ToUpperInvariant();if(d is not ("APPROVED" or "REJECTED"))return Results.BadRequest(new{message="Decision must be approved or rejected"});var wf=(req.WorkflowType??"").ToUpperInvariant();var multi=wf.StartsWith("KPI_MULTI_");var mode=multi?wf.Replace("KPI_MULTI_",""):"LEGACY";ApprovalStep? step=null;if(multi){if(mode=="SEQUENTIAL"){var cur=req.Steps.Where(x=>x.Status=="PENDING").OrderBy(x=>x.StepNo).FirstOrDefault();if(cur is null)return Results.BadRequest(new{message="No pending step"});if(cur.ApproverId!=uid)return Results.Forbid();step=cur;}else{step=req.Steps.FirstOrDefault(x=>x.Status=="PENDING"&&x.ApproverId==uid);if(step is null)return Results.Forbid();}}else{
 step=canAdminAll
     ? req.Steps.OrderBy(x=>x.StepNo).FirstOrDefault(x=>x.Status=="PENDING")
     : req.Steps.OrderBy(x=>x.StepNo).FirstOrDefault(x=>x.Status=="PENDING"&&x.ApproverId==uid);
 if(step is null)return Results.Forbid();
}
 step.Status=d;step.ActedAt=DateTime.UtcNow;if(multi){if(mode=="ANY"){if(d=="APPROVED"){req.Status="APPROVED";req.CompletedAt=DateTime.UtcNow;foreach(var x in req.Steps.Where(x=>x.Status=="PENDING"))x.Status="SKIPPED";}else if(req.Steps.All(x=>x.Status=="REJECTED")){req.Status="REJECTED";req.CompletedAt=DateTime.UtcNow;}else req.Status="PENDING";}else{if(d=="REJECTED"){req.Status="REJECTED";req.CompletedAt=DateTime.UtcNow;}else if(req.Steps.All(x=>x.Status=="APPROVED")){req.Status="APPROVED";req.CompletedAt=DateTime.UtcNow;}else req.Status="PENDING";}if(req.EntityType=="PERFORMANCE_PERIOD"){var p=await db.PerformancePeriods.FirstOrDefaultAsync(x=>x.Id==req.EntityId);if(p!=null)p.Status=req.Status=="APPROVED"?"APPROVED":req.Status=="REJECTED"?"REJECTED":"SUBMITTED";}}else{req.Status=d;req.CompletedAt=DateTime.UtcNow;}await db.SaveChangesAsync();return Results.Ok(new{req.Id,req.Status,req.WorkflowType});});

static string DocumentFunctionModule(string? category,string? entityType=null,string? storagePath=null)
{
    var source=$"{category} {entityType} {storagePath}".ToUpperInvariant();
    if(source.Contains("CAPITAL MANAGEMENT")||source.Contains("CAPITAL MGMT")||source.Contains("CAPITAL_DOCUMENT"))return "CAPITAL";
    if(source.Contains("INVESTMENT MANAGEMENT")||source.Contains("INVEST MGMT")||source.Contains("INVESTMENT_DOCUMENT"))return "INVESTMENT";
    if(source.Contains("IT ASSETS")||source.Contains("IT POLICIES")||source.Contains("LICENSE")||source.Contains("HANDOVER"))return "IT_ASSETS";
    if(source.Contains("PERSONAL FC")||source.Contains("PERSONAL_FC")||source.Contains("CLUB"))return "PERSONAL_FC";
    if(source.Contains("SYSTEM")||source.Contains("AVATAR")||source.Contains("USER PROFILE")||source.Contains("USER_PROFILE")||source.Contains("SIGNATURE"))return "USERS";
    if(source.Contains("PROJECT"))return "PROJECTS";
    if(source.Contains("CONTRACT"))return "CONTRACTS";
    if(source.Contains("BUDGET"))return "BUDGET";
    if(source.Contains("SUPPLIER"))return "SUPPLIERS";
    if(source.Contains("KPI")||source.Contains("PERFORMANCE"))return "KPI";
    return "DOCUMENTS";
}

app.MapGet("/api/documents/overview", async (HttpContext http,AppDbContext db, IWebHostEnvironment env) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);var actor=await db.Users.FirstAsync(x=>x.Id==uid);if(!await RbacService.CanAsync(db,actor,"DOCUMENTS","VIEW"))return Results.Forbid();var allDocs=await db.Documents.AsNoTracking().Where(x=>x.Status=="ACTIVE").OrderByDescending(x=>x.UploadedAt).ToListAsync();var docs=await RbacService.FilterAccessibleDocumentsAsync(db,actor,allDocs);
    var users=await db.Users.AsNoTracking().ToDictionaryAsync(x=>x.Id,x=>x.Name);
    var projects=await db.Projects.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED").ToDictionaryAsync(x=>x.Id,x=>x.Name);
    var storageRoot=Path.Combine(env.ContentRootPath,"storage","documents"); Directory.CreateDirectory(storageRoot);
    var folderNames=Directory.GetDirectories(storageRoot).Select(Path.GetFileName).Where(x=>!string.IsNullOrWhiteSpace(x)).Cast<string>().ToList();
    folderNames.AddRange(docs.Select(x=>x.Category).Where(x=>!string.IsNullOrWhiteSpace(x)));
    var folders=folderNames.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x).Select(name=>new{name,count=docs.Count(d=>string.Equals(d.Category,name,StringComparison.OrdinalIgnoreCase))}).ToList();
    var rows=docs.Select(d=>new{d.Id,d.Name,d.OriginalFileName,d.Category,d.EntityType,d.EntityId,d.UploadedAt,d.FileSize,d.MimeType,Author=users.TryGetValue(d.UploadedBy,out var a)?a:"System",Project=d.EntityType=="PROJECT"&&projects.TryGetValue(d.EntityId,out var p)?p:null,DownloadUrl=$"/api/documents/{d.Id}/download"}).ToList();
    return Results.Ok(new{folders,documents=rows});
});
app.MapPost("/api/documents/folders",async(FolderCreateRequest input,HttpContext http,AppDbContext db,IWebHostEnvironment env)=>{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);var actor=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid);if(actor is null)return Results.Unauthorized();if(!await RbacService.CanAsync(db,actor,"DOCUMENTS","CREATE"))return Results.Forbid();var safe=string.Concat((input.Name??"").Trim().Select(ch=>Path.GetInvalidFileNameChars().Contains(ch)?'_':ch)); if(string.IsNullOrWhiteSpace(safe)) return Results.BadRequest(new{message="Folder name is required."}); Directory.CreateDirectory(Path.Combine(env.ContentRootPath,"storage","documents",safe)); return Results.Ok(new{name=safe});
});



app.MapGet("/api/documents/{id:long}/view", async (
    long id,
    AppDbContext db,
    IWebHostEnvironment env,
    M365StorageService m365,
    HttpContext http) =>
{
    var d = await db.Documents.AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == id && x.Status == "ACTIVE", http.RequestAborted);
    if (d is null) return Results.NotFound();
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);
    var actor=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid,http.RequestAborted);
    if(actor is null) return Results.Unauthorized();
    if(!await RbacService.CanAccessDocumentAsync(db,actor,d,"VIEW")) return Results.Forbid();

    if (string.Equals(d.StorageProvider, "M365", StringComparison.OrdinalIgnoreCase))
    {
        if (string.IsNullOrWhiteSpace(d.StorageItemId)) return Results.NotFound();

        var f = await m365.DownloadAsync(
            d.StorageItemId,
            d.OriginalFileName,
            d.MimeType,
            http.RequestAborted
        );

        return Results.File(
            f.Stream,
            f.MimeType,
            enableRangeProcessing: true
        );
    }

    var decodedPath = Uri.UnescapeDataString(d.FilePath ?? "");
    var relative = decodedPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
    var full = Path.GetFullPath(Path.Combine(env.ContentRootPath, relative));
    var storageRoot = Path.GetFullPath(Path.Combine(env.ContentRootPath, "storage")) + Path.DirectorySeparatorChar;

    if (!full.StartsWith(storageRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
        return Results.NotFound();

    return Results.File(
        full,
        d.MimeType ?? "application/octet-stream",
        enableRangeProcessing: true
    );
});

app.MapGet("/api/documents/{id:long}/download", async (
    long id,
    AppDbContext db,
    IWebHostEnvironment env,
    M365StorageService m365,
    HttpContext http) =>
{
    var d = await db.Documents.AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == id && x.Status == "ACTIVE", http.RequestAborted);
    if (d is null) return Results.NotFound();
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);
    var actor=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid,http.RequestAborted);
    if(actor is null) return Results.Unauthorized();
    if(!await RbacService.CanAccessDocumentAsync(db,actor,d,"DOWNLOAD")) return Results.Forbid();

    if (string.Equals(d.StorageProvider, "M365", StringComparison.OrdinalIgnoreCase))
    {
        if (string.IsNullOrWhiteSpace(d.StorageItemId)) return Results.NotFound();

        var f = await m365.DownloadAsync(
            d.StorageItemId,
            d.OriginalFileName,
            d.MimeType,
            http.RequestAborted
        );

        return Results.File(
            f.Stream,
            f.MimeType,
            f.FileName,
            enableRangeProcessing: true
        );
    }

    var decodedPath = Uri.UnescapeDataString(d.FilePath ?? "");
    var relative = decodedPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
    var full = Path.GetFullPath(Path.Combine(env.ContentRootPath, relative));
    var storageRoot = Path.GetFullPath(Path.Combine(env.ContentRootPath, "storage")) + Path.DirectorySeparatorChar;

    if (!full.StartsWith(storageRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
        return Results.NotFound();

    return Results.File(
        full,
        d.MimeType ?? "application/octet-stream",
        d.OriginalFileName,
        enableRangeProcessing: true
    );
});

app.MapPost("/api/documents/upload", async (
    HttpRequest request,
    HttpContext http,
    AppDbContext db,
    IWebHostEnvironment env,
    M365StorageService m365) =>
{
    var form = await request.ReadFormAsync(http.RequestAborted);
    var file = form.Files.FirstOrDefault();
    if (file is null) return Results.BadRequest(new { message = "No file selected." });

    var category = (form["category"].FirstOrDefault() ?? "GENERAL").Trim();
    var requestedEntityType = (form["entityType"].FirstOrDefault() ?? "PROJECT").Trim().ToUpperInvariant();
    var genericDocumentUpload=requestedEntityType=="DOCUMENT";
    var entityType = genericDocumentUpload?DocumentFunctionModule(category,requestedEntityType):requestedEntityType;

    long.TryParse(form["entityId"].FirstOrDefault(), out var entityId);
    long.TryParse(form["orgUnitId"].FirstOrDefault(), out var orgUnitId);
    var uploadedBy=Convert.ToInt64(http.Items["AuthUserId"]);
    var actor=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uploadedBy,http.RequestAborted);
    if(actor is null) return Results.Unauthorized();
    if(entityType=="CONTRACT"&&entityId>0)orgUnitId=await db.Contracts.AsNoTracking().Where(x=>x.Id==entityId).Select(x=>x.OrgUnitId).FirstOrDefaultAsync(http.RequestAborted);
    else if(entityType=="SUPPLIER"&&entityId>0)orgUnitId=await db.Suppliers.AsNoTracking().Where(x=>x.Id==entityId).Select(x=>x.OrgUnitId).FirstOrDefaultAsync(http.RequestAborted);
    else if(entityType=="PROJECT"&&entityId>0)orgUnitId=await db.Projects.AsNoTracking().Where(x=>x.Id==entityId).Select(x=>x.OrgUnitId).FirstOrDefaultAsync(http.RequestAborted);
    if(orgUnitId<=0)orgUnitId=actor.OrgUnitId;

    var permissionModule=DocumentFunctionModule(category,entityType);
    var canUpload=string.Equals(actor.Role,"ROOT",StringComparison.OrdinalIgnoreCase)||
        await RbacService.CanAsync(db,actor,permissionModule,"UPLOAD")||
        await RbacService.CanAsync(db,actor,permissionModule,"CREATE")||
        await RbacService.CanAsync(db,actor,permissionModule,"FULL");
    if(!canUpload)return Results.Forbid();

    if(entityType=="PROJECT" && entityId>0 && !await RbacService.IsAllScopeAsync(db,actor,"DOCUMENTS"))
    {
        var projectIds=await RbacService.AllowedProjectIdsAsync(db,actor,"DOCUMENTS");
        if(!projectIds.Contains(entityId)) return Results.Forbid();
    }

    var safeFile = Path.GetFileName(file.FileName);
    if (string.IsNullOrWhiteSpace(safeFile))
        return Results.BadRequest(new { message = "Invalid file name." });

    var entityCode = $"{entityType}-{entityId}";

    if (entityType == "PROJECT" && entityId > 0)
    {
        entityCode = await db.Projects.AsNoTracking()
            .Where(x => x.Id == entityId)
            .Select(x => x.Code)
            .FirstOrDefaultAsync(http.RequestAborted)
            ?? entityCode;
    }
    else if (entityType == "CONTRACT" && entityId > 0)
    {
        entityCode = await db.Contracts.AsNoTracking()
            .Where(x => x.Id == entityId)
            .Select(x => x.ContractNumber)
            .FirstOrDefaultAsync(http.RequestAborted)
            ?? entityCode;
    }
    else if (entityType == "SUPPLIER" && entityId > 0)
    {
        entityCode = await db.Suppliers.AsNoTracking()
            .Where(x => x.Id == entityId)
            .Select(x => x.Code)
            .FirstOrDefaultAsync(http.RequestAborted)
            ?? entityCode;
    }

    if (m365.Enabled)
    {
        var folder = genericDocumentUpload ? M365StorageService.BuildCategoryFolder(category) : M365StorageService.BuildEntityFolder(entityType, entityCode);

        await using var stream = file.OpenReadStream();
        var stored = await m365.UploadAsync(
            stream,
            safeFile,
            file.ContentType ?? "application/octet-stream",
            folder,
            http.RequestAborted
        );

        var rec = new DocumentRecord
        {
            OrgUnitId = orgUnitId,
            EntityType = entityType,
            EntityId = entityId,
            Category = category,
            Name = Path.GetFileNameWithoutExtension(safeFile),
            OriginalFileName = safeFile,
            FilePath = "",
            MimeType = file.ContentType ?? "application/octet-stream",
            FileSize = stored.Size,
            UploadedBy = uploadedBy,
            UploadedAt = DateTime.UtcNow,
            Status = "ACTIVE",
            IsCurrent = true,
            StorageProvider = "M365",
            StorageDriveId = stored.DriveId,
            StorageItemId = stored.ItemId,
            StorageWebUrl = stored.WebUrl,
            StoragePath = stored.Path
        };

        db.Documents.Add(rec);
        await db.SaveChangesAsync(http.RequestAborted);

        return Results.Created(
            $"/api/documents/{rec.Id}",
            new
            {
                rec.Id,
                rec.StorageProvider,
                rec.StorageWebUrl,
                rec.StoragePath
            }
        );
    }

    var safeCategory = string.Concat(
        category.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_')
    );

    var dir = Path.Combine(env.ContentRootPath, "storage", "documents", safeCategory);
    Directory.CreateDirectory(dir);

    var ext = Path.GetExtension(safeFile);
    var unique = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{ext}";
    var fullPath = Path.Combine(dir, unique);

    await using (var fs = File.Create(fullPath))
        await file.CopyToAsync(fs, http.RequestAborted);

    var local = new DocumentRecord
    {
        OrgUnitId = orgUnitId,
        EntityType = entityType,
        EntityId = entityId,
        Category = category,
        Name = Path.GetFileNameWithoutExtension(safeFile),
        OriginalFileName = safeFile,
        FilePath = $"storage/documents/{safeCategory}/{unique}",
        MimeType = file.ContentType ?? "application/octet-stream",
        FileSize = file.Length,
        UploadedBy = uploadedBy,
        UploadedAt = DateTime.UtcNow,
        Status = "ACTIVE",
        IsCurrent = true,
        StorageProvider = "LOCAL"
    };

    db.Documents.Add(local);
    await db.SaveChangesAsync(http.RequestAborted);

    return Results.Created(
        $"/api/documents/{local.Id}",
        new
        {
            local.Id,
            local.FilePath,
            local.StorageProvider
        }
    );
});

app.MapGet("/api/notifications", async (HttpContext http,AppDbContext db) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);
    return Results.Ok(await db.Notifications.AsNoTracking()
        .Where(x=>x.UserId==uid)
        .OrderByDescending(x=>x.CreatedAt)
        .Take(50)
        .Select(x=>new{
            x.Id,x.UserId,x.ProjectId,x.Type,x.Severity,x.Title,x.Message,
            x.EntityType,x.EntityId,x.IsRead,x.ReadAt,x.CreatedAt
        })
        .ToListAsync());
});

app.MapPost("/api/notifications/{id:long}/read", async (long id,HttpContext http,AppDbContext db) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);
    var n=await db.Notifications.FirstOrDefaultAsync(x=>x.Id==id&&x.UserId==uid);
    if(n is null)return Results.NotFound();
    if(!n.IsRead){n.IsRead=true;n.ReadAt=DateTime.UtcNow;await db.SaveChangesAsync();}
    return Results.Ok(new{n.Id,n.IsRead,n.ReadAt});
});

app.MapPost("/api/notifications/read-all", async (HttpContext http,AppDbContext db) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);
    var rows=await db.Notifications.Where(x=>x.UserId==uid&&!x.IsRead).ToListAsync();
    var now=DateTime.UtcNow;
    foreach(var n in rows){n.IsRead=true;n.ReadAt=now;}
    if(rows.Count>0)await db.SaveChangesAsync();
    return Results.Ok(new{updated=rows.Count});
});
app.MapGet("/api/activity", async (AppDbContext db) => Results.Ok(await db.ActivityLogs.AsNoTracking().OrderByDescending(x=>x.Timestamp).Take(100).ToListAsync()));


// ---------- R7 Annual Budget Planning ----------

static bool PlannedMonthMatchesBudgetYear(string? plannedMonth, int budgetYear)
{
    if (string.IsNullOrWhiteSpace(plannedMonth)) return true;
    var v = plannedMonth.Trim();
    if (v.Length == 7 && v[4] == '-' && int.TryParse(v[..4], out var y) && int.TryParse(v.Substring(5, 2), out var m) && m >= 1 && m <= 12)
        return y == budgetYear;
    var parts = v.Split('/', StringSplitOptions.TrimEntries);
    if (parts.Length == 2 && int.TryParse(parts[0], out var legacyMonth) && int.TryParse(parts[1], out var legacyYear) && legacyMonth >= 1 && legacyMonth <= 12)
        return legacyYear == budgetYear;
    return false;
}


app.MapGet("/api/scope/options/{module}", async (string module, HttpContext http, AppDbContext db) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);
    var actor=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid);
    if(actor is null) return Results.Unauthorized();

    module=(module??"").Trim().ToUpperInvariant();
    var sc=await RbacService.GetScopeAsync(db,actor,module);
    var mode=(sc.Mode??"OWN_ORG").Trim().ToUpperInvariant();
    var vals=sc.Values??[];

    long[] allowedOrgIds;
    if(mode=="ALL")
    {
        allowedOrgIds=await db.OrgUnits.AsNoTracking()
            .Where(x=>x.Status=="ACTIVE")
            .Select(x=>x.Id)
            .ToArrayAsync();
    }
    else if(mode=="SELECTED_ORGS")
    {
        allowedOrgIds=await db.OrgUnits.AsNoTracking()
            .Where(x=>x.Status=="ACTIVE" &&
                (vals.Contains(x.Code) || vals.Contains(x.Id.ToString())))
            .Select(x=>x.Id)
            .ToArrayAsync();
    }
    else
    {
        allowedOrgIds=actor.OrgUnitId>0 ? new[]{actor.OrgUnitId} : Array.Empty<long>();
    }

    var orgUnits=await db.OrgUnits.AsNoTracking()
        .Where(x=>x.Status=="ACTIVE" && allowedOrgIds.Contains(x.Id))
        .OrderBy(x=>x.Name)
        .Select(x=>new{x.Id,x.Code,x.Name,x.Status})
        .ToListAsync();

    var projects=await db.Projects.AsNoTracking()
        .Where(x =>
            (x.OrgUnitId>0 && allowedOrgIds.Contains(x.OrgUnitId)) ||
            db.ProjectOrgUnits.Any(m=>m.ProjectId==x.Id && allowedOrgIds.Contains(m.OrgUnitId)))
        .OrderBy(x=>x.Name)
        .Select(x=>new
        {
            x.Id,x.Code,x.Name,x.Status,x.OrgUnitId,
            OrgUnitIds=db.ProjectOrgUnits
                .Where(m=>m.ProjectId==x.Id)
                .Select(m=>m.OrgUnitId)
                .ToList(),
            OrgUnitCodes=db.ProjectOrgUnits
                .Where(m=>m.ProjectId==x.Id)
                .Join(db.OrgUnits,m=>m.OrgUnitId,o=>o.Id,(m,o)=>o.Code)
                .ToList()
        })
        .ToListAsync();

    var primaryIds=projects.Where(x=>x.OrgUnitId>0).Select(x=>x.OrgUnitId).Distinct().ToArray();
    var primaryCodes=await db.OrgUnits.AsNoTracking()
        .Where(x=>primaryIds.Contains(x.Id))
        .ToDictionaryAsync(x=>x.Id,x=>x.Code);

    var normalizedProjects=projects.Select(x=>new
    {
        x.Id,x.Code,x.Name,x.Status,x.OrgUnitId,
        OrgUnitIds=x.OrgUnitIds
            .Concat(x.OrgUnitId>0?new[]{x.OrgUnitId}:Array.Empty<long>())
            .Distinct().ToArray(),
        OrgUnitCodes=x.OrgUnitCodes
            .Concat(x.OrgUnitId>0 && primaryCodes.TryGetValue(x.OrgUnitId,out var pc)
                ?new[]{pc}:Array.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
    }).ToList();

    return Results.Ok(new
    {
        module,
        mode,
        orgUnits,
        projects=normalizedProjects
    });
});

static async Task<bool> BudgetProjectMatchesOrgAsync(
    AppDbContext db,
    long? projectId,
    string? orgUnitCode)
{
    if(!projectId.HasValue) return true;
    if(string.IsNullOrWhiteSpace(orgUnitCode)) return false;

    var unit=await db.OrgUnits.AsNoTracking()
        .FirstOrDefaultAsync(x=>x.Code==orgUnitCode);
    if(unit is null) return false;

    return await db.Projects.AsNoTracking().AnyAsync(x =>
        x.Id==projectId.Value &&
        (
            x.OrgUnitId==unit.Id ||
            db.ProjectOrgUnits.Any(m=>m.ProjectId==x.Id && m.OrgUnitId==unit.Id)
        ));
}

app.MapGet("/api/budget-plan/overview", async (int? year, string? orgUnit, HttpContext http, AppDbContext db) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]); var user=await db.Users.Include(x=>x.OrgUnit).FirstAsync(x=>x.Id==uid); var targetYear=year??2026;
    IQueryable<BudgetPlanItem> q=db.BudgetPlanItems.AsNoTracking().Include(x=>x.Project).Where(x=>x.BudgetYear==targetYear); q=await RbacService.BudgetScope(db,user,q);
    if(!string.IsNullOrWhiteSpace(orgUnit)&&orgUnit!="ALL")q=q.Where(x=>x.OrgUnit==orgUnit); var rows=await q.OrderBy(x=>x.OrgUnit).ThenByDescending(x=>x.PlannedAmount).ToListAsync();
    IQueryable<BudgetPlanItem> uq=db.BudgetPlanItems.AsNoTracking().Where(x=>x.BudgetYear==targetYear); uq=await RbacService.BudgetScope(db,user,uq); var units=await uq.Select(x=>x.OrgUnit).Distinct().OrderBy(x=>x).ToListAsync();
    var total=rows.Sum(x=>x.PlannedAmount);var linked=rows.Count(x=>x.ProjectId.HasValue);var byUnit=rows.GroupBy(x=>x.OrgUnit).Select(g=>new{orgUnit=g.Key,amount=g.Sum(x=>x.PlannedAmount),items=g.Count()}).OrderByDescending(x=>x.amount).ToList();
    var ids=rows.SelectMany(x=>new long?[]{x.CreatedByUserId,x.UpdatedByUserId}).Where(x=>x.HasValue).Select(x=>x!.Value).Distinct().ToList();var users=await db.Users.AsNoTracking().Where(x=>ids.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,x=>x.Name);
    return Results.Ok(new{year=targetYear,totalPlanned=total,itemCount=rows.Count,linkedProjects=linked,unlinkedItems=rows.Count-linked,orgUnits=units,byUnit,items=rows.Select(x=>new{x.Id,x.BudgetYear,x.OrgUnit,x.SourceSheet,x.SourceRow,x.ProjectId,Project=x.Project!=null?x.Project.Name:null,x.Name,x.Category,x.Vendor,x.PlannedAmount,x.Quantity,x.UnitPrice,x.PlannedMonth,x.MonthlyPlanJson,x.Note,x.Status,x.CreatedByUserId,x.UpdatedByUserId,x.CreatedAt,x.UpdatedAt,CreatedByName=x.CreatedByUserId.HasValue&&users.TryGetValue(x.CreatedByUserId.Value,out var cb)?cb:"System Created",
EditedByName=x.UpdatedByUserId.HasValue&&users.TryGetValue(x.UpdatedByUserId.Value,out var ub)?ub:null})});
});
app.MapGet("/api/budget-plan/years",async(HttpContext http,AppDbContext db)=>{var uid=Convert.ToInt64(http.Items["AuthUserId"]);var user=await db.Users.Include(x=>x.OrgUnit).FirstAsync(x=>x.Id==uid);IQueryable<BudgetPlanItem> q=db.BudgetPlanItems.AsNoTracking();q=await RbacService.BudgetScope(db,user,q);return Results.Ok(await q.Select(x=>x.BudgetYear).Distinct().OrderByDescending(x=>x).ToListAsync());});
app.MapPost("/api/budget-plan/years/{sourceYear:int}/clone",async(int sourceYear,int targetYear,HttpContext http,AppDbContext db)=>{var uid=Convert.ToInt64(http.Items["AuthUserId"]);var user=await db.Users.Include(x=>x.OrgUnit).FirstAsync(x=>x.Id==uid);if(!await RbacService.CanAsync(db,user,"BUDGET","FULL"))return Results.Forbid();if(sourceYear==targetYear)return Results.BadRequest(new{message="Target year must differ from source year."});if(await db.BudgetPlanItems.AnyAsync(x=>x.BudgetYear==targetYear))return Results.BadRequest(new{message=$"Budget plan {targetYear} already exists."});var source=await db.BudgetPlanItems.AsNoTracking().Where(x=>x.BudgetYear==sourceYear).ToListAsync();foreach(var x in source)db.BudgetPlanItems.Add(new BudgetPlanItem{BudgetYear=targetYear,OrgUnit=x.OrgUnit,CreatedByUserId=uid,UpdatedByUserId=uid,CreatedAt=DateTime.UtcNow,UpdatedAt=DateTime.UtcNow,SourceSheet=$"CLONED-{sourceYear}",SourceRow=x.SourceRow,ProjectId=x.ProjectId,Name=x.Name,Category=x.Category,Vendor=x.Vendor,PlannedAmount=x.PlannedAmount,Quantity=x.Quantity,UnitPrice=x.UnitPrice,PlannedMonth=x.PlannedMonth,MonthlyPlanJson=x.MonthlyPlanJson,Note=x.Note,Status="DRAFT"});await db.SaveChangesAsync();return Results.Created($"/api/budget-plan/overview?year={targetYear}",new{sourceYear,targetYear,items=source.Count});});
app.MapPost("/api/budget-plan",async(BudgetPlanItem input,HttpContext http,AppDbContext db)=>{var uid=Convert.ToInt64(http.Items["AuthUserId"]);var user=await db.Users.Include(x=>x.OrgUnit).FirstAsync(x=>x.Id==uid);input.CreatedByUserId=uid;input.UpdatedByUserId=uid;if(!PlannedMonthMatchesBudgetYear(input.PlannedMonth,input.BudgetYear))return Results.BadRequest(new{message=$"Planned Month must belong to Budget Year {input.BudgetYear} and use mm/yyyy, for example 09/{input.BudgetYear}."});if(!await RbacService.CanBudgetOrg(db,user,input.OrgUnit))return Results.Forbid();if(!await BudgetProjectMatchesOrgAsync(db,input.ProjectId,input.OrgUnit))return Results.BadRequest(new{message="Selected Project does not belong to the selected/permitted Business Unit."});input.Id=0;
input.Project=null;
input.CreatedByUserId=uid;
input.UpdatedByUserId=uid;
input.CreatedAt=DateTime.UtcNow;
input.UpdatedAt=DateTime.UtcNow;
db.BudgetPlanItems.Add(input);
await db.SaveChangesAsync();
return Results.Created($"/api/budget-plan/{input.Id}",new{input.Id});});
app.MapPut("/api/budget-plan/{id:long}", async (long id, BudgetPlanItem input, HttpContext http, AppDbContext db) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);
    var user=await db.Users.Include(x=>x.OrgUnit).FirstOrDefaultAsync(x=>x.Id==uid);
    if(user is null) return Results.Unauthorized();

    if(!await RbacService.CanAsync(db,user,"BUDGET","EDIT"))
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    if(!PlannedMonthMatchesBudgetYear(input.PlannedMonth,input.BudgetYear))
        return Results.BadRequest(new{message=$"Planned Month must belong to Budget Year {input.BudgetYear} and use mm/yyyy, for example 09/{input.BudgetYear}."});

    var row=await db.BudgetPlanItems.FindAsync(id);
    if(row is null) return Results.NotFound(new{message="Budget Plan Item not found."});
    if(string.Equals(row.Status,"SUBMITTED",StringComparison.OrdinalIgnoreCase)||string.Equals(row.Status,"APPROVED",StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new{message="Submitted or approved Plan/Budget items are read-only."});

    var canEditExisting=row.CreatedByUserId==uid || await RbacService.CanBudgetItem(db,user,id);
    if(!canEditExisting)
        return Results.Json(new{message="You do not have access to edit this Budget Plan Item."},
            statusCode:StatusCodes.Status403Forbidden);

    if(!await RbacService.CanBudgetOrg(db,user,input.OrgUnit))
        return Results.Json(new{message="You do not have permission for the selected Business Unit."},
            statusCode:StatusCodes.Status403Forbidden);

    row.BudgetYear=input.BudgetYear;
    row.ProjectId=input.ProjectId;
    row.OrgUnit=input.OrgUnit;
    row.Name=input.Name;
    row.Category=input.Category;
    row.Vendor=input.Vendor;
    row.PlannedAmount=input.PlannedAmount;
    row.Quantity=input.Quantity;
    row.UnitPrice=input.UnitPrice;
    row.PlannedMonth=input.PlannedMonth;
    row.MonthlyPlanJson=input.MonthlyPlanJson;
    row.Note=input.Note;
    row.Status=input.Status;
    row.UpdatedByUserId=uid;
    row.UpdatedAt=DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok(new{row.Id,row.UpdatedByUserId,row.UpdatedAt});
});

// BUDGET_PLAN_DELETE_V1
// root may delete any plan item. Other users require BUDGET/DELETE and access
// to the item's creator/data scope.
app.MapDelete("/api/budget-plan/{id:long}", async (long id, HttpContext http, AppDbContext db) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);
    var user=await db.Users.Include(x=>x.OrgUnit).FirstOrDefaultAsync(x=>x.Id==uid);
    if(user is null) return Results.Unauthorized();

    var row=await db.BudgetPlanItems.FindAsync(id);
    if(row is null)
        return Results.NotFound(new{message="Budget Plan Item not found."});
    if(string.Equals(row.Status,"SUBMITTED",StringComparison.OrdinalIgnoreCase)||string.Equals(row.Status,"APPROVED",StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new{message="Submitted or approved Plan/Budget items cannot be deleted."});

    var isRoot=string.Equals(user.Name?.Trim(),"root",StringComparison.OrdinalIgnoreCase) ||
               string.Equals(user.Email?.Trim(),"root",StringComparison.OrdinalIgnoreCase);

    if(!isRoot)
    {
        if(!await RbacService.CanAsync(db,user,"BUDGET","DELETE"))
            return Results.Json(new{message="You do not have permission to delete Budget Plan Items."},
                statusCode:StatusCodes.Status403Forbidden);

        var canDeleteItem=row.CreatedByUserId==uid || await RbacService.CanBudgetItem(db,user,id);
        if(!canDeleteItem)
            return Results.Json(new{message="You do not have access to delete this Budget Plan Item."},
                statusCode:StatusCodes.Status403Forbidden);
    }

    db.BudgetPlanItems.Remove(row);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// ---------- R7 Team / Department Performance (UPF) ----------
app.MapGet("/api/performance/overview", async (string? period, long? periodId, long? userId, string? periodType, int? year, int? month, string? level, string? department, HttpContext http, AppDbContext db) =>
{
    var authUserId = Convert.ToInt64(http.Items["AuthUserId"]);
    var currentUser = await db.Users.Include(x=>x.OrgUnit).FirstAsync(x=>x.Id==authUserId);
    var canManage = await RbacService.CanAsync(db,currentUser,"KPI","APPROVE");
    IQueryable<PerformancePeriod> periodQuery = db.PerformancePeriods.AsNoTracking();
    periodQuery = await RbacService.KpiScope(db,currentUser,periodQuery);

    if (!string.IsNullOrWhiteSpace(periodType))
    {
        var pt = periodType.ToUpperInvariant();
        if (pt == "YEARLY") periodQuery = periodQuery.Where(x => x.Period.Length == 4);
        if (pt == "MONTHLY") periodQuery = periodQuery.Where(x => x.Period.Length >= 7);
    }
    if (year.HasValue) periodQuery = periodQuery.Where(x => x.Period.StartsWith(year.Value.ToString()));
    if (month.HasValue)
    {
        var ym = $"{year ?? DateTime.Today.Year:D4}-{month.Value:D2}";
        periodQuery = periodQuery.Where(x => x.Period == ym);
    }
    if (!string.IsNullOrWhiteSpace(level))
    {
        var selectedLevel = level.Trim().ToUpperInvariant();
        periodQuery = periodQuery.Where(x => x.Level == selectedLevel);
    }
    if (!string.IsNullOrWhiteSpace(department))
    {
        var requestedUnit = department.Trim();
        var unit = await db.OrgUnits.AsNoTracking()
            .Where(x => x.Name == requestedUnit || x.Code == requestedUnit)
            .Select(x => new { x.Name, x.Code })
            .FirstOrDefaultAsync();
        var unitName = unit?.Name ?? requestedUnit;
        var unitCode = unit?.Code ?? requestedUnit;
        periodQuery = periodQuery.Where(x => x.Department == unitName || x.Department == unitCode);
    }

    // Keep the member filter options independent from the selected member.
    // Otherwise the dropdown collapses to a single option after filtering.
    var memberIds = await periodQuery
        .Where(x => x.UserId.HasValue)
        .Select(x => x.UserId!.Value)
        .Distinct()
        .ToListAsync();
    var memberRows = await db.Users.AsNoTracking().Include(x=>x.OrgUnit)
        .Where(x=>memberIds.Contains(x.Id))
        .OrderBy(x=>x.OrgUnit==null||x.OrgUnit.Code==null||x.OrgUnit.Code=="").ThenBy(x=>x.OrgUnit==null?"":x.OrgUnit.Code)
        .ThenBy(x=>x.Department==null||x.Department=="").ThenBy(x=>x.Department)
        .ThenBy(x=>x.JobTitle==null||x.JobTitle=="").ThenBy(x=>x.JobTitle)
        .ThenBy(x=>x.JoinDate==null).ThenBy(x=>x.JoinDate).ThenBy(x=>x.Name)
        .Select(x=>new{Id=x.Id,Name=x.Name})
        .ToListAsync();

    if (userId.HasValue)
        periodQuery = periodQuery.Where(x => x.UserId == userId.Value);

    var periods = await periodQuery.OrderByDescending(x => x.Period).ThenBy(x => x.Level).ThenBy(x => x.EmployeeName).ToListAsync();
    var allMembers = !userId.HasValue && !periodId.HasValue && string.IsNullOrWhiteSpace(period);
    var selected = allMembers ? null :
                   periodId.HasValue ? periods.FirstOrDefault(x => x.Id == periodId.Value) :
                   !string.IsNullOrWhiteSpace(period) ? periods.FirstOrDefault(x => x.Period == period) : periods.FirstOrDefault();

    if (periods.Count == 0)
        return Results.Ok(new { members = memberRows, periods = Array.Empty<object>(), selected = (object?)null, items = Array.Empty<object>(), teamScore = 0d, selfScore = 0d, managerScore = 0d, totalWeight = 0d, canManage, currentUserId = authUserId });

    var selectedPeriodIds = selected is null ? periods.Select(x => x.Id).ToList() : new List<long>{selected.Id};
    var items = await db.PerformanceItems.AsNoTracking().Include(x => x.Project).Include(x => x.PerformancePeriod)
        .Where(x => selectedPeriodIds.Contains(x.PerformancePeriodId))
        .OrderBy(x => x.PerformancePeriod!.EmployeeName).ThenBy(x => x.SourceRow).ToListAsync();

    var totalWeight = items.Sum(x => x.Weight);
    var teamScore = totalWeight == 0 ? 0 : items.Sum(x => x.FinalScore * x.Weight) / totalWeight;
    var selfScore = totalWeight == 0 ? 0 : items.Sum(x => x.SelfScore * x.Weight) / totalWeight;
    var managerScore = totalWeight == 0 ? 0 : items.Sum(x => x.ManagerScore * x.Weight) / totalWeight;

    return Results.Ok(new {
        canManage,
        currentUserId = authUserId,
        members = memberRows,
        periods = periods.Select(x => new { x.Id, x.PeriodKey, x.Period, PeriodType = x.Period.Length == 4 ? "YEARLY" : "MONTHLY", x.Level, x.UserId, x.EmployeeName, x.Department, x.Status, x.IsLocked, x.SourceSheet }),
        selected = selected is null ? null : new { selected.Id, selected.PeriodKey, selected.Period, PeriodType = selected.Period.Length == 4 ? "YEARLY" : "MONTHLY", selected.Level, selected.UserId, selected.EmployeeName, selected.Department, selected.Status, selected.IsLocked, selected.SourceSheet },
        teamScore = Math.Round(teamScore, 2), selfScore = Math.Round(selfScore, 2), managerScore = Math.Round(managerScore, 2), totalWeight = Math.Round(totalWeight, 3),
        items = items.Select(x => new { x.Id, x.PerformancePeriodId, EmployeeName = x.PerformancePeriod != null ? x.PerformancePeriod.EmployeeName : "", x.ProjectId, Project = x.Project != null ? x.Project.Name : null, x.SourceRow, x.Strategy, x.Function, x.Plan, x.Actual, x.Weight, x.SelfScore, x.ManagerScore, x.HodScore, x.FinalScore, x.NextPlan, x.Note, x.AllocationsJson, x.Status })
    });
});

app.MapPost("/api/performance/periods", async (PerformancePeriod input, HttpContext http, AppDbContext db) =>
{
    var authUserId = Convert.ToInt64(http.Items["AuthUserId"]);
    var role = Convert.ToString(http.Items["AuthUserRole"]);
    var canManage = AuthService.IsManagerRole(role);

    string? projectName = null;
    if (canManage && string.Equals(input.Level, "PROJECT", StringComparison.OrdinalIgnoreCase))
    {
        projectName = await ResolveKpiProjectName(db, input.EmployeeName);
        if (string.IsNullOrWhiteSpace(projectName))
            return Results.BadRequest(new { message = "Select a valid Project from the project list." });
    }

    if (!canManage)
    {
        input.UserId = authUserId;
        var me = await db.Users.AsNoTracking().FirstAsync(x => x.Id == authUserId);
        input.EmployeeName = me.Name; input.Department = me.Department; input.Level = "INDIVIDUAL";
    }
    else if (input.UserId.HasValue)
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == input.UserId.Value);
        if (u != null) { if (string.IsNullOrWhiteSpace(input.Department)) input.Department = u.Department; var unitCode=await db.OrgUnits.AsNoTracking().Where(o=>o.Name==input.Department||o.Code==input.Department).Select(o=>o.Code).FirstOrDefaultAsync()??input.Department; input.EmployeeName=KpiPeriodEmployeeName(u.Name,input.Level,unitCode,projectName); }
    }

    if (string.IsNullOrWhiteSpace(input.Period)) return Results.BadRequest(new { message = "Period is required." });
    input.Period = input.Period.Trim();
    input.Level = string.IsNullOrWhiteSpace(input.Level) ? "INDIVIDUAL" : input.Level.Trim().ToUpperInvariant();
    var requestedUnit = input.Department?.Trim() ?? "";
    var unitIdentity = await db.OrgUnits.AsNoTracking()
        .Where(o => o.Name == requestedUnit || o.Code == requestedUnit)
        .Select(o => new { o.Name, o.Code })
        .FirstOrDefaultAsync();
    var unitName = unitIdentity?.Name ?? requestedUnit;
    var unitCodeForDuplicate = unitIdentity?.Code ?? requestedUnit;
    input.Department = unitName;
    var duplicatePeriod = await db.PerformancePeriods.AsNoTracking().AnyAsync(p =>
        p.UserId == input.UserId &&
        p.Level == input.Level &&
        p.Period == input.Period &&
        (p.Department == unitName || p.Department == unitCodeForDuplicate));
    if (duplicatePeriod)
        return Results.Conflict(new { message = "A KPI already exists for this Member, Level, Month/Year and Business Unit." });
    input.Id = 0; input.User = null; input.Items = new();
    if (string.IsNullOrWhiteSpace(input.PeriodKey)) input.PeriodKey = $"{input.EmployeeName}-{input.Period}-{Guid.NewGuid().ToString("N")[..6]}";
    db.PerformancePeriods.Add(input); await db.SaveChangesAsync();
    return Results.Created($"/api/performance/periods/{input.Id}", new { input.Id });
});

app.MapPut("/api/performance/periods/{id:long}", async (long id, PerformancePeriod input, HttpContext http, AppDbContext db) =>
{
    var authUserId = Convert.ToInt64(http.Items["AuthUserId"]);
    var role = Convert.ToString(http.Items["AuthUserRole"]);
    var canManage = AuthService.IsManagerRole(role);
    var x = await db.PerformancePeriods.FindAsync(id); if (x is null) return Results.NotFound();
    if (!canManage && x.UserId != authUserId) return Results.StatusCode(StatusCodes.Status403Forbidden);
    if (x.IsLocked) return Results.BadRequest(new { message = "Unlock the period before editing." });

    string? projectName = null;
    if (canManage && string.Equals(input.Level, "PROJECT", StringComparison.OrdinalIgnoreCase))
    {
        projectName = await ResolveKpiProjectName(db, input.EmployeeName);
        if (string.IsNullOrWhiteSpace(projectName))
            return Results.BadRequest(new { message = "Select a valid Project from the project list." });
    }

    x.Period = input.Period?.Trim() ?? "";
    if (canManage)
    {
        x.Level = input.Level; x.Department = input.Department; x.Status = input.Status; x.UserId = input.UserId;
        if (input.UserId.HasValue)
        {
            var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(v => v.Id == input.UserId.Value);
            if (u != null) { var unitCode=await db.OrgUnits.AsNoTracking().Where(o=>o.Name==x.Department||o.Code==x.Department).Select(o=>o.Code).FirstOrDefaultAsync()??x.Department; x.EmployeeName = KpiPeriodEmployeeName(u.Name,x.Level,unitCode,projectName); }
        }
        else x.EmployeeName = input.EmployeeName;
    }
    x.Level = string.IsNullOrWhiteSpace(x.Level) ? "INDIVIDUAL" : x.Level.Trim().ToUpperInvariant();
    var requestedUnit = x.Department?.Trim() ?? "";
    var unitIdentity = await db.OrgUnits.AsNoTracking()
        .Where(o => o.Name == requestedUnit || o.Code == requestedUnit)
        .Select(o => new { o.Name, o.Code })
        .FirstOrDefaultAsync();
    var unitName = unitIdentity?.Name ?? requestedUnit;
    var unitCodeForDuplicate = unitIdentity?.Code ?? requestedUnit;
    x.Department = unitName;
    var duplicatePeriod = await db.PerformancePeriods.AsNoTracking().AnyAsync(p =>
        p.Id != x.Id &&
        p.UserId == x.UserId &&
        p.Level == x.Level &&
        p.Period == x.Period &&
        (p.Department == unitName || p.Department == unitCodeForDuplicate));
    if (duplicatePeriod)
        return Results.Conflict(new { message = "A KPI already exists for this Member, Level, Month/Year and Business Unit." });
    await db.SaveChangesAsync(); return Results.Ok(new { x.Id });
});

app.MapPost("/api/performance/periods/{id:long}/lock", async (long id, HttpContext http, AppDbContext db) =>
{
    var authUserId = Convert.ToInt64(http.Items["AuthUserId"]);
    var role = Convert.ToString(http.Items["AuthUserRole"]);
    var canManage = AuthService.IsManagerRole(role);

    var x = await db.PerformancePeriods.FindAsync(id);
    if (x is null) return Results.NotFound();

    var isOwner = x.UserId.HasValue && x.UserId.Value == authUserId;
    var finalStatus = string.Equals(x.Status, "APPROVED", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(x.Status, "REJECTED", StringComparison.OrdinalIgnoreCase);

    if (finalStatus)
        return Results.BadRequest(new
        {
            message = $"KPI {x.Status} is final and cannot be unlocked."
        });

    if (!canManage && !isOwner)
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    if (!canManage && !x.IsLocked)
        return Results.BadRequest(new
        {
            message = "Member can unlock their own KPI for editing, but cannot manually lock an open KPI."
        });

    var unlocking = x.IsLocked;

    if (unlocking)
    {
        var pendingRequests = await db.ApprovalRequests
            .Include(r => r.Steps)
            .Where(r => r.EntityId == x.Id
                     && (r.WorkflowType == "KPI_APPROVAL"
                         || r.WorkflowType == "KPI_LEVELS"
                         || r.WorkflowType.StartsWith("KPI_MULTI_"))
                     && r.Status == "PENDING")
            .ToListAsync();

        // Once any approver has acted, the submitted KPI becomes immutable.
        // It may only continue through the approval workflow (or be rejected);
        // the owner/manager can no longer withdraw it by unlocking.
        var hasApprovalAction = pendingRequests.Any(r => r.Steps.Any(s =>
            s.Status == "APPROVED" || s.Status == "REJECTED" || s.ActedAt != null));
        if (hasApprovalAction)
            return Results.BadRequest(new
            {
                message = "KPI cannot be unlocked because approval has already started. Complete the approval workflow or reject the KPI."
            });

        foreach (var req in pendingRequests)
        {
            req.Status = "CANCELLED";
            req.CompletedAt = DateTime.UtcNow;

            foreach (var step in req.Steps.Where(z => z.Status == "PENDING"))
            {
                step.Status = "CANCELLED";
                step.Comment = string.IsNullOrWhiteSpace(step.Comment)
                    ? "Withdrawn by KPI owner for editing."
                    : step.Comment;
                step.ActedAt = DateTime.UtcNow;
            }
        }

        x.IsLocked = false;
        x.LockedAt = null;
        x.Status = "OPEN";

        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            x.Id,
            x.IsLocked,
            x.Status,
            approvalWithdrawn = pendingRequests.Count,
            message = pendingRequests.Count > 0
                ? "KPI unlocked. Pending approval was withdrawn; edit and submit again when ready."
                : "KPI unlocked for editing."
        });
    }

    x.IsLocked = true;
    x.LockedAt = DateTime.UtcNow;
    x.Status = "LOCKED";

    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        x.Id,
        x.IsLocked,
        x.Status,
        approvalWithdrawn = 0
    });
});

app.MapPost("/api/performance/periods/{id:long}/clone", async (long id, string period, HttpContext http, AppDbContext db) =>
{
    var authUserId = Convert.ToInt64(http.Items["AuthUserId"]);
    var role = Convert.ToString(http.Items["AuthUserRole"]);
    var canManage = AuthService.IsManagerRole(role);
    var src = await db.PerformancePeriods.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
    if (src is null) return Results.NotFound();
    if (!canManage && src.UserId != authUserId) return Results.StatusCode(StatusCodes.Status403Forbidden);
    if (string.IsNullOrWhiteSpace(period)) return Results.BadRequest(new { message = "period is required" });

    var key = $"{src.EmployeeName}-{src.Department}-{period}-{Guid.NewGuid().ToString("N")[..6]}";
    var dest = new PerformancePeriod { PeriodKey = key, Period = period, Level = src.Level, UserId = src.UserId, EmployeeName = src.EmployeeName, Department = src.Department, SourceSheet = "MPMS", Status = "OPEN", IsLocked = false };
    db.PerformancePeriods.Add(dest); await db.SaveChangesAsync();
    foreach (var item in src.Items) db.PerformanceItems.Add(new PerformanceItem {
        PerformancePeriodId = dest.Id, ProjectId = item.ProjectId, Strategy = item.Strategy, Function = item.Function,
        Plan = item.NextPlan.Length > 0 ? item.NextPlan : item.Plan, Actual = "", Weight = item.Weight,
        SelfScore = 0, ManagerScore = 0, HodScore = 0, FinalScore = 0, NextPlan = "", Note = "",
        AllocationsJson = item.AllocationsJson, Status = "OPEN"
    });
    await db.SaveChangesAsync(); return Results.Created($"/api/performance/periods/{dest.Id}", new { dest.Id, dest.Period });
});

app.MapDelete("/api/performance/periods/{id:long}", async (long id, HttpContext http, AppDbContext db) =>
{
    var authUserId = Convert.ToInt64(http.Items["AuthUserId"]);
    var role = Convert.ToString(http.Items["AuthUserRole"]);
    var canManage = AuthService.IsManagerRole(role);
    var x = await db.PerformancePeriods.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id); if (x is null) return Results.NotFound();
    if (!canManage && x.UserId != authUserId) return Results.StatusCode(StatusCodes.Status403Forbidden);
    if (x.IsLocked) return Results.BadRequest(new { message = "Locked period cannot be deleted." });
    db.PerformancePeriods.Remove(x); await db.SaveChangesAsync(); return Results.NoContent();
});

static string KpiPeriodEmployeeName(string? value, string? level, string? department, string? projectName = null)
{
    var clean=System.Text.RegularExpressions.Regex.Replace(
        value?.Trim()??"",
        @"^\[(?:UPF|Project|PRJ)\]\[[^\]]+\]\s*",
        "",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    var unit=string.IsNullOrWhiteSpace(department)?"Unit":department.Trim();
    return string.Equals(level,"DEPARTMENT",StringComparison.OrdinalIgnoreCase)?$"[UPF][{unit}] {clean}":
           string.Equals(level,"PROJECT",StringComparison.OrdinalIgnoreCase)?$"[PRJ][{projectName?.Trim() ?? "Project"}] {clean}":clean;
}

static async Task<string?> ResolveKpiProjectName(AppDbContext db, string? employeeName)
{
    var match=System.Text.RegularExpressions.Regex.Match(
        employeeName?.Trim()??"",
        @"^\[PRJ\]\[([^\]]+)\]",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    if(!match.Success)return null;
    var requested=match.Groups[1].Value.Trim();
    return await db.Projects.AsNoTracking()
        .Where(p=>p.Name==requested||p.Code==requested)
        .Select(p=>p.Name)
        .FirstOrDefaultAsync();
}

static string CleanKpiItemPlan(string? value)=>System.Text.RegularExpressions.Regex.Replace(
    value?.Trim()??"",@"^\[(?:UPF|Project|PRJ)\]\[[^\]]+\]\s*","",System.Text.RegularExpressions.RegexOptions.IgnoreCase);

app.MapPost("/api/performance/items", async (PerformanceItem input, HttpContext http, AppDbContext db) =>
{
    var authUserId = Convert.ToInt64(http.Items["AuthUserId"]);
    var canManage = AuthService.IsManagerRole(Convert.ToString(http.Items["AuthUserRole"]));
    var period = await db.PerformancePeriods.FindAsync(input.PerformancePeriodId);
    if (period is null) return Results.BadRequest(new { message = "Performance period not found" });
    if (!canManage && period.UserId != authUserId) return Results.StatusCode(StatusCodes.Status403Forbidden);
    if (period.IsLocked) return Results.BadRequest(new { message = "Period is locked" });

    input.Id = 0; input.Project = null; input.PerformancePeriod = null;
    input.Plan=CleanKpiItemPlan(input.Plan);
    if (!canManage) { input.ManagerScore = 0; input.HodScore = 0; }
    input.FinalScore = input.HodScore > 0 ? input.HodScore : input.ManagerScore > 0 ? input.ManagerScore : input.SelfScore;
    db.PerformanceItems.Add(input); await db.SaveChangesAsync();
    return Results.Created($"/api/performance/items/{input.Id}", new { input.Id });
});

app.MapPut("/api/performance/items/{id:long}", async (long id, PerformanceItem input, HttpContext http, AppDbContext db) =>
{
    var authUserId = Convert.ToInt64(http.Items["AuthUserId"]);
    var canManage = AuthService.IsManagerRole(Convert.ToString(http.Items["AuthUserRole"]));
    var x = await db.PerformanceItems.Include(x => x.PerformancePeriod).FirstOrDefaultAsync(x => x.Id == id);
    if (x is null) return Results.NotFound();
    if (!canManage && x.PerformancePeriod?.UserId != authUserId) return Results.StatusCode(StatusCodes.Status403Forbidden);
    if (x.PerformancePeriod?.IsLocked == true) return Results.BadRequest(new { message = "Period is locked" });

    x.ProjectId = input.ProjectId; x.Strategy = input.Strategy; x.Function = input.Function; x.Plan = CleanKpiItemPlan(input.Plan);
    x.Actual = input.Actual; x.Weight = input.Weight; x.SelfScore = input.SelfScore; x.NextPlan = input.NextPlan; x.Note = input.Note; x.Status = input.Status;
    if (canManage) { x.ManagerScore = input.ManagerScore; x.HodScore = input.HodScore; }
    x.FinalScore = x.HodScore > 0 ? x.HodScore : x.ManagerScore > 0 ? x.ManagerScore : x.SelfScore;
    await db.SaveChangesAsync(); return Results.Ok(new { x.Id, x.FinalScore });
});

app.MapDelete("/api/performance/items/{id:long}", async (long id, HttpContext http, AppDbContext db) =>
{
    var authUserId = Convert.ToInt64(http.Items["AuthUserId"]);
    var canManage = AuthService.IsManagerRole(Convert.ToString(http.Items["AuthUserRole"]));
    var x = await db.PerformanceItems.Include(x => x.PerformancePeriod).FirstOrDefaultAsync(x => x.Id == id);
    if (x is null) return Results.NotFound();
    if (!canManage && x.PerformancePeriod?.UserId != authUserId) return Results.StatusCode(StatusCodes.Status403Forbidden);
    if (x.PerformancePeriod?.IsLocked == true) return Results.BadRequest(new { message = "Period is locked" });
    db.PerformanceItems.Remove(x); await db.SaveChangesAsync(); return Results.NoContent();
});


// ---------- R7.4 Excel Exchange: KPI & Annual Budget ----------
app.MapGet("/api/excel/kpi/export/{periodId:long}", async (long periodId, HttpContext http, AppDbContext db, IWebHostEnvironment env) =>
{
    var userId = Convert.ToInt64(http.Items["AuthUserId"]);
    var canManage = AuthService.IsManagerRole(Convert.ToString(http.Items["AuthUserRole"]));
    try
    {
        var result = await ExcelExchangeService.ExportKpiAsync(db, env, periodId, userId, canManage);
        return Results.File(result.Bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.FileName);
    }
    catch (UnauthorizedAccessException) { return Results.StatusCode(StatusCodes.Status403Forbidden); }
    catch (Exception ex) { return Results.BadRequest(new { message = ex.Message }); }
});

app.MapPost("/api/excel/kpi/import", async (HttpRequest request, HttpContext http, AppDbContext db, bool dryRun = true, string mode = "replace") =>
{
    if (!request.HasFormContentType) return Results.BadRequest(new { message = "multipart/form-data required." });
    var form = await request.ReadFormAsync();
    var file = form.Files.FirstOrDefault();
    if (file is null || file.Length == 0) return Results.BadRequest(new { message = "Excel file is required." });
    if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) return Results.BadRequest(new { message = "Only .xlsx files are supported." });
    var userId = Convert.ToInt64(http.Items["AuthUserId"]);
    var canManage = AuthService.IsManagerRole(Convert.ToString(http.Items["AuthUserRole"]));
    await using var stream = file.OpenReadStream();
    try { return Results.Ok(await ExcelExchangeService.ImportKpiAsync(db, stream, file.FileName, userId, canManage, dryRun, mode)); }
    catch (Exception ex) { return Results.BadRequest(new { message = ex.Message }); }
});

app.MapGet("/api/excel/budget/export/{year:int}", async (int year, HttpContext http, AppDbContext db) =>
{
    var uid = Convert.ToInt64(http.Items["AuthUserId"]);
    var user = await db.Users.Include(x => x.OrgUnit).FirstOrDefaultAsync(x => x.Id == uid);
    if (user is null) return Results.Unauthorized();
    if (!string.Equals(user.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        return Results.Json(new { message = "Tài khoản của bạn đang không hoạt động." }, statusCode: StatusCodes.Status403Forbidden);
    var canExport = await RbacService.CanAsync(db, user, "BUDGET", "DOWNLOAD");
    if (!canExport)
        return Results.Json(new { message = "Bạn chưa có quyền Export Budget." }, statusCode: StatusCodes.Status403Forbidden);
    var result = await ExcelExchangeService.ExportBudgetAsync(db, year, user);
    return Results.File(result.Bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.FileName);
});

app.MapPost("/api/excel/budget/import", async (
    HttpRequest request,
    HttpContext http,
    AppDbContext db,
    bool dryRun = true,
    string mode = "merge") =>
{
    var uid = Convert.ToInt64(http.Items["AuthUserId"]);
    var user = await db.Users.Include(x => x.OrgUnit).FirstOrDefaultAsync(x => x.Id == uid);

    if (user is null)
        return Results.Unauthorized();

    if (!string.Equals(user.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        return Results.Json(new { message = "Tài khoản của bạn đang không hoạt động." },
            statusCode: StatusCodes.Status403Forbidden);

    var manager = AuthService.IsManagerRole(Convert.ToString(http.Items["AuthUserRole"]));

    var canUpload = await RbacService.CanAsync(db, user, "BUDGET", "UPLOAD");
    var canCreate = await RbacService.CanAsync(db, user, "BUDGET", "CREATE");
    var canEdit   = await RbacService.CanAsync(db, user, "BUDGET", "EDIT");
    var canFull   = await RbacService.CanAsync(db, user, "BUDGET", "FULL");

    var scope = await RbacService.GetScopeAsync(db, user, "BUDGET");
    var hasScopedBudgetAccess =
        !manager &&
        (
            scope.Mode.Equals("OWN_ORG", StringComparison.OrdinalIgnoreCase) ||
            scope.Mode.Equals("SELECTED_ORGS", StringComparison.OrdinalIgnoreCase)
        );

    if (!manager && !canUpload && !canCreate && !canEdit && !canFull && !hasScopedBudgetAccess)
        return Results.Json(
            new { message = "Bạn chưa được cấp quyền Import Budget hoặc chưa được gán phạm vi Business Unit cho Budget." },
            statusCode: StatusCodes.Status403Forbidden);

    if (!request.HasFormContentType)
        return Results.BadRequest(new { message = "multipart/form-data required." });

    var form = await request.ReadFormAsync();
    var file = form.Files.FirstOrDefault();

    if (file is null || file.Length == 0)
        return Results.BadRequest(new { message = "Vui lòng chọn file Excel để import." });

    if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { message = "Chỉ hỗ trợ file .xlsx." });

    await using var input = file.OpenReadStream();
    using var buffer = new MemoryStream();
    await input.CopyToAsync(buffer);
    var bytes = buffer.ToArray();

    var foundOrgUnits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var foundProjectRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    try
    {
        using var verifyStream = new MemoryStream(bytes);
        using var book = new XLWorkbook(verifyStream);

        foreach (var ws in book.Worksheets)
        {
            var used = ws.RangeUsed();
            if (used is null) continue;

            var firstRow = used.RangeAddress.FirstAddress.RowNumber;
            var lastRow  = used.RangeAddress.LastAddress.RowNumber;
            var firstCol = used.RangeAddress.FirstAddress.ColumnNumber;
            var lastCol  = used.RangeAddress.LastAddress.ColumnNumber;

            int headerRow = 0;
            int orgCol = 0;
            int projectCodeCol = 0;
            int projectNameCol = 0;

            var searchLastRow = Math.Min(lastRow, firstRow + 30);

            for (var r = firstRow; r <= searchLastRow; r++)
            {
                for (var c = firstCol; c <= lastCol; c++)
                {
                    var text = ws.Cell(r, c).GetString().Trim();

                    if (orgCol == 0 &&
                        (text.Equals("Business Unit", StringComparison.OrdinalIgnoreCase) ||
                         text.Equals("Org Unit", StringComparison.OrdinalIgnoreCase) ||
                         text.Equals("BusinessUnit", StringComparison.OrdinalIgnoreCase)))
                    {
                        headerRow = r;
                        orgCol = c;
                    }

                    if (projectCodeCol == 0 &&
                        (text.Equals("Project Code", StringComparison.OrdinalIgnoreCase) ||
                         text.Equals("ProjectCode", StringComparison.OrdinalIgnoreCase)))
                    {
                        headerRow = r;
                        projectCodeCol = c;
                    }

                    if (projectNameCol == 0 &&
                        (text.Equals("Project", StringComparison.OrdinalIgnoreCase) ||
                         text.Equals("Project Name", StringComparison.OrdinalIgnoreCase)))
                    {
                        headerRow = r;
                        projectNameCol = c;
                    }
                }
            }

            if (headerRow == 0) continue;

            for (var r = headerRow + 1; r <= lastRow; r++)
            {
                if (orgCol != 0)
                {
                    var org = ws.Cell(r, orgCol).GetString().Trim();
                    if (!string.IsNullOrWhiteSpace(org))
                        foundOrgUnits.Add(org);
                }

                var projectCode = projectCodeCol != 0 ? ws.Cell(r, projectCodeCol).GetString().Trim() : "";
                var projectName = projectNameCol != 0 ? ws.Cell(r, projectNameCol).GetString().Trim() : "";
                var projectRef = !string.IsNullOrWhiteSpace(projectCode) ? projectCode : projectName;

                if (!string.IsNullOrWhiteSpace(projectRef))
                    foundProjectRefs.Add(projectRef);
            }
        }
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { message = $"Không đọc được file Budget: {ex.Message}" });
    }

    if (!manager)
    {
        if (foundOrgUnits.Count == 0)
            return Results.BadRequest(new
            {
                message = "Member phải sử dụng file Export từ MPMS làm template Import để hệ thống kiểm tra Business Unit."
            });

        var deniedOrgs = new List<string>();
        foreach (var org in foundOrgUnits)
            if (!await RbacService.CanBudgetOrg(db, user, org))
                deniedOrgs.Add(org);

        if (deniedOrgs.Count > 0)
            return Results.Json(new
            {
                message = "File có Business Unit ngoài phạm vi được cấp cho tài khoản của bạn.",
                deniedBusinessUnits = deniedOrgs
            }, statusCode: StatusCodes.Status403Forbidden);
    }

    if (foundProjectRefs.Count > 0)
    {
        var projects = await db.Projects.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED")
            .Select(x => new { x.Code, x.Name })
            .ToListAsync();

        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prj in projects)
        {
            if (!string.IsNullOrWhiteSpace(prj.Code)) existing.Add(prj.Code.Trim());
            if (!string.IsNullOrWhiteSpace(prj.Name)) existing.Add(prj.Name.Trim());
        }

        var missingProjects = foundProjectRefs
            .Where(x => !existing.Contains(x.Trim()))
            .OrderBy(x => x)
            .ToList();

        if (missingProjects.Count > 0)
            return Results.Json(new
            {
                code = "PROJECT_NOT_FOUND",
                message = "Dự án của bạn chưa tồn tại trên hệ thống. Vui lòng vào Projects → New Project để tạo mới trước khi Import Budget.",
                missingProjects
            }, statusCode: StatusCodes.Status409Conflict);
    }

    try
    {
        using var serviceStream = new MemoryStream(bytes);
        var result = await ExcelExchangeService.ImportBudgetAsync(db, serviceStream, file.FileName, dryRun, mode);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

// ---------- R7 Edit / Delete endpoints for operational modules ----------
app.MapDelete("/api/projects/{id:long}", async (long id, HttpContext http, AppDbContext db) =>
{
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);
    var actor=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid);
    if(actor is null) return Results.Unauthorized();

    var project=await db.Projects.FindAsync(id);
    if(project is null) return Results.NotFound(new{message="Project not found."});

    var isCreator=project.CreatedByUserId==uid;
    var isOwner=project.OwnerId==uid;
    var isMember=await db.ProjectMembers.AsNoTracking()
        .AnyAsync(x=>x.ProjectId==id && x.UserId==uid && x.IsActive);

    var canEdit=await RbacService.CanAsync(db,actor,"PROJECTS","EDIT");
    var canDelete=await RbacService.CanAsync(db,actor,"PROJECTS","DELETE");
    var allScope=await RbacService.IsAllScopeAsync(db,actor,"PROJECTS");

    var inScope=allScope;
    if(!inScope)
    {
        IQueryable<Project> scoped=db.Projects.AsNoTracking().Where(x=>x.Status!="DEACTIVATED" && x.Status!="DELETED").Where(x=>x.Id==id);
        scoped=await RbacService.ApplyProjectScopeAsync(db,actor,scoped);
        inScope=await scoped.AnyAsync();
    }

    // Creator and Owner may always deactivate their own project.
    // Other active project members need PROJECTS EDIT.
    // Admin/authorized users may use PROJECTS DELETE or PROJECTS EDIT within scope.
    var allowed=
        isCreator ||
        isOwner ||
        (isMember && canEdit) ||
        (inScope && (canEdit || canDelete));

    if(!allowed)
        return Results.Json(new{
            message="You do not have permission to deactivate this Project.",
            projectId=id,
            isCreator,
            isOwner,
            isMember,
            canEdit,
            canDelete,
            inScope
        },statusCode:StatusCodes.Status403Forbidden);

    if(string.Equals(project.Status,"DEACTIVATED",StringComparison.OrdinalIgnoreCase))
        return Results.Ok(new{
            project.Id,project.Code,project.Name,project.Status,
            message="Project is already deactivated."
        });

    project.Status="DEACTIVATED";
    project.UpdatedByUserId=uid;
    project.UpdatedAt=DateTime.UtcNow;

    // Preserve membership rows for audit, but mark them inactive.
    var memberships=await db.ProjectMembers
        .Where(x=>x.ProjectId==id && x.IsActive)
        .ToListAsync();
    foreach(var m in memberships)
    {
        m.IsActive=false;
        m.UpdatedByUserId=uid;
        m.UpdatedAt=DateTime.UtcNow;
    }

    await db.SaveChangesAsync();

    return Results.Ok(new{
        project.Id,
        project.Code,
        project.Name,
        project.Status,
        project.UpdatedByUserId,
        project.UpdatedAt,
        message="Project deactivated. Historical data has been retained."
    });
});
app.MapPut("/api/tasks/{id:long}", async (long id, ProjectTask input, AppDbContext db) => { var x=await db.Tasks.FindAsync(id);if(x is null)return Results.NotFound();x.ProjectId=input.ProjectId;x.ParentTaskId=input.ParentTaskId;x.MilestoneId=input.MilestoneId;x.Code=input.Code;x.Name=input.Name;x.Description=input.Description;x.AssigneeId=input.AssigneeId;x.Status=input.Status;x.Priority=input.Priority;x.StartDate=input.StartDate;x.DueDate=input.DueDate;x.CompletedDate=input.CompletedDate;x.ProgressPct=Math.Clamp(input.ProgressPct,0,100);x.EstimatedHours=Math.Max(0,input.EstimatedHours);x.ActualHours=Math.Max(0,input.ActualHours);x.SortOrder=input.SortOrder;await db.SaveChangesAsync();return Results.Ok(new{x.Id});});
app.MapDelete("/api/tasks/{id:long}", async (long id, AppDbContext db) => { var x=await db.Tasks.FindAsync(id); if(x is null)return Results.NotFound(); db.Tasks.Remove(x); await db.SaveChangesAsync(); return Results.NoContent(); });
app.MapPut("/api/milestones/{id:long}", async (long id, Milestone input, AppDbContext db) => {var x=await db.Milestones.FindAsync(id);if(x is null)return Results.NotFound();x.ProjectId=input.ProjectId;x.Name=input.Name;x.Description=input.Description;x.DueDate=input.DueDate;x.ActualDate=input.ActualDate;x.Status=input.Status;x.WeightPct=Math.Clamp(input.WeightPct,0,100);await db.SaveChangesAsync();return Results.Ok(new{x.Id});});
app.MapDelete("/api/milestones/{id:long}", async (long id, AppDbContext db) => { var x=await db.Milestones.FindAsync(id); if(x is null)return Results.NotFound(); db.Milestones.Remove(x); await db.SaveChangesAsync(); return Results.NoContent(); });
app.MapPut("/api/risks/{id:long}", async (long id, Risk input, AppDbContext db) => {var x=await db.Risks.FindAsync(id);if(x is null)return Results.NotFound();x.ProjectId=input.ProjectId;x.Code=input.Code;x.Title=input.Title;x.Description=input.Description;x.Category=input.Category;x.Probability=Math.Clamp(input.Probability,1,5);x.Impact=Math.Clamp(input.Impact,1,5);x.SeverityScore=x.Probability*x.Impact;x.OwnerId=input.OwnerId;x.ResponseStrategy=input.ResponseStrategy;x.MitigationPlan=input.MitigationPlan;x.ContingencyPlan=input.ContingencyPlan;x.TargetDate=input.TargetDate;x.Status=input.Status;await db.SaveChangesAsync();return Results.Ok(new{x.Id,x.SeverityScore});});
app.MapDelete("/api/risks/{id:long}", async (long id, AppDbContext db) => { var x=await db.Risks.FindAsync(id); if(x is null)return Results.NotFound(); var issues=await db.Issues.Where(i=>i.RiskId==id).ToListAsync(); foreach(var i in issues)i.RiskId=null; db.Risks.Remove(x); await db.SaveChangesAsync(); return Results.NoContent(); });
app.MapPut("/api/issues/{id:long}", async (long id, Issue input, AppDbContext db) => {var x=await db.Issues.FindAsync(id);if(x is null)return Results.NotFound();x.ProjectId=input.ProjectId;x.RiskId=input.RiskId;x.Code=input.Code;x.Title=input.Title;x.Description=input.Description;x.Category=input.Category;x.Severity=input.Severity;x.Status=input.Status;x.ReportedBy=input.ReportedBy;x.AssignedTo=input.AssignedTo;x.ReportedDate=input.ReportedDate;x.TargetResolutionDate=input.TargetResolutionDate;x.ResolvedDate=input.ResolvedDate;x.Resolution=input.Resolution;await db.SaveChangesAsync();return Results.Ok(new{x.Id});});
app.MapDelete("/api/issues/{id:long}", async (long id, AppDbContext db) => { var x=await db.Issues.FindAsync(id); if(x is null)return Results.NotFound(); db.Issues.Remove(x); await db.SaveChangesAsync(); return Results.NoContent(); });
app.MapPut("/api/budgets/{id:long}", async (long id, BudgetLine input, AppDbContext db) => {var x=await db.BudgetLines.FindAsync(id);if(x is null)return Results.NotFound();x.ProjectId=input.ProjectId;x.Code=input.Code;x.Category=input.Category;x.Description=input.Description;x.BaselineAmount=input.BaselineAmount;x.RevisedAmount=input.RevisedAmount;x.CommittedAmount=input.CommittedAmount;x.ActualAmount=input.ActualAmount;x.ForecastAmount=input.ForecastAmount;x.Currency=input.Currency;await db.SaveChangesAsync();return Results.Ok(new{x.Id});});
app.MapDelete("/api/budgets/{id:long}", async (long id, AppDbContext db) => { var x=await db.BudgetLines.FindAsync(id); if(x is null)return Results.NotFound(); db.BudgetTransactions.RemoveRange(db.BudgetTransactions.Where(t=>t.BudgetLineId==id)); db.BudgetLines.Remove(x); await db.SaveChangesAsync(); return Results.NoContent(); });
app.MapPut("/api/suppliers/{id:long}", async (long id, Supplier input, AppDbContext db) =>
{
    await SupplierProfileSchema.EnsureAsync(db);
    var x=await db.Suppliers.FindAsync(id);
    if(x is null)return Results.NotFound();
    x.OrgUnitId=input.OrgUnitId;
    if(!string.IsNullOrWhiteSpace(input.Code))x.Code=input.Code;
    x.Name=input.Name??"";
    x.CompanyName=input.CompanyName??"";
    x.ShortName=input.ShortName??"";
    x.Website=input.Website??"";
    x.TaxCode=input.TaxCode??"";
    x.BusinessRegistrationNo=input.BusinessRegistrationNo??"";
    x.LegalRepresentative=input.LegalRepresentative??"";
    x.RegistrationDate=input.RegistrationDate;
    x.Country=input.Country??"Vietnam";
    x.Category=input.Category??"";
    x.ContactName=input.ContactName??"";
    x.ContactPosition=input.ContactPosition??"";
    x.Email=input.Email??"";
    x.Phone=input.Phone??"";
    x.AlternativePhone=input.AlternativePhone??"";
    x.Address=input.Address??"";
    x.ProvinceCity=input.ProvinceCity??"";
    x.PaymentTerms=input.PaymentTerms??"";
    x.Currency=input.Currency??"VND";
    x.BankName=input.BankName??"";
    x.BankAccountNo=input.BankAccountNo??"";
    x.BankAccountName=input.BankAccountName??"";
    x.BankBranch=input.BankBranch??"";
    x.InternalOwnerId=input.InternalOwnerId;
    x.Notes=input.Notes??"";
    x.Status=input.Status??"ACTIVE";
    x.Rating=Math.Clamp(input.Rating,0,5);
    await db.SaveChangesAsync();
    return Results.Ok(new{x.Id});
});
app.MapDelete("/api/suppliers/{id:long}", async (long id, AppDbContext db) => { var x=await db.Suppliers.FindAsync(id); if(x is null)return Results.NotFound(); var evals=await db.SupplierEvaluations.Include(e=>e.Scores).Where(e=>e.SupplierId==id).ToListAsync(); db.SupplierEvaluations.RemoveRange(evals); db.Contracts.RemoveRange(db.Contracts.Where(c=>c.SupplierId==id)); db.Suppliers.Remove(x); await db.SaveChangesAsync(); return Results.NoContent(); });
app.MapPut("/api/kpi/evaluations/{id:long}", async (long id, SupplierEvaluation input, AppDbContext db) => { var x=await db.SupplierEvaluations.FindAsync(id); if(x is null)return Results.NotFound(); x.OverallScore=Math.Clamp(input.OverallScore,0,5); x.Status=input.Status; x.Rating=x.OverallScore>=4.5?"EXCELLENT":x.OverallScore>=4.0?"VERY_GOOD":x.OverallScore>=3.0?"ACCEPTABLE":x.OverallScore>=2.0?"NEEDS_IMPROVEMENT":"POOR"; await db.SaveChangesAsync(); return Results.Ok(new{x.Id,x.OverallScore,x.Rating,x.Status}); });
app.MapDelete("/api/kpi/evaluations/{id:long}", async (long id, AppDbContext db) => { var x=await db.SupplierEvaluations.Include(e=>e.Scores).FirstOrDefaultAsync(e=>e.Id==id); if(x is null)return Results.NotFound(); db.SupplierEvaluations.Remove(x); await db.SaveChangesAsync(); return Results.NoContent(); });
app.MapPut("/api/documents/{id:long}", async (long id,DocumentRecord input,HttpContext http,AppDbContext db) =>
{
    var x=await db.Documents.FindAsync(id);if(x is null)return Results.NotFound();
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);
    var actor=await db.Users.AsNoTracking().FirstOrDefaultAsync(u=>u.Id==uid);
    if(actor is null)return Results.Unauthorized();
    if(!await RbacService.CanAccessDocumentAsync(db,actor,x,"EDIT"))return Results.Forbid();
    if(string.Equals(input.EntityType,"PROJECT",StringComparison.OrdinalIgnoreCase) &&
       !await RbacService.IsAllScopeAsync(db,actor,"DOCUMENTS"))
    {
        var projectIds=await RbacService.AllowedProjectIdsAsync(db,actor,"DOCUMENTS");
        if(!projectIds.Contains(input.EntityId))return Results.Forbid();
    }
    x.Name=input.Name;x.Category=input.Category;x.EntityType=input.EntityType;x.EntityId=input.EntityId;x.Status=input.Status;
    await db.SaveChangesAsync();return Results.Ok(new{x.Id});
});
app.MapDelete("/api/documents/{id:long}", async (
    long id,
    AppDbContext db,
    IWebHostEnvironment env,
    M365StorageService m365,
    HttpContext http) =>
{
    var x = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, http.RequestAborted);
    if (x is null) return Results.NotFound();
    var uid=Convert.ToInt64(http.Items["AuthUserId"]);
    var actor=await db.Users.AsNoTracking().FirstOrDefaultAsync(u=>u.Id==uid,http.RequestAborted);
    if(actor is null)return Results.Unauthorized();
    if(!await RbacService.CanAccessDocumentAsync(db,actor,x,"DELETE"))return Results.Forbid();

    if (string.Equals(x.StorageProvider, "M365", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(x.StorageItemId) && m365.Enabled)
    {
        await m365.DeleteAsync(x.StorageItemId, http.RequestAborted);
    }
    else
    {
        try
        {
            var decodedPath = Uri.UnescapeDataString(x.FilePath ?? "");
            var relative = decodedPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var full = Path.GetFullPath(Path.Combine(env.ContentRootPath, relative));
            var storageRoot = Path.GetFullPath(Path.Combine(env.ContentRootPath, "storage")) + Path.DirectorySeparatorChar;

            if (full.StartsWith(storageRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(full))
                File.Delete(full);
        }
        catch {}
    }

    x.Status = "DEACTIVATED";
    x.IsCurrent = false;
    await db.SaveChangesAsync(http.RequestAborted);

    return Results.NoContent();
});


using(var __supplierScope=app.Services.CreateScope())
{
    var __supplierDb=__supplierScope.ServiceProvider.GetRequiredService<AppDbContext>();
    await SupplierProfileSchema.EnsureAsync(__supplierDb);
}

app.MapAIHelpEndpoints();
app.MapM365DocumentEndpoints();

using(var m365StorageScope=app.Services.CreateScope())
{
    var db=m365StorageScope.ServiceProvider.GetRequiredService<AppDbContext>();
    // The hosted SQL Server can take longer than the default 30 seconds for
    // idempotent schema checks and ALTER TABLE batches during application startup.
    db.Database.SetCommandTimeout(180);
    await M365StorageSchema.EnsureAsync(db);
    await ClubTournamentSchema.EnsureAsync(db);
    await ClubFinanceSchema.EnsureAsync(db);
    await ClubProfileSchema.EnsureAsync(db);
    // LEGACY_TOURNAMENT_SEED_OPT_IN_V1
    // Legacy spreadsheet seed is opt-in so a deliberately deleted tournament is not recreated on restart.
    if(builder.Configuration.GetValue<bool>("SeedLegacyClubTournaments"))
        await ClubTournamentSeedService.SeedAsync(db, app.Environment);
}

app.MapAccessControlEndpoints();
app.MapSettingsEndpoints();


// AUTO_MAPPED_ENDPOINT_MODULES_V1
app.MapAdminAccountEndpoints(); // AdminAccountEndpoints.cs
app.MapBusinessUnitProfileEndpoints(); // BusinessUnitProfileEndpoints.cs
app.MapITAssetEndpoints();
app.MapPersonalFcEndpoints(); // ITAssetEndpoints.cs
app.MapITDomainEndpoints(); // ITDomainEndpoints.cs
app.MapKpiApprovalEndpoints(); // KpiApprovalEndpoints.cs
app.MapCapitalInvestmentEndpoints(); // CapitalInvestmentEndpoints.cs

app.MapClubTournamentEndpoints();

app.MapClubFinanceEndpoints();

app.MapClubProfileEndpoints();

app.MapClubDocumentEndpoints();

app.MapPersonalFcAvatarEndpoints();


// IT_ASSET_LICENSE_MANAGER_USER_SQLSERVER_SCHEMA_V4
try
{
    using var assetLicenseSqlScope = app.Services.CreateScope();
    var dbSql = assetLicenseSqlScope.ServiceProvider.GetRequiredService<AppDbContext>();

    if ((dbSql.Database.ProviderName ?? "").Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        const string ensureManagerUserColumnsSql = """
DECLARE @assetObjectId INT = NULL;
DECLARE @assetTable NVARCHAR(520) = NULL;
DECLARE @licenseObjectId INT = NULL;
DECLARE @licenseTable NVARCHAR(520) = NULL;

SELECT TOP (1)
    @assetObjectId = t.object_id,
    @assetTable = QUOTENAME(s.name) + N'.' + QUOTENAME(t.name)
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
INNER JOIN sys.columns c ON c.object_id = t.object_id
WHERE c.name IN (N'AssetCode', N'AssetName', N'SerialNumber')
GROUP BY t.object_id, s.name, t.name
ORDER BY
    CASE WHEN t.name = N'ITAssets' THEN 0
         WHEN t.name = N'ITAsset' THEN 1
         ELSE 2 END,
    t.name;

IF @assetObjectId IS NOT NULL
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = @assetObjectId AND name = N'ManagerUserId'
    )
        EXEC(N'ALTER TABLE ' + @assetTable + N' ADD [ManagerUserId] BIGINT NULL');

    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = @assetObjectId AND name = N'UsingUserId'
    )
        EXEC(N'ALTER TABLE ' + @assetTable + N' ADD [UsingUserId] BIGINT NULL');
END;

SELECT TOP (1)
    @licenseObjectId = t.object_id,
    @licenseTable = QUOTENAME(s.name) + N'.' + QUOTENAME(t.name)
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
INNER JOIN sys.columns c ON c.object_id = t.object_id
WHERE c.name IN (N'LicenseKey', N'LicenseType', N'ProductName')
GROUP BY t.object_id, s.name, t.name
ORDER BY
    CASE WHEN t.name = N'ITLicenses' THEN 0
         WHEN t.name = N'ITLicense' THEN 1
         WHEN t.name = N'ITAssetLicenses' THEN 2
         ELSE 3 END,
    t.name;

IF @licenseObjectId IS NOT NULL
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = @licenseObjectId AND name = N'ManagerUserId'
    )
        EXEC(N'ALTER TABLE ' + @licenseTable + N' ADD [ManagerUserId] BIGINT NULL');

    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = @licenseObjectId AND name = N'UsingUserId'
    )
        EXEC(N'ALTER TABLE ' + @licenseTable + N' ADD [UsingUserId] BIGINT NULL');
END;
""";

        await dbSql.Database.ExecuteSqlRawAsync(ensureManagerUserColumnsSql);
        app.Logger.LogInformation("IT Asset/License ManagerUserId and UsingUserId SQL Server schema check completed.");
    }
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Failed to ensure IT Asset/License ManagerUserId/UsingUserId SQL Server columns.");
    throw;
}


// IT_ASSET_LICENSE_MANAGER_USER_DEDICATED_ENDPOINTS_V6
app.MapGet("/api/it-assets/assets/{id:long}/users", async (long id, AppDbContext db) =>
{
    var x = await db.Set<ITAsset>().FindAsync(id);
    return x == null
        ? Results.NotFound()
        : Results.Ok(new { managerUserId = x.ManagerUserId, usingUserId = x.UsingUserId });
});

app.MapPut("/api/it-assets/assets/{id:long}/users", async (long id, System.Text.Json.JsonElement body, AppDbContext db) =>
{
    var x = await db.Set<ITAsset>().FindAsync(id);
    if (x == null) return Results.NotFound();

    static long? ReadNullableLong(System.Text.Json.JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var v) || v.ValueKind == System.Text.Json.JsonValueKind.Null)
            return null;
        if (v.ValueKind == System.Text.Json.JsonValueKind.Number && v.TryGetInt64(out var n))
            return n;
        if (v.ValueKind == System.Text.Json.JsonValueKind.String && long.TryParse(v.GetString(), out n))
            return n;
        return null;
    }

    x.ManagerUserId = ReadNullableLong(body, "managerUserId");
    x.UsingUserId = ReadNullableLong(body, "usingUserId");
    await db.SaveChangesAsync();

    return Results.Ok(new { id = x.Id, managerUserId = x.ManagerUserId, usingUserId = x.UsingUserId });
});

app.MapGet("/api/it-assets/licenses/{id:long}/users", async (long id, AppDbContext db) =>
{
    var x = await db.Set<ITLicense>().FindAsync(id);
    return x == null
        ? Results.NotFound()
        : Results.Ok(new { managerUserId = x.ManagerUserId, usingUserId = x.UsingUserId });
});

app.MapPut("/api/it-assets/licenses/{id:long}/users", async (long id, System.Text.Json.JsonElement body, AppDbContext db) =>
{
    var x = await db.Set<ITLicense>().FindAsync(id);
    if (x == null) return Results.NotFound();

    static long? ReadNullableLong(System.Text.Json.JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var v) || v.ValueKind == System.Text.Json.JsonValueKind.Null)
            return null;
        if (v.ValueKind == System.Text.Json.JsonValueKind.Number && v.TryGetInt64(out var n))
            return n;
        if (v.ValueKind == System.Text.Json.JsonValueKind.String && long.TryParse(v.GetString(), out n))
            return n;
        return null;
    }

    x.ManagerUserId = ReadNullableLong(body, "managerUserId");
    x.UsingUserId = ReadNullableLong(body, "usingUserId");
    await db.SaveChangesAsync();

    return Results.Ok(new { id = x.Id, managerUserId = x.ManagerUserId, usingUserId = x.UsingUserId });
});

app.Run();

public sealed record UniversalCodeMergeRequest(string Entity,string SourceCode,string TargetCode);

public sealed record UserHrProfileRequest(string? EmployeeCode,string? Phone,string? PersonalEmail,DateTime? DateOfBirth,string? Gender,string? Address,DateTime? JoinDate,string? EmploymentType,long? ManagerUserId,string? EmergencyContactName,string? EmergencyContactPhone,string? Notes);
