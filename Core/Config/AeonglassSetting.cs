namespace STS2_Aeonglass.Core.Config;

/// <summary>本 mod 的<b>持久化设置模型</b>（键名即存盘键，改名会让旧值读不回来）。</summary>
/// <remarks>
/// ① 职责：机制开关与调参数的唯一数据源；各机制用 <c>AeonglassSettingStore.Current.X</c> 读。
/// ② 挂点与时机：RitsuLib 设置框架读写；run 开始/机制注册时读一次，部分项局内实时读。
/// ③ 依赖：存盘文件 <c>mod_data/STS2_Aeonglass/STS2_Aeonglass_settings.json</c>。
/// ④ ⚠️ 口径：<b>2026-10-15 起这些项已从设置界面全部下架</b>（测试期结束，数值取注释里的"定档"值）；
///    属性与读取代码<b>全部保留</b> ⇒ 旧存档里改过的值仍然生效（要强制回正式口径：手动改或删那个 json）。
///    另：加字段给个合理默认值即可，不用写迁移（老配置缺字段时会取默认值）。
/// </remarks>
public sealed class AeonglassSetting
{
    /// <summary>
    /// <b>战斗内控制面板（默认开）</b> —— 所有机制的测试 / 操作按钮都收在这<b>一个</b>面板里（按机制分页）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 2026-10-13 统一：原先每个机制一个面板（沙砾 / 时空裂隙 / 意图与凋萎 / 回溯 / 双卡组 各一份，外加一个外观调试面板，
    /// 一共 7 个开关）。现在只有<b>一个</b>面板 + 页签，本项就是"面板出不出"的<b>唯一</b>开关。
    /// </para>
    /// <para>
    /// 页签按<b>机制总开关</b>出现（时之沙 关掉就没有「沙砾」页，以此类推）；关掉本项 = 一个节点都不建。
    /// </para>
    /// </remarks>
    public bool ControlPanel { get; set; } = true;

    /// <summary>
    /// <b>【测试期临时设施】借用静默猎手的卡池（默认开）</b>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 只往 <c>AeonglassCardPool</c> 里追加本体的静默猎手卡（奖励 / 商店能发出来），起始卡组与
    /// 本体静默猎手自己的卡池都不动。做它的唯一原因是"自己的牌太少，凑不出可测的卡牌奖励"。
    /// </para>
    /// <para>
    /// <b>断开方式</b>：关掉这一项（<b>需重启游戏</b> —— 卡池列表只算一次并缓存），
    /// 或直接删掉 <c>Core/Content/Cards/SilentCardPoolBorrow.cs</c> +
    /// <c>Core/Patches/SilentCardPoolBorrowPatch.cs</c> 与本字段、设置页那一项。
    /// </para>
    /// </remarks>
    public bool DebugBorrowSilentCardPool { get; set; } = true;

    /// <summary>
    /// 外观朝向：<c>true</c> = 保持骨架原始朝向；<c>false</c>（默认）= 把 <c>Visuals.scale.x</c> 取负。
    /// </summary>
    /// <remarks>
    /// boss 骨架按「面向左（朝玩家）」绘制，玩家位需要面向右 —— 所以默认镜像。
    /// 这一项必须实测确认（见计划文档风险 2）。
    /// </remarks>
    public bool ScaleXPositive { get; set; }

    /// <summary>
    /// 战斗外观缩放（字符串，方便在设置页里手输）。
    /// </summary>
    /// <remarks>
    /// 起点 0.12 ≈ 原版 boss 的 0.2 × (玩家位宽度 244 / boss 位宽度 428)。
    /// 用调试面板量准之后，把它抄进 <c>aeonglass_combat.tscn</c> 的 <c>Visuals.scale</c>。
    /// </remarks>
    public string Scale { get; set; } = "0.12";

    // ============================ 机制开关（一条机制一个开关）============================

    /// <summary>
    /// <b>时之沙总开关（默认开）</b>。
    /// </summary>
    /// <remarks>
    /// 关掉后：不注册沙砾 / 空隙两个次级资源、不注册两处 UI、不注册"支付→生成空隙"的订阅，
    /// 也不挂 时之沙的额外按键面板 —— 回到"只有卡牌数值"的状态。
    /// <b>需要重启游戏生效</b>（资源注册只在初始化时做一次）。
    /// </remarks>
    public bool SandEnabled { get; set; } = true;

