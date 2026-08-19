using MAIPT.PM.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MAIPT.PM.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<MasterCategory> Categories => Set<MasterCategory>();
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<ProjectTask> Tasks => Set<ProjectTask>();
    public DbSet<Risk> Risks => Set<Risk>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<BudgetLine> BudgetLines => Set<BudgetLine>();
    public DbSet<BudgetTransaction> BudgetTransactions => Set<BudgetTransaction>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<Deliverable> Deliverables => Set<Deliverable>();
    public DbSet<KpiCriteria> KpiCriteria => Set<KpiCriteria>();
    public DbSet<SupplierEvaluation> SupplierEvaluations => Set<SupplierEvaluation>();
    public DbSet<KpiScore> KpiScores => Set<KpiScore>();
    public DbSet<ChangeRequest> ChangeRequests => Set<ChangeRequest>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();
    public DbSet<DocumentRecord> Documents => Set<DocumentRecord>();
    public DbSet<NotificationRecord> Notifications => Set<NotificationRecord>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<BudgetPlanItem> BudgetPlanItems => Set<BudgetPlanItem>();
    public DbSet<PerformancePeriod> PerformancePeriods => Set<PerformancePeriod>();
    public DbSet<PerformanceItem> PerformanceItems => Set<PerformanceItem>();
    public DbSet<AuthAccount> AuthAccounts => Set<AuthAccount>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<OrgUnit>().HasIndex(x => x.Code).IsUnique();
        b.Entity<AppUser>().HasIndex(x => x.Email).IsUnique();
        b.Entity<MasterCategory>().HasIndex(x => new { x.Scope, x.Code }).IsUnique();
        b.Entity<Project>().HasIndex(x => new { x.OrgUnitId, x.Code }).IsUnique();
        b.Entity<Project>().HasIndex(x => x.Status);
        b.Entity<Project>().HasIndex(x => x.PortfolioId);
        b.Entity<ProjectMember>().HasIndex(x => new { x.ProjectId, x.UserId }).IsUnique();
        b.Entity<ProjectTask>().HasIndex(x => new { x.ProjectId, x.Status, x.DueDate });
        b.Entity<Risk>().HasIndex(x => new { x.ProjectId, x.Status, x.SeverityScore });
        b.Entity<Issue>().HasIndex(x => new { x.ProjectId, x.Status, x.Severity });
        b.Entity<BudgetLine>().HasIndex(x => x.ProjectId);
        b.Entity<BudgetTransaction>().HasIndex(x => x.BudgetLineId);
        b.Entity<Supplier>().HasIndex(x => new { x.OrgUnitId, x.Code }).IsUnique();
        b.Entity<Contract>().HasIndex(x => new { x.ProjectId, x.SupplierId });
        b.Entity<Deliverable>().HasIndex(x => new { x.ContractId, x.DueDate });
        b.Entity<KpiCriteria>().HasIndex(x => new { x.Code, x.Version }).IsUnique();
        b.Entity<SupplierEvaluation>().HasIndex(x => new { x.SupplierId, x.ProjectId, x.PeriodEnd });
        b.Entity<KpiScore>().HasIndex(x => new { x.EvaluationId, x.CriteriaId }).IsUnique();
        b.Entity<ApprovalStep>().HasIndex(x => new { x.ApprovalRequestId, x.StepNo }).IsUnique();
        b.Entity<NotificationRecord>().HasIndex(x => new { x.UserId, x.IsRead });
        b.Entity<ActivityLog>().HasIndex(x => new { x.EntityType, x.EntityId });
        b.Entity<ActivityLog>().HasIndex(x => new { x.ProjectId, x.Timestamp });
        b.Entity<BudgetPlanItem>().HasIndex(x => new { x.BudgetYear, x.OrgUnit });
        b.Entity<BudgetPlanItem>().HasIndex(x => x.ProjectId);
        b.Entity<PerformancePeriod>().HasIndex(x => x.PeriodKey).IsUnique();
        b.Entity<PerformanceItem>().HasIndex(x => new { x.PerformancePeriodId, x.ProjectId });
        b.Entity<AuthAccount>().HasIndex(x => x.UserId).IsUnique();
        b.Entity<AuthSession>().HasIndex(x => x.TokenHash).IsUnique();
        b.Entity<AuthSession>().HasIndex(x => new { x.UserId, x.ExpiresAt });

        b.Entity<Project>()
            .HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Project>()
            .HasOne(x => x.Sponsor).WithMany().HasForeignKey(x => x.SponsorId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ProjectTask>()
            .HasOne(x => x.ParentTask).WithMany().HasForeignKey(x => x.ParentTaskId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Issue>()
            .HasOne(x => x.Risk).WithMany().HasForeignKey(x => x.RiskId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<KpiScore>()
            .HasOne(x => x.Evaluation).WithMany(x => x.Scores).HasForeignKey(x => x.EvaluationId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ApprovalStep>()
            .HasOne(x => x.ApprovalRequest).WithMany(x => x.Steps).HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<PerformanceItem>()
            .HasOne(x => x.PerformancePeriod).WithMany(x => x.Items).HasForeignKey(x => x.PerformancePeriodId).OnDelete(DeleteBehavior.Cascade);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added) entry.Entity.CreatedAt = now;
            if (entry.State is EntityState.Added or EntityState.Modified) entry.Entity.UpdatedAt = now;
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
