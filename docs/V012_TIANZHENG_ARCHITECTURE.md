# v0.12 Tianzheng Architecture 2D native-semantic acceptance matrix

> Historical/native-semantic completeness matrix for the v0.12 Tianzheng scope. It is **not a product-release gate**: [V012_RELEASE_POLICY.md](V012_RELEASE_POLICY.md) superseded the original shipping blocker before v0.12.0 was released. Current CadCore is v0.12.6.

## 中文

### 目标与发布原则

v0.12 的目标沿用 v0.11.0 已公开的后续范围：在 v0.11 的自定义对象保留、Proxy Graphics fallback 与 raw evidence 基础上，为天正建筑二维对象建立**证据约束的原生参数语义**。本里程碑覆盖墙、柱、门窗、轴网、楼梯、房间以及标高/索引/尺寸类对象。

以下能力**不能单独视为 v0.12 原生语义验收完成**：

- 仅识别 `TCH_*` 类名或 CLASSES 表；
- 仅保留 unknown/proxy entity；
- 仅能显示 Proxy Graphics；
- 仅捕获 raw DXF / raw DWG evidence；
- 仅生成 schema corpus / fingerprint；
- 根据“看起来像宽高/面积/角度”的数值位置猜测字段含义。

作为 **Tianzheng native-semantic completeness** 的完整验收标准，所有 v0.11.0 历史列入 v0.12 的核心对象类别仍应至少达到 **Partial semantic**，并满足真实 Reader 回归与 fail-closed 条件。证据不足的类别继续标记为 semantic blocker，但依据 `V012_RELEASE_POLICY.md`，它们不再阻塞 0.12.x 产品发布。

### 状态定义

| 状态 | 含义 | 是否计入 v0.12 原生语义验收 |
| --- | --- | --- |
| **Drawable2D** | 已恢复足够且可信的二维参数，可直接生成 native Scene geometry | 是 |
| **Partial** | 已恢复一组可明确命名的原生参数，但不足以安全重建完整二维几何 | 是，但仍必须明确 non-claims |
| **Evidence-only** | 已有类名、raw payload、公开属性目标或部分 entget 证据，但字段语义映射尚不足 | 否，继续阻塞对应类别 |
| **Preserved only** | 只能保留对象身份/raw/proxy，尚无可信原生语义 | 否 |

`CadCustomSemanticCoverage.Partial` 与 `Drawable2D` 是代码层正式状态。`Evidence-only` / `Preserved only` 是里程碑管理状态，不会伪装成已解码 semantic。

### 当前对象矩阵

| 对象/类别 | 当前状态 | 已验证内容 | 仍未声明支持 | v0.12 gate |
| --- | --- | --- | --- | --- |
| `TCH_WALL` 墙 | **Drawable2D** | straight wall 起终点、左右厚度；可选标高/高度；direct 与 packed profile；native wall outline | 曲墙、全部墙型属性、完整 BIM 约束 | ✅ 已满足当前二维语义 gate |
| `TCH_OPENING` 门窗洞口 | **Partial** | group 10/20 anchor、可选 Z；opening→host wall relationship | 宽、高、窗台/离地高、类型、编号、开启方向、框料、native opening geometry | ✅ 已满足 partial gate；完整几何仍待证据 |
| `TCH_SPACE` 房间 | **Partial** | `TDbSpace` guard；anchor、名称、编号 | 房间边界、面积、体积、周长、踢脚线、墙/门/窗面积等数值字段映射 | ✅ 已满足 partial gate；计算属性保持 raw-only |
| `TCH_ELEVATION` 标高 | **Partial** | `TDbSymbElevation` guard；anchor、标高文本、可选出图比例 | 符号/箭头几何、方向、文字高度、样式 | ✅ 已满足 partial gate |
| `TCH_COLUMN` 柱 | **Evidence-only** | 已知目标属性包括截面形状、转角、截面宽高、柱高；类型已被公开实现登记 | 可信 raw group → 属性映射、reader regression、native semantic | ❌ 阻塞 |
| 轴网 / 轴号（如 `TCH_AXIS_LABEL` 等） | **Evidence-only** | 已知 Tianzheng/custom-object 类型存在 | 轴线/轴号稳定 raw schema、几何与编号字段映射 | ❌ 阻塞 |
| `TCH_LINESTAIR` / `TCH_RECTSTAIR` 楼梯 | **Evidence-only** | 已知目标属性包括踏步数、踏步高宽、梯高、梯段/梯井宽等；类型已登记 | 可信 raw group → 参数映射、reader regression | ❌ 阻塞 |
| 索引/图名类（含 drawing/index pointer family） | **Evidence-only** | 已发现相关自定义类型/命令证据 | 稳定 entget schema、文本/编号/anchor 字段映射 | ❌ 阻塞 |
| 天正尺寸类（如 `TCH_DIMENSION2` family） | **Evidence-only** | 已发现相关自定义类型登记 | 稳定 entget schema、尺寸定义点/文本/样式参数映射 | ❌ 阻塞 |
| `TCH_ARROW` | **Evidence-only** | 已有公开 `TDbSymbArrow` entget，含 point 10/11 与 group 1 等结构 | point 10/11 的严格角色、70/41 的语义、可验证 native geometry | 辅助标注研究，不替代索引/尺寸 gate |
| `TCH_MULTILEADER` | **Evidence-only** | 已有公开 `TDbSymbMultiLeader` entget 结构 | point/group 角色与 native leader geometry | 辅助标注研究，不替代索引/尺寸 gate |
| 其他 `TCH_*` | **Preserved only / Evidence-only** | custom class/object identity、raw payload、proxy fallback（若可用） | 原生参数语义 | 不计为已支持 |

