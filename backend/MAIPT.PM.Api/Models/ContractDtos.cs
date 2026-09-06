namespace MAIPT.PM.Api.Models;

public sealed class ContractUpsertRequest
{
    public long OrgUnitId { get; set; }
    public long ProjectId { get; set; }
    public long SupplierId { get; set; }
    public string ContractNumber { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string ContractType { get; set; } = "SERVICE";
    public long Value { get; set; }
    public string Currency { get; set; } = "VND";
    public DateOnly? SignedDate { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public long? OwnerId { get; set; }
    public string Status { get; set; } = "ACTIVE";
}
