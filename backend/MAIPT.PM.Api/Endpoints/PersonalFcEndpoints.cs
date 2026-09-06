using MAIPT.PM.Api.Data; using MAIPT.PM.Api.Models; using Microsoft.EntityFrameworkCore;
namespace MAIPT.PM.Api.Endpoints;
public static class PersonalFcEndpoints {
 static long U(HttpContext h)=>Convert.ToInt64(h.Items["AuthUserId"]);
 static bool Root(HttpContext h)=>string.Equals(Convert.ToString(h.Items["AuthUserRole"]),"ROOT",StringComparison.OrdinalIgnoreCase);
 static async Task<bool> ClubRead(AppDbContext d,HttpContext h,long id)=>Root(h)||await d.PersonalFcClubs.AnyAsync(x=>x.Id==id&&x.OwnerUserId==U(h))||await d.PersonalFcClubMembers.AnyAsync(x=>x.ClubId==id&&x.UserId==U(h)&&x.Status=="ACTIVE");
 static async Task<bool> ClubManage(AppDbContext d,HttpContext h,long id)=>Root(h)||await d.PersonalFcClubs.AnyAsync(x=>x.Id==id&&x.OwnerUserId==U(h))||await d.PersonalFcClubMembers.AnyAsync(x=>x.ClubId==id&&x.UserId==U(h)&&(x.MemberRole=="ADMIN"||x.MemberRole=="TREASURER"));
 public static void MapPersonalFcEndpoints(this WebApplication app){
  app.MapGet("/api/personal-fc/overview",async(HttpContext h,AppDbContext d)=>{
   var r=Root(h);var u=U(h);var q=d.PersonalFcTransactions.AsNoTracking().Where(x=>r||x.OwnerUserId==u);
   var m=new DateOnly(DateTime.Today.Year,DateTime.Today.Month,1);var z=q.Where(x=>x.TransactionDate>=m);
   var inc=await z.Where(x=>x.TransactionType=="INCOME").SumAsync(x=>(decimal?)x.Amount)??0;
   var exp=await z.Where(x=>x.TransactionType=="EXPENSE").SumAsync(x=>(decimal?)x.Amount)??0;
   var a=await d.PersonalFcAssets.Where(x=>(r||x.OwnerUserId==u)&&x.IncludeInNetWorth).SumAsync(x=>(decimal?)x.CurrentValue)??0;
   var ac=await d.PersonalFcAccounts.Where(x=>(r||x.OwnerUserId==u)&&x.IncludeInNetWorth).SumAsync(x=>(decimal?)x.OpeningBalance)??0;
   var debt=await d.PersonalFcDebts.Where(x=>(r||x.OwnerUserId==u)&&x.Status=="ACTIVE").SumAsync(x=>(decimal?)x.OutstandingAmount)??0;
   return Results.Ok(new{income=inc,expense=exp,netCashFlow=inc-exp,netWorth=a+ac-debt,assets=a,accounts=ac,debts=debt});
  });
  app.MapGet("/api/personal-fc/categories",async(HttpContext h,AppDbContext d)=>{var r=Root(h);var u=U(h);return Results.Ok(await d.PersonalFcCategories.AsNoTracking().Where(x=>x.Active&&(x.OwnerUserId==null||r||x.OwnerUserId==u)).OrderBy(x=>x.Type).ThenBy(x=>x.SortOrder).ToListAsync());});
  app.MapPost("/api/personal-fc/categories",async(PersonalFcCategory x,HttpContext h,AppDbContext d)=>{x.Id=0;x.OwnerUserId=Root(h)?x.OwnerUserId:U(h);d.Add(x);await d.SaveChangesAsync();return Results.Ok(x);});

  app.MapGet("/api/personal-fc/accounts",async(HttpContext h,AppDbContext d)=>{var r=Root(h);var u=U(h);return Results.Ok(await d.PersonalFcAccounts.AsNoTracking().Where(x=>r||x.OwnerUserId==u).OrderBy(x=>x.Name).ToListAsync());});
  app.MapPost("/api/personal-fc/accounts",async(PersonalFcAccount x,HttpContext h,AppDbContext d)=>{x.Id=0;x.OwnerUserId=U(h);x.CreatedAt=x.UpdatedAt=DateTime.UtcNow;d.Add(x);await d.SaveChangesAsync();return Results.Ok(x);});
  app.MapDelete("/api/personal-fc/accounts/{id:long}",async(long id,HttpContext h,AppDbContext d)=>{var x=await d.PersonalFcAccounts.FindAsync(id);if(x==null)return Results.NotFound();if(!Root(h)&&x.OwnerUserId!=U(h))return Results.Forbid();if(await d.PersonalFcTransactions.AnyAsync(t=>t.AccountId==id||t.ToAccountId==id)||await d.PersonalFcClubFunds.AnyAsync(f=>f.DefaultAccountId==id)||await d.PersonalFcClubTransactions.AnyAsync(t=>t.AccountId==id))return Results.Conflict(new{message="Account is in use. Merge it into another account before deleting."});d.Remove(x);await d.SaveChangesAsync();return Results.NoContent();});

  app.MapGet("/api/personal-fc/transactions",async(HttpContext h,AppDbContext d)=>{var r=Root(h);var u=U(h);return Results.Ok(await d.PersonalFcTransactions.AsNoTracking().Where(x=>r||x.OwnerUserId==u).OrderByDescending(x=>x.TransactionDate).ThenByDescending(x=>x.Id).Take(1000).ToListAsync());});
  app.MapPost("/api/personal-fc/transactions",async(PersonalFcTransaction x,HttpContext h,AppDbContext d)=>{x.Id=0;x.OwnerUserId=U(h);x.CreatedAt=DateTime.UtcNow;d.Add(x);await d.SaveChangesAsync();return Results.Ok(x);});
  app.MapDelete("/api/personal-fc/transactions/{id:long}",async(long id,HttpContext h,AppDbContext d)=>{var x=await d.PersonalFcTransactions.FindAsync(id);if(x==null)return Results.NotFound();if(!Root(h)&&x.OwnerUserId!=U(h))return Results.Forbid();d.Remove(x);await d.SaveChangesAsync();return Results.NoContent();});

  // PERSONAL_FC_RECORD_ACTIONS_V1
  app.MapPost("/api/personal-fc/transactions/{id:long}/duplicate",async(long id,HttpContext h,AppDbContext d)=>{
   var x=await d.PersonalFcTransactions.AsNoTracking().FirstOrDefaultAsync(t=>t.Id==id);if(x==null)return Results.NotFound();if(!Root(h)&&x.OwnerUserId!=U(h))return Results.Forbid();
   x.Id=0;x.OwnerUserId=U(h);x.Description=$"{x.Description} (Copy)";x.CreatedAt=DateTime.UtcNow;d.Add(x);await d.SaveChangesAsync();return Results.Ok(x);
  });

  app.MapGet("/api/personal-fc/assets",async(HttpContext h,AppDbContext d)=>{var r=Root(h);var u=U(h);return Results.Ok(await d.PersonalFcAssets.AsNoTracking().Where(x=>r||x.OwnerUserId==u).ToListAsync());});
  app.MapPost("/api/personal-fc/assets",async(PersonalFcAsset x,HttpContext h,AppDbContext d)=>{x.Id=0;x.OwnerUserId=U(h);d.Add(x);await d.SaveChangesAsync();return Results.Ok(x);});
  app.MapDelete("/api/personal-fc/assets/{id:long}",async(long id,HttpContext h,AppDbContext d)=>{var x=await d.PersonalFcAssets.FindAsync(id);if(x==null)return Results.NotFound();if(!Root(h)&&x.OwnerUserId!=U(h))return Results.Forbid();d.Remove(x);await d.SaveChangesAsync();return Results.NoContent();});

  app.MapGet("/api/personal-fc/debts",async(HttpContext h,AppDbContext d)=>{var r=Root(h);var u=U(h);return Results.Ok(await d.PersonalFcDebts.AsNoTracking().Where(x=>r||x.OwnerUserId==u).ToListAsync());});
  app.MapPost("/api/personal-fc/debts",async(PersonalFcDebt x,HttpContext h,AppDbContext d)=>{x.Id=0;x.OwnerUserId=U(h);d.Add(x);await d.SaveChangesAsync();return Results.Ok(x);});
  app.MapDelete("/api/personal-fc/debts/{id:long}",async(long id,HttpContext h,AppDbContext d)=>{var x=await d.PersonalFcDebts.FindAsync(id);if(x==null)return Results.NotFound();if(!Root(h)&&x.OwnerUserId!=U(h))return Results.Forbid();d.Remove(x);await d.SaveChangesAsync();return Results.NoContent();});
  app.MapPost("/api/personal-fc/debts/{id:long}/close",async(long id,HttpContext h,AppDbContext d)=>{var x=await d.PersonalFcDebts.FindAsync(id);if(x==null)return Results.NotFound();if(!Root(h)&&x.OwnerUserId!=U(h))return Results.Forbid();x.Status="CLOSED";await d.SaveChangesAsync();return Results.Ok(x);});

  // PERSONAL_FC_SAFE_CATEGORY_DELETE_V1
  app.MapDelete("/api/personal-fc/categories/{id:long}",async(long id,HttpContext h,AppDbContext d)=>{
   var x=await d.PersonalFcCategories.FindAsync(id);if(x==null)return Results.NotFound();
   if(x.OwnerUserId==null&&!Root(h))return Results.Json(new{message="This is a system Category. Only ROOT can delete it."},statusCode:403);
   if(x.OwnerUserId!=null&&!Root(h)&&x.OwnerUserId!=U(h))return Results.Json(new{message="You do not have permission to delete this Category."},statusCode:403);
   var fallback=await d.PersonalFcCategories.FirstOrDefaultAsync(c=>c.Id!=id&&c.Active&&c.Type==x.Type&&c.CategoryName=="Other"&&(c.OwnerUserId==null||c.OwnerUserId==x.OwnerUserId));
   var personalRows=await d.PersonalFcTransactions.Where(t=>t.CategoryId==id).ToListAsync();
   foreach(var row in personalRows)row.CategoryId=fallback?.Id;
   var clubQuery=d.PersonalFcClubTransactions.Where(t=>t.Category==x.CategoryName);
   if(!Root(h)){var userId=U(h);var managedClubIds=d.PersonalFcClubs.Where(c=>c.OwnerUserId==userId).Select(c=>c.Id).Union(d.PersonalFcClubMembers.Where(m=>m.UserId==userId&&(m.MemberRole=="ADMIN"||m.MemberRole=="TREASURER")).Select(m=>m.ClubId));clubQuery=clubQuery.Where(t=>managedClubIds.Contains(t.ClubId));}
   var clubRows=await clubQuery.ToListAsync();
   foreach(var row in clubRows)row.Category=fallback?.CategoryName??"Other";
   d.Remove(x);await d.SaveChangesAsync();
   return Results.Ok(new{deleted=true,reassignedPersonalTransactions=personalRows.Count,reassignedClubTransactions=clubRows.Count,reassignedTo=fallback?.CategoryName??"Other"});
  });
  app.MapPost("/api/personal-fc/categories/{id:long}/deactivate",async(long id,HttpContext h,AppDbContext d)=>{var x=await d.PersonalFcCategories.FindAsync(id);if(x==null)return Results.NotFound();if(x.OwnerUserId==null&&!Root(h))return Results.Forbid();if(x.OwnerUserId!=null&&!Root(h)&&x.OwnerUserId!=U(h))return Results.Forbid();x.Active=false;await d.SaveChangesAsync();return Results.Ok(x);});

  app.MapPost("/api/personal-fc/accounts/{sourceId:long}/merge/{targetId:long}",async(long sourceId,long targetId,HttpContext h,AppDbContext d)=>{
   if(sourceId==targetId)return Results.BadRequest(new{message="Source and target must be different."});var s=await d.PersonalFcAccounts.FindAsync(sourceId);var t=await d.PersonalFcAccounts.FindAsync(targetId);if(s==null||t==null)return Results.NotFound();if(!Root(h)&&(s.OwnerUserId!=U(h)||t.OwnerUserId!=U(h)))return Results.Forbid();if(!string.Equals(s.Currency,t.Currency,StringComparison.OrdinalIgnoreCase))return Results.BadRequest(new{message="Accounts must use the same currency."});
   await using var tr=await d.Database.BeginTransactionAsync();var rows=await d.PersonalFcTransactions.Where(x=>x.AccountId==sourceId||x.ToAccountId==sourceId).ToListAsync();foreach(var x in rows){if(x.AccountId==sourceId)x.AccountId=targetId;if(x.ToAccountId==sourceId)x.ToAccountId=targetId;}var funds=await d.PersonalFcClubFunds.Where(x=>x.DefaultAccountId==sourceId).ToListAsync();foreach(var x in funds)x.DefaultAccountId=targetId;var clubTx=await d.PersonalFcClubTransactions.Where(x=>x.AccountId==sourceId).ToListAsync();foreach(var x in clubTx)x.AccountId=targetId;t.OpeningBalance+=s.OpeningBalance;t.UpdatedAt=DateTime.UtcNow;d.Remove(s);await d.SaveChangesAsync();await tr.CommitAsync();return Results.Ok(new{merged=true,sourceId,targetId});
  });
  app.MapPost("/api/personal-fc/assets/{sourceId:long}/merge/{targetId:long}",async(long sourceId,long targetId,HttpContext h,AppDbContext d)=>{if(sourceId==targetId)return Results.BadRequest(new{message="Source and target must be different."});var s=await d.PersonalFcAssets.FindAsync(sourceId);var t=await d.PersonalFcAssets.FindAsync(targetId);if(s==null||t==null)return Results.NotFound();if(!Root(h)&&(s.OwnerUserId!=U(h)||t.OwnerUserId!=U(h)))return Results.Forbid();if(!string.Equals(s.Currency,t.Currency,StringComparison.OrdinalIgnoreCase))return Results.BadRequest(new{message="Assets must use the same currency."});t.CostValue+=s.CostValue;t.CurrentValue+=s.CurrentValue;t.Notes=$"{t.Notes}\nMerged: {s.Name}. {s.Notes}".Trim();d.Remove(s);await d.SaveChangesAsync();return Results.Ok(new{merged=true,sourceId,targetId});});
  app.MapPost("/api/personal-fc/debts/{sourceId:long}/merge/{targetId:long}",async(long sourceId,long targetId,HttpContext h,AppDbContext d)=>{if(sourceId==targetId)return Results.BadRequest(new{message="Source and target must be different."});var s=await d.PersonalFcDebts.FindAsync(sourceId);var t=await d.PersonalFcDebts.FindAsync(targetId);if(s==null||t==null)return Results.NotFound();if(!Root(h)&&(s.OwnerUserId!=U(h)||t.OwnerUserId!=U(h)))return Results.Forbid();if(!string.Equals(s.Currency,t.Currency,StringComparison.OrdinalIgnoreCase))return Results.BadRequest(new{message="Debts must use the same currency."});var total=t.OutstandingAmount+s.OutstandingAmount;if(total>0)t.InterestRate=(t.InterestRate*t.OutstandingAmount+s.InterestRate*s.OutstandingAmount)/total;t.OriginalAmount+=s.OriginalAmount;t.OutstandingAmount=total;t.Notes=$"{t.Notes}\nMerged: {s.Name}. {s.Notes}".Trim();d.Remove(s);await d.SaveChangesAsync();return Results.Ok(new{merged=true,sourceId,targetId});});
  app.MapPost("/api/personal-fc/categories/{sourceId:long}/merge/{targetId:long}",async(long sourceId,long targetId,HttpContext h,AppDbContext d)=>{if(sourceId==targetId)return Results.BadRequest(new{message="Source and target must be different."});var s=await d.PersonalFcCategories.FindAsync(sourceId);var t=await d.PersonalFcCategories.FindAsync(targetId);if(s==null||t==null)return Results.NotFound();if(!Root(h)&&((s.OwnerUserId==null||s.OwnerUserId!=U(h))||(t.OwnerUserId!=null&&t.OwnerUserId!=U(h))))return Results.Forbid();if(!string.Equals(s.Type,t.Type,StringComparison.OrdinalIgnoreCase))return Results.BadRequest(new{message="Categories must have the same type."});var rows=await d.PersonalFcTransactions.Where(x=>x.CategoryId==sourceId).ToListAsync();foreach(var x in rows)x.CategoryId=targetId;d.Remove(s);await d.SaveChangesAsync();return Results.Ok(new{merged=true,sourceId,targetId});});

  app.MapGet("/api/personal-fc/clubs",async(HttpContext h,AppDbContext d)=>{var r=Root(h);var u=U(h);var ids=d.PersonalFcClubMembers.Where(x=>x.UserId==u&&x.Status=="ACTIVE").Select(x=>x.ClubId);return Results.Ok(await d.PersonalFcClubs.AsNoTracking().Where(x=>r||x.OwnerUserId==u||ids.Contains(x.Id)).OrderBy(x=>x.Name).ToListAsync());});
  app.MapPost("/api/personal-fc/clubs",async(PersonalFcClub x,HttpContext h,AppDbContext d)=>{x.Id=0;x.OwnerUserId=U(h);d.Add(x);await d.SaveChangesAsync();return Results.Ok(x);});
  app.MapGet("/api/personal-fc/clubs/{id:long}/detail",async(long id,HttpContext h,AppDbContext d)=>{
   if(!await ClubRead(d,h,id))return Results.Forbid(); var c=await d.PersonalFcClubs.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id);if(c==null)return Results.NotFound();
   var m=await d.PersonalFcClubMembers.AsNoTracking().Where(x=>x.ClubId==id).ToListAsync(); var f=await d.PersonalFcClubFunds.AsNoTracking().Where(x=>x.ClubId==id).ToListAsync();
   var sp=await d.PersonalFcClubSponsors.AsNoTracking().Where(x=>x.ClubId==id).ToListAsync(); var ev=await d.PersonalFcClubEvents.AsNoTracking().Where(x=>x.ClubId==id).OrderByDescending(x=>x.EventDate).Take(20).ToListAsync();
   var q=d.PersonalFcClubTransactions.AsNoTracking().Where(x=>x.ClubId==id); var inc=await q.Where(x=>x.TransactionType=="INCOME").SumAsync(x=>(decimal?)x.Amount)??0; var exp=await q.Where(x=>x.TransactionType=="EXPENSE").SumAsync(x=>(decimal?)x.Amount)??0;
   var bal=f.Sum(x=>x.OpeningBalance)+inc-exp; var active=m.Count(x=>string.Equals(x.Status,"ACTIVE",StringComparison.OrdinalIgnoreCase)); var upcoming=ev.Count(x=>x.EventDate>=DateOnly.FromDateTime(DateTime.Today)&&!string.Equals(x.Status,"COMPLETED",StringComparison.OrdinalIgnoreCase));
   return Results.Ok(new{club=c,members=m,funds=f,sponsors=sp,events=ev,fundBalance=bal,totalMembers=m.Count,activeMembers=active,upcomingActivities=upcoming});
  });
  app.MapPost("/api/personal-fc/clubs/{id:long}/members",async(long id,PersonalFcClubMember x,HttpContext h,AppDbContext d)=>{if(!await ClubManage(d,h,id))return Results.Forbid();x.Id=0;x.ClubId=id;d.Add(x);await d.SaveChangesAsync();return Results.Ok(x);});
  app.MapPost("/api/personal-fc/clubs/{id:long}/funds",async(long id,PersonalFcClubFund x,HttpContext h,AppDbContext d)=>{if(!await ClubManage(d,h,id))return Results.Forbid();x.Id=0;x.ClubId=id;d.Add(x);await d.SaveChangesAsync();return Results.Ok(x);});
  app.MapPost("/api/personal-fc/clubs/{id:long}/transactions",async(long id,PersonalFcClubTransaction x,HttpContext h,AppDbContext d)=>{if(!await ClubManage(d,h,id))return Results.Forbid();x.Id=0;x.ClubId=id;x.CreatedByUserId=U(h);d.Add(x);await d.SaveChangesAsync();return Results.Ok(x);});
  // CLUB_MEMBER_FEE_PICKLEBALL_CATEGORY_V1
  app.MapPost("/api/personal-fc/clubs/{id:long}/finance/assign-member-fee-category",async(long id,int year,int month,HttpContext h,AppDbContext d)=>{
   if(!await ClubManage(d,h,id))return Results.Forbid();
   if(month<1||month>12||year<2000||year>2200)return Results.BadRequest(new{message="Invalid year or month."});
   var period=$"{year:D4}-{month:D2}";
   var rows=await d.PersonalFcClubTransactions.Where(x=>x.ClubId==id&&x.SourceType=="MEMBER_FEE"&&x.PeriodKey==period).ToListAsync();
   foreach(var x in rows)x.Category="Pickleball";
   await d.SaveChangesAsync();
   return Results.Ok(new{updated=rows.Count,category="Pickleball",period});
  });
  app.MapPost("/api/personal-fc/clubs/{id:long}/sponsors",async(long id,PersonalFcClubSponsor x,HttpContext h,AppDbContext d)=>{if(!await ClubManage(d,h,id))return Results.Forbid();x.Id=0;x.ClubId=id;d.Add(x);await d.SaveChangesAsync();return Results.Ok(x);});
  app.MapPost("/api/personal-fc/clubs/{id:long}/events",async(long id,PersonalFcClubEvent x,HttpContext h,AppDbContext d)=>{if(!await ClubManage(d,h,id))return Results.Forbid();x.Id=0;x.ClubId=id;d.Add(x);await d.SaveChangesAsync();return Results.Ok(x);});
  // PersonalFcFormsV2
  app.MapPut("/api/personal-fc/accounts/{id:long}",async(long id,PersonalFcAccount input,HttpContext h,AppDbContext d)=>{
   var x=await d.PersonalFcAccounts.FindAsync(id); if(x==null)return Results.NotFound(); if(!Root(h)&&x.OwnerUserId!=U(h))return Results.Forbid();
   x.Name=input.Name;x.AccountType=input.AccountType;x.Institution=input.Institution;x.Currency=input.Currency;x.OpeningBalance=input.OpeningBalance;x.IncludeInNetWorth=input.IncludeInNetWorth;x.Status=input.Status;x.Notes=input.Notes;x.UpdatedAt=DateTime.UtcNow;
   await d.SaveChangesAsync();return Results.Ok(x);
  });
  app.MapPut("/api/personal-fc/transactions/{id:long}",async(long id,PersonalFcTransaction input,HttpContext h,AppDbContext d)=>{
   var x=await d.PersonalFcTransactions.FindAsync(id); if(x==null)return Results.NotFound(); if(!Root(h)&&x.OwnerUserId!=U(h))return Results.Forbid();
   x.AccountId=input.AccountId;x.ToAccountId=input.ToAccountId;x.CategoryId=input.CategoryId;x.TransactionDate=input.TransactionDate;x.TransactionType=input.TransactionType;x.Amount=input.Amount;x.Currency=input.Currency;x.Description=input.Description;x.Notes=input.Notes;
   await d.SaveChangesAsync();return Results.Ok(x);
  });
  app.MapPut("/api/personal-fc/assets/{id:long}",async(long id,PersonalFcAsset input,HttpContext h,AppDbContext d)=>{
   var x=await d.PersonalFcAssets.FindAsync(id); if(x==null)return Results.NotFound(); if(!Root(h)&&x.OwnerUserId!=U(h))return Results.Forbid();
   x.AssetType=input.AssetType;x.Name=input.Name;x.CostValue=input.CostValue;x.CurrentValue=input.CurrentValue;x.Currency=input.Currency;x.IncludeInNetWorth=input.IncludeInNetWorth;x.Notes=input.Notes;
   await d.SaveChangesAsync();return Results.Ok(x);
  });
  app.MapPut("/api/personal-fc/debts/{id:long}",async(long id,PersonalFcDebt input,HttpContext h,AppDbContext d)=>{
   var x=await d.PersonalFcDebts.FindAsync(id); if(x==null)return Results.NotFound(); if(!Root(h)&&x.OwnerUserId!=U(h))return Results.Forbid();
   x.Name=input.Name;x.Lender=input.Lender;x.OriginalAmount=input.OriginalAmount;x.OutstandingAmount=input.OutstandingAmount;x.InterestRate=input.InterestRate;x.Currency=input.Currency;x.Status=input.Status;x.Notes=input.Notes;
   await d.SaveChangesAsync();return Results.Ok(x);
  });
  app.MapPut("/api/personal-fc/clubs/{id:long}",async(long id,PersonalFcClub input,HttpContext h,AppDbContext d)=>{
   var x=await d.PersonalFcClubs.FindAsync(id); if(x==null)return Results.NotFound(); if(!await ClubManage(d,h,id))return Results.Forbid();
   x.Code=input.Code;x.Name=input.Name;x.ClubType=input.ClubType;x.Status=input.Status;x.Notes=input.Notes;
   await d.SaveChangesAsync();return Results.Ok(x);
  });
  app.MapPut("/api/personal-fc/clubs/{clubId:long}/members/{id:long}",async(long clubId,long id,PersonalFcClubMember input,HttpContext h,AppDbContext d)=>{
   if(!await ClubManage(d,h,clubId))return Results.Forbid(); var x=await d.PersonalFcClubMembers.FindAsync(id); if(x==null||x.ClubId!=clubId)return Results.NotFound();
   x.UserId=input.UserId;x.MemberName=input.MemberName;x.Email=input.Email;x.MemberRole=input.MemberRole;x.MembershipType=input.MembershipType;x.MembershipFee=input.MembershipFee;x.JoinDate=input.JoinDate;x.Status=input.Status;
   await d.SaveChangesAsync();return Results.Ok(x);
  });
  app.MapPut("/api/personal-fc/clubs/{clubId:long}/funds/{id:long}",async(long clubId,long id,PersonalFcClubFund input,HttpContext h,AppDbContext d)=>{
   if(!await ClubManage(d,h,clubId))return Results.Forbid(); var x=await d.PersonalFcClubFunds.FindAsync(id); if(x==null||x.ClubId!=clubId)return Results.NotFound();
   x.Name=input.Name;x.FundType=input.FundType;x.Currency=input.Currency;x.OpeningBalance=input.OpeningBalance;x.Status=input.Status;
   await d.SaveChangesAsync();return Results.Ok(x);
  });
  app.MapPut("/api/personal-fc/clubs/{clubId:long}/sponsors/{id:long}",async(long clubId,long id,PersonalFcClubSponsor input,HttpContext h,AppDbContext d)=>{
   if(!await ClubManage(d,h,clubId))return Results.Forbid(); var x=await d.PersonalFcClubSponsors.FindAsync(id); if(x==null||x.ClubId!=clubId)return Results.NotFound();
   x.SponsorName=input.SponsorName;x.CommittedAmount=input.CommittedAmount;x.ReceivedAmount=input.ReceivedAmount;x.Currency=input.Currency;x.Notes=input.Notes;
   await d.SaveChangesAsync();return Results.Ok(x);
  });
  app.MapPut("/api/personal-fc/clubs/{clubId:long}/events/{id:long}",async(long clubId,long id,PersonalFcClubEvent input,HttpContext h,AppDbContext d)=>{
   if(!await ClubManage(d,h,clubId))return Results.Forbid(); var x=await d.PersonalFcClubEvents.FindAsync(id); if(x==null||x.ClubId!=clubId)return Results.NotFound();
   x.Name=input.Name;x.EventDate=input.EventDate;x.BudgetAmount=input.BudgetAmount;x.RegistrationIncome=input.RegistrationIncome;x.SponsorIncome=input.SponsorIncome;x.ExpenseAmount=input.ExpenseAmount;x.Currency=input.Currency;x.Status=input.Status;x.Notes=input.Notes;
   await d.SaveChangesAsync();return Results.Ok(x);
  });
  app.MapPut("/api/personal-fc/categories/{id:long}",async(long id,PersonalFcCategory input,HttpContext h,AppDbContext d)=>{
   var x=await d.PersonalFcCategories.FindAsync(id); if(x==null)return Results.NotFound();
   if(x.OwnerUserId==null && !Root(h))return Results.Forbid(); if(x.OwnerUserId!=null && !Root(h) && x.OwnerUserId!=U(h))return Results.Forbid();
   x.Code=input.Code;x.Type=input.Type;x.ParentCategory=input.ParentCategory;x.CategoryName=input.CategoryName;x.Icon=input.Icon;x.Active=input.Active;x.SortOrder=input.SortOrder;
   await d.SaveChangesAsync();return Results.Ok(x);
  });

