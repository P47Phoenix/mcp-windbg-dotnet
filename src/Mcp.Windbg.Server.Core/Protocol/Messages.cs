using System.Text.Json.Serialization;

namespace Mcp.Windbg.Server.Protocol;

public sealed record IncomingMessage(
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("name")] string? Name = null,
    [property: JsonPropertyName("args")] Dictionary<string, object>? Args = null
);

public sealed record OutgoingMessage<T>(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("result")] T? Result,
    [property: JsonPropertyName("error")] string? Error = null
);

public sealed record ToolDescriptor(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description
);
