using Godot;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes;

using STS2RitsuLib.CardPiles.Nodes;

using STS2_Aeonglass.Core.Systems;
using STS2_Aeonglass.Core.Systems.Decks;
using STS2_Aeonglass.Core.Systems.Persistence;

using STS2_Aeonglass.Core.Presentation.Support;

using STS2_Aeonglass.Core.Systems.Bootstrap;
namespace STS2_Aeonglass.Core.Presentation.Combat;
/// <summary>顶栏"卡组按钮"的图标替换（双卡组示意）。</summary>
/// <remarks>
/// ① 职责：给顶栏牌堆/卡组按钮换上我们的图标。
/// ② 调用时机：切副、主动刷新、<c>DeckTipSwapPatch</c> 命中时。
/// ③ 依赖：顶栏按钮节点（<c>NodeSearch</c> 找 <c>NModCardPileButton</c>）+ <c>Const.Paths.Ui.*</c>。
/// ④ ⚠️ 找不到按钮就跳过（不同版本顶栏结构会变）；只改图标，不动按钮行为。
/// </remarks>

internal static class DeckButtonIcons
{
    /// <summary>原卡组（永时）的图标词根。</summary>
    private const string PrimaryStem = "deck_a";

    /// <summary>副卡组（消逝）的图标词根 —— 反色版。</summary>
    private const string SecondaryStem = Const.Paths.CardPiles.DeckBStem;

    /// <summary>图标节点路径 —— 本体那张和 RitsuLib 的牌堆按钮**都**叫这个（实测一致）。</summary>
    private const string IconNodePath = "Control/Icon";

    /// <summary>按"哪颗按钮现在装着哪一副"刷两颗按钮的图标（幂等：贴图没变就不动）。</summary>
    public static void Refresh(Player player)
    {
        if (!CharacterGate.IsActive || !DeckModule.Enabled)
        {
            return;
        }

        var activeIndex = ActiveDeckProvider.ActiveIndexOf(player);
        var topBar = NRun.Instance?.GlobalUi?.TopBar;

        // 本体「卡组」按钮 = 生效中那副。
        if (topBar?.Deck is { } vanillaDeck)
        {
            SetIcon(vanillaDeck, activeIndex);
        }

        // 副卡组按钮 = 不生效那副（递归按牌堆类型认，不靠节点名 / 顺序）。
        if (DeckPiles.DeckBPileType is { } deckBType
            && NodeSearch.FindDescendant<NModCardPileButton>(topBar, button => button.Definition?.PileType == deckBType) is { } modDeckB)
        {
            SetIcon(modDeckB, 1 - activeIndex);
        }
    }

    private static void SetIcon(Node button, int deckIndex)
    {
        if (button.GetNodeOrNull<TextureRect>(IconNodePath) is not { } icon)
        {
            return;
        }

        var path = Const.Paths.Ui.PileIcon(deckIndex == 1 ? SecondaryStem : PrimaryStem);
        if (icon.Texture?.ResourcePath == path)
        {
            return;
        }

        // 贴图静态缓存：同一张图标不要每次都走一遍 ResourceLoader（切换卡组会反复调这里）。
        if (!TextureCache.TryGetValue(path, out var texture) || !GodotObject.IsInstanceValid(texture))
        {
            texture = ResourceLoader.Load<Texture2D>(path);
            if (texture is null)
            {
                return;
            }

            TextureCache[path] = texture;
        }

        if (GodotObject.IsInstanceValid(texture))
        {
            icon.Texture = texture;
        }
    }

    /// <summary>图标贴图缓存（路径 → 贴图；进程内一次）。</summary>
    private static readonly Dictionary<string, Texture2D> TextureCache = [];

}
