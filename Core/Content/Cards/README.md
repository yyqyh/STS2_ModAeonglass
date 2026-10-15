# 卡牌写法约定（2026-10-15 定稿）

> 目的：**一张卡只写"它自己不同的那几行"**，其余靠模板与约定收口；注释统一四段，改数值/改口径时不用翻实现。

---

## 一、模板（`Templates/AeonglassCardTemplates.cs`）

| 模板 | 用在哪 | 构造参数 | 子类还要写什么 |
|---|---|---|---|
| `AeonglassAttackTemplate` | 普通攻击件（BA 通道） | `cost, rarity, damage, upgradeDelta` | 额外效果 → 覆写 `OnPlayExtra`；条件改数值 → 覆写 `DamageThisPlay` |
| `AeonglassBlockTemplate` | 普通格挡件（BB 通道，自带 `CardTag.Defend`） | `cost, rarity, block, upgradeDelta` | 同上（`BlockThisPlay` / `OnPlayExtra`） |
| `AeonglassStasisAttackTemplate` | 搁浅攻击件（STA：铭刻 / 破片） | `cost, rarity, damage, upgradeDelta, strandCombats` | 一般**什么都不用写** |
| `AeonglassStasisBlockTemplate` | 搁浅格挡件（STA：封沙） | `cost, rarity, block, upgradeDelta, strandCombats` | 一般**什么都不用写** |
| `AeonglassPowerCardTemplate` | 能力卡（Power 通道） | `cost, rarity` | `OnPlay` 里 `PowerCmd.Apply<T>`；数值可升级就覆写 `OnUpgrade` |
| `AeonglassVariantCardTemplate` | 纯差分（播种那种"两副各走一条路"） | `cost, type, rarity, target` | 在 `OnPlay` 里读 `IsVariantB` 走两条路；有"效果块"就覆写 `VariantBlocks` |
| `AeonglassVariantAttackTemplate` | 差分 + 攻击件（潮汐） | `cost, rarity, damage, upgradeDelta` | 覆写 `VariantBlocks` 给当前副的额外效果；条件改数值覆写 `DamageThisPlay` |
| `AeonglassVariantBlockTemplate` | 差分 + 格挡件（堤岸） | `cost, rarity, block, upgradeDelta` | 同上（`BlockThisPlay` / `VariantBlocks`） |

**模板不覆盖的情况**（别硬塞进模板）：诅咒/状态卡、Token 卡、指向意图卡（操作时间）、X 费卡、带自定义关键词的卡 ——
这些各写各的类，但**注释骨架照抄**（见第二节）。差分卡不要各写各的，用下面那三个差分模板。

> 差分三兄弟已经把"`a`/`b` 文案 stem + 卡面差分文本注入 + 此刻在哪一副"收进模板；
> **判定单点**仍是 `DeckPiles.DeckIndexOf`（经 `DeckVariants.SideOf`），卡里只准用 `Side` / `IsVariantB`。
> 已知缺口：**技能 + 搁浅**目前只有「穷途陌路」一张，仍手写搁浅样板（凑到第二张再抽第四个模板）。

---

## 二、注释骨架（卡 / 遗物 / 角色同一套）

```csharp
/// <summary>
/// <b>卡名</b>（CardXXX，v2 + delta 20261015）· <b>费用 · 稀有度 · 类型</b> —— 基础档数值（升级档数值）· 关键词。
/// </summary>
/// <remarks>
/// ① 效果：基础档做什么；升级档改哪一项。
/// ② 差分：永时副 / 消逝副 各走什么（无差分就写"无差分"）。
/// ③ 依赖：读哪套数据 / 挂哪个钩子 / 与哪条机制联动（例：本场翻转次数、意图手牌、回溯接缝、搁浅区）。
/// ④ ⚠️ 口径与风险：每次改动带日期写清改了哪一项（例：2026-10-15 delta：16→30，×2.7 超模待盯）。
/// </remarks>
```

- **溯源标记**统一 `（CardXXX，v2 + delta YYYYMMDD）`；被 delta 改过的卡必须在 ④ 里点名改了哪一项。
- **遗物**：① 定位 ② 触发时机 ③ 存档字段（`[SavedProperty]` 跟不跟存档走）④ 与哪条机制联动 + 口径来源。
- **角色**：① 角色定位 ② 起始牌组与双卡组翻转 ③ 次级资源（沙砾 / 空隙）④ 差分与孪生 ⑤ 卡池 / 能量色 / 图标路径。

---

## 三、命名与归属

- 类名：`Aeonglass` + 英文卡名（`AeonglassSandSeal`），一张卡**一个文件**，放在按稀有度的目录下：`Basic / Common / Uncommon / Rare / Curse / Token`。
- 本地化 id：`STS2_AEONGLASS_CARD_AEONGLASS_<类名去掉 Aeonglass 前缀的 UPPER_SNAKE>`，写在 `localization/zhs/cards.json`（**复数表名**）。
- 池归属：角色池 `[RegisterCard(typeof(AeonglassCardPool))]`；玩家拿不到的衍生卡/展示件 → `AeonglassTokenCardPool`（**绝不进角色池**，否则会被奖励/商店抽到）。
- 稀有度写在构造参数里（目录只是分拣），改稀有度要同时挪目录。

## 四、改动纪律

1. **数值只改构造参数**，不要在 `OnPlay` 里写死；升级增量用 `UpgradeValueBy`。
2. 只改 C# ⇒ `dotnet build -p:RunPckExport=false`；**动了 JSON / 图 / tscn ⇒ 必须重导 PCK**（`--import` + `--export-pack BasicExport`）。
3. 卡面文案里的动态数字一律走 `DynamicVars`（`{Damage:diff()}` / `{Block:diff()}`），别把数字写进 JSON。
4. 新卡先在 `aeonglass_apply_v2.csv` 里立行（含 `final_effect` / `final_upgrade` / `action` / `impacts`），再写代码。

## 五、数值口径（2026-10-15 换代）

- **现行口径 = v2 定价公式**：`aeonglass_pricing_formula_v2.md`（锚定原版 P90：0 费 9 / 1 费 12 / 2 费 18 / 3 费 30；格挡 9 / 9 / 16 / 22；
  附加效果只扣 1–2 点；代价一律加法；变形 = AOE ×0.8 / 多段 ×0.9 / X 费每点 ×0.8）。
- **旧口径（0 费≈6 / 1 费≈8·6 / 2 费≈15 / 3 费≈27）已作废**，代码注释里若还写着旧基线的对比，只当历史记录看。
- 全池对照表：`aeonglass_cards_v2_value_audit.csv`（亏模 11 / 合规 3 / 超模 4）。
- ⚠️ **纪律**：这张表只是体检结论 —— <b>数值一律等卡池侧发新版数字再改</b>，不要照着公式自己调。
