using System.Data;
using System.Text.Json;
using MAIPT.PM.Api.Data;
using MAIPT.PM.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MAIPT.PM.Api.Endpoints;

public static class CapitalInvestmentEndpoints
{
    static string Text(JsonElement body,string name)=>body.TryGetProperty(name,out var v)&&v.ValueKind==JsonValueKind.String?(v.GetString()??"").Trim():"";
    static decimal Number(JsonElement body,string name)=>body.TryGetProperty(name,out var v)&&v.TryGetDecimal(out var n)?n:0;
    static int Integer(JsonElement body,string name)=>body.TryGetProperty(name,out var v)&&v.TryGetInt32(out var n)?n:0;
    static DateTime? Date(JsonElement body,string name)=>DateTime.TryParse(Text(body,name),out var d)?d:null;
    static string Json(JsonElement body,string name)=>body.TryGetProperty(name,out var v)&&v.ValueKind==JsonValueKind.Object?v.GetRawText():"{}";
    static void Add(IDbCommand cmd,string name,object? value){var p=cmd.CreateParameter();p.ParameterName=name;p.Value=value??DBNull.Value;cmd.Parameters.Add(p);}

    static async Task EnsureSchema(AppDbContext db)
    {
        var provider=db.Database.ProviderName??"";
        if(provider.Contains("SqlServer",StringComparison.OrdinalIgnoreCase))
            await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'dbo.CapitalInvestmentRecords',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.CapitalInvestmentRecords(
  Id bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
  ManagementType nvarchar(20) NOT NULL, Category nvarchar(60) NOT NULL,
  Title nvarchar(300) NOT NULL, OrgUnit nvarchar(100) NOT NULL, FiscalYear int NOT NULL,
  Amount decimal(20,2) NOT NULL DEFAULT(0), Currency nvarchar(10) NOT NULL DEFAULT('VND'),
  MetricName nvarchar(100) NOT NULL DEFAULT(''), MetricValue decimal(20,4) NOT NULL DEFAULT(0),
  Status nvarchar(30) NOT NULL DEFAULT('PLANNED'), RiskLevel nvarchar(20) NOT NULL DEFAULT('MEDIUM'),
  Counterparty nvarchar(200) NOT NULL DEFAULT(''), StartDate date NULL, EndDate date NULL,
  Notes nvarchar(max) NOT NULL DEFAULT(''), CreatedByUserId bigint NOT NULL,
  ExtraData nvarchar(max) NOT NULL DEFAULT('{{}}'),
  CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), UpdatedByUserId bigint NOT NULL,
  UpdatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
 );
 CREATE INDEX IX_CapitalInvestmentRecords_Filter ON dbo.CapitalInvestmentRecords(ManagementType,FiscalYear,OrgUnit,Category,Status);
END
IF COL_LENGTH('dbo.CapitalInvestmentRecords','ExtraData') IS NULL
 ALTER TABLE dbo.CapitalInvestmentRecords ADD ExtraData nvarchar(max) NOT NULL CONSTRAINT DF_CapitalInvestmentRecords_ExtraData DEFAULT('{{}}');");
        else
            await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS CapitalInvestmentRecords(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, ManagementType TEXT NOT NULL, Category TEXT NOT NULL,
 Title TEXT NOT NULL, OrgUnit TEXT NOT NULL, FiscalYear INTEGER NOT NULL, Amount NUMERIC NOT NULL DEFAULT 0,
 Currency TEXT NOT NULL DEFAULT 'VND', MetricName TEXT NOT NULL DEFAULT '', MetricValue NUMERIC NOT NULL DEFAULT 0,
 Status TEXT NOT NULL DEFAULT 'PLANNED', RiskLevel TEXT NOT NULL DEFAULT 'MEDIUM', Counterparty TEXT NOT NULL DEFAULT '',
 StartDate TEXT NULL, EndDate TEXT NULL, Notes TEXT NOT NULL DEFAULT '', CreatedByUserId INTEGER NOT NULL,
 ExtraData TEXT NOT NULL DEFAULT '{{}}', CreatedAt TEXT NOT NULL, UpdatedByUserId INTEGER NOT NULL, UpdatedAt TEXT NOT NULL);");
        await EnsureSqliteExtraData(db);
    }

    static async Task EnsureSqliteExtraData(AppDbContext db)
    {
        if(db.Database.IsSqlServer())return;
        var conn=db.Database.GetDbConnection();if(conn.State!=ConnectionState.Open)await conn.OpenAsync();
        await using var check=conn.CreateCommand();check.CommandText="PRAGMA table_info(CapitalInvestmentRecords)";var found=false;
        await using(var reader=await check.ExecuteReaderAsync())while(await reader.ReadAsync())if(string.Equals(reader.GetString(1),"ExtraData",StringComparison.OrdinalIgnoreCase)){found=true;break;}
        if(!found)await db.Database.ExecuteSqlRawAsync("ALTER TABLE CapitalInvestmentRecords ADD COLUMN ExtraData TEXT NOT NULL DEFAULT '{{}}'");
    }

    static async Task SeedExamples(AppDbContext db,AppUser actor,string type)
    {
        await EnsureSqliteExtraData(db);
        var unit=await db.OrgUnits.AsNoTracking().Where(x=>x.Id==actor.OrgUnitId).Select(x=>x.Code).FirstOrDefaultAsync()
            ??await db.OrgUnits.AsNoTracking().Where(x=>x.Status!="DEACTIVATED").OrderBy(x=>x.Id).Select(x=>x.Code).FirstOrDefaultAsync()??"HQ";
        var conn=db.Database.GetDbConnection();if(conn.State!=ConnectionState.Open)await conn.OpenAsync();
            var samples=type=="CAPITAL"
                ?new[]{
                    ("CAPITAL_STRUCTURE","2026 Target Capital Mix",125000000000m,"WACC",9.2m,"APPROVED","LOW","Treasury Steering Committee","{\"debt\":52000000000,\"equity\":73000000000,\"debtEquityRatio\":0.71,\"costOfDebt\":7.4,\"costOfEquity\":12.8,\"taxRate\":20,\"wacc\":9.2}"),
                    ("FUNDING_RAISING","Green Bond Funding Programme",30000000000m,"Interest rate",7.1m,"IN_PROGRESS","MEDIUM","Partner Bank","{\"fundingType\":\"Bond\",\"interestRate\":7.1,\"tenorYears\":5,\"committedAmount\":30000000000,\"drawnAmount\":12000000000}"),
                    ("WORKING_CAPITAL","Working Capital Optimisation",18500000000m,"Cash conversion cycle",48m,"ACTIVE","MEDIUM","Internal","{\"cash\":8500000000,\"inventory\":6000000000,\"accountsReceivable\":15000000000,\"accountsPayable\":11000000000,\"cashConversionCycle\":48}"),
                    ("TREASURY","Group Liquidity Reserve",22000000000m,"Available liquidity",22000000000m,"ACTIVE","LOW","Core Bank","{\"bankName\":\"Core Bank\",\"cashBalance\":12000000000,\"creditLimit\":25000000000,\"availableCredit\":10000000000,\"minimumCash\":8000000000}"),
                    ("FINANCIAL_RISK","USD Loan Exposure Hedge",16000000000m,"Hedge Ratio",75m,"ACTIVE","MEDIUM","Partner Bank","{\"riskType\":\"Foreign Exchange\",\"exposureAmount\":16000000000,\"hedgedAmount\":12000000000,\"hedgeRatio\":75,\"hedgingInstrument\":\"Forward contract\",\"currencyPair\":\"USD/VND\"}"),
                    ("DIVIDEND_POLICY","2026 Dividend Proposal",24000000000m,"Payout Ratio",35m,"PLANNED","LOW","Shareholders","{\"profitAfterTax\":24000000000,\"dividendAmount\":8400000000,\"payoutRatio\":35,\"retainedEarnings\":15600000000,\"dividendPerShare\":1200}"),
                    ("FINANCIAL_KPI","Quarterly Capital Efficiency",14500000000m,"ROCE",13.8m,"ACTIVE","LOW","Finance Division","{\"operatingCashFlow\":18500000000,\"freeCashFlow\":14500000000,\"roce\":13.8,\"currentRatio\":1.65,\"dscr\":1.8}"),
                    ("STRATEGIC_FINANCE","Five-Year Capital Strategy",95000000000m,"Target Debt / Equity",0.8m,"IN_PROGRESS","MEDIUM","Board Strategy Committee","{\"planningHorizon\":5,\"targetFunding\":95000000000,\"targetLeverage\":0.8,\"expansionBudget\":70000000000,\"maBudget\":25000000000}")}
                :new[]{
                    ("OPPORTUNITY_PLAN","Solar Rooftop Investment Pipeline",42000000000m,"Expected ROI",15.5m,"PLANNED","MEDIUM","Energy Partner","{\"forecastRevenue\":15000000000,\"forecastProfit\":6500000000,\"investmentNeed\":42000000000,\"probability\":70}"),
                    ("APPRAISAL","BFT Energy Efficiency Upgrade",18000000000m,"IRR",17.8m,"APPROVED","LOW","ESCO Partner","{\"npv\":6200000000,\"irr\":17.8,\"roi\":21.4,\"paybackYears\":4.2,\"discountRate\":10}"),
                    ("INVESTMENT_DECISION","BFT Energy Upgrade Decision",18000000000m,"Approved Amount",18000000000m,"APPROVED","LOW","Investment Committee","{\"decision\":\"APPROVED\",\"approvedAmount\":18000000000,\"committeeDate\":\"2026-06-15\",\"approvalConditions\":\"Monthly benefit tracking\",\"decisionOwner\":\"Investment Committee\"}"),
                    ("PORTFOLIO","Core Real Estate Portfolio",72000000000m,"Unrealised Gain",12000000000m,"ACTIVE","MEDIUM","Portfolio Office","{\"assetClass\":\"Real Estate\",\"investedAmount\":60000000000,\"currentValue\":72000000000,\"ownership\":100,\"unrealisedGain\":12000000000}"),
                    ("CAPITAL_ALLOCATION","2026 Priority CAPEX Portfolio",65000000000m,"Portfolio ROI",16.2m,"IN_PROGRESS","MEDIUM","Investment Committee","{\"requestedCapex\":82000000000,\"approvedCapex\":65000000000,\"priorityScore\":86,\"expectedRoi\":16.2}"),
                    ("PERFORMANCE","Operating Investment Portfolio",58000000000m,"Actual ROI",14.6m,"ACTIVE","LOW","Portfolio Office","{\"plannedValue\":55000000000,\"actualValue\":58000000000,\"plannedRoi\":13.5,\"actualRoi\":14.6,\"variance\":3000000000,\"performanceRating\":\"OUTPERFORM\"}"),
                    ("DIVESTMENT","Non-Core Asset Exit Review",10500000000m,"Expected Gain",1500000000m,"PLANNED","HIGH","M&A Advisor","{\"bookValue\":9000000000,\"expectedProceeds\":10500000000,\"actualProceeds\":0,\"gainLoss\":1500000000,\"exitMethod\":\"Asset Sale\",\"exitReason\":\"Non-core and below target return\"}")};
            foreach(var s in samples)
            {
                await using var exists=conn.CreateCommand();exists.CommandText="SELECT COUNT(*) FROM CapitalInvestmentRecords WHERE ManagementType=@type AND Category=@category";Add(exists,"@type",type);Add(exists,"@category",s.Item1);if(Convert.ToInt64(await exists.ExecuteScalarAsync())>0)continue;
                await using var cmd=conn.CreateCommand();cmd.CommandText=db.Database.IsSqlServer()?@"INSERT INTO CapitalInvestmentRecords(ManagementType,Category,Title,OrgUnit,FiscalYear,Amount,Currency,MetricName,MetricValue,Status,RiskLevel,Counterparty,StartDate,EndDate,Notes,ExtraData,CreatedByUserId,CreatedAt,UpdatedByUserId,UpdatedAt) VALUES(@type,@category,@title,@unit,@year,@amount,'VND',@metric,@metricValue,@status,@risk,@counterparty,NULL,NULL,'Example data for analysis. You may edit or delete this record.',@extra,@uid,SYSUTCDATETIME(),@uid,SYSUTCDATETIME())":@"INSERT INTO CapitalInvestmentRecords(ManagementType,Category,Title,OrgUnit,FiscalYear,Amount,Currency,MetricName,MetricValue,Status,RiskLevel,Counterparty,StartDate,EndDate,Notes,ExtraData,CreatedByUserId,CreatedAt,UpdatedByUserId,UpdatedAt) VALUES(@type,@category,@title,@unit,@year,@amount,'VND',@metric,@metricValue,@status,@risk,@counterparty,NULL,NULL,'Example data for analysis. You may edit or delete this record.',@extra,@uid,CURRENT_TIMESTAMP,@uid,CURRENT_TIMESTAMP)";
                Add(cmd,"@type",type);Add(cmd,"@category",s.Item1);Add(cmd,"@title",s.Item2);Add(cmd,"@unit",unit);Add(cmd,"@year",DateTime.Now.Year);Add(cmd,"@amount",s.Item3);Add(cmd,"@metric",s.Item4);Add(cmd,"@metricValue",s.Item5);Add(cmd,"@status",s.Item6);Add(cmd,"@risk",s.Item7);Add(cmd,"@counterparty",s.Item8);Add(cmd,"@extra",s.Item9);Add(cmd,"@uid",actor.Id);await cmd.ExecuteNonQueryAsync();
            }
    }

    static async Task<(AppUser User,bool CanEdit)?> Actor(HttpContext http,AppDbContext db,string module)
    {
        var uid=Convert.ToInt64(http.Items["AuthUserId"]);var user=await db.Users.FirstOrDefaultAsync(x=>x.Id==uid);if(user is null)return null;
        var root=string.Equals(user.Role,"ROOT",StringComparison.OrdinalIgnoreCase);
        var edit=root||await RbacService.CanAsync(db,user,module,"EDIT")||await RbacService.CanAsync(db,user,module,"CREATE")||await RbacService.CanAsync(db,user,module,"FULL");
        return (user,edit);
    }

    public static void MapCapitalInvestmentEndpoints(this WebApplication app)
    {
        app.MapGet("/api/capital-investment",async(HttpContext http,AppDbContext db,string? managementType,string? category,int? fiscalYear,string? orgUnit,string? status)=>
        {
            var module=string.Equals(managementType,"INVESTMENT",StringComparison.OrdinalIgnoreCase)?"INVESTMENT":"CAPITAL";var actor=await Actor(http,db,module);if(actor is null)return Results.Unauthorized();await EnsureSchema(db);await SeedExamples(db,actor.Value.User,module);
            var canView=string.Equals(actor.Value.User.Role,"ROOT",StringComparison.OrdinalIgnoreCase)||await RbacService.CanAsync(db,actor.Value.User,module,"VIEW")||actor.Value.CanEdit;if(!canView)return Results.StatusCode(403);var isRoot=string.Equals(actor.Value.User.Role,"ROOT",StringComparison.OrdinalIgnoreCase);var allScope=isRoot||await RbacService.IsAllScopeAsync(db,actor.Value.User,module);var allowedCodes=new List<string>();if(!allScope){var ids=await RbacService.AllowedOrgIdsAsync(db,actor.Value.User,module);if(ids.Length==0&&actor.Value.User.OrgUnitId>0)ids=new[]{actor.Value.User.OrgUnitId};allowedCodes=await db.OrgUnits.AsNoTracking().Where(x=>ids.Contains(x.Id)).Select(x=>x.Code).ToListAsync();}
            var scopeSql=allScope?"":allowedCodes.Count==0?" AND 1=0":" AND r.OrgUnit IN ("+string.Join(",",allowedCodes.Select((_,i)=>$"@scope{i}"))+")";
            var sql=@"SELECT r.Id,r.ManagementType,r.Category,r.Title,r.OrgUnit,r.FiscalYear,r.Amount,r.Currency,r.MetricName,r.MetricValue,r.Status,r.RiskLevel,r.Counterparty,r.StartDate,r.EndDate,r.Notes,r.ExtraData,r.UpdatedAt,u.Name CreatedBy FROM CapitalInvestmentRecords r LEFT JOIN Users u ON u.Id=r.CreatedByUserId WHERE (@type='' OR r.ManagementType=@type) AND (@category='' OR r.Category=@category) AND (@year=0 OR r.FiscalYear=@year) AND (@unit='' OR r.OrgUnit=@unit) AND (@status='' OR r.Status=@status)"+scopeSql+" ORDER BY r.UpdatedAt DESC";
            var conn=db.Database.GetDbConnection();if(conn.State!=ConnectionState.Open)await conn.OpenAsync();await using var cmd=conn.CreateCommand();cmd.CommandText=sql;Add(cmd,"@type",(managementType??"").Trim().ToUpperInvariant());Add(cmd,"@category",category??"");Add(cmd,"@year",fiscalYear??0);Add(cmd,"@unit",orgUnit??"");Add(cmd,"@status",status??"");for(var i=0;i<allowedCodes.Count;i++)Add(cmd,$"@scope{i}",allowedCodes[i]);
            var rows=new List<object>();await using var reader=await cmd.ExecuteReaderAsync();while(await reader.ReadAsync())rows.Add(new{id=reader.GetInt64(0),managementType=reader.GetString(1),category=reader.GetString(2),title=reader.GetString(3),orgUnit=reader.GetString(4),fiscalYear=reader.GetInt32(5),amount=reader.GetDecimal(6),currency=reader.GetString(7),metricName=reader.GetString(8),metricValue=reader.GetDecimal(9),status=reader.GetString(10),riskLevel=reader.GetString(11),counterparty=reader.GetString(12),startDate=reader.IsDBNull(13)?null:reader.GetDateTime(13).ToString("yyyy-MM-dd"),endDate=reader.IsDBNull(14)?null:reader.GetDateTime(14).ToString("yyyy-MM-dd"),notes=reader.GetString(15),extraData=JsonSerializer.Deserialize<Dictionary<string,object?>>(reader.IsDBNull(16)?"{}":reader.GetString(16)),updatedAt=reader.GetDateTime(17),createdBy=reader.IsDBNull(18)?"System":reader.GetString(18)});return Results.Ok(rows);
        });

        app.MapPost("/api/capital-investment",async(JsonElement body,HttpContext http,AppDbContext db)=>
        {
            var type=Text(body,"managementType").ToUpperInvariant();var module=type=="INVESTMENT"?"INVESTMENT":"CAPITAL";var actor=await Actor(http,db,module);if(actor is null)return Results.Unauthorized();if(!actor.Value.CanEdit)return Results.StatusCode(403);await EnsureSchema(db);
            var title=Text(body,"title");var unit=Text(body,"orgUnit");var year=Integer(body,"fiscalYear");if(type is not("CAPITAL" or "INVESTMENT")||title==""||unit==""||year<2020)return Results.BadRequest(new{message="Management Type, Title, Business Unit and Fiscal Year are required."});
            if(!await RbacService.CanModuleOrgAsync(db,actor.Value.User,module,unit))return Results.StatusCode(403);
            var sql=db.Database.IsSqlServer()?@"INSERT INTO CapitalInvestmentRecords(ManagementType,Category,Title,OrgUnit,FiscalYear,Amount,Currency,MetricName,MetricValue,Status,RiskLevel,Counterparty,StartDate,EndDate,Notes,ExtraData,CreatedByUserId,CreatedAt,UpdatedByUserId,UpdatedAt) OUTPUT INSERTED.Id VALUES(@type,@category,@title,@unit,@year,@amount,@currency,@metric,@metricValue,@status,@risk,@counterparty,@start,@end,@notes,@extra,@uid,SYSUTCDATETIME(),@uid,SYSUTCDATETIME())":@"INSERT INTO CapitalInvestmentRecords(ManagementType,Category,Title,OrgUnit,FiscalYear,Amount,Currency,MetricName,MetricValue,Status,RiskLevel,Counterparty,StartDate,EndDate,Notes,ExtraData,CreatedByUserId,CreatedAt,UpdatedByUserId,UpdatedAt) VALUES(@type,@category,@title,@unit,@year,@amount,@currency,@metric,@metricValue,@status,@risk,@counterparty,@start,@end,@notes,@extra,@uid,CURRENT_TIMESTAMP,@uid,CURRENT_TIMESTAMP); SELECT last_insert_rowid();";
            var conn=db.Database.GetDbConnection();if(conn.State!=ConnectionState.Open)await conn.OpenAsync();await using var cmd=conn.CreateCommand();cmd.CommandText=sql;Bind(cmd,body,actor.Value.User.Id,type,title,unit,year);var id=Convert.ToInt64(await cmd.ExecuteScalarAsync());return Results.Created($"/api/capital-investment/{id}",new{id});
        });

        app.MapPut("/api/capital-investment/{id:long}",async(long id,JsonElement body,HttpContext http,AppDbContext db)=>
        {
            var type=Text(body,"managementType").ToUpperInvariant();var module=type=="INVESTMENT"?"INVESTMENT":"CAPITAL";var actor=await Actor(http,db,module);if(actor is null)return Results.Unauthorized();if(!actor.Value.CanEdit)return Results.StatusCode(403);await EnsureSchema(db);var title=Text(body,"title");var unit=Text(body,"orgUnit");var year=Integer(body,"fiscalYear");if(title==""||unit==""||year<2020)return Results.BadRequest(new{message="Title, Business Unit and Fiscal Year are required."});if(!await RbacService.CanModuleOrgAsync(db,actor.Value.User,module,unit))return Results.StatusCode(403);
            var conn=db.Database.GetDbConnection();if(conn.State!=ConnectionState.Open)await conn.OpenAsync();await using var cmd=conn.CreateCommand();cmd.CommandText=db.Database.IsSqlServer()?@"UPDATE CapitalInvestmentRecords SET ManagementType=@type,Category=@category,Title=@title,OrgUnit=@unit,FiscalYear=@year,Amount=@amount,Currency=@currency,MetricName=@metric,MetricValue=@metricValue,Status=@status,RiskLevel=@risk,Counterparty=@counterparty,StartDate=@start,EndDate=@end,Notes=@notes,ExtraData=@extra,UpdatedByUserId=@uid,UpdatedAt=SYSUTCDATETIME() WHERE Id=@id":@"UPDATE CapitalInvestmentRecords SET ManagementType=@type,Category=@category,Title=@title,OrgUnit=@unit,FiscalYear=@year,Amount=@amount,Currency=@currency,MetricName=@metric,MetricValue=@metricValue,Status=@status,RiskLevel=@risk,Counterparty=@counterparty,StartDate=@start,EndDate=@end,Notes=@notes,ExtraData=@extra,UpdatedByUserId=@uid,UpdatedAt=CURRENT_TIMESTAMP WHERE Id=@id";Bind(cmd,body,actor.Value.User.Id,type,title,unit,year);Add(cmd,"@id",id);return await cmd.ExecuteNonQueryAsync()==0?Results.NotFound():Results.Ok(new{id});
        });

        app.MapDelete("/api/capital-investment/{id:long}",async(long id,HttpContext http,AppDbContext db)=>
        {
            await EnsureSchema(db);var conn=db.Database.GetDbConnection();if(conn.State!=ConnectionState.Open)await conn.OpenAsync();
            await using var lookup=conn.CreateCommand();lookup.CommandText="SELECT ManagementType,OrgUnit FROM CapitalInvestmentRecords WHERE Id=@id";Add(lookup,"@id",id);
            string type="",unit="";await using(var reader=await lookup.ExecuteReaderAsync()){if(!await reader.ReadAsync())return Results.NotFound();type=reader.GetString(0);unit=reader.GetString(1);}
            var module=type.Equals("INVESTMENT",StringComparison.OrdinalIgnoreCase)?"INVESTMENT":"CAPITAL";var actor=await Actor(http,db,module);if(actor is null)return Results.Unauthorized();if(!actor.Value.CanEdit)return Results.StatusCode(403);if(!await RbacService.CanModuleOrgAsync(db,actor.Value.User,module,unit))return Results.StatusCode(403);
            var rows=await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM CapitalInvestmentRecords WHERE Id={id}");return rows==0?Results.NotFound():Results.NoContent();
        });
    }

    static void Bind(IDbCommand cmd,JsonElement body,long uid,string type,string title,string unit,int year)
    {
        Add(cmd,"@type",type);Add(cmd,"@category",Text(body,"category"));Add(cmd,"@title",title);Add(cmd,"@unit",unit);Add(cmd,"@year",year);Add(cmd,"@amount",Number(body,"amount"));Add(cmd,"@currency",Text(body,"currency"));Add(cmd,"@metric",Text(body,"metricName"));Add(cmd,"@metricValue",Number(body,"metricValue"));Add(cmd,"@status",Text(body,"status"));Add(cmd,"@risk",Text(body,"riskLevel"));Add(cmd,"@counterparty",Text(body,"counterparty"));Add(cmd,"@start",Date(body,"startDate"));Add(cmd,"@end",Date(body,"endDate"));Add(cmd,"@notes",Text(body,"notes"));Add(cmd,"@extra",Json(body,"extraData"));Add(cmd,"@uid",uid);
    }
}
