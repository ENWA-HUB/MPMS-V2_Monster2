using MAIPT.PM.Api.Data; using MAIPT.PM.Api.Models; using Microsoft.EntityFrameworkCore;
namespace MAIPT.PM.Api.Endpoints;
public static class ClubTournamentEndpoints {
 static long U(HttpContext h)=>Convert.ToInt64(h.Items["AuthUserId"]);
 static bool Root(HttpContext h)=>string.Equals(Convert.ToString(h.Items["AuthUserRole"]),"ROOT",StringComparison.OrdinalIgnoreCase);
 static string MemberCode(string? value)=>(value??"").Trim().ToUpperInvariant();
 static async Task<bool> Read(AppDbContext d,HttpContext h,long c)=>Root(h)||await d.PersonalFcClubs.AnyAsync(x=>x.Id==c&&x.OwnerUserId==U(h))||await d.PersonalFcClubMembers.AnyAsync(x=>x.ClubId==c&&x.UserId==U(h)&&x.Status=="ACTIVE");
 static async Task<bool> Manage(AppDbContext d,HttpContext h,long c)=>Root(h)||await d.PersonalFcClubs.AnyAsync(x=>x.Id==c&&x.OwnerUserId==U(h))||await d.PersonalFcClubMembers.AnyAsync(x=>x.ClubId==c&&x.UserId==U(h)&&(x.MemberRole=="ADMIN"||x.MemberRole=="TREASURER"));
 // TOURNAMENT_SAFE_CASCADE_DELETE_V1
 static async Task<IResult> DeleteTournamentAsync(WebApplication app,AppDbContext d,PersonalFcTournament tour){
  await using var tx=await d.Database.BeginTransactionAsync();
  try{
   var matches=await d.PersonalFcTournamentMatches.Where(x=>x.TournamentId==tour.Id).ToListAsync();
   if(matches.Count>0){d.PersonalFcTournamentMatches.RemoveRange(matches);await d.SaveChangesAsync();}
   var teams=await d.PersonalFcTournamentTeams.Where(x=>x.TournamentId==tour.Id).ToListAsync();
   if(teams.Count>0){d.PersonalFcTournamentTeams.RemoveRange(teams);await d.SaveChangesAsync();}
   var registrations=await d.PersonalFcTournamentRegistrations.Where(x=>x.TournamentId==tour.Id).ToListAsync();
   if(registrations.Count>0){d.PersonalFcTournamentRegistrations.RemoveRange(registrations);await d.SaveChangesAsync();}
   d.PersonalFcTournaments.Remove(tour);
   await d.SaveChangesAsync();
   await tx.CommitAsync();
   return Results.Ok(new{deleted=true,id=tour.Id,code=tour.Code,name=tour.Name,removedMatches=matches.Count,removedTeams=teams.Count,removedRegistrations=registrations.Count});
  }catch(Exception ex){
   await tx.RollbackAsync();
   app.Logger.LogError(ex,"Delete tournament failed. ClubId={ClubId}, TournamentId={TournamentId}, Code={Code}",tour.ClubId,tour.Id,tour.Code);
   return Results.Problem($"Tournament delete failed: {ex.GetBaseException().Message}",statusCode:500);
  }
 }
 public static void MapClubTournamentEndpoints(this WebApplication app){
  app.MapGet("/api/personal-fc/clubs/{clubId:long}/directory",async(long clubId,HttpContext h,AppDbContext d)=>!await Read(d,h,clubId)?Results.Forbid():Results.Ok(await d.PersonalFcClubMembers.AsNoTracking().Where(x=>x.ClubId==clubId).OrderBy(x=>x.MemberCode).ThenBy(x=>x.MemberName).ToListAsync()));
  
  app.MapPost("/api/personal-fc/clubs/{clubId:long}/directory",async(long clubId,PersonalFcClubMember x,HttpContext h,AppDbContext d)=>{
   if(!await Manage(d,h,clubId))return Results.Forbid();
   x.MemberCode=MemberCode(x.MemberCode);
   if(string.IsNullOrWhiteSpace(x.MemberCode))return Results.BadRequest(new{message="Member code is required."});
   if(await d.PersonalFcClubMembers.AnyAsync(m=>m.ClubId==clubId&&m.MemberCode.ToUpper()==x.MemberCode))
      return Results.Conflict(new{message=$"Member code '{x.MemberCode}' already exists in this club. Please use a different code or edit the existing member."});
   x.Id=0; x.ClubId=clubId;
   if(string.IsNullOrWhiteSpace(x.Status))x.Status="ACTIVE";
   if(string.IsNullOrWhiteSpace(x.RegistrationStatus))x.RegistrationStatus="APPROVED";
   if(!x.UserId.HasValue && !string.IsNullOrWhiteSpace(x.Email)){
    var email=x.Email.Trim().ToLower();
    var linked=await d.Users.AsNoTracking().FirstOrDefaultAsync(u=>u.Email.ToLower()==email);
    if(linked!=null)x.UserId=linked.Id;
   }
   d.PersonalFcClubMembers.Add(x);
   try{await d.SaveChangesAsync();}
   catch(DbUpdateException){return Results.Conflict(new{message=$"Member code '{x.MemberCode}' already exists in this club."});}
   return Results.Ok(x);
  });

  app.MapPut("/api/personal-fc/clubs/{clubId:long}/directory/{id:long}",async(long clubId,long id,PersonalFcClubMember input,HttpContext h,AppDbContext d)=>{
   if(!await Manage(d,h,clubId))return Results.Forbid();
   var x=await d.PersonalFcClubMembers.FirstOrDefaultAsync(m=>m.Id==id&&m.ClubId==clubId);
   if(x==null)return Results.NotFound();
   var memberCode=MemberCode(input.MemberCode);
   if(string.IsNullOrWhiteSpace(memberCode))return Results.BadRequest(new{message="Member code is required."});
   if(await d.PersonalFcClubMembers.AnyAsync(m=>m.ClubId==clubId&&m.Id!=id&&m.MemberCode.ToUpper()==memberCode))
      return Results.Conflict(new{message=$"Member code '{memberCode}' already exists in this club. Please use a different code."});
   x.MemberCode=memberCode;
   x.MemberName=input.MemberName;
   x.CellPhone=input.CellPhone;
   x.Email=input.Email;
   if(input.UserId.HasValue)x.UserId=input.UserId;
   else if(!string.IsNullOrWhiteSpace(input.Email)){
    var email=input.Email.Trim().ToLower();
    var linked=await d.Users.AsNoTracking().FirstOrDefaultAsync(u=>u.Email.ToLower()==email);
    if(linked!=null)x.UserId=linked.Id;
   }
   x.Sex=input.Sex;
   x.SkillRank=input.SkillRank;
   x.BirthDate=input.BirthDate;
   x.JoinDate=input.JoinDate;
   x.MemberRole=input.MemberRole;
   x.MembershipType=input.MembershipType;
   x.MembershipFee=input.MembershipFee;
   x.MonthlyFeeAmount=input.MonthlyFeeAmount;
   x.RecurringFeeEnabled=input.RecurringFeeEnabled;
   x.RecurringFeeDay=input.RecurringFeeDay;
   x.RegistrationStatus=input.RegistrationStatus;
   x.Status=input.Status;
   x.Active=input.Active;
   x.Notes=input.Notes;
   try{await d.SaveChangesAsync();}
   catch(DbUpdateException){return Results.Conflict(new{message=$"Member code '{memberCode}' already exists in this club."});}
   return Results.Ok(x);
  });

  app.MapDelete("/api/personal-fc/clubs/{clubId:long}/directory/{id:long}",async(long clubId,long id,HttpContext h,AppDbContext d)=>{
   if(!await Manage(d,h,clubId))return Results.Forbid();
   var x=await d.PersonalFcClubMembers.FirstOrDefaultAsync(m=>m.Id==id&&m.ClubId==clubId);
   if(x==null)return Results.NotFound(new{message="Club member not found."});
   await using var tx=await d.Database.BeginTransactionAsync();
   try{
    // CLUB_MEMBER_SAFE_DELETE_V1: keep historical tournament rows, only unlink this directory member.
    var registrations=await d.PersonalFcTournamentRegistrations.Where(r=>r.MemberId==id).ToListAsync();
    foreach(var registration in registrations)registration.MemberId=null;
    var teams=await d.PersonalFcTournamentTeams.Where(t=>t.Member1Id==id||t.Member2Id==id).ToListAsync();
    foreach(var team in teams){if(team.Member1Id==id)team.Member1Id=null;if(team.Member2Id==id)team.Member2Id=null;}
    if(registrations.Count>0||teams.Count>0)await d.SaveChangesAsync();
    d.PersonalFcClubMembers.Remove(x);
    await d.SaveChangesAsync();
    await tx.CommitAsync();
    return Results.Ok(new{deleted=true,id,code=x.MemberCode,unlinkedTournamentRegistrations=registrations.Count,unlinkedTournamentTeams=teams.Count});
   }catch(Exception ex){
    await tx.RollbackAsync();
    app.Logger.LogError(ex,"Delete club member failed. ClubId={ClubId}, MemberId={MemberId}, Code={Code}",clubId,id,x.MemberCode);
    return Results.Problem($"Club member delete failed: {ex.GetBaseException().Message}",statusCode:500);
   }
  });


  app.MapPost("/api/personal-fc/clubs/{clubId:long}/directory/{id:long}/approve",async(long clubId,long id,HttpContext h,AppDbContext d)=>{
   if(!await Manage(d,h,clubId))return Results.Forbid();
   var x=await d.PersonalFcClubMembers.FirstOrDefaultAsync(m=>m.Id==id&&m.ClubId==clubId);
   if(x==null)return Results.NotFound();
   x.RegistrationStatus="APPROVED";
   x.Status="ACTIVE";
   x.Active=true;
   await d.SaveChangesAsync();
   return Results.Ok(x);
  });


  app.MapPost("/api/personal-fc/clubs/{clubId:long}/directory/{id:long}/reject",async(long clubId,long id,HttpContext h,AppDbContext d)=>{
   if(!await Manage(d,h,clubId))return Results.Forbid();
   var x=await d.PersonalFcClubMembers.FirstOrDefaultAsync(m=>m.Id==id&&m.ClubId==clubId);
   if(x==null)return Results.NotFound();
   x.RegistrationStatus="REJECTED";
   x.Status="INACTIVE";
   x.Active=false;
   await d.SaveChangesAsync();
   return Results.Ok(x);
  });


  app.MapPost("/api/personal-fc/clubs/{clubId:long}/directory/{id:long}/activate",async(long clubId,long id,HttpContext h,AppDbContext d)=>{
   if(!await Manage(d,h,clubId))return Results.Forbid();
   var x=await d.PersonalFcClubMembers.FirstOrDefaultAsync(m=>m.Id==id&&m.ClubId==clubId);
   if(x==null)return Results.NotFound();
   
   x.Status="ACTIVE";
   x.Active=true;
   await d.SaveChangesAsync();
   return Results.Ok(x);
  });


  app.MapPost("/api/personal-fc/clubs/{clubId:long}/directory/{id:long}/deactivate",async(long clubId,long id,HttpContext h,AppDbContext d)=>{
   if(!await Manage(d,h,clubId))return Results.Forbid();
   var x=await d.PersonalFcClubMembers.FirstOrDefaultAsync(m=>m.Id==id&&m.ClubId==clubId);
   if(x==null)return Results.NotFound();
   
   x.Status="INACTIVE";
   x.Active=false;
   await d.SaveChangesAsync();
   return Results.Ok(x);
  });
app.MapPost("/api/personal-fc/clubs/{clubId:long}/directory/link-users",async(long clubId,HttpContext h,AppDbContext d)=>{
   if(!await Manage(d,h,clubId))return Results.Forbid();
   var rows=await d.PersonalFcClubMembers.Where(x=>x.ClubId==clubId && x.UserId==null && x.Email!=null && x.Email!="").ToListAsync();
   var users=await d.Users.AsNoTracking().Where(u=>u.Email!=null && u.Email!="").ToListAsync();
   var byEmail=users.GroupBy(u=>u.Email.Trim().ToLower()).ToDictionary(g=>g.Key,g=>g.First().Id);
   var linked=0;
   foreach(var x in rows){
    var key=x.Email.Trim().ToLower();
    if(byEmail.TryGetValue(key,out var uid)){x.UserId=uid;linked++;}
   }
   await d.SaveChangesAsync();
   return Results.Ok(new{clubId,checkedRows=rows.Count,linked});
  });

  app.MapGet("/api/personal-fc/clubs/{clubId:long}/tournaments",async(long clubId,HttpContext h,AppDbContext d)=>!await Read(d,h,clubId)?Results.Forbid():Results.Ok(await d.PersonalFcTournaments.AsNoTracking().Where(x=>x.ClubId==clubId).OrderByDescending(x=>x.TournamentDate).ThenByDescending(x=>x.Id).ToListAsync()));
  app.MapPost("/api/personal-fc/clubs/{clubId:long}/tournaments",async(long clubId,PersonalFcTournament x,HttpContext h,AppDbContext d)=>{
   if(!await Manage(d,h,clubId))return Results.Forbid();
   x.Code=(x.Code??"").Trim().ToUpperInvariant();
   x.Name=(x.Name??"").Trim();
   if(string.IsNullOrWhiteSpace(x.Code))return Results.BadRequest(new{message="Tournament code is required."});
   if(await d.PersonalFcTournaments.AnyAsync(t=>t.ClubId==clubId&&t.Code==x.Code))
      return Results.Conflict(new{message=$"Tournament code '{x.Code}' already exists. Use Edit instead of creating another tournament."});
   x.Id=0;x.ClubId=clubId;
   d.Add(x);await d.SaveChangesAsync();
   return Results.Ok(x);
  });

  // CLUB_TOURNAMENT_RECORD_ACTIONS_V1
  app.MapPost("/api/personal-fc/clubs/{clubId:long}/tournaments/{id:long}/duplicate",async(long clubId,long id,HttpContext h,AppDbContext d)=>{
   if(!await Manage(d,h,clubId))return Results.Forbid();var s=await d.PersonalFcTournaments.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id&&x.ClubId==clubId);if(s==null)return Results.NotFound();
   var x=new PersonalFcTournament{ClubId=clubId,Code=$"{s.Code}-COPY-{DateTime.UtcNow:HHmmss}",Name=$"{s.Name} (Copy)",TournamentDate=s.TournamentDate,Season=s.Season,Venue=s.Venue,Status="DRAFT",Format=s.Format,Notes=s.Notes};d.Add(x);await d.SaveChangesAsync();return Results.Ok(x);
  });
  app.MapPost("/api/personal-fc/clubs/{clubId:long}/tournaments/{sourceId:long}/merge/{targetId:long}",async(long clubId,long sourceId,long targetId,HttpContext h,AppDbContext d)=>{
   if(!await Manage(d,h,clubId))return Results.Forbid();if(sourceId==targetId)return Results.BadRequest(new{message="Source and target must be different."});var s=await d.PersonalFcTournaments.FindAsync(sourceId);var t=await d.PersonalFcTournaments.FindAsync(targetId);if(s==null||t==null||s.ClubId!=clubId||t.ClubId!=clubId)return Results.NotFound();
   var sr=await d.PersonalFcTournamentRegistrations.Where(x=>x.TournamentId==sourceId).ToListAsync();var targetMemberIds=await d.PersonalFcTournamentRegistrations.Where(x=>x.TournamentId==targetId&&x.MemberId!=null).Select(x=>x.MemberId).ToListAsync();if(sr.Any(x=>x.MemberId!=null&&targetMemberIds.Contains(x.MemberId)))return Results.Conflict(new{message="The tournaments contain duplicate members. Remove duplicates before merging."});var st=await d.PersonalFcTournamentTeams.Where(x=>x.TournamentId==sourceId).ToListAsync();var targetCodes=await d.PersonalFcTournamentTeams.Where(x=>x.TournamentId==targetId).Select(x=>x.TeamCode).ToListAsync();if(st.Any(x=>targetCodes.Any(c=>string.Equals(c,x.TeamCode,StringComparison.OrdinalIgnoreCase))))return Results.Conflict(new{message="The tournaments contain duplicate team codes. Rename them before merging."});
   await using var tr=await d.Database.BeginTransactionAsync();foreach(var x in sr)x.TournamentId=targetId;foreach(var x in st)x.TournamentId=targetId;var sm=await d.PersonalFcTournamentMatches.Where(x=>x.TournamentId==sourceId).ToListAsync();foreach(var x in sm)x.TournamentId=targetId;t.Notes=$"{t.Notes}\nMerged tournament: {s.Name} ({s.Code}). {s.Notes}".Trim();d.Remove(s);await d.SaveChangesAsync();await tr.CommitAsync();return Results.Ok(new{merged=true,sourceId,targetId,registrations=sr.Count,teams=st.Count,matches=sm.Count});
  });
  
