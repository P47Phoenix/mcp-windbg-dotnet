# mcp-windbg-dotnet

Minimal Model Context Protocol (MCP) server for policy-aware WinDBG / CDB automation & structured dump analysis.

---
## 1. Install WinDbg
You need the Windows debugging tools (x64). For this project we assume you use the current Microsoft Store distribution of WinDbg. The former "Preview" branding has been removed upstream.

Install (Microsoft Store):
1. Open Microsoft Store, search for "WinDbg".
2. Install "WinDbg".
3. Launch once so it completes initial provisioning (symbols/settings).

Notes:
- Modern WinDbg provides the updated UI and underlying debugging engine.
- If you require a stable, non-packaged path with an unsuffixed `cdb.exe` for scripting, you can additionally install the Windows SDK (intentionally omitted here per project guidance to keep this section minimal).
- Store installation resides under a protected `WindowsApps` folder; for automation you may copy or symlink the needed console debugger binaries into a tools directory you control and point `WINDBG_PATH` there.

Prepare environment variable (after you have a tools directory with `cdb.exe` or variants):
```powershell
$env:WINDBG_PATH = "C:\Tools\WinDbg"
```
Persist (optional):
```powershell
Add-Content $PROFILE '$env:WINDBG_PATH="C:\Tools\WinDbg"'
```

Quick sanity test (if `cdb.exe` present):
```powershell
Get-ChildItem $env:WINDBG_PATH
& "$env:WINDBG_PATH\cdb.exe" -version
```

---
## 2. Example MCP Configs

### VS Code (settings.json)
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

### Node.js (simple pipe client)
```javascript
import { spawn } from 'node:child_process';
const proc = spawn('dotnet', ['run', '--project', 'src/Mcp.Windbg.Server/Mcp.Windbg.Server.csproj'], {
  stdio: ['pipe', 'pipe', 'inherit'],
  env: { ...process.env, WINDBG_PATH: 'C:/Program Files (x86)/Windows Kits/10/Debuggers/x64' }
});
proc.stdout.on('data', d => process.stdout.write('[SERVER] ' + d));
proc.stdin.write('{"method":"list_tools"}\n');
```

### Python (simple pipe client)
```python
import json, subprocess, threading
proc = subprocess.Popen([
    'dotnet','run','--project','src/Mcp.Windbg.Server/Mcp.Windbg.Server.csproj'
], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
def reader():
    for line in proc.stdout: print('[SERVER]', line.rstrip())
threading.Thread(target=reader, daemon=True).start()
proc.stdin.write(json.dumps({"method":"list_tools"}) + "\n")
proc.stdin.flush()
```

### Wire Message Examples
List tools:
```json
{"method":"list_tools"}
```
Run command (example):
```json
{"method":"call_tool","name":"run_command","args":{"sessionId":"sess1","command":"!analyze -v"}}
```

---
## 3. Tool Catalog
| Tool | Description |
|------|-------------|
| health_check | Server liveness, uptime metadata |
| open_dump | Open a crash dump and create a session |
| open_remote | Start remote debug session (`-remote`) |
| close_dump | Close an existing session |
| run_command | Execute a debugger command within a session (policy enforced) |
| list_dumps | Enumerate dump files from configured search paths |
| session_info | Return metadata about a session |
| analyze_dump | Structured multi-section crash analysis |

Notes:
- Arguments & results are being migrated to typed contracts (see `AnalyzeDumpTool`).
- Additional managed (.NET) enrichment (`analyze_managed_context`) planned.

---
For extended product docs, PRDs, ADRs, and roadmap: see `docs/prd/` directory.

