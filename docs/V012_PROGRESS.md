# v0.12 Tianzheng Architecture 2D progress ledger

> Incremental progress log for the in-progress v0.12 milestone. The acceptance baseline remains [V012_TIANZHENG_ARCHITECTURE.md](V012_TIANZHENG_ARCHITECTURE.md). This file records later merged work without redefining or shrinking the release gate.

## 中文

### 2026-09-03 当前增量

在既有验收矩阵之后，主线又完成了以下增量：

- **Corpus schema v3**：匿名 Tianzheng schema corpus 现在分别统计 `PartialSemanticEntityCount` 与 `Drawable2DSemanticEntityCount`，并要求二者之和严格等于 `NativeSemanticEntityCount`。旧 schema v2 JSON fail closed，不静默补零。
- **`TCH_DRAWINGNAME` — Partial**：公开 AutoLISP 证据明确将 raw group 1 作为图名文字读取，并与 ActiveX `NameText` 对应。CadCore 仅恢复 `Text`；不声明插入点、比例、下划线、索引号、索引关系或 native 图名符号几何。
- **Privacy-safe A/B payload differ**：新增 `CadDxfCustomPayloadDiffer`。对于 group-code 布局完全一致的两份 raw DXF custom payload，仅报告发生变化的 group index / code / occurrence；不输出原始值。布局变化时只报告结构差异，不进行启发式对齐；truncated 输入拒绝逐值比较。
- **Entity-level identity gate**：A/B differ 新增 `Compare(CadCustomEntity before, CadCustomEntity after)` 作为推荐入口。比较前要求有效 DXF identity 一致；双方已知时 C++ class 与 application identity 也必须一致；两侧都必须存在 raw DXF evidence。身份不一致或 evidence 缺失会在产生候选差异前 fail closed，异常信息不包含 handle 或 raw value。

### 轴网范围澄清

公开天正手册说明，**轴网线本身不是天正自定义对象**：DOTE 图层上的普通 AutoCAD `LINE`、`ARC`、`CIRCLE` 可直接被天正识别为轴线。CadCore 已通过通用 CAD primitive 管线支持这些几何，因此 v0.12 不需要再为普通轴线复制一套 Tianzheng native decoder。

真正仍需 proprietary semantic 的部分是**轴号 / axis-label 系统**以及相关的天正尺寸标注对象。这个澄清不是缩小 v0.11.0 已承诺的“轴网”范围，而是把 gate 精确定位到其中真正依赖天正自定义对象语义的部分。

### 对验收矩阵的影响

`TCH_DRAWINGNAME` 本身已从 Evidence-only 推进到 **Partial**，但这**不等于索引/图名类别整体通过 gate**。`TCH_INDEXPOINTER` / `TCH_DRAWINGINDEX` 尚未获得可靠的 raw group → 参数映射，因此索引/图名 family 继续阻塞 v0.12 正式升版。

A/B differ 属于研究工具，不计入 native semantic gate。它的作用是为柱、轴号、楼梯、尺寸、索引等剩余 blocker 建立可重复的单变量实验流程：在天正中仅修改一个已知属性，比较前后 payload 的结构槽位，再用第二组独立样本和公开资料交叉验证后，才允许把候选 group 命名为 semantic 字段。实体级 identity gate 进一步阻止把不同 custom class 或不同已知应用误当成同一 A/B profile。

### 当前 release blocker

仍需至少达到 Partial semantic：

- `TCH_COLUMN` 柱；
- 自定义轴号 / axis-label family（普通 `LINE` / `ARC` / `CIRCLE` 轴线已由通用 CAD 管线覆盖）；
- `TCH_LINESTAIR` / `TCH_RECTSTAIR` 楼梯；
- `TCH_INDEXPOINTER` / `TCH_DRAWINGINDEX` 等索引对象；
- `TCH_DIMENSION2` 等天正尺寸对象。

产品版本继续保持 `0.11.0`，CLR ABI 保持 `1.0.0.0`，Host Contract 保持 `SpatialViewer.CadHost >=1.0.0,<2.0.0`。

---

## English

### Current delta — 2026-09-03

The following work has landed after the acceptance baseline was written:

- **Corpus schema v3**: the anonymized Tianzheng schema corpus now records `PartialSemanticEntityCount` and `Drawable2DSemanticEntityCount` separately and requires their sum to equal `NativeSemanticEntityCount`. Older v2 JSON fails closed instead of silently defaulting the new fields.
- **`TCH_DRAWINGNAME` — Partial**: public AutoLISP evidence reads raw group 1 as drawing-name text and maps the same value to the ActiveX `NameText` property. CadCore recovers only `Text`; insertion point, scale, underline, index number, index relationships, and native drawing-title geometry remain explicit non-claims.
- **Privacy-safe A/B payload differ**: `CadDxfCustomPayloadDiffer` compares two raw custom DXF payloads. For identical group-code layouts it reports only changed group index/code/occurrence, never the before/after raw values. Layout changes are reported structurally without heuristic alignment, and truncated inputs are not value-diffed.
- **Entity-level identity gate**: the preferred A/B entry point is now `Compare(CadCustomEntity before, CadCustomEntity after)`. Effective DXF identity must match; known C++ class and application identities must also match when present on both sides; both entities must contain raw DXF evidence. Identity/evidence failures occur before any candidate diff is produced, and exceptions do not expose handles or raw values.

### Axis-grid scope clarification

