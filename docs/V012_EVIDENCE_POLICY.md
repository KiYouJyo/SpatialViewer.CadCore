# v0.12 Tianzheng semantic evidence policy

This document defines the evidence strength used for the final SpatialViewer.CadCore v0.12 Tianzheng semantic blockers. It does not reduce the release gate in `V012_TIANZHENG_ARCHITECTURE.md`.

## 中文

### 为什么分级

最后三个 blocker（`TCH_AXIS_LABEL`、`TCH_DRAWINGINDEX` / `TCH_INDEXPOINTER`、`TCH_DIMENSION2`）已经不缺类型识别、raw payload、Proxy Graphics 或 A/B 工具，真正缺的是**可信的 raw field → semantic 映射**。

公开资料常见两种强度，必须严格区分：

1. **ParameterExistence**：只能证明某个对象/功能确实具有某项可编辑属性，例如“轴号文字”“索引文字”“标注出图比例”。这类资料不能推出具体 DXF group。
2. **RawFieldMapping**：公开 `entget` / `assoc` / `entmod`、可验证样本或同等强度资料直接把该属性绑定到明确的 DXF group code + occurrence。只有这一等级才能与 `TCHDIFF` consensus 合并，形成 semantic implementation candidate。

### 解除 gate 的最小条件

单个字段要进入 named semantic，至少需要：

- canonical `TCHPLAN` case；
- `TCHRUN` 原子 bundle，或严格配对的 `TCHSIG + TCHDIFF`；
- 至少 2 组独立、单变量 observation；
- stable candidate；
- 至少一条与该 stable candidate 的 `code + occurrence` 精确一致的 **RawFieldMapping** 外部证据；
- 实际 CadCore decoder；
- real Reader regression；
- malformed / duplicate / truncated / identity mismatch 等 fail-closed 回归。

`ParameterExistence`、跨类类推、Proxy Graphics、对象特性面板截图、仅有类型名、单次 A/B observation 均不能单独解除 gate。

### 剩余 gate 的公开证据层级

| Case | 当前公开证据 | 等级 | 仍缺什么 |
| --- | --- | --- | --- |
| `AXIS_LABEL_TEXT` / `TCH_AXIS_LABEL` | 多份天正建筑教学/使用资料明确说明轴号对象可直接双击并在位修改轴号文字，且轴号作为关联对象统一编辑 | `ParameterExistence` | exact raw group + occurrence |
| `DRAWING_INDEX_TEXT` / `TCH_DRAWINGINDEX` | T20 产品资料明确说明索引图名/图名编号属于可编辑索引标注功能 | `ParameterExistence`（产品语义级） | exact `TCH_DRAWINGINDEX` raw group + occurrence |
| `INDEX_POINTER_TEXT` / `TCH_INDEXPOINTER` | T20 产品资料明确说明索引符号具有对象编辑参数并持续改进 | `ParameterExistence`（产品语义级） | exact `TCH_INDEXPOINTER` raw group + occurrence |
| `DIMENSION_PLOT_SCALE` / `TCH_DIMENSION2` | 明经 CAD 公开 AutoLISP 帖明确引用“动态调整 `TCH_DIMENSION2` 标注出图比例”的专门实现 | `ParameterExistence`（exact type） | exact raw group + occurrence |

这里特意把索引两项标为“产品语义级”：公开资料足以确认用户可编辑的索引文字/编号能力，但没有把资料中的 UI 名称与某一个 raw `TCH_*` class 字段直接绑定。因此它仍不能作为 `RawFieldMapping`。

### TCH_DIMENSION2 与 group 47

多个 `TDbEntity` 派生标注对象以及公开 `TCH_ELEVATION` 代码确实反复使用 group 47 作为出图比例；公开 entget 样本中也能看到相关天正标注对象带有 `(47 . 100.0)`。这些都是有价值的交叉证据，但在 `TCH_DIMENSION2` **自身**字段证据出现前，仍只保留为 research hypothesis。

2026 年公开的明经 CAD AutoLISP 帖已经确认 `TCH_DIMENSION2` 的出图比例可以被专门动态调整，但当前搜索索引没有暴露其具体 `entget/entmod` group 代码。因此仍不能据此把 group 47 或其他 group 直接命名为 `PlotScale`。

### `TCH_DIMENSION` / `TCH_DIMENSION2` identity drift

当前公开资料出现了一个必须单独记录的版本差异：2026 年较新的天正兼容 AutoLISP 支持表使用 `TCH_DIMENSION`，而同一资料引用的旧实现标题、LibreDWG class registry 以及既有第三方 CAD 代码使用 `TCH_DIMENSION2`。现有证据不足以判断这是简单重命名、两个并存的对象类型，还是不同天正版本/保存格式产生的不同 class。

CadCore 因此采用严格边界：

- `TCH_DIMENSION` 仍按 `TCH_*` 身份完整保留为 Tianzheng opaque custom entity，继续保留 raw/proxy evidence；
- 不把 `TCH_DIMENSION` alias 成 `TCH_DIMENSION2`；
- `DIMENSION_PLOT_SCALE` canonical experiment 仍严格绑定 `TCH_DIMENSION2`，选择 `TCH_DIMENSION` 时 fail closed；
- 这个 identity drift 暂时作为兼容性观察线，不凭公开名称差异新增或解除 semantic gate；
- 如果真实现代 T20 corpus / `TCHRUN` 证明 `TCH_DIMENSION` 已经替代 `TCH_DIMENSION2`，必须显式重新评估 v0.12 dimension gate，而不是通过 alias 让 gate 虚假变绿。

