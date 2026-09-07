using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

const string Host = "192.168.78.12";
const int Port = 9330;
const string ZoneWanted = "Living Room";

Console.WriteLine($"Roon Core {Host}:{Port}  zone '{ZoneWanted}'");
Console.WriteLine("If zones come back Unauthorized, enable this extension in Roon:");
Console.WriteLine("  Settings → Extensions → Cadence Works Crestron Probe → Enable");
Console.WriteLine();

using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
using var ws = new ClientWebSocket();
await ws.ConnectAsync(new Uri($"ws://{Host}:{Port}/api"), cts.Token);
Console.WriteLine("WebSocket open — leave this running.");
Console.WriteLine("In Roon on the Mac Mini: Settings → Extensions.");
Console.WriteLine("You should see Cadence Works Crestron Probe. Enable it.");
Console.WriteLine();

var nextId = 0;
int NextId() => nextId++;
var pending = new Dictionary<int, string>();

var infoId = NextId();
pending[infoId] = "info";
await SendRequestAsync(ws, infoId, "com.roonlabs.registry:1/info", null, cts.Token);

var registerId = NextId();
pending[registerId] = "register";
var registerBody = JsonSerializer.Serialize(new
{
    extension_id = "com.cadenceworks.crestron.probe",
    display_name = "Cadence Works Crestron Probe",
    display_version = "0.0.1",
    publisher = "Cadence Works/Cursor",
    email = "probe@cadenceworks.local",
    required_services = new[] { "com.roonlabs.transport:2" },
    optional_services = Array.Empty<string>(),
    provided_services = new[] { "com.roonlabs.pairing:1", "com.roonlabs.ping:1" }
});
await SendRequestAsync(ws, registerId, "com.roonlabs.registry:1/register", registerBody, cts.Token);

var subscribed = false;
var coreId = "";
while (!cts.IsCancellationRequested && ws.State == WebSocketState.Open)
{
    string raw;
    try
    {
        raw = await ReadMessageAsync(ws, cts.Token);
    }
    catch (OperationCanceledException)
    {
        break;
    }

    var msg = ParseMoo(raw);
    Console.WriteLine($"<- {msg.Verb} {msg.Name} id={msg.RequestId} {Truncate(msg.Body, 500)}");

    if (msg.Verb == "COMPLETE" && msg.Name == "Success" && msg.Body.Contains("core_id"))
    {
        try
        {
            using var doc = JsonDocument.Parse(msg.Body);
            if (doc.RootElement.TryGetProperty("core_id", out var cid))
            {
                coreId = cid.GetString() ?? coreId;
            }
        }
        catch
        {
        }
    }

    if (msg.Verb == "REQUEST" && msg.Service == "com.roonlabs.ping:1" && msg.Name == "ping")
    {
        await SendCompleteAsync(ws, msg.RequestId, "Success", null, cts.Token);
        continue;
    }

    if (msg.Verb == "REQUEST" && msg.Service == "com.roonlabs.pairing:1")
    {
        var pairing = "{\"paired_core_id\":\"" + coreId + "\"}";
        if (msg.Name == "subscribe_pairing")
        {
            await SendContinueAsync(ws, msg.RequestId, "Subscribed", pairing, cts.Token);
        }
        else
        {
            await SendCompleteAsync(ws, msg.RequestId, "Success", pairing, cts.Token);
        }

        continue;
    }

    if (msg.Verb == "CONTINUE" && msg.Name == "Registered")
    {
        Console.WriteLine("Registered with Core.");
        if (!subscribed)
        {
            var subId = NextId();
            pending[subId] = "zones";
            var sub = JsonSerializer.Serialize(new Dictionary<string, int> { ["subscription_key"] = 0 });
            await SendRequestAsync(ws, subId, "com.roonlabs.transport:2/subscribe_zones", sub, cts.Token);
            subscribed = true;
        }
    }

    if (msg.Verb == "COMPLETE" && msg.Name == "Unauthorized")
    {
        Console.WriteLine("WAITING: enable the extension in Roon, then this probe will keep trying.");
        subscribed = false;
        await Task.Delay(3000, cts.Token);
        var subId = NextId();
        pending[subId] = "zones";
        var sub = JsonSerializer.Serialize(new Dictionary<string, int> { ["subscription_key"] = 0 });
        await SendRequestAsync(ws, subId, "com.roonlabs.transport:2/subscribe_zones", sub, cts.Token);
        subscribed = true;
    }

    if ((msg.Verb == "CONTINUE" && (msg.Name == "Subscribed" || msg.Name == "Changed")) && msg.Body.Length > 0)
    {
        PrintZones(msg.Body, ZoneWanted);
    }
}

Console.WriteLine("Probe finished.");