    /// <summary>
    /// <b>搁浅总开关（默认开）</b>。
    /// </summary>
    /// <remarks>
    /// 关掉后：不注册搁浅牌堆，滞留卡的"打出后进搁浅"覆写会退回本体默认行为
    /// （进弃牌堆），卡退化成一张普通技能牌 —— <b>不会平白消失</b>。
    /// <b>需要重启游戏生效</b>（牌堆注册只在初始化时做一次）。
    /// </remarks>
    public bool StasisEnabled { get; set; } = true;

    /// <summary>
    /// 搁浅的 <b>X</b>：滞留牌打出后，过几场战斗归队（默认 2）。
    /// </summary>
    /// <remarks>打出<b>当场</b>不计一场；归队目标是"归队那一刻的当前卡组"。</remarks>
    public int StasisCombats { get; set; } = 2;

    /// <summary>
    /// 搁浅：同时滞留的**上限**（Step 3 定稿 4）。
    /// </summary>
    /// <remarks>超过上限时新的滞留会被拒绝（不丢牌、也不静默失效，日志会写明原因）。</remarks>
    public int StasisMaxStasis { get; set; } = 4;

    /// <summary>
    /// 搁浅：<b>回退开关</b> —— 归队是否回到"轮换之前"的旧口径（默认 <c>false</c> = 新口径）。
    /// </summary>
    /// <remarks>
    /// 新口径（默认）：一局结束的固定顺序 = <b>双卡组 轮换 → 搁浅 递减 → 搁浅 归队</b>（由 <c>EndOfCombatSequencer</c> 显式驱动）；
    /// 打开这个开关 = 回到旧行为（<c>AfterCombatEnd</c> 里先归队、之后才轮换），仅供对照排查。
    /// </remarks>
    public bool StasisReturnBeforeSwitch { get; set; }

    /// <summary>
    /// 搁浅：有牌归队时闪一下顶栏「搁浅」按钮（<b>默认开</b>，Step 3 第 4 条）。
    /// </summary>
    /// <remarks>纯表现层；关掉后行为与之前一致。找不到按钮（非本角色 / 顶栏未建）时静默跳过。</remarks>
    public bool StasisFlash { get; set; } = true;

    // ============================ 时空裂隙 ============================

    /// <summary><b>时空裂隙总开关（默认开）</b>。</summary>
    /// <remarks>
    /// 关掉后：不注册间隙牌堆与存档槽、不挂调试面板；`Activate` / `Recall` 直接拒绝并打日志 —— 不留坏状态。
    /// <b>需要重启游戏生效</b>（牌堆与数据槽只在初始化时注册一次）。
    /// </remarks>
    public bool GapEnabled { get; set; } = true;

    /// <summary>
    /// 间隙的<b>战斗按键</b>（「坍缩」/「唤回」，默认开）—— 挂在<b>能量计数器正下方</b>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 2026-10-13 用户口径：顶栏已经有两个我们的牌堆按钮，不再往里塞；这两个动作要贴着能量 / 手牌那一片。
    /// </para>
    /// <para>
    /// 关掉 = 不生成这两个按钮（机制本身与 <c>GapCollapseActions.Activate</c> / <c>Recall</c> 接口不受影响）。
    /// 按钮不可用时是原版"灰掉"的禁用态（判定复用 <c>GapCollapseActions.PlanCollapse</c> / <c>PlanRecall</c>）。
    /// </para>
    /// </remarks>
    public bool GapActionButtons { get; set; } = true;

    /// <summary>时空裂隙容量（<b>Step 5 起 = 手牌上限</b>，默认 10）。</summary>
    /// <remarks>规则层 v1：坍缩时把整手牌复制进间隙 ⇒ 容量按"手牌上限"给（本体手牌上限 10）。</remarks>
    public int GapCapacity { get; set; } = 10;

