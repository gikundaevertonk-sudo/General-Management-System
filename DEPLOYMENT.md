# GMS Desktop Application - Deployment Guide

## Publishing the Application

### Quick Start (PowerShell)

```powershell
.\publish-desktop.ps1
```

This creates a **standalone executable** in the `publish/` folder that can run on any Windows 10/11 machine without requiring .NET installation.

### What Gets Published

- **GMS.Desktop.exe** - Main application executable (~200MB)
- All dependencies bundled inside
- Ready to run on any Windows PC

### Deployment Options

#### Option 1: USB/Portable Install
1. Run `publish-desktop.ps1`
2. Copy entire `publish` folder to USB drive
3. Give USB to customer
4. They extract and run `GMS.Desktop.exe`

#### Option 2: Program Files Install
1. Run `publish-desktop.ps1`
2. Copy `publish` folder to `C:\Program Files\GMS`
3. Create desktop shortcut to `GMS.Desktop.exe`
4. Share installer with customers

#### Option 3: Installer (Future)
We can create a proper MSI installer using WiX that:
- Adds to Start Menu
- Creates uninstaller
- Handles registry entries
- Auto-updates

### Configuration

The application reads connection settings from:
- **User Secrets** (local development)
- **Environment Variables** (deployment)
- **appsettings.json** (fallback)

#### For Deployment, set:
```powershell
# Windows environment variables
[System.Environment]::SetEnvironmentVariable("ConnectionStrings:Gms", "your-supabase-connection-string", "User")
```

Or edit `appsettings.json` in the application folder:
```json
{
  "ConnectionStrings": {
    "Gms": "your-supabase-connection-string"
  }
}
```

### System Requirements

- **OS:** Windows 10 / Windows 11
- **Architecture:** x64 (64-bit)
- **RAM:** 2GB minimum, 4GB recommended
- **Disk:** ~300MB for application
- **Internet:** Required for Supabase connection

### Distribution Checklist

- [ ] Run `publish-desktop.ps1` to create executable
- [ ] Test `publish/GMS.Desktop.exe` on a clean Windows machine
- [ ] Create installation instructions PDF
- [ ] Include connection string setup guide
- [ ] Provide support email/phone
- [ ] Document login credentials for trial organizations

### Login Credentials (Default)

- **Organization:** `default`
- **Username:** `admin`
- **Password:** `ChangeMe#2026` (users should change on first login)

### Troubleshooting

**"GMS.Desktop.exe not found"**
- Run `publish-desktop.ps1` first
- Wait for publish to complete (2-3 minutes)

**"Cannot connect to database"**
- Verify Supabase connection string in appsettings.json
- Check internet connection
- Verify user secrets are set correctly

**"Application crashes on startup"**
- Check event log (Windows Event Viewer)
- Verify all dependencies are included in publish folder
- Try running from administrator account

### Building Installer (WiX)

To create a professional MSI installer:

```powershell
# Install WiX tools
dotnet tool install --global wix

# Create installer
wix extension add WixToolset.UI.wixext
# Add Setup.wxs project file and build
```

This creates an `.msi` file that:
- Installs to Program Files
- Creates Start Menu shortcuts
- Adds Uninstall option
- Handles upgrades
