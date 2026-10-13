using Godot;

using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Characters;
using STS2RitsuLib.Scaffolding.Godot;

using STS2_Aeonglass.Core.Presentation.Animations;
using STS2_Aeonglass.Core.Content.Relics;

namespace STS2_Aeonglass.Core.Content.Characters;

/// <summary>角色：<b>永世沙漏（Aeonglass）</b> —— 原版 Act 3 boss 转可玩角色。</summary>
/// <remarks>
/// <para>
/// <b>第 ① 步只做「人物模型」</b>：把 boss 的战斗 Spine 接成玩家外观、跑通选人 → 进战斗 → 四态动画，
/// 外加最小可玩壳（两张占位基础牌）与调试面板。<b>不实现任何机制</b>（M1–M7 一行都没有）。
/// </para>
/// <para>
/// <b>战斗外观</b>：直接用原版 boss 的骨架（<c>aeonglass.skel</c>）+ 我们自己的状态机映射，
/// 场景见 <see cref="Const.Paths.Character.CombatVisualsScene" />。
/// 其余字段（能量计数器 / 商店 / 火堆 / 音效 / 多人手势）全部由 <see cref="PlaceholderCharacterId" />
/// 补齐 —— 占用清单见 <c>Core/Framework/Diagnostics/PlaceholderAssetRegistry.cs</c>。
/// </para>
/// <para><b>数值全部是占位</b>（见计划文档附录 D），定档在阶段 ④。</para>
/// </remarks>
[RegisterCharacter]
public sealed class Aeonglass : ModCharacterTemplate<AeonglassCardPool, AeonglassRelicPool, AeonglassPotionPool>
{
    /// <summary>沙漏玻璃的青色（① 占位配色）。</summary>
    public override Color NameColor => new("4FD1C5");

    public override Color MapDrawingColor => new("2A6E68");

    /// <summary>
    /// 战斗能量计数器上 <c>3/3</c> 那个数字的**描边色**。
    /// </summary>
    /// <remarks>
    /// 不覆写就落到基类默认的近全透明（<c>#0000000D</c>），数字几乎没描边。
    /// 这里取与卡池 <c>EnergyOutlineColor</c> 相同的青绿 <c>#1A5E5A</c>，和沙漏的 UI 主色一致。
    /// <para>
    /// ⚠️ 与 <c>CardPoolModel.EnergyOutlineColor</c>（**卡面**费用数字的描边）是两个独立字段，别混淆。
    /// </para>
    /// </remarks>
    public override Color EnergyLabelOutlineColor => new("1A5E5A");

    /// <summary>① 占位（定档在 ④）。</summary>
    public override int StartingHp => 70;

    /// <summary>① 占位（定档在 ④）。</summary>
    public override int StartingGold => 99;

    /// <summary>沙漏没有性别；本体枚举只有 Neutral / Feminine / Masculine。</summary>
    public override CharacterGender Gender => CharacterGender.Neutral;

    /// <summary>美术占位来源：本体铁甲战士。我们没填的 <c>AssetProfile</c> 字段都由它补齐。</summary>
    public override string? PlaceholderCharacterId => Const.PlaceholderCharacterId;

    /// <summary>初始遗物：<b>沙漏之心</b>。</summary>
    /// <remarks>
    /// <para>
    /// <b>它现在没有任何效果，但必须存在</b>：本体选人界面里写死读
    /// <c>characterModel.StartingRelics[0]</c>（见 <c>NCharacterSelectScreen.SelectCharacter</c>），
    /// 空列表会让"点击该角色"抛 <c>ArgumentOutOfRangeException</c> → 选人失败 → 游戏沿用上一个角色开局。
    /// </para>
    /// <para>
    /// 放在角色类（而不是只靠 <c>[RegisterCharacterStarterRelic]</c> 注解）是为了**保证它是第 0 个** ——
    /// 注解顺序由类型发现顺序决定，而选人界面只看第 0 个（小丑那边踩过同样的坑）。
    /// </para>
    /// </remarks>
    protected override IEnumerable<RelicModel> LocalStartingRelics => [ModelDb.Relic<AeonglassCore>()];

