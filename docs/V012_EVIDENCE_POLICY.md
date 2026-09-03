# v0.12 Tianzheng semantic evidence policy

This document defines the evidence strength used for the final SpatialViewer.CadCore v0.12 Tianzheng semantic blockers. It does not reduce the release gate in `V012_TIANZHENG_ARCHITECTURE.md`.

## 中文

### 为什么分级

最后三个 blocker（`TCH_AXIS_LABEL`、`TCH_DRAWINGINDEX` / `TCH_INDEXPOINTER`、`TCH_DIMENSION2`）已经不缺类型识别、raw payload、Proxy Graphics 或 A/B 工具，真正缺的是**可信的 raw field → semantic 映射**。

公开资料常见两种强度，必须严格区分：

1. **ParameterExistence**：只能证明某个对象确实具有某项可编辑属性，例如“轴号文字”“索引文字”“标注出图比例”。这类资料不能推出具体 DXF group。
2. **RawFieldMapping**：公开 `entget` / `assoc` / `entmod`、可验证样本或同等强度资料直接把该属性绑定到明确的 DXF group code + occurrence。只有这一等级才能与 `TCHDIFF` consensus 合并，形成 semantic implementation candidate。

### 解除 gate 的最小条件

单个字段要进入 named semantic，至少需要：

- canonical `TCHPLAN` case；
- `TCHSIG` 一致的结构签名；
- 至少 2 组独立、单变量 `TCHDIFF` observation；
- stable candidate；
- 至少一条与该 stable candidate 的 `code + occurrence` 精确一致的 **RawFieldMapping** 外部证据；
- 实际 CadCore decoder；
- real Reader regression；
- malformed / duplicate / truncated / identity mismatch 等 fail-closed 回归。

`ParameterExistence`、跨类类推、Proxy Graphics、对象特性面板截图、仅有类型名、单次 A/B observation 均不能单独解除 gate。

### 当前新增证据：TCH_DIMENSION2

2026 年公开的明经 CAD AutoLISP 帖明确描述了对 `TCH_DIMENSION2` **标注出图比例**的动态调整，并将其作为专门的旧版实现引用。该资料足以把 `DIMENSION_PLOT_SCALE` 的参数存在性提升为 **ParameterExistence**，但当前搜索索引没有暴露其具体 `entget/entmod` group 代码，因此**仍不是 RawFieldMapping**，不能据此把 group 47 或其他 group 直接命名为 `PlotScale`。

这与既有证据并不冲突：多个 `TDbEntity` 派生标注对象以及公开 `TCH_ELEVATION` 代码确实反复使用 group 47 作为出图比例，但在 `TCH_DIMENSION2` 自身字段证据出现前，跨类类推仍保持 research hypothesis。

## English

### Evidence grades

The final three blockers no longer lack object identification, raw capture, proxy fallback or A/B tooling. They lack trustworthy raw-field naming evidence.

- **ParameterExistence** confirms that a property exists on the exact Tianzheng object type. It cannot identify a DXF group.
- **RawFieldMapping** directly binds that property to a specific DXF group code and occurrence through public `entget` / `assoc` / `entmod`, a verifiable sample, or evidence of equivalent strength.

A named semantic requires a canonical experiment case, matching `TCHSIG`, at least two independent single-variable `TCHDIFF` observations, a stable candidate, matching RawFieldMapping evidence, a real decoder, a real Reader regression, and fail-closed negative tests.

A public 2026 AutoLISP post explicitly describes dynamically changing the plot/output scale of `TCH_DIMENSION2`. This upgrades `DIMENSION_PLOT_SCALE` to **ParameterExistence** evidence only. The indexed material does not expose the raw group used by that implementation, so it does not justify naming group 47 (or any other group) as the `TCH_DIMENSION2` plot scale.

## 日本語

### evidence grade

残り 3 blocker で不足しているのは object type、raw capture、Proxy Graphics、A/B tooling ではなく、信頼できる raw-field naming evidence です。

- **ParameterExistence**：対象の Tianzheng object type にその property が存在することだけを確認します。DXF group は特定できません。
- **RawFieldMapping**：公開 `entget` / `assoc` / `entmod`、検証可能 sample、または同等の evidence により、property と DXF group code + occurrence を直接対応付けます。

named semantic へ進むには canonical case、matching `TCHSIG`、最低 2 組の independent single-variable `TCHDIFF`、stable candidate、matching RawFieldMapping evidence、real decoder、real Reader regression、fail-closed negative tests が必要です。

2026 年公開 AutoLISP 投稿では `TCH_DIMENSION2` の出図 scale を動的変更できることが明示されています。したがって `DIMENSION_PLOT_SCALE` の property existence は確認できますが、検索 index から raw group は確認できません。よって現時点で group 47 などを `TCH_DIMENSION2` の `PlotScale` と命名することはしません。
