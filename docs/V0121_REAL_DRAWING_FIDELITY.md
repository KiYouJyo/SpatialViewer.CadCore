# v0.12.1 real-drawing fidelity work

## 中文

本补丁线由 v0.12.0 接入真实建筑图后的显示对照驱动。首批可见差异集中在图框/标题栏、轴网轴号、门窗等 Tianzheng/custom-object 构件，以及普通 CAD 对象的旋转与块展开精度。

### 已完成

- 将 ACadSharp 已明确解析出的 `ProxyText` / `ProxyUnicodeText` 保留为 reader-independent `CadProxyText`，并进入 `TextGeometry` 显示 fallback；保留起点、字高、方向、宽度系数与倾斜角，但不提升为 Tianzheng native semantic。
- 支持安全的 proxy lightweight-polyline、bulge 与 closed-state fallback；`ProxyPolylineWithNormal` 仅允许 XY 平面对象进入二维显示。
- 实现平衡的二维 model-transform push/pop 栈。平移、旋转、等比缩放可以安全进入 fallback；非均匀缩放、非平面/3D 耦合、clip、非法矩阵和栈不平衡继续整条 fail closed。
- 修正普通文字、尺寸文字和旋转椭圆的绕点矩阵顺序，旋转后插入点/文字点/椭圆中心保持不动。
- 修正匿名/动态块定义过滤：只排除 `*Model_Space` / `*Paper_Space...` 内部块，不再误删 `*U###` 等匿名块。真实 DXF 回归验证 90° 匿名块 INSERT 能正确展开到 Scene。
- 增加 `AnonymousBlockDefinitionCount`、`PaperLayoutCount`、`PaperSpaceEntityCount`、`PaperViewportCount` 导入诊断，用于区分图框来自匿名块还是 Paper Space/Layout。
- 修正 ACadSharp 角度单位边界。ACadSharp 在 reader 层已经把 `IsAngle` 字段转换为 radians，CadCore 不再二次乘 `π/180`。ARC、TEXT/MTEXT、ATTRIBUTE、INSERT、HATCH 和 Viewport twist 均沿统一弧度边界传递，并由真实 DXF 回归覆盖。
- 新增 `CadSourceContentProfiler`。它直接从 DWG/DXF source structure 统计 Model Space、Paper Space、viewport、匿名/动态块、TABLE cache block 与 Xref 的定义/引用/卸载/空缓存状态，专门用于判断“图框为什么没显示”。Profiler 仅返回计数和布尔状态，不保留图纸文字、handle、Xref 名称或文件路径。

### 图框缺失判读

`CadSourceContentProfiler.AnalyzeFile(path)` 返回结构计数，可按以下规则定位：

- `PaperSpaceEntityCount > 0`：图框或标题栏可能位于 Layout/Paper Space；模型空间视图不应假装它不存在，宿主应切换/显示相应 layout scene。
- `AnonymousBlockDefinitionCount / AnonymousBlockReferenceCount > 0`：内容可能来自动态块、匿名块、标注缓存或 TABLE cache；v0.12.1 已保留这些定义，不再按 `*` 前缀整体删除。
- `TableEntityCount / TableCacheBlockDefinitionCount > 0`：ACadSharp 的 `TableEntity` 本身继承 `Insert`，表格显示依赖匿名 cache block；优先验证 cache geometry，而不是另造一套表格语义。
- `ExternalReferenceDefinitionCount / ExternalReferenceReferenceCount > 0`：图框可能来自 Xref。若同时 `EmptyExternalReferenceDefinitionCount > 0` 或 `UnloadedExternalReferenceDefinitionCount > 0`，当前文件内没有足够本地几何可供可靠显示；内核只报告依赖，不伪造外部内容。
- 上述计数均为 0：继续检查 unsupported/custom/proxy primitive，而不是把问题归因给 layout/block/xref。

### 仍保持的安全边界

- clip commands 在 reader-independent proxy model 能精确表达前继续 fail closed。
- 非均匀/非平面 proxy model transform 不做近似降级。
- Proxy Graphics 只承担显示 fallback；native Tianzheng semantic evidence policy、RawFieldMapping 门槛及剩余语义 gate 不因此降低。
- Xref profiler 不读取、拼接或自动加载外部路径；外部参照恢复必须由显式的宿主资源解析策略处理。

### 后续优先级

