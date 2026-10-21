Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!include "WinVer.nsh"

!ifndef VERSION
!error "VERSION, PAYLOAD, SOURCE and OUTPUT must be supplied by build-installer.ps1"
!endif

Name "蓝斗篷启动"
OutFile "${OUTPUT}"
InstallDir "$LOCALAPPDATA\Programs\BlueCloakLauncher"
InstallDirRegKey HKCU "Software\BlueCloakLauncher" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
ShowInstDetails show
ShowUninstDetails show
VIProductVersion "${VERSION}.0"
VIAddVersionKey /LANG=2052 "ProductName" "蓝斗篷启动安装程序"
VIAddVersionKey /LANG=2052 "FileDescription" "蓝斗篷启动安装程序"
VIAddVersionKey /LANG=2052 "FileVersion" "${VERSION}"
VIAddVersionKey /LANG=2052 "ProductVersion" "${VERSION}"
VIAddVersionKey /LANG=2052 "LegalCopyright" "Blue Cloak Launcher contributors"
!define MUI_ICON "${SOURCE}\LumaDock.ico"
!define MUI_UNICON "${SOURCE}\LumaDock.ico"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TEXT "安装蓝斗篷启动，按你的分组快速打开应用、文件夹和网站。$\r$\n$\r$\n默认仅为当前用户安装，无需管理员权限。升级前请从系统托盘退出旧版。$\r$\n$\r$\n程序不会覆盖已有分组数据；从便携版迁移时，请先导出分组备份，安装后再导入。"
!define MUI_FINISHPAGE_RUN "$INSTDIR\蓝斗篷启动.exe"
!define MUI_FINISHPAGE_RUN_TEXT "启动蓝斗篷启动"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "${SOURCE}\LICENSE"
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH
!insertmacro MUI_LANGUAGE "SimpChinese"

Function .onInit
  SetShellVarContext current
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "此安装包需要 64 位 Windows 10 或 Windows 11。" /SD IDOK
    Abort
  ${EndIf}
  ${IfNot} ${AtLeastWin10}
    MessageBox MB_ICONSTOP "此安装包需要 Windows 10 或 Windows 11。" /SD IDOK
    Abort
  ${EndIf}
  SetRegView 64
  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
  ${If} $0 < 528040
    MessageBox MB_ICONSTOP "请先安装 Microsoft .NET Framework 4.8，再运行本安装程序。Windows 11 通常已自带。官方下载：https://dotnet.microsoft.com/download/dotnet-framework/net48" /SD IDOK
    Abort
  ${EndIf}
  System::Call 'kernel32::OpenMutexW(i 0x100000, i 0, w "LumaDock.Desktop.Singleton") p.r0'
  ${If} $0 != 0
    System::Call 'kernel32::CloseHandle(p r0)'
    MessageBox MB_ICONEXCLAMATION "蓝斗篷启动正在运行。请先右键系统托盘里的软件图标，选择退出，然后重新安装。" /SD IDOK
    Abort
  ${EndIf}
FunctionEnd

