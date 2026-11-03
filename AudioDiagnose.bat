@echo off
chcp 65001 >nul
title 音声途切れ診断ツール
color 0A

echo ========================================
echo   無線ヘッドホン 途切れ診断ツール
echo ========================================
echo.
echo このツールで「ヘッドホン側の問題」か「ソフト側の問題」かを切り分けます。
echo.

:MENU
echo.
echo [1] デバイス情報を表示する
echo [2] サンプルレート・ビット深度を確認する
echo [3] Bluetooth接続状態を確認する
echo [4] 途切れテスト（システム音を10秒再生）
echo [5] 現在動いているSimpleMultiAudioを強制終了
echo [6] 終了
echo.
set /p choice=番号を選んでください: 

if "%choice%"=="1" goto DEVICES
if "%choice%"=="2" goto FORMAT
if "%choice%"=="3" goto BLUETOOTH
if "%choice%"=="4" goto TEST
if "%choice%"=="5" goto KILL
if "%choice%"=="6" exit
goto MENU

:DEVICES
echo.
echo ===== 再生デバイス一覧 =====
powershell -Command "Get-AudioDevice -List | Format-Table Name, Type, Default, ID -AutoSize" 2>nul
if errorlevel 1 (
    echo AudioDeviceCmdlets が入っていないため、別の方法で表示します...
    powershell -Command "Get-WmiObject Win32_SoundDevice | Select-Object Name, Status | Format-Table -AutoSize"
)
echo.
pause
goto MENU

:FORMAT
echo.
echo ===== 現在のデフォルト再生デバイスのフォーマット =====
powershell -Command ^
"$dev = (New-Object -ComObject MMDeviceAPI.MMDeviceEnumerator).GetDefaultAudioEndpoint(0,1); ^
 $prop = $dev.Properties; ^
 Write-Host 'デバイス名:' $dev.FriendlyName; ^
 Write-Host '状態:' $dev.State"
echo.
echo ※ より詳しい情報は「サウンド設定」→「デバイスのプロパティ」→「追加のデバイスのプロパティ」→「詳細」タブで確認できます。
echo.
pause
goto MENU

:BLUETOOTH
echo.
echo ===== Bluetooth デバイス状態 =====
powershell -Command "Get-PnpDevice -Class Bluetooth | Where-Object {$_.FriendlyName -like '*head*' -or $_.FriendlyName -like '*headset*' -or $_.FriendlyName -like '*ear*' -or $_.Status -eq 'OK'} | Select-Object FriendlyName, Status, InstanceId | Format-Table -AutoSize"
echo.
echo ===== 無線デバイスの接続状態 =====
powershell -Command "Get-PnpDevice | Where-Object {$_.FriendlyName -match 'Bluetooth|Headset|Headphone|イヤホン|ヘッドホン'} | Select-Object FriendlyName, Status | Format-Table -AutoSize"
echo.
pause
goto MENU

:TEST
echo.
echo ===== 途切れテスト =====
echo.
echo 今からシステム音を10秒間再生します。
echo ・この間に途切れる → ヘッドホン or Windows側の問題の可能性大
echo ・この間は途切れないのにSimpleMultiAudio使用時だけ途切れる → ソフト側の可能性大
echo.
echo 準備ができたら何かキーを押してください...
pause >nul
echo.
echo 再生中...（10秒）
powershell -Command "[console]::beep(440,1000); Start-Sleep -Milliseconds 500; [console]::beep(523,1000); Start-Sleep -Milliseconds 500; [console]::beep(659,1000); Start-Sleep -Milliseconds 500; [console]::beep(784,1000); Start-Sleep -Milliseconds 500; [console]::beep(880,2000)"
echo.
echo テスト終了。途切れましたか？
echo.
pause
goto MENU

:KILL
echo.
echo SimpleMultiAudio を強制終了します...
taskkill /f /im SimpleMultiAudio.exe 2>nul
if errorlevel 1 (
    echo SimpleMultiAudio は起動していませんでした。
) else (
    echo 終了しました。
)
echo.
pause
goto MENU