    /// <summary>战斗外观 + UI 空槽。</summary>
    /// <remarks>
    /// <para>
    /// 场景结构照小丑的 <c>jester_combat.tscn</c>（去掉它那个 3D 分支）：<c>Visuals</c>(SpineSprite)、
    /// <c>Bounds</c>(Control)、<c>CenterPos</c>(Marker2D)、<c>IntentPos</c>(Marker2D)，四个都要勾
    /// <c>unique_name_in_owner</c> —— 本体 <c>NCreatureVisuals._Ready</c> 用 <c>GetNode</c>（非 OrNull）
    /// 取它们，缺一个直接抛异常。
    /// </para>
    /// <para>
    /// UI 那 6 个路径指向的是<b>透明空槽图</b>（尺寸＝最终规格），替换图片即生效。
    /// 没在这里填的字段（能量计数器 / 商店 / 火堆 / 卡痕 / 音效 / 多人手势）继续由
    /// <see cref="PlaceholderCharacterId" /> 补。<b>音效必须留空</b>：自己写
    /// <c>event:/sfx/characters/aeonglass/...</c> 而没在 <c>VirtualFmodEventRegistry</c> 注册，
    /// 会在日志里打 <c>error</c>。
    /// </para>
    /// </remarks>
    public override CharacterAssetProfile AssetProfile => new(
        Scenes: new CharacterSceneAssetSet(
            VisualsPath: Const.Paths.Character.CombatVisualsScene,
            EnergyCounterPath: Const.Paths.Character.EnergyCounterScene,
            MerchantAnimPath: Const.Paths.Character.MerchantScene,
            RestSiteAnimPath: Const.Paths.Character.RestSiteScene),
        Vfx: new CharacterVfxAssetSet(
            TrailStyle: new CharacterTrailStyle(
                OuterTrailModulate: new Color("4FD1C5"),
                InnerTrailModulate: new Color("9BE8DD"),
                BigSparksColor: new Color("BCF2EA"),
                LittleSparksColor: new Color("7FD9CD"),
                PrimarySpriteModulate: new Color("8FE3D8"),
                SecondarySpriteModulate: new Color("6FC9BE"))),
        Ui: new CharacterUiAssetSet(
            IconTexturePath: Const.Paths.Ui.Icon,
            IconOutlineTexturePath: Const.Paths.Ui.IconOutline,
            IconPath: Const.Paths.Ui.IconScene,
            CharacterSelectIconPath: Const.Paths.Ui.CharacterSelectIcon,
            CharacterSelectLockedIconPath: Const.Paths.Ui.CharacterSelectLockedIcon,
            CharacterSelectBgPath: Const.Paths.Ui.CharacterSelectBgScene,
            CharacterSelectTransitionPath: Const.Paths.Character.TransitionMaterial,
            MapMarkerPath: Const.Paths.Ui.MapMarker),
        Multiplayer: new CharacterMultiplayerAssetSet(
            ArmPointingTexturePath: Const.Paths.Ui.Hands.Point,
            ArmRockTexturePath: Const.Paths.Ui.Hands.Rock,
            ArmPaperTexturePath: Const.Paths.Ui.Hands.Paper,
            ArmScissorsTexturePath: Const.Paths.Ui.Hands.Scissors));

    /// <summary>用我们自己的场景当战斗外观根节点。</summary>
    /// <remarks>
    /// 与 <c>AssetProfile.Scenes.VisualsPath</c> <b>冗余但稳</b>（小丑也是两个都写）：
    /// RitsuLib 优先用本方法的返回值，返回 null 时才回退到 <c>VisualsPath</c>（由 RitsuLib 的补丁
    /// 改 <c>CharacterModel.VisualsPath</c> 的 getter 实现）。
    /// </remarks>
    protected override NCreatureVisuals? TryCreateCreatureVisuals()
        => RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(Const.Paths.Character.CombatVisualsScene);

