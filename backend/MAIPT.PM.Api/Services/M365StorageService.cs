using System.Net.Http.Headers;
using System.Text.Json;

namespace MAIPT.PM.Api.Services;

public sealed record M365StoredFile(string DriveId,string ItemId,string FileName,string WebUrl,long Size,string? MimeType,string Path);
public sealed record M365RemoteFile(string DriveId,string ItemId,string FileName,string WebUrl,long Size,string MimeType,string Path,DateTime LastModifiedAt,string UploadedByEmail);

public sealed class M365StorageService
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _cfg;
    public M365StorageService(IHttpClientFactory factory,IConfiguration cfg){_factory=factory;_cfg=cfg;}

    public bool Enabled => string.Equals(Environment.GetEnvironmentVariable("MPMS_M365_ENABLED")??_cfg["M365Storage:Enabled"],"true",StringComparison.OrdinalIgnoreCase);
    string TenantId => EnvOrCfg("MPMS_M365_TENANT_ID","M365Storage:TenantId");
    string ClientId => EnvOrCfg("MPMS_M365_CLIENT_ID","M365Storage:ClientId");
    // Client secrets must never fall back to appsettings.json because that file is
    // copied into publish output and is commonly tracked by source control.
    string ClientSecret => Environment.GetEnvironmentVariable("MPMS_M365_CLIENT_SECRET")??"";
    public string DriveId => EnvOrCfg("MPMS_M365_DRIVE_ID","M365Storage:DriveId");

    string EnvOrCfg(string env,string key){var v=Environment.GetEnvironmentVariable(env);return !string.IsNullOrWhiteSpace(v)?v:(_cfg[key]??"");}

    async Task<string> TokenAsync(CancellationToken ct=default)
    {
        if(string.IsNullOrWhiteSpace(TenantId)||string.IsNullOrWhiteSpace(ClientId)||string.IsNullOrWhiteSpace(ClientSecret)||string.IsNullOrWhiteSpace(DriveId))
            throw new InvalidOperationException("M365 SharePoint storage is not fully configured.");
        var http=_factory.CreateClient();
        var body=new FormUrlEncodedContent(new Dictionary<string,string>{{"client_id",ClientId},{"client_secret",ClientSecret},{"scope","https://graph.microsoft.com/.default"},{"grant_type","client_credentials"}});
        var r=await http.PostAsync($"https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/token",body,ct);
        var txt=await r.Content.ReadAsStringAsync(ct);
        if(!r.IsSuccessStatusCode) throw new InvalidOperationException($"M365 token request failed: {(int)r.StatusCode} {txt}");
        using var d=JsonDocument.Parse(txt); return d.RootElement.GetProperty("access_token").GetString()??throw new InvalidOperationException("M365 access token missing.");
    }

    async Task<HttpClient> GraphAsync(CancellationToken ct=default){var h=_factory.CreateClient();h.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",await TokenAsync(ct));return h;}
    static string Safe(string? v){var s=(v??"").Trim();if(string.IsNullOrWhiteSpace(s))return "General";foreach(var c in new[]{'\\','/',':','*','?','\"','<','>','|','#','%'})s=s.Replace(c,'-');return s.Trim().Trim('.');}
    static string Esc(string p)=>string.Join("/",p.Split('/',StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));

    public static string BuildEntityFolder(string entityType,string entityCode)
    {
        var t=(entityType??"").Trim().ToUpperInvariant();
        var code=Safe(entityCode);
        return t switch
        {
            "PROJECT" or "PROJECTS" => $"PROJECT MANAGEMENT/PROJECTS/{code}",
            "CONTRACT" or "CONTRACTS" => $"FINANCIAL CONTROL/CONTRACTS/{code}",
            "SUPPLIER" or "SUPPLIERS" => $"ORGANIZATION/SUPPLIERS/{code}",
            "BUDGET" => $"FINANCIAL CONTROL/BUDGET/{code}",
            "KPI" or "PERFORMANCE" => $"PERFORMANCE/KPI/{code}",
            "IT_ASSET" or "IT-ASSET" or "ASSET" => $"IT ASSETS & SERVICES/ASSETS/{code}",
            "IT_HANDOVER" or "HANDOVER" => $"IT ASSETS & SERVICES/HANDOVER/{code}",
            "IT_DOMAIN" => $"IT ASSETS & SERVICES/DOMAINS/{code}",
            "LICENSE" or "LICENSES" => $"IT ASSETS & SERVICES/LICENSES/{code}",
            "CLUB" => $"PERSONAL FC/CLUB/{code}",
            "CLUB_LIBRARY" or "CLUB_LOGO" or "CLUB_REGULATION" or "CLUB_TEAM_IMAGE" => $"PERSONAL FC/CLUB/{t}/{code}",
            "DOCUMENT" => $"GENERAL/{code}",
            _ => $"GENERAL/{t}/{code}"
        };
    }

    public static string BuildCategoryFolder(string category)
    {
        var c=(category??"GENERAL").Trim();
        var u=c.ToUpperInvariant();
        if(u.StartsWith("CLUB_")) return $"PERSONAL FC/CLUB/{Safe(c)}";
        if(u=="IT POLICIES & PROCEDURES") return "IT ASSETS & SERVICES/IT Policies & Procedures";
        if(u.Contains("CONTRACT")) return $"FINANCIAL CONTROL/CONTRACTS/{Safe(c)}";
        if(u.Contains("BUDGET")) return $"FINANCIAL CONTROL/BUDGET/{Safe(c)}";
        if(u.Contains("SUPPLIER")) return $"ORGANIZATION/SUPPLIERS/{Safe(c)}";
        if(u.Contains("KPI")||u.Contains("PERFORMANCE")) return $"PERFORMANCE/{Safe(c)}";
        if(u.Contains("CAPITAL")) return $"CAPITAL & INVESTMENT/CAPITAL MANAGEMENT/{Safe(c)}";
        if(u.Contains("INVEST")) return $"CAPITAL & INVESTMENT/INVESTMENT MANAGEMENT/{Safe(c)}";
        if(u.Contains("DOMAIN")) return $"IT ASSETS & SERVICES/DOMAINS/{Safe(c)}";
        if(u.Contains("HANDOVER")) return $"IT ASSETS & SERVICES/HANDOVER/{Safe(c)}";
        if(u.Contains("ASSET")||u.Contains("LICENSE")) return $"IT ASSETS & SERVICES/{Safe(c)}";
        if(u=="AVATAR") return "SYSTEM/AVATAR";
        if(u=="GENERAL") return "SYSTEM/GENERAL";
        return $"GENERAL/{Safe(c)}";
    }

    public async Task EnsureFolderAsync(string folderPath,CancellationToken ct=default)
    {
        var http=await GraphAsync(ct); var path="";
        foreach(var raw in folderPath.Split('/',StringSplitOptions.RemoveEmptyEntries))
        {
            var name=Safe(raw); path=string.IsNullOrWhiteSpace(path)?name:$"{path}/{name}";
            var get=await http.GetAsync($"https://graph.microsoft.com/v1.0/drives/{DriveId}/root:/{Esc(path)}",ct);
            if(get.IsSuccessStatusCode) continue;
            var parent=path.Contains('/')?path[..path.LastIndexOf('/')]:"";
            var url=string.IsNullOrWhiteSpace(parent)?$"https://graph.microsoft.com/v1.0/drives/{DriveId}/root/children":$"https://graph.microsoft.com/v1.0/drives/{DriveId}/root:/{Esc(parent)}:/children";
            var json=JsonSerializer.Serialize(new{name,folder=new{},conflictBehavior="fail"}).Replace("\"conflictBehavior\"","\"@microsoft.graph.conflictBehavior\"");
            var r=await http.PostAsync(url,new StringContent(json,System.Text.Encoding.UTF8,"application/json"),ct);
            if(!r.IsSuccessStatusCode && (int)r.StatusCode!=409){var txt=await r.Content.ReadAsStringAsync(ct);throw new InvalidOperationException($"Unable to create SharePoint folder {path}: {(int)r.StatusCode} {txt}");}
        }
    }

    public async Task<M365StoredFile> UploadAsync(Stream stream,string fileName,string mimeType,string folderPath,CancellationToken ct=default)
    {
        await EnsureFolderAsync(folderPath,ct); var http=await GraphAsync(ct); var full=$"{folderPath.TrimEnd('/')}/{Safe(Path.GetFileName(fileName))}";
        using var c=new StreamContent(stream); c.Headers.ContentType=new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(mimeType)?"application/octet-stream":mimeType);
        var r=await http.PutAsync($"https://graph.microsoft.com/v1.0/drives/{DriveId}/root:/{Esc(full)}:/content",c,ct); var txt=await r.Content.ReadAsStringAsync(ct);
        if(!r.IsSuccessStatusCode) throw new InvalidOperationException($"SharePoint upload failed: {(int)r.StatusCode} {txt}");
        using var d=JsonDocument.Parse(txt); var x=d.RootElement;
        return new M365StoredFile(DriveId,x.GetProperty("id").GetString()!,x.GetProperty("name").GetString()??fileName,x.TryGetProperty("webUrl",out var wu)?wu.GetString()??"":"",x.TryGetProperty("size",out var sz)?sz.GetInt64():0,mimeType,full);
    }

    public async Task<IReadOnlyList<M365RemoteFile>> ListFilesRecursiveAsync(string? folderPath=null,CancellationToken ct=default)
    {
        var http=await GraphAsync(ct);
        var files=new List<M365RemoteFile>();
        var folders=new Queue<string>();
        folders.Enqueue((folderPath??"").Trim().Trim('/'));

        while(folders.Count>0)
        {
            var current=folders.Dequeue();
            var select="$select=id,name,size,webUrl,file,folder,lastModifiedDateTime,createdBy&$top=200";
            string? next=string.IsNullOrWhiteSpace(current)
                ?$"https://graph.microsoft.com/v1.0/drives/{DriveId}/root/children?{select}"
                :$"https://graph.microsoft.com/v1.0/drives/{DriveId}/root:/{Esc(current)}:/children?{select}";

            while(!string.IsNullOrWhiteSpace(next))
            {
                var response=await http.GetAsync(next,ct);
                var text=await response.Content.ReadAsStringAsync(ct);
                if(!response.IsSuccessStatusCode)
                    throw new InvalidOperationException($"Unable to list SharePoint folder '{current}': {(int)response.StatusCode} {text}");

                using var json=JsonDocument.Parse(text);
                foreach(var item in json.RootElement.GetProperty("value").EnumerateArray())
                {
                    var name=item.TryGetProperty("name",out var n)?n.GetString()??"":"";
                    if(string.IsNullOrWhiteSpace(name))continue;
                    var path=string.IsNullOrWhiteSpace(current)?name:$"{current}/{name}";
                    if(item.TryGetProperty("folder",out _))
                    {
                        folders.Enqueue(path);
                        continue;
                    }
                    if(!item.TryGetProperty("file",out var file))continue;
                    var id=item.GetProperty("id").GetString()??"";
                    if(string.IsNullOrWhiteSpace(id))continue;
                    var mime=file.TryGetProperty("mimeType",out var mt)?mt.GetString()??"application/octet-stream":"application/octet-stream";
                    var modified=item.TryGetProperty("lastModifiedDateTime",out var lm)&&lm.TryGetDateTime(out var dt)?dt:DateTime.UtcNow;
                    var uploadedByEmail="";
                    if(item.TryGetProperty("createdBy",out var createdBy)&&createdBy.TryGetProperty("user",out var createdUser))
                    {
                        if(createdUser.TryGetProperty("email",out var email))uploadedByEmail=email.GetString()??"";
                        if(string.IsNullOrWhiteSpace(uploadedByEmail)&&createdUser.TryGetProperty("userPrincipalName",out var upn))uploadedByEmail=upn.GetString()??"";
                    }
                    files.Add(new M365RemoteFile(
                        DriveId,id,name,
                        item.TryGetProperty("webUrl",out var wu)?wu.GetString()??"":"",
                        item.TryGetProperty("size",out var sz)?sz.GetInt64():0,
                        mime,path,modified,uploadedByEmail));
                }
                next=json.RootElement.TryGetProperty("@odata.nextLink",out var nl)?nl.GetString():null;
            }
        }
        return files;
    }

    public async Task<(Stream Stream,string MimeType,string FileName)> DownloadAsync(string itemId,string fileName,string? mimeType,CancellationToken ct=default)
    {
        var http=await GraphAsync(ct); var r=await http.GetAsync($"https://graph.microsoft.com/v1.0/drives/{DriveId}/items/{itemId}/content",HttpCompletionOption.ResponseHeadersRead,ct);
        if(!r.IsSuccessStatusCode) throw new FileNotFoundException($"SharePoint item {itemId} not found.");
        var ms=new MemoryStream(); await r.Content.CopyToAsync(ms,ct); ms.Position=0; return (ms,mimeType??r.Content.Headers.ContentType?.MediaType??"application/octet-stream",fileName);
    }

    public async Task DeleteAsync(string itemId,CancellationToken ct=default)
    {
        var http=await GraphAsync(ct); var r=await http.DeleteAsync($"https://graph.microsoft.com/v1.0/drives/{DriveId}/items/{itemId}",ct);
        if(!r.IsSuccessStatusCode && (int)r.StatusCode!=404){var txt=await r.Content.ReadAsStringAsync(ct);throw new InvalidOperationException($"SharePoint delete failed: {(int)r.StatusCode} {txt}");}
    }
}
