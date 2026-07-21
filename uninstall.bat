@echo off
setlocal
set "INSTALL_DIR=%LOCALAPPDATA%\VDScrollSwitch"
set "EXE_NAME=VDScrollSwitch.exe"

echo Uninstalling VDScrollSwitch...

taskkill /F /IM %EXE_NAME% >nul 2>&1
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v VDScrollSwitch /f >nul 2>&1
rmdir /S /Q "%INSTALL_DIR%" >nul 2>&1
del /Q "%APPDATA%\Microsoft\Windows\Start Menu\Programs\VDScrollSwitch.lnk" >nul 2>&1
del /Q "%USERPROFILE%\Desktop\VDScrollSwitch.lnk" >nul 2>&1

echo Done. VDScrollSwitch removed.
pause
