using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MAIPT.PM.Api.Data;
using MAIPT.PM.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MAIPT.PM.Api.Endpoints;

public static class KpiApprovalEndpoints
{
    static bool CanManage(string? role) => AuthService.IsManagerRole(role);
    static List<long> ApproverIds(JsonElement body)
    {
        var ids = new List<long>();
        if (body.TryGetProperty("approverIds", out var a) && a.ValueKind == JsonValueKind.Array)
            foreach (var v in a.EnumerateArray()) if (v.TryGetInt64(out var id)) ids.Add(id);
        return ids.Distinct().ToList();
    }
    static string Text(JsonElement body, string name)
        => body.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "").Trim() : "";

    static string ApprovalLevelName(int stepNo) => stepNo switch
    {
        10 => "Level 1",
        20 => "Project Manager",
        30 => "Level 2",
        40 => "Level 3",
        50 => "Level 4",
        60 => "Level 5",
        70 => "Level 6 / Final",
        _ => $"Level {stepNo}"
    };

    static async Task<string> GraphToken()
    {
        var tenant=Environment.GetEnvironmentVariable("MPMS_M365_TENANT_ID");
        var client=Environment.GetEnvironmentVariable("MPMS_M365_CLIENT_ID");
        var secret=Environment.GetEnvironmentVariable("MPMS_M365_CLIENT_SECRET");
        if(string.IsNullOrWhiteSpace(tenant)||string.IsNullOrWhiteSpace(client)||string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("Microsoft 365 mail environment variables are missing.");
        using var http=new HttpClient();
        using var content=new FormUrlEncodedContent(new Dictionary<string,string>{
            ["client_id"]=client,["client_secret"]=secret,["scope"]="https://graph.microsoft.com/.default",["grant_type"]="client_credentials"});
        using var r=await http.PostAsync($"https://login.microsoftonline.com/{Uri.EscapeDataString(tenant)}/oauth2/v2.0/token",content);
        var json=await r.Content.ReadAsStringAsync();
        if(!r.IsSuccessStatusCode) throw new InvalidOperationException($"Token request failed: {(int)r.StatusCode} {json}");
        using var doc=JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("access_token").GetString() ?? throw new InvalidOperationException("No access token.");
    }

    static async Task SendMail(string to,string subject,string html)
    {
        var from=Environment.GetEnvironmentVariable("MPMS_MAIL_FROM");
        if(string.IsNullOrWhiteSpace(from)) throw new InvalidOperationException("MPMS_MAIL_FROM is missing.");
        var token=await GraphToken();
        using var http=new HttpClient();
        http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
        var payload=new{message=new{subject,body=new{contentType="HTML",content=html},toRecipients=new[]{new{emailAddress=new{address=to}}}},saveToSentItems=true};
        using var r=await http.PostAsJsonAsync($"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(from)}/sendMail",payload);
        if(!r.IsSuccessStatusCode) throw new InvalidOperationException($"sendMail failed for {to}: {(int)r.StatusCode} {await r.Content.ReadAsStringAsync()}");
    }


    static string ApprovalEmailHtml(string recipientName,string title,string code,string department,string summary,string requester,string category,string status,string comment,string detailUrl,string intro)
    {
        static string H(string? v) => WebUtility.HtmlEncode(v ?? "");
        var commentRow=string.IsNullOrWhiteSpace(comment) ? "" : $"<tr><td style='padding:4px 18px 4px 0;font-weight:700'>Comment</td><td style='padding:4px 0'>{H(comment)}</td></tr>";
        return $@"<!doctype html><html><body style='margin:0;background:#fff;font-family:Segoe UI,Arial,sans-serif;color:#111827'>
<div style='padding:20px 14px;max-width:1180px'>
<p style='margin:0 0 34px;color:#4f5bd5;font-size:16px;font-weight:700'>Dear {H(recipientName)},</p>
<p style='margin:0 0 34px;color:#4f5bd5;font-size:16px;line-height:1.45'>Please be informed that an approval <strong style='color:#ef1717'>{H(title)}</strong> {H(intro)}</p>
<p style='margin:0 0 28px;color:#4f5bd5;font-size:16px'>Please click below link for more information.</p>
<p style='margin:0 0 6px'><a href='{H(detailUrl)}' style='color:#0563c1;text-decoration:underline;font-size:16px;font-weight:600'>View Details</a></p>
<table cellpadding='0' cellspacing='0' style='border-collapse:collapse;font-size:16px;line-height:1.25'>
<tr><td style='padding:4px 18px 4px 0;font-weight:700'>Code</td><td style='padding:4px 0;color:#ef1717'>{H(code)}</td></tr>
<tr><td style='padding:4px 18px 4px 0'>Department</td><td style='padding:4px 0'>{H(department)}</td></tr>
<tr><td style='padding:4px 18px 4px 0'>Summary of Proposal</td><td style='padding:4px 0;color:#ef1717'>{H(summary)}</td></tr>
<tr><td style='padding:4px 18px 4px 0'>Requester</td><td style='padding:4px 0'>{H(requester)}</td></tr>
<tr><td style='padding:4px 18px 4px 0'>Category</td><td style='padding:4px 0'>{H(category)}</td></tr>
<tr><td style='padding:4px 18px 4px 0'>Status</td><td style='padding:4px 0;color:#ef1717;font-weight:700'>{H(status)}</td></tr>{commentRow}
</table>
<p style='margin:28px 0 18px;color:#4f5bd5;font-size:16px'>Thanks and Regards.</p>
<p style='margin:0;color:#ef1717;font-size:12px'>This email was sent by <span style='color:#8a258f'>MPMS</span> system.</p>
</div></body></html>";
    }

    public static void MapKpiApprovalEndpoints(this WebApplication app)
    {



        app.MapGet("/api/performance/approval-level-options/{periodId:long}", async (long periodId, HttpContext http, AppDbContext db) =>
        {
            var uid=Convert.ToInt64(http.Items["AuthUserId"]);
            var period=await db.PerformancePeriods.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==periodId);
            if(period is null)return Results.NotFound(new{message="Performance period not found."});

            var owner=period.UserId.HasValue?await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==period.UserId.Value):null;
            var users=await db.Users.AsNoTracking().Where(x=>x.Status=="ACTIVE"&&x.Email!=""&&x.Id!=uid).OrderBy(x=>x.Name).ToListAsync();

            bool IsPm(AppUser x)
            {
                var role=(x.Role??"").Trim().ToUpperInvariant();
                var title=(x.JobTitle??"").Trim().ToUpperInvariant();
                return role is "PM" or "PROJECT_MANAGER" || title.Contains("PROJECT MANAGER");
            }

            var pm=users.Where(IsPm)
                .OrderByDescending(x=>owner!=null&&x.OrgUnitId==owner.OrgUnitId)
                .ThenByDescending(x=>owner!=null&&x.Department==owner.Department)
                .ThenBy(x=>x.Name)
                .FirstOrDefault();

            return Results.Ok(new{
                approvers=users.Select(x=>new{x.Id,x.Name,x.Email,x.Role,x.JobTitle,x.Department,isProjectManager=pm!=null&&x.Id==pm.Id}),
                defaultProjectManagerId=pm?.Id
            });
        });

        app.MapGet("/api/performance/approval-options/{periodId:long}", async (long periodId, HttpContext http, AppDbContext db) =>
        {
            var uid=Convert.ToInt64(http.Items["AuthUserId"]);
            var period=await db.PerformancePeriods.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==periodId);
            if(period is null)return Results.NotFound(new{message="Performance period not found."});
            var owner=period.UserId.HasValue?await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==period.UserId.Value):null;
            var all=await db.Users.AsNoTracking().Where(x=>x.Status=="ACTIVE"&&x.Id!=uid).OrderBy(x=>x.Name).ToListAsync();
            bool IsPm(AppUser x){var r=(x.Role??"").ToUpperInvariant();var t=(x.JobTitle??"").ToUpperInvariant();return r is "PM" or "PROJECT_MANAGER"||t.Contains("PROJECT MANAGER");}
            var pm=all.Where(IsPm).OrderByDescending(x=>owner!=null&&x.OrgUnitId==owner.OrgUnitId).ThenByDescending(x=>owner!=null&&x.Department==owner.Department).ThenBy(x=>x.Name).FirstOrDefault();
            var ordered=all.OrderByDescending(x=>pm!=null&&x.Id==pm.Id).ThenBy(x=>x.Name).Select(x=>new{x.Id,x.Name,x.Email,x.Role,x.JobTitle,x.Department,isProjectManager=pm!=null&&x.Id==pm.Id}).ToList();
            return Results.Ok(new{approvers=ordered,defaultApproverIds=pm is null?Array.Empty<long>():new[]{pm.Id},defaultMode="ALL"});
        });


        app.MapPost("/api/performance/periods/{id:long}/submit-approval-levels", async (long id, JsonElement body, HttpContext http, AppDbContext db) =>
        {
            var uid=Convert.ToInt64(http.Items["AuthUserId"]);
            var canManage=CanManage(Convert.ToString(http.Items["AuthUserRole"]));
            var period=await db.PerformancePeriods.Include(x=>x.Items).FirstOrDefaultAsync(x=>x.Id==id);

            if(period is null)return Results.NotFound(new{message="Performance period not found."});
            if(!canManage&&period.UserId!=uid)return Results.StatusCode(403);
            if(period.Status=="APPROVED")return Results.BadRequest(new{message="Approved KPI cannot be submitted again."});
            if(period.Status=="SUBMITTED")return Results.BadRequest(new{message="KPI is already pending approval."});
            if(period.Items.Count==0)return Results.BadRequest(new{message="Add at least one KPI item before submitting."});
            if(!body.TryGetProperty("levels",out var levels)||levels.ValueKind!=JsonValueKind.Object)return Results.BadRequest(new{message="Approval levels are missing."});

            long? One(string name)
            {
                if(!levels.TryGetProperty(name,out var n))return null;
                if(n.ValueKind==JsonValueKind.Number&&n.TryGetInt64(out var v)&&v>0&&v!=uid)return v;
                return null;
            }

            var rows=new List<(int StepNo,long UserId,string Label)>();
            void Add(int step,long? id,string label){if(id.HasValue)rows.Add((step,id.Value,label));}

            Add(10,One("level1"),"Level 1");
            Add(20,One("projectManager"),"Project Manager");
            Add(30,One("level2"),"Level 2");
            Add(40,One("level3"),"Level 3");

            if(levels.TryGetProperty("level4",out var l4)&&l4.ValueKind==JsonValueKind.Array)
                foreach(var n in l4.EnumerateArray())
                    if(n.TryGetInt64(out var v)&&v>0&&v!=uid&&!rows.Any(x=>x.UserId==v))
                        rows.Add((50,v,"Level 4"));

            Add(60,One("level5"),"Level 5");
            Add(70,One("level6"),"Final Approver");

            if(rows.Count==0)return Results.BadRequest(new{message="Select at least one approver."});
            if(rows.GroupBy(x=>x.UserId).Any(g=>g.Count()>1))return Results.BadRequest(new{message="The same person cannot be selected at multiple approval levels."});

            var ids=rows.Select(x=>x.UserId).Distinct().ToList();
            var approvers=await db.Users.Where(x=>ids.Contains(x.Id)&&x.Status=="ACTIVE"&&x.Email!="").ToListAsync();
            if(approvers.Count!=ids.Count)return Results.BadRequest(new{message="One or more selected approvers are invalid, inactive, or have no email."});

            var old=await db.ApprovalRequests.Include(x=>x.Steps).Where(x=>x.EntityType=="PERFORMANCE_PERIOD"&&x.EntityId==id&&x.Status=="PENDING").ToListAsync();
            if(old.Count>0)db.ApprovalRequests.RemoveRange(old);

            var firstLevel=rows.Min(x=>x.StepNo);
            var req=new ApprovalRequest{EntityType="PERFORMANCE_PERIOD",EntityId=id,WorkflowType="KPI_LEVELS",RequestedBy=uid,RequestedAt=DateTime.UtcNow,Status="PENDING"};
            foreach(var r in rows)req.Steps.Add(new ApprovalStep{StepNo=r.StepNo,ApproverId=r.UserId,Status=r.StepNo==firstLevel?"PENDING":"WAITING",Comment=""});

            db.ApprovalRequests.Add(req);
            period.Status="SUBMITTED";
            period.IsLocked=true;
            period.LockedAt=DateTime.UtcNow;

            var firstApproverIds=rows.Where(x=>x.StepNo==firstLevel).Select(x=>x.UserId).Distinct().ToHashSet();
            foreach(var a in approvers.Where(x=>firstApproverIds.Contains(x.Id)))
                db.Notifications.Add(new NotificationRecord{
                    UserId=a.Id,Type="APPROVAL",Severity="INFO",
                    Title=$"KPI approval required - {period.EmployeeName}",
                    Message=$"{period.EmployeeName} submitted KPI {period.Period} for your approval.",
                    EntityType="PERFORMANCE_PERIOD",EntityId=period.Id,IsRead=false
                });

            await db.SaveChangesAsync();

            var publicUrl=(Environment.GetEnvironmentVariable("MPMS_PUBLIC_URL")??"https://project.maipt.org").TrimEnd('/');
            var msg=Text(body,"message");
            var warnings=new List<string>();
            var requester=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid);
            var detailUrl=$"{publicUrl}/?view=approvals&approvalId={req.Id}&entityType=PERFORMANCE_PERIOD&entityId={period.Id}";
            var approvalCode=$"KPI-{req.Id:0000}-{DateTime.UtcNow:yyyy}";
            var summary=$"Team Performance KPI / {period.EmployeeName} / {period.Period}";

            // Sequential workflow: only the first selected level receives the
            // initial email. All approvers at that level receive it together.
            foreach(var a in approvers.Where(x=>firstApproverIds.Contains(x.Id)))
            {
                try
                {
                    var html=ApprovalEmailHtml(a.Name,summary,approvalCode,period.Department??"",summary,requester?.Name??period.EmployeeName,"Team Performance / KPI","Pending Approval",msg,detailUrl,"is waiting for your approval.");
                    await SendMail(a.Email,$"[MPMS] Approval Required - {approvalCode} - {period.EmployeeName}",html);
                }
                catch(Exception ex){warnings.Add($"{a.Email}: {ex.Message}");}
            }

            return Results.Ok(new{period.Id,period.Status,approvalRequestId=req.Id,workflowType=req.WorkflowType,steps=rows.Select(x=>new{x.StepNo,x.UserId,x.Label}),currentLevel=firstLevel,mailSent=firstApproverIds.Count-warnings.Count,mailWarnings=warnings});
        });

        app.MapPost("/api/performance/periods/{id:long}/submit-approval-v2", async (long id, System.Text.Json.JsonElement body, HttpContext http, AppDbContext db) =>
        {
            var uid=Convert.ToInt64(http.Items["AuthUserId"]);
            var period=await db.PerformancePeriods.FirstOrDefaultAsync(x=>x.Id==id);
            if(period is null)return Results.NotFound(new{message="Performance period not found."});

            var totalWeightRaw=await db.PerformanceItems
                .Where(x=>x.PerformancePeriodId==period.Id)
                .SumAsync(x=>(double?)x.Weight) ?? 0d;

            var totalWeightPercent=totalWeightRaw<=1.000001d
                ? totalWeightRaw*100d
                : totalWeightRaw;

            if(Math.Abs(totalWeightPercent-100d)>0.01d)
                return Results.BadRequest(new{
                    message=$"Total Weight is {totalWeightPercent:0.##}%. It must equal 100%. Please check KPI weights before submitting."
                });
            if(period.IsLocked)return Results.BadRequest(new{message="Locked KPI period cannot be submitted."});
            var mode=body.TryGetProperty("mode",out var mn)?(mn.GetString()??"ALL").ToUpperInvariant():"ALL";
            if(mode is not ("ANY" or "ALL" or "SEQUENTIAL"))return Results.BadRequest(new{message="Invalid approval mode."});
            var ids=body.TryGetProperty("approverIds",out var an)&&an.ValueKind==System.Text.Json.JsonValueKind.Array?an.EnumerateArray().Where(x=>x.TryGetInt64(out _)).Select(x=>x.GetInt64()).Distinct().Where(x=>x!=uid).ToArray():Array.Empty<long>();
            if(ids.Length==0)return Results.BadRequest(new{message="Select at least one approver."});
            var approvers=await db.Users.Where(x=>ids.Contains(x.Id)&&x.Status=="ACTIVE").ToListAsync();
            if(approvers.Count!=ids.Length)return Results.BadRequest(new{message="Invalid or inactive approver."});
            if(await db.ApprovalRequests.AnyAsync(x=>x.EntityType=="PERFORMANCE_PERIOD"&&x.EntityId==id&&x.Status=="PENDING"))return Results.BadRequest(new{message="This KPI already has a pending approval request."});
            var req=new ApprovalRequest{EntityType="PERFORMANCE_PERIOD",EntityId=id,WorkflowType=$"KPI_MULTI_{mode}",RequestedBy=uid,RequestedAt=DateTime.UtcNow,Status="PENDING"};
            for(var i=0;i<ids.Length;i++)req.Steps.Add(new ApprovalStep{StepNo=mode=="SEQUENTIAL"?i+1:1,ApproverId=ids[i],Status="PENDING"});
            db.ApprovalRequests.Add(req);period.Status="SUBMITTED";await db.SaveChangesAsync();
            var notifyIds=mode=="SEQUENTIAL"
                ? req.Steps.Where(x=>x.StepNo==req.Steps.Min(z=>z.StepNo)).Select(x=>x.ApproverId).ToHashSet()
                : approvers.Select(x=>x.Id).ToHashSet();
            foreach(var a in approvers.Where(x=>notifyIds.Contains(x.Id)))
                db.Notifications.Add(new NotificationRecord{
                    UserId=a.Id,Type="APPROVAL",Severity="INFO",
                    Title="KPI approval required",
                    Message=$"{period.EmployeeName} / {period.Period}",
                    EntityType="APPROVAL_REQUEST",EntityId=req.Id,IsRead=false
                });
            await db.SaveChangesAsync();
            return Results.Ok(new{period.Id,period.Status,approvalRequestId=req.Id,approvalMode=mode,approvers=approvers.Select(x=>new{x.Id,x.Name,x.Email})});
        });


        app.MapGet("/api/performance/approvers", async (AppDbContext db) =>
            Results.Ok(await db.Users.AsNoTracking().Where(x=>x.Status=="ACTIVE"&&x.Email!=""&&
                (x.Role=="ADMIN"||x.Role=="CIO"||x.Role=="HOD"||x.Role=="PM"||x.Role=="PROJECT_MANAGER"))
                .OrderBy(x=>x.Name).Select(x=>new{x.Id,x.Name,x.Email,x.JobTitle,x.Department,x.Role}).ToListAsync()));

        app.MapGet("/api/budget-plan/approval-options", async (HttpContext http,AppDbContext db) =>
        {
            var uid=Convert.ToInt64(http.Items["AuthUserId"]);
            return Results.Ok(await db.Users.AsNoTracking()
                .Where(x=>x.Status=="ACTIVE"&&x.Email!=""&&x.Id!=uid)
                .OrderBy(x=>x.Name)
                .Select(x=>new{x.Id,x.Name,x.Email,x.JobTitle,x.Department,x.Role})
                .ToListAsync());
        });

        // Submit all Budget Plan Items in one Business Unit + Budget Year for approval.
        app.MapPost("/api/budget-plan/submit", async (JsonElement body,HttpContext http,AppDbContext db) =>
        {
            var uid=Convert.ToInt64(http.Items["AuthUserId"]);
            var user=await db.Users.Include(x=>x.OrgUnit).FirstOrDefaultAsync(x=>x.Id==uid);
            if(user is null)return Results.Unauthorized();
            if(!body.TryGetProperty("budgetYear",out var yn)||!yn.TryGetInt32(out var budgetYear))
                return Results.BadRequest(new{message="Select a Budget Year."});
            var orgUnit=Text(body,"orgUnit");
            if(string.IsNullOrWhiteSpace(orgUnit)||orgUnit=="ALL")
                return Results.BadRequest(new{message="Select one Business Unit."});
            if(!await RbacService.CanBudgetOrg(db,user,orgUnit))return Results.StatusCode(403);

            var items=await db.BudgetPlanItems.Where(x=>x.BudgetYear==budgetYear&&x.OrgUnit==orgUnit).OrderBy(x=>x.Id).ToListAsync();
            if(items.Count==0)return Results.BadRequest(new{message=$"No Plan/Budget items exist for {orgUnit} / {budgetYear}."});
            if(items.Any(x=>x.Status=="APPROVED"))return Results.Ok(new{alreadyApproved=true,message="This Plan/Budget has already been approved.",budgetYear,orgUnit,status="APPROVED"});
            if(items.Any(x=>x.Status=="SUBMITTED"))return Results.Ok(new{alreadySubmitted=true,message="This Plan/Budget has already been submitted and is pending approval.",budgetYear,orgUnit,status="PENDING"});

            if(!body.TryGetProperty("levels",out var levels)||levels.ValueKind!=JsonValueKind.Object)
                return Results.BadRequest(new{message="Approval levels are missing."});
            long? One(string name){if(!levels.TryGetProperty(name,out var n))return null;return n.ValueKind==JsonValueKind.Number&&n.TryGetInt64(out var v)&&v>0&&v!=uid?v:null;}
            var rows=new List<(int StepNo,long UserId,string Label)>();
            void Add(int step,long? id,string label){if(id.HasValue)rows.Add((step,id.Value,label));}
            Add(10,One("level1"),"Level 1");Add(20,One("projectManager"),"Project Manager");Add(30,One("level2"),"Level 2");Add(40,One("level3"),"Level 3");
            if(levels.TryGetProperty("level4",out var l4)&&l4.ValueKind==JsonValueKind.Array)foreach(var n in l4.EnumerateArray())if(n.TryGetInt64(out var v)&&v>0&&v!=uid)rows.Add((50,v,"Level 4"));
            Add(60,One("level5"),"Level 5");Add(70,One("level6"),"Level 6 / Final");
            if(rows.Count==0)return Results.BadRequest(new{message="Select at least one approver other than yourself."});
            if(rows.GroupBy(x=>x.UserId).Any(g=>g.Count()>1))return Results.BadRequest(new{message="The same person cannot be selected at multiple approval levels."});
            var ids=rows.Select(x=>x.UserId).Distinct().ToList();
            var approvers=await db.Users.Where(x=>ids.Contains(x.Id)&&x.Status=="ACTIVE"&&x.Email!="").ToListAsync();
            if(approvers.Count!=ids.Count)return Results.BadRequest(new{message="One or more approvers are invalid, inactive, or have no email."});

            var firstStep=rows.Min(x=>x.StepNo);
            var req=new ApprovalRequest{EntityType="BUDGET_PLAN",EntityId=items[0].Id,WorkflowType="BUDGET_PLAN_LEVELS",RequestedBy=uid,RequestedAt=DateTime.UtcNow,Status="PENDING"};
            foreach(var row in rows)req.Steps.Add(new ApprovalStep{StepNo=row.StepNo,ApproverId=row.UserId,Status=row.StepNo==firstStep?"PENDING":"WAITING",Comment=""});
            db.ApprovalRequests.Add(req);
            foreach(var item in items){item.Status="SUBMITTED";item.UpdatedByUserId=uid;item.UpdatedAt=DateTime.UtcNow;}
            var firstIds=rows.Where(x=>x.StepNo==firstStep).Select(x=>x.UserId).ToHashSet();
            foreach(var a in approvers.Where(x=>firstIds.Contains(x.Id)))db.Notifications.Add(new NotificationRecord{UserId=a.Id,Type="APPROVAL",Severity="INFO",Title=$"Plan/Budget approval required - {orgUnit}",Message=$"{user.Name} submitted Plan/Budget {orgUnit} / {budgetYear} for approval.",EntityType="BUDGET_PLAN",EntityId=items[0].Id,IsRead=false});
            await db.SaveChangesAsync();

            var publicUrl=(Environment.GetEnvironmentVariable("MPMS_PUBLIC_URL")??"https://project.maipt.org").TrimEnd('/');
            var warnings=new List<string>();
            var detailUrl=$"{publicUrl}/?view=approvals&approvalId={req.Id}&entityType=BUDGET_PLAN&entityId={items[0].Id}";
            var code=$"BUD-{req.Id:0000}-{budgetYear}";
            foreach(var a in approvers.Where(x=>firstIds.Contains(x.Id)))try{
                var html=ApprovalEmailHtml(a.Name,$"Plan/Budget {orgUnit} / {budgetYear}",code,orgUnit,$"{items.Count} items / total {items.Sum(x=>x.PlannedAmount):N0}",user.Name,"Annual Work & Budget Plan","Pending Approval",Text(body,"message"),detailUrl,"is waiting for your approval.");
                await SendMail(a.Email,$"[MPMS] Approval Required - {code} - {orgUnit}",html);
            }catch(Exception ex){warnings.Add($"{a.Email}: {ex.Message}");}
            if(warnings.Count>0)
            {
                db.Notifications.Add(new NotificationRecord{UserId=uid,Type="SYSTEM",Severity="WARNING",Title=$"Plan/Budget email was not sent - {orgUnit} / {budgetYear}",Message=string.Join(" | ",warnings),EntityType="APPROVAL_REQUEST",EntityId=req.Id,IsRead=false});
                await db.SaveChangesAsync();
            }
            return Results.Ok(new{approvalRequestId=req.Id,budgetYear,orgUnit,itemCount=items.Count,status="PENDING",currentLevel=firstStep,mailSent=firstIds.Count-warnings.Count,mailWarnings=warnings});
        });

        // Recall a pending Plan/Budget submission so its owner can edit and submit it again.
        app.MapPost("/api/budget-plan/recall", async (JsonElement body,HttpContext http,AppDbContext db) =>
        {
            var uid=Convert.ToInt64(http.Items["AuthUserId"]);
            var user=await db.Users.Include(x=>x.OrgUnit).FirstOrDefaultAsync(x=>x.Id==uid);
            if(user is null)return Results.Unauthorized();
            if(!body.TryGetProperty("budgetYear",out var yn)||!yn.TryGetInt32(out var budgetYear))
                return Results.BadRequest(new{message="Select a Budget Year."});
            var orgUnit=Text(body,"orgUnit");
            if(string.IsNullOrWhiteSpace(orgUnit)||orgUnit=="ALL")
                return Results.BadRequest(new{message="Select one Business Unit."});
            if(!await RbacService.CanBudgetOrg(db,user,orgUnit))return Results.StatusCode(403);

            var items=await db.BudgetPlanItems.Where(x=>x.BudgetYear==budgetYear&&x.OrgUnit==orgUnit).ToListAsync();
            if(items.Count==0)return Results.BadRequest(new{message=$"No Plan/Budget items exist for {orgUnit} / {budgetYear}."});
            var itemIds=items.Select(x=>x.Id).ToList();
            var requests=await db.ApprovalRequests.Include(x=>x.Steps)
                .Where(x=>x.EntityType=="BUDGET_PLAN"&&itemIds.Contains(x.EntityId)&&x.Status=="PENDING"&&
                    (x.WorkflowType=="BUDGET_PLAN_APPROVAL"||x.WorkflowType=="BUDGET_PLAN_LEVELS"))
                .ToListAsync();
            if(requests.Count==0)return Results.Ok(new{alreadyRecalled=true,message="This Plan/Budget is already editable; there is no pending submission to recall.",budgetYear,orgUnit,status=items.First().Status});

            var role=(Convert.ToString(http.Items["AuthUserRole"])??"").Trim().ToUpperInvariant();
            var isRoot=role=="ROOT"||string.Equals(user.Name,"root",StringComparison.OrdinalIgnoreCase)||string.Equals(user.Email,"root",StringComparison.OrdinalIgnoreCase);
            var canFull=await RbacService.CanAsync(db,user,"BUDGET","FULL");
            if(!isRoot&&!canFull&&requests.Any(x=>x.RequestedBy!=uid))return Results.StatusCode(403);

            var now=DateTime.UtcNow;
            foreach(var request in requests)
            {
                request.Status="RECALLED";
                request.CompletedAt=now;
                foreach(var step in request.Steps.Where(x=>x.Status=="PENDING"))
                {
                    step.Status="RECALLED";
                    step.ActedAt=now;
                    step.Comment="Recalled by requester for adjustment.";
                }
            }
            foreach(var item in items){item.Status="PLANNED";item.UpdatedByUserId=uid;item.UpdatedAt=now;}
            await db.SaveChangesAsync();
            return Results.Ok(new{recalled=true,message="Plan/Budget submission recalled. You can edit it and submit again.",budgetYear,orgUnit,status="PLANNED"});
        });

        app.MapPost("/api/performance/periods/{id:long}/submit", async (long id,JsonElement body,HttpContext http,AppDbContext db) =>
        {
            var uid=Convert.ToInt64(http.Items["AuthUserId"]);
            var canManage=CanManage(Convert.ToString(http.Items["AuthUserRole"]));
            var period=await db.PerformancePeriods.Include(x=>x.Items).FirstOrDefaultAsync(x=>x.Id==id);
            if(period is null) return Results.NotFound();

            var totalWeightRaw=await db.PerformanceItems
                .Where(x=>x.PerformancePeriodId==period.Id)
                .SumAsync(x=>(double?)x.Weight) ?? 0d;

            var totalWeightPercent=totalWeightRaw<=1.000001d
                ? totalWeightRaw*100d
                : totalWeightRaw;

            if(Math.Abs(totalWeightPercent-100d)>0.01d)
                return Results.BadRequest(new{
                    message=$"Total Weight is {totalWeightPercent:0.##}%. It must equal 100%. Please check KPI weights before submitting."
                });
            if(!canManage&&period.UserId!=uid) return Results.StatusCode(403);
            if(period.Status=="APPROVED") return Results.BadRequest(new{message="Approved KPI cannot be submitted again."});
            if(period.Status=="SUBMITTED") return Results.BadRequest(new{message="KPI is already pending approval."});
            if(period.Items.Count==0) return Results.BadRequest(new{message="Add at least one KPI item before submitting."});
            var ids=ApproverIds(body).Where(x=>x!=uid).Distinct().ToList();
            if(ids.Count==0) return Results.BadRequest(new{message="Select at least one approver other than yourself."});
            var approvers=await db.Users.Where(x=>ids.Contains(x.Id)&&x.Status=="ACTIVE"&&x.Email!="").ToListAsync();
            if(approvers.Count!=ids.Count) return Results.BadRequest(new{message="One or more approvers are invalid or have no email."});

            var old=await db.ApprovalRequests.Include(x=>x.Steps).Where(x=>x.EntityType=="PERFORMANCE_PERIOD"&&x.EntityId==id&&x.Status=="PENDING").ToListAsync();
            if(old.Count>0) db.ApprovalRequests.RemoveRange(old);
            var req=new ApprovalRequest{EntityType="PERFORMANCE_PERIOD",EntityId=id,WorkflowType="KPI_APPROVAL",RequestedBy=uid,RequestedAt=DateTime.UtcNow,Status="PENDING"};
            foreach(var a in approvers){
                req.Steps.Add(new ApprovalStep{StepNo=1,ApproverId=a.Id,Status="PENDING",Comment=""});
                db.Notifications.Add(new NotificationRecord{UserId=a.Id,Type="APPROVAL",Severity="INFO",Title=$"KPI approval required - {period.EmployeeName}",Message=$"{period.EmployeeName} submitted KPI {period.Period} for your approval.",EntityType="PERFORMANCE_PERIOD",EntityId=period.Id,IsRead=false});
            }
            db.ApprovalRequests.Add(req);
            period.Status="SUBMITTED"; period.IsLocked=true; period.LockedAt=DateTime.UtcNow;
            await db.SaveChangesAsync();

            var publicUrl=(Environment.GetEnvironmentVariable("MPMS_PUBLIC_URL") ?? "https://project.maipt.org").TrimEnd('/');
            var msg=Text(body,"message"); var warnings=new List<string>();
            var requester=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid);
            var detailUrl=$"{publicUrl}/?view=approvals&approvalId={req.Id}&entityType=PERFORMANCE_PERIOD&entityId={period.Id}";
            var approvalCode=$"KPI-{req.Id:0000}-{DateTime.UtcNow:yyyy}";
            var summary=$"Team Performance KPI / {period.EmployeeName} / {period.Period}";
            foreach(var a in approvers){
                try{
                    var html=ApprovalEmailHtml(a.Name,summary,approvalCode,period.Department ?? "",summary,requester?.Name ?? period.EmployeeName,"Team Performance / KPI","Pending Approval",msg,detailUrl,"is waiting for your approval.");
                    await SendMail(a.Email,$"[MPMS] Approval Required - {approvalCode} - {period.EmployeeName}",html);
                }catch(Exception ex){warnings.Add($"{a.Email}: {ex.Message}");}
            }
            return Results.Ok(new{period.Id,period.Status,approvalRequestId=req.Id,mailSent=approvers.Count-warnings.Count,mailWarnings=warnings});
        });

        app.MapGet("/api/kpi-approvals", async (HttpContext http,AppDbContext db) =>
        {
            var uid=Convert.ToInt64(http.Items["AuthUserId"]);
            var role=(Convert.ToString(http.Items["AuthUserRole"])??"").Trim().ToUpperInvariant();
            var isRoot=role=="ROOT";
            var q=db.ApprovalRequests.AsNoTracking()
                .Include(x=>x.Steps).ThenInclude(x=>x.Approver)
                .Where(x=>x.WorkflowType=="KPI_APPROVAL"||x.WorkflowType=="KPI_LEVELS"||x.WorkflowType.StartsWith("KPI_MULTI_")||x.WorkflowType=="BUDGET_PLAN_APPROVAL"||x.WorkflowType=="BUDGET_PLAN_LEVELS")
                .AsQueryable();
            if(!isRoot)
                q=q.Where(x=>x.RequestedBy==uid||x.Steps.Any(s=>s.ApproverId==uid));
            var rows=await q.OrderByDescending(x=>x.RequestedAt).ToListAsync();
            var kpiIds=rows.Where(x=>!x.WorkflowType.StartsWith("BUDGET_PLAN_")).Select(x=>x.EntityId).Distinct().ToList();
            var budgetIds=rows.Where(x=>x.WorkflowType.StartsWith("BUDGET_PLAN_")).Select(x=>x.EntityId).Distinct().ToList();
            var periods=await db.PerformancePeriods.AsNoTracking().Where(x=>kpiIds.Contains(x.Id)).ToDictionaryAsync(x=>x.Id);
            var budgets=await db.BudgetPlanItems.AsNoTracking().Where(x=>budgetIds.Contains(x.Id)).ToDictionaryAsync(x=>x.Id);
            return Results.Ok(rows.Select(x=>{periods.TryGetValue(x.EntityId,out var p);budgets.TryGetValue(x.EntityId,out var b);var isBudget=x.WorkflowType.StartsWith("BUDGET_PLAN_");return new{x.Id,x.EntityType,x.EntityId,x.WorkflowType,x.Status,x.RequestedAt,x.RequestedBy,ApprovalType=isBudget?"BUDGET_PLAN":"KPI",EmployeeName=isBudget?$"Plan/Budget - {b?.OrgUnit}":p?.EmployeeName,Period=isBudget?b?.BudgetYear.ToString():p?.Period,Department=isBudget?b?.OrgUnit:p?.Department,Level=isBudget?"BUDGET_PLAN":p?.Level,CanAct=x.Status=="PENDING"&&(
    (x.WorkflowType=="KPI_LEVELS"||x.WorkflowType=="BUDGET_PLAN_LEVELS"||x.WorkflowType.EndsWith("_SEQUENTIAL"))
        ? x.Steps.Any(z=>z.ApproverId==uid&&z.Status=="PENDING"&&z.StepNo==x.Steps.Where(k=>k.Status=="PENDING").Min(k=>k.StepNo))
        : x.Steps.Any(z=>z.ApproverId==uid&&z.Status=="PENDING")
),Steps=x.Steps.OrderBy(z=>z.Id).Select(z=>new{z.Id,z.StepNo,z.Status,z.Comment,z.ApproverId,Approver=z.Approver!=null?z.Approver.Name:""})};}));
        });


        // KPI_APPROVER_REVIEW_SCORE_V2
        app.MapGet("/api/kpi-approvals/{id:long}/review", async (long id,HttpContext http,AppDbContext db) =>
        {
            var uid=Convert.ToInt64(http.Items["AuthUserId"]);

            var req=await db.ApprovalRequests.AsNoTracking()
                .Include(x=>x.Steps)
                .FirstOrDefaultAsync(x=>x.Id==id &&
                    (x.WorkflowType=="KPI_APPROVAL" ||
                     x.WorkflowType=="KPI_LEVELS" ||
                     x.WorkflowType.StartsWith("KPI_MULTI_") ||
                     x.WorkflowType=="BUDGET_PLAN_APPROVAL" ||
                     x.WorkflowType=="BUDGET_PLAN_LEVELS"));

            if(req is null)return Results.NotFound(new{message="KPI approval request not found."});

            var pendingSteps=req.Steps.Where(x=>x.Status=="PENDING").ToList();
            var currentLevel=pendingSteps.Select(x=>x.StepNo).DefaultIfEmpty(-1).Min();

            var sequential=req.WorkflowType=="KPI_LEVELS" || req.WorkflowType=="BUDGET_PLAN_LEVELS" || req.WorkflowType.EndsWith("_SEQUENTIAL");

            var myStep=req.Steps.FirstOrDefault(x=>x.ApproverId==uid&&x.Status=="PENDING" &&
                (!sequential || x.StepNo==currentLevel));

            var canAct=req.Status=="PENDING" && myStep!=null;
            var viewerRole=(Convert.ToString(http.Items["AuthUserRole"])??"").Trim().ToUpperInvariant();
            var isRootViewer=viewerRole=="ROOT";
            // An assigned approver keeps read-only access after their step or
            // the whole request is completed. Only canAct is restricted to the
            // current pending level.
            var isAssignedApprover=req.Steps.Any(x=>x.ApproverId==uid);
            var canView=isAssignedApprover || req.RequestedBy==uid || isRootViewer;

            if(!canView)
                return Results.Json(
                    new{message="You are not the requester or an assigned approver for this request."},
                    statusCode:StatusCodes.Status403Forbidden);

            if(req.WorkflowType=="BUDGET_PLAN_APPROVAL"||req.WorkflowType=="BUDGET_PLAN_LEVELS")
            {
                var anchor=await db.BudgetPlanItems.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==req.EntityId);
                if(anchor is null)return Results.NotFound(new{message="Plan/Budget submission not found."});
                var budgetItems=await db.BudgetPlanItems.AsNoTracking()
                    .Where(x=>x.BudgetYear==anchor.BudgetYear&&x.OrgUnit==anchor.OrgUnit)
                    .OrderBy(x=>x.PlannedMonth).ThenBy(x=>x.Name).ThenBy(x=>x.Id)
                    .ToListAsync();
                var projectIds=budgetItems.Where(x=>x.ProjectId.HasValue).Select(x=>x.ProjectId!.Value).Distinct().ToList();
                var projectNames=await db.Projects.AsNoTracking().Where(x=>projectIds.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,x=>x.Name);
                return Results.Ok(new{
                    approvalType="BUDGET_PLAN",
                    request=new{req.Id,req.Status,req.WorkflowType,req.RequestedBy,req.EntityId,CanAct=canAct,CurrentLevel=currentLevel,MyStepId=myStep!=null?myStep.Id:(long?)null,MyStepNo=myStep!=null?myStep.StepNo:(int?)null},
                    period=new{Id=anchor.Id,Period=anchor.BudgetYear.ToString(),EmployeeName=$"Plan/Budget - {anchor.OrgUnit}",Department=anchor.OrgUnit,Status=req.Status,IsLocked=req.Status=="PENDING"||req.Status=="APPROVED"},
                    scoreField="NONE",
                    summary=new{budgetYear=anchor.BudgetYear,orgUnit=anchor.OrgUnit,itemCount=budgetItems.Count,totalPlanned=budgetItems.Sum(x=>x.PlannedAmount)},
                    items=budgetItems.Select(x=>new{x.Id,x.Name,x.Category,x.Vendor,x.PlannedAmount,x.Quantity,x.UnitPrice,x.PlannedMonth,x.Note,x.Status,x.ProjectId,Project=x.ProjectId.HasValue&&projectNames.TryGetValue(x.ProjectId.Value,out var pn)?pn:""})
                });
            }

            var period=await db.PerformancePeriods.AsNoTracking()
                .FirstOrDefaultAsync(x=>x.Id==req.EntityId);
            if(period is null)return Results.NotFound(new{message="Performance period not found."});

            var items=await db.PerformanceItems.AsNoTracking()
                .Where(x=>x.PerformancePeriodId==period.Id)
                .OrderBy(x=>x.SourceRow).ThenBy(x=>x.Id)
                .ToListAsync();

            var me=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid);
            var role=(me?.Role??"").Trim().ToUpperInvariant();
            var title=(me?.JobTitle??"").Trim().ToUpperInvariant();

            var highestStep=req.Steps.Select(x=>x.StepNo).DefaultIfEmpty(1).Max();
            var useHod=role is "HOD" or "CIO" or "ADMIN" ||
                       title.Contains("HEAD") || title.Contains("DIRECTOR") || title.Contains("CIO") ||
                       (myStep!=null && myStep.StepNo==highestStep);

            return Results.Ok(new{
                request=new{
                    req.Id,req.Status,req.WorkflowType,req.RequestedBy,
                    req.EntityId,CanAct=canAct,
                    CurrentLevel=currentLevel,
                    MyStepId=myStep!=null?myStep.Id:(long?)null,
                    MyStepNo=myStep!=null?myStep.StepNo:(int?)null,
                    CurrentApprovers=req.Steps
                        .Where(z=>z.Status=="PENDING" && (!sequential || z.StepNo==currentLevel))
                        .Select(z=>new{z.ApproverId,z.StepNo})
                        .ToList()
                },
                period=new{
                    period.Id,period.Period,period.EmployeeName,period.Department,
                    period.Status,period.IsLocked
                },
                scoreField=useHod?"HOD":"MANAGER",
                items=items.Select(x=>new{
                    x.Id,x.ProjectId,x.Strategy,x.Function,x.Plan,x.Actual,x.Weight,
                    x.SelfScore,x.ManagerScore,x.HodScore,x.FinalScore,
                    x.NextPlan,x.Note,x.Status
                })
            });
        });

        app.MapPost("/api/kpi-approvals/{id:long}/scores", async (long id,JsonElement body,HttpContext http,AppDbContext db) =>
        {
            var uid=Convert.ToInt64(http.Items["AuthUserId"]);

            var req=await db.ApprovalRequests
                .Include(x=>x.Steps)
                .FirstOrDefaultAsync(x=>x.Id==id &&
                    (x.WorkflowType=="KPI_APPROVAL" ||
                     x.WorkflowType=="KPI_LEVELS" ||
                     x.WorkflowType.StartsWith("KPI_MULTI_")));

            if(req is null)return Results.NotFound(new{message="KPI approval request not found."});
            if(req.Status!="PENDING")
                return Results.BadRequest(new{message="Approved or rejected KPI is final and cannot be edited."});

            var pendingSteps=req.Steps.Where(x=>x.Status=="PENDING").ToList();
            var currentLevel=pendingSteps.Select(x=>x.StepNo).DefaultIfEmpty(-1).Min();
            var sequential=req.WorkflowType=="KPI_LEVELS" || req.WorkflowType.EndsWith("_SEQUENTIAL");

            var myStep=req.Steps.FirstOrDefault(x=>x.ApproverId==uid&&x.Status=="PENDING" &&
                (!sequential || x.StepNo==currentLevel));

            if(myStep is null)return Results.StatusCode(StatusCodes.Status403Forbidden);

            if(!body.TryGetProperty("items",out var arr) || arr.ValueKind!=JsonValueKind.Array)
                return Results.BadRequest(new{message="Score items are required."});

            var me=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid);
            var role=(me?.Role??"").Trim().ToUpperInvariant();
            var title=(me?.JobTitle??"").Trim().ToUpperInvariant();

            var highestStep=req.Steps.Select(x=>x.StepNo).DefaultIfEmpty(1).Max();
            var useHod=role is "HOD" or "CIO" or "ADMIN" ||
                       title.Contains("HEAD") || title.Contains("DIRECTOR") || title.Contains("CIO") ||
                       myStep.StepNo==highestStep;

            var period=await db.PerformancePeriods.FirstOrDefaultAsync(x=>x.Id==req.EntityId);
            if(period is null)return Results.NotFound(new{message="Performance period not found."});

            var ids=arr.EnumerateArray()
                .Where(x=>x.TryGetProperty("id",out var n)&&n.TryGetInt64(out _))
                .Select(x=>x.GetProperty("id").GetInt64())
                .Distinct().ToArray();

            var dbItems=await db.PerformanceItems
                .Where(x=>x.PerformancePeriodId==period.Id&&ids.Contains(x.Id))
                .ToDictionaryAsync(x=>x.Id);

            var updated=0;
            foreach(var row in arr.EnumerateArray())
            {
                if(!row.TryGetProperty("id",out var idn)||!idn.TryGetInt64(out var itemId))
                    continue;
                if(!dbItems.TryGetValue(itemId,out var item))
                    continue;
                if(!row.TryGetProperty("score",out var sn)||!sn.TryGetDouble(out var score))
                    return Results.BadRequest(new{message=$"Score is required for KPI item {itemId}."});

                score=Math.Clamp(score,0,5);

                if(useHod)item.HodScore=score;
                else item.ManagerScore=score;

                item.FinalScore=item.HodScore>0
                    ? item.HodScore
                    : item.ManagerScore>0
                        ? item.ManagerScore
                        : item.SelfScore;

                item.UpdatedAt=DateTime.UtcNow;
                updated++;
            }

            await db.SaveChangesAsync();
            return Results.Ok(new{updated,scoreField=useHod?"HOD":"MANAGER"});
        });

        app.MapPost("/api/kpi-approvals/{id:long}/resend-email", async (long id,HttpContext http,AppDbContext db) =>
        {
            var uid=Convert.ToInt64(http.Items["AuthUserId"]);
            var role=(Convert.ToString(http.Items["AuthUserRole"])??"").Trim().ToUpperInvariant();
            var req=await db.ApprovalRequests.AsNoTracking().Include(x=>x.Steps).FirstOrDefaultAsync(x=>x.Id==id&&x.WorkflowType=="BUDGET_PLAN_LEVELS");
            if(req is null)return Results.NotFound(new{message="Plan/Budget approval request not found."});
            if(req.Status!="PENDING")return Results.BadRequest(new{message="This approval request is already completed."});
            if(req.RequestedBy!=uid&&role!="ROOT"&&!req.Steps.Any(x=>x.ApproverId==uid))return Results.StatusCode(403);
            var anchor=await db.BudgetPlanItems.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==req.EntityId);
            if(anchor is null)return Results.NotFound(new{message="Plan/Budget submission not found."});
            var currentLevel=req.Steps.Where(x=>x.Status=="PENDING").Min(x=>x.StepNo);
            var currentIds=req.Steps.Where(x=>x.Status=="PENDING"&&x.StepNo==currentLevel).Select(x=>x.ApproverId).Distinct().ToArray();
            var recipients=await db.Users.AsNoTracking().Where(x=>currentIds.Contains(x.Id)&&x.Status=="ACTIVE"&&x.Email!="").ToListAsync();
            var requester=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==req.RequestedBy);
            var items=await db.BudgetPlanItems.AsNoTracking().Where(x=>x.BudgetYear==anchor.BudgetYear&&x.OrgUnit==anchor.OrgUnit).ToListAsync();
            var publicUrl=(Environment.GetEnvironmentVariable("MPMS_PUBLIC_URL")??"https://project.maipt.org").TrimEnd('/');
            var detailUrl=$"{publicUrl}/?view=approvals&approvalId={req.Id}&entityType=BUDGET_PLAN&entityId={anchor.Id}";
            var warnings=new List<string>();var sent=0;
            foreach(var a in recipients)try{var html=ApprovalEmailHtml(a.Name,$"Plan/Budget {anchor.OrgUnit} / {anchor.BudgetYear}",$"BUD-{req.Id:0000}-{anchor.BudgetYear}",anchor.OrgUnit,$"{items.Count} items / total {items.Sum(x=>x.PlannedAmount):N0}",requester?.Name??"MPMS","Annual Work & Budget Plan","Pending Approval","",detailUrl,"is waiting for your approval.");await SendMail(a.Email,$"[MPMS] Approval Required - BUD-{req.Id:0000}-{anchor.BudgetYear} - {anchor.OrgUnit}",html);sent++;}catch(Exception ex){warnings.Add($"{a.Email}: {ex.Message}");}
            if(warnings.Count>0){db.Notifications.Add(new NotificationRecord{UserId=req.RequestedBy,Type="SYSTEM",Severity="WARNING",Title=$"Plan/Budget email was not sent - {anchor.OrgUnit} / {anchor.BudgetYear}",Message=string.Join(" | ",warnings),EntityType="APPROVAL_REQUEST",EntityId=req.Id,IsRead=false});await db.SaveChangesAsync();}
            return Results.Ok(new{sent,currentLevel,mailWarnings=warnings});
        });

        app.MapPost("/api/kpi-approvals/{id:long}/{decision}", async (long id,string decision,JsonElement body,HttpContext http,AppDbContext db) =>
        {
            var uid=Convert.ToInt64(http.Items["AuthUserId"]);
            var d=decision.ToUpperInvariant();
            if(d is not ("APPROVED" or "REJECTED"))return Results.BadRequest(new{message="Decision must be APPROVED or REJECTED."});

            var req=await db.ApprovalRequests.Include(x=>x.Steps).FirstOrDefaultAsync(x=>x.Id==id&&(x.WorkflowType=="KPI_APPROVAL"||x.WorkflowType=="KPI_LEVELS"||x.WorkflowType=="BUDGET_PLAN_APPROVAL"||x.WorkflowType=="BUDGET_PLAN_LEVELS"));
            if(req is null)return Results.NotFound();
            if(req.Status!="PENDING")return Results.BadRequest(new{message="Approval already completed."});

            ApprovalStep? step;
            if(req.WorkflowType=="KPI_LEVELS"||req.WorkflowType=="BUDGET_PLAN_LEVELS")
            {
                var current=req.Steps.Where(x=>x.Status=="PENDING").Select(x=>x.StepNo).DefaultIfEmpty(-1).Min();
                // Normalize requests created by older builds: later levels must
                // be WAITING and must never be actionable or emailed early.
                if(current>=0)
                    foreach(var future in req.Steps.Where(x=>x.Status=="PENDING"&&x.StepNo>current))
                        future.Status="WAITING";
                if(current<0)return Results.BadRequest(new{message="No pending approval level."});
                step=req.Steps.FirstOrDefault(x=>x.ApproverId==uid&&x.Status=="PENDING"&&x.StepNo==current);
                if(step is null)return Results.StatusCode(403);
            }
            else
            {
                step=req.Steps.FirstOrDefault(x=>x.ApproverId==uid&&x.Status=="PENDING");
                if(step is null)return Results.StatusCode(403);
            }

            step.Status=d;
            step.Comment=Text(body,"comment");
            step.ActedAt=DateTime.UtcNow;

            if(d=="REJECTED")
            {
                req.Status="REJECTED";
                req.CompletedAt=DateTime.UtcNow;
                foreach(var waiting in req.Steps.Where(x=>x.Status=="WAITING"))waiting.Status="SKIPPED";
            }
            else if(req.WorkflowType=="KPI_LEVELS"||req.WorkflowType=="BUDGET_PLAN_LEVELS")
            {
                // Level 4 (and any other multiple-approver level) only completes
                // after every PENDING approver at that same level has approved.
                var levelComplete=!req.Steps.Any(x=>x.StepNo==step.StepNo&&x.Status=="PENDING");
                if(levelComplete)
                {
                    var nextWaiting=req.Steps.Where(x=>x.Status=="WAITING"&&x.StepNo>step.StepNo)
                        .Select(x=>(int?)x.StepNo).Min();
                    if(nextWaiting.HasValue)
                    {
                        foreach(var nextStep in req.Steps.Where(x=>x.Status=="WAITING"&&x.StepNo==nextWaiting.Value))
                            nextStep.Status="PENDING";
                        req.Status="PENDING";
                    }
                    else
                    {
                        req.Status="APPROVED";
                        req.CompletedAt=DateTime.UtcNow;
                    }
                }
                else req.Status="PENDING";
            }
            else if(req.Steps.All(x=>x.Status=="APPROVED")){req.Status="APPROVED";req.CompletedAt=DateTime.UtcNow;}
            else req.Status="PENDING";

            if(req.WorkflowType=="BUDGET_PLAN_APPROVAL"||req.WorkflowType=="BUDGET_PLAN_LEVELS")
            {
                var anchor=await db.BudgetPlanItems.FindAsync(req.EntityId);
                if(anchor is null)return Results.NotFound(new{message="Plan/Budget submission not found."});
                var budgetItems=await db.BudgetPlanItems.Where(x=>x.BudgetYear==anchor.BudgetYear&&x.OrgUnit==anchor.OrgUnit).ToListAsync();
                var requester=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==req.RequestedBy);
                var actionBy=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid);
                var publicUrl=(Environment.GetEnvironmentVariable("MPMS_PUBLIC_URL")??"https://project.maipt.org").TrimEnd('/');
                var requesterDetailUrl=$"{publicUrl}/?view=budgetPlan";
                var mailWarnings=new List<string>();
                var itemStatus=req.Status=="APPROVED"?"APPROVED":req.Status=="REJECTED"?"REJECTED":"SUBMITTED";
                foreach(var item in budgetItems){item.Status=itemStatus;item.UpdatedAt=DateTime.UtcNow;}
                db.Notifications.Add(new NotificationRecord{UserId=req.RequestedBy,Type="APPROVAL",Severity=d=="REJECTED"?"WARNING":"INFO",Title=$"Plan/Budget {d.ToLowerInvariant()} - {anchor.OrgUnit} / {anchor.BudgetYear}",Message=step.Comment==""?$"Plan/Budget was {d.ToLowerInvariant()} by an approver.":step.Comment,EntityType="BUDGET_PLAN",EntityId=anchor.Id,IsRead=false});

                // Always give the submitter feedback for every approval action. Intermediate
                // approvals show the next pending level; reject/final approval show final status.
                if(requester is not null&&!string.IsNullOrWhiteSpace(requester.Email))
                {
                    var nextPendingLevel=req.Status=="PENDING"
                        ?req.Steps.Where(x=>x.Status=="PENDING").Select(x=>(int?)x.StepNo).Min()
                        :null;
                    var currentLevelComplete=!req.Steps.Any(x=>x.StepNo==step.StepNo&&x.Status=="PENDING");
                    var feedbackStatus=req.Status=="PENDING"&&!currentLevelComplete
                        ?$"Approved by one approver at {ApprovalLevelName(step.StepNo)} - Waiting for remaining approver(s)"
                        :req.Status=="PENDING"
                        ?$"{ApprovalLevelName(step.StepNo)} Completed - Pending {ApprovalLevelName(nextPendingLevel??-1)}"
                        :req.Status=="APPROVED"?"Final Approved":"Rejected";
                    var feedbackIntro=req.Status=="PENDING"&&!currentLevelComplete
                        ?$"was approved by {actionBy?.Name??"an approver"} at {ApprovalLevelName(step.StepNo)}. It remains at this level until every approver at the level has approved."
                        :req.Status=="PENDING"
                        ?$"was approved by {actionBy?.Name??"an approver"} at {ApprovalLevelName(step.StepNo)} and has moved to {ApprovalLevelName(nextPendingLevel??-1)}."
                        :req.Status=="APPROVED"
                            ?$"was fully approved by {actionBy?.Name??"the final approver"}."
                            :$"was rejected by {actionBy?.Name??"an approver"} at {ApprovalLevelName(step.StepNo)}.";
                    try
                    {
                        var html=ApprovalEmailHtml(requester.Name,$"Plan/Budget {anchor.OrgUnit} / {anchor.BudgetYear}",$"BUD-{req.Id:0000}-{anchor.BudgetYear}",anchor.OrgUnit,$"{budgetItems.Count} items / total {budgetItems.Sum(x=>x.PlannedAmount):N0}",requester.Name,"Annual Work & Budget Plan",feedbackStatus,step.Comment,requesterDetailUrl,feedbackIntro);
                        await SendMail(requester.Email,$"[MPMS] Plan/Budget {feedbackStatus} - {anchor.OrgUnit} / {anchor.BudgetYear}",html);
                    }
                    catch(Exception ex)
                    {
                        mailWarnings.Add($"{requester.Email}: {ex.Message}");
                        db.Notifications.Add(new NotificationRecord{UserId=req.RequestedBy,Type="SYSTEM",Severity="WARNING",Title=$"Approval feedback email was not sent - {anchor.OrgUnit} / {anchor.BudgetYear}",Message=ex.Message,EntityType="APPROVAL_REQUEST",EntityId=req.Id,IsRead=false});
                    }
                }
                if(req.Status=="PENDING"&&req.WorkflowType=="BUDGET_PLAN_LEVELS")
                {
                    var nextLevel=req.Steps.Where(x=>x.Status=="PENDING").Min(x=>x.StepNo);
                    var nextIds=nextLevel>step.StepNo
                        ?req.Steps.Where(x=>x.Status=="PENDING"&&x.StepNo==nextLevel).Select(x=>x.ApproverId).Distinct().ToArray()
                        :Array.Empty<long>();
                    var nextApprovers=await db.Users.AsNoTracking().Where(x=>nextIds.Contains(x.Id)).ToListAsync();
                    var detailUrl=$"{publicUrl}/?view=approvals&approvalId={req.Id}&entityType=BUDGET_PLAN&entityId={anchor.Id}";
                    foreach(var a in nextApprovers)
                    {
                        db.Notifications.Add(new NotificationRecord{UserId=a.Id,Type="APPROVAL",Severity="INFO",Title=$"Plan/Budget approval required - {anchor.OrgUnit}",Message=$"Plan/Budget {anchor.OrgUnit} / {anchor.BudgetYear} is now waiting for your approval.",EntityType="BUDGET_PLAN",EntityId=anchor.Id,IsRead=false});
                        try{var html=ApprovalEmailHtml(a.Name,$"Plan/Budget {anchor.OrgUnit} / {anchor.BudgetYear}",$"BUD-{req.Id:0000}-{anchor.BudgetYear}",anchor.OrgUnit,$"{budgetItems.Count} items / total {budgetItems.Sum(x=>x.PlannedAmount):N0}",requester?.Name??"MPMS","Annual Work & Budget Plan","Pending Approval","",detailUrl,"is now waiting for your approval.");await SendMail(a.Email,$"[MPMS] Approval Required - BUD-{req.Id:0000}-{anchor.BudgetYear} - {anchor.OrgUnit}",html);}catch(Exception ex){mailWarnings.Add($"{a.Email}: {ex.Message}");db.Notifications.Add(new NotificationRecord{UserId=req.RequestedBy,Type="SYSTEM",Severity="WARNING",Title=$"Plan/Budget email was not sent - {anchor.OrgUnit} / {anchor.BudgetYear}",Message=$"{a.Email}: {ex.Message}",EntityType="APPROVAL_REQUEST",EntityId=req.Id,IsRead=false});}
                    }
                }
                await db.SaveChangesAsync();
                return Results.Ok(new{req.Id,RequestStatus=req.Status,StepStatus=step.Status,MailWarnings=mailWarnings});
            }

            var period=await db.PerformancePeriods.FindAsync(req.EntityId);
            var kpiMailWarnings=new List<string>();
            if(period!=null)
            {
                if(req.Status=="APPROVED"){period.Status="APPROVED";period.IsLocked=true;period.LockedAt??=DateTime.UtcNow;}
                else if(req.Status=="REJECTED"){period.Status="REJECTED";period.IsLocked=true;period.LockedAt??=DateTime.UtcNow;}
                else period.Status="SUBMITTED";

                if(period.UserId.HasValue)db.Notifications.Add(new NotificationRecord{
                    UserId=period.UserId.Value,Type="APPROVAL",Severity=d=="REJECTED"?"WARNING":"INFO",
                    Title=$"KPI {d.ToLowerInvariant()} - {period.Period}",
                    Message=step.Comment==""?$"Your KPI {period.Period} was {d.ToLowerInvariant()} by an approver.":$"Your KPI {period.Period} was {d.ToLowerInvariant()}. Comment: {step.Comment}",
                    EntityType="PERFORMANCE_PERIOD",EntityId=period.Id,IsRead=false
                });

                var requester=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==req.RequestedBy);
                var actionBy=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid);
                if(requester is not null&&!string.IsNullOrWhiteSpace(requester.Email))
                {
                    var nextPendingLevel=req.Status=="PENDING"
                        ?req.Steps.Where(x=>x.Status=="PENDING").Select(x=>(int?)x.StepNo).Min()
                        :null;
                    var currentLevelComplete=!req.Steps.Any(x=>x.StepNo==step.StepNo&&x.Status=="PENDING");
                    var feedbackStatus=req.Status=="PENDING"&&!currentLevelComplete
                        ?$"Approved by one approver at {ApprovalLevelName(step.StepNo)} - Waiting for remaining approver(s)"
                        :req.Status=="PENDING"
                        ?$"{ApprovalLevelName(step.StepNo)} Completed - Pending {ApprovalLevelName(nextPendingLevel??-1)}"
                        :req.Status=="APPROVED"?"Final Approved":"Rejected";
                    var feedbackIntro=req.Status=="PENDING"&&!currentLevelComplete
                        ?$"was approved by {actionBy?.Name??"an approver"} at {ApprovalLevelName(step.StepNo)}. It remains at this level until every approver at the level has approved."
                        :req.Status=="PENDING"
                        ?$"was approved by {actionBy?.Name??"an approver"} at {ApprovalLevelName(step.StepNo)} and has moved to {ApprovalLevelName(nextPendingLevel??-1)}."
                        :req.Status=="APPROVED"
                            ?$"was fully approved by {actionBy?.Name??"the final approver"}."
                            :$"was rejected by {actionBy?.Name??"an approver"} at {ApprovalLevelName(step.StepNo)}.";
                    var publicUrl=(Environment.GetEnvironmentVariable("MPMS_PUBLIC_URL")??"https://project.maipt.org").TrimEnd('/');
                    try
                    {
                        var html=ApprovalEmailHtml(requester.Name,$"KPI {period.EmployeeName} / {period.Period}",$"KPI-{req.Id:0000}",period.Department??"",period.EmployeeName,requester.Name,"Performance & KPI",feedbackStatus,step.Comment,$"{publicUrl}/?view=teamPerformance",feedbackIntro);
                        await SendMail(requester.Email,$"[MPMS] KPI {feedbackStatus} - {period.EmployeeName} / {period.Period}",html);
                    }
                    catch(Exception ex)
                    {
                        kpiMailWarnings.Add($"{requester.Email}: {ex.Message}");
                        db.Notifications.Add(new NotificationRecord{UserId=req.RequestedBy,Type="SYSTEM",Severity="WARNING",Title=$"KPI approval feedback email was not sent - {period.Period}",Message=ex.Message,EntityType="APPROVAL_REQUEST",EntityId=req.Id,IsRead=false});
                    }
                }
            }

            if(req.Status=="PENDING" && req.WorkflowType=="KPI_LEVELS")
            {
                var nextLevel=req.Steps.Where(x=>x.Status=="PENDING").Select(x=>x.StepNo).DefaultIfEmpty(-1).Min();
                // Do not expose or notify the next level until every approver
                // in the current level has approved.
                if(nextLevel>step.StepNo)
                {
                    var nextIds=req.Steps.Where(x=>x.Status=="PENDING"&&x.StepNo==nextLevel)
                        .Select(x=>x.ApproverId).Distinct().ToArray();
                    var nextApprovers=await db.Users.AsNoTracking()
                        .Where(x=>nextIds.Contains(x.Id)&&x.Status=="ACTIVE"&&x.Email!="")
                        .ToListAsync();
                    var requester=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==req.RequestedBy);
                    var publicUrl=(Environment.GetEnvironmentVariable("MPMS_PUBLIC_URL")??"https://project.maipt.org").TrimEnd('/');
                    var detailUrl=$"{publicUrl}/?view=approvals&approvalId={req.Id}&entityType=PERFORMANCE_PERIOD&entityId={req.EntityId}";
                    foreach(var nextId in nextIds)
                    {
                        var exists=await db.Notifications.AnyAsync(n=>
                            n.UserId==nextId &&
                            !n.IsRead &&
                            n.Type=="APPROVAL" &&
                            n.EntityType=="PERFORMANCE_PERIOD" &&
                            n.EntityId==req.EntityId);
                        if(!exists)
                            db.Notifications.Add(new NotificationRecord{
                                UserId=nextId,Type="APPROVAL",Severity="INFO",
                                Title=$"KPI approval required - {period?.EmployeeName}",
                                Message=$"{period?.EmployeeName} KPI {period?.Period} is now waiting for your approval.",
                                EntityType="PERFORMANCE_PERIOD",EntityId=req.EntityId,IsRead=false
                            });
                    }
                    foreach(var a in nextApprovers)
                    {
                        try
                        {
                            var summary=$"Team Performance KPI / {period?.EmployeeName} / {period?.Period}";
                            var html=ApprovalEmailHtml(a.Name,summary,$"KPI-{req.Id:0000}",period?.Department??"",summary,requester?.Name??period?.EmployeeName??"MPMS","Team Performance / KPI","Pending Approval","",detailUrl,$"is now waiting for your approval at {ApprovalLevelName(nextLevel)}.");
                            await SendMail(a.Email,$"[MPMS] Approval Required - KPI-{req.Id:0000} - {period?.EmployeeName}",html);
                        }
                        catch(Exception ex)
                        {
                            kpiMailWarnings.Add($"{a.Email}: {ex.Message}");
                            db.Notifications.Add(new NotificationRecord{UserId=req.RequestedBy,Type="SYSTEM",Severity="WARNING",Title=$"KPI email was not sent - {period?.Period}",Message=$"{a.Email}: {ex.Message}",EntityType="APPROVAL_REQUEST",EntityId=req.Id,IsRead=false});
                        }
                    }
                }
            }

            await db.SaveChangesAsync();
            var next=req.Status=="PENDING"?req.Steps.Where(x=>x.Status=="PENDING").Select(x=>(int?)x.StepNo).Min():null;
            return Results.Ok(new{req.Id,RequestStatus=req.Status,StepStatus=step.Status,CurrentLevel=next,MailWarnings=kpiMailWarnings});
        });

    }
}