### 每个 native semantic profile 的最低验收条件

1. **强对象身份**：必须使用明确的 `TCH_*` identity，并优先使用公开 subclass marker / 已验证 C++ class 作为 schema guard。
2. **字段证据**：字段名称必须来自公开 entget/AutoLISP、可验证样本、官方/专利属性定义与 raw layout 的可靠对应；不得仅凭数值范围猜测。
3. **Reader 回归**：至少有 text-DXF Reader → CadCore 的真实读取边界测试；能够获得合法真实样本时再扩展 DWG 变体 corpus。
4. **Fail closed**：缺 subclass、字段缺失、非数值/非有限值、truncated payload、第三方近似对象必须拒绝 native semantic，而不是生成“看起来合理”的结果。
5. **显示分离**：`Partial` semantic 不得自动压制 Proxy Graphics；只有 `Drawable2D` 才能进入明确的 native geometry path。
6. **Non-claims**：每个 partial profile 必须列出未恢复字段，防止后续把未知 group code 顺手命名。
7. **ABI/Host 不漂移**：CLR ABI 保持 `1.0.0.0`，Host Contract 保持 `SpatialViewer.CadHost >=1.0.0,<2.0.0`，除非另有独立兼容性里程碑。

### 研究与样本工具链

v0.12 已具备隐私安全的 Tianzheng schema corpus 工作流，可在不导出图纸内容的情况下聚合：class identity、group-code signature、subclass signature、reference multiplicity、truncation/native/proxy/raw-DWG 覆盖率以及 opening→wall relationship coverage。

Corpus JSON 支持验证后的 `ToJson` / `FromJson` / `MergeJson`，但 corpus 的存在只用于**发现和验证 decoder profile**，不等同于 native semantic 支持。

### 当前发布结论

- 产品版本继续保持 `0.11.0`。
- v0.12 **尚未达到正式发布条件**。
- 当前主要阻塞项：**柱、轴网、楼梯、索引/图名、天正尺寸**的 evidence-backed semantic profile。
- 在公开证据不足时，优先收集匿名 schema corpus / 真实 entget，而不是编写猜测 decoder。

---

## English

### Goal and release rule

v0.12 keeps the follow-up scope declared by v0.11.0: build **evidence-gated native parameter semantics** for Tianzheng Architecture 2D objects on top of custom-object preservation, Proxy Graphics fallback, and raw evidence capture. The milestone covers walls, columns, openings, grids, stairs, rooms, and elevation/index/dimension annotations.

The following do **not** count as completed native-semantic support on their own: recognizing a `TCH_*` class, preserving an unknown/proxy entity, drawing Proxy Graphics, capturing raw DXF/DWG evidence, producing a schema corpus/fingerprint, or guessing field meanings from plausible numeric values.

Before Product/File/Informational version advances from `0.11.0` to `0.12.0`, every core category promised for v0.12 by the v0.11.0 release note must reach at least **Partial semantic** with a real Reader regression and fail-closed behavior. Categories without enough evidence continue to block release; the historical scope is not silently reduced to pass the gate.

### Status levels

| Status | Meaning | Counts toward the v0.12 native-semantic gate |
| --- | --- | --- |
| **Drawable2D** | Enough trustworthy 2D parameters are recovered to build native Scene geometry | Yes |
| **Partial** | A clearly named native parameter subset is recovered, but complete 2D geometry cannot yet be reconstructed safely | Yes, with explicit non-claims |
| **Evidence-only** | Class/raw/public-property evidence exists, but raw-field semantics are not yet reliable enough | No |
| **Preserved only** | Identity/raw/proxy can be preserved, but no trustworthy native semantics exist yet | No |

