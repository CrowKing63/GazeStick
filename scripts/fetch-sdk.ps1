param(
    [string]$SdkVersion = "2.1.0"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$DllTargetDir = Join-Path $RepoRoot "lib"
$DllTargetPath = Join-Path $DllTargetDir "beam_eye_tracker_client.dll"
$DownloadUrl = "https://eyewarecistorage.blob.core.windows.net/beam-sdk/beam_eye_tracker_sdk-$SdkVersion.zip"

Write-Host "=== Fetch Beam SDK v$SdkVersion ===" -ForegroundColor Cyan

# Skip if already present
if (Test-Path $DllTargetPath) {
    Write-Host "SDK DLL already present at $DllTargetPath — skipping download." -ForegroundColor Green
    return
}

# Ensure target directory exists
if (-not (Test-Path $DllTargetDir)) {
    New-Item -ItemType Directory -Path $DllTargetDir -Force | Out-Null
}

# Download
$zipPath = Join-Path $RepoRoot "_beam_sdk_temp.zip"
try {
    Write-Host "Downloading Beam SDK v$SdkVersion ..." -ForegroundColor Yellow
    Invoke-WebRequest -Uri $DownloadUrl -OutFile $zipPath -UseBasicParsing
    Write-Host "Downloaded ($((Get-Item $zipPath).Length) bytes)" -ForegroundColor Green

    # Extract
    $extractPath = Join-Path $RepoRoot "_beam_sdk_temp_extract"
    if (Test-Path $extractPath) { Remove-Item $extractPath -Recurse -Force }
    Expand-Archive -Path $zipPath -DestinationPath $extractPath

    # Locate DLL within extracted tree
    $foundDll = Get-ChildItem -Path $extractPath -Recurse -Filter "beam_eye_tracker_client.dll" | Select-Object -First 1
    if (-not $foundDll) {
        throw "beam_eye_tracker_client.dll not found in the downloaded SDK archive."
    }

    Copy-Item -Path $foundDll.FullName -Destination $DllTargetPath -Force
    Write-Host "SDK DLL copied to $DllTargetPath" -ForegroundColor Green
}
catch {
    Write-Host "ERROR: Failed to download/extract Beam SDK v$SdkVersion." -ForegroundColor Red
    Write-Host "URL: $DownloadUrl" -ForegroundColor Red
    Write-Host "This may happen if the SDK URL has changed. Check https://docs.beam.eyeware.tech/ for the latest version." -ForegroundColor Red
    Write-Host "To update, edit the `$SdkVersion variable at the top of this script or set the SdkVersion parameter." -ForegroundColor Red
    throw $_
}
finally {
    # Cleanup temp files
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    $extractPath = Join-Path $RepoRoot "_beam_sdk_temp_extract"
    if (Test-Path $extractPath) { Remove-Item $extractPath -Recurse -Force }
}
