using System;

namespace Mcp.Windbg.Server.Configuration;

public sealed class ServerConfig
{
    public string Version { get; init; } = "0.1.0";
    public string Implementation { get; init; } = "skeleton";
    public int ConfigReloadSeconds { get; init; } = 30;
}

public sealed class RootConfig
{
    public ServerConfig Server { get; init; } = new();
}
