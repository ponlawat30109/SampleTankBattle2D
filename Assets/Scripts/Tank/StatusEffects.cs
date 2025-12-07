using System;
using UnityEngine;
using FishNet.Connection;

namespace TankBattle.Tank
{
    public interface IStatusEffect
    {
        string EffectId { get; }
        float Duration { get; }
        void Apply(TankCore target, NetworkConnection attacker, int damage, Vector2 knockback);
    }

    public abstract class StatusEffectBase : IStatusEffect
    {
        public abstract string EffectId { get; }
        public float Duration { get; protected set; }

        protected StatusEffectBase(float duration)
        {
            Duration = duration;
        }

        public abstract void Apply(TankCore target, NetworkConnection attacker, int damage, Vector2 knockback);
    }

    public class StaggerEffect : StatusEffectBase
    {
        public override string EffectId => "stagger";

        public StaggerEffect(float duration) : base(duration) { }

        public override void Apply(TankCore target, NetworkConnection attacker, int damage, Vector2 knockback)
        {
            target.AddOrRefreshEffect(this, attacker, damage, knockback);
            target.SetStagger(true);

            // target.Observers_OnHit(damage, knockback, Duration, EffectId, attacker);
        }
    }

    public class IFrameEffect : StatusEffectBase
    {
        public override string EffectId => "iframe";

        public IFrameEffect(float duration) : base(duration) { }

        public override void Apply(TankCore target, NetworkConnection attacker, int damage, Vector2 knockback)
        {
            target.AddOrRefreshEffect(this, attacker, damage, knockback);
            target.SetIFrame(true, Duration);
        }
    }

    public class KnockbackEffect : StatusEffectBase
    {
        public override string EffectId => "knockback";
        public Vector2 Impulse { get; private set; }

        public KnockbackEffect(Vector2 impulse, float duration = 0.15f) : base(duration)
        {
            Impulse = impulse;
        }

        public override void Apply(TankCore target, NetworkConnection attacker, int damage, Vector2 knockback)
        {
            target.ApplyServerKnockback(Impulse);
            target.AddOrRefreshEffect(this, attacker, damage, knockback);
        }
    }
}
