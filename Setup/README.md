# The customer installer

Packages `GMS.Desktop` into a single `GMS-Setup.exe` for customers to run. This is the only thing
that ever goes to a customer — `GMS.Web` is the hosted site, and `GMS.Operator` stays with you.

## Building it

From the repository root, in PowerShell:

```powershell
.\publish-desktop.ps1
.\Setup\build-installer-nsis.ps1
```

The result is `GMS-Setup.exe` in the repository root. It is not committed — it is around 55 MB and
`.gitignore` covers both it and `publish/`.

You need [NSIS](https://nsis.sourceforge.io/Download) installed; the build script looks in both
`C:\Program Files\NSIS` and `C:\Program Files (x86)\NSIS` and tells you if it is missing.

## Publishing a release

1. Bump the version in `GMS.Core`, `GMS.Desktop`, `GMS.Web` and in `GMS-Installer.nsi`
   (`VERSION` and `PRODUCT_VERSION`).
2. Build, as above.
3. Take the size and checksum:
   ```powershell
   (Get-Item .\GMS-Setup.exe).Length
   (Get-FileHash .\GMS-Setup.exe -Algorithm SHA256).Hash
   ```
4. Copy the file to `wwwroot/releases/GMS-Setup-<version>.exe` **on the server**.
5. Add an entry at the top of `GMS.Web/releases.json` with the version, date, url, size, checksum
   and notes written as what changed for the person using it.

The download page reads `releases.json` at request time, so publishing is copying a file and
editing that JSON. No redeploy.

## The secrets check

`build-installer-nsis.ps1` refuses to build if the publish folder carries a database connection
string, anything under `Platform:Operator`, a Supabase host name or a password hash. The installer
ships the publish folder wholesale, so anything left in there goes to every customer. A note in a
README is not a guard; refusing to build is.

## Files

```
GMS-Installer.nsi          the installer script - version, files, shortcuts, uninstaller
build-installer-nsis.ps1   the secrets pre-flight, then makensis
```

There used to be a parallel WiX/MSI path here (`Product.wxs`, `Setup.wixproj`,
`build-installer.ps1`). It was never the one being used, and every release meant remembering to bump
a version in a file nothing built, so it has been removed. If an MSI is ever genuinely wanted — for
Group Policy deployment, say — start it again from scratch rather than reviving that.
