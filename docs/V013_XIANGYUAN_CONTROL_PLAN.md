# Xiangyuan Control Planning compatibility roadmap

> Status: compatibility foundation. This document deliberately separates **safe preservation/display** from **native Xiangyuan semantics**. CadCore must not invent proprietary field meanings from screenshots, numeric ranges, command prefixes, or guessed DXF class names.

## 中文

### 为什么单独建立湘源兼容线

湘源控规长期依赖 CAD 二次开发对象，而不是只使用普通 AutoCAD primitive。公开资料明确说明：

- 地块采用自定义对象，地块图层、颜色、边界线、控制指标块和地块属性数据相互关联；
- 图则存在自定义图则对象；
- 后续版本对燃气、热力、输油等管线增加了自定义对象；
- 软件提供“成果出图”“对象转块（LZXVPORTTOBLK）”“全部炸开”等流程，把依赖湘源环境的对象转换为普通 AutoCAD 图形。

因此 CadCore 的目标不是重新实现湘源编辑器，而是让原生 DWG/DXF 在未安装湘源时仍能尽可能可靠地 **识别、保留、显示和诊断**。

### Foundation 已建立

本阶段复用 CadCore 已有的 vendor-neutral custom-object 基础：

1. CLASSES table identity preservation；
2. `ProxyEntity` / `UnknownEntity` preservation；
3. text-DXF raw group capture；
4. modern DWG bounded raw object-record evidence；
5. ObjectARX Proxy Graphics，包括 polyline/polygon/circle/arc/text、transform、clip、subentity traits、mesh/shell edge fallback；
6. custom-object handle references。

新增湘源专用的保守识别：

- `CadCustomObjectVendor.Xiangyuan`；
- `CadCustomClassDefinition.IsXiangyuan`；
- `CadCustomEntity.IsXiangyuan`；
- document metadata：`XiangyuanDetected` / `XiangyuanClassCount` / `XiangyuanEntityCount`；
- entity metadata：`CustomVendor=Xiangyuan` / `XiangyuanObject=True`。

识别只接受明确的 application/C++ identity：application 可识别 `LzxSoft` / `Xiangyuan` / `湘源`，C++ identity 暂只识别明确的 `Xiangyuan` / `湘源`。**不会因为公开命令存在 `LZX...` 前缀就猜测真实 DXF class 或 C++ class 一定使用 `LZX/Lzx` 前缀。**

### 当前支持声明

当前可以声明：

- 湘源 application-defined object 能与普通 unsupported entity 区分；
- 已知 CLASSES identity 会被 reader-independent 保留；
- Proxy Graphics 存在且属于 CadCore 已验证子集时可继续走现有 2D fallback；
- raw evidence 可用于后续真实样本研究；
- 未知湘源 semantic fail closed。

当前不能声明：

- 地块代码、用地性质、容积率、建筑密度、绿地率、高度等 raw field mapping；
- 湘源地块 native boundary reconstruction；
- 指标块与地块属性的原生 relationship semantics；
- 自定义图则、街区地块、管线对象的 native semantic decoder；
- 任何真实湘源 DXF class-name convention。

### 下一阶段优先级

**P0 — 真实样本兼容矩阵**

P0 的代码侧采集器已经建立：`CadXiangyuanSchemaCorpus` 可从导入后的文档生成 privacy-safe schema corpus，并支持 JSON 导出、反序列化校验和多样本 merge。Corpus 只保留 structural identity / coverage，不输出路径、handle、坐标、文本值、raw DXF value 或 raw DWG bytes。

优先收集不同湘源代际生成的匿名测试 DWG/DXF，并统计：

- CLASSES: DXF name / C++ class / application identity；
- object count / proxy availability；
- proxy primitive kinds；
- raw DXF schema fingerprint；
- DWG object-record availability；
- 哪些对象在不装湘源时存在显示缺失；
- Proxy Graphics 类型组合与 opaque/proxy 覆盖率；
- generic resolved object-reference 覆盖率。

**P1 — 地块对象**

地块是控规看图最关键对象。优先寻找有证据的单变量 A/B 样本：

- 地块编号；
- 用地代码/用地性质；
- 边界；
- 面积；
- 容积率；
- 建筑密度；
- 绿地率；
- 建筑高度；
- 配套/备注等常用控制指标。

只有真实 class identity + repeatable raw-field evidence + Reader regression 后，才进入 named semantic。

P1 的实验入口也已经建立：`CadXiangyuanExperimentAnalyzer` 在通用 privacy-safe A/B differ / repeatability consensus 外再增加 Xiangyuan vendor gate。baseline/modified 必须都具有明确湘源 identity，且通用 identity/schema/capture-method 门禁仍继续生效。输出只保留 changed group slot 或 DWG changed byte range，不保留 before/after 原始值。

**P2 — 图则与街区地块**

优先保证图则边框、视口/裁剪、指标表、地块文字不丢失，再研究 native relationship。

**P3 — 道路/市政/分析对象**

普通 LINE/ARC/PLINE/TEXT/HATCH 继续走通用 CAD 管线；只有确认是湘源 custom object 的道路、管线或分析对象才进入 vendor-specific 研究。

### 版本策略

湘源兼容作为下一条主线推进，但本 foundation 不单独把产品版本从 `0.12.6` 提升到 `0.13.0`。在至少一组真实湘源 Reader fixture、识别回归和 Proxy/opaque 显示验收通过后，再决定 v0.13.0 release gate。

---

## English

CadCore now has a dedicated Xiangyuan Control Planning compatibility foundation while reusing the existing vendor-neutral ObjectARX preservation stack. Xiangyuan custom classes/entities can be identified from explicit application/C++ identities, counted in document metadata, and kept distinct from unrelated custom objects. Existing raw-DXF/raw-DWG evidence and Proxy Graphics fallback remain available.

The foundation intentionally does **not** infer real Xiangyuan DXF class names from the public `LZX...` command namespace and does not claim parcel/control-index native semantics yet. The next gate is a real anonymized DWG/DXF corpus, followed by evidence-backed parcel semantics, atlas/street-block compatibility, and then utility/analysis custom objects.

---

## 日本語

CadCore は既存の vendor-neutral ObjectARX preservation / Proxy Graphics 基盤を再利用し、湘源控規専用の compatibility foundation を追加します。明示的な application / C++ identity に基づいて Xiangyuan custom class/entity を識別し、document metadata で件数を集計し、無関係な custom object と区別します。

公開 command の `LZX...` prefix だけから実際の DXF class 名を推測せず、地块や控制指标の native semantic もまだ宣言しません。次の gate は匿名化した実 DWG/DXF corpus と Reader regression です。
