
namespace MAIPT.PM.Api.Models;
public class SupplierEvaluationCreateRequest{
 public long SupplierId{get;set;} public long ProjectId{get;set;} public long? ContractId{get;set;}
 public string PeriodType{get;set;}="QUARTER"; public DateOnly PeriodStart{get;set;} public DateOnly PeriodEnd{get;set;}
 public long EvaluatorId{get;set;}=1; public string Status{get;set;}="SUBMITTED"; public List<KpiScoreCreateRequest> Scores{get;set;}=new();
}
public class KpiScoreCreateRequest{ public long CriteriaId{get;set;} public double Score{get;set;} public string Comment{get;set;}=""; }
public class FolderCreateRequest{ public string Name{get;set;}=""; }
