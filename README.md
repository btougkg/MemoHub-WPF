# MemoHub

📝 Markdown対応のモダンなメモ管理アプリケーション

## 概要

MemoHubは、.NET WPFで構築されたMarkdown形式のメモ管理アプリケーションです。直感的なUIと強力な検索機能、リアルタイムプレビュー、類似メモ検出など、効率的なメモ管理をサポートします。

## 主な機能

### ✨ 基本機能

- **Markdownサポート**: メモをMarkdown形式で保存・表示
- **リアルタイムプレビュー**: 編集中のメモをリアルタイムでHTMLプレビュー
- **高速検索**: タイトルと内容を対象とした全文検索
- **SQLiteデータベース**: 軽量で高速なローカルデータベース

### 🔗 自動リンク機能

- **URLの自動リンク化**: HTTP/HTTPSのURLを自動的にクリック可能なリンクに変換
- **UNCパス対応**: `\\server\share\path` 形式のネットワークパスをリンク化（日本語パス対応）
- **Windowsパス対応**: `C:\folder\file` 形式のローカルパスをリンク化（日本語パス対応）

### 🎯 便利機能

- **類似メモ検出**: 新規保存時に類似したメモを自動検出し、重複防止をサポート
- **コードブロック表示**: メモ内容を見やすいコードブロック形式で表示
- **ワンクリックコピー**: ホバー時に表示されるコピーボタンで簡単にコピー
- **ボタン表示切り替え**: 検索結果のアクションボタンの表示/非表示を切り替え可能
- **設定の永続化**: ボタン表示設定などをデータベースに保存

### ⌨️ ショートカットキー

- **Ctrl+N**: 新規メモ作成
- **Ctrl+S**: 検索実行
- **Ctrl+R**: リセット（初期表示に戻る）
- **Enter**: 検索ボックスでEnterキーを押して検索実行

### 🎨 モダンなUI

- **マスター・詳細パターン**: 左側にメモ一覧、右側に詳細表示
- **レスポンシブデザイン**: ウィンドウサイズに応じて自動調整
- **ダークテーマ対応**: 見やすい配色とフォント
- **ホバーエフェクト**: マウスオーバー時の視覚的フィードバック

## システム要件

- **OS**: Windows 10/11
- **.NET**: .NET 10.0以降
- **WebView2 Runtime**: Microsoft Edge WebView2 Runtime（通常はWindows 11に標準搭載）

## インストール方法

### 開発環境でのビルド

1. リポジトリをクローン:

```bash
git clone <repository-url>
cd MemoHub
```

2. プロジェクトをビルド:

```bash
dotnet build
```

3. アプリケーションを実行:

```bash
dotnet run
```

### 配布用パッケージの作成

自己完結型の実行ファイルを作成する場合:

```bash
dotnet publish --configuration Release --self-contained --runtime win-x64
```

ビルド成果物は `bin\Release\net10.0-windows\win-x64\publish\` に生成されます。

## 使い方

### 1. メモの作成

- 「＋ 新規メモ」ボタンをクリック、または **Ctrl+N**
- タイトルと内容を入力
- 「💾 保存」ボタンで保存

### 2. メモの検索

- ヘッダーの検索ボックスにキーワードを入力
- 「🔍 検索」ボタンをクリック、または **Ctrl+S** / **Enter**
- タイトルと内容の両方から検索

### 3. メモの編集

- 一覧からメモを選択
- 「✏️ 編集」ボタンをクリック
- 編集後、「💾 更新」ボタンで保存

### 4. メモの削除

- 一覧からメモを選択
- 「🗑️ 削除」ボタンをクリック
- 確認ダイアログで「はい」を選択

### 5. リンクの利用

メモに以下の形式でパスやURLを記載すると、自動的にクリック可能なリンクになります：

- **HTTPSリンク**: `https://example.com`
- **UNCパス**: `\\server\share\folder\file.txt`
- **Windowsパス**: `C:\Users\username\Documents\file.txt`
- **日本語パス**: `\\server\共有フォルダ\ファイル.txt`

### 6. ボタン表示の切り替え

- ヘッダー右側の「👁️‍🗨️ ボタン非表示」/「👁️ ボタン表示」ボタンをクリック
- 検索結果のコピー・編集・削除ボタンの表示を切り替え
- 設定は自動的に保存され、次回起動時に復元

## データの保存場所

メモデータは `data\memohub.db` に保存されます（SQLiteデータベース）。
このファイルをバックアップすることで、全てのメモを保存できます。

## プロジェクト構成

```
MemoHub/
├── MainWindow.xaml          # メインウィンドウのUI定義
├── MainWindow.xaml.cs       # メインウィンドウのロジック
├── Db.cs                    # データベースアクセス層
├── MemoHub.csproj           # プロジェクト設定ファイル
├── memo_icon.ico            # アプリケーションアイコン
├── data/
│   └── memohub.db          # SQLiteデータベースファイル
└── README.md               # このファイル
```

## 技術スタック

- **.NET 10.0-windows**: WPFアプリケーションフレームワーク
- **WPF (Windows Presentation Foundation)**: UIフレームワーク
- **Microsoft.Web.WebView2**: HTMLレンダリングとプレビュー
- **Microsoft.Data.Sqlite**: SQLiteデータベース接続
- **Markdig**: Markdownパーサー（参照のみ、現在はプレーンテキスト+HTMLエンコード方式を採用）

## データベーススキーマ

### memosテーブル

```sql
CREATE TABLE memos (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    title TEXT NOT NULL,
    content TEXT NOT NULL,
    updated_at TEXT NOT NULL
);
CREATE INDEX idx_memos_updated_at ON memos(updated_at DESC);
```

### settingsテーブル

```sql
CREATE TABLE settings (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);
```

## ライセンス

このプロジェクトは個人利用目的で開発されています。

## トラブルシューティング

### WebView2が動作しない場合

Microsoft Edge WebView2 Runtimeをインストールしてください：
https://developer.microsoft.com/en-us/microsoft-edge/webview2/

### データベースファイルが見つからない場合

初回起動時に自動的に `data\memohub.db` が作成されます。
作成されない場合は、アプリケーションに書き込み権限があるか確認してください。

### 日本語のUNCパスがリンクにならない場合

アプリケーションを再起動してみてください。
それでも解決しない場合は、パスに特殊文字が含まれていないか確認してください。

## 今後の機能拡張案

- [ ] タグ機能
- [ ] カテゴリー分類
- [ ] メモのエクスポート（Markdown/HTML/PDF）
- [ ] 複数データベースのサポート
- [ ] クラウド同期機能
- [ ] テーマのカスタマイズ
- [ ] 添付ファイルのサポート

## お問い合わせ

バグ報告や機能リクエストは、GitHubのIssuesページでお願いします。

---

**MemoHub** - シンプルで強力なMarkdownメモアプリ 📝
