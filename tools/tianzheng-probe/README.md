# Tianzheng controlled research probe

`TianzhengDiffProbe.lsp` is the privacy-safe in-product research helper for the remaining SpatialViewer.CadCore v0.12 Tianzheng semantic gates and explicitly separated identity-drift research.

Commands:

- `TCHPLAN`: list the canonical single-variable experiments;
- `TCHRUN`: preferred workflow; atomically validate one A/B pair and emit its matching `TCHSIG + TCHDIFF` transcript;
- `TCHDIFF`: compare two otherwise-equivalent `TCH_*` objects and report only changed DXF group slots;
- `TCHSIG`: print one object's ordered group-code signature and structural counts.

No command prints raw DXF values.

`TianzhengExportProbe.ps1` is a separate host-only helper. It never opens a drawing and never loads or executes the target ARX. It reads only the PE export directory of a local module, filters native symbol names relevant to the remaining axis/dimension/index research, and emits a path-free module fingerprint plus matching decorated symbols.

## 中文

### 当前范围

楼梯已经取得证据支持的 Partial semantic，因此不再属于探针 blocker。当前只剩三类正式 gate：

1. 轴号 / `TCH_AXIS_LABEL`；
2. 索引对象 / `TCH_DRAWINGINDEX`、`TCH_INDEXPOINTER`；
3. 天正尺寸 / `TCH_DIMENSION2`。

另外存在一条**不计入 release gate 的 identity-drift 研究线**：较新的公开资料出现 `TCH_DIMENSION`，而旧资料/LibreDWG 使用 `TCH_DIMENSION2`。两者暂不 alias。

### 标准实验 case

运行 `TCHPLAN` 可查看固定 case：

| 选项 | Case ID | 目标对象 | 唯一允许主动修改的属性 | 作用 |
| --- | --- | --- | --- | --- |
| `Axis` | `AXIS_LABEL_TEXT` | `TCH_AXIS_LABEL` | 显示的轴号文字/编号 | release gate |
| `Index` | `DRAWING_INDEX_TEXT` | `TCH_DRAWINGINDEX` | 显示的图纸索引文字/编号 | release gate |
| `Pointer` | `INDEX_POINTER_TEXT` | `TCH_INDEXPOINTER` | 显示的索引指向编号/文字 | release gate |
| `DimScale` | `DIMENSION_PLOT_SCALE` | `TCH_DIMENSION2` | 出图比例 | release gate |
| `DimScaleModern` | `DIMENSION_PLOT_SCALE_MODERN` | `TCH_DIMENSION` | 出图比例 | **ResearchOnly** |

`DimScaleModern` 的目的只是取得现代对象的可比较匿名结构证据，并与旧 `TCH_DIMENSION2` 结果对照。CadCore 明确标记该 case 为 `CanClearReleaseGate=false`；即使以后获得 repeatable consensus 和匹配 RawFieldMapping，也不能直接通过 semantic evidence assessor 清除旧 dimension gate。

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

如果当前对象在 `LIST` / probe 中显示 `TCH_DIMENSION2`，尺寸实验选 `DimScale`；如果明确显示 `TCH_DIMENSION`，选 `DimScaleModern`。不要为了让命令通过而人工转换对象类型或修改 case 输出。

如果 identity、subclass 或 group layout 不一致，`TCHRUN` 只输出拒绝原因，**不会输出半份可解析 bundle**。`TCHSIG` / `TCHDIFF` 仍保留，便于单独诊断结构或进行 `Adhoc` 非 gate 研究。

### 本地 native export 探针

如果本机安装的天正包含 `tch_kernal.arx`，可在普通 PowerShell 7 中运行：

```powershell
pwsh -NoProfile -File .\tools\tianzheng-probe\TianzhengExportProbe.ps1 `
  -ModulePath 'C:\path\to\tch_kernal.arx'