### 现代版本限制

公开的 2025 Autodesk Community 讨论还报告：T20 V10 的部分自定义天正对象可能无法通过普通 `entget` / `vlax-dump-object` 暴露需要的字段。这个事实不会降低 gate，反而说明实验工具必须保留 fail-closed 策略：如果当前天正版本不暴露可比较 raw layout，就记录为“不足以解码”，而不是回退到猜测、跨类类推或 Proxy Graphics 反推字段。

## English

### Evidence grades

The final three blockers no longer lack object identification, raw capture, proxy fallback or A/B tooling. They lack trustworthy raw-field naming evidence.

- **ParameterExistence** confirms that the intended editable property exists at the object/product level. It cannot identify a DXF group.
- **RawFieldMapping** directly binds that property to a specific DXF group code and occurrence through public `entget` / `assoc` / `entmod`, a verifiable sample, or evidence of equivalent strength.

A named semantic requires a canonical experiment case, an atomic `TCHRUN` bundle (or strictly paired `TCHSIG + TCHDIFF`), at least two independent single-variable observations, a stable candidate, matching RawFieldMapping evidence, a real decoder, a real Reader regression, and fail-closed negative tests.

Current public material confirms editable axis-label text, index-symbol/index-drawing-name behavior, and `TCH_DIMENSION2` plot/output scale at ParameterExistence strength. None of the remaining cases has a sufficiently verified raw group + occurrence mapping yet. The index evidence is intentionally classified at product-semantic level rather than exact raw-class level.

Public related-object evidence repeatedly associates group 47 with plot scale, including Tianzheng annotation entget material, but this remains a hypothesis for `TCH_DIMENSION2` until evidence from that exact type identifies its field. A public 2026 AutoLISP post confirms that `TCH_DIMENSION2` plot scale is dynamically adjustable but the indexed code does not expose the field used.

### `TCH_DIMENSION` / `TCH_DIMENSION2` identity drift

Newer public Tianzheng-compatible AutoLISP material lists `TCH_DIMENSION`, while the older implementation it references, LibreDWG's class registry and existing third-party CAD code use `TCH_DIMENSION2`. There is not enough evidence to treat these as a rename or as the same payload schema.

CadCore therefore preserves `TCH_DIMENSION` as a Tianzheng opaque custom entity with raw/proxy evidence, but does not alias it to `TCH_DIMENSION2`. The canonical `DIMENSION_PLOT_SCALE` experiment remains bound to `TCH_DIMENSION2` and fails closed for `TCH_DIMENSION`. If a real modern T20 corpus proves that `TCH_DIMENSION` supersedes the legacy identity, the v0.12 dimension gate must be explicitly revisited rather than silently cleared through an alias.

A 2025 Autodesk Community discussion also reports that some T20 V10 custom objects may not expose useful group data through ordinary `entget` / `vlax-dump-object`. CadCore therefore keeps the gate fail closed when a version does not expose a comparable raw layout.

## 日本語

### evidence grade

残り 3 blocker で不足しているのは object type、raw capture、Proxy Graphics、A/B tooling ではなく、信頼できる raw-field naming evidence です。

- **ParameterExistence**：対象 property が object/product level で編集可能であることを確認します。DXF group は特定できません。
- **RawFieldMapping**：公開 `entget` / `assoc` / `entmod`、検証可能 sample、または同等の evidence により、property と DXF group code + occurrence を直接対応付けます。

named semantic へ進むには canonical case、atomic `TCHRUN` bundle（または厳密に対応した `TCHSIG + TCHDIFF`）、最低 2 組の independent single-variable observation、stable candidate、matching RawFieldMapping evidence、real decoder、real Reader regression、fail-closed negative tests が必要です。

現在の公開資料では、軸番号 text、索引 symbol / 索引図名の編集機能、`TCH_DIMENSION2` の出図 scale について ParameterExistence までは確認できます。しかし残る case のいずれも exact raw group + occurrence の mapping はまだ確認できていません。索引 evidence は意図的に product-semantic level に留めています。

group 47 は複数の関連 Tianzheng annotation object で出図 scale と関連しますが、`TCH_DIMENSION2` 自身の field evidence が出るまでは hypothesis のままです。2026 年公開 AutoLISP 投稿は `TCH_DIMENSION2` の出図 scale が動的に変更可能であることを確認しますが、検索 index では使用 raw field が確認できません。

### `TCH_DIMENSION` / `TCH_DIMENSION2` identity drift

新しい公開 Tianzheng 対応 AutoLISP 資料では `TCH_DIMENSION` が列挙されていますが、その資料が参照する旧実装、LibreDWG class registry、既存 third-party CAD code では `TCH_DIMENSION2` が使われています。現時点では rename、共存 type、version/save-format 差のどれかを断定できません。

そのため CadCore は `TCH_DIMENSION` を Tianzheng opaque custom entity として raw/proxy evidence ごと保持しますが、`TCH_DIMENSION2` への alias は行いません。canonical `DIMENSION_PLOT_SCALE` experiment は引き続き `TCH_DIMENSION2` に限定し、`TCH_DIMENSION` は fail closed とします。modern T20 の real corpus で置換関係が確認された場合は、alias で gate を見かけ上通過させるのではなく、v0.12 dimension gate を明示的に再評価します。

また 2025 年の Autodesk Community では、T20 V10 の一部 custom object が通常の `entget` / `vlax-dump-object` で必要 group data を公開しない可能性が報告されています。したがって比較可能 raw layout を取得できない version は fail closed とし、推測で gate を解除しません。
