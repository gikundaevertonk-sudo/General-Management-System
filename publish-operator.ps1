# Publish the operator console and register it to start at logon.
#
# Run this once to set it up, and again whenever you change the console - it stops the running
# copy, republishes, and restarts it. After that you never touch a terminal to use the console:
# open the desktop shortcut, or bookmark http://127.0.0.1:5240.
#
#   .\publish-operator.ps1              publish + register the logon task + start it
#   .\publish-operator.ps1 -Remove      stop it, delete the task and the shortcut

param([switch]$Remove)

$ErrorActionPreference = 'Stop'

$installTo = Join-Path $env:LOCALAPPDATA 'GMS Operator'
$port      = 5240
$url       = "http://127.0.0.1:$port"
# .url, not .lnk: a Windows shortcut's target has to be a file path, so pointing a .lnk at an
# address silently leaves it with no target at all. An internet shortcut is the right kind.
$shortcut  = Join-Path ([Environment]::GetFolderPath('Desktop')) 'GMS Operator.url'
$startMenu = Join-Path ([Environment]::GetFolderPath('Programs')) 'GMS Operator.url'
# The Startup folder, not Task Scheduler: registering a task in the root library needs
# administrator rights on this machine, and this needs none. It is also obvious and trivially
# undone - the autostart is one file you can see and delete.
$startup   = Join-Path ([Environment]::GetFolderPath('Startup')) 'GMS Operator.vbs'
$repo      = Split-Path -Parent $MyInvocation.MyCommand.Path

function Stop-Console {
    Get-Process GMS.Operator -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -like "$installTo*" } |
        ForEach-Object { Stop-Process -Id $_.Id -Force }
    Start-Sleep -Milliseconds 700
}

if ($Remove) {
    Write-Host 'Removing the operator console...' -ForegroundColor Yellow
    Stop-Console
    Remove-Item $startup, $shortcut, $startMenu -ErrorAction SilentlyContinue
    Remove-Item $installTo -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host 'Removed. Nothing starts at logon any more.' -ForegroundColor Green
    return
}

Write-Host '=== GMS Operator Console setup ===' -ForegroundColor Cyan

# ---------------------------------------------------------------------------
# 1. Carry the configuration across from user-secrets.
#    A published app runs in Production, and .NET loads user-secrets only in Development, so the
#    settings have to be written beside the executable. This is no more exposed than where they
#    already live: user-secrets is itself plaintext JSON under %APPDATA%\Microsoft\UserSecrets.
# ---------------------------------------------------------------------------
Write-Host 'Reading configuration from user-secrets...' -ForegroundColor Yellow
$secrets = dotnet user-secrets list --project (Join-Path $repo 'GMS.Operator') 2>$null
function Get-Secret($key) { ($secrets | Where-Object { $_ -like "$key = *" }) -replace [regex]::Escape("$key = "), '' }
$cs   = Get-Secret 'ConnectionStrings:Gms'
$user = Get-Secret 'Platform:Operator:UserName'
$hash = Get-Secret 'Platform:Operator:PasswordHash'
foreach ($pair in @(@('ConnectionStrings:Gms',$cs), @('Platform:Operator:UserName',$user), @('Platform:Operator:PasswordHash',$hash))) {
    if ([string]::IsNullOrWhiteSpace($pair[1])) {
        throw "$($pair[0]) is not set. Run: dotnet user-secrets set `"$($pair[0])`" `"<value>`" --project GMS.Operator"
    }
}

Stop-Console

# ---------------------------------------------------------------------------
# 2. Publish. Self-contained so it does not need the .NET SDK or runtime present to run.
# ---------------------------------------------------------------------------
Write-Host 'Publishing (this takes a minute)...' -ForegroundColor Yellow
if (Test-Path $installTo) { Remove-Item $installTo -Recurse -Force }
dotnet publish (Join-Path $repo 'GMS.Operator\GMS.Operator.csproj') `
    --configuration Release --runtime win-x64 --self-contained true `
    --output $installTo -v q --nologo
if ($LASTEXITCODE -ne 0) { throw 'publish failed' }

# ---------------------------------------------------------------------------
# 3. Settings beside the executable. Urls binds to 127.0.0.1 only - the console can read across
#    every tenant and delete an organisation, so nothing outside this machine should reach it.
# ---------------------------------------------------------------------------
@{
    Urls              = $url
    ConnectionStrings = @{ Gms = $cs }
    Platform          = @{ Operator = @{ UserName = $user; PasswordHash = $hash } }
    Logging           = @{ LogLevel = @{ Default = 'Warning'; 'Microsoft.AspNetCore' = 'Warning' } }
} | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $installTo 'appsettings.Production.json') -Encoding utf8

$exe = Join-Path $installTo 'GMS.Operator.exe'

# ---------------------------------------------------------------------------
# 4. Start it at logon, with no window.
#    The launcher is a one-line script rather than a shortcut because a shortcut to a console
#    executable flashes a black window every time you log in; Run(..., 0, False) does not.
# ---------------------------------------------------------------------------
Write-Host 'Setting it to start at logon...' -ForegroundColor Yellow
"CreateObject(""WScript.Shell"").Run """"""$exe"""""", 0, False" |
    Set-Content $startup -Encoding ascii

Write-Host 'Starting it now...' -ForegroundColor Yellow
Start-Process -FilePath $exe -WorkingDirectory $installTo -WindowStyle Hidden

# ---------------------------------------------------------------------------
# 5. A desktop shortcut that just opens the address.
# ---------------------------------------------------------------------------
# The icon is copied into the install folder and referenced there, not in the repository. A
# shortcut pointing at the repo loses its picture the moment that folder is renamed, moved or
# cloned somewhere else - and the shortcut is the only way this console gets opened, so it should
# not depend on anything outside its own installation.
$icon = Join-Path $installTo 'gms.ico'
$repoIcon = Join-Path $repo 'assets\gms.ico'
if (Test-Path $repoIcon) { Copy-Item $repoIcon $icon -Force }

$lines = @('[InternetShortcut]', "URL=$url")
if (Test-Path $icon) { $lines += @("IconFile=$icon", 'IconIndex=0') }
foreach ($target in @($shortcut, $startMenu)) { $lines | Set-Content $target -Encoding ascii }

# Clear the broken .lnk an earlier version of this script left behind: a Windows shortcut's target
# has to be a file path, so pointing one at an address leaves it with no target at all.
Remove-Item (Join-Path ([Environment]::GetFolderPath('Desktop')) 'GMS Operator.lnk') -ErrorAction SilentlyContinue

Write-Host ''
Write-Host "  Console:   $url" -ForegroundColor Cyan
Write-Host "  Sign in as: $user"
Write-Host "  Shortcuts: desktop and Start menu, both named 'GMS Operator'"
Write-Host "  Starts automatically at logon. No terminal needed."
Write-Host ''
Write-Host "  User-secrets on GMS.Operator is the master copy of the credential and the" -ForegroundColor DarkGray
Write-Host "  connection string; this script copies them next to the executable, which is the" -ForegroundColor DarkGray
Write-Host "  only place a published app can read them. Change either one there, then run this" -ForegroundColor DarkGray
Write-Host "  script again - that is what keeps the two copies the same." -ForegroundColor DarkGray
Write-Host ''
Write-Host "  To remove everything: .\publish-operator.ps1 -Remove"
Write-Host ''
