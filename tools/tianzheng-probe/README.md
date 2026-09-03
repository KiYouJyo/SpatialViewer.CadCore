# Tianzheng controlled A/B probe

`TianzhengDiffProbe.lsp` is a small in-product research helper for the remaining SpatialViewer.CadCore v0.12 Tianzheng semantic blockers.

It is intentionally **not** a decoder. It reports which DXF group slots changed between two otherwise-equivalent `TCH_*` objects without printing their raw values.

## 中文

### 用途

剩余 v0.12 blocker（轴号、楼梯、索引、天正尺寸）缺少可靠的 raw group → 参数映射。`TCHDIFF` 用于在 AutoCAD + 天正内部做受控单变量 A/B 实验，不需要把工程图纸上传到仓库。

推荐流程：

1. 新建两个结构等价的同类天正对象，或复制一个对象作为实验副本。
2. 只修改副本的**一个已知属性**，例如轴号文字、楼梯踏步数、索引号或尺寸出图比例。
3. `APPLOAD` 加载 `TianzhengDiffProbe.lsp`。
4. 运行 `TCHDIFF`，先选 baseline，再选 modified。
5. 记录输出的 `slot / code / occurrence`，不要据此立即命名字段。
6. 再做至少一组独立 A/B 实验，并用 CadCore 的 repeatability consensus 与公开资料交叉验证。

### 隐私与安全边界

脚本不会输出：

- raw DXF value；
- entity name；
- object handle（group 5 被排除）；
- 文件名或图纸路径；
- 项目文字、坐标、尺寸值本身。

脚本只输出对象 `TCH_*` 类型、结构长度以及发生变化的 group code / occurrence / slot index。

`-1` entity name 与 group 5 handle 会在比较前排除，因为复制对象时它们天然不同且没有字段研究价值。其他 group（包括 relationship/owner/reactor 相关 group）不会被自动忽略；它们可能形成噪声，因此必须依靠第二组独立实验的 consensus 过滤。

如果 DXF object identity、subclass profile 或 group-code layout 不一致，脚本 fail closed，不做启发式对齐。

### 不能据此声称什么

一次实验中出现 `code=40` 变化，**不代表** group 40 就是柱宽、梯宽或尺寸比例。只有满足以下条件才允许进入 native semantic：

- 至少两组独立受控实验稳定命中同一 slot；
- 对象 identity/schema 一致；
- 有公开 AutoLISP/entget、可验证样本或其他独立证据支持字段命名；
- 最终在 CadCore 中加入真实 Reader regression 与 fail-closed 测试。

## English

### Purpose

The remaining v0.12 blockers — axis labels, stairs, index objects and Tianzheng dimensions — still lack reliable raw-group mappings. `TCHDIFF` allows controlled single-variable A/B experiments inside AutoCAD + Tianzheng without uploading project drawings.

Recommended workflow:

1. Create two structurally equivalent objects of the same Tianzheng type, or duplicate one object.
2. Change exactly **one known property** on the modified object.
3. Load `TianzhengDiffProbe.lsp` with `APPLOAD`.
4. Run `TCHDIFF`; select baseline first, modified second.
5. Record only the reported `slot / code / occurrence` candidates.
6. Repeat with at least one independent pair and intersect the evidence with CadCore's repeatability-consensus tooling plus external documentation.

The probe never prints raw DXF values, entity names, object handles, file names/paths, project text, coordinates or dimension values. Group `-1` and handle group `5` are excluded before comparison. Other relationship/owner/reactor groups are deliberately not hidden; repeatable experiments are required to filter that noise.

DXF identity, subclass profile and group-code layout mismatches fail closed. No heuristic alignment is performed.

A changed group slot is **evidence only**. It must not be named as a semantic field until repeated experiments and independent evidence support that interpretation, followed by a real CadCore Reader regression.

## 日本語

### 目的

v0.12 に残る blocker（軸番号、階段、索引、Tianzheng 寸法）は、信頼できる raw group → parameter mapping が不足しています。`TCHDIFF` は AutoCAD + Tianzheng 内で single-variable A/B 実験を行い、project drawing を repository にアップロードせず changed slot を確認するための probe です。

推奨手順：

1. 同じ Tianzheng type の構造的に等価な object を 2 個用意します。
2. modified 側で既知 property を **1 個だけ**変更します。
3. `APPLOAD` で `TianzhengDiffProbe.lsp` をロードします。
4. `TCHDIFF` を実行し、baseline → modified の順に選択します。
5. 出力された `slot / code / occurrence` のみを記録します。
6. 独立した 2 組目の実験を行い、CadCore repeatability consensus と外部 evidence で交差検証します。

probe は raw DXF value、entity name、object handle、file/path、project text、coordinate、dimension value を出力しません。`-1` と group 5 は比較前に除外します。その他の relationship/owner/reactor group は自動除外せず、repeatability consensus で noise を除去します。

DXF identity、subclass profile、group-code layout が一致しない場合は fail closed し、heuristic alignment を行いません。

changed slot は semantic field ではなく**研究 evidence**です。複数の独立実験と外部資料で意味が確認され、CadCore の real Reader regression が追加されるまでは parameter 名を付けません。
