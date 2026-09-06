using System.Text.Json;
using MAIPT.PM.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MAIPT.PM.Api.Data;
public static class ClubTournamentSeedService {
 static string N(string? s)=>new string((s??"").Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
 static string P(string? s)=>new string((s??"").Where(char.IsDigit).ToArray());

 public static async Task SeedAsync(AppDbContext db,IWebHostEnvironment env){
  var club=await db.PersonalFcClubs.FirstOrDefaultAsync(x=>x.Code=="BPC");
  if(club==null){club=new PersonalFcClub{OwnerUserId=1,Code="BPC",Name="B.Pickleball Club",ClubType="SPORT",Status="ACTIVE",Notes="Imported from B.Pickleball workbook"};db.PersonalFcClubs.Add(club);await db.SaveChangesAsync();}

  var seed=Path.Combine(env.ContentRootPath,"data","seeds");
  if(File.Exists(Path.Combine(seed,"BPC_members.json"))){
   var rows=JsonSerializer.Deserialize<List<MemberSeed>>(await File.ReadAllTextAsync(Path.Combine(seed,"BPC_members.json")),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??[];
   foreach(var r in rows){
    if(await db.PersonalFcClubMembers.AnyAsync(x=>x.ClubId==club.Id&&x.MemberCode==r.MemberCode))continue;
    db.PersonalFcClubMembers.Add(new PersonalFcClubMember{ClubId=club.Id,MemberName=r.FullName,Email=r.Email,MemberRole="MEMBER",MembershipType="MONTHLY",MembershipFee=r.MonthlyFeeAmount,JoinDate=DateOnly.TryParse(r.JoinedAt,out var j)?j:null,Status=r.Active?"ACTIVE":"INACTIVE",MemberCode=r.MemberCode,CellPhone=r.CellPhone,Sex=r.Sex,SkillRank=r.SkillRank,BirthDate=DateOnly.TryParse(r.BirthDate,out var b)?b:null,RegistrationStatus=r.RegistrationStatus,MonthlyFeeAmount=r.MonthlyFeeAmount,RecurringFeeEnabled=r.RecurringFeeEnabled,RecurringFeeDay=r.RecurringFeeDay,Notes=r.Notes,Active=r.Active});
   } await db.SaveChangesAsync();
  }
  await SeedSportday(db,club.Id,Path.Combine(seed,"BPC_sportday_20251214.json"));
  await SeedAutumn(db,club.Id,Path.Combine(seed,"BPC_autumn_tour.json"));
 }

 static async Task SeedSportday(AppDbContext db,long clubId,string file){
  if(!File.Exists(file))return;
  var tour=await db.PersonalFcTournaments.FirstOrDefaultAsync(x=>x.Code=="BPC-SPORTDAY-20251214");
  if(tour==null){tour=new PersonalFcTournament{ClubId=clubId,Code="BPC-SPORTDAY-20251214",Name="B.Pickleball SportDay 14DEC2025",TournamentDate=new DateOnly(2025,12,14),Season="SPORTDAY",Status="COMPLETED",Format="GROUP_ROUND_ROBIN",Notes="Imported from sportday_tour sheet"};db.Add(tour);await db.SaveChangesAsync();}
  if(await db.PersonalFcTournamentRegistrations.AnyAsync(x=>x.TournamentId==tour.Id))return;
  var rows=JsonSerializer.Deserialize<List<SportSeed>>(await File.ReadAllTextAsync(file),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??[];
  var members=await db.PersonalFcClubMembers.Where(x=>x.ClubId==clubId).ToListAsync();
  foreach(var r in rows){var member=members.FirstOrDefault(m=>(P(m.CellPhone)!=""&&P(m.CellPhone)==P(r.Phone))||N(m.MemberName)==N(r.FullName));db.Add(new PersonalFcTournamentRegistration{TournamentId=tour.Id,MemberId=member?.Id,FullName=r.FullName,Company=r.Company,Department=r.Department,Phone=r.Phone,BirthYear=r.BirthYear,Sex=r.Sex,SkillRank=r.SkillRank,DivisionName=r.DivisionName,GroupName=r.GroupName,ShirtSize=r.ShirtSize,Notes=r.Notes,OrderNo=r.OrderNo});}
  await db.SaveChangesAsync();
 }
 static async Task SeedAutumn(AppDbContext db,long clubId,string file){
  if(!File.Exists(file))return;
  var tour=await db.PersonalFcTournaments.FirstOrDefaultAsync(x=>x.Code=="BPC-AUTUMN-TOUR");
  if(tour==null){tour=new PersonalFcTournament{ClubId=clubId,Code="BPC-AUTUMN-TOUR",Name="B.Pickleball Autumn Tour",Season="AUTUMN",Status="REGISTRATION",Format="GROUP_ROUND_ROBIN",Notes="Imported from autunm_tour sheet; source workbook has no tournament date"};db.Add(tour);await db.SaveChangesAsync();}
  if(await db.PersonalFcTournamentRegistrations.AnyAsync(x=>x.TournamentId==tour.Id))return;
  var rows=JsonSerializer.Deserialize<List<AutumnSeed>>(await File.ReadAllTextAsync(file),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??[];
  var members=await db.PersonalFcClubMembers.Where(x=>x.ClubId==clubId).ToListAsync();
  foreach(var r in rows){var member=members.FirstOrDefault(m=>(P(m.CellPhone)!=""&&P(m.CellPhone)==P(r.Phone))||N(m.MemberName)==N(r.FullName));db.Add(new PersonalFcTournamentRegistration{TournamentId=tour.Id,MemberId=member?.Id,FullName=r.FullName,Company=r.Company,Phone=r.Phone,BirthDate=DateOnly.TryParse(r.BirthDate,out var b)?b:null,Sex=r.Sex,ShirtSize=r.ShirtSize,OrderNo=r.OrderNo});}
  await db.SaveChangesAsync();
 }

 class MemberSeed{public string MemberCode{get;set;}="";public string FullName{get;set;}="";public string CellPhone{get;set;}="";public string Email{get;set;}="";public string Sex{get;set;}="";public string SkillRank{get;set;}="";public string? BirthDate{get;set;}public string Notes{get;set;}="";public bool Active{get;set;}public string? JoinedAt{get;set;}public string RegistrationStatus{get;set;}="APPROVED";public decimal MonthlyFeeAmount{get;set;}public bool RecurringFeeEnabled{get;set;}public int RecurringFeeDay{get;set;}}
 class SportSeed{public string FullName{get;set;}="";public int? BirthYear{get;set;}public string SkillRank{get;set;}="";public string Company{get;set;}="";public string Sex{get;set;}="";public string DivisionName{get;set;}="";public string Department{get;set;}="";public string Notes{get;set;}="";public string Phone{get;set;}="";public string ShirtSize{get;set;}="";public string GroupName{get;set;}="";public int? OrderNo{get;set;}}
 class AutumnSeed{public int? OrderNo{get;set;}public string FullName{get;set;}="";public string Company{get;set;}="";public string Phone{get;set;}="";public string? BirthDate{get;set;}public string Sex{get;set;}="";public string ShirtSize{get;set;}="";}
}