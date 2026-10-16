using MegaCrit.Sts2.Core.Logging;

using STS2RitsuLib;

namespace STS2_Aeonglass;

/// <summary>本 mod 的统一常量：<b>ModId 必须与清单 id、DLL 名、资源根目录名三者一致</b>。</summary>
/// <remarks>
/// 内容 id 由 RitsuLib 按 <c>&lt;模组id&gt;_&lt;类别&gt;_&lt;类名&gt;</c> 自动生成（全大写、类名转 SCREAMING_SNAKE），
/// 本地化键就是拿这些 id 拼的 —— 启动日志里 <c>ContentIdLogger</c> 会把真实 id 全打出来，对不上时以日志为准。
/// </remarks>
internal static class Const
{
    /// <summary>必须与清单 <c>STS2_Aeonglass.json</c> 的 id、DLL 名一致。</summary>
    public const string ModId = "STS2_Aeonglass";

    /// <summary>显示用名称。</summary>
    public const string Name = "STS2_Aeonglass";

    /// <summary>中文显示名（面板 / 日志用）。</summary>
    public const string DisplayName = "永世沙漏";

    /// <summary>
    /// 版本号：<b>必须与清单 <c>STS2_Aeonglass.json</c> 的 <c>version</c> 一致</b>
    /// （csproj 里有校验目标，不一致直接编译报错）。
    /// </summary>
    public const string Version = "0.0.1";

    /// <summary>
    /// <b>美术占位来源</b>：第 ① 步除了战斗 Spine / UI 空槽之外，全部回退到本体铁甲战士。
    /// 占用清单见 <c>Core/Framework/Diagnostics/PlaceholderAssetRegistry.cs</c>。
    /// </summary>
    public const string PlaceholderCharacterId = "ironclad";

    /// <summary>
    /// 能量颜色标识（四个池共用）。已换成自己的名字。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本体 <c>EnergyIconHelper.GetPath(prefix)</c> 会把它拼成
    /// <c>res://images/atlases/ui_atlas.sprites/card/energy_&lt;名字&gt;.tres</c> —— 那个文件**我们不存在**；
    /// 真正接住它的是 RitsuLib 的两个补丁：池实现 <c>IModBigEnergyIconPool</c> /
    /// <c>IModTextEnergyIconPool</c> 时，用池上的 <c>BigEnergyIconPath</c> / <c>TextEnergyIconPath</c>
    /// 替换解析结果（见 <see cref="Paths.Ui.Energy.Icon" /> / <see cref="Paths.Ui.Energy.TextIcon" />）。
    /// </para>
    /// <para>
    /// ⚠️ <b>改这个常量 ⇒ 四个池（卡池 / token 卡池 / 遗物池 / 药水池）必须同时填上那两个路径</b>：
    /// 任一池漏填，RitsuLib 的路径校验会静默跳过、回落到本体图集查表，而那张
    /// <c>energy_aeonglass.tres</c> 不存在 ⇒ **该池的图标直接消失**（不是退回铁甲）。
    /// </para>
    /// </remarks>
    public const string EnergyColorName = "aeonglass";

    /// <summary>
    /// <b>分区日志前缀</b>：照《Lab A/B/C 合装清单》§3 #10 —— 结论必须能按机制分开 grep。
    /// </summary>
    /// <remarks>
    /// 日志行本身已经带 <c>[STS2_Aeonglass]</c> 前缀（<see cref="Logger" />），这里再叠一层机制标签，
    /// 于是 <c>grep '\[回溯\]' godot.log</c> 就能拿到回溯整条链路。
    /// </remarks>
    public static class Prefix
    {
        public const string Sand = "[沙砾]";

        public const string Gap = "[间隙]";

        public const string Intent = "[意图]";

        public const string Rewind = "[回溯]";

        public const string Wither = "[凋萎]";

        public const string Decks = "[双卡组]";

        public const string Stasis = "[搁浅]";

        public const string Riot = "[乱流]";

        public const string Deferral = "[延宕]";

        public const string AlphaBeta = "[α or β]";

        /// <summary>引擎 / 宿主级消息（对应 Lab B 的 <c>[LabB]</c>）。</summary>
        public const string Host = "[HOST]";
    }