static void PrintZones(string json, string zoneWanted)
{
    try
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        JsonElement zones;
        if (root.TryGetProperty("zones", out zones) || root.TryGetProperty("zones_changed", out zones))
        {
            foreach (var z in zones.EnumerateArray())
            {
                var name = z.TryGetProperty("display_name", out var n) ? n.GetString() : "?";
                var state = z.TryGetProperty("state", out var s) ? s.GetString() : "?";
                var id = z.TryGetProperty("zone_id", out var i) ? i.GetString() : "?";
                var mark = string.Equals(name, zoneWanted, StringComparison.OrdinalIgnoreCase) ? "  <== target" : "";
                Console.WriteLine($"  ZONE {name}  state={state}  id={id}{mark}");
                if (z.TryGetProperty("outputs", out var outputs))
                {
                    foreach (var o in outputs.EnumerateArray())
                    {
                        var on = o.TryGetProperty("display_name", out var od) ? od.GetString() : "?";
                        Console.WriteLine($"       output: {on}");
                    }
                }

                if (z.TryGetProperty("now_playing", out var np))
                {
                    var oneLine = np.TryGetProperty("one_line", out var ol) && ol.TryGetProperty("line1", out var l1)
                        ? l1.GetString()
                        : np.ToString();
                    Console.WriteLine($"       now playing: {oneLine}");
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("  zone parse: " + ex.Message);
    }
}

static async Task SendRequestAsync(ClientWebSocket ws, int id, string serviceMethod, string? json, CancellationToken ct)
{
    var sb = new StringBuilder();
    sb.Append("MOO/1 REQUEST ").Append(serviceMethod).Append('\n');
    sb.Append("Request-Id: ").Append(id).Append('\n');
    if (!string.IsNullOrEmpty(json))
    {
        var len = Encoding.UTF8.GetByteCount(json);
        sb.Append("Content-Type: application/json\n");
        sb.Append("Content-Length: ").Append(len).Append('\n');
        sb.Append('\n').Append(json);
    }
    else
    {
        sb.Append('\n');
    }

    Console.WriteLine($"-> REQUEST {serviceMethod} id={id}");
    await ws.SendAsync(Encoding.UTF8.GetBytes(sb.ToString()), WebSocketMessageType.Binary, true, ct);
}

static async Task SendCompleteAsync(ClientWebSocket ws, int id, string status, string? json, CancellationToken ct)
{
    var sb = new StringBuilder();
    sb.Append("MOO/1 COMPLETE ").Append(status).Append('\n');
    sb.Append("Request-Id: ").Append(id).Append('\n');
    if (!string.IsNullOrEmpty(json))
    {
        var len = Encoding.UTF8.GetByteCount(json);
        sb.Append("Content-Type: application/json\n");
        sb.Append("Content-Length: ").Append(len).Append('\n');
        sb.Append('\n').Append(json);
    }
    else
    {
        sb.Append('\n');
    }

    await ws.SendAsync(Encoding.UTF8.GetBytes(sb.ToString()), WebSocketMessageType.Binary, true, ct);
}

static async Task SendContinueAsync(ClientWebSocket ws, int id, string status, string? json, CancellationToken ct)
{
    var sb = new StringBuilder();
    sb.Append("MOO/1 CONTINUE ").Append(status).Append('\n');
    sb.Append("Request-Id: ").Append(id).Append('\n');
    if (!string.IsNullOrEmpty(json))
    {
        var len = Encoding.UTF8.GetByteCount(json);
        sb.Append("Content-Type: application/json\n");
        sb.Append("Content-Length: ").Append(len).Append('\n');
        sb.Append('\n').Append(json);
    }
    else
    {
        sb.Append('\n');
    }

    await ws.SendAsync(Encoding.UTF8.GetBytes(sb.ToString()), WebSocketMessageType.Binary, true, ct);
}

static async Task<string> ReadMessageAsync(ClientWebSocket ws, CancellationToken ct)
{
    var buffer = new byte[1024 * 256];
    using var ms = new MemoryStream();
    WebSocketReceiveResult result;
    do
    {
        result = await ws.ReceiveAsync(buffer, ct);
        if (result.MessageType == WebSocketMessageType.Close)
        {
            throw new InvalidOperationException("socket closed");
        }

        ms.Write(buffer, 0, result.Count);
    }
    while (!result.EndOfMessage);

    return Encoding.UTF8.GetString(ms.ToArray());
}

static MooMsg ParseMoo(string raw)
{
    var nl = raw.IndexOf('\n');
    var first = nl < 0 ? raw : raw[..nl];
    var parts = first.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
    var verb = parts.Length > 1 ? parts[1] : "";
    var name = parts.Length > 2 ? parts[2].Trim() : "";
    var service = "";
    var method = name;
    var slash = name.LastIndexOf('/');
    if (verb == "REQUEST" && slash > 0)
    {
        service = name[..slash];
        method = name[(slash + 1)..];
    }

    var requestId = -1;
    var headerEnd = raw.IndexOf("\n\n", StringComparison.Ordinal);
    var headers = headerEnd < 0 ? raw : raw[..headerEnd];
    foreach (var line in headers.Split('\n'))
    {
        if (line.StartsWith("Request-Id:", StringComparison.OrdinalIgnoreCase))
        {
            int.TryParse(line.Split(':')[1].Trim(), out requestId);
        }
    }

    var body = headerEnd < 0 ? "" : raw[(headerEnd + 2)..];
    return new MooMsg(verb, service, method, requestId, body);
}

static string Truncate(string s, int max)
{
    s = s.Replace("\r", " ").Replace("\n", " ");
    return s.Length <= max ? s : s[..max] + "...";
}

internal readonly record struct MooMsg(string Verb, string Service, string Name, int RequestId, string Body);
