namespace MAIPT.PM.Api.Models;

public abstract class AuditableEntity
{
    public long? CreatedByUserId { get; set; }
    public long? UpdatedByUserId { get; set; }

    public long Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class OrgUnit : AuditableEntity
{
    // BUSINESS_UNIT_PROFILE_V1
    public string? UnitType { get; set; }
    public long? ParentOrgUnitId { get; set; }
    public string? ShortName { get; set; }
    public string? InternationalName { get; set; }
    public string? Description { get; set; }
    public string? LegalType { get; set; }
    public string? TaxCode { get; set; }
    public string? RegistrationNo { get; set; }
    public DateTime? IncorporationDate { get; set; }
    public string? LegalRepresentative { get; set; }
    public string? RepresentativeTitle { get; set; }
    public string? RegisteredAddress { get; set; }
    public string? OfficeAddress { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? HeadName { get; set; }
    public string? FinanceContact { get; set; }
    public string? ITContact { get; set; }
    public string? HRContact { get; set; }
    public string? DefaultCurrency { get; set; }
    public string? FiscalYear { get; set; }
    public string? CostCenter { get; set; }
    public string? CompanyCode { get; set; }

    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public long? ParentId { get; set; }
    public OrgUnit? Parent { get; set; }
    public string Type { get; set; } = "SBU";
    public string Status { get; set; } = "ACTIVE";

    public DateTime? TaxIssueDate { get; set; }
    public DateTime? RegistrationIssueDate { get; set; }
}

public class AppUser : AuditableEntity
{
    public long OrgUnitId { get; set; }
    public OrgUnit? OrgUnit { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string JobTitle { get; set; } = "";
    public string Department { get; set; } = "";
    public string Role { get; set; } = "MEMBER";
    public string Status { get; set; } = "ACTIVE";

    public string? EmployeeCode { get; set; }
    public string? Phone { get; set; }
    public string? PersonalEmail { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Address { get; set; }
    public DateTime? JoinDate { get; set; }
    public string? EmploymentType { get; set; }
    public long? ManagerUserId { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? Notes { get; set; }
    public string? ProfileImagePath { get; set; }
    public string? SignaturePath { get; set; }

}

public class MasterCategory : AuditableEntity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Scope { get; set; } = "GENERAL";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "ACTIVE";
}

public class Portfolio : AuditableEntity
{
    public long OrgUnitId { get; set; }
    public OrgUnit? OrgUnit { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public long? OwnerId { get; set; }
    public AppUser? Owner { get; set; }
    public string Description { get; set; } = "";
    public string Status { get; set; } = "ACTIVE";
}

public class Project : AuditableEntity
{
    public long OrgUnitId { get; set; }
    public OrgUnit? OrgUnit { get; set; }
    public long? PortfolioId { get; set; }
    public Portfolio? Portfolio { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public long OwnerId { get; set; }
    public AppUser? Owner { get; set; }
    public long? SponsorId { get; set; }
    public AppUser? Sponsor { get; set; }
    public string Status { get; set; } = "PLANNING";
    public string Priority { get; set; } = "MEDIUM";
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateOnly? BaselineEndDate { get; set; }
    public long BudgetAmount { get; set; }
    public string Currency { get; set; } = "VND";
    public int ProgressPct { get; set; }
    public double HealthScore { get; set; } = 100;
    public string HealthStatus { get; set; } = "GREEN";
    public DateTime? ArchivedAt { get; set; }
}

public class ProjectOrgUnit : AuditableEntity
{
    public long ProjectId { get; set; }
    public Project? Project { get; set; }
    public long OrgUnitId { get; set; }
    public OrgUnit? OrgUnit { get; set; }
    public bool IsPrimary { get; set; }
}

public class ProjectMember : AuditableEntity
{
    public long ProjectId { get; set; }
    public Project? Project { get; set; }
    public long UserId { get; set; }
    public AppUser? User { get; set; }
    public string ProjectRole { get; set; } = "MEMBER";
    public int AllocationPct { get; set; } = 100;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Milestone : AuditableEntity
{
    public long ProjectId { get; set; }
    public Project? Project { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DateOnly? DueDate { get; set; }
    public DateOnly? ActualDate { get; set; }
    public string Status { get; set; } = "OPEN";
    public double WeightPct { get; set; }
}

public class ProjectTask : AuditableEntity
{
    public long ProjectId { get; set; }
    public Project? Project { get; set; }
    public long? ParentTaskId { get; set; }
    public ProjectTask? ParentTask { get; set; }
    public long? MilestoneId { get; set; }
    public Milestone? Milestone { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public long? AssigneeId { get; set; }
    public AppUser? Assignee { get; set; }
    public string Status { get; set; } = "TODO";
    public string Priority { get; set; } = "MEDIUM";
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateOnly? CompletedDate { get; set; }
    public int ProgressPct { get; set; }
    public double EstimatedHours { get; set; }
    public double ActualHours { get; set; }
    public int SortOrder { get; set; }
}

public class Risk : AuditableEntity
{
    public long ProjectId { get; set; }
    public Project? Project { get; set; }
    public string Code { get; set; } = "";
    public string Category { get; set; } = "GENERAL";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int Probability { get; set; }
    public int Impact { get; set; }
    public int SeverityScore { get; set; }
    public long? OwnerId { get; set; }
    public AppUser? Owner { get; set; }
    public string ResponseStrategy { get; set; } = "MITIGATE";
    public string MitigationPlan { get; set; } = "";
    public string ContingencyPlan { get; set; } = "";
    public DateOnly? TargetDate { get; set; }
    public string Status { get; set; } = "OPEN";
}

public class Issue : AuditableEntity
{
    public long ProjectId { get; set; }
    public Project? Project { get; set; }
    public long? RiskId { get; set; }
    public Risk? Risk { get; set; }
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "GENERAL";
    public string Severity { get; set; } = "MEDIUM";
    public string Status { get; set; } = "OPEN";
    public long? ReportedBy { get; set; }
    public long? AssignedTo { get; set; }
    public DateOnly? ReportedDate { get; set; }
    public DateOnly? TargetResolutionDate { get; set; }
    public DateOnly? ResolvedDate { get; set; }
    public string Resolution { get; set; } = "";
}

public class BudgetLine : AuditableEntity
{
    public long ProjectId { get; set; }
    public Project? Project { get; set; }
    public string Code { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public long BaselineAmount { get; set; }
    public long RevisedAmount { get; set; }
    public long CommittedAmount { get; set; }
    public long ActualAmount { get; set; }
    public long ForecastAmount { get; set; }
    public string Currency { get; set; } = "VND";
}

public class BudgetTransaction : AuditableEntity
{
    public long BudgetLineId { get; set; }
    public BudgetLine? BudgetLine { get; set; }
    public long? ContractId { get; set; }
    public Contract? Contract { get; set; }
    public string Type { get; set; } = "ACTUAL";
    public string ReferenceNo { get; set; } = "";
    public string Description { get; set; } = "";
    public long Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public DateOnly? TxnDate { get; set; }
    public string Status { get; set; } = "POSTED";
    public long? RequestedBy { get; set; }
    public long? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
}

public class Supplier : AuditableEntity
{
    public long OrgUnitId { get; set; }
    public OrgUnit? OrgUnit { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string TaxCode { get; set; } = "";
    public string Category { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public string Status { get; set; } = "ACTIVE";
    public double Rating { get; set; }

    public string CompanyName { get; set; } = "";
    public string ShortName { get; set; } = "";
    public string Website { get; set; } = "";
    public string BusinessRegistrationNo { get; set; } = "";
    public string LegalRepresentative { get; set; } = "";
    public DateOnly? RegistrationDate { get; set; }
    public string Country { get; set; } = "Vietnam";
    public string ContactPosition { get; set; } = "";
    public string AlternativePhone { get; set; } = "";
    public string ProvinceCity { get; set; } = "";
    public string PaymentTerms { get; set; } = "";
    public string Currency { get; set; } = "VND";
    public string BankName { get; set; } = "";
    public string BankAccountNo { get; set; } = "";
    public string BankAccountName { get; set; } = "";
    public string BankBranch { get; set; } = "";
    public long? InternalOwnerId { get; set; }
    public string Notes { get; set; } = "";
}

public class Contract : AuditableEntity
{
    public long OrgUnitId { get; set; }
    public long ProjectId { get; set; }
    public Project? Project { get; set; }
    public long SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
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

public class Deliverable : AuditableEntity
{
    public long ContractId { get; set; }
    public Contract? Contract { get; set; }
    public long? MilestoneId { get; set; }
    public Milestone? Milestone { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DateOnly? DueDate { get; set; }
    public DateOnly? SubmittedDate { get; set; }
    public DateOnly? AcceptedDate { get; set; }
    public long? AcceptedBy { get; set; }
    public string Status { get; set; } = "OPEN";
    public int CompletionPct { get; set; }
}

public class KpiCriteria : AuditableEntity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "SUPPLIER";
    public double WeightPct { get; set; }
    public string Description { get; set; } = "";
    public int Version { get; set; } = 1;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SupplierEvaluation : AuditableEntity
{
    public long SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public long ProjectId { get; set; }
    public Project? Project { get; set; }
    public long? ContractId { get; set; }
    public Contract? Contract { get; set; }
    public string PeriodType { get; set; } = "QUARTER";
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public long EvaluatorId { get; set; }
    public AppUser? Evaluator { get; set; }
    public double OverallScore { get; set; }
    public string Rating { get; set; } = "ACCEPTABLE";
    public string Status { get; set; } = "DRAFT";
    public DateTime? SubmittedAt { get; set; }
    public long? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public List<KpiScore> Scores { get; set; } = new();
}

public class KpiScore : AuditableEntity
{
    public long EvaluationId { get; set; }
    public SupplierEvaluation? Evaluation { get; set; }
    public long CriteriaId { get; set; }
    public KpiCriteria? Criteria { get; set; }
    public string CriteriaNameSnapshot { get; set; } = "";
    public double WeightPctSnapshot { get; set; }
    public double Score { get; set; }
    public double WeightedScore { get; set; }
    public string EvidenceUrl { get; set; } = "";
    public string Comment { get; set; } = "";
}

public class ChangeRequest : AuditableEntity
{
    public long ProjectId { get; set; }
    public Project? Project { get; set; }
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public long RequestedBy { get; set; }
    public DateOnly? RequestDate { get; set; }
    public string ChangeType { get; set; } = "SCOPE";
    public string Reason { get; set; } = "";
    public string ScopeImpact { get; set; } = "";
    public int ScheduleImpactDays { get; set; }
    public long CostImpact { get; set; }
    public string RiskImpact { get; set; } = "";
    public string Status { get; set; } = "DRAFT";
    public string Decision { get; set; } = "";
    public long? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
}

public class ApprovalRequest : AuditableEntity
{
    public long? ProjectId { get; set; }
    public string EntityType { get; set; } = "";
    public long EntityId { get; set; }
    public string WorkflowType { get; set; } = "GENERAL";
    public long RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "PENDING";
    public DateTime? CompletedAt { get; set; }
    public List<ApprovalStep> Steps { get; set; } = new();
}

public class ApprovalStep : AuditableEntity
{
    public long ApprovalRequestId { get; set; }
    public ApprovalRequest? ApprovalRequest { get; set; }
    public int StepNo { get; set; }
    public long ApproverId { get; set; }
    public AppUser? Approver { get; set; }
    public string Status { get; set; } = "PENDING";
    public string Comment { get; set; } = "";
    public DateTime? ActedAt { get; set; }
}

public class DocumentRecord : AuditableEntity
{
    public string StorageProvider { get; set; } = "LOCAL";
    public string? StorageDriveId { get; set; }
    public string? StorageItemId { get; set; }
    public string? StorageWebUrl { get; set; }
    public string? StoragePath { get; set; }

    public long OrgUnitId { get; set; }
    public string EntityType { get; set; } = "";
    public long EntityId { get; set; }
    public string Category { get; set; } = "GENERAL";
    public string Name { get; set; } = "";
    public string OriginalFileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string MimeType { get; set; } = "";
    public long FileSize { get; set; }
    public int Version { get; set; } = 1;
    public string Checksum { get; set; } = "";
    public long UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public bool IsCurrent { get; set; } = true;
    public string Status { get; set; } = "ACTIVE";
}

public class NotificationRecord : AuditableEntity
{
    public long UserId { get; set; }
    public long? ProjectId { get; set; }
    public string Type { get; set; } = "INFO";
    public string Severity { get; set; } = "INFO";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string EntityType { get; set; } = "";
    public long? EntityId { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
}

public class ActivityLog
{
    public long Id { get; set; }
    public long? UserId { get; set; }
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public long EntityId { get; set; }
    public long? ProjectId { get; set; }
    public string OldValuesJson { get; set; } = "";
    public string NewValuesJson { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public string UserAgent { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}


public class ITDomain
{
    public long Id { get; set; }
    public long? OrgUnitId { get; set; }
    public long? SupplierId { get; set; }
    public long? ContractId { get; set; }
    public long? ManagerUserId { get; set; }
    public string DomainName { get; set; } = "";
    public string DomainType { get; set; } = "INTERNATIONAL";
    public string Purpose { get; set; } = "";
    public string Registrar { get; set; } = "";
    public string RegistrarUrl { get; set; } = "";
    public string LoginEmail { get; set; } = "";
    public string LoginPasswordProtected { get; set; } = "";
    public DateOnly? RegistrationDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string RenewalStatus { get; set; } = "MANUAL";
    public bool AutoRenew { get; set; }
    public bool WhoisPrivacy { get; set; }
    public string DnsProvider { get; set; } = "";
    public string NameServers { get; set; } = "";
    public string RegistrantOrganization { get; set; } = "";
    public string RegistrantContact { get; set; } = "";
    public string AdminContact { get; set; } = "";
    public string TechnicalContact { get; set; } = "";
    public string DeclarationNo { get; set; } = "";
    public DateOnly? DeclarationDate { get; set; }
    public string RecoveryEmail { get; set; } = "";
    public string MfaMethod { get; set; } = "";
    public string Status { get; set; } = "ACTIVE";
    public string Notes { get; set; } = "";
    public long? CreatedByUserId { get; set; }
    public long? UpdatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