    /// <summary>
    /// 时空裂隙 坍缩（复制）后是否**补抽**（<b>Step 5 起默认关</b>）。
    /// </summary>
    /// <remarks>旧口径会补抽"原手牌数量"的牌；新口径取消补抽（坍缩的代价就是这回合手牌变少）。</remarks>
    public bool GapRefillDraw { get; set; }

    /// <summary>
    /// 时空裂隙 副本是否**一次性**（定档 on）—— <b>硬闸</b>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>当前实现下这一项已无实际差异</b>：间隙里放的是真实卡牌实例，"换回"就是把实例搬进手牌、
    /// 搬走后间隙自然归空（见 <c>GapRecallActions.Recall</c> 第 ④ 步），所以副本在结构上就是一次性的。
    /// </para>
    /// <para>保留开关只为将来若改回"记录式"实现（间隙只存数据、可反复换回）时重新启用那条硬闸。</para>
    /// </remarks>
    public bool GapOneShot { get; set; } = true;

    /// <summary>
    /// 坍缩出来的副本是否带**虚无**（Ethereal，<b>定档 on</b>）。
    /// </summary>
    /// <remarks>
    /// 2026-10-14 规则层定档：**副本带虚无**（默认 true）—— 收益必须当回合打掉，否则回合末烧掉。
    /// 老配置里若**显式存过** false，会在设置页里保持 false（只有全新存档才吃到新默认值）。
    /// </remarks>
    public bool GapEthereal { get; set; } = true;

    /// <summary>
    /// **时空裂隙 两个动作的统一定价**（沙砾，定档 **2**）：坍缩、唤回各收这一笔。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 2026-10-13 用户口径：**两个动作统一 2 沙砾**（取代原先的"坍缩 2 / 唤回按张计费"）。
    /// 一次动作<b>固定收这一笔</b>，不按张数乘。
    /// </para>
    /// <para>不足 ⇒ 整次动作被拒（什么都不发生：不搬牌、不弃手牌）。</para>
    /// </remarks>
    public int GapActionCost { get; set; } = 2;

    // ============================ 回溯 ============================

    /// <summary>
    /// <b>回溯总开关（默认开）</b>。
    /// </summary>
    /// <remarks>
    /// 关掉后：不装配回溯引擎、不挂调试按键、不装 VFX 屏障补丁 —— 回到"没有回溯"的状态。
    /// <b>需要重启游戏生效</b>（引擎与补丁只在初始化时装一次）。
    /// </remarks>
    public bool RewindEnabled { get; set; } = true;

    /// <summary>
    /// 回溯 成功回溯后<b>消耗全部沙砾</b>（定档开）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ 代价是在**回档成功之后**收的：沙砾属于 时之沙的回档范围，先收会被恢复动作还回来。
    /// 见 <c>RewindModule.TryRequest</c> 的注释。
    /// </para>
    /// <para>关掉它 = 回溯免费（仅供实验；"每场 1 次"那条硬闸仍然生效）。</para>
    /// </remarks>
    public bool RewindCostAllSand { get; set; } = true;

    // ============================ 双卡组 ============================

    /// <summary>
    /// <b>双卡组总开关（默认开）</b>。
    /// </summary>
    /// <remarks>
    /// 关掉后：不注册第二副牌组的牌堆、run 钩子直接返回、调试按键不挂 ——
    /// <c>player.Deck</c> 就是本体那一副，一切照旧。
    /// <b>需要重启游戏生效</b>（牌堆只在初始化时注册一次）。
    /// ⚠️ 千万别在"已经切过卡组"的局里关它：非生效的那副会留在 <c>deck_b</c> 里不再轮换（不丢牌，但也不会回来）。
    /// </remarks>
    public bool DecksEnabled { get; set; } = true;

    /// <summary>
    /// <b>「乱流」抽弃互换总开关（默认开）</b>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 关掉后：两个 getter 的后置改写直接返回原值（等于这张诅咒只是一张不可打出的牌），不留坏状态。
    /// </para>
    /// <para>
    /// <b>2026-10-14 改默认为开</b>：本机制<b>接线无条件安装</b>（补丁 + 生命周期订阅），
    /// 开关只 gate 运行时行为 —— 若它默认关，第一次测的人会看到"调用卡牌没效果"
    /// （log 里只有 <c>总开关=False</c> 这一条线索）。与其他机制（时空裂隙/双卡组 默认开）保持一致。
    /// </para>
    /// </remarks>
    public bool RiotEnabled { get; set; } = true;

