using System.Text.Json.Nodes;

namespace Mcp.Windbg.Server.Tools;

public interface ITool
{
    string Name { get; }
    string Description { get; }
    Task<JsonNode> ExecuteAsync(JsonNode? args, CancellationToken ct);
}