  // PersonalFcOverviewV3
  app.MapGet("/api/personal-fc/overview-detail",async(int? year,int? month,long? accountId,HttpContext h,AppDbContext d)=>{
   var uid=U(h); var root=Root(h); var y=year??DateTime.Today.Year; var m=month??DateTime.Today.Month;
   var aq=d.PersonalFcAccounts.AsNoTracking().Where(x=>root||x.OwnerUserId==uid);
   if(accountId.HasValue) aq=aq.Where(x=>x.Id==accountId.Value);
   var accounts=await aq.OrderBy(x=>x.Name).ToListAsync();

   var tq=d.PersonalFcTransactions.AsNoTracking().Where(x=>root||x.OwnerUserId==uid);
   if(accountId.HasValue) tq=tq.Where(x=>x.AccountId==accountId.Value||x.ToAccountId==accountId.Value);
   var tx=await tq.ToListAsync();

   decimal Signed(PersonalFcTransaction t,long aid){
    var ty=(t.TransactionType??"").ToUpperInvariant();
    if(ty=="TRANSFER"){decimal v=0;if(t.AccountId==aid)v-=t.Amount;if(t.ToAccountId==aid)v+=t.Amount;return v;}
    if(ty=="INCOME")return t.AccountId==aid?t.Amount:0;
    if(ty=="EXPENSE"||ty=="INVESTMENT"||ty=="DEBT_PAYMENT"||ty=="ASSET_PURCHASE")return t.AccountId==aid?-t.Amount:0;
    return 0;
   }

   var accountSummary=accounts.Select(a=>{
    var rows=tx.Where(t=>t.AccountId==a.Id||t.ToAccountId==a.Id).ToList();
    var inc=rows.Where(t=>t.TransactionType=="INCOME"&&t.AccountId==a.Id).Sum(t=>t.Amount);
    var exp=rows.Where(t=>(t.TransactionType=="EXPENSE"||t.TransactionType=="INVESTMENT"||t.TransactionType=="DEBT_PAYMENT"||t.TransactionType=="ASSET_PURCHASE")&&t.AccountId==a.Id).Sum(t=>t.Amount);
    var cur=a.OpeningBalance+rows.Sum(t=>Signed(t,a.Id));
    return new{a.Id,a.Name,a.AccountType,a.Institution,a.Currency,openingBalance=a.OpeningBalance,income=inc,expense=exp,currentBalance=cur};
   }).ToList();

   var selected=tx.Where(t=>t.TransactionDate.Year==y&&t.TransactionDate.Month==m).ToList();
   var prevDate=new DateTime(y,m,1).AddMonths(-1);
   var prev=tx.Where(t=>t.TransactionDate.Year==prevDate.Year&&t.TransactionDate.Month==prevDate.Month).ToList();
   decimal Inc(List<PersonalFcTransaction> r)=>r.Where(t=>t.TransactionType=="INCOME").Sum(t=>t.Amount);
   decimal Exp(List<PersonalFcTransaction> r)=>r.Where(t=>t.TransactionType=="EXPENSE").Sum(t=>t.Amount);
   decimal Pct(decimal cur,decimal prv)=>prv==0?(cur==0?0:100):Math.Round((cur-prv)/Math.Abs(prv)*100,1);

   var totalIncome=Inc(selected); var totalExpense=Exp(selected);
   var totalBalance=accountSummary.Sum(x=>x.currentBalance);
   var previousBalance=accounts.Sum(a=>{
    var rows=tx.Where(t=>(t.AccountId==a.Id||t.ToAccountId==a.Id)&&(t.TransactionDate.Year<y||(t.TransactionDate.Year==y&&t.TransactionDate.Month<m))).ToList();
    return a.OpeningBalance+rows.Sum(t=>Signed(t,a.Id));
   });

   var monthly=Enumerable.Range(1,12).Select(mm=>{
    var rows=tx.Where(t=>t.TransactionDate.Year==y&&t.TransactionDate.Month==mm).ToList();
    var inc=Inc(rows);var exp=Exp(rows);var end=new DateOnly(y,mm,DateTime.DaysInMonth(y,mm));
    var bal=accounts.Sum(a=>a.OpeningBalance+tx.Where(t=>(t.AccountId==a.Id||t.ToAccountId==a.Id)&&t.TransactionDate<=end).Sum(t=>Signed(t,a.Id)));
    return new{month=mm,income=inc,expense=exp,balance=bal,net=inc-exp};
   }).ToList();

   var cats=await d.PersonalFcCategories.AsNoTracking().ToDictionaryAsync(x=>x.Id,x=>x.CategoryName);
   var expenseByCategory=selected.Where(t=>t.TransactionType=="EXPENSE").GroupBy(t=>t.CategoryId).Select(g=>new{
    categoryId=g.Key,category=g.Key.HasValue&&cats.TryGetValue(g.Key.Value,out var nm)?nm:"Other",amount=g.Sum(x=>x.Amount)
   }).OrderByDescending(x=>x.amount).Take(8).ToList();

   var changes=selected.OrderBy(t=>t.TransactionDate).ThenBy(t=>t.Id).Select(t=>new{
    id=t.Id,date=t.TransactionDate,description=string.IsNullOrWhiteSpace(t.Description)?t.TransactionType:t.Description,
    amount=t.TransactionType=="INCOME"?t.Amount:t.TransactionType=="TRANSFER"?0:-t.Amount,type=t.TransactionType
   }).ToList();

   return Results.Ok(new{
    year=y,month=m,totalBalance,totalIncome,totalExpense,
    balanceChangePct=Pct(totalBalance,previousBalance),incomeChangePct=Pct(totalIncome,Inc(prev)),expenseChangePct=Pct(totalExpense,Exp(prev)),
    monthly,expenseByCategory,accountSummary,changes
   });
  });

 }
}
