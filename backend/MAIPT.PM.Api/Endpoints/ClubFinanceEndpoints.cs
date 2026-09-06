using MAIPT.PM.Api.Data;
using MAIPT.PM.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MAIPT.PM.Api.Endpoints;

public static class ClubFinanceEndpoints
{
    static long U(HttpContext h)=>Convert.ToInt64(h.Items["AuthUserId"]);
    static bool Root(HttpContext h)=>string.Equals(Convert.ToString(h.Items["AuthUserRole"]),"ROOT",StringComparison.OrdinalIgnoreCase);
    static async Task<bool> CanRead(AppDbContext d,HttpContext h,long clubId)=>
        Root(h)||await d.PersonalFcClubs.AnyAsync(x=>x.Id==clubId&&x.OwnerUserId==U(h))||
        await d.PersonalFcClubMembers.AnyAsync(x=>x.ClubId==clubId&&x.UserId==U(h)&&x.Status=="ACTIVE");
    static async Task<bool> CanManage(AppDbContext d,HttpContext h,long clubId)=>
        Root(h)||await d.PersonalFcClubs.AnyAsync(x=>x.Id==clubId&&x.OwnerUserId==U(h))||
        await d.PersonalFcClubMembers.AnyAsync(x=>x.ClubId==clubId&&x.UserId==U(h)&&(x.MemberRole=="ADMIN"||x.MemberRole=="TREASURER"));

