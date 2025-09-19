using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mcp.Windbg.Server.Tools;

namespace Mcp.Windbg.Server.Protocol;

public sealed class MessageLoop
{
    private readonly ToolRegistry _registry;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly CancellationToken _ct;

    public MessageLoop(ToolRegistry registry, CancellationToken ct)
    {
        _registry = registry;
        _ct = ct;
        _jsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = false
        };
    }

    public async Task RunAsync()
    {
        using var stdin = Console.OpenStandardInput();
        using var stdout = Console.OpenStandardOutput();
        using var reader = new StreamReader(stdin, Encoding.UTF8);
        using var writer = new StreamWriter(stdout, Encoding.UTF8) { AutoFlush = true };

        while (!_ct.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync();
            }
            catch
            {
                break;
            }
            if (line == null) break; // EOF
            if (string.IsNullOrWhiteSpace(line)) continue;

            OutgoingMessage<object?> response;
            try
            {
                var incoming = JsonSerializer.Deserialize<IncomingMessage>(line, _jsonOptions)
                              ?? throw new InvalidOperationException("Invalid message");
                switch (incoming.Method)
                {
                    case "list_tools":
                        var tools = _registry.List().Select(t => new ToolDescriptor(t.Name, t.Description));
                        response = new(true, tools);
                        break;
                    case "call_tool":
                        if (string.IsNullOrWhiteSpace(incoming.Name))
                            throw new ArgumentException("Tool name missing");
                        if (!_registry.TryGet(incoming.Name, out var tool))
                            throw new ArgumentException($"Unknown tool: {incoming.Name}");
                        JsonNode? args = null;
                        if (incoming.Args != null)
                        {
                            args = JsonNode.Parse(JsonSerializer.Serialize(incoming.Args, _jsonOptions));
                        }
                        var result = await tool.ExecuteAsync(args, _ct);
                        response = new(true, result);
                        break;
                    default:
                        throw new ArgumentException($"Unknown method: {incoming.Method}");
                }
            }
            catch (Exception ex)
            {
                response = new(false, null, ex.Message);
            }

            var json = JsonSerializer.Serialize(response, _jsonOptions);
            await writer.WriteLineAsync(json);
        }
    }
}
