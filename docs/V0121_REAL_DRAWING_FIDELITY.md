# v0.12.1 real-drawing fidelity work

## 中文

本补丁线由 v0.12.0 接入真实建筑图后的显示对照驱动。首批可见差异集中在：图框/标题栏、轴网轴号、门窗等 Tianzheng/custom-object 构件。

### Phase 1 — safe proxy coverage

- 将 ACadSharp 已明确解析出的 `ProxyText` / `ProxyUnicodeText` 保留为 reader-independent `CadProxyText`，并进入 `TextGeometry` 显示 fallback。
- 保留代理文字的起点、字高、方向、宽度系数与倾斜角；这只是 Proxy Graphics presentation，不提升为 Tianzheng native semantic。
- `ProxyPolylineWithNormal` 先检查法向量；只有 XY 平面对象才允许降为二维 polyline，避免其派生类型意外落入无 normal guard 的 `ProxyPolyline` 分支。
- 负 Z proxy text、非平面 polyline、无效字高/方向继续 fail closed。
- model-transform / clip state command 仍保持整条 proxy stream fail closed，直到状态机能够安全实现。

### 后续优先级

1. safe `ProxyLwPolyine` / bulge / closed-state fallback；
2. 可验证的平面 model-transform push/pop 状态机，避免带变换的门窗/轴号整条 proxy stream 被丢弃；
3. title-block/layout/unsupported-entity 诊断，区分 Paper Space、块/Xref、TABLE 与 custom/proxy 缺失；
4. 根据真实图纸继续补齐 3-point circle/arc 等高价值 proxy primitive；
5. native Tianzheng semantic evidence policy 不因显示 fallback 扩展而降低标准。

## English

v0.12.1 is driven by side-by-side rendering of real architectural drawings after v0.12.0 integration. The first visible gaps cluster around title frames, grid/axis annotations, and Tianzheng door/window components.

Phase 1 retains ACadSharp-decoded `ProxyText` / `ProxyUnicodeText` as display-only `CadProxyText`, carries origin/height/direction/width/oblique presentation into `TextGeometry`, and fixes `ProxyPolylineWithNormal` so non-planar instances cannot fall through to the unguarded base-polyline mapping. Unsafe normals and malformed text still fail closed. Stateful model transforms and clipping remain blocked until implemented safely.

Next priorities are safe lightweight-polyline fallback, a verified planar transform stack, title-block/layout diagnostics, and additional high-value proxy primitives. Proxy fallback improvements do not weaken the native Tianzheng semantic evidence policy.

## 日本語

v0.12.1 は、v0.12.0 を実建築図面に接続した際の比較表示を起点にした fidelity patch line です。最初の差分は図枠/タイトル欄、通り芯・軸番号、天正の建具に集中しています。

Phase 1 では ACadSharp が既に解析済みの `ProxyText` / `ProxyUnicodeText` を display-only `CadProxyText` として保持し、原点・文字高・方向・幅係数・傾斜角を `TextGeometry` fallback に渡します。また `ProxyPolylineWithNormal` は normal を先に検証し、非平面 object が base `ProxyPolyline` branch に誤って fall-through しないようにします。危険な normal、壊れた text、stateful model-transform / clip は引き続き fail closed です。

次は safe lightweight-polyline fallback、検証可能な planar transform stack、図枠/layout 診断、追加の高価値 proxy primitive を優先します。display fallback の拡張によって native Tianzheng semantic evidence policy を緩和することはありません。