1. 用同一批真实建筑图重新对照并运行 source profiler，量化匿名块、角度、proxy transform 与 Paper Space/Xref 对图框、门窗、轴号缺失的贡献；
2. 对 `TABLE` cache block 做真实 Reader→CadDocument→Scene 回归，确认缓存几何的恢复率；
3. 设计显式、可控的 Xref resolver contract，不在内核里偷偷访问任意外部路径；
4. 按真实图纸出现频率补齐 3-point circle/arc 等高价值 proxy primitive；
5. 只有在可验证地表达 clip 语义后再开放 proxy clip；
6. Tianzheng native semantics 继续按独立证据策略推进，不以显示结果反推 proprietary raw fields。

## English

v0.12.1 is driven by side-by-side comparison against real architectural drawings after v0.12.0 integration. The visible gaps include title frames, grid/axis annotations, Tianzheng door/window objects, and generic CAD rotation/block-expansion fidelity.

Completed work retains ACadSharp-decoded proxy text and safe lightweight polylines as display-only fallbacks, supports balanced planar model-transform stacks for translation/rotation/uniform scale, and keeps clipping, non-uniform/non-planar transforms, malformed matrices, and unbalanced stacks fail-closed. Generic text, dimension text, and ellipse pivot transforms were corrected so their anchors remain fixed during rotation.

Anonymous block handling was corrected: only internal `*Model_Space` / `*Paper_Space...` records are filtered, while `*U###` and other anonymous definitions are retained. A real DXF regression verifies a 90-degree anonymous-block INSERT through Reader to Scene. Import metadata exposes anonymous-block and paper-space/layout counts for title-frame diagnosis.

ACadSharp already converts `IsAngle` values to radians at its reader boundary. CadCore no longer applies a second degree-to-radian conversion; ARC, TEXT/MTEXT, ATTRIBUTE, INSERT, HATCH and viewport-twist paths share the corrected radians contract and are covered by real-DXF regressions.

`CadSourceContentProfiler` now adds privacy-safe source-structure diagnosis for Model Space, Paper Space, viewports, anonymous/dynamic blocks, TABLE cache blocks, and Xref definitions/references including unloaded or empty definitions. It exposes counts and boolean conditions only; drawing text, handles, Xref names and file paths are not retained. `TableEntity` is treated according to ACadSharp's actual `Insert`-derived anonymous-cache model, while Xrefs with no local cached geometry remain explicit dependencies rather than fabricated drawable content.

Proxy fallback remains display-only and does not weaken native Tianzheng semantic evidence requirements. Next work is real-drawing source profiling, TABLE cache Reader-to-Scene validation, an explicit host-controlled Xref resolver contract, additional high-value proxy primitives, and clip support only after its semantics can be represented exactly.

## 日本語

v0.12.1 は v0.12.0 を実建築図面へ接続した後の比較表示を起点とする fidelity patch line です。図枠/タイトル欄、通り芯・軸番号、天正建具に加え、一般 CAD オブジェクトの回転とブロック展開精度も対象にしています。

ACadSharp が解析済みの proxy text と安全な lightweight polyline を display-only fallback として保持し、平行移動・回転・等比拡大縮小に限定した平衡 planar model-transform stack を実装しました。clip、非等方/非平面 transform、不正行列、stack 不整合は引き続き fail closed です。通常文字・寸法文字・回転楕円の pivot 行列も修正し、回転時に基準点が移動しないことを回帰テストで固定しています。

匿名ブロックの扱いも修正し、`*Model_Space` / `*Paper_Space...` の内部 record のみを除外し、`*U###` などの匿名定義は保持します。実 DXF で 90 度回転した匿名 block INSERT が Reader から Scene まで正しく展開されることを確認しています。さらに匿名ブロック数、Paper Space entity 数、viewport 数などの診断 metadata を追加しました。

ACadSharp は reader 境界で `IsAngle` を既に radians へ変換します。CadCore 側の二重変換を除去し、ARC、TEXT/MTEXT、ATTRIBUTE、INSERT、HATCH、Viewport twist を共通の radians 契約に統一しました。

さらに `CadSourceContentProfiler` を追加し、Model Space、Paper Space、viewport、匿名/動的 block、TABLE cache block、Xref の定義・参照・unloaded・空 cache 状態を件数だけで診断できるようにしました。図面文字列、handle、Xref 名、外部ファイル path は保持しません。`TableEntity` は ACadSharp の実際の `Insert` 派生・匿名 cache 構造に従って扱い、ローカル geometry を持たない Xref は偽の表示を作らず依存関係として報告します。

Proxy Graphics はあくまで表示 fallback であり、native Tianzheng semantic の証拠基準は緩和しません。次は実図面での source profile、TABLE cache の Reader→Scene 回帰、host 管理下の明示的 Xref resolver、高頻度 proxy primitive、そして正確に表現可能になった段階での clip 対応を優先します。
