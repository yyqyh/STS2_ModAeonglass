using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2_Aeonglass.Core.Systems.Intent;

namespace STS2_Aeonglass.Core.Content.Cards.Templates;

/// <summary>常用效果工厂：伤害 / 格挡 / 抽牌 / 挂能力 / 眩晕 / 条件 / 自定义。</summary>
public static class CardEffects
{
    public const string DefaultAttackFx = "vfx/vfx_attack_blunt";

    public static CardEffect Damage(
        string var = "Damage",
        string hitFx = DefaultAttackFx) =>
        new($"damage:{var}", async ctx =>
        {
            var amount = ctx.Card.ResolveDamageThisPlay(var);
            if (amount <= 0m)
            {
                return;
            }

            var attack = DamageCmd.Attack(amount)
                .FromCard(ctx.Card, ctx.Play)
                .WithHitFx(hitFx);

            if (ctx.Card.TargetType == TargetType.AllEnemies)
            {
                if (ctx.Card.Owner.Creature.CombatState is { } combat)
                {
                    await attack.TargetingAllOpponents(combat).Execute(ctx.ChoiceContext);
                }

                return;
            }

            var target = ctx.Target;
            if (target is null)
            {
                throw new InvalidOperationException($"{ctx.Card.Id.Entry} 需要目标，但 CardPlay.Target 为空");
            }

            await attack.Targeting(target).Execute(ctx.ChoiceContext);
        });

    public static CardEffect DamageFixed(
        decimal amount,
        string key = "damage-fixed",
        string hitFx = DefaultAttackFx) =>
        new($"damage-fixed:{key}", async ctx =>
        {
            if (amount <= 0m)
            {
                return;
            }

            var attack = DamageCmd.Attack(amount)
                .FromCard(ctx.Card, ctx.Play)
                .WithHitFx(hitFx);

            if (ctx.Card.TargetType == TargetType.AllEnemies)
            {
                if (ctx.Card.Owner.Creature.CombatState is { } combat)
                {
                    await attack.TargetingAllOpponents(combat).Execute(ctx.ChoiceContext);
                }

                return;
            }

            var target = ctx.Target;
            if (target is null)
            {
                throw new InvalidOperationException($"{ctx.Card.Id.Entry} 需要目标，但 CardPlay.Target 为空");
            }

            await attack.Targeting(target).Execute(ctx.ChoiceContext);
        });

    public static CardEffect GainBlock(string var = "Block") =>
        new($"block:{var}", async ctx =>
        {
            var amount = ctx.Card.ResolveBlockThisPlay(var);
            if (amount > 0m)
            {
                await CreatureCmd.GainBlock(ctx.Card.Owner.Creature, amount, ValueProp.Move, ctx.Play);
            }
        });

    public static CardEffect GainBlockFixed(decimal amount, string key = "block-fixed") =>
        new($"block-fixed:{key}", async ctx =>
        {
            if (amount > 0m)
            {
                await CreatureCmd.GainBlock(ctx.Card.Owner.Creature, amount, ValueProp.Move, ctx.Play);
            }
        });

    public static CardEffect Draw(string var) =>
        new($"draw:{var}", ctx => CardPileCmd.Draw(ctx.ChoiceContext, (int)ctx.Vars[var].BaseValue, ctx.Player));

    public static CardEffect ApplyPower<TPower>(
        string? amountVar = null,
        decimal amount = 1m,
        bool targetSelf = true)
        where TPower : PowerModel =>
        new($"power:{typeof(TPower).Name}", async ctx =>
        {
            var target = targetSelf ? ctx.Card.Owner.Creature : ctx.Target ?? ctx.Card.Owner.Creature;
            var value = amountVar is null ? amount : ctx.Vars[amountVar].BaseValue;
            await PowerCmd.Apply<TPower>(
                ctx.ChoiceContext, target, value, ctx.Card.Owner.Creature, ctx.Card);
        });

    public static CardEffect StunTarget() =>
        new("stun-target", async ctx =>
        {
            if (ctx.Target is { } target)
            {
                await CreatureCmd.Stun(target, IntentOps.FallbackNextMoveId(target));
            }
        });

    public static CardEffect If(
        string key,
        Func<CardEffectContext, bool> condition,
        CardEffect then) =>
        new($"if:{key}", ctx => condition(ctx) ? then.Apply(ctx) : Task.CompletedTask);

    public static CardEffect Log(
        string key,
        Func<CardEffectContext, string> message,
        string? prefix = null) =>
        new($"log:{key}", ctx =>
        {
            Const.Logger.Info(
                $"[{Const.ModId}] {(string.IsNullOrWhiteSpace(prefix) ? Const.Prefix.Host : prefix)} {message(ctx)}");
            return Task.CompletedTask;
        });

    public static CardEffect Custom(string key, Func<CardEffectContext, Task> apply) =>
        new($"custom:{key}", apply);
}