    public static void MapClubFinanceEndpoints(this WebApplication app)
    {
        app.MapGet("/api/personal-fc/clubs/{clubId:long}/finance/funds",async(long clubId,HttpContext h,AppDbContext d)=>{
            if(!await CanRead(d,h,clubId))return Results.Forbid();
            var funds=await d.PersonalFcClubFunds.AsNoTracking().Where(x=>x.ClubId==clubId).OrderBy(x=>x.FundCode).ThenBy(x=>x.Name).ToListAsync();
            var posted=await d.PersonalFcClubTransactions.AsNoTracking().Where(x=>x.ClubId==clubId&&x.Status=="APPROVED").ToListAsync();
            var accounts=await d.PersonalFcAccounts.AsNoTracking().OrderBy(x=>x.Name).Select(x=>new{x.Id,x.Name,x.AccountType,x.Institution,x.Currency}).ToListAsync();
            return Results.Ok(funds.Select(f=>new{
                f.Id,f.ClubId,f.FundCode,f.Name,f.FundType,f.Purpose,f.SourceType,f.DefaultAccountId,f.OpeningBalance,f.Currency,f.EffectiveDate,f.Status,f.Notes,
                currentBalance=f.OpeningBalance+
                    posted.Where(t=>t.FundId==f.Id&&t.TransactionType=="INCOME").Sum(t=>t.Amount)-
                    posted.Where(t=>t.FundId==f.Id&&t.TransactionType=="EXPENSE").Sum(t=>t.Amount),
                defaultAccount=accounts.FirstOrDefault(a=>a.Id==f.DefaultAccountId)
            }));
        });

        app.MapPost("/api/personal-fc/clubs/{clubId:long}/finance/funds",async(long clubId,PersonalFcClubFund x,HttpContext h,AppDbContext d)=>{
            if(!await CanManage(d,h,clubId))return Results.Forbid();
            x.Id=0;x.ClubId=clubId;
            x.FundCode=(x.FundCode??"").Trim().ToUpperInvariant();
            if(string.IsNullOrWhiteSpace(x.FundCode))x.FundCode="GENERAL";
            if(string.IsNullOrWhiteSpace(x.FundType))x.FundType="GENERAL";
            if(string.IsNullOrWhiteSpace(x.SourceType))x.SourceType="MEMBER_FEE";
            if(string.IsNullOrWhiteSpace(x.Currency))x.Currency="VND";
            if(string.IsNullOrWhiteSpace(x.Status))x.Status="ACTIVE";
            d.PersonalFcClubFunds.Add(x);await d.SaveChangesAsync();return Results.Ok(x);
        });

        app.MapPut("/api/personal-fc/clubs/{clubId:long}/finance/funds/{id:long}",async(long clubId,long id,PersonalFcClubFund input,HttpContext h,AppDbContext d)=>{
            if(!await CanManage(d,h,clubId))return Results.Forbid();
            var x=await d.PersonalFcClubFunds.FirstOrDefaultAsync(z=>z.Id==id&&z.ClubId==clubId);if(x==null)return Results.NotFound();
            x.FundCode=(input.FundCode??"").Trim().ToUpperInvariant();x.Name=input.Name;x.FundType=input.FundType;x.Purpose=input.Purpose;
            x.SourceType=input.SourceType;x.DefaultAccountId=input.DefaultAccountId;x.OpeningBalance=input.OpeningBalance;x.Currency=input.Currency;
            x.EffectiveDate=input.EffectiveDate;x.Status=input.Status;x.Notes=input.Notes;
            await d.SaveChangesAsync();return Results.Ok(x);
        });

        app.MapDelete("/api/personal-fc/clubs/{clubId:long}/finance/funds/{id:long}",async(long clubId,long id,HttpContext h,AppDbContext d)=>{
            if(!await CanManage(d,h,clubId))return Results.Forbid();
            var x=await d.PersonalFcClubFunds.FirstOrDefaultAsync(z=>z.Id==id&&z.ClubId==clubId);if(x==null)return Results.NotFound();
            var used=await d.PersonalFcClubTransactions.AnyAsync(t=>t.FundId==id&&t.Status=="APPROVED");
            if(used)return Results.BadRequest(new{message="Fund already has approved transactions. Deactivate it instead of deleting."});
            d.PersonalFcClubFunds.Remove(x);await d.SaveChangesAsync();return Results.NoContent();
        });

        app.MapGet("/api/personal-fc/clubs/{clubId:long}/finance/transactions",async(long clubId,int? year,int? month,string? status,string? source,HttpContext h,AppDbContext d)=>{
            if(!await CanRead(d,h,clubId))return Results.Forbid();
            var q=d.PersonalFcClubTransactions.AsNoTracking().Where(x=>x.ClubId==clubId);
            if(year.HasValue)q=q.Where(x=>x.TransactionDate.Year==year.Value);
            if(month.HasValue)q=q.Where(x=>x.TransactionDate.Month==month.Value);
            if(!string.IsNullOrWhiteSpace(status))q=q.Where(x=>x.Status==status);
            if(!string.IsNullOrWhiteSpace(source))q=q.Where(x=>x.SourceType==source);
            var rows=await q.OrderByDescending(x=>x.TransactionDate).ThenByDescending(x=>x.Id).Take(1000).ToListAsync();
            var funds=await d.PersonalFcClubFunds.AsNoTracking().Where(x=>x.ClubId==clubId).ToDictionaryAsync(x=>x.Id,x=>x.Name);
            var accounts=await d.PersonalFcAccounts.AsNoTracking().ToDictionaryAsync(x=>x.Id,x=>x.Name);
            var members=await d.PersonalFcClubMembers.AsNoTracking().Where(x=>x.ClubId==clubId).ToDictionaryAsync(x=>x.Id,x=>x.MemberName);
            return Results.Ok(rows.Select(x=>new{
                x.Id,x.ClubId,x.FundId,x.AccountId,x.MemberId,x.TransactionDate,x.DueDate,x.TransactionType,x.SourceType,x.Category,
                x.Amount,x.Currency,x.PaymentMethod,x.ReferenceNo,x.Description,x.Status,x.ApprovedByUserId,x.ApprovedAt,x.PeriodKey,x.AutoGenerated,
                x.CreatedByUserId,
                fundName=x.FundId.HasValue&&funds.TryGetValue(x.FundId.Value,out var fn)?fn:"",
                accountName=x.AccountId.HasValue&&accounts.TryGetValue(x.AccountId.Value,out var an)?an:"",
                memberName=x.MemberId.HasValue&&members.TryGetValue(x.MemberId.Value,out var mn)?mn:""
            }));
        });

        app.MapPost("/api/personal-fc/clubs/{clubId:long}/finance/transactions",async(long clubId,PersonalFcClubTransaction x,HttpContext h,AppDbContext d)=>{
            if(!await CanManage(d,h,clubId))return Results.Forbid();
            x.Id=0;x.ClubId=clubId;x.CreatedByUserId=U(h);
            if(string.IsNullOrWhiteSpace(x.TransactionType))x.TransactionType="INCOME";
            if(string.IsNullOrWhiteSpace(x.SourceType))x.SourceType="MANUAL";
            if(string.IsNullOrWhiteSpace(x.Category))x.Category="OTHER";
            if(string.IsNullOrWhiteSpace(x.PaymentMethod))x.PaymentMethod="BANK_TRANSFER";
            if(string.IsNullOrWhiteSpace(x.Currency))x.Currency="VND";
            x.Status="PENDING";x.ApprovedAt=null;x.ApprovedByUserId=null;
            d.PersonalFcClubTransactions.Add(x);await d.SaveChangesAsync();return Results.Ok(x);
        });

        app.MapPut("/api/personal-fc/clubs/{clubId:long}/finance/transactions/{id:long}",async(long clubId,long id,PersonalFcClubTransaction input,HttpContext h,AppDbContext d)=>{
            if(!await CanManage(d,h,clubId))return Results.Forbid();
            var x=await d.PersonalFcClubTransactions.FirstOrDefaultAsync(z=>z.Id==id&&z.ClubId==clubId);if(x==null)return Results.NotFound();
            if(x.Status=="APPROVED"&&!Root(h))return Results.BadRequest(new{message="Approved transaction is posted. Only ROOT can edit it."});
            x.FundId=input.FundId;x.AccountId=input.AccountId;x.MemberId=input.MemberId;x.TransactionDate=input.TransactionDate;x.DueDate=input.DueDate;
            x.TransactionType=input.TransactionType;x.SourceType=input.SourceType;x.Category=input.Category;x.Amount=input.Amount;x.Currency=input.Currency;
            x.PaymentMethod=input.PaymentMethod;x.ReferenceNo=input.ReferenceNo;x.Description=input.Description;
            await d.SaveChangesAsync();return Results.Ok(x);
        });

        app.MapDelete("/api/personal-fc/clubs/{clubId:long}/finance/transactions/{id:long}",async(long clubId,long id,HttpContext h,AppDbContext d)=>{
            if(!await CanManage(d,h,clubId))return Results.Forbid();
            var x=await d.PersonalFcClubTransactions.FirstOrDefaultAsync(z=>z.Id==id&&z.ClubId==clubId);if(x==null)return Results.NotFound();
            if(x.Status=="APPROVED"&&!Root(h))return Results.BadRequest(new{message="Approved transaction is posted. Reject/reverse it instead."});
            d.PersonalFcClubTransactions.Remove(x);await d.SaveChangesAsync();return Results.NoContent();
        });

        app.MapPost("/api/personal-fc/clubs/{clubId:long}/finance/transactions/{id:long}/approve",async(long clubId,long id,HttpContext h,AppDbContext d)=>{
            if(!await CanManage(d,h,clubId))return Results.Forbid();
            var x=await d.PersonalFcClubTransactions.FirstOrDefaultAsync(z=>z.Id==id&&z.ClubId==clubId);if(x==null)return Results.NotFound();
            if(x.Amount<=0)return Results.BadRequest(new{message="Amount must be greater than zero."});

            var fund=x.FundId.HasValue?await d.PersonalFcClubFunds.FirstOrDefaultAsync(f=>f.Id==x.FundId&&f.ClubId==clubId):null;
            fund??=await d.PersonalFcClubFunds.Where(f=>f.ClubId==clubId&&f.Status=="ACTIVE"&&(f.FundCode=="GENERAL"||f.FundType=="GENERAL")).OrderBy(f=>f.Id).FirstOrDefaultAsync();
            fund??=await d.PersonalFcClubFunds.Where(f=>f.ClubId==clubId&&f.Status=="ACTIVE").OrderBy(f=>f.Id).FirstOrDefaultAsync();
            if(fund==null)return Results.BadRequest(new{message="No active Fund. Create GENERAL Fund first."});

            var accountId=x.AccountId??fund.DefaultAccountId;
            if(!accountId.HasValue)return Results.BadRequest(new{message="Fund has no Default Account. Configure Default Account first."});
            var account=await d.PersonalFcAccounts.FirstOrDefaultAsync(a=>a.Id==accountId.Value);
            if(account==null)return Results.BadRequest(new{message="Selected Account does not exist."});

            await using var tr=await d.Database.BeginTransactionAsync();
            x.FundId=fund.Id;x.AccountId=account.Id;x.Status="APPROVED";x.ApprovedByUserId=U(h);x.ApprovedAt=DateTime.UtcNow;

            if(!x.PostedPersonalTransactionId.HasValue){
                var marker=$"[CLUB:{clubId}:TX:{x.Id}]";
                var posted=await d.PersonalFcTransactions.FirstOrDefaultAsync(t=>t.AccountId==account.Id&&t.Description.StartsWith(marker));
                if(posted==null){
                    posted=new PersonalFcTransaction{OwnerUserId=account.OwnerUserId,AccountId=account.Id,ToAccountId=null,CategoryId=null,TransactionDate=x.TransactionDate,TransactionType=x.TransactionType,Amount=x.Amount,Currency=x.Currency,Description=$"{marker} {x.Description}",Notes=$"Club finance posting; Source={x.SourceType}; Category={x.Category}; FundId={fund.Id}; MemberId={x.MemberId}"};
                    d.PersonalFcTransactions.Add(posted);await d.SaveChangesAsync();
                }
                x.PostedPersonalTransactionId=posted.Id;
            }
            await d.SaveChangesAsync();await tr.CommitAsync();
            return Results.Ok(new{x.Id,x.Status,x.FundId,x.AccountId,x.PostedPersonalTransactionId,x.Amount});
        });

        app.MapPost("/api/personal-fc/clubs/{clubId:long}/finance/transactions/{id:long}/reject",async(long clubId,long id,HttpContext h,AppDbContext d)=>{
            if(!await CanManage(d,h,clubId))return Results.Forbid();
            var x=await d.PersonalFcClubTransactions.FirstOrDefaultAsync(z=>z.Id==id&&z.ClubId==clubId);if(x==null)return Results.NotFound();
            x.Status="REJECTED";x.ApprovedByUserId=U(h);x.ApprovedAt=DateTime.UtcNow;
            await d.SaveChangesAsync();return Results.Ok(x);
        });

        app.MapPost("/api/personal-fc/clubs/{clubId:long}/finance/reconcile-approved",async(long clubId,HttpContext h,AppDbContext d)=>{
            if(!await CanManage(d,h,clubId))return Results.Forbid();
            var general=await d.PersonalFcClubFunds.Where(f=>f.ClubId==clubId&&f.Status=="ACTIVE"&&(f.FundCode=="GENERAL"||f.FundType=="GENERAL")).OrderBy(f=>f.Id).FirstOrDefaultAsync();
            general??=await d.PersonalFcClubFunds.Where(f=>f.ClubId==clubId&&f.Status=="ACTIVE").OrderBy(f=>f.Id).FirstOrDefaultAsync();
            if(general==null)return Results.BadRequest(new{message="No active Fund. Create GENERAL Fund first."});
            if(!general.DefaultAccountId.HasValue)return Results.BadRequest(new{message="GENERAL Fund has no Default Account. Configure it first."});
            var rows=await d.PersonalFcClubTransactions.Where(x=>x.ClubId==clubId&&x.Status=="APPROVED").OrderBy(x=>x.Id).ToListAsync();
            var linked=0;var fundFixed=0;var accountFixed=0;
            await using var tr=await d.Database.BeginTransactionAsync();
            foreach(var x in rows){
                if(!x.FundId.HasValue){x.FundId=general.Id;fundFixed++;}
                if(!x.AccountId.HasValue){x.AccountId=general.DefaultAccountId;accountFixed++;}
                if(x.PostedPersonalTransactionId.HasValue)continue;
                var account=await d.PersonalFcAccounts.FirstOrDefaultAsync(a=>a.Id==x.AccountId!.Value);if(account==null)continue;
                var marker=$"[CLUB:{clubId}:TX:{x.Id}]";
                var posted=await d.PersonalFcTransactions.FirstOrDefaultAsync(t=>t.AccountId==account.Id&&t.Description.StartsWith(marker));
                if(posted==null){posted=new PersonalFcTransaction{OwnerUserId=account.OwnerUserId,AccountId=account.Id,ToAccountId=null,CategoryId=null,TransactionDate=x.TransactionDate,TransactionType=x.TransactionType,Amount=x.Amount,Currency=x.Currency,Description=$"{marker} {x.Description}",Notes=$"Club finance reconciliation; Source={x.SourceType}; Category={x.Category}; FundId={x.FundId}; MemberId={x.MemberId}"};d.PersonalFcTransactions.Add(posted);await d.SaveChangesAsync();}
                x.PostedPersonalTransactionId=posted.Id;linked++;
            }
            await d.SaveChangesAsync();await tr.CommitAsync();
            return Results.Ok(new{approvedRows=rows.Count,linked,fundFixed,accountFixed});
        });

        app.MapPost("/api/personal-fc/clubs/{clubId:long}/finance/generate-member-fees",async(long clubId,int? year,int? month,HttpContext h,AppDbContext d)=>{
            if(!await CanManage(d,h,clubId))return Results.Forbid();
            var y=year??DateTime.Today.Year;var m=month??DateTime.Today.Month;var period=$"{y:0000}-{m:00}";
            var fund=await d.PersonalFcClubFunds.Where(x=>x.ClubId==clubId&&x.Status=="ACTIVE"&&(x.FundType=="GENERAL"||x.FundCode=="GENERAL")).OrderBy(x=>x.Id).FirstOrDefaultAsync()
                     ??await d.PersonalFcClubFunds.Where(x=>x.ClubId==clubId&&x.Status=="ACTIVE").OrderBy(x=>x.Id).FirstOrDefaultAsync();
            var members=await d.PersonalFcClubMembers.Where(x=>x.ClubId==clubId&&x.Status=="ACTIVE"&&x.RecurringFeeEnabled&&x.MonthlyFeeAmount>0).OrderBy(x=>x.MemberCode).ToListAsync();
            var created=0;var skipped=0;
            foreach(var member in members){
                var exists=await d.PersonalFcClubTransactions.AnyAsync(x=>x.ClubId==clubId&&x.MemberId==member.Id&&x.SourceType=="MEMBER_FEE"&&x.PeriodKey==period);
                if(exists){skipped++;continue;}
                var day=Math.Max(1,Math.Min(member.RecurringFeeDay<=0?1:member.RecurringFeeDay,DateTime.DaysInMonth(y,m)));
                var due=new DateOnly(y,m,day);
                d.PersonalFcClubTransactions.Add(new PersonalFcClubTransaction{
                    ClubId=clubId,FundId=fund?.Id,AccountId=fund?.DefaultAccountId,MemberId=member.Id,
                    TransactionDate=due,DueDate=due,TransactionType="INCOME",SourceType="MEMBER_FEE",Category="MEMBERSHIP",
                    Amount=member.MonthlyFeeAmount,Currency=fund?.Currency??"VND",PaymentMethod="BANK_TRANSFER",
                    ReferenceNo=$"{period}-{member.MemberCode}",Description=$"Membership fee {period} - {member.MemberName}",
                    Status="PENDING",PeriodKey=period,AutoGenerated=true,CreatedByUserId=U(h)
                });created++;
            }
            await d.SaveChangesAsync();
            return Results.Ok(new{period,created,skipped,fundId=fund?.Id,defaultAccountId=fund?.DefaultAccountId,
                warning=fund==null?"No active Fund found. Generated transactions require Fund/Account before approval.":fund.DefaultAccountId==null?"Default Account is not configured for the Fund.":""});
        });
    }
}
