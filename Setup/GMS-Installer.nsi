; NSIS Installer Script for GMS Desktop
; Simple and straightforward Windows installer

!include "MUI2.nsh"

; Basic Settings
Name "General Management System"
OutFile "..\GMS-Setup.exe"
InstallDir "$PROGRAMFILES\General Management System"
InstallDirRegKey HKLM "Software\GMS" "Install_Dir"

; Request admin privileges
RequestExecutionLevel admin

; MUI Settings
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_LANGUAGE "English"

; Installer sections
Section "Install"
  SetOutPath "$INSTDIR"

  ; Copy all files from publish folder
  File /r "..\publish\*.*"

  ; Create uninstaller
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  ; Create Start Menu shortcuts
  CreateDirectory "$SMPROGRAMS\General Management System"
  CreateShortcut "$SMPROGRAMS\General Management System\General Management System.lnk" "$INSTDIR\GMS.Desktop.exe"
  CreateShortcut "$SMPROGRAMS\General Management System\Uninstall.lnk" "$INSTDIR\Uninstall.exe"

  ; Create Desktop shortcut
  CreateShortcut "$DESKTOP\General Management System.lnk" "$INSTDIR\GMS.Desktop.exe"

  ; Write registry for uninstall
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\GMS" "DisplayName" "General Management System"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\GMS" "UninstallString" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "Software\GMS" "Install_Dir" "$INSTDIR"
SectionEnd

; Uninstaller section
Section "Uninstall"
  ; Remove files
  RMDir /r "$INSTDIR"

  ; Remove shortcuts
  RMDir /r "$SMPROGRAMS\General Management System"
  Delete "$DESKTOP\General Management System.lnk"

  ; Remove registry
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\GMS"
  DeleteRegKey HKLM "Software\GMS"
SectionEnd