    /// <summary>战斗 Spine 动画状态机 —— <b>① 的核心</b>。</summary>
    /// <remarks>
    /// <para>
    /// 接线走 RitsuLib：它 patch 了 <c>CharacterModel.GenerateAnimator</c>，前置逻辑是
    /// 「若 <c>IModCreatureAnimatorFactory.TryCreateCreatureAnimator(controller)</c> 返回非 null 就用它」，
    /// 而 <c>ModCharacterTemplate</c> 已实现该接口并转发到本方法 —— 所以<b>不必覆写 GenerateAnimator</b>。
    /// </para>
    /// <para>
    /// <b>降级映射</b>（骨架只有 6 个身体动画，缺 <c>attack</c> / <c>cast</c> /
    /// <c>low_health_loop</c> / <c>relaxed_loop</c>）：<c>Attack</c> → <c>attack_heavy</c>；
    /// <c>Cast</c> / <c>PowerUp</c> → <c>wither</c>；<c>Relaxed</c> 与低血分支 → <c>idle_loop</c>
    /// （低血分支<b>故意不注册</b>，注册了也只会打一条 <c>could not find</c> 警告）。
    /// 待填充清单见计划文档 §2.2.2。
    /// </para>
    /// <para>
    /// 注意：本体 <c>AnimState</c> <b>没有</b> <c>attack_heavy</c> 这类常量，所以这里只能用
    /// <see cref="AeonglassAnimNames" /> 里的字符串。
    /// </para>
    /// </remarks>
    protected override CreatureAnimator? SetupCustomCreatureAnimator(MegaSprite controller)
    {
        var idle = new AnimState(AeonglassAnimNames.Idle, isLooping: true);

        // 初始状态就是待机（本体对 idle_loop 会做一次随机相位偏移，见 CreatureAnimator 构造函数）。
        var animator = new CreatureAnimator(idle, controller);

        var attackHeavy = new AnimState(AeonglassAnimNames.AttackHeavy) { NextState = idle };
        var attackDouble = new AnimState(AeonglassAnimNames.AttackDouble) { NextState = idle };
        var wither = new AnimState(AeonglassAnimNames.Wither) { NextState = idle };
        var hurt = new AnimState(AeonglassAnimNames.Hurt) { NextState = idle };
        var die = new AnimState(AeonglassAnimNames.Die);

        animator.AddAnyState(CreatureAnimator.idleTrigger, idle);
        animator.AddAnyState("Relaxed", idle);

        // 默认攻击：本体 AttackCommand.FromCard / FromMonster 会把 trigger 固定成 "Attack"。
        animator.AddAnyState(CreatureAnimator.attackTrigger, attackHeavy);

        // 沙漏自己的两种攻击分型：卡牌侧用 AttackCommand.WithAttackerAnim(名字, 延迟) 点名
        // （那个名字是被当 trigger 传给状态机的），留给阶段 ④ 的卡池用。
        animator.AddAnyState(AeonglassAnimNames.AttackHeavyTrigger, attackHeavy);
        animator.AddAnyState(AeonglassAnimNames.AttackDoubleTrigger, attackDouble);

        // 施法与能力：本体要 cast（我们没有），降级到 wither（原版「渐增强度」动作语义最贴）。
        animator.AddAnyState(CreatureAnimator.castTrigger, wither);
        animator.AddAnyState(CreatureAnimator.powerUpTrigger, wither);

        animator.AddAnyState(CreatureAnimator.hitTrigger, hurt);
        animator.AddAnyState(CreatureAnimator.deathTrigger, die);

        return animator;
    }

    /// <summary>不参与本体 epoch / timeline 进度假设（空白角色没有 <c>*_EPOCH</c> 数据）。</summary>
    public override bool RequiresEpochAndTimeline => false;

    /// <summary>不参与本体的随机角色抽选（避免绕过联机选人规则）。</summary>
    public override bool AllowInVanillaRandomCharacterSelect => false;

    /// <summary>没有前置解锁角色。</summary>
    protected override Type? UnlocksAfterRunAsType => null;

    /// <summary>攻击动画播多久之后才结算伤害（秒）。</summary>
    /// <remarks><c>attack_heavy</c> 比本体人形角色的 <c>attack</c> 长，先用 0.25s，① 实测后再调。</remarks>
    public override float AttackAnimDelay => 0.25f;

    /// <summary>施法动画延迟（<c>wither</c>）。</summary>
    public override float CastAnimDelay => 0.30f;

    /// <summary>打建筑师时的攻击特效 —— 沙漏原版就用「重击」这一类。</summary>
    public override List<string> GetArchitectAttackVfx() => ["vfx/vfx_attack_blunt"];
}