`CadCustomSemanticCoverage.Partial` and `Drawable2D` are code-level states. Evidence-only and Preserved-only remain milestone-management states and must not be represented as decoded semantics.

### Current matrix

| Object/category | Status | Verified | Explicitly not claimed | v0.12 gate |
| --- | --- | --- | --- | --- |
| `TCH_WALL` | **Drawable2D** | straight-wall start/end, left/right widths, optional elevation/height, direct and packed profiles, native outline | curved walls, every wall type/property, complete BIM constraints | ✅ current 2D semantic gate met |
| `TCH_OPENING` | **Partial** | anchor XY, optional Z, opening→host-wall relationship | width, height, sill/clearance, type, number, swing/frame, native opening geometry | ✅ partial gate met |
| `TCH_SPACE` | **Partial** | `TDbSpace`, anchor, room name, room number | boundary, area, volume, perimeter, skirting/wall/door/window-area mappings | ✅ partial gate met |
| `TCH_ELEVATION` | **Partial** | `TDbSymbElevation`, anchor, elevation text, optional plot scale | symbol/arrow geometry, direction, text height, style | ✅ partial gate met |
| `TCH_COLUMN` | **Evidence-only** | target attributes and public type registration exist | trustworthy raw-group mapping and native semantic profile | ❌ blocking |
| grid / axis-label family | **Evidence-only** | Tianzheng custom-object/type evidence exists | stable raw schema and axis/label mapping | ❌ blocking |
| `TCH_LINESTAIR` / `TCH_RECTSTAIR` | **Evidence-only** | target stair attributes and type registration exist | trustworthy raw-group mapping and reader regression | ❌ blocking |
| index/drawing-title family | **Evidence-only** | custom-type/command evidence exists | stable entget schema and text/number/anchor mapping | ❌ blocking |
| Tianzheng dimension family (`TCH_DIMENSION2` etc.) | **Evidence-only** | custom-type registration exists | stable schema and definition-point/text/style mapping | ❌ blocking |
| `TCH_ARROW` | **Evidence-only** | public `TDbSymbArrow` entget structure exists | strict point roles and 70/41 semantics/native geometry | auxiliary research only |
| `TCH_MULTILEADER` | **Evidence-only** | public `TDbSymbMultiLeader` entget structure exists | strict point/group roles and native leader geometry | auxiliary research only |
| other `TCH_*` | **Preserved only / Evidence-only** | identity/raw/proxy when available | native parameter semantics | not counted |

### Minimum acceptance for each semantic profile

1. Strong object identity and preferably a verified subclass/C++ schema guard.
2. Field names backed by public entget/AutoLISP, verifiable samples, or a reliable correspondence between documented properties and raw layout; no value-range guessing.
3. At least one real text-DXF Reader → CadCore regression.
4. Fail closed for missing schema markers, malformed or non-finite values, truncated payloads, and unrelated vendor objects.
5. Partial semantics must not suppress Proxy Graphics; only Drawable2D semantics enter a native-geometry path.
6. Every partial profile documents explicit non-claims.
7. CLR ABI remains `1.0.0.0` and Host Contract remains `SpatialViewer.CadHost >=1.0.0,<2.0.0` unless a separate compatibility milestone changes them.

### Research/corpus tooling

The privacy-safe Tianzheng schema corpus aggregates structural signatures and coverage statistics without exporting drawing contents. Validated `ToJson` / `FromJson` / `MergeJson` support allows reports from independent samples to be combined. Corpus support is a **decoder-research tool**, not a substitute for native semantics.

### Current release conclusion

- Product version remains `0.11.0`.
- v0.12.x product releases are no longer gated by this matrix.
- Current blockers: evidence-backed semantics for **columns, grids, stairs, index/drawing-title objects, and Tianzheng dimensions**.
- When evidence is insufficient, collect anonymous schema/real entget evidence instead of implementing speculative decoders.

---

## 日本語

### 目標とリリース原則

v0.12 は v0.11.0 で示した後続範囲を維持します。custom object の保持、Proxy Graphics fallback、raw evidence 取得を土台として、天正建築 2D の壁・柱・開口/建具・軸網・階段・部屋・標高/索引/寸法について、**根拠により制約された native parameter semantics** を構築します。

`TCH_*` の識別、unknown/proxy entity の保持、Proxy Graphics の表示、raw DXF/DWG の取得、schema corpus/fingerprint の生成、または数値の見た目から field 意味を推測することだけでは native semantic 対応完了とはみなしません。

