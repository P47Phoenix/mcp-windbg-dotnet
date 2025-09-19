using Microsoft.Extensions.Configuration;

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

            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables(prefix: "MWD_");

            var configRoot = builder.Build();
            var cfg = new RootConfig();
            configRoot.GetSection("Server").Bind(cfg.Server);
            _cached = cfg;
            _lastLoad = DateTime.UtcNow;
            return cfg;
        }
    }
}
