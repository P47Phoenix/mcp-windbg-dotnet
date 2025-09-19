using System.Text.Json;
using System.Text.Json.Nodes;
using System.IO;
using System;

namespace Mcp.Windbg.Server.Configuration;

public static class ConfigLoader
{
    private static readonly object _lock = new();
    private static RootConfig? _cached;
    private static DateTime _lastLoad = DateTime.MinValue;

    public static RootConfig Load(int reloadSeconds = 30)
    {
        lock (_lock)
        {
            if (_cached != null && (DateTime.UtcNow - _lastLoad).TotalSeconds < reloadSeconds)
            {
                return _cached;
            }

            // Start with defaults
            var server = new ServerConfig();

            // Load JSON file if present (best-effort, swallow errors for skeleton stability)
            try
            {
                if (File.Exists("appsettings.json"))
                {
                    using var fs = File.OpenRead("appsettings.json");
                    var doc = JsonNode.Parse(fs) as JsonObject;
                    var serverObj = doc?["Server"] as JsonObject;
                    if (serverObj != null)
                    {
                        server = new ServerConfig
                        {
                            Version = serverObj["Version"]?.GetValue<string?>() ?? server.Version,
                            Implementation = serverObj["Implementation"]?.GetValue<string?>() ?? server.Implementation,
                            ConfigReloadSeconds = serverObj["ConfigReloadSeconds"]?.GetValue<int?>() ?? server.ConfigReloadSeconds
                        };
                    }
                }
            }
            catch { /* ignored */ }

            // Environment overrides (prefix MWD_SERVER_*)
            string? env(string key) => Environment.GetEnvironmentVariable($"MWD_SERVER_{key}");
            server = new ServerConfig
            {
                Version = env("VERSION") ?? server.Version,
                Implementation = env("IMPLEMENTATION") ?? server.Implementation,
                ConfigReloadSeconds = int.TryParse(env("CONFIGRELOADSECONDS"), out var cr) ? cr : server.ConfigReloadSeconds
            };

            var cfg = new RootConfig { Server = server };
            _cached = cfg;
            _lastLoad = DateTime.UtcNow;
            return cfg;
        }
    }
}
