using Godot;

namespace STS2_Aeonglass.Core.Systems.Bootstrap;

/// <summary>
/// 把 <see cref="Const.Paths.Preload" /> 的清单交给本体之前，先过一遍**存在性检查**并打一行日志。
/// </summary>
/// <remarks>
/// ① 职责：过滤掉磁盘上不存在的路径（缺失按"美术缺失"忽略，不报错、不让本体崩）+ 一次性日志。
/// ② 调用时机：本体收集角色 <c>AssetPaths</c> 时（即 <c>CharacterModel.ExtraAssetPaths</c> 被读的那一刻）。
/// ③ 依赖：<c>ResourceLoader.Exists</c>。
/// ④ ⚠️ 结果静态缓存一次（本体在不同时机可能读多次 <c>AssetPaths</c>，不能每次都打日志/重复扫盘）。
/// </remarks>

internal static class AssetPreload
{
    private static string[]? _verified;

    /// <summary>上一次过滤用的**入参清单引用**：只有同一个清单再进来才复用缓存（防将来加第二个清单时错返）。</summary>
    private static string[]? _verifiedSource;

    /// <summary>过滤 + 日志（只做一次），返回实际存在的那些路径。</summary>
    internal static string[] Verified(string[] paths)
    {
        // 2026-10-16 加固：缓存必须认入参 —— 原来只有一个全局槽，第二个清单进来会拿到第一份结果。
        if (_verified is { } cached && ReferenceEquals(_verifiedSource, paths))
        {
            return cached;
        }

        var first = _verifiedSource is null;
        var kept = new List<string>(paths.Length);
        var missing = new List<string>();
        foreach (var path in paths)
        {
            if (ResourceLoader.Exists(path))
            {
                kept.Add(path);
            }
            else
            {
                missing.Add(path);
            }
        }

        _verified = [.. kept];
        _verifiedSource = paths;

        // 日志仍只打一次（首份清单）；后续清单只在有缺失时提醒，避免刷屏。
        if (first || missing.Count > 0)
        {
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Host} 资源预载清单：请求 {paths.Length} 条 / 命中 {kept.Count} 条 / 缺失 {missing.Count} 条"
                + (missing.Count > 0 ? $"｜缺失=[{string.Join(", ", missing)}]（按美术缺失忽略）" : "（都会由本体跑局预载读进来）"));
        }

        return _verified;
    }
}
