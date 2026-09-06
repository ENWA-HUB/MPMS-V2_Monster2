using Microsoft.EntityFrameworkCore;

namespace MAIPT.PM.Api.Data;

public static class CodePropagationService
{
    public static async Task PropagateOrgUnitCodeAsync(
        AppDbContext db,
        string oldCode,
        string newCode)
    {
        oldCode=(oldCode??"").Trim();
        newCode=(newCode??"").Trim();

        if(oldCode=="" || newCode=="" ||
           string.Equals(oldCode,newCode,StringComparison.OrdinalIgnoreCase))
            return;

        // 1) Current operational data that stores BU code as text.
        var budgetRows=await db.BudgetPlanItems
            .Where(x=>x.OrgUnit==oldCode)
            .ToListAsync();

        foreach(var row in budgetRows)
            row.OrgUnit=newCode;

        await db.SaveChangesAsync();

        // 2) RBAC scopes are physical table UserModuleScopes,
        // not an EF DbSet in AppDbContext.
        await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE UserModuleScopes
SET ScopeValue={newCode}
WHERE ScopeMode='SELECTED_ORGS'
  AND ScopeValue={oldCode};
");
    }
}
