param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "$PSScriptRoot\TsToMp4Converter\Package\Output"
)

$ErrorActionPreference = "Stop"

# Paths
$projectRoot = "$PSScriptRoot\TsToMp4Converter"
$packageDir = "$projectRoot\Package"
$buildDir = "$projectRoot\bin\$Configuration\net8.0-windows"
$layoutDir = "$OutputDir\layout"
$msixPath = "$OutputDir\TsToMp4Converter.msix"
$certPath = "$packageDir\TsToMp4Converter_TemporaryKey.pfx"
$manifestPath = "$packageDir\Package.appxmanifest"
$assetsSrc = "$packageDir\Assets"
$ffmpegSrc = "$projectRoot\bin\ffmpeg.exe"

# Tools from Windows SDK (choose x64)
$sdkBin = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.18362.0\x64"
$makeAppx = "$sdkBin\makeappx.exe"
$signtool = "$sdkBin\signtool.exe"

Write-Host "=== TS to MP4 Converter - MSIX Build ===" -ForegroundColor Cyan

# Step 1: Build WPF project
Write-Host "[1/4] Building project..." -ForegroundColor Yellow
dotnet build "$projectRoot\TsToMp4Converter.csproj" -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

# Step 2: Prepare layout directory
Write-Host "[2/4] Preparing package layout..." -ForegroundColor Yellow
if (Test-Path $layoutDir) { Remove-Item -Recurse -Force $layoutDir }
New-Item -ItemType Directory -Path $layoutDir -Force | Out-Null
New-Item -ItemType Directory -Path "$layoutDir\bin" -Force | Out-Null
New-Item -ItemType Directory -Path "$layoutDir\Assets" -Force | Out-Null

# Copy build output
Get-ChildItem $buildDir -File | Copy-Item -Destination $layoutDir

# Copy manifest
Copy-Item $manifestPath -Destination "$layoutDir\AppxManifest.xml"

# Copy assets
Copy-Item "$assetsSrc\*" -Destination "$layoutDir\Assets"

# Copy ffmpeg
if (Test-Path $ffmpegSrc) {
    Copy-Item $ffmpegSrc -Destination "$layoutDir\bin\ffmpeg.exe"
    Write-Host "  ffmpeg.exe included in package" -ForegroundColor Green
} else {
    Write-Host "  WARNING: ffmpeg.exe not found - will be downloaded on first run" -ForegroundColor Yellow
}

# Step 3: Create MSIX
Write-Host "[3/4] Creating MSIX package..." -ForegroundColor Yellow
if (Test-Path $msixPath) { Remove-Item -Force $msixPath }

& $makeAppx pack /o /p $msixPath /d $layoutDir /l
if ($LASTEXITCODE -ne 0) { throw "MakeAppx failed" }

# Step 4: Sign MSIX
Write-Host "[4/4] Signing MSIX package..." -ForegroundColor Yellow
& $signtool sign /fd SHA256 /a /f $certPath /p password123 $msixPath
if ($LASTEXITCODE -ne 0) { throw "Signing failed" }

# Clean up layout
Remove-Item -Recurse -Force $layoutDir

Write-Host "=== Done! ===" -ForegroundColor Cyan
Write-Host "MSIX: $msixPath" -ForegroundColor Green
Write-Host "Size: $((Get-Item $msixPath).Length / 1MB -as [int]) MB" -ForegroundColor Green
