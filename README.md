# STS2_Aeonglass

《杀戮尖塔 2》可玩角色 mod：**永世沙漏（Aeonglass）** —— 把原版 Act 3 boss 转成玩家角色。

## 当前进度

**第 ① 步：人物模型**（已完成；已实机通过）

- 战斗外观 = 原版 boss 的 Spine（`aeonglass.skel`，直接从 `orign` 搬来，未做转换）。
- 玩家侧动画状态机：`Idle/Relaxed→idle_loop`、`Attack→attack_heavy`、`Cast/PowerUp→wither`、
  `Hit→hurt`、`Dead→die`；外环旋转走轨道 1 的 `_track1/rings_normal`。
- 最小可玩壳：三池 + **初始遗物「沙漏之心」**（零效果的占位，但**必须存在**）+ 两张占位基础牌（打击 ×4 / 防御 ×4）。

> ⚠️ **初始遗物是硬需求**：本体 `NCharacterSelectScreen.SelectCharacter` 里写死了
> `characterModel.StartingRelics[0]` —— 角色一件初始遗物都没有时，点它会抛 `ArgumentOutOfRangeException`，
> 选人失败、游戏沿用上一个角色开局（表现为"选了沙漏却是铁甲战士"）。2026-10-11 实测踩到。
- 调试：战斗内面板（显示动画名 / 锚点 / 缩放，可手动触发四态）+ 设置页里的调试总开关（默认关）。
- **不包含任何机制**（M1–M7 一行都没有）。

**第 ② 步：接入特效**（已完成编码与构建验证）

- 把原版 boss 的一整套特效搬进 mod 并接到玩家侧：**液体流动**（`scrolling_smoke` shader）、
  **眼棱**（`aeonglass_ray_shader`）、以及 8 组粒子（倾倒 / 碎片 / 泄漏 / 凋萎 / 顶底火花 / 扬尘 / 土块）。
- 战斗场景 `scenes/combat/aeonglass_combat.tscn` 由 boss 场景 `creature_visuals/aeonglass.tscn` **机械变换**而来
  （45 个子资源逐字保留，只换资源路径 / 根节点 / 锚点）。
- 驱动逻辑 `Core/Presentation/Visuals/AeonglassVfx.cs` = 原版 `NAeonGlassVfx` 的玩家侧移植版
  （**节点查询改成容错**：缺节点只降级 + 打一条警告，不再把战斗搞崩）。
- 交接文档：`STS_MOD\项目文档\aeonglass\永世沙漏_角色mod_第②步特效交接_v1_20261011.md`。

计划与交接文档在 `C:\Users\yyqyhyue\Documents\ChatGPT\STS_MOD\项目文档\aeonglass\`。

## 目录结构

```
STS2_Aeonglass/
├─ Const.cs                      ModId / 全部资源路径（唯一来源）
├─ Main.cs                       入口：注册程序集 + 设置 + Harmony + 内容 id 日志
├─ Core/
│  ├─ Config/                    设置数据 / 存储 / 设置页
│  ├─ Content/
│  │  ├─ Characters/             角色 + 三个池
│  │  └─ Cards/Basic/            两张占位基础牌
│  ├─ Framework/Diagnostics/     占位资源清单 + 内容 id 日志
│  └─ Presentation/
│     ├─ Animations/             骨架动画名常量
│     ├─ Visuals/                战斗外观类（轨道 1 环旋转）
│     └─ UI/                     战斗内调试面板（+ 挂载补丁）
└─ STS2_Aeonglass/               资源根（与 ModId 同名）
   ├─ addons/spine_godot_extension/   编辑器专用（不进 PCK）
   ├─ images/character/spine/         aeonglass.{skel,atlas,png,_2.png}
   ├─ images/ui/ · images/packed/map/icons/   UI 空槽图（替换同名 PNG 即生效）
   ├─ scenes/combat/                  战斗外观场景 + 骨架数据
   ├─ scenes/ui/ · scenes/screens/    顶栏图标场景 / 选人背景场景
   └─ localization/zhs/               characters.json / cards.json
```

## 构建

前提：`local.props` 里的 `Sts2Dir` / `GodotExe` / `RitsuLibDir` 指向本机（首次从 `local.props.template` 复制）。

```powershell
# 只验编译（不碰 Godot，最快）
dotnet build -p:STS2_SKIP_PCK_EXPORT=1

# 正常构建：编译 + 拷进游戏 mods 目录 + 导出 PCK
dotnet build

# 改 C# 想快点：跳过 PCK
dotnet build -p:RunPckExport=false
```

**改 JSON / 图片 / 场景 / 骨架必须重新导 PCK 才生效**（只改 C# 不用）。

### 第一次构建前

Godot 需要先导入资源（生成 `.import` 与 `.godot/imported/*.ctex`），否则 PCK 里没有贴图：

```powershell
& $GodotExe --headless --path . --import
```

仓库里已包含一次导入的结果；换机器 / 换 Godot 版本时要重跑。

> 导入时若看到 `Could not resolve type '...AeonglassCreatureVisuals'`（`TypeLoadException`），
> **属正常现象**：Godot 编辑器进程里没有 `sts2.dll`，解析不了继承自游戏类型的脚本类。
> 只影响编辑器内的脚本类注册，不影响导出，也不影响游戏里加载（游戏进程本身带着 `sts2.dll`）。
> 小丑工程同理。

## 换美术

`STS2_Aeonglass/images/ui/` 下那 6 张是**透明空槽图**，文件名与尺寸就是最终规格 —— 出图后**同名覆盖**即可，代码一行不用改。

当前仍借用本体铁甲战士的资源清单见 `Core/Framework/Diagnostics/PlaceholderAssetRegistry.cs`。

## 调试

设置页 → 本 mod → 「调试」→ 打开**调试总开关**与**战斗内调试面板**，进战斗后左上角出现面板。
默认全关；关掉时零节点、零订阅、零日志。
