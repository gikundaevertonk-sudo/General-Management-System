# Build WiX installer for GMS.Desktop
# Requires: WiX Toolset 4.0+ installed
# Install WiX: https://wixtoolset.org/releases/

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishPath = "$scriptPath\..\publish"
$outputDir = "$scriptPath\Output"

Write-Host "=== GMS Desktop Installer Builder ===" -ForegroundColor Cyan
Write-Host ""

# Check if WiX is installed
$wixExePath = "C:\Program Files\WiX Toolset v6.0\bin\wix.exe"

if (-not (Test-Path $wixExePath)) {
    Write-Host "❌ WiX Toolset v6.0 not found" -ForegroundColor Red
    Write-Host ""
    Write-Host "To install WiX:" -ForegroundColor Yellow
    Write-Host "  1. Download v6.0.0 from: https://wixtoolset.org/releases/" -ForegroundColor White
    Write-Host "  2. Download wix-cli-x64.msi" -ForegroundColor White
    Write-Host "  3. Run the installer" -ForegroundColor White
    Write-Host "  4. Run this script again" -ForegroundColor White
    exit 1
}

Write-Host "✅ WiX Toolset v6.0 found" -ForegroundColor Green
Write-Host ""

# Check if publish folder exists
if (-not (Test-Path $publishPath)) {
    Write-Host "❌ Publish folder not found at $publishPath" -ForegroundColor Red
    Write-Host "Run .\publish-desktop.ps1 first" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ Publish folder found" -ForegroundColor Green
Write-Host ""

# Create output directory
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

Write-Host "Building installer with WiX v6.0..." -ForegroundColor Yellow

$wxsFile = "$scriptPath\Product.wxs"
$msiFile = "$outputDir\GMS-Setup.msi"

# WiX v6.0 uses unified 'wix' command
Write-Host "Running: wix build $wxsFile -o $msiFile" -ForegroundColor Gray
& $wixExePath build $wxsFile -o $msiFile
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed. Trying with output path..." -ForegroundColor Yellow
    & $wixExePath build -out $msiFile $wxsFile
    if ($LASTEXITCODE -ne 0) {
        Write-Host "WiX build failed" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "SUCCESS! Installer created!" -ForegroundColor Green
Write-Host ""
Write-Host "Installer file: $msiFile" -ForegroundColor Cyan
$size = [math]::Round((Get-Item $msiFile).Length / 1MB, 0)
Write-Host "Size: $size MB" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Test install: Double-click the MSI file" -ForegroundColor White
Write-Host "  2. Verify it installs to Program Files" -ForegroundColor White
Write-Host "  3. Verify shortcuts created" -ForegroundColor White
Write-Host "  4. Distribute to customers" -ForegroundColor White
Write-Host ""