Product/File/Informational version を `0.11.0` から `0.12.0` に上げる前に、v0.11.0 release note で v0.12 対象とした各主要カテゴリは最低でも **Partial semantic** に到達し、実 Reader 回帰と fail-closed 条件を満たす必要があります。証拠不足のカテゴリはリリースを継続して阻害し、過去に示した範囲を暗黙に縮小して gate を通しません。

### 状態

| 状態 | 意味 | v0.12 native-semantic gate |
| --- | --- | --- |
| **Drawable2D** | 信頼できる 2D parameter が十分に復元され native Scene geometry を生成可能 | 可 |
| **Partial** | 明確に命名できる native parameter の一部を復元したが完全な 2D geometry には不足 | 可（non-claims 必須） |
| **Evidence-only** | class/raw/public property の証拠はあるが raw field の意味対応が未確立 | 不可 |
| **Preserved only** | identity/raw/proxy の保持のみで native semantics は未確立 | 不可 |

`CadCustomSemanticCoverage.Partial` / `Drawable2D` はコード上の正式状態です。Evidence-only / Preserved-only は milestone 管理状態であり、decoded semantic として偽装しません。

### 現在のマトリクス

| オブジェクト/カテゴリ | 状態 | 検証済み | 未対応として明示する内容 | gate |
| --- | --- | --- | --- | --- |
| `TCH_WALL` | **Drawable2D** | 直線壁の始終点、左右厚、任意の標高/高さ、direct/packed profile、native outline | 曲線壁、全 wall type/property、完全 BIM constraint | ✅ |
| `TCH_OPENING` | **Partial** | anchor XY、任意 Z、opening→host-wall relationship | 幅、高さ、腰高、type/number、開き方向、frame、native geometry | ✅ partial |
| `TCH_SPACE` | **Partial** | `TDbSpace`、anchor、部屋名、部屋番号 | boundary、面積、体積、周長、巾木/壁/扉/窓面積の field mapping | ✅ partial |
| `TCH_ELEVATION` | **Partial** | `TDbSymbElevation`、anchor、標高 text、任意 plot scale | symbol/arrow geometry、方向、文字高、style | ✅ partial |
| `TCH_COLUMN` | **Evidence-only** | 目標属性・type 登録の証拠 | raw group mapping、native semantic | ❌ blocker |
| 軸網 / axis-label family | **Evidence-only** | custom type の存在 | 安定 raw schema、軸/番号 mapping | ❌ blocker |
| `TCH_LINESTAIR` / `TCH_RECTSTAIR` | **Evidence-only** | 階段目標属性・type 登録 | raw group mapping、Reader regression | ❌ blocker |
| 索引 / drawing-title family | **Evidence-only** | custom type/command evidence | entget schema、text/number/anchor mapping | ❌ blocker |
| 天正寸法 family (`TCH_DIMENSION2` 等) | **Evidence-only** | type 登録 | definition point/text/style の raw mapping | ❌ blocker |
| `TCH_ARROW` | **Evidence-only** | `TDbSymbArrow` の公開 entget | point role、70/41 semantics、native geometry | 補助研究 |
| `TCH_MULTILEADER` | **Evidence-only** | `TDbSymbMultiLeader` 公開 entget | point/group role、native geometry | 補助研究 |
| その他 `TCH_*` | **Preserved only / Evidence-only** | identity/raw/proxy（利用可能な場合） | native semantics | 対応済みとして数えない |

### semantic profile の最低受入条件

1. 明確な `TCH_*` identity と、可能な限り verified subclass/C++ schema guard。
2. public entget/AutoLISP、検証可能な sample、または documented property と raw layout の信頼できる対応に基づく field 名。数値範囲だけの推測は禁止。
3. 最低 1 本の実 text-DXF Reader → CadCore regression。
4. schema marker 欠落、malformed/non-finite 値、truncated payload、無関係 vendor object は fail closed。
5. Partial semantic は Proxy Graphics を抑止しない。native geometry path に入るのは Drawable2D のみ。
6. Partial profile ごとに明確な non-claims を記載。
7. CLR ABI `1.0.0.0`、Host Contract `SpatialViewer.CadHost >=1.0.0,<2.0.0` を維持。

### 研究・corpus

privacy-safe Tianzheng schema corpus は図面内容を外部化せず structural signature と coverage を集約し、検証済み `ToJson` / `FromJson` / `MergeJson` で複数 sample を統合できます。ただし corpus は decoder profile の発見・検証用であり、native semantics の代替ではありません。

### 現在のリリース判断

- Product version は `0.11.0` のままです。
- この matrix は 0.12.x product release を阻害しません。
- 主な blocker は **柱、軸網、階段、索引/図名、天正寸法**の evidence-backed semantic profile です。
- 証拠が不足する場合は speculative decoder を作らず、匿名 schema / 実 entget の収集を優先します。
