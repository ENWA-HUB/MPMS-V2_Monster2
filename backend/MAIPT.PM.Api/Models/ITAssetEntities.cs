namespace MAIPT.PM.Api.Models;

public class ITAsset : AuditableEntity
{
    public string AssetCode { get; set; } = "";
    public string Category { get; set; } = "LAPTOP";
    public string AssetName { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Model { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public string Specification { get; set; } = "";
    public long? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public long? ContractId { get; set; }
    public Contract? Contract { get; set; }
    public long? ProjectId { get; set; }
    public Project? Project { get; set; }
    public long? BudgetLineId { get; set; }
    public BudgetLine? BudgetLine { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public long PurchasePrice { get; set; }
    public string Currency { get; set; } = "VND";
    public DateOnly? WarrantyExpiry { get; set; }
    public string Location { get; set; } = "";
    public string Department { get; set; } = "";
    public long? OrgUnitId { get; set; }
    public long? ManagerUserId { get; set; }
    public long? UsingUserId { get; set; }

    public string Condition { get; set; } = "GOOD";
    public string Status { get; set; } = "IN_STOCK";
    public string Notes { get; set; } = "";
}

public class ITAssetAssignment : AuditableEntity
{
    public long AssetId { get; set; }
    public ITAsset? Asset { get; set; }
    public long UserId { get; set; }
    public AppUser? User { get; set; }
    public string AssignmentType { get; set; } = "ASSIGN";
    public DateOnly AssignmentDate { get; set; }
    public DateOnly? ReturnDate { get; set; }
    public string FromLocation { get; set; } = "";
    public string ToLocation { get; set; } = "";
    public string ConditionOut { get; set; } = "GOOD";
    public string ConditionIn { get; set; } = "";
    public string Accessories { get; set; } = "";
    public string HandoverNo { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Status { get; set; } = "ACTIVE";
    public long? ReceivedByUserId { get; set; }
    public string ReturnReason { get; set; } = "";
    public string ReturnLocation { get; set; } = "";
    public string AccessoriesReturned { get; set; } = "";
    public string MissingItems { get; set; } = "";
}

public class ITLicense : AuditableEntity
{
    public string Code { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string Vendor { get; set; } = "";
    public string LicenseType { get; set; } = "SUBSCRIPTION";
    public string Licensee { get; set; } = "";
    public string LicenseKey { get; set; } = "";
    public long? OrgUnitId { get; set; }
    public long? AssignedUserId { get; set; }
    public long? AssignedAssetId { get; set; }
    public string ActivationStatus { get; set; } = "NOT_ACTIVATED";
    public DateOnly? ActivationDate { get; set; }
    public int Quantity { get; set; }
    public int AssignedQuantity { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public long Cost { get; set; }
    public string Currency { get; set; } = "VND";
    public long? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public long? ContractId { get; set; }
    public Contract? Contract { get; set; }
    public bool AutoRenew { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public string Notes { get; set; } = "";
  public long? ManagerUserId { get; set; }
  public long? UsingUserId { get; set; }
}

public class ITService : AuditableEntity
{
    public string Code { get; set; } = "";
    public string ServiceType { get; set; } = "INTERNET";
    public string ServiceName { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Site { get; set; } = "";
    public string Department { get; set; } = "";
    public long? OrgUnitId { get; set; }
    public long? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public long? ContractId { get; set; }
    public Contract? Contract { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public long MonthlyCost { get; set; }
    public long AnnualCost { get; set; }
    public string Currency { get; set; } = "VND";
    public string Status { get; set; } = "ACTIVE";
    public string Notes { get; set; } = "";
}

public class ITMaintenance : AuditableEntity
{
    public long AssetId { get; set; }
    public ITAsset? Asset { get; set; }
    public string Type { get; set; } = "REPAIR";
    public DateOnly? OpenDate { get; set; }
    public DateOnly? CloseDate { get; set; }
    public string Vendor { get; set; } = "";
    public string Description { get; set; } = "";
    public long Cost { get; set; }
    public string Currency { get; set; } = "VND";
    public string Status { get; set; } = "OPEN";
    public string Notes { get; set; } = "";
}
