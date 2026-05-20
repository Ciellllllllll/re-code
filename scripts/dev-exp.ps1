param(
    [switch]$Launch,
    [string]$TestSolution = ""
)

$ErrorActionPreference = "Stop"

$root = "D:\Git\GhostText"
$project = "$root\src\GhostTextVsix\GhostTextVsix.csproj"
$vsix = "$root\src\GhostTextVsix\.vsix"
$pkgdef = "$root\src\GhostTextVsix\obj\Debug\net48\GhostTextVsix.pkgdef"

$vsixInstaller = "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\VSIXInstaller.exe"
$devenv = "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe"

$extensionId = "GhostTextVsix.0fd3a8ea-948f-4c4c-97b0-3bba43fc8630"

Write-Host "== GhostText VSIX Experimental deploy ==" -ForegroundColor Cyan

function Get-ExperimentalDevenvProcess {
    Get-CimInstance Win32_Process -Filter "Name = 'devenv.exe'" |
        Where-Object {
            $_.CommandLine -match '(?i)(/rootsuffix\s+Exp|/rootSuffix:Exp)'
        }
}

function Close-ExperimentalInstance {
    $processes = @(Get-ExperimentalDevenvProcess)
    if ($processes.Count -eq 0) {
        Write-Host "No running Experimental Instance found." -ForegroundColor DarkGray
        return
    }

    Write-Host "Closing running Experimental Instance..." -ForegroundColor Yellow
    foreach ($process in $processes) {
        Write-Host "Stopping devenv.exe PID=$($process.ProcessId) CommandLine=$($process.CommandLine)" -ForegroundColor DarkYellow
        Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
    }

    Start-Sleep -Seconds 2
}

function Invoke-ExperimentalDevenv {
    param(
        [string]$Action,
        [string[]]$Arguments
    )

    Write-Host $Action -ForegroundColor Yellow
    & $devenv @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Action failed. ExitCode=$LASTEXITCODE"
    }
}

function Reset-SkippedPackages {
    param(
        [string]$Phase
    )

    Write-Host "Resetting skipped packages $Phase..." -ForegroundColor Yellow
    Invoke-ExperimentalDevenv "Running devenv /ResetSkipPkgs $Phase" @("/rootsuffix", "Exp", "/ResetSkipPkgs")

    Write-Host "Updating Experimental Instance configuration $Phase..." -ForegroundColor Yellow
    Invoke-ExperimentalDevenv "Running devenv /updateconfiguration $Phase" @("/rootsuffix", "Exp", "/updateconfiguration")
}

Set-Location $root

Close-ExperimentalInstance

Write-Host "Cleaning old build outputs..." -ForegroundColor Yellow
Remove-Item "$root\src\GhostTextVsix\bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "$root\src\GhostTextVsix\obj" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $vsix -Force -ErrorAction SilentlyContinue

Write-Host "Building project..." -ForegroundColor Yellow
dotnet msbuild $project /restore /t:Rebuild /p:Configuration=Debug /v:minimal

Write-Host "Generating pkgdef..." -ForegroundColor Yellow
dotnet msbuild $project /t:GeneratePkgDef /p:Configuration=Debug /v:minimal

if (!(Test-Path $pkgdef)) {
    throw "pkgdef was not generated: $pkgdef"
}

Write-Host "Creating VSIX container..." -ForegroundColor Yellow
dotnet msbuild $project /t:CreateVsixContainer /p:Configuration=Debug /v:minimal

if (!(Test-Path $vsix)) {
    throw "VSIX was not generated: $vsix"
}

Write-Host "Generated pkgdef:" -ForegroundColor Green
Get-Item $pkgdef | Select-Object FullName, Length, LastWriteTime

Write-Host "Generated VSIX:" -ForegroundColor Green
Get-Item $vsix | Select-Object FullName, Length, LastWriteTime

Reset-SkippedPackages "before install"

Write-Host "Uninstalling old extension from Experimental Instance..." -ForegroundColor Yellow
& $vsixInstaller /quiet /rootSuffix:Exp /u:$extensionId

if ($LASTEXITCODE -ne 0) {
    Write-Host "Uninstall returned exit code $LASTEXITCODE. Continuing." -ForegroundColor DarkYellow
}

Write-Host "Installing new extension to Experimental Instance..." -ForegroundColor Yellow
& $vsixInstaller /quiet /rootSuffix:Exp $vsix

if ($LASTEXITCODE -ne 0) {
    throw "VSIX install failed. ExitCode=$LASTEXITCODE"
}

Reset-SkippedPackages "after install"

Write-Host "Done." -ForegroundColor Green

if ($Launch) {
    Write-Host "Launching Experimental Instance..." -ForegroundColor Cyan

    if ($TestSolution -ne "") {
        & $devenv /rootsuffix Exp $TestSolution
    }
    else {
        & $devenv /rootsuffix Exp
    }
}
