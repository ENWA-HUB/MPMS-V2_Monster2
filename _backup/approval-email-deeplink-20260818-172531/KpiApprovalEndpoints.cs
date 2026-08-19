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

    public static void MapKpiApprovalEndpoints(this WebApplication app)
    {
        app.MapGet("/api/performance/approvers", async (AppDbContext db) =>
            Results.Ok(await db.Users.AsNoTracking().Where(x=>x.Status=="ACTIVE"&&x.Email!=""&&
                (x.Role=="ADMIN"||x.Role=="CIO"||x.Role=="HOD"||x.Role=="PM"||x.Role=="PROJECT_MANAGER"))
                .OrderBy(x=>x.Name).Select(x=>new{x.Id,x.Name,x.Email,x.JobTitle,x.Department,x.Role}).ToListAsync()));

        app.MapPost("/api/performance/periods/{id:long}/submit", async (long id,JsonElement body,HttpContext http,AppDbContext db) =>
        {
            var uid=Convert.ToInt64(http.Items["AuthUserId"]);
            var canManage=CanManage(Convert.ToString(http.Items["AuthUserRole"]));
            var period=await db.PerformancePeriods.Include(x=>x.Items).FirstOrDefaultAsync(x=>x.Id==id);
            if(period is null) return Results.NotFound();
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

            var publicUrl=Environment.GetEnvironmentVariable("MPMS_PUBLIC_URL") ?? "https://project.maipt.org";
            var msg=Text(body,"message"); var warnings=new List<string>();
            foreach(var a in approvers){
                try{
                    var html=$"<div style='font-family:Segoe UI,Arial,sans-serif'><h2>KPI Approval Required</h2><p>Dear {WebUtility.HtmlEncode(a.Name)},</p><p><b>{WebUtility.HtmlEncode(period.EmployeeName)}</b> submitted KPI <b>{WebUtility.HtmlEncode(period.Period)}</b> for your approval.</p>{(msg==""?"":$"<p><b>Message:</b> {WebUtility.HtmlEncode(msg)}</p>")}<p><a href='{WebUtility.HtmlEncode(publicUrl)}'>Open MPMS Approvals</a></p></div>";
                    await SendMail(a.Email,$"[MPMS] KPI Approval Required - {period.EmployeeName} - {period.Period}",html);
                }catch(Exception ex){warnings.Add($"{a.Email}: {ex.Message}");}
            }
            return Results.Ok(new{period.Id,period.Status,approvalRequestId=req.Id,mailSent=approvers.Count-warnings.Count,mailWarnings=warnings});
        });

        app.MapGet("/api/kpi-approvals", async (HttpContext http,AppDbContext db) =>
        {
            var uid=Convert.ToInt64(http.Items["AuthUserId"]); var canManage=CanManage(Convert.ToString(http.Items["AuthUserRole"]));
            var q=db.ApprovalRequests.AsNoTracking().Include(x=>x.Steps).ThenInclude(x=>x.Approver).Where(x=>x.WorkflowType=="KPI_APPROVAL").AsQueryable();
            if(!canManage) q=q.Where(x=>x.RequestedBy==uid||x.Steps.Any(s=>s.ApproverId==uid));
            var rows=await q.OrderByDescending(x=>x.RequestedAt).ToListAsync();
            var ids=rows.Select(x=>x.EntityId).Distinct().ToList();
            var periods=await db.PerformancePeriods.AsNoTracking().Where(x=>ids.Contains(x.Id)).ToDictionaryAsync(x=>x.Id);
            return Results.Ok(rows.Select(x=>{periods.TryGetValue(x.EntityId,out var p);return new{x.Id,x.EntityType,x.EntityId,x.WorkflowType,x.Status,x.RequestedAt,x.RequestedBy,EmployeeName=p?.EmployeeName,Period=p?.Period,Department=p?.Department,CanAct=x.Status=="PENDING"&&x.Steps.Any(z=>z.ApproverId==uid&&z.Status=="PENDING"),Steps=x.Steps.OrderBy(z=>z.Id).Select(z=>new{z.Id,z.StepNo,z.Status,z.Comment,z.ApproverId,Approver=z.Approver!=null?z.Approver.Name:""})};}));
        });

        app.MapPost("/api/kpi-approvals/{id:long}/{decision}", async (long id,string decision,JsonElement body,HttpContext http,AppDbContext db) =>
        {
            var uid=Convert.ToInt64(http.Items["AuthUserId"]); var d=decision.ToUpperInvariant();
            if(d is not ("APPROVED" or "REJECTED")) return Results.BadRequest(new{message="Decision must be APPROVED or REJECTED."});
            var req=await db.ApprovalRequests.Include(x=>x.Steps).FirstOrDefaultAsync(x=>x.Id==id&&x.WorkflowType=="KPI_APPROVAL");
            if(req is null) return Results.NotFound(); if(req.Status!="PENDING") return Results.BadRequest(new{message="Approval already completed."});
            var step=req.Steps.FirstOrDefault(x=>x.ApproverId==uid&&x.Status=="PENDING"); if(step is null) return Results.StatusCode(403);
            step.Status=d; step.Comment=Text(body,"comment"); step.ActedAt=DateTime.UtcNow;
            if(d=="REJECTED"){req.Status="REJECTED";req.CompletedAt=DateTime.UtcNow;}
            else if(req.Steps.Where(x=>x.Id!=step.Id).All(x=>x.Status=="APPROVED")){req.Status="APPROVED";req.CompletedAt=DateTime.UtcNow;}
            var period=await db.PerformancePeriods.FindAsync(req.EntityId);
            if(period!=null){
                if(req.Status=="APPROVED"){period.Status="APPROVED";period.IsLocked=true;period.LockedAt??=DateTime.UtcNow;}
                else if(req.Status=="REJECTED"){period.Status="REJECTED";period.IsLocked=false;period.LockedAt=null;}
                else period.Status="SUBMITTED";
                if(period.UserId.HasValue) db.Notifications.Add(new NotificationRecord{UserId=period.UserId.Value,Type="APPROVAL",Severity=d=="REJECTED"?"WARNING":"INFO",Title=$"KPI {d.ToLowerInvariant()} - {period.Period}",Message=step.Comment==""?$"Your KPI {period.Period} was {d.ToLowerInvariant()} by an approver.":$"Your KPI {period.Period} was {d.ToLowerInvariant()}. Comment: {step.Comment}",EntityType="PERFORMANCE_PERIOD",EntityId=period.Id,IsRead=false});
            }
            await db.SaveChangesAsync(); return Results.Ok(new { req.Id, RequestStatus = req.Status, StepStatus = step.Status });
        });
    }
}
