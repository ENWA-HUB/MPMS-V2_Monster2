using MAIPT.PM.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MAIPT.PM.Api.Data;

public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.OrgUnits.AnyAsync()) return;

        var org = new OrgUnit { Code = "MAIPT", Name = "MAIPT Organization", Type = "COMPANY" };
        db.OrgUnits.Add(org);
        await db.SaveChangesAsync();

        var cio = new AppUser { OrgUnitId = org.Id, Name = "Mai Pham", Email = "mai@maipt.org", JobTitle = "CIO / Program Director", Department = "IT", Role = "CIO" };
        var pm = new AppUser { OrgUnitId = org.Id, Name = "Project Manager", Email = "pm@maipt.org", JobTitle = "Project Manager", Department = "PMO", Role = "PM" };
        var procurement = new AppUser { OrgUnitId = org.Id, Name = "Procurement Lead", Email = "procurement@maipt.org", Department = "Procurement", Role = "PROCUREMENT" };
        db.Users.AddRange(cio, pm, procurement);
        await db.SaveChangesAsync();

        var digital = new Portfolio { OrgUnitId = org.Id, Code = "DIGITAL", Name = "Digital Transformation", OwnerId = cio.Id, Description = "Digital, Data and Enterprise Platforms" };
        var ops = new Portfolio { OrgUnitId = org.Id, Code = "OPS", Name = "Operations Excellence", OwnerId = cio.Id, Description = "Operational improvement portfolio" };
        db.Portfolios.AddRange(digital, ops);
        await db.SaveChangesAsync();

        var p1 = new Project { OrgUnitId = org.Id, PortfolioId = digital.Id, Code = "PM-001", Name = "Commercial Data Platform", Description = "Customer 360, DWH and executive BI", OwnerId = pm.Id, SponsorId = cio.Id, Status = "ACTIVE", Priority = "CRITICAL", StartDate = new DateOnly(2026, 7, 1), EndDate = new DateOnly(2026, 12, 31), BaselineEndDate = new DateOnly(2026, 12, 15), BudgetAmount = 1_900_000_000, ProgressPct = 42, HealthScore = 78, HealthStatus = "AMBER" };
        var p2 = new Project { OrgUnitId = org.Id, PortfolioId = digital.Id, Code = "PM-002", Name = "Digital Workplace", Description = "M365, SharePoint, Teams governance and digital office", OwnerId = pm.Id, SponsorId = cio.Id, Status = "ACTIVE", Priority = "HIGH", StartDate = new DateOnly(2026, 6, 1), EndDate = new DateOnly(2026, 10, 31), BaselineEndDate = new DateOnly(2026, 10, 31), BudgetAmount = 650_000_000, ProgressPct = 68, HealthScore = 91, HealthStatus = "GREEN" };
        var p3 = new Project { OrgUnitId = org.Id, PortfolioId = ops.Id, Code = "PM-003", Name = "Supplier Performance Management", Description = "Supplier KPI, contracts, deliverables and approvals", OwnerId = pm.Id, SponsorId = cio.Id, Status = "PLANNING", Priority = "HIGH", StartDate = new DateOnly(2026, 8, 1), EndDate = new DateOnly(2026, 11, 30), BudgetAmount = 300_000_000, ProgressPct = 18, HealthScore = 88, HealthStatus = "GREEN" };
        db.Projects.AddRange(p1, p2, p3);
        await db.SaveChangesAsync();

        db.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = p1.Id, UserId = pm.Id, ProjectRole = "PM", AllocationPct = 60 },
            new ProjectMember { ProjectId = p2.Id, UserId = pm.Id, ProjectRole = "PM", AllocationPct = 30 },
            new ProjectMember { ProjectId = p3.Id, UserId = procurement.Id, ProjectRole = "PROCUREMENT", AllocationPct = 40 }
        );

        var m1 = new Milestone { ProjectId = p1.Id, Name = "Data Foundation", DueDate = new DateOnly(2026, 9, 15), Status = "IN_PROGRESS", WeightPct = 35 };
        var m2 = new Milestone { ProjectId = p1.Id, Name = "Executive Dashboard", DueDate = new DateOnly(2026, 11, 30), Status = "OPEN", WeightPct = 30 };
        db.Milestones.AddRange(m1, m2);
        await db.SaveChangesAsync();

        db.Tasks.AddRange(
            new ProjectTask { ProjectId = p1.Id, MilestoneId = m1.Id, Code = "T-001", Name = "Finalize source-system mapping", AssigneeId = pm.Id, Status = "IN_PROGRESS", Priority = "HIGH", DueDate = new DateOnly(2026, 8, 28), ProgressPct = 70 },
            new ProjectTask { ProjectId = p1.Id, MilestoneId = m1.Id, Code = "T-002", Name = "Approve customer golden record", AssigneeId = cio.Id, Status = "REVIEW", Priority = "CRITICAL", DueDate = new DateOnly(2026, 8, 20), ProgressPct = 90 },
            new ProjectTask { ProjectId = p2.Id, Code = "T-003", Name = "SharePoint permissions review", AssigneeId = pm.Id, Status = "IN_PROGRESS", Priority = "MEDIUM", DueDate = new DateOnly(2026, 8, 25), ProgressPct = 55 }
        );

        db.Risks.AddRange(
            new Risk { ProjectId = p1.Id, Code = "R-001", Category = "DATA", Title = "Source data quality lower than expected", Probability = 4, Impact = 4, SeverityScore = 16, OwnerId = pm.Id, MitigationPlan = "Add profiling and cleansing gate before ingestion", Status = "OPEN" },
            new Risk { ProjectId = p2.Id, Code = "R-002", Category = "CHANGE", Title = "Low adoption of Teams channel governance", Probability = 3, Impact = 3, SeverityScore = 9, OwnerId = pm.Id, MitigationPlan = "Targeted adoption sessions", Status = "MONITORING" }
        );

        db.Issues.Add(new Issue { ProjectId = p1.Id, Code = "I-001", Title = "POS extract delayed", Category = "INTEGRATION", Severity = "HIGH", Status = "INVESTIGATING", ReportedBy = pm.Id, AssignedTo = pm.Id, ReportedDate = new DateOnly(2026, 8, 12), TargetResolutionDate = new DateOnly(2026, 8, 19) });

        var bl1 = new BudgetLine { ProjectId = p1.Id, Code = "BL-001", Category = "IMPLEMENTATION", Description = "Data platform implementation", BaselineAmount = 1_600_000_000, RevisedAmount = 1_700_000_000, CommittedAmount = 1_100_000_000, ActualAmount = 760_000_000, ForecastAmount = 1_680_000_000 };
        var bl2 = new BudgetLine { ProjectId = p1.Id, Code = "BL-002", Category = "TRAINING", Description = "Change & training", BaselineAmount = 300_000_000, RevisedAmount = 200_000_000, CommittedAmount = 120_000_000, ActualAmount = 80_000_000, ForecastAmount = 180_000_000 };
        db.BudgetLines.AddRange(bl1, bl2);
        await db.SaveChangesAsync();

        db.BudgetTransactions.AddRange(
            new BudgetTransaction { BudgetLineId = bl1.Id, Type = "ACTUAL", ReferenceNo = "INV-001", Description = "Implementation milestone 1", Amount = 500_000_000, TxnDate = new DateOnly(2026, 7, 31), Status = "POSTED", ApprovedBy = cio.Id, ApprovedAt = DateTime.UtcNow.AddDays(-15) },
            new BudgetTransaction { BudgetLineId = bl1.Id, Type = "ACTUAL", ReferenceNo = "INV-002", Description = "Data integration package", Amount = 260_000_000, TxnDate = new DateOnly(2026, 8, 10), Status = "POSTED", ApprovedBy = cio.Id, ApprovedAt = DateTime.UtcNow.AddDays(-5) }
        );

        var s1 = new Supplier { OrgUnitId = org.Id, Code = "SUP-001", Name = "Innotech", Category = "Technology", ContactName = "Account Manager", Email = "contact@example.com", Status = "ACTIVE", Rating = 4.2 };
        var s2 = new Supplier { OrgUnitId = org.Id, Code = "SUP-002", Name = "FPT Information System", Category = "Technology", ContactName = "Enterprise Sales", Email = "enterprise@example.com", Status = "QUALIFIED", Rating = 4.0 };
        var s3 = new Supplier { OrgUnitId = org.Id, Code = "SUP-003", Name = "CMC", Category = "Infrastructure", ContactName = "Account Team", Email = "cmc@example.com", Status = "ACTIVE", Rating = 3.7 };
        db.Suppliers.AddRange(s1, s2, s3);
        await db.SaveChangesAsync();

        var c1 = new Contract { ProjectId = p1.Id, SupplierId = s1.Id, ContractNumber = "CTR-2026-001", Title = "Commercial Data Platform Phase 1", ContractType = "IMPLEMENTATION", Value = 1_200_000_000, SignedDate = new DateOnly(2026, 7, 1), StartDate = new DateOnly(2026, 7, 1), EndDate = new DateOnly(2026, 12, 20), OwnerId = pm.Id, Status = "ACTIVE" };
        db.Contracts.Add(c1);
        await db.SaveChangesAsync();

        db.Deliverables.AddRange(
            new Deliverable { ContractId = c1.Id, MilestoneId = m1.Id, Name = "Data source assessment", DueDate = new DateOnly(2026, 8, 15), SubmittedDate = new DateOnly(2026, 8, 14), AcceptedDate = new DateOnly(2026, 8, 15), AcceptedBy = pm.Id, Status = "ACCEPTED", CompletionPct = 100 },
            new Deliverable { ContractId = c1.Id, MilestoneId = m1.Id, Name = "Data ingestion framework", DueDate = new DateOnly(2026, 9, 15), Status = "IN_PROGRESS", CompletionPct = 45 }
        );

        var criteria = new[]
        {
            new KpiCriteria { Code="ONTIME", Name="On-time Delivery", WeightPct=20, EffectiveFrom=new DateOnly(2026,1,1) },
            new KpiCriteria { Code="QUALITY", Name="Quality", WeightPct=20, EffectiveFrom=new DateOnly(2026,1,1) },
            new KpiCriteria { Code="COST", Name="Cost Performance", WeightPct=15, EffectiveFrom=new DateOnly(2026,1,1) },
            new KpiCriteria { Code="RESP", Name="Responsiveness", WeightPct=10, EffectiveFrom=new DateOnly(2026,1,1) },
            new KpiCriteria { Code="COMPLIANCE", Name="Compliance", WeightPct=15, EffectiveFrom=new DateOnly(2026,1,1) },
            new KpiCriteria { Code="ISSUE", Name="Issue Resolution", WeightPct=10, EffectiveFrom=new DateOnly(2026,1,1) },
            new KpiCriteria { Code="DOC", Name="Documentation", WeightPct=10, EffectiveFrom=new DateOnly(2026,1,1) },
        };
        db.KpiCriteria.AddRange(criteria);
        await db.SaveChangesAsync();

        var ev = new SupplierEvaluation { SupplierId = s1.Id, ProjectId = p1.Id, ContractId = c1.Id, PeriodType = "QUARTER", PeriodStart = new DateOnly(2026, 7, 1), PeriodEnd = new DateOnly(2026, 9, 30), EvaluatorId = procurement.Id, OverallScore = 4.15, Rating = "VERY_GOOD", Status = "SUBMITTED", SubmittedAt = DateTime.UtcNow.AddDays(-2) };
        db.SupplierEvaluations.Add(ev);
        await db.SaveChangesAsync();

        var scores = new[] { 4.0, 4.5, 4.0, 4.5, 4.0, 4.0, 4.0 };
        for (var i = 0; i < criteria.Length; i++)
        {
            db.KpiScores.Add(new KpiScore { EvaluationId = ev.Id, CriteriaId = criteria[i].Id, CriteriaNameSnapshot = criteria[i].Name, WeightPctSnapshot = criteria[i].WeightPct, Score = scores[i], WeightedScore = scores[i] * criteria[i].WeightPct / 100d, Comment = "Seed evaluation" });
        }

        var approval = new ApprovalRequest { ProjectId = p1.Id, EntityType = "SUPPLIER_EVALUATION", EntityId = ev.Id, WorkflowType = "SUPPLIER_KPI", RequestedBy = procurement.Id, Status = "PENDING" };
        db.ApprovalRequests.Add(approval);
        await db.SaveChangesAsync();
        db.ApprovalSteps.Add(new ApprovalStep { ApprovalRequestId = approval.Id, StepNo = 1, ApproverId = cio.Id, Status = "PENDING" });

        db.Notifications.AddRange(
            new NotificationRecord { UserId = cio.Id, ProjectId = p1.Id, Severity = "WARNING", Type = "APPROVAL", Title = "Supplier KPI waiting approval", Message = "Innotech Q3/2026 evaluation is waiting for approval.", EntityType = "SUPPLIER_EVALUATION", EntityId = ev.Id },
            new NotificationRecord { UserId = pm.Id, ProjectId = p1.Id, Severity = "WARNING", Type = "RISK", Title = "High project risk", Message = "R-001 has severity score 16.", EntityType = "RISK" }
        );

        await db.SaveChangesAsync();
    }
}