    /// <summary>回溯引擎的常量（移植自 <c>STS2_AeonglassLabB</c>）。</summary>
    public static class Rewind
    {
        /// <summary>
        /// mod 自己声明并使用的 <c>RunRngSet</c> 附加流 ID 名单。
        /// </summary>
        /// <remarks>
        /// RitsuLib 的 <c>ModRunRngRegistry</c> 没有"枚举所有流"的公开 API，所以 回溯的快照按这份名单收。
        /// 本 mod 目前<b>没有</b>用到自己的随机流（时之沙/时空裂隙/搁浅 都不用 RNG）⇒ 留空数组；
        /// 将来任何机制引入 <c>GetModRunRng(..., stream)</c> 时，把流 ID 加进来即可被回档覆盖。
        /// </remarks>
        public static readonly string[] ModRngStreams = [];
    }

    /// <summary>操作时间/ 凋萎用到的本体状态 Id（移植自 <c>STS2_AeonglassLabC</c>）。</summary>
    public static class Intent
    {
        /// <summary>原生晕眩状态的 Id（本体用字符串比较判 <c>Creature.IsStunned</c>，不能改名）。</summary>
        public const string StunnedStateId = "STUNNED";

        /// <summary>本体占位状态 Id（<c>PerformMove</c> 会抛，绝不能被我们选中）。</summary>
        public const string UnsetMoveId = "UNSET_MOVE";

        /// <summary>虚无意图卡的 Id（怪物侧不存在同名 MoveState，纯元数据卡）。</summary>
        public const string VoidCardId = "VOID";

        /// <summary>默认排除的状态 Id 片段：这些只应在特定剧本里被强制进入，不该当普通意图打出。</summary>
        public static readonly string[] ExcludedStateFragments =
            ["DEAD", "ENRAGED", "ABOUT_TO_BLOW", "STUN", "ESCAPE"];
    }

    /// <summary>
    /// <b>全部资源路径的唯一来源</b> —— 代码里任何地方都不要再写 <c>res://</c> 字面量。
    /// </summary>
    /// <remarks>
    /// 换美术时只改这一个文件；每个路径只在这里出现一次。
    /// （<c>.tscn</c> 里的 ext_resource 路径不受这里管，那是 Godot 资源自己的引用。）
    /// <b>注意</b>：本体 <c>res://</c> 路径只能写在 C# 里，不能写进 <c>.tscn</c> —— mod 的 Godot 工程里
    /// 那些文件不存在，编辑器加载不出来。
    /// </remarks>
    public static class Paths
    {
        /// <summary>本 mod 的资源根（= ModId 同名目录）。</summary>
        public const string Root = "res://" + ModId;

        /// <summary>
        /// <b>本体</b>资源的路径（不是我们自己的美术）。同样只在这里出现一次。
        /// </summary>
        /// <remarks>
        /// 本体路径只能写在 C# 里（mod 的 Godot 工程里没有这些文件，写进 <c>.tscn</c> 编辑器加载不出来）；
        /// 集中在这里是为了让"代码里不再出现 <c>res://</c> 字面量"这条口径真正成立。
        /// </remarks>
        public static class BaseGame
        {
            /// <summary>本体按钮主题字体（时空裂隙按键的文案与那块底板同套）。</summary>
            public const string ButtonThemeFont = "res://themes/kreon_bold_glyph_space_two.tres";
        }

        /// <summary>角色：战斗外观场景 + 骨架数据。</summary>
        public static class Character
        {
            /// <summary>战斗外观场景（<c>Visuals</c> / <c>Bounds</c> / <c>CenterPos</c> / <c>IntentPos</c>）。</summary>
            public const string CombatVisualsScene = Root + "/scenes/combat/aeonglass_combat.tscn";

            /// <summary>战斗能量计数器场景。</summary>
            public const string EnergyCounterScene = Root + "/scenes/combat/energy_counters/aeonglass_energy_counter.tscn";

            /// <summary>商店形象场景（占位动作 = <c>idle_loop</c>）。</summary>
            public const string MerchantScene = Root + "/scenes/shop/aeonglass_merchant.tscn";

            /// <summary>休息处形象场景（占位动作 = <c>idle_loop</c>）。</summary>
            public const string RestSiteScene = Root + "/scenes/rest_site/aeonglass_rest_site.tscn";

            /// <summary>选完人之后的转场材质（内联 shader + 空槽贴图）。</summary>
            public const string TransitionMaterial = Root + "/materials/transitions/aeonglass_transition_mat.tres";
        }

