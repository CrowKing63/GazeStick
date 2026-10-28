<#
.SYNOPSIS
    Package GazeStick as a signed-less .msix for Microsoft Store submission.

.DESCRIPTION
    Stages a published GazeStick build (exe + Beam SDK DLL + MSIX logos), writes the
    AppxManifest.xml with the release version substituted into the Identity, and runs
    MakeAppx.exe to produce the .msix.

    The Beam Eye Tracker client DLL (lib\beam_eye_tracker_client.dll) MUST ship inside
    every package (see AGENTS.md licensing section). This script fails hard if it is
    missing from the build directory.

.PARAMETER Version
    Release version, e.g. "1.5.0" or "v1.5.0". Normalized to a 4-part X.Y.Z.W for the MSIX Identity.

.PARAMETER BuildDir
    A published GazeStick output directory containing GazeStick.exe and beam_eye_tracker_client.dll.
    Produce it with:
        dotnet publish -c Release -r win-x64 --self-contained false -p:Version=<ver> -o <BuildDir>
        Copy-Item lib\beam_eye_tracker_client.dll <BuildDir>\
    (CI does this; locally run the two lines above, then this script.)

.EXAMPLE
    .\scripts\package-msix.ps1 -Version 1.5.0 -BuildDir publish-msix
#>
param(
    [string]$Version  = "1.5.0",
    [string]$BuildDir = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

# --- Normalize version to 4-part X.Y.Z.W (MSIX Identity requirement) ---
$Version = $Version.TrimStart('v')
while ($Version.Split('.').Count -lt 4) { $Version += ".0" }
$msixVersion = $Version
if ((($msixVersion -split '\.').Count) -eq 3) { $msixVersion = "$msixVersion.0" }

if (-not (Test-Path $BuildDir)) {
    throw "BuildDir '$BuildDir' not found. Publish GazeStick first (see .DESCRIPTION)."
}

# --- Validate required artifacts BEFORE staging (fail fast, per AGENTS.md) ---
$exe = Join-Path $BuildDir "GazeStick.exe"
if (-not (Test-Path $exe)) { throw "GazeStick.exe not found in $BuildDir" }
$beamDll = Join-Path $BuildDir "beam_eye_tracker_client.dll"
if (-not (Test-Path $beamDll)) {
    throw "beam_eye_tracker_client.dll not found in $BuildDir — it must ship in every package. Copy lib\beam_eye_tracker_client.dll into the build dir first."
}

# --- Stage: copy the entire published build (exe + runtime deps + Beam DLL) ---
$staging = Join-Path $root "dist\msix-staging"
if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
New-Item -ItemType Directory -Force $staging | Out-Null
Copy-Item -Path (Join-Path $BuildDir "*") -Destination $staging -Recurse -Force

# --- MSIX logos ---
$assetsOut = Join-Path $staging "Assets"
New-Item -ItemType Directory -Force $assetsOut | Out-Null
Copy-Item (Join-Path $root "msix\Assets\*.png") $assetsOut -Force

# --- AppxManifest.xml: substitute Identity Version only ---
# Read/write as explicit UTF-8 so any non-ASCII is preserved.
# The regex anchors on the leading space before 'Version="' so it ONLY matches the
# Identity element's Version attribute — MinVersion/MaxVersion/MaxVersionTested have no
# leading space and are left untouched (see submission guide, trap #3).
$manifestSrc = Join-Path $root "msix\AppxManifest.xml"
$manifestText = [System.IO.File]::ReadAllText($manifestSrc, [System.Text.Encoding]::UTF8)
$manifestText = [regex]::Replace($manifestText, ' Version="\d+\.\d+\.\d+\.\d+"', (' Version="' + $msixVersion + '"'))
[System.IO.File]::WriteAllText((Join-Path $staging "AppxManifest.xml"), $manifestText, [System.Text.Encoding]::UTF8)

# --- Locate MakeAppx.exe (Windows SDK App Certification Kit) ---
$makeappx = "C:\Program Files (x86)\Windows Kits\10\App Certification Kit\MakeAppx.exe"
if (-not (Test-Path $makeappx)) {
    $found = Get-ChildItem "C:\Program Files (x86)\Windows Kits\" -Recurse -Filter "MakeAppx.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) { $makeappx = $found.FullName }
}
if (-not (Test-Path $makeappx)) { throw "MakeAppx.exe not found in Windows Kits — install the Windows SDK App Certification Kit." }

# --- Pack (unsigned; Microsoft re-signs on Store submission) ---
# Delete any prior .msix first: MakeAppx would otherwise pop a "overwrite?" GUI dialog and hang.
$msixOut = Join-Path $root "dist\GazeStick-$Version.msix"
if (Test-Path $msixOut) { Remove-Item -Force $msixOut }
& $makeappx pack /d $staging /p $msixOut /o /nv
if ($LASTEXITCODE -ne 0) { throw "MakeAppx pack failed (exit $LASTEXITCODE)" }

Write-Host "OK $msixOut created" -ForegroundColor Green