  app.MapPut("/api/personal-fc/clubs/{clubId:long}/tournaments/{id:long}",async(long clubId,long id,PersonalFcTournament input,HttpContext h,AppDbContext d)=>{
   if(!await Manage(d,h,clubId))return Results.Forbid();
   var x=await d.PersonalFcTournaments.FirstOrDefaultAsync(t=>t.Id==id&&t.ClubId==clubId);
   if(x==null)return Results.NotFound();
   input.Code=(input.Code??"").Trim().ToUpperInvariant();
   input.Name=(input.Name??"").Trim();
   if(string.IsNullOrWhiteSpace(input.Code))return Results.BadRequest(new{message="Tournament code is required."});
   if(await d.PersonalFcTournaments.AnyAsync(t=>t.ClubId==clubId&&t.Id!=id&&t.Code==input.Code))
      return Results.Conflict(new{message=$"Tournament code '{input.Code}' already belongs to another tournament."});
   x.Code=input.Code;
   x.Name=input.Name;
   x.TournamentDate=input.TournamentDate;
   x.Season=input.Season;
   x.Venue=input.Venue;
   x.Status=input.Status;
   x.Format=input.Format;
   x.Notes=input.Notes;
   await d.SaveChangesAsync();
   return Results.Ok(x);
  });

