param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "artifacts\release",
    [switch]$RequireSignature
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repoRoot "src\GhostTextVsix\GhostTextVsix.csproj"
$pkgdefPath = Join-Path $repoRoot "src\GhostTextVsix\obj\$Configuration\net48\GhostTextVsix.pkgdef"
$vsixPath = Join-Path $repoRoot "src\GhostTextVsix\.vsix"
$manifestPath = Join-Path $repoRoot "src\GhostTextVsix\source.extension.vsixmanifest"
$artifactRoot = Join-Path $repoRoot $OutputDir
$repositoryUrl = "https://github.com/Ciellllllllll/re-code"
$repositorySlug = "Ciellllllllll/re-code"
$signToolVersion = "0.9.1-beta.26227.3"
$defaultTimestampUrl = "http://timestamp.acs.microsoft.com/"

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

function Get-CertificateSha256Fingerprint {
    param(
        [string]$CertificatePath,
        [string]$Password
    )

    $securePassword = ConvertTo-SecureString -String $Password -AsPlainText -Force
    $certificate = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($CertificatePath, $securePassword)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()

    try {
        return ([BitConverter]::ToString($sha256.ComputeHash($certificate.RawData))).Replace("-", [string]::Empty)
    }
    finally {
        $sha256.Dispose()
        $certificate.Dispose()
    }
}

