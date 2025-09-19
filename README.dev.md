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
Environment variable overrides (prefix `MWD_`) map onto `Server:*` keys. Example:
```powershell
$env:MWD_Server__Version="0.1.1"; dotnet run --project src/Mcp.Windbg.Server
```

### Next Steps (Per PRD)
- Add real MCP protocol bindings.
- Introduce session lifecycle (Week 2).
- Replace temporary JSON loop with spec-compliant transport.

### License
MIT
