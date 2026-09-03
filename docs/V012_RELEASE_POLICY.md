# v0.12 release policy — practical Tianzheng Architecture 2D compatibility

> Effective for the v0.12.0 release on 2026-09-03. This document changes **release eligibility**, not the evidence standard required to claim native Tianzheng semantics.

## 中文

### 发布范围调整

v0.12.0 的正式发布目标调整为 **Tianzheng Architecture 2D practical compatibility（天正建筑二维实用兼容）**。

此前 `V012_TIANZHENG_ARCHITECTURE.md` 将所有历史核心类别至少达到 `Partial semantic` 作为产品升版前置条件。该规则仍保留为“native semantic 完成度”的研究与验收标准，但**不再作为 v0.12.0 产品发布的阻塞条件**。

对于发布资格，本文件覆盖此前的 blocking rule：只要 CadCore 能够安全识别、保留并尽可能显示天正二维对象，未知语义严格 fail closed，并且完整 CI / ABI / Host Contract 通过，即可发布 v0.12.0。

### v0.12.0 可以宣称的内容

- 识别和保留 `TCH_*` / Tianzheng custom-object identity、CLASSES metadata 与 raw evidence；
- 在可信 Proxy Graphics 存在时使用保守的二维 fallback；
- 已有证据支持的 native semantics：`TCH_WALL` Drawable2D，以及 opening、space、elevation、drawing-name、column、line/rect stair 的 Partial semantics；
- 保持 unknown/custom objects 不被 Reader 静默丢弃；
- 保持 malformed / mismatched / unsupported evidence fail closed；
- 提供 privacy-safe corpus、A/B diff、atomic `TCHRUN` 与 native export probe 供后续实图驱动完善。

### v0.12.0 明确不宣称

以下对象在 v0.12.0 中仍可能依赖 opaque/raw/proxy preservation，而不是 native semantic decoding：

- `TCH_AXIS_LABEL` family；
- `TCH_DRAWINGINDEX` / `TCH_INDEXPOINTER` family；
- `TCH_DIMENSION2` 及现代 `TCH_DIMENSION` identity-drift family。

它们的存在不再阻止 v0.12.0 发布，但任何后续“native semantic 支持”声明仍必须满足原 evidence policy：可信 raw-field → semantic mapping、真实 Reader regression、至少必要的独立 observation，以及 fail-closed negative tests。不得因为本次发布策略调整而使用 group-number 猜测、跨类类推或 identity alias 伪造支持。

### 后续开发方式

v0.12.0 发布后，优先使用 SpatialViewer 中的真实图纸显示结果来决定 0.12.x 修复顺序：不可见、位置错误、颜色/文字/比例异常、代理图形缺失、性能回归等实际问题优先。更深层的 Tianzheng semantic decoding 持续进入 0.12.x / 0.13+，不再阻塞当前实用兼容版本交付。

### 兼容契约

- Product/File/Informational version: `0.12.0`
- CLR ABI: `1.0.0.0`
- Host Contract: `SpatialViewer.CadHost >=1.0.0,<2.0.0`
- Release manifest schema: `2`

---

## English

### Release-scope adjustment

v0.12.0 is released as **Tianzheng Architecture 2D practical compatibility**.

`V012_TIANZHENG_ARCHITECTURE.md` originally required every historically scoped core category to reach at least Partial native semantics before the product version could advance. That matrix remains the standard for measuring **native-semantic completion**, but it no longer blocks the v0.12.0 product release.

For release eligibility, this document supersedes that blocking rule. v0.12.0 may ship when CadCore safely identifies and preserves Tianzheng 2D objects, renders conservative fallbacks where evidence permits, fails closed for unsupported semantics, and passes the full CI / ABI / Host Contract checks.

v0.12.0 includes evidence-backed native semantics for walls and selected opening, space, elevation, drawing-name, column and stair properties, plus custom-object/raw/proxy preservation and the privacy-safe research toolchain.

Axis-label, drawing-index/index-pointer and Tianzheng dimension families may still rely on opaque/raw/proxy preservation. Their incomplete native decoding does not block this release, but any future native-semantic claim still requires the unchanged evidence policy: trustworthy raw-field mapping, real Reader regression, independent observations where required, and fail-closed negative coverage. Release scope must never be used to justify guessed group numbers, cross-class inference, or silent `TCH_DIMENSION`/`TCH_DIMENSION2` aliasing.

After v0.12.0, real SpatialViewer drawing behavior drives 0.12.x priorities: missing display, wrong placement, color/text/scale fidelity, proxy gaps and performance regressions take priority. Deeper Tianzheng semantic decoding continues incrementally in 0.12.x / 0.13+.

Compatibility contract remains CLR ABI `1.0.0.0`, `SpatialViewer.CadHost >=1.0.0,<2.0.0`, manifest schema `2`.

---

## 日本語

### リリース範囲の変更

v0.12.0 は **Tianzheng Architecture 2D practical compatibility（天正建築 2D 実用互換）** として正式リリースします。

従来の `V012_TIANZHENG_ARCHITECTURE.md` は、歴史的に対象とした全カテゴリが最低 Partial native semantic に到達することを product version 更新の条件としていました。この matrix は今後も **native semantic 完成度**の研究・検証基準として維持しますが、v0.12.0 のリリース自体を阻止する条件にはしません。

リリース可否については本書が従来の blocking rule を上書きします。Tianzheng 2D object を安全に識別・保持し、根拠がある場合のみ保守的に表示し、未対応 semantic は fail closed とし、完全な CI / ABI / Host Contract を通過すれば v0.12.0 を公開できます。

v0.12.0 では wall と一部 opening / space / elevation / drawing-name / column / stair property に evidence-backed native semantic があり、それ以外も custom identity / raw / proxy preservation と privacy-safe research tooling を利用できます。

`TCH_AXIS_LABEL`、`TCH_DRAWINGINDEX` / `TCH_INDEXPOINTER`、`TCH_DIMENSION2` および modern `TCH_DIMENSION` family は opaque/raw/proxy preservation のままの場合があります。これらは v0.12.0 release を阻止しませんが、将来 native semantic 対応を宣言するには従来どおり exact raw-field evidence、real Reader regression、必要な independent observation、fail-closed negative test が必須です。group number の推測、cross-class inference、dimension identity の silent alias は禁止したままです。

v0.12.0 後は SpatialViewer での実図面表示を 0.12.x の優先順位に使用します。非表示、位置、色、文字、scale、proxy gap、performance regression を先に修正し、より深い Tianzheng semantic decoding は 0.12.x / 0.13+ で継続します。

互換契約は CLR ABI `1.0.0.0`、`SpatialViewer.CadHost >=1.0.0,<2.0.0`、manifest schema `2` を維持します。
