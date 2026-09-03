# v0.12 Tianzheng Architecture 2D progress ledger

> Incremental progress log for the in-progress v0.12 milestone. The acceptance baseline remains [V012_TIANZHENG_ARCHITECTURE.md](V012_TIANZHENG_ARCHITECTURE.md). This file records later merged work without redefining or shrinking the release gate.

## 中文

### 2026-09-03 当前增量

在既有验收矩阵之后，main 已合并以下能力：

- **Corpus schema v3**：匿名 Tianzheng schema corpus 分别统计 `PartialSemanticEntityCount` 与 `Drawable2DSemanticEntityCount`，且二者之和必须严格等于 `NativeSemanticEntityCount`。旧 schema v2 JSON fail closed。
- **`TCH_DRAWINGNAME` — Partial**：raw group 1 → 图名文字；仅恢复 `Text`，不声明插入点、比例、下划线、索引号、索引关系或 native 图名几何。
- **`TCH_OPENING` — Partial 增强**：在既有 group 10/20 anchor、可选 Z 与 opening→wall relationship 之上，新增证据明确的 raw group 302 → 门窗编号 `Number`。编号缺失/空白不阻断 anchor semantic；宽、高、窗台高、类型和 native opening geometry 仍保持未知。
- **`TCH_COLUMN` — Partial**：公开可运行 AutoLISP 明确将 `assoc 11` 标注为“柱插入点”并通过 `entmod/entupd` 修改它。CadCore 仅恢复 point 11/21 与可选 31 为 `CadTianzhengColumnAnchorSemantic`；截面宽高、形状、转角、柱高、材料和 native 柱轮廓均未声明支持。
- **`TCH_LINESTAIR` / `TCH_RECTSTAIR` — Partial**：公开 AutoLISP 属性表对两类楼梯都直接给出 raw group 40 → “踏步高度”；独立的天正对象属性资料也确认双跑楼梯具备踏步高等对应属性。CadCore 仅将唯一、正数、有限的 group 40 提升为 `StepHeight`；直线梯段的踏步宽/数量，以及双跑楼梯的梯间宽、平台宽、踏步宽、梯段宽、旋转角、一/二跑步数、楼梯总高、楼层标记和 native stair geometry 均保持 raw-only。
- **Privacy-safe DXF A/B differ**：只报告 changed group index / code / occurrence，不输出 before/after raw value；layout 改变时不做 heuristic alignment；truncated 输入 fail closed。
- **Entity-level identity gate**：A/B 比较要求有效 DXF identity 一致；双方已知时 C++ class 与 application identity 也必须一致；缺 raw evidence 或身份不一致时在产生候选差异前拒绝。
- **Privacy-safe DWG A/B differ**：同长度 retained object record 只报告连续 changed byte ranges；长度变化只报告长度差异与 exact common prefix；不输出 raw bytes、object-section offset、handle、路径或 SHA，也不把 byte range 直接解释成 Tianzheng Databits 字段。
- **Repeatability consensus**：`CadCustomExperimentAnalyzer` 要求至少两组独立 A/B observation。DXF 只保留每组都稳定变化的 group slot；DWG 只保留每组 changed range 的严格区间交集。identity、schema、byte count 或 capture method 不一致均 fail closed。stable candidate 仍只是 evidence，不会自动命名为柱宽、梯高或尺寸比例。

### 轴网范围澄清

公开天正手册说明，**轴网线本身不是 proprietary custom object**：DOTE 图层上的普通 AutoCAD `LINE`、`ARC`、`CIRCLE` 可作为轴线使用。CadCore 已通过通用 primitive 管线支持这些几何。

因此 v0.12 在“轴网”类别真正剩余的 proprietary gate 是 **轴号 / axis-label system** 及相关天正尺寸标注，而不是重新实现普通轴线几何。

### 当前 gate 状态

已满足至少 Partial semantic：

- `TCH_WALL` — **Drawable2D**；
- `TCH_OPENING` — **Partial**；
- `TCH_SPACE` — **Partial**；
- `TCH_ELEVATION` — **Partial**；
- `TCH_DRAWINGNAME` — **Partial**；
- `TCH_COLUMN` — **Partial**；
- `TCH_LINESTAIR` / `TCH_RECTSTAIR` — **Partial**。

仍阻塞 v0.12 正式升版的 3 类：

1. 自定义轴号 / `TCH_AXIS_LABEL` family；
2. `TCH_INDEXPOINTER` / `TCH_DRAWINGINDEX` 等索引对象；
3. `TCH_DIMENSION2` 等天正尺寸对象。

这 3 类都必须至少取得一组**可明确命名、外部证据支持、真实 Reader 回归且 fail-closed** 的 raw field → semantic 映射，才能解除对应 gate。仅有类型登记、Proxy Graphics、raw payload、corpus 或 A/B candidate 不算完成。

