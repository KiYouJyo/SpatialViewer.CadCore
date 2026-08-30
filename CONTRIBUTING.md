# 贡献指南

[中文](CONTRIBUTING.md) · [日本語](CONTRIBUTING.ja.md) · [English](CONTRIBUTING.en.md)

感谢参与 SpatialViewer.CadCore。

## 开发约束

1. 内核代码不得依赖 SpatialViewer 的 WinUI 页面、控件或应用生命周期。
2. 第三方 CAD 读取器类型只能存在于对应 Adapter 项目中。
3. 修改颜色解析、圆弧/曲线、块、文字、线型、线宽时必须补充回归测试。
4. 保持 `TreatWarningsAsErrors=true`；提交前运行 Release 构建与全部测试。
5. 公共 API 的破坏性变更必须在 PR 中写明迁移方式并更新 CHANGELOG。

## PR

优先使用小而可验证的 PR。内核正确性修复应附最小测试夹具或可复现单元测试。
