# Copilot Guidance

This repository automates Windows debugging via console debugger tooling. For authoritative command-line option reference for modern WinDbg, see:

WinDbg Command-Line Startup Options (Microsoft Docs):
https://learn.microsoft.com/en-us/windows-hardware/drivers/debuggercmds/windbg-command-line-preview

Key points for this project:
- Current automation uses the console debugger `cdb.exe` (stdin/stdout driven) for headless sessions.
- The linked documentation describes options for launching the modern WinDbg (WinDbgX.exe) UI. Many flags (e.g. `-z`, `-remote`, `-c`, symbol path options) conceptually map to equivalent console usage.
- Do NOT substitute `WinDbgX.exe` in place of `cdb.exe` for streaming command automation; it does not provide an interactive stdin/out loop.
- If future work integrates `dbgeng.dll` directly, reuse semantics from the documented switches when designing higher-level tool arguments.

When generating code or documentation that references debugger startup arguments, prefer aligning with the Microsoft naming and behavior in that page.

If adding a new tool that spawns a debugger process:
1. Prefer reusing the existing session abstraction unless a fundamentally different mode (kernel, non-invasive attach) is required.
2. Only introduce WinDbgX for manual inspection tools (e.g., an `open_in_windbgx` helper) – never for command-loop automation.
3. Surface any new flags in a typed arguments object instead of ad-hoc strings.

Revision history:
- 2025-09-22: Initial creation; added canonical doc link for command-line options.