```

默认只筛选包含 `TDb...Axis/Dimension/Dim/Index/Pointer...` 的 native export 名称。输出协议为 `[TCHSYM]`，只包含：schema、模块**文件名**、SHA-256、PE machine/timestamp、export/match 数量以及匹配的 decorated symbol。它不会输出输入路径、ARX 二进制内容、DWG/DXF 内容，也不会 `LoadLibrary` / 执行目标模块。

可把匿名 `[TCHSYM]` 输出与 `TCHRUN` bundle 一并提供。若某版本导出例如 `Get...@TDb...Dimension...` 一类符号，它只能证明该 native class/method identity 在该模块版本中存在；**不能**据此推导 DXF group、occurrence 或字段编码，也不能单独解除 release gate。

`-MatchPattern` 可用于研究其他明确的 export 名称；`-RequireMatch` 会在零匹配时 fail closed。CI 使用 Windows 自带 `kernel32.dll` 的真实 export table 验证 PE parser，并同时检查输出不泄露源路径。

### 隐私与证据边界

AutoLISP 脚本不会输出 raw DXF value、entity name、object handle、subclass 名称、文件名/路径、项目文字、坐标或尺寸值。`-1` entity name 与 group 5 handle 会在比较前排除。PE export 探针不读取图纸，只读取模块的 export directory，并只输出模块文件名和指纹，不输出安装路径。

Case ID 只记录“实验者主动修改了哪个已知属性”，**不是 raw-field 证据**。Native export symbol 同样只属于 identity/API evidence，**不是 raw-field 证据**。即使两组 `DIMENSION_PLOT_SCALE` 实验稳定命中 group 47，同时又发现一个名称包含 `Scale` 的 `TDb...Dimension...` export，也必须再有独立 `RawFieldMapping` 外部证据，之后才允许编写 named semantic、real Reader regression 与 fail-closed decoder。ResearchOnly case 还多一层硬限制：它只能用于比较和决定是否需要重定义 gate，不能直接 semantic-ready。

## English

### Current scope

Evidence-backed Partial stair semantics are complete. The remaining formal blockers are axis labels, index objects and legacy Tianzheng dimensions (`TCH_DIMENSION2`).

A newer public identity, `TCH_DIMENSION`, is tracked separately as identity-drift research. `TCHPLAN` therefore exposes four release-gate cases plus `DimScaleModern` → `DIMENSION_PLOT_SCALE_MODERN` / `TCH_DIMENSION` as **ResearchOnly**.

`DimScaleModern` exists so current T20 objects can produce the same privacy-safe atomic evidence format without being aliased to `TCH_DIMENSION2`. CadCore marks this case `CanClearReleaseGate=false`; repeatable consensus and even matching raw-field evidence cannot make it semantic-ready through the release-gate assessor.

### Preferred workflow: `TCHRUN`

Duplicate one target object and change exactly the named UI property on the modified copy. Run `TCHRUN`, select the case matching the actual object identity, then baseline → modified. The command validates TCH identity, case/object binding, subclass profile and the complete ordered group-code layout before emitting any parsable protocol.

Use `DimScale` when the actual object type is `TCH_DIMENSION2`; use `DimScaleModern` only when it is `TCH_DIMENSION`. Do not rewrite the type or case tag to force a match.

Only after all checks pass does the command print one atomic transcript containing both the baseline `TCHSIG` and the case-bound `TCHDIFF` from that same A/B pair. Repeat with at least one independent pair. On structural mismatch it emits a refusal and no partial bundle.

### Native export probe

`TianzhengExportProbe.ps1 -ModulePath <tch_kernal.arx>` parses the module's PE export directory without loading or executing the ARX. By default it emits only matching `TDb` axis/dimension/index/pointer decorated symbols plus a path-free `[TCHSYM]` module fingerprint. It does not open a drawing, print the installation path or copy any module bytes.

Export symbols are identity/API evidence only. A method name that appears to contain `Scale`, `Text` or a related semantic does not identify a DXF group or occurrence and cannot clear a semantic gate. Use symbol output only as independent context alongside repeatable `TCHRUN` evidence and an exact RawFieldMapping source. `-RequireMatch` fails closed on zero matches. CI exercises the parser against the real `kernel32.dll` export table and asserts that the source path is not emitted.

The AutoLISP probe never prints raw DXF values, entity names, handles, subclass strings, file paths, project text, coordinates or dimension values. A case tag records experimental intent only. Release-gate semantic promotion still requires matching independent RawFieldMapping evidence, a real Reader regression and a fail-closed decoder; ResearchOnly evidence can only inform whether the gate definition must later be revised.

## 日本語

### 現在の範囲

階段は evidence-backed Partial semantic に到達済みです。残る正式 gate は軸番号、索引 object、legacy Tianzheng 寸法 (`TCH_DIMENSION2`) です。

新しい公開資料に現れる `TCH_DIMENSION` は identity-drift research として別管理します。`TCHPLAN` は 4 つの release-gate case に加えて、`DimScaleModern` → `DIMENSION_PLOT_SCALE_MODERN` / `TCH_DIMENSION` を **ResearchOnly** として表示します。

`DimScaleModern` は modern T20 object から同じ privacy-safe atomic evidence を取得するための case であり、`TCH_DIMENSION2` への alias ではありません。CadCore では `CanClearReleaseGate=false` と固定され、repeatable consensus や matching raw-field evidence が得られても release semantic assessor を通過できません。

### 推奨 workflow: `TCHRUN`

同一 object を複製し、modified 側では case が指定する property だけを変更します。実際の object type が `TCH_DIMENSION2` なら `DimScale`、`TCH_DIMENSION` なら `DimScaleModern` を選択します。type や case tag を書き換えて一致させてはいけません。

`TCHRUN` は TCH identity、case/object binding、subclass profile、ordered group-code layout を確認し、すべて一致した場合だけ同じ A/B pair の baseline `TCHSIG` と case-bound `TCHDIFF` を atomic transcript として出力します。構造 mismatch の場合は拒否理由だけを出し、半端な bundle は出力しません。

### native export probe

`TianzhengExportProbe.ps1 -ModulePath <tch_kernal.arx>` は ARX を load/execute せず、PE export directory だけを読み取ります。既定では `TDb` の axis/dimension/index/pointer 関連 decorated symbol と、path を含まない `[TCHSYM]` module fingerprint のみを出力します。drawing、install path、module binary content は出力しません。

export symbol は identity/API evidence に限定されます。`Scale` や `Text` を含む method 名が存在しても DXF group + occurrence の mapping にはなりません。repeatable `TCHRUN` と exact RawFieldMapping evidence が揃うまで release gate は解除しません。`-RequireMatch` は zero match で fail closed します。CI では実際の `kernel32.dll` export table を使って parser と path-redaction contract を検証します。

AutoLISP probe は raw DXF value、entity name、handle、subclass string、file path、project text、coordinate、dimension value を出力しません。ResearchOnly evidence は gate 定義を再検討する材料にのみ使用できます。