Section "蓝斗篷启动（必选）" MainProgram
  SectionIn RO
  SetOutPath "$INSTDIR"
  File "${PAYLOAD}\蓝斗篷启动.exe"
  File "${PAYLOAD}\蓝斗篷启动.exe.config"
  File "${PAYLOAD}\LICENSE"
  File "${PAYLOAD}\THIRD_PARTY_NOTICES.md"
  File "${PAYLOAD}\使用说明.md"
  File "${PAYLOAD}\NSIS-LICENSE.txt"
  SetOutPath "$INSTDIR\assets\fonts"
  File "${PAYLOAD}\assets\fonts\NotoSansSC-Regular.ttf"
  File "${PAYLOAD}\assets\fonts\NotoSansSC-Semibold.ttf"
  File "${PAYLOAD}\assets\fonts\OFL.txt"
  SetOutPath "$INSTDIR"
  WriteUninstaller "$INSTDIR\卸载蓝斗篷启动.exe"
  WriteRegStr HKCU "Software\BlueCloakLauncher" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\BlueCloakLauncher" "DisplayName" "蓝斗篷启动"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\BlueCloakLauncher" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\BlueCloakLauncher" "Publisher" "Blue Cloak Launcher contributors"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\BlueCloakLauncher" "DisplayIcon" "$INSTDIR\蓝斗篷启动.exe,0"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\BlueCloakLauncher" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\BlueCloakLauncher" "UninstallString" '"$INSTDIR\卸载蓝斗篷启动.exe"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\BlueCloakLauncher" "QuietUninstallString" '"$INSTDIR\卸载蓝斗篷启动.exe" /S'
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\BlueCloakLauncher" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\BlueCloakLauncher" "NoRepair" 1
  CreateDirectory "$SMPROGRAMS\蓝斗篷启动"
  CreateShortcut "$SMPROGRAMS\蓝斗篷启动\蓝斗篷启动.lnk" "$INSTDIR\蓝斗篷启动.exe" "" "$INSTDIR\蓝斗篷启动.exe" 0
  CreateShortcut "$SMPROGRAMS\蓝斗篷启动\卸载蓝斗篷启动.lnk" "$INSTDIR\卸载蓝斗篷启动.exe"
SectionEnd

Section "创建桌面快捷方式" DesktopShortcut
  CreateShortcut "$DESKTOP\蓝斗篷启动.lnk" "$INSTDIR\蓝斗篷启动.exe" "" "$INSTDIR\蓝斗篷启动.exe" 0
SectionEnd

Function un.onInit
  SetShellVarContext current
  SetRegView 64
  System::Call 'kernel32::OpenMutexW(i 0x100000, i 0, w "LumaDock.Desktop.Singleton") p.r0'
  ${If} $0 != 0
    System::Call 'kernel32::CloseHandle(p r0)'
    MessageBox MB_ICONEXCLAMATION "请先从系统托盘退出蓝斗篷启动，再卸载。" /SD IDOK
    Abort
  ${EndIf}
FunctionEnd

Section "Uninstall"
  IfFileExists "$INSTDIR\蓝斗篷启动.exe" 0 cleanup_done
  ExecWait '"$INSTDIR\蓝斗篷启动.exe" --uninstall-cleanup' $0
  ${If} $0 != 0
    MessageBox MB_ICONEXCLAMATION "开机启动项清理未完成。请打开软件，在设置中关闭开机启动并退出后重试。" /SD IDOK
    Abort
  ${EndIf}
cleanup_done:
  Delete "$DESKTOP\蓝斗篷启动.lnk"
  Delete "$SMPROGRAMS\蓝斗篷启动\蓝斗篷启动.lnk"
  Delete "$SMPROGRAMS\蓝斗篷启动\卸载蓝斗篷启动.lnk"
  RMDir "$SMPROGRAMS\蓝斗篷启动"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\BlueCloakLauncher"
  DeleteRegValue HKCU "Software\BlueCloakLauncher" "InstallDir"
  DeleteRegKey /ifempty HKCU "Software\BlueCloakLauncher"
  ; Deliberate allow-list: never recursively delete the selected installation folder.
  Delete "$INSTDIR\蓝斗篷启动.exe"
  Delete "$INSTDIR\蓝斗篷启动.exe.config"
  Delete "$INSTDIR\LICENSE"
  Delete "$INSTDIR\THIRD_PARTY_NOTICES.md"
  Delete "$INSTDIR\使用说明.md"
  Delete "$INSTDIR\NSIS-LICENSE.txt"
  Delete "$INSTDIR\assets\fonts\NotoSansSC-Regular.ttf"
  Delete "$INSTDIR\assets\fonts\NotoSansSC-Semibold.ttf"
  Delete "$INSTDIR\assets\fonts\OFL.txt"
  RMDir "$INSTDIR\assets\fonts"
  RMDir "$INSTDIR\assets"
  Delete "$INSTDIR\卸载蓝斗篷启动.exe"
  RMDir "$INSTDIR"
SectionEnd
