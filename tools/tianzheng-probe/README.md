# Tianzheng controlled research probe

`TianzhengDiffProbe.lsp` is the privacy-safe in-product research helper for the remaining SpatialViewer.CadCore v0.12 Tianzheng semantic gates.

Commands:

- `TCHPLAN`: list the canonical single-variable experiments for the unresolved gates;
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

| TCHDIFF 选项 | Case ID | 目标对象 | 唯一允许主动修改的属性 |
| --- | --- | --- | --- |
| `Axis` | `AXIS_LABEL_TEXT` | `TCH_AXIS_LABEL` | 显示的轴号文字/编号 |
| `Index` | `DRAWING_INDEX_TEXT` | `TCH_DRAWINGINDEX` | 显示的图纸索引文字/编号 |
| `Pointer` | `INDEX_POINTER_TEXT` | `TCH_INDEXPOINTER` | 显示的索引指向编号/文字 |
| `DimScale` | `DIMENSION_PLOT_SCALE` | `TCH_DIMENSION2` | 出图比例 |

`TCHDIFF` 会把 case ID 写入匿名输出，并检查所选对象类型是否与 case 匹配。选择错误对象时直接 fail closed。`Adhoc` 保留原有未标记模式，用于非 gate 研究。

推荐流程：

1. 复制同一个目标对象得到 baseline 与 modified。
2. modified 只改表中指定的一个属性；不要同时移动对象、改图层、样式、文字高度或其他参数。
3. `APPLOAD` 加载 `TianzhengDiffProbe.lsp`。
4. 运行 `TCHSIG` 获取结构签名。
5. 运行 `TCHDIFF` 并选择正确的标准 case，随后选择 baseline → modified。
6. 对同一 case 再独立制作至少一组 A/B。
7. 将两组以上输出交给 CadCore 的 case-bound consensus。只有每组都稳定变化的 slot 才保留为 candidate。
8. candidate 仍不能直接命名为 semantic；还必须有公开 AutoLISP/entget、可验证样本或其他独立证据，并最终加入真实 Reader regression 与 fail-closed 测试。

### 隐私与安全边界

脚本不会输出 raw DXF value、entity name、object handle、subclass 名称、文件名/路径、项目文字、坐标或尺寸值。`-1` entity name 与 group 5 handle 会在比较前排除。DXF identity、subclass profile、group-code layout 或标准 case 的目标对象类型不一致时，`TCHDIFF` 都拒绝继续，不做启发式对齐。

Case ID 只记录“实验者主动修改了哪个已知属性”。它**不是字段证据**。例如 `DIMENSION_PLOT_SCALE` 实验稳定命中某个 group 47，也不能只凭 case 名称就宣布 group 47 = 出图比例。

## English

### Current scope

Evidence-backed Partial stair semantics are now complete for the v0.12 gate. The remaining formal blockers are axis labels, index objects and Tianzheng dimensions.

`TCHPLAN` lists four canonical experiment cases: `AXIS_LABEL_TEXT` → `TCH_AXIS_LABEL`, `DRAWING_INDEX_TEXT` → `TCH_DRAWINGINDEX`, `INDEX_POINTER_TEXT` → `TCH_INDEXPOINTER`, and `DIMENSION_PLOT_SCALE` → `TCH_DIMENSION2`.

For a gate experiment, duplicate one object, change exactly the named UI property on the modified copy, capture a `TCHSIG`, then run `TCHDIFF` with the matching case. Repeat with at least one independent pair. CadCore's case-bound consensus refuses mixed experiment intents and retains only slots stable across all observations.

The case tag records experimental intent only. It does not assign semantic meaning to any DXF group. A stable candidate still requires independent external evidence plus a real Reader regression and fail-closed decoder before it can enter native semantics.

The probe never prints raw DXF values, entity names, handles, subclass strings, file paths, project text, coordinates or dimension values. Identity/profile/layout/case-object mismatches fail closed. `Adhoc` preserves the original untagged mode for non-gate research.

## 日本語

### 現在の範囲

階段は evidence-backed Partial semantic に到達したため、v0.12 probe blocker から外れました。残る正式 gate は軸番号、索引 object、Tianzheng 寸法です。

`TCHPLAN` は 4 つの canonical case を表示します：`AXIS_LABEL_TEXT` → `TCH_AXIS_LABEL`、`DRAWING_INDEX_TEXT` → `TCH_DRAWINGINDEX`、`INDEX_POINTER_TEXT` → `TCH_INDEXPOINTER`、`DIMENSION_PLOT_SCALE` → `TCH_DIMENSION2`。

同一 object を複製し、modified 側では case が指定する既知 property だけを変更します。`TCHSIG` を取得してから matching case で `TCHDIFF` を実行し、最低もう 1 組の独立 A/B を作成します。CadCore の case-bound consensus は異なる experiment intent の混在を拒否し、全 observation で安定する slot だけを candidate として残します。

Case ID は experimental intent の記録であり、DXF group の semantic mapping ではありません。stable candidate を native semantic に昇格するには、独立した外部 evidence、real Reader regression、fail-closed decoder が引き続き必要です。

probe は raw DXF value、entity name、handle、subclass string、file path、project text、coordinate、dimension value を出力しません。identity/profile/layout/case-object mismatch は fail closed です。`Adhoc` は非 gate 研究向けの従来 untagged mode として残ります。
