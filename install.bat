@echo off
setlocal
set "INSTALL_DIR=%LOCALAPPDATA%\VDScrollSwitch"
set "EXE_NAME=VDScrollSwitch.exe"

echo Installing VDScrollSwitch...

if not exist "%~dp0%EXE_NAME%" (
    echo ERROR: %EXE_NAME% not found next to this installer.
    pause
    exit /b 1
)

if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"

taskkill /F /IM %EXE_NAME% >nul 2>&1

copy /Y "%~dp0%EXE_NAME%" "%INSTALL_DIR%\%EXE_NAME%" >nul
if errorlevel 1 (
    echo ERROR: failed to copy exe to "%INSTALL_DIR%".
    pause
    exit /b 1
)

set /p AUTOSTART="Start with Windows automatically? (Y/N): "
if /i "%AUTOSTART%"=="Y" (
    reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v VDScrollSwitch /t REG_SZ /d "%INSTALL_DIR%\%EXE_NAME%" /f >nul
    echo Autostart enabled.
)

start "" "%INSTALL_DIR%\%EXE_NAME%"

echo.
echo Done. VDScrollSwitch is running in the system tray.
echo Hold Alt and scroll to switch virtual desktops.
pause