function Invoke-VsixSigning {
    param(
        [string]$VsixPath,
        [string]$DisplayName,
        [string]$PackageUrl
    )

    $certificateBase64 = $env:RECODE_VSIX_CERT_BASE64
    $certificatePassword = $env:RECODE_VSIX_CERT_PASSWORD
    $timestampUrl = $env:RECODE_VSIX_TIMESTAMP_URL

    if ([string]::IsNullOrWhiteSpace($certificateBase64) -or [string]::IsNullOrWhiteSpace($certificatePassword)) {
        if ($RequireSignature) {
            throw "VSIX signing is required, but RECODE_VSIX_CERT_BASE64 or RECODE_VSIX_CERT_PASSWORD is not configured."
        }

        Write-Host "VSIX signing skipped because no signing certificate was configured." -ForegroundColor DarkYellow
        return $false
    }

    $signCommand = Get-Command sign -ErrorAction SilentlyContinue
    if ($null -eq $signCommand) {
        if ($RequireSignature) {
            throw "Sign CLI was not found. Install it with: dotnet tool install --global sign --version $signToolVersion --prerelease"
        }

        Write-Host "VSIX signing skipped because Sign CLI is not installed." -ForegroundColor DarkYellow
        return $false
    }

    if ([string]::IsNullOrWhiteSpace($timestampUrl)) {
        $timestampUrl = $defaultTimestampUrl
    }

    $certificatePath = Join-Path ([System.IO.Path]::GetTempPath()) ("re-code-signing-" + [guid]::NewGuid().ToString() + ".pfx")

    try {
        [System.IO.File]::WriteAllBytes($certificatePath, [Convert]::FromBase64String($certificateBase64))
        $fingerprint = Get-CertificateSha256Fingerprint -CertificatePath $certificatePath -Password $certificatePassword

        & $signCommand.Source code certificate-store $VsixPath `
            -cf $certificatePath `
            -p $certificatePassword `
            -cfp $fingerprint `
            -cfpa sha256 `
            -d "$DisplayName VSIX Signature" `
            -u $PackageUrl `
            -t $timestampUrl

        if ($LASTEXITCODE -ne 0) {
            throw "VSIX signing failed. ExitCode=$LASTEXITCODE"
        }

        Write-Host "VSIX signing completed." -ForegroundColor Green
        return $true
    }
    finally {
        Remove-PathIfExists $certificatePath
    }
}

if (!(Test-Path -LiteralPath $projectPath)) {
    throw "Project not found: $projectPath"
}

[xml]$manifest = Get-Content -LiteralPath $manifestPath
$ns = New-Object System.Xml.XmlNamespaceManager($manifest.NameTable)
$ns.AddNamespace("vsix", "http://schemas.microsoft.com/developer/vsx-schema/2011")
$identity = $manifest.SelectSingleNode("/vsix:PackageManifest/vsix:Metadata/vsix:Identity", $ns)

if ($null -eq $identity) {
    throw "Could not read VSIX identity from $manifestPath"
}

$version = $identity.Version
$displayName = $manifest.SelectSingleNode("/vsix:PackageManifest/vsix:Metadata/vsix:DisplayName", $ns).InnerText
$safeName = ($displayName -replace '[^A-Za-z0-9._-]', '-').ToLowerInvariant()
$releaseVsixName = "$safeName-v$version.vsix"
$releaseVsixPath = Join-Path $artifactRoot $releaseVsixName
$releaseNotesPath = Join-Path $artifactRoot "release-notes.md"
$sha256Path = Join-Path $artifactRoot "SHA256SUMS.txt"

Write-Host "== re:code release build ==" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration"
Write-Host "Version: $version"

Set-Location $repoRoot

Remove-PathIfExists $artifactRoot
Remove-PathIfExists (Join-Path $repoRoot "src\GhostTextVsix\bin")
Remove-PathIfExists (Join-Path $repoRoot "src\GhostTextVsix\obj")
Remove-PathIfExists $vsixPath

$buildArguments = @(
    $projectPath,
    "/restore",
    "/t:Rebuild;GeneratePkgDef;CreateVsixContainer",
    "/p:Configuration=$Configuration",
    "/p:DeployExtension=false",
    "/p:CreateVsixContainer=true",
    "/v:minimal"
)

dotnet msbuild @buildArguments

if ($LASTEXITCODE -ne 0) {
    throw "Release build failed. ExitCode=$LASTEXITCODE"
}

if (!(Test-Path -LiteralPath $pkgdefPath)) {
    throw "pkgdef was not generated: $pkgdefPath"
}

if (!(Test-Path -LiteralPath $vsixPath)) {
    throw "VSIX was not generated: $vsixPath"
}

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
Copy-Item -LiteralPath $vsixPath -Destination $releaseVsixPath -Force
$wasSigned = Invoke-VsixSigning -VsixPath $releaseVsixPath -DisplayName $displayName -PackageUrl $repositoryUrl

$hash = Get-FileHash -LiteralPath $releaseVsixPath -Algorithm SHA256
"$($hash.Hash)  $releaseVsixName" | Set-Content -LiteralPath $sha256Path -Encoding ascii

$releaseNotes = @(
    "# re:code v$version",
    "",
    "## What's Included",
    "",
    "- Visual Studio 2022 extension package: $releaseVsixName",
    "- SHA-256 checksum: SHA256SUMS.txt",
    "",
    "## Installation",
    "",
    "1. Close Visual Studio 2022.",
    "2. Download $releaseVsixName.",
    "3. Double-click the file to launch the VSIX installer.",
    "4. Reopen Visual Studio after the installer completes.",
    "",
    "## Integrity Verification",
    "",
    "PowerShell checksum verification:",
    "",
    "Get-FileHash .\$releaseVsixName -Algorithm SHA256",
    "Get-Content .\SHA256SUMS.txt",
    "",
    "GitHub artifact attestation verification:",
    "",
    "Requires GitHub CLI (gh) with the attestation feature available.",
    "",
    "gh attestation verify .\$releaseVsixName -R $repositorySlug",
    "",
    "## Notes",
    "",
    "- Supported IDEs: Visual Studio 2022 Community / Professional / Enterprise",
    "- Target files: C / C++",
    "- Configure your provider and API key from Tools > Options > re:code",
    ("- Code signing: " + ($(if ($wasSigned) { "signed" } else { "not signed" })))
) -join [Environment]::NewLine

$releaseNotes | Set-Content -LiteralPath $releaseNotesPath -Encoding utf8

Write-Host "Release artifact created:" -ForegroundColor Green
Get-Item -LiteralPath $releaseVsixPath, $sha256Path, $releaseNotesPath |
    Select-Object FullName, Length, LastWriteTime
