# GMS Desktop - Installer Setup

This folder contains the Windows Installer (MSI) configuration for GMS Desktop.

## Prerequisites

1. **WiX Toolset 7.0+** - Required to build the installer
   - Download: https://wixtoolset.org/releases/
   - Install to default location: `C:\Program Files\WiX Toolset v7.0\`
   - Latest version recommended (v7.0.0)

2. **Published Application** - Must have run the publisher first
   - Run: `.\publish-desktop.ps1` (from parent directory)
   - Creates: `publish/` folder with GMS.Desktop.exe and all dependencies

## Building the Installer

### Step 1: Install WiX Toolset

```powershell
# Download and install WiX Toolset v4.0 from:
# https://wixtoolset.org/releases/

# Verify installation
dir "C:\Program Files\WiX Toolset v4.0\bin"
```

### Step 2: Build the MSI

```powershell
cd "C:\Users\Everton\source\repos\General Management System\Setup"
.\build-installer.ps1
```

This will:
1. Compile WiX source files
2. Link dependencies
3. Create `Output\GMS-Setup.msi` (~140 MB)

### Step 3: Test the Installer

```powershell
# Double-click the MSI to install:
.\Output\GMS-Setup.msi

# Or install from command line:
msiexec /i ".\Output\GMS-Setup.msi" /qn
```

## What the Installer Does

✅ Installs to: `C:\Program Files\General Management System\`
✅ Creates Start Menu shortcut
✅ Creates Desktop shortcut
✅ Registers in Windows Add/Remove Programs
✅ Sets working directory for file access
✅ Supports silent/quiet installation
✅ Supports uninstall via Control Panel

## Customization

### Change Installation Directory

Edit `Product.wxs`:
```xml
<Directory Id="INSTALLFOLDER" Name="General Management System" />
```

### Change Application Name

Edit `Product.wxs`:
```xml
<Product Name="Your Custom Name" ... />
```

### Add Company Logo/Icon

Place icon in this folder and reference in `Product.wxs`:
```xml
<Property Id="ARPPRODUCTICON" Value="YourIcon.ico" />
```

### Change Manufacturer/Company

Edit `Product.wxs`:
```xml
<Product Manufacturer="Your Company" ... />
```

## Distribution

Once built, the MSI can be:

1. **Downloaded** - Share `GMS-Setup.msi` (~140 MB)
2. **Burned to CD/USB** - Copy MSI to removable media
3. **Deployed via Group Policy** - In enterprise environments
4. **Silent Install** - Script for automation:
   ```powershell
   msiexec /i "GMS-Setup.msi" /qn ALLUSERS=1
   ```

## Uninstall

Users can uninstall via:
- Control Panel → Programs → Uninstall a program
- Command line: `msiexec /x GMS-Setup.msi /qn`

## File Structure

```
Setup/
├── Setup.wixproj       # WiX project file
├── Product.wxs         # Main installer definition
├── build-installer.ps1 # Build script
├── README.md           # This file
└── Output/
    └── GMS-Setup.msi   # Generated installer
```

## Troubleshooting

**"WiX Toolset not found"**
- Install from: https://wixtoolset.org/releases/
- Verify installation path: `C:\Program Files\WiX Toolset v4.0\bin`

**"Publish folder not found"**
- Run `.\publish-desktop.ps1` first (from parent directory)
- Creates `publish/` folder with all application files

**"Candle compilation failed"**
- Check WiX source syntax in `Product.wxs`
- Verify all referenced files exist in `../publish/`

**"Build produces large MSI"**
- This is normal - MSI includes .NET runtime (~140 MB)
- To reduce size: exclude debug symbols (already configured)

## Advanced Configuration

### Silent Installation with Logging

```powershell
msiexec /i "GMS-Setup.msi" /qn /l*v "install.log"
```

### Repair Installation

```powershell
msiexec /f "GMS-Setup.msi" /qn
```

### Customize Installation Path

```powershell
msiexec /i "GMS-Setup.msi" INSTALLFOLDER="C:\Custom\Path\"
```

## Next Steps

1. Install WiX Toolset
2. Run `.\build-installer.ps1`
3. Test the generated MSI
4. Distribute to customers

## Support

For WiX Toolset help: https://wixtoolset.org/documentation/
For GMS support: Contact your administrator
