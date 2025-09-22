<#
.SYNOPSIS
  Attempt to locate WinDbg / console debugger root using running processes and fallbacks.
.DESCRIPTION
  Strategy:
    1. If WINDBG_PATH env var points to a directory containing cdb*.exe, return it.
    2. Use pslist (Sysinternals) or Get-Process to detect any running WinDbg / cdb* processes and infer their location.
    3. Probe canonical SDK debugger paths.
    4. Search PATH for cdb.exe / cdbX64.exe.
    5. (Optional) Scan WindowsApps for the Store WinDbg (requires permissions) when -ScanWindowsApps is specified.
.PARAMETER ScanWindowsApps
  Include scanning WindowsApps store packages.
.EXAMPLE
  ./find-windbg.ps1
.EXAMPLE
  ./find-windbg.ps1 -ScanWindowsApps
#>
[CmdletBinding()]
param(
  [switch]$ScanWindowsApps
)

function Write-Info($msg){ Write-Host "[info] $msg" -ForegroundColor Cyan }
function Write-Warn($msg){ Write-Host "[warn] $msg" -ForegroundColor Yellow }
function Write-Err($msg){ Write-Host "[err]  $msg" -ForegroundColor Red }

$results = [System.Collections.Generic.List[object]]::new()

function Add-Result([string]$Source,[string]$Root,[string]$Exe){
  $results.Add([pscustomobject]@{Source=$Source;Root=$Root;Executable=$Exe})
}

# 1. Environment variable
$envRoot = $env:WINDBG_PATH
if ($envRoot -and (Test-Path (Join-Path $envRoot 'cdb.exe') -PathType Leaf -ErrorAction SilentlyContinue -or
    (Get-ChildItem -Path $envRoot -Filter 'cdb*.exe' -ErrorAction SilentlyContinue | Where-Object Length -gt 0))) {
  Add-Result 'ENV:WINDBG_PATH' (Resolve-Path $envRoot).Path (Get-ChildItem -Path $envRoot -Filter 'cdb*.exe' | Select-Object -First 1 -ExpandProperty FullName)
}

# Helper to dedupe root entries
function Add-CandidateDir($source,[string]$exePath){
  if (-not (Test-Path $exePath)) { return }
  $root = Split-Path $exePath -Parent
  if (-not ($results | Where-Object Root -eq $root)){
    Add-Result $source $root $exePath
  }
}

# 2. Process inspection using pslist (if available) else Get-Process
$exeNames = 'windbg.exe','WinDbg.exe','cdb.exe','cdbX64.exe','cdbARM64.exe'
$procFound = $false
try {
  $pslist = Get-Command pslist -ErrorAction SilentlyContinue
  if ($pslist){
    $lines = & pslist 2>$null
    foreach($name in $exeNames){
      $match = $lines | Where-Object { $_ -match "(?i)\b$name\b" }
      if($match){ $procFound = $true }
    }
    if($procFound){ Write-Info 'Detected candidate debugger process(es) via pslist.' }
  }
} catch {}

# Fallback Get-Process
try {
  foreach($name in $exeNames){
    Get-Process -Name ($name -replace '.exe$','') -ErrorAction SilentlyContinue | ForEach-Object {
      $path = $_.Path
      if ($path){ Add-CandidateDir "Process:$name" $path }
    }
  }
} catch {}

# 3. Canonical SDK paths
$canonical = @(
  Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/Debuggers/x64',
  Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/11/Debuggers/x64'
) | Where-Object { Test-Path $_ }
foreach($dir in $canonical){
  $exe = Get-ChildItem -Path $dir -Filter 'cdb.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
  if($exe){ Add-CandidateDir 'SDK' $exe.FullName }
}

# 4. PATH search
$pathExes = @('cdb.exe','cdbX64.exe') | ForEach-Object { (& where.exe $_ 2>$null) } | Select-Object -Unique
foreach($p in $pathExes){ Add-CandidateDir 'PATH' $p }

# 5. Optional WindowsApps scan
if ($ScanWindowsApps){
  $appsRoot = 'C:/Program Files/WindowsApps'
  if (Test-Path $appsRoot){
    Get-ChildItem $appsRoot -Directory -Filter 'Microsoft.WinDbg_*' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 3 | ForEach-Object {
      $cdb = Get-ChildItem $_.FullName -Filter 'cdb*.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
      if($cdb){ Add-CandidateDir 'WindowsApps' $cdb.FullName }
    }
  } else {
    Write-Warn 'WindowsApps root not accessible (permission or platform issue).'
  }
}

if (-not $results.Count){
  Write-Err 'No debugger roots found. Set WINDBG_PATH manually.'
  exit 1
}

# Present best candidate (priority order already inherent by insertion sequence)
$best = $results[0]
Write-Info ("Selected debugger root: {0}" -f $best.Root)
$results | Format-Table -AutoSize

# Emit machine-readable output for scripts
[pscustomobject]@{
  SelectedRoot = $best.Root
  Candidates   = $results
} | ConvertTo-Json -Depth 4
