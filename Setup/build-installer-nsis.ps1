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

# ---------------------------------------------------------------------------
# Pre-flight: the installer ships the publish folder wholesale (File /r), so
# anything left in it goes out to every customer. These are the things that must
# never leave this machine. Refusing to build is the only reliable guard - a note
# in a README is not one.
# ---------------------------------------------------------------------------
Write-Host "Checking the customer build carries no operator or database secrets..." -ForegroundColor Yellow
$problems = @()

$appSettings = Join-Path $publishPath "appsettings.json"
if (Test-Path $appSettings) {
    try {
        $cfg = Get-Content $appSettings -Raw | ConvertFrom-Json
        $cs = $cfg.ConnectionStrings.Gms
        if (-not [string]::IsNullOrWhiteSpace($cs)) {
            $problems += "appsettings.json has a connection string. Customers configure their own; shipping yours hands them your database."
        }
    } catch {
        $problems += "appsettings.json is not valid JSON."
    }
}

# The operator console is web-only and its credentials live in GMS.Web user-secrets.
# If either ever appears in a desktop build, something has gone badly wrong.
$forbidden = @{
    "Platform:Operator"   = "operator console credentials"
    "Platform__Operator"  = "operator console credentials (environment form)"
    "supabase.com"        = "a Supabase host name"
    "PBKDF2-SHA256."      = "a password hash"
}
foreach ($needle in $forbidden.Keys) {
    $hits = Get-ChildItem $publishPath -Recurse -File -Include *.json,*.config,*.xml,*.txt,*.bat -ErrorAction SilentlyContinue |
            Where-Object { Select-String -Path $_.FullName -Pattern ([regex]::Escape($needle)) -SimpleMatch -Quiet -ErrorAction SilentlyContinue }
    if ($hits) {
        $problems += "$($forbidden[$needle]) found in: $(($hits | ForEach-Object { $_.Name }) -join ', ')"
    }
}

if ($problems.Count -gt 0) {
    Write-Host ""
    Write-Host "REFUSING TO BUILD - the customer installer would leak something:" -ForegroundColor Red
    foreach ($p in $problems) { Write-Host "  - $p" -ForegroundColor Red }
    Write-Host ""
    Write-Host "Clear it from the publish folder and re-run .\publish-desktop.ps1." -ForegroundColor Yellow
    exit 1
}
Write-Host "✅ No secrets in the customer build" -ForegroundColor Green
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