        /// <summary>界面：顶栏图标、选人界面、地图标记。</summary>
        /// <remarks>
        /// <b>这些都是「空槽」</b>：文件名与尺寸就是最终规格，出美术时用同名 PNG 覆盖即可，代码一行不用改。
        /// </remarks>
        public static class Ui
        {
            private const string ImageDir = Root + "/images/ui/";

            /// <summary>顶栏角色图标（88×88）。</summary>
            public const string Icon = ImageDir + "aeonglass_icon.png";

            /// <summary>顶栏图标的描边层（88×88）。</summary>
            public const string IconOutline = ImageDir + "aeonglass_icon_outline.png";

            /// <summary>顶栏图标的<b>场景</b>（本体 <c>CharacterModel.IconPath</c> 要的是场景，不是贴图）。</summary>
            public const string IconScene = Root + "/scenes/ui/aeonglass_icon.tscn";

            /// <summary>选人界面立绘（132×195）。</summary>
            public const string CharacterSelectIcon = ImageDir + "aeonglass_select.png";

            /// <summary>选人界面未解锁立绘（132×195）。</summary>
            public const string CharacterSelectLockedIcon = ImageDir + "aeonglass_select_locked.png";

            /// <summary>选人界面背景场景。</summary>
            public const string CharacterSelectBgScene = Root + "/scenes/screens/char_select/char_select_bg_aeonglass.tscn";

            /// <summary>跑局地图上的角色标记（49×64）。</summary>
            public const string MapMarker = Root + "/images/packed/map/icons/map_marker_aeonglass.png";

            /// <summary>时之沙 沙砾：战斗计数器场景。</summary>
            public const string SandCounterScene = Root + "/scenes/ui/secondary_resource/aeonglass_sand_counter.tscn";

            /// <summary>时之沙 沙砾：计数器里那套 5 层沙漏艺术（运行时注入到框架的图标槽上）。</summary>
            public const string SandCounterArtScene =
                Root + "/scenes/ui/secondary_resource/aeonglass_sand_counter_art.tscn";

            /// <summary>时空裂隙「坍缩 / 唤回」战斗按键的月牙艺术场景（3 层 + 三段表现）。</summary>
            public const string CollapseButtonArtScene = Root + "/scenes/ui/gap/aeonglass_collapse_button.tscn";

            /// <summary>
            /// 「领域」战斗特效层场景 —— 挂在 <c>CombatSceneContainer</c> 的 <b>BgContainer 与 BackCombatVfxContainer 之间</b>。
            /// </summary>
            /// <remarks>
            /// <para>
            /// 落点说明（2026-10-14）：战斗房间的结构是
            /// <c>CombatSceneContainer/{BgContainer, BackCombatVfxContainer, AllyContainer, EnemyContainer}</c>，
            /// 绘制序 = 树序 ⇒ 插到 <b>index 1</b> 就是"背景之后、人物之前"。
            /// </para>
            /// <para>用户口径：<b>目前常启</b>（不做开关），贴图先给透明空槽，出图后同名覆盖。</para>
            /// </remarks>
            public const string DomainScene = Root + "/scenes/combat/aeonglass_domain.tscn";

            /// <summary>
            /// 「领域」层的贴图（<b>透明空槽</b>，2048×960 —— 与本体第三章 glory 背景同画布，出图后同名覆盖）。
            /// </summary>
            public const string AeonglassDomainLayer = ImageDir + "domain/aeonglass_domain_layer.png";

            /// <summary>时空裂隙「坍缩 / 唤回」按键的 3 层月牙贴图（256×256，画布已对齐）。</summary>
            /// <remarks>
            /// <para>叠放顺序（自下而上）：<see cref="Rim" /> → inner → <see cref="Crack" />。</para>
            /// <para>
            /// ⚠️ <see cref="Crack" /> 是<b>近黑</b>的（均色约 (34,30,22)），所以"改 modulate 变玫红/白"
            /// <b>做不到</b> —— 场景里给它挂了 <c>mix(原色 → tint, amount)</c> 的两行 shader 材质，
            /// 闪色靠 shader 的 <c>tint</c>/<c>amount</c>，不是 modulate。
            /// </para>
            /// </remarks>
            public static class CollapseButton
            {
                private const string Dir = ImageDir + "collapse/";

                /// <summary>外月牙（金边）—— 静止，永远在最底层。</summary>
                public const string Rim = Dir + "collapse_rim_256.png";

