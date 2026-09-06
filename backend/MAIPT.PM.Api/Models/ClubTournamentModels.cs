namespace MAIPT.PM.Api.Models;

public class PersonalFcTournament {
 public long Id {get;set;} public long ClubId {get;set;} public string Code {get;set;}="";
 public string Name {get;set;}=""; public DateOnly? TournamentDate {get;set;} public string Season {get;set;}="";
 public string Status {get;set;}="DRAFT"; public string Format {get;set;}="GROUP_ROUND_ROBIN";
 public string Venue {get;set;}=""; public string Notes {get;set;}=""; public DateTime CreatedAt {get;set;}=DateTime.UtcNow;
}
public class PersonalFcTournamentRegistration {
 public long Id {get;set;} public long TournamentId {get;set;} public long? MemberId {get;set;}
 public string FullName {get;set;}=""; public string Company {get;set;}=""; public string Department {get;set;}="";
 public string Phone {get;set;}=""; public DateOnly? BirthDate {get;set;} public int? BirthYear {get;set;}
 public string Sex {get;set;}=""; public string SkillRank {get;set;}=""; public string DivisionName {get;set;}="";
 public string GroupName {get;set;}=""; public string ShirtSize {get;set;}=""; public string Notes {get;set;}="";
 public string RegistrationStatus {get;set;}="REGISTERED"; public int? OrderNo {get;set;}
}
public class PersonalFcTournamentTeam {
 public long Id {get;set;} public long TournamentId {get;set;} public string DivisionName {get;set;}="";
 public string GroupName {get;set;}=""; public string TeamCode {get;set;}=""; public string TeamName {get;set;}="";
 public long? Registration1Id {get;set;} public long? Registration2Id {get;set;} public long? Member1Id {get;set;} public long? Member2Id {get;set;}
 public string Status {get;set;}="ACTIVE";
}
public class PersonalFcTournamentMatch {
 public long Id {get;set;} public long TournamentId {get;set;} public string DivisionName {get;set;}="";
 public string GroupName {get;set;}=""; public string RoundName {get;set;}="GROUP"; public int SequenceNo {get;set;}
 public string CourtNo {get;set;}=""; public long? Team1Id {get;set;} public long? Team2Id {get;set;}
 public int? Score1 {get;set;} public int? Score2 {get;set;} public long? WinnerTeamId {get;set;}
 public string Status {get;set;}="SCHEDULED"; public DateTime? ScheduledAt {get;set;} public string Notes {get;set;}="";
}
