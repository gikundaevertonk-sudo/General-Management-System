# Publish GMS.Desktop as a standalone executable for distribution
# This creates a complete application that can run on any Windows 10/11 machine without .NET installed

$projectPath = "GMS.Desktop"
$outputDir = "publish"
$publishProfile = "Release"

Write-Host "=== GMS.Desktop Publisher ===" -ForegroundColor Cyan
Write-Host ""

# Clean previous publish
Write-Host "Cleaning previous publish..." -ForegroundColor Yellow
if (Test-Path $outputDir) {
    Remove-Item $outputDir -Recurse -Force
}

# Create publish directory
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

# Publish as self-contained executable
Write-Host "Publishing as self-contained executable..." -ForegroundColor Yellow
Write-Host "This may take 2-3 minutes..." -ForegroundColor Gray

dotnet publish $projectPath `
  --configuration $publishProfile `
  --output $outputDir `
  --self-contained `
  --runtime win-x64

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✅ Publish completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Executable location:" -ForegroundColor Cyan
    Write-Host "  $PWD\$outputDir\GMS.Desktop.exe" -ForegroundColor White
    Write-Host ""
    Write-Host "What you can now do:" -ForegroundColor Cyan
    Write-Host "  1. Run locally:     .\$outputDir\GMS.Desktop.exe" -ForegroundColor White
    Write-Host "  2. Copy to USB:     Copy the entire '$outputDir' folder to USB" -ForegroundColor White
    Write-Host "  3. Install on PC:   Copy '$outputDir' to Program Files or Desktop" -ForegroundColor White
    Write-Host "  4. Create shortcut: Right-click GMS.Desktop.exe > Send to > Desktop (create shortcut)" -ForegroundColor White
    Write-Host ""
    Write-Host "Size: ~$([math]::Round((Get-ChildItem $outputDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB, 0)) MB" -ForegroundColor Gray
    Write-Host ""

    # Optionally create a batch file launcher
    $launcherPath = "$outputDir\Start GMS.bat"
    "@echo off" | Out-File $launcherPath -Encoding ASCII
    "start GMS.Desktop.exe" | Out-File $launcherPath -Encoding ASCII -Append
    Write-Host "Created launcher: Start GMS.bat" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "❌ Publish failed! Check errors above." -ForegroundColor Red
}