                /// <summary>内月牙 · 原色（灰青玻璃）= 常态 A。</summary>
                public const string InnerNormal = Dir + "collapse_inner_normal_256.png";

                /// <summary>内月牙 · 玫红 = 常态 B（信号色，没吃做旧）。</summary>
                public const string InnerRose = Dir + "collapse_inner_rose_256.png";

                /// <summary>裂缝（近黑，单独一层，颜色靠 shader tint）。</summary>
                public const string Crack = Dir + "collapse_crack_256.png";
            }

            /// <summary>时之沙 沙砾：卡面费用显示场景。</summary>
            public const string SandCardCostScene = Root + "/scenes/ui/secondary_resource/aeonglass_sand_card_cost.tscn";

            /// <summary>
            /// 时之沙 沙砾计数器艺术的 5 层贴图（17 张 256×256，画布已对齐 —— 按同一个矩形铺满即可，不要逐张调位置）。
            /// </summary>
            /// <remarks>
            /// <para>
            /// <b>档位</b>：<see cref="Top" /> 画的是 <c>level</c> 份沙、<see cref="Bottom" /> 画的是
            /// <c>6 − level</c> 份沙 —— 两层用<b>同一个 level</b>（= 当前沙砾值），所以
            /// <b>上 + 下 恒等于 6</b>，调用方不需要自己算 6 − level。
            /// </para>
            /// <para><c>level 6</c> = 沙全在上腔（满）；<c>level 0</c> = 沙全落到底（耗尽）。</para>
            /// </remarks>
            public static class SandCounter
            {
                /// <summary>档位数 − 1（= 沙砾容量）。</summary>
                public const int MaxLevel = 6;

                private const string Dir = ImageDir + "sand_counter/";

                /// <summary>L1 玻璃壳 + 上下金盖 + 宝珠底座（静止，最底层）。</summary>
                public const string Shell = Dir + "aeonglass_sand_counter_layer_1_shell.png";

                /// <summary>L5 宝珠 + 光晕（既有蓝色光晕，也靠 modulate 变红 / 闪白）。</summary>
                public const string Orb = Dir + "aeonglass_sand_counter_layer_5_orb.png";

                /// <summary>L2 上腔沙：画 <paramref name="level" /> 份沙。</summary>
                public static string Top(int level) =>
                    Dir + "aeonglass_sand_counter_top_" + ClampLevel(level) + ".png";

                /// <summary>L3 下腔沙：画 <c>6 − <paramref name="level" /></c> 份沙。</summary>
                public static string Bottom(int level) =>
                    Dir + "aeonglass_sand_counter_bottom_" + ClampLevel(level) + ".png";

                /// <summary>把档位夹到 <c>0..6</c>（资源侧本来就有硬顶，这里只是兜底）。</summary>
                public static int ClampLevel(int level) => level < 0 ? 0 : level > MaxLevel ? MaxLevel : level;
            }

            /// <summary>
            /// 时空裂隙 表现层：间隙「传送门」场景（左右各一道，<c>AeonglassGapPortals.cs</c> 实例化两份）。
            /// </summary>
            /// <remarks>
            /// <b>门的视觉全在这个场景里</b>（贴图 / 染色 / 透明度 / 呼吸动画）——
            /// 后续做特效既可以在 Godot 编辑器里改，也可以直接改文本，改完**必须重导 PCK**（改资源只重编译 DLL 不生效）。
            /// </remarks>
            public const string GapPortalScene = Root + "/scenes/ui/gap/aeonglass_gap_portal.tscn";

            /// <summary>牌堆按钮的图标（空槽，同名覆盖即换图）。</summary>
            public static string PileIcon(string stem) => Root + "/images/ui/piles/" + stem + ".png";

            /// <summary>能量计数器的五个图层（128×128 透明空槽，出图后同名覆盖）。</summary>
            public static class Energy
            {
                private const string Dir = ImageDir + "energy/";

                public const string Layer1 = Dir + "aeonglass_orb_layer_1.png";
                public const string Layer2 = Dir + "aeonglass_orb_layer_2.png";
                public const string Layer3 = Dir + "aeonglass_orb_layer_3.png";
                public const string Layer4 = Dir + "aeonglass_orb_layer_4.png";
                public const string Layer5 = Dir + "aeonglass_orb_layer_5.png";

