# SpatialViewer.CadCore

[中文](README.md) · [日本語](README.ja.md) · [English](README.en.md)

SpatialViewer 的独立 CAD 二维看图内核仓库。这里维护 CAD 文件读取适配、CAD 语义模型、几何/场景转换、渲染抽象与回归测试；WinUI 3 产品界面保留在 `KiYouJyo/SpatialViewer`。

> 当前阶段：从 `SpatialViewer` 中剥离既有 CAD 内核，并保持现有 API/命名空间尽可能稳定，先建立独立构建与测试边界，再逐步演进颜色、圆弧/曲线精度和更多 CAD 实体支持。

## 设计原则

- **UI 无关**：核心解析、模型、几何与场景转换不得依赖 WinUI 3 页面或控件。
- **读取器隔离**：ACadSharp 仅存在于适配项目中，不向上层公开第三方类型。
- **语义优先**：ARC/CIRCLE/ELLIPSE 等保持曲线语义，不在导入阶段永久离散为折线。
- **可回归**：颜色、曲线、块、文字、线型等修改必须由单元测试/夹具覆盖。
- **独立版本**：内核与 SpatialViewer UI 分别版本化，后续由明确依赖版本进行集成。

## 仓库边界

本仓库是 CAD 内核的唯一源代码归属。`SpatialViewer` 负责窗口、标签页、工具栏、面板和用户交互，只通过稳定接口使用本仓库提供的能力。

## 许可证

MIT License。第三方依赖及许可信息见 `THIRD-PARTY-NOTICES.md`。
