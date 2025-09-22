using System;

namespace Mcp.Windbg.Server.Configuration;

public sealed class ServerConfig
{
    public string Version { get; init; } = "0.1.0";
    public string Implementation { get; init; } = "skeleton";
    public int ConfigReloadSeconds { get; init; } = 30;
}

public sealed class SessionConfig
{
    public int MaxConcurrentSessions { get; init; } = 5;
    public int IdleTimeoutMinutes { get; init; } = 10;
    public string[] DumpSearchPaths { get; init; } = Array.Empty<string>();
}

public sealed class PolicyConfig  
{
    public bool Enabled { get; init; } = true;
    public bool AllowByDefault { get; init; } = false;
    public string[] AllowedCommands { get; init; } = Array.Empty<string>();
    public string[] AllowedPatterns { get; init; } = Array.Empty<string>();
    public string[] DeniedPatterns { get; init; } = Array.Empty<string>();
}

public sealed class FeatureFlags
{
    public bool SessionManagement { get; init; } = true;
    public bool PolicyEnforcement { get; init; } = true;
    public bool ManagedAnalysis { get; init; } = false;
    public bool MetricsCollection { get; init; } = true;
}

public sealed class RootConfig
{
    public ServerConfig Server { get; init; } = new();
    public SessionConfig Sessions { get; init; } = new();
    public PolicyConfig Policy { get; init; } = new();
    public FeatureFlags Features { get; init; } = new();
}