                /// <summary>
                /// 卡面费用宝石 ＋ 卡池/遗物池/药水池大图（128×128，RitsuLib 让这两处共用同一张）。
                /// </summary>
                public const string Icon = Dir + "aeonglass_energy_icon.png";

                /// <summary>
                /// 卡面文案**行内**的能量小图标（24×24）。富文本 <c>[img]</c> 按原始尺寸渲染，
                /// 所以它必须是单独出的小图，不能拿 128 那张缩（缩了会糊）。
                /// </summary>
                public const string TextIcon = Dir + "aeonglass_energy_icon_text.png";
            }

            /// <summary>多人手势图（422×1200 透明空槽）。</summary>
            public static class Hands
            {
                private const string Dir = ImageDir + "hands/";

                public const string Point = Dir + "multiplayer_hand_aeonglass_point.png";
                public const string Rock = Dir + "multiplayer_hand_aeonglass_rock.png";
                public const string Paper = Dir + "multiplayer_hand_aeonglass_paper.png";
                public const string Scissors = Dir + "multiplayer_hand_aeonglass_scissors.png";
            }
        }

        /// <summary>时空裂隙 表现层用的特效贴图（② 特效阶段搬进来的，出正式美术后同名覆盖）。</summary>
        public static class Vfx
        {
            private const string Dir = Root + "/images/vfx/aeonglass/";

            /// <summary>射线/环（做传送门框与射线）。</summary>
            public const string Ray = Dir + "aeonglass_ray.png";

            /// <summary>球核（做门内核心）。</summary>
            public const string Glob = Dir + "aeonglass_glob.png";

            /// <summary>门内滚动烟雾（"流动的时间"）。</summary>
            public const string Smoke = Dir + "aeonglass_scrolling_smoke.png";

        }

        /// <summary>
        /// 卡牌框材质（<b>双色 · 对角分割</b>）——每张框一份，只差 <c>uv_rect</c>。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 卡框贴图是本体 <c>ui_atlas_0.png</c> 的 <c>AtlasTexture</c> 切片，UV 是**图集坐标**，
        /// 分割线 / 面板 / 图标都要先把坐标归一化回卡框本地像素 ⇒ 每种框必须单独一份材质。
        /// </para>
        /// <para>
        /// 颜色**不在这里**：按 [永时] / [消逝] 换色走
        /// <c>Core/Presentation/Art/AeonglassFramePalette</c> 与 <c>AeonglassFrameMaterial</c>
        /// （那里 <c>Duplicate()</c> 出一份一份的染色材质，不碰这三份共享资源）。
        /// </para>
        /// </remarks>
        public static class CardFrame
        {
            private const string Dir = Root + "/materials/cards/frames/";

            /// <summary>攻击框（图集切片 (1320, 83, 598, 844)）。</summary>
            public const string Attack = Dir + "aeonglass_frame_attack_mat.tres";

            /// <summary>技能框（同时兜住"状态牌"——本体给状态 / 诅咒用的是技能框）。</summary>
            public const string Skill = Dir + "aeonglass_frame_skill_mat.tres";

            /// <summary>能力框。</summary>
            public const string Power = Dir + "aeonglass_frame_power_mat.tres";
        }

        /// <summary>遗物：自己的图标（空槽）。</summary>
        public static class Relic
        {
            private const string Dir = Root + "/images/relics/";

            /// <summary>初始遗物「沙漏之心」的图标（128×128 透明空槽，出图后同名覆盖）。</summary>
            public const string Core = Dir + "aeonglass_core.png";

            /// <summary>
            /// 事件遗物「破碎的怀表」的图标（128×128 透明空槽，出图后同名覆盖）。
            /// </summary>
            public const string BrokenWatch = Dir + "aeonglass_broken_watch.png";
        }

        /// <summary>
        /// 事件：立绘空槽。
        /// </summary>
        /// <remarks>
        /// <b>必须有这张图</b>：本体事件立绘的默认路径是按事件 id 推出来的
        /// （<c>res://images/events/&lt;id 小写&gt;.png</c>），文件不存在时
        /// <c>NEventRoom.SetupLayout()</c> 里的 <c>CreateInitialPortrait</c> 会抛
        /// <c>AssetLoadException</c> ⇒ <b>整个事件布局初始化中断、界面上一个选项都没有</b>（实测踩到）。
        /// 这里显式给 <c>EventAssetProfile.InitialPortraitPath</c> 指到我们的空槽图，出图后同名覆盖即可。
        /// </remarks>
        public static class Event
        {
            private const string Dir = Root + "/images/events/";