    /// <summary>
    /// 【调试】<b>强制互换</b>（不等乱流进弃牌堆，直接一直互换）。
    /// </summary>
    /// <remarks>用来单独验"互换"这条链路，以及排查"是没点亮还是改写没生效"。</remarks>
    public bool RiotForceSwap { get; set; }

    /// <summary>每场战斗胜利后<b>自动轮到另一副</b>（默认开）。</summary>
    /// <remarks>这是设计节奏，不收费；收费只针对下面的"主动切换 / 热切换"。</remarks>
    public bool DecksAutoSwitch { get; set; } = true;

    /// <summary>允许玩家<b>主动切换</b>卡组（默认开）。</summary>
    public bool DecksManualSwitch { get; set; } = true;

    /// <summary>主动切换时<b>连战斗内牌堆一起重铺</b>（热切换，默认开）。</summary>
    /// <remarks>关掉它 ⇒ 战斗中主动切换只换主卡组，本场手牌/抽牌堆不动，下一场才生效。</remarks>
    public bool DecksHotSwitch { get; set; } = true;

    /// <summary>
    /// 切换代价的来源：<c>none</c> / <c>sand</c>（沙砾）/ <c>void</c>（空隙）/ <c>energy</c>（能量）。
    /// </summary>
    /// <remarks>
    /// <b>默认 none = 免费</b>：定价数值用户要求"之后再定价"，本轮只把结构做成可配置。
    /// 语义上最贴的正式候选是"消耗空隙"（空隙 = 已流走的时间，切换＝再动一次时间）。
    /// </remarks>
    public string DecksCostSource { get; set; } = "none";

    /// <summary>切换代价的数量（默认 0 = 免费）。</summary>
    public int DecksCostAmount { get; set; }

    /// <summary>
    /// 每场战斗允许主动切换几次（<b>0 = 不限</b>，测试期默认）。
    /// </summary>
    /// <remarks>
    /// ⚠️ 注意这里 <c>0</c> 的含义是"不限"，不是"禁止" —— 与"代价 0"一起构成测试期的默认值。
    /// 正式定价时这条是堵"两副牌变一副大牌组"的第二道闸。
    /// </remarks>
    /// <remarks>2026-10-13 定档：**每场 1 次**（原来是测试期的 0 = 不限）。</remarks>
    public int DecksSwitchesPerCombat { get; set; } = 1;

    /// <summary>
    /// 主动切换的冷却：每 N 场战斗才能主动切换一次（<b>Step 4 定稿 2</b>）。
    /// </summary>
    /// <remarks>
    /// 规则层 v1 · P5 定稿：**冷却语义，不累积** —— 累积会催生"攒 3 点额度 + 一场内反复热切换"的解，
    /// 直接破坏"同层高频、跨层低频"的结构。0 = 无冷却（仅调试）。
    /// </remarks>
    public int DecksManualCooldownCombats { get; set; } = 2;

    /// <summary>
    /// <b>热切换的附加沙砾代价</b>（Step 4：热切换消耗沙砾）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 在 <c>DeckSwitchCost</c> 的基础代价**之上**额外扣这么多沙砾，且**只在战斗内切换（热切换）时**收 ——
    /// "把整副牌换掉并重铺这一场"比"场间换副"更值钱。
    /// </para>
    /// <para><b>默认 0</b>：最终数值属 Step 8 的配平范围（用户口径"具体数值之后再定价"）。</para>
    /// </remarks>
    /// <remarks>2026-10-13 定档：**2 沙砾**（原来是测试期的 0）。</remarks>
    public int DecksHotSwitchSandCost { get; set; } = 2;

    // ============================ 操作时间（意图手牌）============================

    /// <summary>
    /// <b>操作时间总开关（默认开）</b>。
    /// </summary>
    /// <remarks>
    /// 关掉后：不接管怪物掷招、不建意图牌堆、不挂调试按键 —— 怪物回到原版行为。
    /// <b>需要重启游戏生效</b>（补丁与订阅只在初始化时装一次）。
    /// </remarks>
    public bool IntentEnabled { get; set; } = true;

