# mcp-windbg-dotnet

Minimal Model Context Protocol (MCP) server for policy-aware WinDBG / CDB automation & structured dump analysis.

---
## 1. Quick Setup
Goal: Point `WINDBG_PATH` at a directory containing `cdb.exe` (from the Windows SDK Debugging Tools) and run the server.

1. Install "Debugging Tools for Windows" (part of the Windows 10/11 SDK). During SDK setup ensure the Debugging Tools feature is selected. (You do NOT need the Store WinDbg for this minimal setup.)
2. After install, `cdb.exe` is typically located here:
  `C:\Program Files (x86)\Windows Kits\10\Debuggers\x64`
3. Set environment variable (current session):
```powershell
$env:WINDBG_PATH = 'C:/Program Files (x86)/Windows Kits/10/Debuggers/x64'
```
4. Sanity check:
```powershell
& "$env:WINDBG_PATH/cdb.exe" -version
```
5. Run server & list tools:
```powershell
'{"method":"list_tools"}' | dotnet run --project src/Mcp.Windbg.Server/Mcp.Windbg.Server.csproj
```
Expected output starts with `{ "ok": true, "result": [ { "name": "health_check" ... } ] }`.

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
        // Directory containing console debugger binaries you copied/symlinked
        "WINDBG_PATH": "C:/Tools/WinDbg"
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
  env: { ...process.env, WINDBG_PATH: 'C:/Tools/WinDbg' }
});
proc.stdout.on('data', d => process.stdout.write('[SERVER] ' + d));
proc.stdin.write('{"method":"list_tools"}\n');
```

### Python (simple pipe client)
```python
import json, subprocess, threading, os
proc = subprocess.Popen([
  'dotnet','run','--project','src/Mcp.Windbg.Server/Mcp.Windbg.Server.csproj'
], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, env={**os.environ, 'WINDBG_PATH': 'C:/Tools/WinDbg'})
def reader():
    for line in proc.stdout: print('[SERVER]', line.rstrip())
threading.Thread(target=reader, daemon=True).start()
proc.stdin.write(json.dumps({"method":"list_tools"}) + "\n")
proc.stdin.flush()
```

### Manual sanity test (PowerShell)
Send a single `list_tools` request through stdin and capture the one-line JSON response:
```powershell
'{"method":"list_tools"}' | dotnet run --project src/Mcp.Windbg.Server/Mcp.Windbg.Server.csproj
```
Expected output starts with:
```
{"ok":true,"result":[{"name":"health_check"...
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

