using System.Text;
using System.Text.Json;

namespace MAIPT.PM.Api.Endpoints;

public static class AIHelpEndpoints
{
    public static void MapAIHelpEndpoints(this WebApplication app)
    {
        app.MapPost("/api/ai-help/ask", async (AIHelpRequest input, IWebHostEnvironment env) =>
        {
            var question=(input.Question??"").Trim();
            var view=(input.View??"").Trim();
            if(string.IsNullOrWhiteSpace(question))
                return Results.BadRequest(new{message="Please enter a question."});

            var q=Normalize(question);

            var blocked=new[]{"database"," db ","sql","api ","endpoint","source code","server","hosting","monster","web.config","iis","backend","frontend","connection string","schema","runtime","deploy","deployment","ssh","ftp"};
            if(blocked.Any(x=>q.Contains(Normalize(x))))
                return Results.Ok(new{answer="I only help end users use MPMS. I cannot provide system, database, source-code, server or deployment information. Please ask how to perform the task in the MPMS interface.",mode="restricted"});

            var kbPath=Path.Combine(env.ContentRootPath,"knowledge","ai-help-kb.json");
            if(!File.Exists(kbPath))
                kbPath=Path.Combine(env.ContentRootPath,"data","ai-help-kb.json");

            if(!File.Exists(kbPath))
                return Results.Ok(new{answer="The MPMS user guide is not available yet. Please contact the MPMS administrator.",mode="fallback"});

            var items=JsonSerializer.Deserialize<List<AIHelpItem>>(await File.ReadAllTextAsync(kbPath),new JsonSerializerOptions{PropertyNameCaseInsensitive=true}) ?? new();

            var ranked=items.Select(x=>new{
                Item=x,
                QuestionScore=QuestionScore(x,q),
                ViewScore=ViewScore(x,view)
            }).OrderByDescending(x=>x.QuestionScore)
              .ThenByDescending(x=>x.ViewScore)
              .ThenBy(x=>x.Item.Title)
              .ToList();

            var best=ranked.FirstOrDefault();

            if(best is not null && best.QuestionScore>0)
                return Results.Ok(new{answer=best.Item.Answer,title=best.Item.Title,mode="guide",matchedBy="question"});

            if(IsVague(q))
            {
                var byView=ranked.Where(x=>x.ViewScore>0).OrderByDescending(x=>x.ViewScore).FirstOrDefault();
                if(byView is not null)
                    return Results.Ok(new{answer=byView.Item.Answer,title=byView.Item.Title,mode="guide",matchedBy="currentView"});
            }

            return Results.Ok(new{answer="I could not identify which MPMS function you mean. Please include the feature name, for example: Project, Performance & KPI, Budget, Contract, Supplier, IT Asset, License, Domain, Permission, Import or Export.",mode="clarify"});
        });
    }

    static int QuestionScore(AIHelpItem item,string question)
    {
        var score=0;
        foreach(var keyword in item.Keywords??new List<string>())
        {
            var k=Normalize(keyword);
            if(string.IsNullOrWhiteSpace(k)) continue;
            if(question==k) score+=100;
            else if(question.Contains(k)) score+=20+Math.Min(k.Length,30);
        }

        var title=Normalize(item.Title??"");
        if(!string.IsNullOrWhiteSpace(title) && question.Contains(title)) score+=25;

        foreach(var token in Tokenize(question))
        {
            if(token.Length<4) continue;
            if(Tokenize(title).Contains(token)) score+=3;
            foreach(var k in item.Keywords??new List<string>())
                if(Tokenize(Normalize(k)).Contains(token)) score+=2;
        }
        return score;
    }

    static int ViewScore(AIHelpItem item,string view)
    {
        if(string.IsNullOrWhiteSpace(view)) return 0;
        var v=Normalize(view);
        var intent=Normalize(item.Intent??"");
        var title=Normalize(item.Title??"");
        var score=0;
        if(!string.IsNullOrWhiteSpace(intent) && v==intent) score+=10;
        if(!string.IsNullOrWhiteSpace(intent) && (v.Contains(intent)||intent.Contains(v))) score+=5;
        if(!string.IsNullOrWhiteSpace(title) && v.Contains(title)) score+=2;
        return score;
    }

    static bool IsVague(string q)
    {
        var featureWords=new[]{"project","performance","kpi","budget","contract","supplier","asset","license","domain","permission","import","export","handover","maintenance"};
        return !featureWords.Any(x=>q.Contains(x));
    }

    static HashSet<string> Tokenize(string s)=>
        s.Split(' ',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).ToHashSet();

    static string Normalize(string value)
    {
        if(string.IsNullOrWhiteSpace(value)) return "";
        var src=value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb=new StringBuilder();
        foreach(var c in src)
        {
            var uc=System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if(uc==System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if(char.IsLetterOrDigit(c)||char.IsWhiteSpace(c)) sb.Append(c);
            else sb.Append(' ');
        }
        return string.Join(' ',sb.ToString().Normalize(NormalizationForm.FormC).Split(' ',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries));
    }
}

public sealed class AIHelpRequest
{
    public string? Question{get;set;}
    public string? View{get;set;}
}

public sealed class AIHelpItem
{
    public string? Id{get;set;}
    public string? Title{get;set;}
    public string? Intent{get;set;}
    public List<string>? Keywords{get;set;}
    public string? Answer{get;set;}
}
