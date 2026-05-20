param(
    [switch]$Launch
)

$ErrorActionPreference = "Stop"

$devenv = "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe"

Write-Host "== Reset Visual Studio Experimental Instance ==" -ForegroundColor Cyan
Write-Host "This clears the Experimental Instance user data. Use only as a last resort." -ForegroundColor Yellow

Write-Host "Closing running Experimental Instance..." -ForegroundColor Yellow
$processes = @(Get-CimInstance Win32_Process -Filter "Name = 'devenv.exe'" |
    Where-Object {
        $_.CommandLine -match '(?i)(/rootsuffix\s+Exp|/rootSuffix:Exp)'
    })

foreach ($process in $processes) {
    Write-Host "Stopping devenv.exe PID=$($process.ProcessId) CommandLine=$($process.CommandLine)" -ForegroundColor DarkYellow
    Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
}

Write-Host "Resetting Experimental Instance user data..." -ForegroundColor Yellow
& $devenv /rootsuffix Exp /resetuserdata
if ($LASTEXITCODE -ne 0) {
    throw "Experimental Instance reset failed. ExitCode=$LASTEXITCODE"
}

Write-Host "Updating Experimental Instance configuration..." -ForegroundColor Yellow
& $devenv /rootsuffix Exp /updateconfiguration
if ($LASTEXITCODE -ne 0) {
    throw "Experimental Instance configuration update failed. ExitCode=$LASTEXITCODE"
}

Write-Host "Done." -ForegroundColor Green

if ($Launch) {
    Write-Host "Launching Experimental Instance..." -ForegroundColor Cyan
    & $devenv /rootsuffix Exp
}
