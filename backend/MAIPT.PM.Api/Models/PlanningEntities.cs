namespace MAIPT.PM.Api.Models;

public class BudgetPlanItem : AuditableEntity
{
    public int BudgetYear { get; set; } = 2026;
    public string OrgUnit { get; set; } = "";
    public string SourceSheet { get; set; } = "";
    public int SourceRow { get; set; }
    public long? ProjectId { get; set; }
    public Project? Project { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Vendor { get; set; } = "";
    public long PlannedAmount { get; set; }
    public double? Quantity { get; set; }
    public long? UnitPrice { get; set; }
    public string PlannedMonth { get; set; } = "";
    public string MonthlyPlanJson { get; set; } = "{}";
    public string Note { get; set; } = "";
    public string Status { get; set; } = "PLANNED";
}

public class PerformancePeriod : AuditableEntity
{
    public string PeriodKey { get; set; } = "";
    public string Period { get; set; } = ""; // YYYY-MM
    public string Level { get; set; } = "INDIVIDUAL";
    public long? UserId { get; set; }
    public AppUser? User { get; set; }
    public string EmployeeName { get; set; } = "";
    public string Department { get; set; } = "IT";
    public string SourceSheet { get; set; } = "";
    public string Status { get; set; } = "OPEN";
    public bool IsLocked { get; set; }
    public DateTime? LockedAt { get; set; }
    public List<PerformanceItem> Items { get; set; } = new();
}

public class PerformanceItem : AuditableEntity
{
    public long PerformancePeriodId { get; set; }
    public PerformancePeriod? PerformancePeriod { get; set; }
    public long? ProjectId { get; set; }
    public Project? Project { get; set; }
    public int SourceRow { get; set; }
    public string Strategy { get; set; } = "";
    public string Function { get; set; } = "";
    public string Plan { get; set; } = "";
    public string Actual { get; set; } = "";
    public double Weight { get; set; }
    public double SelfScore { get; set; }
    public double ManagerScore { get; set; }
    public double HodScore { get; set; }
    public double FinalScore { get; set; }
    public string NextPlan { get; set; } = "";
    public string Note { get; set; } = "";
    public string AllocationsJson { get; set; } = "{}";
    public string Status { get; set; } = "OPEN";
}
