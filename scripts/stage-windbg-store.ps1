<#
.SYNOPSIS
  Stage console debugger binaries from the Microsoft Store WinDbg package into an accessible tools folder.
.DESCRIPTION
  The Microsoft Store installation lives under WindowsApps with restricted ACLs.
  This script copies (or optionally symlinks) the architecture-specific debugger
  binaries (cdbX64.exe, kdX64.exe, ntsdX64.exe, dbgeng.dll, dbghelp.dll, symsrv.dll, etc.)
  into a destination folder you can reference via WINDBG_PATH.

  By default it renames cdbX64.exe -> cdb.exe to provide a stable name for automation.
.PARAMETER StorePath
  Full path to the architecture subfolder (e.g.
  C:\Program Files\WindowsApps\Microsoft.WinDbg_1.2409.17001.0_x64__8wekyb3d8bbwe\amd64 )
  If omitted, the script will attempt to auto-detect the newest WinDbg package under WindowsApps.
.PARAMETER Destination
  Target folder to place staged binaries. Defaults to C:\Tools\WinDbg
.PARAMETER Symlink
  Use symbolic links instead of copying (requires Developer Mode or elevation + SeCreateSymbolicLink privilege).
.PARAMETER NoRenameCdb
  Do NOT rename cdbX64.exe -> cdb.exe.
.PARAMETER Force
  Overwrite existing files in the destination.
.EXAMPLE
  ./stage-windbg-store.ps1 -StorePath 'C:\Program Files\WindowsApps\Microsoft.WinDbg_1.2409.17001.0_x64__8wekyb3d8bbwe\amd64'
.EXAMPLE
  ./stage-windbg-store.ps1 -Symlink -Force
#>
[CmdletBinding()]
param(
  [string]$StorePath,
  [string]$Destination = 'C:/Tools/WinDbg',
  [switch]$Symlink,
  [switch]$NoRenameCdb,
  [switch]$Force
)

function Write-Info($m){ Write-Host "[info] $m" -ForegroundColor Cyan }
function Write-Warn($m){ Write-Host "[warn] $m" -ForegroundColor Yellow }
function Write-Err($m){ Write-Host "[err]  $m" -ForegroundColor Red }

if (-not $StorePath) {
  $root = 'C:/Program Files/WindowsApps'
  if (-not (Test-Path $root)) { Write-Err "WindowsApps root not found: $root"; exit 1 }
  $pkg = Get-ChildItem $root -Directory -Filter 'Microsoft.WinDbg_*' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
  if (-not $pkg) { Write-Err 'Could not auto-detect WinDbg package.'; exit 1 }
  $archDirs = 'amd64','x64'
  foreach($a in $archDirs){
    $candidate = Join-Path $pkg.FullName $a
    if (Test-Path $candidate) { $StorePath = $candidate; break }
  }
  if (-not $StorePath) { Write-Err 'No architecture subfolder (amd64/x64) found.'; exit 1 }
  Write-Info "Auto-detected store architecture folder: $StorePath"
}

if (-not (Test-Path $StorePath)) { Write-Err "StorePath not found: $StorePath"; exit 1 }

$binNames = @(
  'cdbX64.exe','cdb.exe','kdX64.exe','ntsdX64.exe','dbgeng.dll','dbghelp.dll','symsrv.dll','srcsrv.dll','dbgmodel.dll'
)

New-Item -ItemType Directory -Path $Destination -Force | Out-Null

$copied = @()
foreach($name in $binNames){
  $src = Join-Path $StorePath $name
  if (-not (Test-Path $src)) { continue }
  $targetName = $name
  if (-not $NoRenameCdb -and $name -ieq 'cdbX64.exe') { $targetName = 'cdb.exe' }
  $dst = Join-Path $Destination $targetName
  if (Test-Path $dst -and -not $Force) {
    Write-Warn "Skipping existing: $targetName (use -Force to overwrite)"
    continue
  }
  if ($Symlink) {
    if (Test-Path $dst) { Remove-Item $dst -Force }
    New-Item -ItemType SymbolicLink -Path $dst -Target $src -ErrorAction Stop | Out-Null
  } else {
    Copy-Item $src $dst -Force:$Force -ErrorAction Stop
  }
  $copied += $targetName
  Write-Info "Staged $targetName"
}

if (-not $copied) {
  Write-Warn 'No binaries were staged. Check permissions or package contents.'
} else {
  Write-Info ("Staged/linked binaries: {0}" -f ($copied -join ', '))
  Write-Host "`nSet environment for this session:" -ForegroundColor Green
  Write-Host "$env:WINDBG_PATH = '$Destination'" -ForegroundColor Green
}

# Emit JSON summary
[pscustomobject]@{ Destination=$Destination; Binaries=$copied } | ConvertTo-Json -Depth 3
