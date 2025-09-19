using Mcp.Windbg.Server.Configuration;
using Mcp.Windbg.Server.Protocol;
using Mcp.Windbg.Server.Tools;

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var cfg = ConfigLoader.Load();
Console.Error.WriteLine($"[startup] mcp-windbg-dotnet skeleton version {cfg.Server.Version}");

// Register tools
var registry = new ToolRegistry()
    .Register(new HealthCheckTool());

var loop = new MessageLoop(registry, cts.Token);
await loop.RunAsync();

Console.Error.WriteLine("[shutdown] clean exit");
