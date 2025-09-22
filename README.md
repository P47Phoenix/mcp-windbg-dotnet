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
 - This server actually invokes the console debugger (`cdb.exe` / `cdbX64.exe`); the WinDbg GUI itself is **not** required for headless automation.
 - To populate `C:\Tools\WinDbg`, copy `cdb.exe` (and optionally supporting DLLs like `dbgeng.dll`, `dbghelp.dll` if needed) from an SDK debugger directory or from the Store package folder you have access to.

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
### Manual debugger discovery (no helper scripts)
Use these ad‑hoc PowerShell commands to locate a usable console debugger root:

Check environment first:
```powershell
if (Test-Path "$env:WINDBG_PATH/cdb.exe" -or (Get-ChildItem $env:WINDBG_PATH -Filter 'cdb*.exe' -ErrorAction SilentlyContinue)) {
  "WINDBG_PATH ok: $env:WINDBG_PATH"
} else { "WINDBG_PATH not set or no cdb*.exe present" }
```

Canonical SDK path (if you installed the Windows SDK):
```powershell
$classic = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/Debuggers/x64/cdb.exe'
Test-Path $classic
```

PATH search:
```powershell
where.exe cdb.exe 2>$null
where.exe cdbX64.exe 2>$null
```

Lightweight filesystem search (first few matches):
```powershell
Get-ChildItem 'C:/Program Files','C:/Program Files (x86)' -Filter 'cdb*.exe' -Recurse -ErrorAction SilentlyContinue | Select-Object -First 5 FullName
```

List newest Microsoft Store WinDbg package & show its cdb variants (may require permission adjustments):
```powershell
$pkg = Get-ChildItem 'C:/Program Files/WindowsApps' -Directory -Filter 'Microsoft.WinDbg_*' 2>$null | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($pkg) { Get-ChildItem $pkg.FullName -Filter 'cdb*.exe' -Recurse | Select-Object FullName } else { 'No Store package found or access denied.' }
```

### Manually staging Store binaries (copy only)
Example using the path you identified (`amd64` folder). Adjust version/build if different.
```powershell
$store = 'C:/Program Files/WindowsApps/Microsoft.WinDbg_1.2409.17001.0_x64__8wekyb3d8bbwe/amd64'
New-Item -ItemType Directory -Path 'C:/Tools/WinDbg' -Force | Out-Null
Copy-Item "$store/cdbX64.exe" 'C:/Tools/WinDbg/cdb.exe' -Force
Copy-Item "$store/dbgeng.dll","$store/dbghelp.dll","$store/symsrv.dll","$store/srcsrv.dll" 'C:/Tools/WinDbg' -Force -ErrorAction SilentlyContinue
$env:WINDBG_PATH = 'C:/Tools/WinDbg'
"Staged debugger root: $env:WINDBG_PATH"
```
If access to `WindowsApps` is denied, adjust folder permissions manually (right‑click -> Security) or run in an elevated terminal. Avoid recursive wide-open ACL changes—copy only what you need.

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

