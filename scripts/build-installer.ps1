param(
    [string]$Version = "1.2.0"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$PublishDir = Join-Path $RepoRoot "publish-installer"
$DllSource = Join-Path $RepoRoot "lib\beam_eye_tracker_client.dll"
$IssPath = Join-Path $PSScriptRoot "setup.iss"

Write-Host "=== Building GazeStick Installer v$Version ===" -ForegroundColor Cyan

# 1. Fetch SDK DLL (shared with portable build)
& "$PSScriptRoot\fetch-sdk.ps1"

# 2. Kill any running instance that could lock build output
Get-Process GazeStick -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

# 3. Build (self-contained for installer — no .NET runtime dependency)
dotnet publish -c Release -r win-x64 --self-contained true -p:Version=$Version -o $PublishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

# 4. Copy SDK DLL into publish output (csproj CopyBeamDll target already does this on build,
#    but ensure it's present since the publish output is a separate directory)
if (Test-Path $DllSource) {
    Copy-Item -Path $DllSource -Destination (Join-Path $PublishDir "beam_eye_tracker_client.dll") -Force
    Write-Host "SDK DLL copied." -ForegroundColor Green
} else {
    throw "beam_eye_tracker_client.dll not found at $DllSource — run scripts/fetch-sdk.ps1 first."
}

# 5. Check if Inno Setup is installed
$iscc = Get-Command "iscc" -ErrorAction SilentlyContinue
if (-not $iscc) {
    throw "Inno Setup compiler (iscc.exe) not found. Install it from https://jrsoftware.org/isdl.php"
}

# 6. Build installer
Write-Host "Running Inno Setup compiler ..." -ForegroundColor Yellow
& $iscc.Source "/DMyAppVersion=$Version" "/DSourcePath=$PublishDir" $IssPath
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE"
}
Write-Host "Installer created: GazeStick-setup-$Version.exe" -ForegroundColor Green

# 7. Cleanup
Remove-Item $PublishDir -Recurse -Force
