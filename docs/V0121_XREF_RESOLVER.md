# v0.12.1 Host-controlled Xref resolver

## 中文

### 目标

v0.12.1 为缺少本地缓存几何的 DWG/DXF 外部参照增加**显式宿主解析边界**。CadCore 不会根据图纸中的 Xref path 自行访问文件系统；只有宿主明确调用 `ImportWithExternalReferencesAsync` 并提供 `ICadExternalReferenceResolver` 时，外部内容才会进入导入链。

普通 `ImportAsync` 的行为保持不变：它只读取用户明确打开的主图，不自动打开任何 Xref。

### 宿主契约

宿主收到 `CadExternalReferenceRequest`：

- `ParentDocumentPath`：当前主图路径，仅供宿主决定自己的解析策略；
- `ReferenceName`：主图中的 Xref block identity；
- `SourceReference`：图纸保存的原始外部参照字符串；
- `IsOverlay`：是否为 overlay。

宿主可以：

1. 返回 `null`，明确拒绝解析；
2. 返回 `CadExternalReferenceResource(Stream, Dxf/Dwg)`，提供已获准的外部图内容；
3. 抛出异常；CadCore 会 fail closed，保留主图并记录不含外部路径的诊断。

提供的 stream 必须可读且可 seek。CadCore 会 rewind 后读取，并在本次解析结束时 dispose 该 stream。

### 安全与确定性边界

- CadCore **不**对 `SourceReference` 执行 `File.Exists`、路径拼接、相对路径解析、目录搜索或自动打开。
- `ImportAsync` 永不触发 resolver。
- 已有本地 Xref cache geometry 时，本地缓存优先，不调用 resolver。
- `IsUnloaded` 的 Xref 保持 unloaded，不因解析器存在而被偷偷加载。
- resolver 返回错误格式、坏流或抛出异常时，只跳过该 Xref，不使主图导入失败。
- diagnostics 与 resolution summary 只保存状态、计数和异常类型，不保存 Xref path。
- 第一版只解析主图直接依赖；宿主提供的子图若还有 nested Xref，仅统计为 dependency，不递归调用 resolver。

### 合并后的几何规则

外部图被映射为主图中原 Xref block 的定义，因此原有 INSERT 的平移、旋转和缩放继续由既有 block renderer 处理。

为了避免主图与外部图发生名称/对象冲突：

- 外部普通子块使用确定性 namespace：`__XREF_####__::<block>`；
- 外部非 `0` 图层使用确定性 namespace：`__XREF_####__|<layer>`；
- Layer `0` 不重命名，继续遵循 CAD block 的 Layer-0 继承语义；
- 外部实体 handle 加入 Xref namespace，避免与主图 ObjectId 冲突；
- 外部图 `$INSBASE` 作为 Xref 根 block base point 保留。

### 已验证回归

CI 中的真实 ACadSharp Writer→Reader→CadDocument→Scene 回归证明：

- 主图保存一个实际不存在的 Xref path 时，普通 `ImportAsync` 仍只读取主图；
- 宿主可以完全忽略该 path，并用 MemoryStream 提供同一 Xref 的 DXF 内容；
- 外部根几何、嵌套普通 block 与 `$INSBASE` 均能正确进入 Scene；
- resolver decline 与 resolver exception 均保留可用主图；
- unloaded Xref 不调用 resolver；
- nested Xref 只报告 dependency，不发生递归访问；
- 错误 host resource fail closed；
- 外部 path 与异常 message 不进入 diagnostics。

当前门禁：Cad `238/238`、Core `19/19`、Rendering `23/23`。

---

## English

v0.12.1 adds an **explicit host-controlled resolution boundary** for DWG/DXF external references that have no usable local cache geometry. CadCore never interprets an Xref source string as a path to probe or open. External content is considered only when the host explicitly calls `ImportWithExternalReferencesAsync` with an `ICadExternalReferenceResolver`.

Ordinary `ImportAsync` remains local-only. The resolver may decline with `null`, provide an approved readable/seekable DXF or DWG stream, or fail. Failures are fail-closed: the parent drawing remains usable and diagnostics contain status/counts and failure type rather than Xref paths.

Cached Xref geometry remains authoritative, unloaded references stay unloaded, and v0.12.1 does not recursively resolve nested Xrefs inside a host-supplied child drawing. Child blocks and non-zero layers are deterministically namespaced, Layer 0 keeps block-inheritance semantics, external handles are namespaced, and the child drawing's model-space insertion base is preserved as the root Xref block base point.

Reader-to-Scene regression coverage verifies host MemoryStream resolution while the source Xref path is nonexistent, nested ordinary blocks, insertion-base transforms, decline/error behavior, unloaded references, non-recursive nested dependencies, invalid resources, and path-safe diagnostics.

Current gates: Cad `238/238`, Core `19/19`, Rendering `23/23`.

---

## 日本語

v0.12.1 では、ローカル cache geometry を持たない DWG/DXF 外部参照に対して、**host が明示的に管理する resolver 境界**を追加します。CadCore 自身は Xref の source string を filesystem path として解釈・探索・open しません。外部内容を読むのは、host が `ImportWithExternalReferencesAsync` と `ICadExternalReferenceResolver` を明示的に使用した場合だけです。

通常の `ImportAsync` は従来どおり主図面のみを読みます。resolver は `null` で拒否するか、許可済みの readable/seekable DXF/DWG stream を返すことができます。resolver error、形式不一致、破損 stream は fail closed とし、主図面は保持されます。診断には Xref path を保存しません。

既存の local Xref cache を優先し、unloaded Xref はその状態を維持します。初版では host 提供子図面内の nested Xref を再帰解決せず dependency としてのみ計上します。通常 block と Layer 0 以外の layer は Xref namespace に隔離し、Layer 0 の block inheritance、外部 entity handle の identity 分離、子図面 `$INSBASE` を保持します。

実際の ACadSharp Writer→Reader→CadDocument→Scene 回帰により、存在しない source path と host MemoryStream の分離、nested block、insertion-base transform、decline/error、unloaded、nested dependency 非再帰、invalid resource、path-safe diagnostics を確認済みです。

現在の gate は Cad `238/238`、Core `19/19`、Rendering `23/23` です。