    /// <summary>
    /// <b>C11：接管原生 <c>RollMove</c>（默认开，已定稿）</b>。
    /// </summary>
    /// <remarks>
    /// 开着 = 被托管的怪不再走原生掷招，由我们的意图手牌决定它这一回合打哪张。
    /// ⚠️ 这是 Lab C 里唯一"会用但未实测"的路径（Lab 默认是 off），所以**合装阶段要人工过 2–3 回合**
    /// 确认日志里出现 `C11 接管 RollMove：跳过 …` 且 `log=` 不再每回合 +1。
    /// </remarks>
    public bool IntentManagedRollMove { get; set; } = true;

    /// <summary>
    /// 虚无在"抽牌堆 ↔ 手牌 ↔ 弃牌堆"循环里被抽到时的口径。
    /// </summary>
    /// <remarks>
    /// <c>once</c>（默认）= 抽到一次、惩戒一次，**直接移除**（保持"只生效一次"；不占手牌位）；
    /// <c>recycle</c> = 每次抽到都惩戒（污染变持续，强度大涨，要重平衡）；
    /// <c>nopenalty</c> = 不再惩戒，只占一个手牌位（挡一次出牌位）。
    /// </remarks>
    public string IntentVoidMode { get; set; } = "once";

    /// <summary>
    /// 回合结束时**未打出的意图卡**去哪。
    /// </summary>
    /// <remarks>
    /// <c>discard</c>（默认）= 一起进弃牌堆 ⇒ "抽牌堆→手牌→弃牌堆→洗回"闭环成立；
    /// <c>deckbottom</c> = 确定性回抽牌堆底（Lab C 的旧行为，不消耗 RNG）；
    /// <c>deckshuffle</c> = 随机洗回抽牌堆（消耗怪物 RNG）。
    /// </remarks>
    public string IntentHandEndOfTurn { get; set; } = "discard";

    // ============================ 凋萎（= 虚无）============================

    /// <summary>
    /// 虚无在**怪物回合结束**结算时造成的伤害（<c>ValueProp.Unpowered</c>）。<b>Step 6 定稿 3</b>。
    /// </summary>
    /// <remarks>
    /// 时序（用户口径）：虚无进弃牌堆 → 洗回 → 被抽到 → 进手牌占位（不扣血）→ **怪物行动之后**
    /// 掉这么多血 + 把虚无消耗掉 → 然后下回合抽牌。
    /// </remarks>
    public int WitherVoidDamage { get; set; } = 3;

    /// <summary>副惩戒：虚无被抽到时，是否额外"吃掉"一张意图（占位变成真吃掉）。默认关。</summary>
    public bool WitherEatsIntent { get; set; }

    /// <summary>一次"塞虚无"塞几张（调试按键用）。默认 1。</summary>
    public int WitherInjectPerUse { get; set; } = 1;

    /// <summary>意图：怪物头顶的「意图卡带」（档 2 UI，默认开）。</summary>
    /// <remarks>
    /// 把怪物的意图手牌画成一条只读卡带（空槽卡框 + 意图图标 + 角标），最多 5 张 + "…+N"，
    /// 挂在怪物意图行上方并自动跟随。原版意图图标照常显示（不冲突）。
    /// </remarks>
    public bool IntentIntentBelt { get; set; } = true;

    // ============================ 调试设施 ============================

    /// <summary>
    /// <b>【测试期】解除所有"每场 / 每回合次数"限制（默认开）</b>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 用户 2026-10-11 明确要求："测试解放每场额度（所有功能都不带限制，之前 时空裂隙 等等也是）"。
    /// 打开后：时之沙 倒置每回合 1 次、意图 每场意图操纵上限、回溯 每场 1 次回溯、
    /// 双卡组 每场切换上限与冷却 —— 全部不再拦截（代价本身照旧收，只是不限次数）。
    /// </para>
    /// <para>
    /// ⚠️ 这是**测试开关**：正式发布前应关掉（各机制自己的上限开关仍然独立保留，改这里不影响它们）。
    /// </para>
    /// </remarks>
    public bool DebugUnlimitedPerCombat { get; set; } = true;
}
