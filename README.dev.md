## Development Guide (Skeleton Phase)

This repository now contains the initial .NET skeleton for an MCP (Model Context Protocol) WinDBG server.

### Layout
```
src/               Main source projects
  Mcp.Windbg.Server/  Skeleton server (health_check only)
tests/             Test projects
docs/prd/          Product requirements & planning
```

### Building (example commands)
```powershell
dotnet restore
dotnet build
dotnet test
```

### Running The Skeleton Server
It uses a simple line-delimited JSON protocol (temporary) until full MCP .NET integration:

Input examples:
```json
{"method":"list_tools"}
{"method":"call_tool","name":"health_check"}
```

Output examples:
```json
{"ok":true,"result":[{"name":"health_check","description":"Return server health, version and uptime."}]}
{"ok":true,"result":{"status":"ok","serverVersion":"0.1.0","implementation":"skeleton","timestampUtc":"2025-09-19T00:00:00.0000000Z","uptimeSeconds":0.123}}
```

### Configuration
We intentionally removed the Microsoft.Extensions.Configuration stack to avoid runtime assembly load issues in this minimal phase. A lightweight loader now:
1. Reads `appsettings.json` if present (root only, optional).
2. Applies environment overrides with prefix `MWD_SERVER_` (upper-case, flat keys).

Supported keys (defaults in parentheses):
- `MWD_SERVER_VERSION` (0.1.0)
- `MWD_SERVER_IMPLEMENTATION` (skeleton)
- `MWD_SERVER_CONFIGRELOADSECONDS` (30)

Example:
```powershell
$env:MWD_SERVER_VERSION="0.1.1"
dotnet run --project src/Mcp.Windbg.Server
```

Hot reload interval is respected only when `ConfigLoader.Load()` is called again after the configured seconds.

### Next Steps (Per PRD)
- Add real MCP protocol bindings.
- Introduce session lifecycle (Week 2).
- Replace temporary JSON loop with spec-compliant transport.

### License
MIT
