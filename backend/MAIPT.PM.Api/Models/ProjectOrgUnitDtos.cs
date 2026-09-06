namespace MAIPT.PM.Api.Models;
public sealed class ProjectOrgUnitsRequest
{
    public List<long> OrgUnitIds { get; set; } = [];
    public long PrimaryOrgUnitId { get; set; }
}
public sealed class ProjectMemberBatchRequest
{
    public long UserId { get; set; }
    public List<long> ProjectIds { get; set; } = [];
    public string ProjectRole { get; set; } = "MEMBER";
    public int AllocationPct { get; set; } = 100;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
}