  app.MapDelete("/api/personal-fc/clubs/{clubId:long}/tournaments/{id:long}",
  async(long clubId,long id,HttpContext h,AppDbContext d)=>{
   if(!await Manage(d,h,clubId))return Results.Forbid();
   var tour=await d.PersonalFcTournaments
     .FirstOrDefaultAsync(x=>x.Id==id&&x.ClubId==clubId);
   if(tour==null)return Results.NotFound(new{message="Tournament not found."});
   return await DeleteTournamentAsync(app,d,tour);
  });

  app.MapDelete("/api/personal-fc/clubs/{clubId:long}/tournaments/by-code/{code}",
  async(long clubId,string code,HttpContext h,AppDbContext d)=>{
   if(!await Manage(d,h,clubId))return Results.Forbid();
   var normalized=(code??"").Trim().ToUpperInvariant();
   if(string.IsNullOrWhiteSpace(normalized))return Results.BadRequest(new{message="Tournament code is required."});
   var tours=await d.PersonalFcTournaments.Where(x=>x.ClubId==clubId&&x.Code.ToUpper()==normalized).ToListAsync();
   if(tours.Count==0)return Results.NotFound(new{message=$"Tournament code '{normalized}' was not found in this club."});
   if(tours.Count>1)return Results.Conflict(new{message=$"More than one tournament has code '{normalized}'. Delete by ID to avoid deleting the wrong record.",ids=tours.Select(x=>x.Id)});
   return await DeleteTournamentAsync(app,d,tours[0]);
  });

