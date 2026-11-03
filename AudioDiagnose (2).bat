@echo off
chcp 65001 >nul
title Audio Diagnose - Full
color 0A
cls

echo ========================================
echo   Full Audio Diagnose
echo ========================================
echo.

echo [1] SimpleMultiAudio process
echo ----------------------------------------
tasklist /fi "imagename eq SimpleMultiAudio.exe" 2>nul | find /i "SimpleMultiAudio.exe" >nul
if errorlevel 1 (
    echo   Not running
) else (
    echo   Running
    tasklist /fi "imagename eq SimpleMultiAudio.exe" /fo list | findstr /i "PID Image"
)
echo.

echo [2] Audio devices
echo ----------------------------------------
powershell -NoProfile -Command "Get-CimInstance Win32_SoundDevice | Select-Object Name,Status,Manufacturer | Format-Table -AutoSize"
echo.

echo [3] Bluetooth / Headphone devices
echo ----------------------------------------
powershell -NoProfile -Command "Get-PnpDevice | Where-Object { $_.FriendlyName -match 'Bluetooth|Headset|Headphone|Soundcore|Sony|JBL|WH-|WF-|AirPods|Ear|Buds' } | Select-Object FriendlyName,Status,Class | Format-Table -AutoSize"
echo.

echo [4] Default playback device
echo ----------------------------------------
powershell -NoProfile -Command "try { $e = New-Object -ComObject MMDeviceAPI.MMDeviceEnumerator; $d = $e.GetDefaultAudioEndpoint(0,1); Write-Host ('  Name: ' + $d.FriendlyName); Write-Host ('  State: ' + $d.State) } catch { Write-Host '  Could not get default device' }"
echo.

echo [5] Bluetooth services (Hands-Free check)
echo ----------------------------------------
powershell -NoProfile -Command "Get-PnpDevice | Where-Object { $_.FriendlyName -match 'Hands-Free|HF|AG|テレフォニー|Headset' } | Select-Object FriendlyName,Status | Format-Table -AutoSize"
echo.

echo [6] Network adapters (Wi-Fi interference check)
echo ----------------------------------------
powershell -NoProfile -Command "Get-NetAdapter | Where-Object { $_.Status -eq 'Up' } | Select-Object Name,InterfaceDescription,LinkSpeed | Format-Table -AutoSize"
echo.

echo ========================================
echo   How to read results
echo ========================================
echo.
echo - If SimpleMultiAudio is Running and audio stutters only then
echo   -^> Software side is suspicious
echo.
echo - If Hands-Free / HF / テレフォニー is present and Enabled
echo   -^> Disable Hands-Free Telephony (common cause of stutter)
echo.
echo - If Bluetooth device Status is not OK
echo   -^> Headphone connection problem
echo.
echo - Wi-Fi and Bluetooth both use 2.4GHz
echo   -^> Close distance to router can cause dropouts
echo.
echo ========================================
echo Press any key to exit...
pause >nul