### 当前发布结论

- Product/File/Informational version 继续保持 `0.11.0`。
- CLR ABI 继续保持 `1.0.0.0`。
- Host Contract 继续保持 `SpatialViewer.CadHost >=1.0.0,<2.0.0`。
- v0.12 **尚未达到正式发布条件**；剩余 3 个 semantic blocker 全部解除后才进入最终版本升档、三语 release notes、完整 CI、tag 与 publish 收尾。

---

## English

### Current delta — 2026-09-03

The following work has landed on `main` after the original acceptance matrix:

- **Corpus schema v3**: `PartialSemanticEntityCount` and `Drawable2DSemanticEntityCount` are tracked separately and must sum exactly to `NativeSemanticEntityCount`; older v2 JSON fails closed.
- **`TCH_DRAWINGNAME` — Partial**: raw group 1 is promoted only as drawing-name `Text`. Insertion point, scale, underline, index number/relationship and native drawing-title geometry remain non-claims.
- **`TCH_OPENING` — stronger Partial coverage**: on top of the established point-10 anchor, optional Z and opening→wall relationship, raw group 302 is now retained as optional opening/door-window `Number`. Missing/blank 302 does not invalidate the anchor profile. Width, height, sill/clearance, type and native opening geometry remain unknown.
- **`TCH_COLUMN` — Partial**: published operational AutoLISP explicitly identifies `assoc 11` as the column insertion point and edits that point through `entmod/entupd`. CadCore recovers point 11/21 plus optional 31 only. Section dimensions/shape, rotation, column height, material and native column outline remain explicit non-claims.
- **`TCH_LINESTAIR` / `TCH_RECTSTAIR` — Partial**: a published AutoLISP property table directly maps raw group 40 to stair step/riser height for both object types; independent Tianzheng object-property material also confirms the corresponding double-flight stair property set. CadCore promotes only one unique, positive, finite group-40 value as `StepHeight`. Straight-flight tread width/count, and double-flight stairwell/platform/tread/flight widths, rotation, flight counts, total stair height, floor markers and native stair geometry remain raw-only.
- **Privacy-safe DXF A/B differ**: reports changed group index/code/occurrence without retaining before/after raw values; structural changes are not heuristically aligned; truncated inputs fail closed.
- **Entity-level identity gate**: effective DXF identity must match; known C++ class/application identities must also match when present on both sides; missing evidence or identity mismatch is rejected before candidate diff output.
- **Privacy-safe DWG A/B differ**: equal-length retained object records report contiguous changed byte ranges only. Length changes report only length mismatch and exact common prefix. Raw bytes, object-section offsets, handles, paths and SHA values are excluded, and byte ranges are not labeled as Tianzheng Databits fields.
- **Repeatability consensus**: `CadCustomExperimentAnalyzer` requires at least two independent observations. DXF consensus keeps only group slots changed in every observation; DWG consensus keeps only the strict interval intersection of changed ranges. Identity/schema/byte-count/capture-method mismatches fail closed. A stable candidate remains evidence, not an automatically named semantic field.

### Axis-grid scope clarification

Public Tianzheng manuals describe ordinary AutoCAD `LINE`, `ARC` and `CIRCLE` geometry on the DOTE layer as axis geometry. CadCore already handles those primitives through the generic CAD pipeline.

The remaining proprietary part of the v0.12 grid gate is therefore the **axis-number / axis-label system** and related Tianzheng dimension annotations, not ordinary axis-line geometry.

### Current gate state

At least Partial semantics are already satisfied for:

- `TCH_WALL` — **Drawable2D**;
- `TCH_OPENING` — **Partial**;
- `TCH_SPACE` — **Partial**;
- `TCH_ELEVATION` — **Partial**;
- `TCH_DRAWINGNAME` — **Partial**;
- `TCH_COLUMN` — **Partial**;
- `TCH_LINESTAIR` / `TCH_RECTSTAIR` — **Partial**.

Three categories still block v0.12 release:

1. custom axis-number / `TCH_AXIS_LABEL` family;
2. `TCH_INDEXPOINTER` / `TCH_DRAWINGINDEX` index objects;
3. `TCH_DIMENSION2` and related Tianzheng dimensions.

Each category must reach at least one **clearly named, externally evidenced raw-field → semantic mapping with a real Reader regression and fail-closed behavior**. Type registration, Proxy Graphics, raw capture, corpus data or A/B candidates alone do not clear a gate.

### Release conclusion

- Product/File/Informational version remains `0.11.0`.
- CLR ABI remains `1.0.0.0`.
- Host Contract remains `SpatialViewer.CadHost >=1.0.0,<2.0.0`.
- v0.12 is **not release-ready**. Final version bump, trilingual release notes, full CI, tag and publish work happen only after all three remaining semantic blockers are cleared.

