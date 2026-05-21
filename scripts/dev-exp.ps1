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
$visualStudioLocalAppData = Join-Path $env:LOCALAPPDATA "Microsoft\VisualStudio"

$vsixInstaller = "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\VSIXInstaller.exe"
$devenvExe = "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe"
$devenvCom = "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.com"

$extensionId = "GhostTextVsix.0fd3a8ea-948f-4c4c-97b0-3bba43fc8630"
$extensionAssemblyName = "GhostTextVsix.dll"
$extensionStateFiles = @(
    "ExtensionMetadataCache.sqlite",
    "ExtensionMetadata.mpack",
    "extensions.configurationchanged"
)
$commonBuildProperties = @(
    "/p:Configuration=Debug",
    "/p:DeployExtension=false",
    "/p:CreateVsixContainer=true"
)

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

function Remove-PathIfExists {
    param(
        [string]$Path
    )

    if (!(Test-Path -LiteralPath $Path)) {
        return
    }

    try {
        Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
    }
    catch {
        Write-Warning "Remove-Item failed for '$Path': $($_.Exception.Message). Retrying with .NET file APIs."

        if ([System.IO.Directory]::Exists($Path)) {
            [System.IO.Directory]::Delete($Path, $true)
        }
        elseif ([System.IO.File]::Exists($Path)) {
            [System.IO.File]::Delete($Path)
        }
        else {
            throw
        }
    }
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

function Get-ExperimentalHiveDirectories {
    if (!(Test-Path -LiteralPath $visualStudioLocalAppData)) {
        return @()
    }

    return @(
        Get-ChildItem -LiteralPath $visualStudioLocalAppData -Directory -Force |
            Where-Object { $_.Name -match "Exp$" }
    )
}

function Test-GhostTextExtensionDirectory {
    param(
        [System.IO.DirectoryInfo]$Directory
    )

    $manifest = Join-Path $Directory.FullName "manifest.json"
    $vsixManifest = Join-Path $Directory.FullName "extension.vsixmanifest"
    $assembly = Join-Path $Directory.FullName $extensionAssemblyName

    foreach ($candidate in @($manifest, $vsixManifest)) {
        if (!(Test-Path -LiteralPath $candidate)) {
            continue
        }

        try {
            $content = Get-Content -LiteralPath $candidate -Raw -ErrorAction Stop
            if ($content.Contains($extensionId)) {
                return $true
            }
        }
        catch {
            Write-Warning "Could not inspect extension metadata '$candidate': $($_.Exception.Message)"
        }
    }

    # Older build/deploy runs can leave DLL-only folders. Those broken entries
    # confuse VSIXInstaller and can make the first Experimental launch disabled.
    return (Test-Path -LiteralPath $assembly) -and
        !(Test-Path -LiteralPath $manifest) -and
        !(Test-Path -LiteralPath $vsixManifest)
}

function Remove-ExperimentalExtensionDirectories {
    foreach ($hive in Get-ExperimentalHiveDirectories) {
        $extensionsDir = Join-Path $hive.FullName "Extensions"
        if (!(Test-Path -LiteralPath $extensionsDir)) {
            continue
        }

        Get-ChildItem -LiteralPath $extensionsDir -Directory -Force |
            Where-Object { Test-GhostTextExtensionDirectory -Directory $_ } |
            ForEach-Object {
                Write-Host "Removing stale Experimental extension directory: $($_.FullName)"
                Remove-Item -LiteralPath $_.FullName -Recurse -Force
            }
    }
}

function Clear-ExperimentalExtensionCaches {
    foreach ($hive in Get-ExperimentalHiveDirectories) {
        $extensionsDir = Join-Path $hive.FullName "Extensions"
        foreach ($stateFile in $extensionStateFiles) {
            $path = Join-Path $extensionsDir $stateFile
            if (Test-Path -LiteralPath $path) {
                Write-Host "Removing Experimental extension state file: $path"
                Remove-Item -LiteralPath $path -Force
            }
        }

        $componentModelCache = Join-Path $hive.FullName "ComponentModelCache"
        if (Test-Path -LiteralPath $componentModelCache) {
            Write-Host "Removing Experimental ComponentModelCache: $componentModelCache"
            Remove-Item -LiteralPath $componentModelCache -Recurse -Force
        }

        $definitionCache = Join-Path $hive.FullName "ExtensibilitySettings\DefinitionCache.dat"
        if (Test-Path -LiteralPath $definitionCache) {
            Write-Host "Removing Experimental extension definition cache: $definitionCache"
            Remove-Item -LiteralPath $definitionCache -Force
        }
    }
}

function Reset-ExperimentalExtensionInstallState {
    Write-Host "Clearing Experimental extension install state..." -ForegroundColor Yellow
    Remove-ExperimentalExtensionDirectories
    Clear-ExperimentalExtensionCaches
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
    Reset-ExperimentalExtensionInstallState

    Write-Host "Installing new extension to Experimental Instance..." -ForegroundColor Yellow

    & $vsixInstaller /quiet /rootSuffix:Exp $vsix

    if ($LASTEXITCODE -ne 0) {
        throw "VSIX install failed. ExitCode=$LASTEXITCODE"
    }

    Close-ExperimentalInstance "after install"
    Clear-ExperimentalExtensionCaches
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

Remove-PathIfExists "$root\src\GhostTextVsix\bin"
Remove-PathIfExists "$root\src\GhostTextVsix\obj"
Remove-PathIfExists $vsix

$rebuildArguments = @(
    $project,
    "/restore",
    "/t:Rebuild"
) + $commonBuildProperties + @(
    "/v:minimal"
)

Invoke-DotNetMsBuild `
    -Action "Building project..." `
    -Arguments $rebuildArguments

$pkgdefArguments = @(
    $project,
    "/t:GeneratePkgDef"
) + $commonBuildProperties + @(
    "/v:minimal"
)

Invoke-DotNetMsBuild `
    -Action "Generating pkgdef..." `
    -Arguments $pkgdefArguments

if (!(Test-Path $pkgdef)) {
    throw "pkgdef was not generated: $pkgdef"
}

$vsixArguments = @(
    $project,
    "/t:CreateVsixContainer"
) + $commonBuildProperties + @(
    "/v:minimal"
)

Invoke-DotNetMsBuild `
    -Action "Creating VSIX container..." `
    -Arguments $vsixArguments

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
