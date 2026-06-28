param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$PublishDir = Join-Path $RepoRoot "publish"
$DllSource = Join-Path $RepoRoot "beam-sdk\bin\win64\beam_eye_tracker_client.dll"
$ZipName = "GazeStick-$Version-win-x64.zip"

Write-Host "=== Building GazeStick v$Version ===" -ForegroundColor Cyan

# Kill any running instance that could lock build output
Get-Process GazeStick -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

# Build
dotnet publish -c Release -r win-x64 --self-contained false -o $PublishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

# Copy SDK DLL
if (Test-Path $DllSource) {
    Copy-Item -Path $DllSource -Destination (Join-Path $PublishDir "beam_eye_tracker_client.dll") -Force
    Write-Host "SDK DLL copied." -ForegroundColor Green
} else {
    Write-Warning "beam_eye_tracker_client.dll not found at $DllSource"
    Write-Warning "The zip will NOT include the DLL."
}

# Create zip
if (Test-Path $ZipName) { Remove-Item $ZipName -Force }
Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipName
Write-Host "Created: $ZipName" -ForegroundColor Green

# Cleanup
Remove-Item $PublishDir -Recurse -Force
