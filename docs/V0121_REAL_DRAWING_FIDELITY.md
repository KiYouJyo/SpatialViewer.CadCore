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

### 仍保持的安全边界

- clip commands 在 reader-independent proxy model 能精确表达前继续 fail closed。
- 非均匀/非平面 proxy model transform 不做近似降级。
- Proxy Graphics 只承担显示 fallback；native Tianzheng semantic evidence policy、RawFieldMapping 门槛及剩余语义 gate 不因此降低。

### 后续优先级

1. 用同一批真实建筑图重新对照，量化匿名块、角度和 proxy transform 修复对门窗/轴号/图框的实际恢复率；
2. 继续追 title-block/layout 路径，区分 Paper Space、Xref、TABLE 与 unsupported/custom/proxy 缺失；
3. 按真实图纸出现频率补齐 3-point circle/arc 等高价值 proxy primitive；
4. 只有在可验证地表达 clip 语义后再开放 proxy clip；
5. Tianzheng native semantics 继续按独立证据策略推进，不以显示结果反推 proprietary raw fields。

## English

v0.12.1 is driven by side-by-side comparison against real architectural drawings after v0.12.0 integration. The visible gaps include title frames, grid/axis annotations, Tianzheng door/window objects, and generic CAD rotation/block-expansion fidelity.

Completed work now retains ACadSharp-decoded proxy text and safe lightweight polylines as display-only fallbacks, supports balanced planar model-transform stacks for translation/rotation/uniform scale, and keeps clipping, non-uniform/non-planar transforms, malformed matrices, and unbalanced stacks fail-closed. Generic text, dimension text, and ellipse pivot transforms were corrected so their anchors remain fixed during rotation.

Anonymous block handling was also corrected: only internal `*Model_Space` / `*Paper_Space...` records are filtered, while `*U###` and other anonymous definitions are retained. A real DXF regression verifies a 90-degree anonymous-block INSERT through Reader to Scene. Import metadata now exposes anonymous-block and paper-space/layout counts for title-frame diagnosis.

ACadSharp already converts `IsAngle` values to radians at its reader boundary. CadCore no longer applies a second degree-to-radian conversion; ARC, TEXT/MTEXT, ATTRIBUTE, INSERT, HATCH and viewport-twist paths share the corrected radians contract and are covered by real-DXF regressions.

Proxy fallback remains display-only and does not weaken native Tianzheng semantic evidence requirements. Next work is real-drawing remeasurement, title-block/layout/Xref/TABLE diagnosis, additional high-value proxy primitives, and clip support only after its semantics can be represented exactly.

## 日本語

v0.12.1 は v0.12.0 を実建築図面へ接続した後の比較表示を起点とする fidelity patch line です。図枠/タイトル欄、通り芯・軸番号、天正建具に加え、一般 CAD オブジェクトの回転とブロック展開精度も対象にしています。

ACadSharp が解析済みの proxy text と安全な lightweight polyline を display-only fallback として保持し、平行移動・回転・等比拡大縮小に限定した平衡 planar model-transform stack を実装しました。clip、非等方/非平面 transform、不正行列、stack 不整合は引き続き fail closed です。通常文字・寸法文字・回転楕円の pivot 行列も修正し、回転時に基準点が移動しないことを回帰テストで固定しています。

匿名ブロックの扱いも修正し、`*Model_Space` / `*Paper_Space...` の内部 record のみを除外し、`*U###` などの匿名定義は保持します。実 DXF で 90 度回転した匿名 block INSERT が Reader から Scene まで正しく展開されることを確認しています。さらに匿名ブロック数、Paper Space entity 数、viewport 数などの診断 metadata を追加しました。

ACadSharp は reader 境界で `IsAngle` を既に radians へ変換します。CadCore 側の二重変換を除去し、ARC、TEXT/MTEXT、ATTRIBUTE、INSERT、HATCH、Viewport twist を共通の radians 契約に統一しました。

Proxy Graphics はあくまで表示 fallback であり、native Tianzheng semantic の証拠基準は緩和しません。次は実図面での再計測、図枠/layout/Xref/TABLE の切り分け、高頻度 proxy primitive の追加、そして正確に表現可能になった段階での clip 対応を優先します。
