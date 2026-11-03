# 音声途切れ詳細診断スクリプト
# 右クリック → PowerShellで実行

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  無線ヘッドホン 途切れ詳細診断" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. 再生デバイス一覧
Write-Host "[1] 再生デバイス一覧" -ForegroundColor Yellow
try {
    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class Audio {
    [DllImport("ole32.dll")]
    public static extern int CoInitialize(IntPtr pvReserved);
}
"@
} catch {}

$devices = Get-CimInstance -ClassName Win32_SoundDevice | Select-Object Name, Status, Manufacturer
$devices | Format-Table -AutoSize

# 2. Bluetooth関連
Write-Host "[2] Bluetooth / ヘッドホン関連デバイス" -ForegroundColor Yellow
Get-PnpDevice | Where-Object {
    $_.FriendlyName -match 'Bluetooth|Headset|Headphone|イヤホン|ヘッドホン|Soundcore|Sony|JBL|AirPods|WH-|WF-'
} | Select-Object FriendlyName, Status, Class | Format-Table -AutoSize

# 3. 現在のデフォルト再生デバイス
Write-Host "[3] 現在のデフォルト再生デバイス" -ForegroundColor Yellow
try {
    $audio = New-Object -ComObject MMDeviceAPI.MMDeviceEnumerator
    $default = $audio.GetDefaultAudioEndpoint(0, 1)  # eRender, eMultimedia
    Write-Host "  名前: $($default.FriendlyName)"
    Write-Host "  状態: $($default.State)"
} catch {
    Write-Host "  取得できませんでした（権限または環境の問題）"
}

# 4. サンプルレート確認のヒント
Write-Host ""
Write-Host "[4] 確認してほしいポイント" -ForegroundColor Yellow
Write-Host "  ・Windowsのサウンド設定で「空間サウンド」がオフになっているか"
Write-Host "  ・ヘッドホンのプロパティ → 詳細 でサンプルレートが 48000Hz になっているか"
Write-Host "  ・Bluetoothの「ハンズフリー」と「ステレオ」が両方有効になっていないか"
Write-Host "    （ハンズフリーが有効だと音質が落ちて途切れやすい）"
Write-Host ""

# 5. SimpleMultiAudio が動いているか
Write-Host "[5] SimpleMultiAudio の状態" -ForegroundColor Yellow
$proc = Get-Process -Name "SimpleMultiAudio" -ErrorAction SilentlyContinue
if ($proc) {
    Write-Host "  起動中です (PID: $($proc.Id))" -ForegroundColor Green
    Write-Host "  メモリ使用量: $([math]::Round($proc.WorkingSet64 / 1MB, 1)) MB"
} else {
    Write-Host "  起動していません" -ForegroundColor Gray
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "診断結果の見方" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "【ヘッドホン側が怪しい場合】"
Write-Host "  ・Bluetooth接続が不安定（壁や距離、2.4GHz帯の干渉）"
Write-Host "  ・「ハンズフリー テレフォニー」が有効になっている"
Write-Host "  ・サンプルレートが 44100Hz や低い値になっている"
Write-Host ""
Write-Host "【ソフト側が怪しい場合】"
Write-Host "  ・SimpleMultiAudioを止めると途切れなくなる"
Write-Host "  ・他のソフト（Voicemeeterや類似ツール）でも同じ症状が出る"
Write-Host ""
Write-Host "何かキーを押すと終了します..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
