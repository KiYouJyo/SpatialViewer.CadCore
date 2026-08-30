# コントリビューションガイド

[中文](CONTRIBUTING.md) · [日本語](CONTRIBUTING.ja.md) · [English](CONTRIBUTING.en.md)

## 開発ルール

1. 内核コードから SpatialViewer の WinUI ページ、コントロール、アプリライフサイクルへ依存しないでください。
2. 第三者 CAD リーダー型は各 Adapter プロジェクト内に限定します。
3. 色、曲線／円弧、ブロック、文字、線種、線幅の変更には回帰テストが必要です。
4. `TreatWarningsAsErrors=true` を維持し、マージ前に Release ビルドと全テストを実行してください。
5. 公開 API の破壊的変更は移行方法を PR に記載し CHANGELOG を更新してください。
