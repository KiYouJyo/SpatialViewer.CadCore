# v0.12.1 Safe ObjectARX Proxy Clip

## 中文

### 目标

v0.12.1 为 ObjectARX Proxy Graphics 增加一个**可证明正确的二维裁剪子集**。该能力只用于第三方 CAD 自定义对象的显示回退，不代表 CadCore 已解码天正或其他厂商对象的原生语义。

核心原则是不把未知裁剪近似成一个“看起来差不多”的矩形：只有 CadCore 能从 ACadSharp 已解析字段完整表达的 clip 才会进入 Scene；其余情况整条 proxy stream fail closed。

### Scene / Rendering 契约

既有 `SceneNode.ClipBounds` 保持不变，继续表示**最终世界坐标中的轴对齐矩形**，主要服务 layout viewport。

Proxy Clip 使用独立的变换感知链路：

1. `SceneNode.LocalClipPolygon` 保存节点局部坐标中的二维边界；
2. Scene 展平时将其随完整父级/块变换转换为世界多边形；
3. 嵌套 clip 不做 bbox 合并，而是作为 `SceneItem.ClipPolygons` stack 独立保留；
4. spatial bounds 只使用各多边形 bbox 做保守裁减；
5. hit-test 对每个实际多边形逐一检查，因此 bbox 内但 polygon 外的区域不可选中；
6. `RenderPreparation` 原样传递 clip stack；
7. Win2D 为矩形和每层多边形分别建立嵌套 `CanvasActiveLayer`，由 Direct2D 完成真实交集裁剪。

因此旋转 block / model transform 下的 clip 仍随几何一起旋转，不会被错误转换成世界轴对齐矩形。

### 当前接受的 ObjectARX 子集

`ProxyPushClip` / `ProxyPopClip` 仅在以下条件全部满足时翻译为 reader-independent `CadProxyClipGroup`：

- push/pop scope 完整平衡，并允许嵌套；
- `FrontClipOn == false` 且 `BackClipOn == false`；
- extrusion 为平面的 `+Z`；
- `ClipBoundaryOrigin` 为原点；
- `ClipBoundaryTransformMatrix` 为 identity；
- `InverseBlockTransformMatrix` 必须与当前已验证二维 model transform 的逆矩阵一致；
- model transform 本身必须继续满足既有 proxy mapper 的 planar proper-similarity 约束；
- boundary 所有坐标有限、面积有效且非退化；
- 两点 boundary 按 Autodesk ObjectARX 约定展开为矩形；
- 三点及以上 boundary 作为实际 polygon 保留；
- `DrawBoundary` 只生成闭合描边，不伪造填充。

model transform 可以在 clip pop 之前先 pop；clip polygon 与子几何均在各自出现时固化到同一 reader-independent object coordinate system，因此作用域仍保持 ObjectARX stack 顺序。

### Fail-closed 边界

以下情况不会尝试近似显示：

- front/back 3D clipping plane；
- 非 `+Z` extrusion；
- 非零 clip origin；
- 非 identity clip-space matrix；
- inverse block transform 与当前 model transform 不一致；
- 非均匀缩放、reflection、透视或 XY/Z coupling；
- 非有限、重复退化、零面积或点数不足的 boundary；
- 未配对的 push/pop clip 或 model-transform stack。

若 stream 不含 clip command，则新的 clip-aware mapper 直接委托给既有 `ACadSharpProxyGraphicsMapping`，普通 Proxy Circle / Arc / Text / Polyline 等行为不变。既有 mapper 本身仍对 clip command fail closed，避免其他调用方绕过完整 clip contract。

### 证据边界

实现依据 Autodesk ObjectARX Graphics Interface 的公开 clip-boundary 示例：示例明确说明两点 boundary 作为 rectangle、三点形成 triangle，并展示 `xToClipSpace = identity`、关闭前后 Z clip、以及 inverse block-reference transform 与当前 model transform 相互对应的典型对象空间裁剪流程。

该证据只用于公开的 Proxy Graphics 显示协议，不用于推断任何天正私有 DXF 字段。