---

## 日本語

### 2026-09-03 現在の追加進捗

元の acceptance matrix 作成後、`main` には次の内容が追加されています。

- **Corpus schema v3**：`PartialSemanticEntityCount` と `Drawable2DSemanticEntityCount` を分離し、その合計が `NativeSemanticEntityCount` と完全一致することを必須化。旧 v2 JSON は fail closed。
- **`TCH_DRAWINGNAME` — Partial**：raw group 1 のみを図名 `Text` として semantic に昇格。挿入点、scale、下線、索引番号/relationship、native 図名 geometry は未対応。
- **`TCH_OPENING` — Partial 強化**：既存の point-10 anchor、optional Z、opening→wall relationship に加え、raw group 302 を optional `Number` として保持。302 が欠落/空白でも anchor semantic は成立し、幅、高さ、窓台高、type、native geometry は未解読のまま。
- **`TCH_COLUMN` — Partial**：公開された実運用 AutoLISP が `assoc 11` を「柱挿入点」と明示し、`entmod/entupd` でその point を更新しています。CadCore は point 11/21 と optional 31 のみを `CadTianzhengColumnAnchorSemantic` に昇格。断面寸法/形状、回転角、柱高、material、native 柱輪郭は non-claim。
- **`TCH_LINESTAIR` / `TCH_RECTSTAIR` — Partial**：公開 AutoLISP property table は両 object type について raw group 40 を踏步高さに直接対応付け、独立した Tianzheng object-property 資料も double-flight stair の対応 property set を確認できます。CadCore が semantic に昇格するのは unique / positive / finite な group 40 の `StepHeight` のみです。straight-flight の踏面幅/段数、double-flight の階段室幅、platform/tread/flight 幅、rotation、各 flight 段数、総階高、floor marker、native stair geometry は raw-only のままです。
- **Privacy-safe DXF A/B differ**：changed group index/code/occurrence のみを返し、raw before/after value は保持しません。structure change に heuristic alignment を行わず、truncated input は fail closed。
- **Entity-level identity gate**：effective DXF identity 一致を必須化し、双方で既知の C++ class/application identity も一致必須。evidence 欠落/identity mismatch は candidate diff 生成前に拒否。
- **Privacy-safe DWG A/B differ**：同一長 object record は連続 changed byte range のみを返し、長さ違いは length mismatch と exact common prefix のみ。raw bytes、object-section offset、handle、path、SHA を出力せず、range を Tianzheng Databits field と自動解釈しません。
- **Repeatability consensus**：`CadCustomExperimentAnalyzer` は最低 2 組の独立 observation を要求。DXF は全 observation で安定して変化した group slot のみ、DWG は changed range の厳密な区間交差のみを candidate とします。identity/schema/byte-count/capture-method mismatch は fail closed。stable candidate は evidence であり semantic field 名ではありません。

### 軸網 scope の明確化

公開 Tianzheng manual では DOTE layer 上の通常 AutoCAD `LINE` / `ARC` / `CIRCLE` が軸線として扱われます。これらは CadCore の generic CAD primitive pipeline ですでに対応済みです。

したがって v0.12 の軸網カテゴリで残る proprietary gate は **軸番号 / axis-label system** と関連する Tianzheng dimension annotation です。

### 現在の gate 状態

最低 Partial semantic を満たしているもの：

- `TCH_WALL` — **Drawable2D**；
- `TCH_OPENING` — **Partial**；
- `TCH_SPACE` — **Partial**；
- `TCH_ELEVATION` — **Partial**；
- `TCH_DRAWINGNAME` — **Partial**；
- `TCH_COLUMN` — **Partial**；
- `TCH_LINESTAIR` / `TCH_RECTSTAIR` — **Partial**。

v0.12 正式版を引き続き阻害している 3 カテゴリ：

1. custom 軸番号 / `TCH_AXIS_LABEL` family；
2. `TCH_INDEXPOINTER` / `TCH_DRAWINGINDEX` 索引 object；
3. `TCH_DIMENSION2` 等の Tianzheng dimension。

各カテゴリは、少なくとも 1 組の**明確に命名でき、外部 evidence があり、real Reader regression と fail-closed behavior を備えた raw field → semantic mapping**を取得する必要があります。type 登録、Proxy Graphics、raw evidence、corpus、A/B candidate だけでは gate を通過しません。

### release 結論

- Product/File/Informational version は `0.11.0` のまま。
- CLR ABI は `1.0.0.0` のまま。
- Host Contract は `SpatialViewer.CadHost >=1.0.0,<2.0.0` のまま。
- v0.12 は **まだ release-ready ではありません**。残る 3 semantic blocker をすべて解除した後にのみ、最終 version bump、三言語 release note、full CI、tag、publish を実施します。
