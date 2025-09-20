# AI Coding Agent Instructions

These instructions make an AI agent productive quickly in this repo. Keep responses concise and follow existing patterns exactly.

## Big Picture
- Purpose: Skeleton Model Context Protocol (MCP) WinDBG server in .NET. Current scope: minimal JSON line protocol + single `health_check` tool. Future expansion (see `docs/prd/`) will add session lifecycle, command parity, managed enrichment, security/observability.
- Runtime entrypoint: `src/Mcp.Windbg.Server/Program.cs` wires config load, tool registration, then starts `MessageLoop`.
- Core abstraction: Tool model (`ITool`, `ToolRegistry`) + streaming message loop that dispatches `list_tools` and `call_tool` requests over stdin/stdout.

## Architecture & Flow
1. External orchestrator writes one JSON object per line to stdin (temporary protocol).
2. `MessageLoop` (`Protocol/MessageLoop.cs`) deserializes into `IncomingMessage` (`Protocol/Messages.cs`).
3. On `list_tools`: gather descriptors from `ToolRegistry` (name + description).
4. On `call_tool`: locate tool by name (case-insensitive) and invoke `ExecuteAsync(JsonNode? args, CancellationToken)`.
5. Response is `OutgoingMessage` with `ok/result/error` fields serialized compact (no indentation).
6. Config is fetched on demand via `ConfigLoader.Load()`; inexpensive and internally cached with time-based reload.

## Configuration
- Source precedence: defaults -> `appsettings.json` (optional) -> env vars (`MWD_SERVER_*`).
- Supported env keys: `MWD_SERVER_VERSION`, `MWD_SERVER_IMPLEMENTATION`, `MWD_SERVER_CONFIGRELOADSECONDS`.
- Avoid adding heavy configuration frameworks until later phases (intentional minimalism).

## Adding a Tool (Follow This Pattern)
- Implement `ITool` in `src/Mcp.Windbg.Server/Tools/`.
- Keep deterministic field names and lightweight JSON via `JsonObject` / `JsonArray`.
- Register in `Program.cs` with `.Register(new YourTool())` (chainable).
- Provide a succinct `Description`; it surfaces in `list_tools` results.
- Ensure idempotent, side-effect-light operation unless explicitly a mutating tool (future phases).

## Testing Pattern
- Unit test tool behavior directly (see `HealthCheckToolTests.cs`).
- For protocol-level behavior, simulate IO with `StringReader`/`StringWriter` (see `ListToolsIntegrationTests.cs`).
- Use `dotnet test` at repo root (solution includes tests project).

## Build & Run
```powershell
# Restore, build, test
dotnet restore
dotnet build
dotnet test

# Run server (stdin/stdout protocol)
dotnet run --project src/Mcp.Windbg.Server
```
Example interaction:
```json
{"method":"list_tools"}
{"method":"call_tool","name":"health_check"}
```

## Conventions
- Namespace roots: `Mcp.Windbg.Server.*`.
- JSON property names are camelCase and fixed by record definitions.
- `ToolRegistry` is the single source of truth for discoverable tools.
- Keep new files small & focused; avoid premature abstractions (skeleton phase principle).
- Failures: throw exceptions inside tool execution for user-visible `error` field population.

## Future Evolution Hooks
- Protocol replacement (spec-compliant MCP) will slot under `Protocol/` without changing tool surface.
- Session management & WinDBG / CLRMD integration will likely introduce repositories/services; prefer composition over static singletons.
- Security/observability layer will wrap tool dispatch (insertion point: inside `MessageLoop` after method switch).

## When Unsure
Inspect analogous existing code first (MessageLoop, HealthCheckTool). Mirror style & error handling. Ask for clarification only when a decision can't be inferred from PRD docs.

_Last synced: 2025-09-20_
