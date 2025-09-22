# mcp-windbg-dotnet

## Product Documentation
See the PRD hub and templates in `docs/prd/`:
- PRD Hub: `docs/prd/README.md`
- Decision Log: `docs/prd/decision-log/`
- Backlog: `docs/prd/backlog/`
- Metrics: `docs/prd/metrics/`
- Release Plan: `docs/prd/release-plan/`
- Risk Register: `docs/prd/risk-register.md`

Templates available:
- Product Requirement: `docs/prd/_template-prd.md`
- Feature Spec: `docs/prd/_template-feature-spec.md`
- Acceptance Checklist: `docs/prd/_template-acceptance-checklist.md`

## Overview
`mcp-windbg-dotnet` provides a Model Context Protocol (MCP) server that exposes a curated, policy-aware subset of WinDBG / CDB functionality and higher-level analysis helpers (e.g. `analyze_dump`) to AI copilots and MCP-enabled tools.

## Features (Current)
- Session lifecycle: open dump (`open_dump`), open remote (`open_remote`), close (`close_dump`)
- Command execution with allow/deny policy (`run_command`)
- Discovery (`list_tools`), health (`health_check`), session info (`session_info`)
- Structured crash analysis (`analyze_dump`) returning JSON + Markdown
- Typed contracts (in progress migration) — example: `AnalyzeDumpTool`

## Build & Run (Local)
Prerequisites: .NET 8 SDK, Windows (required for CDB/WinDBG integration).

Build solution:
```powershell
dotnet build .\mcp-windbg-dotnet.sln
```

Run MCP server (foreground):
```powershell
dotnet run --project .\src\Mcp.Windbg.Server\Mcp.Windbg.Server.csproj
```

Optional environment (example):
```powershell
$env:WINDBG_PATH = "C:\\Program Files (x86)\\Windows Kits\\10\\Debuggers\\x64"
```

## Protocol Messages (Wire Examples)
List tools:
```json
{"method":"list_tools"}
```

Call a tool (generic shape):
```json
{"method":"call_tool","name":"health_check"}
```

Analyze dump (typed args example):
```json
{
	"method": "call_tool",
	"name": "analyze_dump",
	"args": {
		"sessionId": "sess-123",
		"includeModules": true,
		"includeThreads": true,
		"includeRegisters": false,
		"stackFrameCount": 12
	}
}
```

Response (abridged):
```json
{
	"ok": true,
	"result": {
		"success": true,
		"sessionId": "sess-123",
		"analysisTimeMs": 842,
		"timestamp": "2025-09-22T12:34:56.789Z",
		"analysis": { "sections": [ { "title": "Crash Analysis" } ] },
		"markdown": "# Crash Dump Analysis Report...",
		"options": {
			"includeModules": true,
			"includeThreads": true,
			"includeRegisters": false,
			"stackFrameCount": 12
		}
	}
}
```

## VS Code MCP Configuration Example
If you are using an MCP-capable VS Code extension (hypothetical setting name shown): add to your `settings.json`:
```jsonc
{
	"modelContextProtocol.servers": {
		"windbg": {
			"command": "dotnet",
			"args": [
				"run",
				"--project",
				"${workspaceFolder}/src/Mcp.Windbg.Server/Mcp.Windbg.Server.csproj"
			],
			"env": {
				"WINDBG_PATH": "C:/Program Files (x86)/Windows Kits/10/Debuggers/x64"
			},
			"restart": "onFailure",
			"version": 1
		}
	}
}
```

Once started, the client should issue `list_tools` and then allow invoking e.g. `open_dump`:
```json
{
	"method": "call_tool",
	"name": "open_dump",
	"args": { "path": "C:/dumps/sample.dmp" }
}
```

## Claude (JavaScript) MCP Client Sample
Below is a minimal Node.js script using a generic MCP client pattern (adjust to your actual Claude SDK / client library):
```javascript
import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';

function startWindbgServer() {
	const proc = spawn('dotnet', ['run', '--project', 'src/Mcp.Windbg.Server/Mcp.Windbg.Server.csproj'], {
		stdio: ['pipe', 'pipe', 'inherit'],
		env: { ...process.env, WINDBG_PATH: 'C:/Program Files (x86)/Windows Kits/10/Debuggers/x64' }
	});
	return proc;
}

async function main() {
	const srv = startWindbgServer();
	const rl = createInterface({ input: srv.stdout });
	rl.on('line', line => console.log('[SERVER]', line));

	function send(obj) {
		srv.stdin.write(JSON.stringify(obj) + '\n');
	}

	// 1. List tools
	send({ method: 'list_tools' });

	// 2. Open a dump (after you confirm path exists)
	// send({ method: 'call_tool', name: 'open_dump', args: { path: 'C:/dumps/sample.dmp' } });

	// 3. Later analyze
	// send({ method: 'call_tool', name: 'analyze_dump', args: { sessionId: 'sess-id', stackFrameCount: 15 } });
}

main().catch(err => console.error(err));
```

## Claude (Python) MCP Client Sample
```python
import json, subprocess, threading

proc = subprocess.Popen([
		'dotnet', 'run', '--project', 'src/Mcp.Windbg.Server/Mcp.Windbg.Server.csproj'
], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)

def reader():
		for line in proc.stdout:
				print('[SERVER]', line.rstrip())

threading.Thread(target=reader, daemon=True).start()

def send(msg):
		proc.stdin.write(json.dumps(msg) + '\n')
		proc.stdin.flush()

send({ 'method': 'list_tools' })
# send({ 'method': 'call_tool', 'name': 'health_check' })
# send({ 'method': 'call_tool', 'name': 'open_dump', 'args': { 'path': 'C:/dumps/sample.dmp' } })

```

## Tool Summary (Current Names)
| Tool | Purpose |
|------|---------|
| health_check | Basic liveness & uptime info |
| open_dump | Create a session from a crash dump path |
| open_remote | Attach remote (CDB `-remote`) |
| close_dump | Close an existing session |
| run_command | Execute a WinDBG command within a session (policy guarded) |
| list_dumps | List candidate dump files from configured search paths |
| session_info | Return metadata about a session |
| analyze_dump | Perform structured multi-section analysis |

## Typed Contract Example: analyze_dump (Args / Result)
Args schema (conceptual):
```json
{
	"sessionId": "string",
	"includeModules": "boolean?",
	"includeThreads": "boolean?",
	"includeRegisters": "boolean?",
	"stackFrameCount": "int?"
}
```
Result (top-level fields):
```json
{
	"success": true,
	"sessionId": "string",
	"analysisTimeMs": 123,
	"timestamp": "ISO-8601",
	"analysis": { "sections": [ { "title": "...", "content": "..." } ], "sessionInfo": { ... } },
	"markdown": "# Crash Dump Analysis Report...",
	"options": { "includeModules": true, "includeThreads": true, "includeRegisters": false, "stackFrameCount": 10 }
}
```

## Roadmap (High-Level)
- Migrate all remaining tools to typed contracts
- Managed runtime enrichment (CLRMD) tool (`analyze_managed_context`)
- Metrics & observability surfaces
- Rate limiting & advanced policy rules

## Contributing
PRs/issues welcome. Please align changes with existing PRD documents and create ADRs for notable architectural decisions.

---
Generated sections may evolve; treat MCP client config examples as guidance and adapt to your specific MCP-enabled tooling.

