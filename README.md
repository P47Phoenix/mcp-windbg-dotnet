# mcp-windbg-dotnet

Minimal Model Context Protocol (MCP) server for policy-aware WinDBG / CDB automation & structured dump analysis.

---
## 1. Install WinDBG / CDB
You must have the Windows Debugging Tools installed (x64) and accessible.

Option A – Windows SDK (classic WinDBG + CDB):
1. Download & run latest Windows 10/11 SDK installer.
2. In feature selection, ensure “Debugging Tools for Windows” is checked.
3. Default path (x64 tools):
   - `C:\Program Files (x86)\Windows Kits\10\Debuggers\x64` (adjust version if different)

Option B – WinDbg Preview (Microsoft Store):
1. Install “WinDbg Preview” from Microsoft Store.
2. Preview includes a modern UI; the classic CDB may still be preferred for automation.

Option C – winget (script-friendly):
```powershell
winget install --id Microsoft.WindowsSDK --source winget
```
After install, verify path contains `cdb.exe`.

Option D – Standalone Debugging Tools (Visual Studio installer):
1. Launch Visual Studio Installer > Modify.
2. Individual components: add “Just-In-Time Debugger / Windows 10 SDK Debugging Tools”.

Environment Setup (recommended):
```powershell
$env:WINDBG_PATH = "C:\Program Files (x86)\Windows Kits\10\Debuggers\x64"
# Persist (PowerShell profile):  Add-Content $PROFILE '$env:WINDBG_PATH="C:\Program Files (x86)\Windows Kits\10\Debuggers\x64"'
```
Quick sanity test:
```powershell
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

