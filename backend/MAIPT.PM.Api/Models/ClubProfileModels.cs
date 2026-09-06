namespace MAIPT.PM.Api.Models;

public class PersonalFcClubProfile
{
    public long Id { get; set; }
    public long ClubId { get; set; }
    public string About { get; set; } = "";
    public string Mission { get; set; } = "";
    public string Vision { get; set; } = "";
    public string CoreValues { get; set; } = "";
    public DateOnly? FoundedDate { get; set; }
    public string ContactEmail { get; set; } = "";
    public string ContactPhone { get; set; } = "";
    public string Website { get; set; } = "";
    public string SocialLink { get; set; } = "";
    public string MainVenue { get; set; } = "";
    public string RegulationsSummary { get; set; } = "";
    public string ActivitiesSummary { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
