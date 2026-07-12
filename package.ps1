param(
    [string]$Version = "1.2.0"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$PublishDir = Join-Path $RepoRoot "publish"
$DllSource = Join-Path $RepoRoot "lib\beam_eye_tracker_client.dll"
$ZipName = "GazeStick-portable-$Version-win-x64.zip"

Write-Host "=== Building GazeStick Portable v$Version ===" -ForegroundColor Cyan

# 1. Fetch SDK DLL (reused by both portable and installer builds)
& "$PSScriptRoot\scripts\fetch-sdk.ps1"

# 2. Kill any running instance that could lock build output
Get-Process GazeStick -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

# 3. Build (framework-dependent for portable)
dotnet publish -c Release -r win-x64 --self-contained false -p:Version=$Version -o $PublishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

# 4. Copy SDK DLL
if (Test-Path $DllSource) {
    Copy-Item -Path $DllSource -Destination (Join-Path $PublishDir "beam_eye_tracker_client.dll") -Force
    Write-Host "SDK DLL copied." -ForegroundColor Green
} else {
    throw "beam_eye_tracker_client.dll not found at $DllSource — run scripts/fetch-sdk.ps1 first."
}

# 5. Create portable zip
if (Test-Path $ZipName) { Remove-Item $ZipName -Force }
Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipName
Write-Host "Created: $ZipName" -ForegroundColor Green

# 6. Cleanup
Remove-Item $PublishDir -Recurse -Force