  // Allows an authenticated club manager to remove one exact code without first discovering ClubId.
  app.MapDelete("/api/personal-fc/tournaments/by-code/{code}",
  async(string code,HttpContext h,AppDbContext d)=>{
   var normalized=(code??"").Trim().ToUpperInvariant();
   if(string.IsNullOrWhiteSpace(normalized))return Results.BadRequest(new{message="Tournament code is required."});
   var tours=await d.PersonalFcTournaments.Where(x=>x.Code.ToUpper()==normalized).ToListAsync();
   if(tours.Count==0)return Results.NotFound(new{message=$"Tournament code '{normalized}' was not found."});
   var manageable=new List<PersonalFcTournament>();
   foreach(var tour in tours)if(await Manage(d,h,tour.ClubId))manageable.Add(tour);
   if(manageable.Count==0)return Results.Forbid();
   if(manageable.Count>1)return Results.Conflict(new{message=$"More than one manageable tournament has code '{normalized}'. Delete by ClubId and ID to avoid deleting the wrong record.",items=manageable.Select(x=>new{x.Id,x.ClubId,x.Name})});
   return await DeleteTournamentAsync(app,d,manageable[0]);
  });


  app.MapPost("/api/personal-fc/tournaments/{id:long}/activate",async(long id,HttpContext h,AppDbContext d)=>{
   var x=await d.PersonalFcTournaments.FirstOrDefaultAsync(t=>t.Id==id);
   if(x==null)return Results.NotFound();
   if(!await Manage(d,h,x.ClubId))return Results.Forbid();
   x.Status="ACTIVE";
   await d.SaveChangesAsync();
   return Results.Ok(x);
  });


