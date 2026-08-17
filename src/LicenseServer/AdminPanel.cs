using System.Net;
using System.Text;
using System.Text.Json;

namespace LicenseServer;

public static class AdminPanel
{
    public static void Map(WebApplication app, Func<string, bool> authorize, Func<string, string?> createKey, Func<object> listKeys, Func<string, bool> revokeKey, Func<string, bool> resetHwid, Func<string, int, bool> extendKey)
    {
        app.MapGet("/admin", () => Results.Content(Html(), "text/html; charset=utf-8"));
        app.MapGet("/admin/api/keys", (HttpRequest r) =>
        {
            if (!authorize(r.Headers.Authorization.ToString())) return Results.Unauthorized();
            return Results.Json(listKeys());
        });
        app.MapPost("/admin/api/keys", async (HttpRequest r) =>
        {
            if (!authorize(r.Headers.Authorization.ToString())) return Results.Unauthorized();
            using var doc = await JsonDocument.ParseAsync(r.Body);
            var plan = doc.RootElement.GetProperty("plan").GetString() ?? "";
            var key = createKey(plan);
            return key is null ? Results.BadRequest(new { error = "unsupported_plan" }) : Results.Ok(new { key });
        });
        app.MapPost("/admin/api/revoke", async (HttpRequest r) =>
        {
            if (!authorize(r.Headers.Authorization.ToString())) return Results.Unauthorized();
            using var doc = await JsonDocument.ParseAsync(r.Body); return Results.Ok(new { success = revokeKey(doc.RootElement.GetProperty("key").GetString() ?? "") });
        });
        app.MapPost("/admin/api/reset-hwid", async (HttpRequest r) =>
        {
            if (!authorize(r.Headers.Authorization.ToString())) return Results.Unauthorized();
            using var doc = await JsonDocument.ParseAsync(r.Body); return Results.Ok(new { success = resetHwid(doc.RootElement.GetProperty("key").GetString() ?? "") });
        });
        app.MapPost("/admin/api/extend", async (HttpRequest r) =>
        {
            if (!authorize(r.Headers.Authorization.ToString())) return Results.Unauthorized();
            using var doc = await JsonDocument.ParseAsync(r.Body); var key = doc.RootElement.GetProperty("key").GetString() ?? ""; var days = doc.RootElement.GetProperty("days").GetInt32();
            return Results.Ok(new { success = extendKey(key, days) });
        });
    }

    static string Html() => """
<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>UndOpti License Panel</title><style>body{font-family:system-ui;background:#0d1117;color:#e6edf3;margin:0;padding:28px}main{max-width:1100px;margin:auto}h1{margin-bottom:4px}.muted{color:#8b949e}.card{background:#161b22;border:1px solid #30363d;border-radius:12px;padding:18px;margin:16px 0}button,input,select{padding:10px;border-radius:8px;border:1px solid #30363d;background:#0d1117;color:#e6edf3;margin:4px}button{cursor:pointer}table{width:100%;border-collapse:collapse}td,th{text-align:left;padding:9px;border-bottom:1px solid #30363d;font-size:14px}.key{font-family:monospace}</style></head><body><main><h1>UndOpti License Panel</h1><div class='muted'>Private administrator console</div><div class='card'><input id='token' type='password' placeholder='Admin token' style='width:260px'><select id='plan'><option value='1d'>1 Day</option><option value='7d'>7 Days</option><option value='30d'>30 Days</option><option value='1y'>1 Year</option><option value='lifetime'>Lifetime</option></select><button onclick='generate()'>Generate key</button><button onclick='load()'>Refresh</button><div id='new'></div></div><div class='card'><table><thead><tr><th>Key</th><th>Plan</th><th>HWID</th><th>Expires</th><th>Actions</th></tr></thead><tbody id='rows'></tbody></table></div></main><script>const auth=()=>({Authorization:'Bearer '+document.getElementById('token').value,'Content-Type':'application/json'});async function load(){let r=await fetch('/admin/api/keys',{headers:auth()});if(!r.ok){alert('Unauthorized');return}let d=await r.json();rows.innerHTML=d.map(x=>`<tr><td class='key'>${x.key}</td><td>${x.plan}</td><td class='key'>${x.hardwareId||'—'}</td><td>${x.expiresAt||'Lifetime'}</td><td><button onclick="post('/admin/api/revoke',{key:'${x.key}'})">Revoke</button><button onclick="post('/admin/api/reset-hwid',{key:'${x.key}'})">Reset HWID</button></td></tr>`).join('')}async function generate(){let plan=document.getElementById('plan').value;let r=await fetch('/admin/api/keys',{method:'POST',headers:auth(),body:JSON.stringify({plan})});let d=await r.json();document.getElementById('new').innerHTML=r.ok?`<p class='key'>${d.key}</p>`:`<p>${d.error||'Error'}</p>`;load()}async function post(u,b){let r=await fetch(u,{method:'POST',headers:auth(),body:JSON.stringify(b)});if(!r.ok)alert('Unauthorized/error');load()}</script></body></html>
""";
}
