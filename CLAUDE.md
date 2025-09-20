# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 会話ガイドライン

- トークン節約のため思考は英語で行うが回答は日本語で
- いい質問ですね！等の私に対する共感は不要です

## プロジェクト概要

このプロジェクトは自作音楽ゲーム用7鍵盤コントローラー（Nakuru Controller）の設定ツールです。Teensy4.0マイコンと磁気スイッチを使用したコントローラーの各種設定を行うuno platformアプリケーションです。

### プロジェクト状態
- **優先順位**: Windows対応 → Android/Linux対応
- **開発方針**: 段階的実装

## アーキテクチャ

### フォルダ構造
```
NakuruContorller_uno/
├── NakuruController/
│   ├── マイコン/
│   │   └── 7kContoroller.cpp    # Teensy4.0用ファームウェア
│   └── NakuruController_Driver/  # Uno Platformアプリケーション
│       └── NakuruController_Driver/
│           ├── Presentation/     # UIレイヤー (MVUX)
│           ├── Models/          # データモデル
│           ├── Services/        # ビジネスロジック
│           └── Platforms/       # プラットフォーム固有実装
├── 要求仕様.md                   # 機能要件定義
└── ハードウェア.md               # ハードウェア仕様
```

### 技術スタック
- **UI Framework**: Uno Platform (Uno.Sdk)
- **Target Framework**: net9.0-desktop
- **状態管理**: MVUX
- **アーキテクチャパターン**: MVUX + C# Markup + DI + Feature Sliced
- **通信**: シリアル通信
- **Uno Features**: CSharpMarkup, Material, Hosting, Toolkit, Logging, MVUX, Configuration, HttpKiota, Serialization, Localization, Navigation, ThemeService, SkiaRenderer

## ビルド・実行コマンド

### Uno Platformアプリケーション

```bash
# ビルド（Windows Desktop）
cd NakuruController/NakuruController_Driver
dotnet build NakuruController_Driver/NakuruController_Driver.csproj -f net9.0-desktop

# 実行
dotnet run --project NakuruController_Driver/NakuruController_Driver.csproj -f net9.0-desktop

# デバッグ（VS Code）
# F5キーまたは "Uno Platform Desktop Debug" 構成を使用

# パブリッシュ
dotnet publish NakuruController_Driver/NakuruController_Driver.csproj -f net9.0-desktop
```

## マイコン通信プロトコル

### コマンド (Uno → Teensy)
- `START_ANALOG\n` - アナログ値出力開始
- `STOP_ANALOG\n` - アナログ値出力停止
- `HEARTBEAT\n` - ハートビート（1秒ごと送信必須）

### レスポンス (Teensy → Uno)
```json
{
  "type": "analog_values",
  "timestamp": 12345,
  "keys": [
    {"id": 0, "ad": 512, "pressed": false},
    ...
  ]
}
```
- 10ms周期で自動送信
- 10秒間ハートビートが途切れると自動停止

## MVUX パターンの重要事項

### ViewModel の自動生成
- `partial record` として定義されたModelクラスから自動的にViewModelが生成される
- **重要**: DataContextには自動生成されたViewModelを使用する（例: `MainViewModel`、`MainModel`ではなく）
- IState<T>プロパティはMVUXが自動的にバインディング可能な形式に変換

### Navigation実装
- Frame-based navigationを使用（RoutingではなくFrame.Navigate()）
- NavigationViewのSelectionChangedイベントでページ切り替えを処理
- App.xaml.csでViewMapを登録することが必須

## 実装フェーズ

### Phase 1: アナログ値出力機能 ✅
- マイコンからのリアルタイムアナログ値送信
- ハートビート機能による接続管理

### Phase 2: NakuruController_Driver 基本機能 🚧
1. ✅ シリアル通信の確立
2. 🚧 アナログ値リアルタイム表示（ItemsRepeater内でのIStateバインディングに課題）
3. ⬜ グラフ描画機能

### Phase 3: 設定機能
1. キーアサイン機能
2. アクチュエーションポイント設定
3. 設定の永続化

### Phase 4: 拡張機能
1. ラピッドトリガー対応
2. 多言語対応
3. クロスプラットフォーム対応

## トラブルシューティング

### COMポートが認識されない場合
1. Arduino IDEで **Tools → USB Type** を **"Serial + Keyboard + Mouse + Joystick"** に設定
2. Teensy4.0にファームウェアを再アップロード
3. Arduino IDEのシリアルモニタが開いている場合は閉じる

### シリアル通信のテスト
```bash
# COMポート確認用テストプログラムの実行
cd C:/Users/rice5
dotnet run --project TestSerial/TestSerial.csproj
```

### ページナビゲーションが機能しない場合
1. App.xaml.csでViewMapが正しく登録されているか確認
2. Shell.csでNavigationViewItemのTagにTypeが設定されているか確認
3. Frame.Navigate()でページ遷移を実装しているか確認