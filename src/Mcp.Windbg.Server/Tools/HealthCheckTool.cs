using System.Text.Json.Nodes;
using Mcp.Windbg.Server.Configuration;

namespace Mcp.Windbg.Server.Tools;

public sealed class HealthCheckTool : ITool
{
    private readonly DateTime _started = DateTime.UtcNow;
    public string Name => "health_check";
    public string Description => "Return server health, version and uptime.";

    public Task<JsonNode> ExecuteAsync(JsonNode? args, CancellationToken ct)
    {
        var cfg = ConfigLoader.Load();
        var now = DateTime.UtcNow;
        var root = new JsonObject
        {
            ["status"] = "ok",
            ["serverVersion"] = cfg.Server.Version,
            ["implementation"] = cfg.Server.Implementation,
            ["timestampUtc"] = now.ToString("O"),
            ["uptimeSeconds"] = (now - _started).TotalSeconds,
        };
        return Task.FromResult<JsonNode>(root);
    }
}
