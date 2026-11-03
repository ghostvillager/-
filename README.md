# SimpleMultiAudio Fixed（音割れ対策版）

元リポジトリ: https://github.com/ghostvillager/-

## 修正内容
- レイテンシを 80ms → **200ms** に変更
- バッファを 300ms → **1000ms** に拡大
- Bluetooth無線ヘッドホンとの同時再生時に出やすいビープ音・音割れを軽減

## ビルド方法
1. .NET 8 SDK が入っている環境で
2. このフォルダで以下を実行：

```bash
dotnet build -c Release
```

3. 生成された `bin\Release\net8.0-windows\SimpleMultiAudio.exe` を実行

## 必要なもの
- Windows 10 / 11
- .NET 8 Desktop Runtime（なければ起動時に促されます）

## 使い方
元のツールと同じです。
無線ヘッドホンとPCスピーカーの両方にチェックを入れて「開始」を押してください。
