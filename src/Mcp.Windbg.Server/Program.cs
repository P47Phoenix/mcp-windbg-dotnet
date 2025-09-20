using Mcp.Windbg.Server.Configuration;
using Mcp.Windbg.Server.Protocol;
using Mcp.Windbg.Server.Tools;
using Mcp.Windbg.Server.Tools.Session;
using Mcp.Windbg.Server.Tools.Analysis;
using Mcp.Windbg.Server.Session;
using Mcp.Windbg.Server.Policy;

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var cfg = ConfigLoader.Load();
Console.Error.WriteLine($"[startup] mcp-windbg-dotnet version {cfg.Server.Version}");

// Initialize services
var sessionRepository = new SessionRepository(
    cfg.Sessions.MaxConcurrentSessions,
    cfg.Sessions.IdleTimeoutMinutes);

var commandPolicy = cfg.Features.PolicyEnforcement && cfg.Policy.Enabled
    ? new DefaultCommandPolicy(new CommandPolicyConfig
    {
        AllowByDefault = cfg.Policy.AllowByDefault,
        AllowedCommands = cfg.Policy.AllowedCommands,
        AllowedPatterns = cfg.Policy.AllowedPatterns,
        DeniedPatterns = cfg.Policy.DeniedPatterns
    })
    : DefaultCommandPolicy.CreateDefault();

// Register tools
var registry = new ToolRegistry()
    .Register(new HealthCheckTool());

// Register session management tools if feature is enabled
if (cfg.Features.SessionManagement)
{
    registry
        .Register(new OpenDumpTool(sessionRepository))
        .Register(new CloseDumpTool(sessionRepository))
        .Register(new RunCommandTool(sessionRepository, commandPolicy))
        .Register(new ListDumpsTool(cfg.Sessions.DumpSearchPaths.Length > 0 
            ? cfg.Sessions.DumpSearchPaths 
            : Array.Empty<string>()))
        .Register(new SessionInfoTool(sessionRepository))
        .Register(new AnalyzeDumpTool(sessionRepository));
}

Console.Error.WriteLine($"[startup] Registered {registry.List().Count()} tools");
if (cfg.Features.SessionManagement)
{
    Console.Error.WriteLine($"[startup] Session management: max {cfg.Sessions.MaxConcurrentSessions} sessions, {cfg.Sessions.IdleTimeoutMinutes}min timeout");
    Console.Error.WriteLine($"[startup] Policy enforcement: {(cfg.Features.PolicyEnforcement && cfg.Policy.Enabled ? "enabled" : "disabled")}");
}

try
{
    var loop = new MessageLoop(registry, cts.Token);
    await loop.RunAsync();
}
finally
{
    // Cleanup
    sessionRepository.Dispose();
}

Console.Error.WriteLine("[shutdown] clean exit");
