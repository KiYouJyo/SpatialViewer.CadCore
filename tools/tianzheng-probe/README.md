# Tianzheng controlled research probe

`TianzhengDiffProbe.lsp` is the privacy-safe in-product research helper for the remaining SpatialViewer.CadCore v0.12 Tianzheng semantic gates.

Commands:

- `TCHPLAN`: list the canonical single-variable experiments for the unresolved gates;
- `TCHRUN`: preferred gate workflow; atomically validate one A/B pair and emit its matching `TCHSIG + TCHDIFF` transcript;
- `TCHDIFF`: compare two otherwise-equivalent `TCH_*` objects and report only changed DXF group slots;
- `TCHSIG`: print one object's ordered group-code signature and structural counts.

No command prints raw DXF values.

## 中文

### 当前范围

楼梯已经取得证据支持的 Partial semantic，因此不再属于探针 blocker。当前只剩三类正式 gate：

1. 轴号 / `TCH_AXIS_LABEL`；
2. 索引对象 / `TCH_DRAWINGINDEX`、`TCH_INDEXPOINTER`；
3. 天正尺寸 / `TCH_DIMENSION2`。

### 标准实验 case

运行 `TCHPLAN` 可查看固定 case：

| 选项 | Case ID | 目标对象 | 唯一允许主动修改的属性 |
| --- | --- | --- | --- |
| `Axis` | `AXIS_LABEL_TEXT` | `TCH_AXIS_LABEL` | 显示的轴号文字/编号 |
| `Index` | `DRAWING_INDEX_TEXT` | `TCH_DRAWINGINDEX` | 显示的图纸索引文字/编号 |
| `Pointer` | `INDEX_POINTER_TEXT` | `TCH_INDEXPOINTER` | 显示的索引指向编号/文字 |
| `DimScale` | `DIMENSION_PLOT_SCALE` | `TCH_DIMENSION2` | 出图比例 |

### 推荐：`TCHRUN`

`TCHRUN` 把原来分开的 `TCHSIG` 与 case-bound `TCHDIFF` 合并成一次原子实验：

1. 复制同一个目标对象，得到 baseline 与 modified。
2. modified **只改表中指定的一项属性**；不要同时移动对象、改图层、样式、字高或其他参数。
3. `APPLOAD` 加载 `TianzhengDiffProbe.lsp`。
4. 运行 `TCHRUN`，选择正确 case，再依次选择 baseline → modified。
5. 脚本先检查：两对象都是 `TCH_*`、DXF identity 相同、与 case 目标类型一致、subclass profile 一致、ordered group-code layout 完全一致。
6. **只有全部检查通过后**才输出一份可复制的 bundle，其中同时包含该 baseline 的 `[TCHSIG]` 与同一 A/B pair 的 `[TCHDIFF]`。
7. 对同一 case 独立制作至少第二组 A/B，再运行一次 `TCHRUN`。
8. 两份 bundle 可直接交给 CadCore bundle parser 与 case-bound consensus；只有各组都稳定变化的 slot 才成为 candidate。

如果 identity、subclass 或 group layout 不一致，`TCHRUN` 只输出拒绝原因，**不会输出半份可解析 bundle**。这样可以避免把一个对象的 signature 与另一组 diff 错配。

`TCHSIG` / `TCHDIFF` 仍保留，便于单独诊断结构或进行 `Adhoc` 非 gate 研究。

### 隐私与证据边界

脚本不会输出 raw DXF value、entity name、object handle、subclass 名称、文件名/路径、项目文字、坐标或尺寸值。`-1` entity name 与 group 5 handle 会在比较前排除。

Case ID 只记录“实验者主动修改了哪个已知属性”，**不是 raw-field 证据**。即使两组 `DIMENSION_PLOT_SCALE` 实验稳定命中 group 47，也必须再有独立 `RawFieldMapping` 外部证据，之后才允许编写 named semantic、real Reader regression 与 fail-closed decoder。

## English

### Current scope

Evidence-backed Partial stair semantics are complete. The remaining formal blockers are axis labels, index objects and Tianzheng dimensions.

`TCHPLAN` lists four canonical experiment cases: `AXIS_LABEL_TEXT` → `TCH_AXIS_LABEL`, `DRAWING_INDEX_TEXT` → `TCH_DRAWINGINDEX`, `INDEX_POINTER_TEXT` → `TCH_INDEXPOINTER`, and `DIMENSION_PLOT_SCALE` → `TCH_DIMENSION2`.

### Preferred workflow: `TCHRUN`

Duplicate one target object and change exactly the named UI property on the modified copy. Run `TCHRUN`, select the canonical case, then baseline → modified. The command validates TCH identity, case/object binding, subclass profile and the complete ordered group-code layout before emitting any parsable protocol.

Only after all checks pass does it print one atomic transcript containing both the baseline `TCHSIG` and the case-bound `TCHDIFF` from that same A/B pair. Repeat with at least one independent pair. CadCore's bundle parser validates signature/diff agreement, and the existing case-bound consensus retains only slots stable across the independent bundles.

On a structural mismatch, `TCHRUN` emits a refusal message and no partial protocol bundle. `TCHSIG` and `TCHDIFF` remain available for standalone diagnostics and `Adhoc` research.

The probe never prints raw DXF values, entity names, handles, subclass strings, file paths, project text, coordinates or dimension values. A case tag records experimental intent only; stable slots still require matching independent RawFieldMapping evidence plus a real Reader regression and fail-closed decoder before semantic promotion.

## 日本語

### 現在の範囲

階段は evidence-backed Partial semantic に到達済みです。残る正式 gate は軸番号、索引 object、Tianzheng 寸法です。

`TCHPLAN` は `AXIS_LABEL_TEXT` → `TCH_AXIS_LABEL`、`DRAWING_INDEX_TEXT` → `TCH_DRAWINGINDEX`、`INDEX_POINTER_TEXT` → `TCH_INDEXPOINTER`、`DIMENSION_PLOT_SCALE` → `TCH_DIMENSION2` の canonical case を表示します。

### 推奨 workflow: `TCHRUN`

同一 object を複製し、modified 側では case が指定する property だけを変更します。`TCHRUN` で case を選択し、baseline → modified の順に指定します。command は TCH identity、case/object binding、subclass profile、ordered group-code layout をすべて確認してから protocol を出力します。

すべて一致した場合だけ、同じ A/B pair に属する baseline `TCHSIG` と case-bound `TCHDIFF` を 1 つの atomic transcript として出力します。同じ case でもう 1 組以上の独立 A/B を作り、CadCore bundle parser + case-bound consensus に渡します。

構造 mismatch の場合、`TCHRUN` は拒否理由だけを出し、解析可能な半端な bundle を出力しません。`TCHSIG` / `TCHDIFF` は standalone 診断と `Adhoc` 研究用に残ります。

probe は raw DXF value、entity name、handle、subclass string、file path、project text、coordinate、dimension value を出力しません。Case tag は experimental intent のみであり、named semantic には matching RawFieldMapping evidence、real Reader regression、fail-closed decoder が引き続き必要です。
