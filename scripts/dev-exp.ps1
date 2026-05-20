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

Set-Location $root

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

Write-Host "Updating Experimental Instance configuration..." -ForegroundColor Yellow
& $devenv /rootsuffix Exp /updateconfiguration

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