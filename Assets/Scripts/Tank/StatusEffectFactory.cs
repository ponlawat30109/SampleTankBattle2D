using System;
using UnityEngine;
using FishNet.Connection;

namespace TankBattle.Tank
{
    public enum StatusEffectType
    {
        Stagger,
        IFrame,
        Knockback
    }

    public static class StatusEffectFactory
    {
        public static IStatusEffect Create(StatusEffectType type, float duration = 0f, Vector2 knockback = default)
        {
            return type switch
            {
                StatusEffectType.Stagger => new StaggerEffect(duration <= 0f ? 0.4f : duration),
                StatusEffectType.IFrame => new IFrameEffect(duration <= 0f ? 0.6f : duration),
                StatusEffectType.Knockback => new KnockbackEffect(knockback, duration <= 0f ? 0.15f : duration),
                _ => throw new Exception("Invalid StatusEffectType"),
            };
        }
    }
}