参考：<https://help.autodesk.com/cloudhelp/2025/JPN/OARX-DevGuide/files/GUID-84B4458C-50DA-4D98-8B7B-0996D18C6D13.htm>

---

## English

v0.12.1 adds an **exact, evidence-backed 2D subset** of ObjectARX Proxy Graphics clipping. This is display fallback only; it does not claim native Tianzheng or other vendor semantics.

Existing final-world axis-aligned `SceneNode.ClipBounds` remains unchanged for layout viewports. Proxy clipping instead uses `LocalClipPolygon`, which is transformed through the full scene hierarchy into an inherited stack of final-world polygons. Spatial bounds use polygon bounding boxes conservatively, hit-testing checks the actual polygons, `RenderPreparation` preserves the stack, and Win2D applies each rectangle/polygon as nested active layers so Direct2D performs the real intersection. Rotated block/model transforms therefore keep rotated clip polygons rather than collapsing them to axis-aligned bounds.

The accepted `ProxyPushClip` / `ProxyPopClip` subset requires balanced scopes, no front/back Z planes, +Z extrusion, zero boundary origin, identity clip-space transform, and an inverse block transform that matches the inverse of the current already-safe planar model transform. Boundaries must be finite and non-degenerate. Two points expand to the ObjectARX rectangle form; three or more points remain polygons. `DrawBoundary` is retained as a closed outline without inventing fill.

3D planes, non-zero origins, non-identity clip-space transforms, inverse mismatches, non-uniform/reflected/perspective transforms, malformed boundaries and unbalanced state stacks fail closed for the proxy stream. Clip-free streams delegate directly to the established proxy mapper, preserving previous Circle/Arc/Text/Polyline behavior.

The implementation boundary follows Autodesk's public ObjectARX clip-boundary example, including its two-point rectangle / three-point triangle rule, identity clip-space example, disabled front/back Z clipping and inverse model/block transform relationship. It does not infer proprietary CAD fields.

Reference: <https://help.autodesk.com/cloudhelp/2025/JPN/OARX-DevGuide/files/GUID-84B4458C-50DA-4D98-8B7B-0996D18C6D13.htm>

---

## 日本語

v0.12.1 では ObjectARX Proxy Graphics に対して、**公開仕様で検証可能な 2D clip の範囲だけ**を追加します。これは custom object の表示 fallback であり、天正その他ベンダー固有 object の native semantics を解読したことを意味しません。

既存の `SceneNode.ClipBounds` は layout viewport 用の最終 world 座標・軸平行 rectangle として維持します。Proxy Clip は別経路で `LocalClipPolygon` を保持し、親 node / block transform を通して world polygon に変換します。nested clip は bbox 一個に潰さず stack のまま保持し、spatial bounds は保守的な bbox、hit-test は実 polygon、Rendering は nested Win2D active layer を使用します。そのため回転 block 内でも clip polygon は geometry と一緒に回転します。

受理する `ProxyPushClip` / `ProxyPopClip` は、scope が平衡していること、front/back Z clip が無効、extrusion が +Z、boundary origin が原点、clip-space matrix が identity、inverse block transform が現在の安全な planar model transform の逆と一致することを必須とします。boundary は有限かつ非退化である必要があります。2 点は ObjectARX の rectangle、3 点以上は polygon として保持し、`DrawBoundary` は fill を作らず閉じた outline として扱います。

3D clipping plane、非ゼロ origin、非 identity clip-space、inverse mismatch、non-uniform scale、reflection、perspective、破損 boundary、未対応の push/pop はすべて fail closed とします。clip command を含まない stream は従来の proxy mapper にそのまま委譲するため、既存の Circle / Arc / Text / Polyline の挙動は変更しません。

根拠は Autodesk 公開の ObjectARX clip-boundary example です。2 点 rectangle / 3 点 triangle、identity clip space、front/back Z clip 無効、model/block inverse transform の関係を実装境界として使用し、独自 CAD field の推測には使用しません。

Reference: <https://help.autodesk.com/cloudhelp/2025/JPN/OARX-DevGuide/files/GUID-84B4458C-50DA-4D98-8B7B-0996D18C6D13.htm>
