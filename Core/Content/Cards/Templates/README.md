# 卡牌模板（2026-10-19）

新卡统一继承 `AeonglassCardTemplate`，用 `AeonglassCardSpec` 声明基础字段、变量、效果与升级；搁浅卡继承 `AeonglassStasisCardTemplate`。

## 常用入口

| API | 用途 |
|---|---|
| `AeonglassCardSpec.Attack(...)` | 攻击：自带 `Damage` var + 伤害效果 |
| `AeonglassCardSpec.Block(...)` | 格挡：自带 `Block` var + 格挡效果 + `Defend` 标签 + `GainsBlock` |
| `AeonglassCardSpec.Skill(...)` / `Power(...)` / `Status(...)` | 其它类型 |
| `.Var(name, base, upgradeDelta)` | 额外数值变量 |
| `.Effect(CardEffects...)` | 伤害 / 格挡 / 抽牌 / 挂能力 / 条件 / 自定义 |
| `.Feature(new VariantFeature(...))` | 差分块 + `{Variant}` 文案 |
| `.UpgradeAddKeyword / UpgradeRemoveKeyword / UpgradeEnergy` | 升级操作 |

复杂卡（选牌、意图指向、回溯、X 费公式、token 外壳）保留 `OnPlay` / `OnPlayExtra` 覆写；Spec 仍负责静态声明。

## 注意

- 同一 var 的升级增量只写 `CardVarSpec.UpgradeDelta`，不要再用 `UpgradeVar` 重复加。
- 自定义公式卡用 `new AeonglassCardSpec(...)` 只声明字段，不要用 `Attack/Block` 工厂，避免默认效果重复结算。
- `IDeckVariantCard.HasVariant` 只在带 `VariantFeature` 时为 true；`DeckVariants` 已按它短路。
- mod 关键词用 `XxxKeyword.Value`（RitsuLib 生成的独立动态 `CardKeyword`），不要再用 `CardKeyword.None`；搁浅基类会自动追加 `StasisKeyword.Value`。
- 迁移回归见 `项目文档/STS2_Aeonglass/卡牌模板通用化_20261019.md`。