            /// <summary>「破碎的怀表」事件的立绘（空槽，出图后同名覆盖）。</summary>
            public const string BrokenWatchPortrait = Dir + "aeonglass_broken_watch_event.png";
        }

        /// <summary>次级资源（时之沙）的图标：全是空槽，出图后同名覆盖。</summary>
        public static class SecondaryResource
        {
            private const string Dir = Root + "/images/ui/resources/";

            /// <summary>沙砾：小图（战斗计数器 / 卡面费用用）。</summary>
            public const string SandSmall = Dir + "sand_small.png";

            /// <summary>沙砾：大图。</summary>
            public const string SandLarge = Dir + "sand_large.png";

            /// <summary>空隙：小图。</summary>
            public const string VoidSmall = Dir + "void_small.png";

            /// <summary>空隙：大图。</summary>
            public const string VoidLarge = Dir + "void_large.png";
        }

        /// <summary>牌堆（模组牌区）的模组内 ID 词根；完整 ID 由框架拼成 <c>{MOD}_CARDPILE_{STEM}</c>。</summary>
        public static class CardPiles
        {
            /// <summary>搁浅 搁浅牌堆。</summary>
            public const string EmptyZoneStem = "empty_zone";

            /// <summary>时空裂隙牌堆。</summary>
            public const string TimeGapStem = "time_gap";

            /// <summary>时空裂隙的**显示**牌堆（额外手牌容器；存储仍在 <see cref="TimeGapStem" />）。</summary>
            public const string TimeGapViewStem = "time_gap_view";

            /// <summary>双卡组 第二副牌组（"非生效那一副"的常驻牌堆）。</summary>
            public const string DeckBStem = "deck_b";
        }

        /// <summary>
        /// **跑局预载清单**：交给 <c>CharacterModel.ExtraAssetPaths</c>，让本体的跑局预载把我们一起读进来
        /// （现在是"首次用到才加载"，领域层那张 2.69MB 的图正好压在"进战斗"那一帧）。
        /// </summary>
        /// <remarks>
        /// <para>
        /// ① 只列**代码里直接读**的资源 + 我们自己的 UI 场景；角色 <c>AssetPaths</c> 已经覆盖的
        /// （战斗外观 / 能量计数器 / 火堆 / 商店 / 选人背景 / 图标）不重复列；**选人界面专用图不列**。
        /// </para>
        /// <para>
        /// ② 依赖 Godot 的递归加载：列 <c>.tscn</c> 会连带读它的 <c>ext_resource</c>
        /// ⇒ 场景内部的贴图（沙砾计数器 5 层、传送门自带美术）**不必逐张列**。
        /// </para>
        /// <para>
        /// ③ ⚠️ 加新美术时：能被场景带进来的就别加这里；只有"代码直接 <c>Load</c>"的才加。
        /// 实际交给本体前会先过一遍存在性检查（见 <c>AssetPreload.Verified</c>），缺失按美术缺失忽略。
        /// </para>
        /// </remarks>
        public static class Preload
        {
            /// <summary>跑局（含战斗）会用到、且本体不会自动预载的那些。</summary>
            public static readonly string[] RunAndCombat =
            [
                // 我们自己的 UI 场景（代码里按路径实例化）
                Ui.GapPortalScene,
                Ui.CollapseButtonArtScene,
                Ui.SandCounterScene,
                Ui.SandCounterArtScene,
                Ui.SandCardCostScene,
                Ui.DomainScene,

                // 代码里直接 Load 的贴图
                Vfx.Ray,
                Vfx.Glob,
                Vfx.Smoke,
                Ui.PileIcon(CardPiles.EmptyZoneStem),
                Ui.PileIcon(CardPiles.TimeGapStem),
                Ui.PileIcon(CardPiles.DeckBStem),
                Ui.PileIcon("deck_a"),      // 顶栏"当前生效那一副"的按钮图标（DeckButtonIcons 直接读）

                // 卡框材质（AeonglassFrameMaterial 直接读）
                CardFrame.Attack,
                CardFrame.Skill,
                CardFrame.Power,
            ];
        }
    }

    /// <summary>惰性创建的日志器，前缀就是模组 id。</summary>
    public static Logger Logger => _logger ??= RitsuLibFramework.CreateLogger(ModId);

    private static Logger? _logger;
}
