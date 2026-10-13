using Godot;

using STS2_Aeonglass.Core.Config;

namespace STS2_Aeonglass.Core.Presentation.UI;

/// <summary>把意图卡带的调度器挂进战斗场景（面板 / 卡带都随房间销毁）。</summary>
internal static class MonsterIntentBeltHost
{
    internal static void Attach(Node parent)
    {
        AeonglassSettingStore.Initialize();

        var setting = AeonglassSettingStore.Current;
        if (!setting.IntentEnabled || !setting.IntentIntentBelt)
        {
            return;
        }

        if (parent.GetNodeOrNull<MonsterIntentBeltManager>("%" + MonsterIntentBeltManager.NodeName) is not null)
        {
            return;
        }

        var manager = new MonsterIntentBeltManager
        {
            Name = MonsterIntentBeltManager.NodeName,
            UniqueNameInOwner = true,
            ZIndex = 215,
        };

        parent.AddChild(manager);
    }
}
