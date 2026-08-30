# SpatialViewer.CadCore

[中文](README.md) · [日本語](README.ja.md) · [English](README.en.md)

SpatialViewer 向けの独立した 2D CAD ビューアー内核です。本リポジトリでは CAD 読み込みアダプター、CAD セマンティックモデル、ジオメトリ／シーン変換、描画抽象、Windows 描画バックエンド、および回帰テストを管理します。WinUI 3 の製品 UI は `KiYouJyo/SpatialViewer` に残します。

## 原則

- **UI 非依存** — 解析、CAD セマンティクス、ジオメトリ、シーン変換は WinUI のページやコントロールに依存しません。
- **リーダー分離** — ACadSharp はアダプタープロジェクト内に閉じ込め、第三者型を公開 API に漏らしません。
- **意味を保持** — ARC/CIRCLE/ELLIPSE はインポート時に恒久的な折れ線へ変換せず、曲線プリミティブとして保持します。
- **回帰テスト優先** — 色、曲線、ブロック、文字、線種の変更には自動テストを付与します。
- **独立バージョン** — CadCore と SpatialViewer UI は別々にバージョン管理し、明示的な依存リビジョンで統合します。

## リポジトリ境界

本リポジトリを CAD 内核の唯一のソースとして扱います。`SpatialViewer` はアプリケーションシェル、タブ、ツールバー、パネル、ユーザー操作を担当します。

## ライセンス

MIT。第三者ライセンスは `THIRD-PARTY-NOTICES.md` を参照してください。
