# Build NSIS Installer for GMS Desktop
# Much simpler than WiX!

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishPath = "$scriptPath\..\publish"

Write-Host "=== GMS Desktop Installer Builder (NSIS) ===" -ForegroundColor Cyan
Write-Host ""

# Check if NSIS is installed
$nsisPath = "C:\Program Files\NSIS\makensis.exe"
if (-not (Test-Path $nsisPath)) {
    $nsisPath = "C:\Program Files (x86)\NSIS\makensis.exe"
}

if (-not (Test-Path $nsisPath)) {
    Write-Host "NSIS not found. Please install it:" -ForegroundColor Red
    Write-Host "  https://nsis.sourceforge.io/Download" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Or use Chocolatey:" -ForegroundColor Yellow
    Write-Host "  choco install nsis" -ForegroundColor White
    exit 1
}

Write-Host "✅ NSIS found at: $nsisPath" -ForegroundColor Green
Write-Host ""

# Check if publish folder exists
if (-not (Test-Path $publishPath)) {
    Write-Host "❌ Publish folder not found" -ForegroundColor Red
    Write-Host "Run .\publish-desktop.ps1 first" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ Publish folder found" -ForegroundColor Green
Write-Host ""

# Build the installer
Write-Host "Building installer..." -ForegroundColor Yellow
& $nsisPath "$scriptPath\GMS-Installer.nsi"

if ($LASTEXITCODE -eq 0) {
    $msiFile = "$scriptPath\..\GMS-Setup.exe"
    if (Test-Path $msiFile) {
        $size = [math]::Round((Get-Item $msiFile).Length / 1MB, 0)
        Write-Host ""
        Write-Host "✅ SUCCESS! Installer created!" -ForegroundColor Green
        Write-Host ""
        Write-Host "File: $msiFile" -ForegroundColor Cyan
        Write-Host "Size: $size MB" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "To test: Double-click GMS-Setup.exe" -ForegroundColor White
    }
} else {
    Write-Host "❌ Build failed" -ForegroundColor Red
    exit 1
}