Public Tianzheng manuals state that the **axis grid lines themselves are not custom objects**. Ordinary AutoCAD `LINE`, `ARC`, and `CIRCLE` geometry on the DOTE layer can be recognized as axes. CadCore already supports those primitives through the generic CAD pipeline, so v0.12 does not need a separate proprietary decoder for ordinary axis-line geometry.

The remaining proprietary gate is the **axis-number / axis-label system** plus related Tianzheng dimension annotations. This does not shrink the axis-grid scope declared by v0.11.0; it identifies which part of that scope actually depends on Tianzheng custom-object semantics.

### Effect on the acceptance matrix

`TCH_DRAWINGNAME` itself has advanced from Evidence-only to **Partial**, but the **index/drawing-title category is still blocked**. Reliable raw group → parameter mappings are still missing for `TCH_INDEXPOINTER` / `TCH_DRAWINGINDEX`.

The A/B differ is research tooling and does not count toward a native-semantic gate. Its purpose is to make controlled single-variable experiments repeatable for the remaining blockers: change exactly one known Tianzheng property, identify which structural payload slots changed, then require a second independent sample and external evidence before promoting any candidate group to a named semantic field. The entity-level identity gate prevents unrelated custom classes or known applications from being treated as one A/B profile.

### Current release blockers

The following categories still need at least Partial semantics:

- `TCH_COLUMN` columns;
- custom axis-number / axis-label objects (ordinary `LINE` / `ARC` / `CIRCLE` axis geometry is already covered by the generic CAD pipeline);
- `TCH_LINESTAIR` / `TCH_RECTSTAIR` stairs;
- `TCH_INDEXPOINTER` / `TCH_DRAWINGINDEX` index objects;
- `TCH_DIMENSION2` and related Tianzheng dimension objects.

Product version remains `0.11.0`, CLR ABI remains `1.0.0.0`, and Host Contract remains `SpatialViewer.CadHost >=1.0.0,<2.0.0`.

---

## 日本語

### 2026-09-03 現在の追加進捗

acceptance baseline 作成後、main には次の内容が追加されています。

- **Corpus schema v3**：匿名 Tianzheng schema corpus は `PartialSemanticEntityCount` と `Drawable2DSemanticEntityCount` を分離して集計し、その合計が `NativeSemanticEntityCount` と完全一致することを検証します。旧 v2 JSON は新フィールドを暗黙にゼロ補完せず fail closed します。
- **`TCH_DRAWINGNAME` — Partial**：公開 AutoLISP では raw group 1 が図名文字列として取得され、ActiveX `NameText` と対応しています。CadCore が semantic に昇格するのは `Text` のみです。挿入点、scale、下線、索引番号、索引 relationship、native 図名 geometry は未対応として明示します。
- **Privacy-safe A/B payload differ**：`CadDxfCustomPayloadDiffer` を追加しました。同一 group-code layout の payload 同士では、変化した group index / code / occurrence だけを返し、前後の raw value は返しません。layout が異なる場合は heuristic alignment を行わず構造差分だけを返し、truncated input は value diff しません。
- **Entity-level identity gate**：推奨 A/B entry point として `Compare(CadCustomEntity before, CadCustomEntity after)` を追加しました。effective DXF identity は一致必須で、双方に既知 C++ class / application identity がある場合はそれらも一致必須です。両 entity に raw DXF evidence が必要で、不一致や evidence 欠落は candidate diff 生成前に fail closed します。例外 message に handle / raw value は含めません。

### 軸網 scope の明確化

公開天正 manual では、**軸線そのものは custom object ではありません**。DOTE layer 上の通常 AutoCAD `LINE` / `ARC` / `CIRCLE` は軸線として認識されます。これらは CadCore の generic CAD primitive pipeline ですでに対応しているため、通常の軸線 geometry 用に別 Tianzheng decoder を作る必要はありません。

引き続き proprietary semantic が必要なのは **軸番号 / axis-label system** と関連する天正寸法 annotation です。この整理は v0.11.0 で示した軸網 scope を縮小するものではなく、その中で実際に Tianzheng custom-object semantics に依存する部分を明確化するものです。

### acceptance matrix への影響

`TCH_DRAWINGNAME` 単体は Evidence-only から **Partial** へ進みましたが、**索引 / drawing-title category 全体はまだ gate 未達**です。`TCH_INDEXPOINTER` / `TCH_DRAWINGINDEX` の信頼できる raw group → parameter mapping が未確立だからです。

A/B differ は研究 tool であり native-semantic gate には算入しません。柱・軸号・階段・寸法・索引など残る blocker について、天正上で既知 property を 1 個だけ変更し、前後 payload の変化 slot を抽出し、さらに独立 sample と外部 evidence で交差検証してから semantic field 名を確定するために使用します。entity-level identity gate は異なる custom class / known application を同一 A/B profile と誤認することを防ぎます。

### 現在の release blocker

最低 Partial semantic が必要なカテゴリ：

- `TCH_COLUMN` 柱；
- custom 軸番号 / axis-label object（通常 `LINE` / `ARC` / `CIRCLE` 軸線 geometry は generic CAD pipeline で対応済み）；
- `TCH_LINESTAIR` / `TCH_RECTSTAIR` 階段；
- `TCH_INDEXPOINTER` / `TCH_DRAWINGINDEX` 索引；
- `TCH_DIMENSION2` 等の天正寸法。

Product version は `0.11.0`、CLR ABI は `1.0.0.0`、Host Contract は `SpatialViewer.CadHost >=1.0.0,<2.0.0` のままです。
