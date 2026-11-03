@echo off
chcp 65001 >nul
title Audio Diagnose
color 0A

:MENU
cls
echo ========================================
echo   Wireless Headphone Diagnose Tool
echo ========================================
echo.
echo [1] Show audio devices
echo [2] Show Bluetooth devices
echo [3] Kill SimpleMultiAudio
echo [4] Play test sound (10 sec)
echo [5] Exit
echo.
set /p choice=Select number: 

if "%choice%"=="1" goto DEVICES
if "%choice%"=="2" goto BLUETOOTH
if "%choice%"=="3" goto KILL
if "%choice%"=="4" goto TEST
if "%choice%"=="5" exit
goto MENU

:DEVICES
echo.
echo ===== Audio Devices =====
powershell -NoProfile -Command "Get-CimInstance Win32_SoundDevice | Select-Object Name,Status,Manufacturer | Format-Table -AutoSize"
echo.
pause
goto MENU

:BLUETOOTH
echo.
echo ===== Bluetooth / Headphone Devices =====
powershell -NoProfile -Command "Get-PnpDevice | Where-Object { $_.FriendlyName -match 'Bluetooth|Headset|Headphone|Soundcore|Sony|JBL|WH-|WF-|AirPods' } | Select-Object FriendlyName,Status,Class | Format-Table -AutoSize"
echo.
pause
goto MENU

:KILL
echo.
echo Killing SimpleMultiAudio...
taskkill /f /im SimpleMultiAudio.exe 2>nul
if errorlevel 1 (
    echo SimpleMultiAudio is not running.
) else (
    echo Killed.
)
echo.
pause
goto MENU

:TEST
echo.
echo ===== Sound Test =====
echo Play test sound for about 10 seconds.
echo - If it stutters now = Headphone or Windows problem
echo - If it is fine now but stutters only with SimpleMultiAudio = Software problem
echo.
echo Press any key to start...
pause >nul
echo.
echo Playing...
powershell -NoProfile -Command "[console]::beep(440,800); Start-Sleep -m 300; [console]::beep(523,800); Start-Sleep -m 300; [console]::beep(659,800); Start-Sleep -m 300; [console]::beep(784,800); Start-Sleep -m 300; [console]::beep(880,1500)"
echo.
echo Test finished. Did it stutter?
echo.
pause
goto MENU
