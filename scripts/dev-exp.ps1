param(
    [switch]$Launch,
    [string]$TestSolution = "D:\Git\TestProject\TestProject\TestProject.sln",
    [switch]$SkipResetSkipPkgs
)

$ErrorActionPreference = "Stop"

$root = "D:\Git\GhostText"
$project = "$root\src\GhostTextVsix\GhostTextVsix.csproj"
$vsix = "$root\src\GhostTextVsix\.vsix"
$pkgdef = "$root\src\GhostTextVsix\obj\Debug\net48\GhostTextVsix.pkgdef"

$vsixInstaller = "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\VSIXInstaller.exe"
$devenvExe = "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe"
$devenvCom = "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.com"

$extensionId = "GhostTextVsix.0fd3a8ea-948f-4c4c-97b0-3bba43fc8630"

Write-Host "== re:code VSIX Experimental deploy ==" -ForegroundColor Cyan

function Assert-FileExists {
    param(
        [string]$Path,
        [string]$Name
    )

    if (!(Test-Path $Path)) {
        throw "$Name was not found: $Path"
    }
}

function Get-ExperimentalDevenvProcess {
    Get-CimInstance Win32_Process -Filter "Name = 'devenv.exe'" |
        Where-Object {
            $_.CommandLine -match '(?i)(/rootsuffix\s+Exp|/rootsuffix:Exp|/rootSuffix\s+Exp|/rootSuffix:Exp)'
        }
}

function Close-ExperimentalInstance {
    param(
        [string]$Reason = ""
    )

    $processes = @(Get-ExperimentalDevenvProcess)

    if ($processes.Count -eq 0) {
        Write-Host "No running Experimental Instance found." -ForegroundColor DarkGray
        return
    }

    if ($Reason -ne "") {
        Write-Host "Closing running Experimental Instance. Reason=$Reason" -ForegroundColor Yellow
    }
    else {
        Write-Host "Closing running Experimental Instance..." -ForegroundColor Yellow
    }

    foreach ($process in $processes) {
        Write-Host "Stopping Experimental devenv.exe PID=$($process.ProcessId)" -ForegroundColor DarkYellow
        Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
    }

    Start-Sleep -Seconds 2
}

function Invoke-DotNetMsBuild {
    param(
        [string]$Action,
        [string[]]$Arguments
    )

    Write-Host $Action -ForegroundColor Yellow

    dotnet msbuild @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "$Action failed. ExitCode=$LASTEXITCODE"
    }
}

function Invoke-DevenvMaintenance {
    param(
        [string]$Action,
        [string[]]$Arguments,
        [switch]$AllowFailure
    )

    Write-Host $Action -ForegroundColor Yellow

    $runner = $devenvExe
    if (Test-Path $devenvCom) {
        $runner = $devenvCom
    }

    & $runner @Arguments

    $exitCode = $LASTEXITCODE

    if ($exitCode -ne 0) {
        if ($AllowFailure) {
            Write-Host "$Action returned exit code $exitCode. Continuing." -ForegroundColor DarkYellow
        }
        else {
            throw "$Action failed. ExitCode=$exitCode"
        }
    }

    # devenv maintenance commands can leave an empty Experimental Instance window.
    # Close it before the final explicit launch.
    Close-ExperimentalInstance "after maintenance command"
}

function Reset-SkippedPackages {
    if ($SkipResetSkipPkgs) {
        Write-Host "Skipping ResetSkipPkgs because -SkipResetSkipPkgs was specified." -ForegroundColor DarkYellow
        return
    }

    Invoke-DevenvMaintenance `
        -Action "Resetting skipped packages in Experimental Instance..." `
        -Arguments @("/rootsuffix", "Exp", "/ResetSkipPkgs") `
        -AllowFailure

    Invoke-DevenvMaintenance `
        -Action "Updating Experimental Instance configuration..." `
        -Arguments @("/rootsuffix", "Exp", "/updateconfiguration") `
        -AllowFailure
}

function Install-VsixToExperimentalInstance {
    Write-Host "Uninstalling old extension from Experimental Instance..." -ForegroundColor Yellow

    & $vsixInstaller /quiet /rootSuffix:Exp /u:$extensionId

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Uninstall returned exit code $LASTEXITCODE. Continuing because the extension may not be installed." -ForegroundColor DarkYellow
    }

    # VSIXInstaller can also trigger configuration changes. Make sure no empty Exp instance remains.
    Close-ExperimentalInstance "after uninstall"

    Write-Host "Installing new extension to Experimental Instance..." -ForegroundColor Yellow

    & $vsixInstaller /quiet /rootSuffix:Exp $vsix

    if ($LASTEXITCODE -ne 0) {
        throw "VSIX install failed. ExitCode=$LASTEXITCODE"
    }

    Close-ExperimentalInstance "after install"
}

function Launch-ExperimentalInstance {
    Write-Host "Launching Experimental Instance..." -ForegroundColor Cyan

    $arguments = @("/rootsuffix", "Exp")

    if ($TestSolution -ne "") {
        if (!(Test-Path $TestSolution)) {
            throw "Test solution was not found: $TestSolution"
        }

        $arguments += "`"$TestSolution`""
    }

    $argumentText = $arguments -join " "

    Start-Process `
        -FilePath $devenvExe `
        -ArgumentList $argumentText `
        -WorkingDirectory $root
}

Assert-FileExists $vsixInstaller "VSIXInstaller.exe"
Assert-FileExists $devenvExe "devenv.exe"

Set-Location $root

Close-ExperimentalInstance "before deploy"

Write-Host "Cleaning old build outputs..." -ForegroundColor Yellow

Remove-Item "$root\src\GhostTextVsix\bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "$root\src\GhostTextVsix\obj" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $vsix -Force -ErrorAction SilentlyContinue

Invoke-DotNetMsBuild `
    -Action "Building project..." `
    -Arguments @(
        $project,
        "/restore",
        "/t:Rebuild",
        "/p:Configuration=Debug",
        "/v:minimal"
    )

Invoke-DotNetMsBuild `
    -Action "Generating pkgdef..." `
    -Arguments @(
        $project,
        "/t:GeneratePkgDef",
        "/p:Configuration=Debug",
        "/v:minimal"
    )

if (!(Test-Path $pkgdef)) {
    throw "pkgdef was not generated: $pkgdef"
}

Invoke-DotNetMsBuild `
    -Action "Creating VSIX container..." `
    -Arguments @(
        $project,
        "/t:CreateVsixContainer",
        "/p:Configuration=Debug",
        "/v:minimal"
    )

if (!(Test-Path $vsix)) {
    throw "VSIX was not generated: $vsix"
}

Write-Host "Generated pkgdef:" -ForegroundColor Green
Get-Item $pkgdef | Select-Object FullName, Length, LastWriteTime

Write-Host "Generated VSIX:" -ForegroundColor Green
Get-Item $vsix | Select-Object FullName, Length, LastWriteTime

Install-VsixToExperimentalInstance

Reset-SkippedPackages

# Final cleanup before explicit launch.
Close-ExperimentalInstance "before final launch"

Write-Host "Done." -ForegroundColor Green

if ($Launch) {
    Launch-ExperimentalInstance
}