  app.MapPost("/api/personal-fc/tournaments/{id:long}/deactivate",async(long id,HttpContext h,AppDbContext d)=>{
   var x=await d.PersonalFcTournaments.FirstOrDefaultAsync(t=>t.Id==id);
   if(x==null)return Results.NotFound();
   if(!await Manage(d,h,x.ClubId))return Results.Forbid();
   x.Status="INACTIVE";
   await d.SaveChangesAsync();
   return Results.Ok(x);
  });


  app.MapPost("/api/personal-fc/tournaments/{id:long}/complete",async(long id,HttpContext h,AppDbContext d)=>{
   var x=await d.PersonalFcTournaments.FirstOrDefaultAsync(t=>t.Id==id);
   if(x==null)return Results.NotFound();
   if(!await Manage(d,h,x.ClubId))return Results.Forbid();
   x.Status="COMPLETED";
   await d.SaveChangesAsync();
   return Results.Ok(x);
  });


  app.MapPut("/api/personal-fc/tournaments/{id:long}/registrations/{registrationId:long}",async(long id,long registrationId,PersonalFcTournamentRegistration input,HttpContext h,AppDbContext d)=>{
   var t=await d.PersonalFcTournaments.FirstOrDefaultAsync(x=>x.Id==id);
   if(t==null)return Results.NotFound();
   if(!await Manage(d,h,t.ClubId))return Results.Forbid();

   var x=await d.PersonalFcTournamentRegistrations.FirstOrDefaultAsync(r=>r.Id==registrationId&&r.TournamentId==id);
   if(x==null)return Results.NotFound();

   x.MemberId=input.MemberId;
   x.FullName=input.FullName;
   x.Company=input.Company;
   x.Department=input.Department;
   x.Phone=input.Phone;
   x.BirthYear=input.BirthYear;
   x.BirthDate=input.BirthDate;
   x.Sex=input.Sex;
   x.SkillRank=input.SkillRank;
   x.DivisionName=input.DivisionName;
   x.GroupName=input.GroupName;
   x.ShirtSize=input.ShirtSize;
   x.Notes=input.Notes;
   x.OrderNo=input.OrderNo;
   await d.SaveChangesAsync();
   return Results.Ok(x);
  });

  
  // TOURNAMENT_REGISTRATION_DELETE_V12_3
    // TOURNAMENT_REGISTRATION_DELETE_V12_4
  app.MapDelete("/api/personal-fc/tournaments/{id:long}/registrations/{registrationId:long}",
   async(long id,long registrationId,HttpContext h,AppDbContext d)=>{
    var t=await d.PersonalFcTournaments.FindAsync(id);
    if(t==null)return Results.NotFound(new{message="Tournament not found."});
    if(!await Manage(d,h,t.ClubId))return Results.Forbid();

    var reg=await d.PersonalFcTournamentRegistrations
      .AsNoTracking()
      .FirstOrDefaultAsync(x=>x.Id==registrationId&&x.TournamentId==id);

    if(reg==null)
      return Results.NotFound(new{message="Tournament member not found."});

    try
    {
      var affectedTeamIds=await d.PersonalFcTournamentTeams
        .Where(x=>x.TournamentId==id &&
          (x.Registration1Id==registrationId || x.Registration2Id==registrationId))
        .Select(x=>x.Id)
        .ToListAsync();

      if(affectedTeamIds.Count>0)
      {
        var matchesToDelete=await d.PersonalFcTournamentMatches
          .Where(x=>x.TournamentId==id &&
            (
              (x.Team1Id.HasValue && affectedTeamIds.Contains(x.Team1Id.Value)) ||
              (x.Team2Id.HasValue && affectedTeamIds.Contains(x.Team2Id.Value))
            ))
          .ToListAsync();

        if(matchesToDelete.Count>0)
          d.PersonalFcTournamentMatches.RemoveRange(matchesToDelete);

        var teamsToDelete=await d.PersonalFcTournamentTeams
          .Where(x=>x.TournamentId==id && affectedTeamIds.Contains(x.Id))
          .ToListAsync();

        if(teamsToDelete.Count>0)
          d.PersonalFcTournamentTeams.RemoveRange(teamsToDelete);
      }

      var trackedReg=await d.PersonalFcTournamentRegistrations
        .FirstOrDefaultAsync(x=>x.Id==registrationId&&x.TournamentId==id);

      if(trackedReg==null)
        return Results.NotFound(new{message="Tournament member not found."});

      d.PersonalFcTournamentRegistrations.Remove(trackedReg);
      await d.SaveChangesAsync();

      var stillExists=await d.PersonalFcTournamentRegistrations
        .AsNoTracking()
        .AnyAsync(x=>x.Id==registrationId&&x.TournamentId==id);

      if(stillExists)
        return Results.Problem("Tournament member delete did not persist.",statusCode:500);

      return Results.Ok(new{
        deleted=true,
        id=registrationId,
        removedTeams=affectedTeamIds.Count
      });
    }
    catch(Exception ex)
    {
      app.Logger.LogError(
        ex,
        "Delete tournament member failed. TournamentId={TournamentId}, RegistrationId={RegistrationId}",
        id,registrationId
      );

      return Results.Problem(
        $"Tournament member delete failed: {ex.GetBaseException().Message}",
        statusCode:500
      );
    }
   });

app.MapGet("/api/personal-fc/tournaments/{id:long}/participant-candidates",async(long id,HttpContext h,AppDbContext d)=>{
   var t=await d.PersonalFcTournaments.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id);
   if(t==null)return Results.NotFound();
   if(!await Read(d,h,t.ClubId))return Results.Forbid();

   var registeredMemberIds=await d.PersonalFcTournamentRegistrations.AsNoTracking()
    .Where(r=>r.TournamentId==id && r.MemberId!=null)
    .Select(r=>r.MemberId!.Value)
    .ToListAsync();

   var q=d.PersonalFcClubMembers.AsNoTracking()
    .Where(m=>m.ClubId==t.ClubId)
    .Where(m=>m.RegistrationStatus==null || m.RegistrationStatus!="REJECTED")
    .Where(m=>m.Status==null || m.Status=="" || m.Status=="ACTIVE" || m.Active);

   if(registeredMemberIds.Count>0)
    q=q.Where(m=>!registeredMemberIds.Contains(m.Id));

   var rows=await q.OrderBy(m=>m.MemberCode).ThenBy(m=>m.MemberName).ToListAsync();
   return Results.Ok(rows);
  });

  app.MapGet("/api/personal-fc/tournaments/{id:long}/detail",async(long id,HttpContext h,AppDbContext d)=>{var t=await d.PersonalFcTournaments.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id);if(t==null)return Results.NotFound();if(!await Read(d,h,t.ClubId))return Results.Forbid();var r=await d.PersonalFcTournamentRegistrations.AsNoTracking().Where(x=>x.TournamentId==id).OrderBy(x=>x.OrderNo).ThenBy(x=>x.FullName).ToListAsync();var teams=await d.PersonalFcTournamentTeams.AsNoTracking().Where(x=>x.TournamentId==id).ToListAsync();var matches=await d.PersonalFcTournamentMatches.AsNoTracking().Where(x=>x.TournamentId==id).OrderBy(x=>x.SequenceNo).ToListAsync();return Results.Ok(new{tournament=t,registrations=r,teams,matches});});
  app.MapPost("/api/personal-fc/tournaments/{id:long}/registrations",async(long id,PersonalFcTournamentRegistration x,HttpContext h,AppDbContext d)=>{var t=await d.PersonalFcTournaments.FindAsync(id);if(t==null)return Results.NotFound();if(!await Manage(d,h,t.ClubId))return Results.Forbid();x.Id=0;x.TournamentId=id;if(x.MemberId.HasValue&&await d.PersonalFcTournamentRegistrations.AnyAsync(r=>r.TournamentId==id&&r.MemberId==x.MemberId))
     return Results.BadRequest(new{message="Participant already registered in this tournament."});
   d.Add(x);await d.SaveChangesAsync();return Results.Ok(x);});

  app.MapPost("/api/personal-fc/tournaments/{id:long}/auto-pair",async(long id,HttpContext h,AppDbContext d)=>{var t=await d.PersonalFcTournaments.FindAsync(id);if(t==null)return Results.NotFound();if(!await Manage(d,h,t.ClubId))return Results.Forbid();if(await d.PersonalFcTournamentTeams.AnyAsync(x=>x.TournamentId==id))return Results.BadRequest(new{message="Teams already exist"});var regs=await d.PersonalFcTournamentRegistrations.Where(x=>x.TournamentId==id).OrderBy(x=>x.OrderNo).ThenBy(x=>x.Id).ToListAsync();int n=0;foreach(var g in regs.GroupBy(x=>new{x.DivisionName,x.GroupName})){var list=g.ToList();for(int i=0;i<list.Count;i+=2){var a=list[i];var b=i+1<list.Count?list[i+1]:null;n++;d.PersonalFcTournamentTeams.Add(new PersonalFcTournamentTeam{TournamentId=id,DivisionName=g.Key.DivisionName,GroupName=g.Key.GroupName,TeamCode=$"{g.Key.GroupName}-{n:00}",TeamName=b==null?a.FullName:$"{a.FullName} / {b.FullName}",Registration1Id=a.Id,Registration2Id=b?.Id,Member1Id=a.MemberId,Member2Id=b?.MemberId});}}await d.SaveChangesAsync();return Results.Ok(new{created=n});});
  
  // TOURNAMENT_SCORE_RANKING_V10
  app.MapPut("/api/personal-fc/tournaments/{id:long}/matches/{matchId:long}/score",
   async(long id,long matchId,System.Text.Json.JsonElement input,HttpContext h,AppDbContext d)=>{
    var t=await d.PersonalFcTournaments.FindAsync(id);if(t==null)return Results.NotFound();if(!await Manage(d,h,t.ClubId))return Results.Forbid();
    var m=await d.PersonalFcTournamentMatches.FirstOrDefaultAsync(x=>x.Id==matchId&&x.TournamentId==id);if(m==null)return Results.NotFound();
    int? s1=null,s2=null;
    if(input.TryGetProperty("score1",out var a)&&a.ValueKind!=System.Text.Json.JsonValueKind.Null)s1=a.GetInt32();
    if(input.TryGetProperty("score2",out var b)&&b.ValueKind!=System.Text.Json.JsonValueKind.Null)s2=b.GetInt32();
    if((s1.HasValue&&s1.Value<0)||(s2.HasValue&&s2.Value<0))return Results.BadRequest(new{message="Score cannot be negative."});
    if(s1.HasValue&&s2.HasValue&&s1.Value==s2.Value)return Results.BadRequest(new{message="Match cannot end in a draw."});
    m.Score1=s1;m.Score2=s2;m.Status=s1.HasValue&&s2.HasValue?"COMPLETED":"SCHEDULED";await d.SaveChangesAsync();return Results.Ok(m);
  });

  app.MapPost("/api/personal-fc/tournaments/{id:long}/generate-next-round",
   async(long id,System.Text.Json.JsonElement input,HttpContext h,AppDbContext d)=>{
    var t=await d.PersonalFcTournaments.FindAsync(id);if(t==null)return Results.NotFound();if(!await Manage(d,h,t.ClubId))return Results.Forbid();
    var mode=input.TryGetProperty("mode",out var md)?(md.GetString()??"TOP2_CROSS"):"TOP2_CROSS";
    var topN=input.TryGetProperty("topN",out var tn)?Math.Max(1,Math.Min(4,tn.GetInt32())):2;
    var teams=await d.PersonalFcTournamentTeams.Where(x=>x.TournamentId==id).ToListAsync();
    var gm=await d.PersonalFcTournamentMatches.Where(x=>x.TournamentId==id&&x.RoundName=="GROUP").ToListAsync();
    if(gm.Count==0)return Results.BadRequest(new{message="No group-stage matches exist."});
    if(gm.Any(x=>!x.Score1.HasValue||!x.Score2.HasValue))return Results.BadRequest(new{message="Complete all group-stage scores first."});
    if(await d.PersonalFcTournamentMatches.AnyAsync(x=>x.TournamentId==id&&x.RoundName!="GROUP"))return Results.BadRequest(new{message="A next-round bracket already exists."});
    var st=new List<(long TeamId,string Div,string Grp,int W,int PF,int PA,int Diff)>();
    foreach(var g in teams.GroupBy(x=>new{x.DivisionName,x.GroupName}))foreach(var tm in g){
      var ms=gm.Where(m=>m.DivisionName==g.Key.DivisionName&&m.GroupName==g.Key.GroupName&&(m.Team1Id==tm.Id||m.Team2Id==tm.Id)).ToList();int w=0,pf=0,pa=0;
      foreach(var m in ms){var is1=m.Team1Id==tm.Id;var aa=is1?m.Score1!.Value:m.Score2!.Value;var bb=is1?m.Score2!.Value:m.Score1!.Value;pf+=aa;pa+=bb;if(aa>bb)w++;}
      st.Add((tm.Id,g.Key.DivisionName??"",g.Key.GroupName??"",w,pf,pa,pf-pa));
    }
    var q=st.GroupBy(x=>new{x.Div,x.Grp}).SelectMany(g=>g.OrderByDescending(x=>x.W).ThenByDescending(x=>x.Diff).ThenByDescending(x=>x.PF).ThenBy(x=>x.TeamId).Take(topN).Select((x,i)=>new{x.TeamId,x.Div,x.Grp,Rank=i+1})).ToList();
    var pairs=new List<(long A,long B,string Div)>();
    foreach(var div in q.GroupBy(x=>x.Div)){
      var list=div.ToList();
      if(mode=="TOP2_CROSS"&&topN>=2){var groups=list.Select(x=>x.Grp).Distinct().OrderBy(x=>x).ToList();for(int i=0;i+1<groups.Count;i+=2){var ga=groups[i];var gb=groups[i+1];var a1=list.FirstOrDefault(x=>x.Grp==ga&&x.Rank==1);var a2=list.FirstOrDefault(x=>x.Grp==ga&&x.Rank==2);var b1=list.FirstOrDefault(x=>x.Grp==gb&&x.Rank==1);var b2=list.FirstOrDefault(x=>x.Grp==gb&&x.Rank==2);if(a1!=null&&b2!=null)pairs.Add((a1.TeamId,b2.TeamId,div.Key));if(b1!=null&&a2!=null)pairs.Add((b1.TeamId,a2.TeamId,div.Key));}}
      else{var ordered=list.OrderBy(x=>x.Rank).ThenByDescending(x=>st.First(z=>z.TeamId==x.TeamId).Diff).ToList();if(mode=="SEEDED"){int l=0,r=ordered.Count-1;while(l<r){pairs.Add((ordered[l].TeamId,ordered[r].TeamId,div.Key));l++;r--;}}else for(int i=0;i+1<ordered.Count;i+=2)pairs.Add((ordered[i].TeamId,ordered[i+1].TeamId,div.Key));}
    }
    if(pairs.Count==0)return Results.BadRequest(new{message="Not enough qualified teams to create next round."});
    var seq=gm.Max(x=>x.SequenceNo);var rn=pairs.Count<=2?"SEMIFINAL":pairs.Count<=4?"QUARTERFINAL":pairs.Count<=8?"ROUND_OF_16":"KNOCKOUT";
    foreach(var pp in pairs){seq++;d.PersonalFcTournamentMatches.Add(new PersonalFcTournamentMatch{TournamentId=id,DivisionName=pp.Div,GroupName="",RoundName=rn,SequenceNo=seq,Team1Id=pp.A,Team2Id=pp.B,Status="SCHEDULED"});}
    await d.SaveChangesAsync();return Results.Ok(new{created=pairs.Count,round=rn,qualified=q.Count});
  });

app.MapPost("/api/personal-fc/tournaments/{id:long}/generate-round-robin",async(long id,HttpContext h,AppDbContext d)=>{var t=await d.PersonalFcTournaments.FindAsync(id);if(t==null)return Results.NotFound();if(!await Manage(d,h,t.ClubId))return Results.Forbid();if(await d.PersonalFcTournamentMatches.AnyAsync(x=>x.TournamentId==id))return Results.BadRequest(new{message="Matches already exist"});var teams=await d.PersonalFcTournamentTeams.Where(x=>x.TournamentId==id).ToListAsync();int seq=0;foreach(var g in teams.GroupBy(x=>new{x.DivisionName,x.GroupName})){var list=g.ToList();for(int i=0;i<list.Count;i++)for(int j=i+1;j<list.Count;j++){seq++;d.PersonalFcTournamentMatches.Add(new PersonalFcTournamentMatch{TournamentId=id,DivisionName=g.Key.DivisionName,GroupName=g.Key.GroupName,RoundName="GROUP",SequenceNo=seq,Team1Id=list[i].Id,Team2Id=list[j].Id,Status="SCHEDULED"});}}await d.SaveChangesAsync();return Results.Ok(new{created=seq});});
 }
}
