using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Connection;
using UnityEngine;

namespace TankBattle.Tank
{
    [RequireComponent(typeof(TankController))]
    public class TankCore : NetworkBehaviour
    {
        [field: SerializeField] public float HP { get; set; } = 100f;
        [field: SerializeField] public float MaxHP { get; set; } = 100;

        public event Action<float> OnHPChanged;

        public bool IsStaggered => _isStaggered;
        public bool IsIFrame => _isIFrame;

        private Rigidbody2D _rb;
        public TankController TankController { get; set; }
        public Vector3 SpawnPosition { get; private set; }

        private bool _isStaggered = false;
        private bool _isIFrame = false;

        private readonly Dictionary<string, float> _activeEffects = new(StringComparer.Ordinal);

        private Coroutine _blinkCoroutine;
        private Coroutine _iframeCoroutine;
        private Renderer[] _cachedRenderers;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (_rb == null)
                _rb = GetComponent<Rigidbody2D>();

            TankController = GetComponent<TankController>();

            _cachedRenderers = GetComponentsInChildren<Renderer>(true);

            SpawnPosition = transform.position;

            GameRoundController.Instance.RegisterTank(this);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            GameRoundController.Instance.UnregisterTank(this);
        }

        // private void OnDestroy()
        // {
        //     GameRoundController.Instance.UnregisterTank(this);
        // }

        #region HP
        public void Server_ApplyDamage(NetworkConnection attacker, int damage, Vector2 knockback, float stagger = 0.4f, float iframe = 0.6f)
        {
            if (!IsServerInitialized)
                return;
            if (_isIFrame)
                return;

            HP -= Math.Max(0, damage);
            if (HP <= 0)
            {
                HP = 0;
                HandleDeath(attacker);
                return;
            }

            // var knock = StatusEffectFactory.Create(StatusEffectType.Knockback, 0.15f, knockback) as KnockbackEffect;
            var stag = StatusEffectFactory.Create(StatusEffectType.Stagger, stagger);
            var ifr = StatusEffectFactory.Create(StatusEffectType.IFrame, iframe);

            // knock?.Apply(this, attacker, damage, knockback);
            stag?.Apply(this, attacker, damage, knockback);
            ifr?.Apply(this, attacker, damage, knockback);

            StartCoroutine(ProcessEffectTimers());

            Observers_UpdateHP(HP);
        }

        private void HandleDeath(NetworkConnection attacker)
        {
            Observers_UpdateHP(HP);
            SetStagger(true);
            SetIFrame(true);
            // Observers_OnDead();

            if (IsServerInitialized)
            {
                var nob = GetComponent<NetworkObject>();
                GameRoundController.Instance.Server_PlayerDied(attacker, nob);
            }
        }
        #endregion

        #region Respawn
        public void ClearEffects()
        {
            if (!IsServerInitialized) return;
            _activeEffects.Clear();
            _isStaggered = false;
            _isIFrame = false;

            Observers_OnStaggerEnd();
            Observers_OnIFrameEnd();

            if (_blinkCoroutine != null)
            {
                StopCoroutine(_blinkCoroutine);
                _blinkCoroutine = null;
            }

            if (_iframeCoroutine != null)
            {
                StopCoroutine(_iframeCoroutine);
                _iframeCoroutine = null;
            }
        }

        public void Server_RespawnAfter(float seconds)
        {
            if (!IsServerInitialized) return;
            StartCoroutine(RespawnCoroutine(seconds));
        }

        private IEnumerator RespawnCoroutine(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            Server_RespawnAt(SpawnPosition);
        }

        public void Server_RespawnAt(Vector3 pos)
        {
            if (!IsServerInitialized)
                return;

            HP = MaxHP;
            ClearEffects();

            transform.position = pos;
            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.angularVelocity = 0f;
            }

            Observers_UpdateHP(HP);

            SetIFrame(true, 1.0f);
            Observers_OnRespawn();
            Observers_Teleport(pos);
        }
        #endregion

        #region StatusEffect
        public void AddOrRefreshEffect(IStatusEffect effect, NetworkConnection attacker, int damage, Vector2 knockback)
        {
            if (!IsServerInitialized)
                return;
            float now = Time.time;
            float end = now + effect.Duration;
            _activeEffects[effect.EffectId] = end;
        }

        public void ApplyServerKnockback(Vector2 impulse)
        {
            if (!IsServerInitialized)
                return;
            if (_rb != null)
                _rb.AddForce(impulse, ForceMode2D.Impulse);
        }

        public void SetStagger(bool value)
        {
            if (!IsServerInitialized)
                return;
            _isStaggered = value;
            if (value)
                Observers_OnStaggerBegin();
            else
                Observers_OnStaggerEnd();
        }

        public void SetIFrame(bool value, float duration = 0f)
        {
            if (!IsServerInitialized)
                return;
            _isIFrame = value;
            if (value)
            {
                Observers_OnIFrameBegin(duration);

                if (duration > 0f)
                {
                    if (_iframeCoroutine != null)
                        StopCoroutine(_iframeCoroutine);
                    _iframeCoroutine = StartCoroutine(IFrameServerTimer(duration));
                }
            }
            else
            {
                Observers_OnIFrameEnd();

                if (_iframeCoroutine != null)
                {
                    StopCoroutine(_iframeCoroutine);
                    _iframeCoroutine = null;
                }
            }
        }

        private IEnumerator IFrameServerTimer(float duration)
        {
            yield return new WaitForSeconds(duration);

            SetIFrame(false);
            _iframeCoroutine = null;
        }

        private IEnumerator ProcessEffectTimers()
        {
            if (!IsServerInitialized)
                yield break;

            while (_activeEffects.Count > 0)
            {
                float now = Time.time;
                var expired = new List<string>();
                foreach (var kv in _activeEffects)
                {
                    if (kv.Value <= now)
                        expired.Add(kv.Key);
                }

                foreach (var id in expired)
                {
                    _activeEffects.Remove(id);
                    if (id == "stagger")
                        SetStagger(false);
                    if (id == "iframe")
                        SetIFrame(false);
                }

                yield return null;
            }
        }
        #endregion

        #region ObserversRpc
        // [ObserversRpc]
        // public void Observers_OnHit(int damage, Vector2 knockback, float duration, string effectId, NetworkConnection attacker) { }

        [ObserversRpc]
        public void Observers_OnStaggerBegin()
        {
            if (TankController != null && TankController.IsOwner)
                TankController.SetInputEnabled(false);
        }

        [ObserversRpc]
        public void Observers_OnStaggerEnd()
        {
            if (TankController != null && TankController.IsOwner)
                TankController.SetInputEnabled(true);
        }

        [ObserversRpc]
        public void Observers_OnIFrameBegin(float duration)
        {
            if (_blinkCoroutine != null)
                StopCoroutine(_blinkCoroutine);

            // _cachedRenderers ??= GetComponentsInChildren<Renderer>(true);
            _blinkCoroutine = StartCoroutine(BlinkRoutine(duration));
        }

        [ObserversRpc]
        public void Observers_OnIFrameEnd()
        {
            if (_blinkCoroutine != null)
            {
                StopCoroutine(_blinkCoroutine);
                _blinkCoroutine = null;
            }

            // _cachedRenderers ??= GetComponentsInChildren<Renderer>(true);

            if (_cachedRenderers != null)
                foreach (var r in _cachedRenderers)
                    if (r != null)
                        r.enabled = true;
        }

        private IEnumerator BlinkRoutine(float duration)
        {
            // _cachedRenderers ??= GetComponentsInChildren<Renderer>(true);

            float elapsed = 0f;
            float interval = 0.12f;
            bool visible = true;

            while (elapsed < duration)
            {
                if (_cachedRenderers != null)
                    foreach (var r in _cachedRenderers)
                        if (r != null)
                            r.enabled = visible;

                yield return new WaitForSeconds(interval);
                elapsed += interval;
                visible = !visible;
            }

            if (_cachedRenderers != null)
                foreach (var r in _cachedRenderers)
                    if (r != null)
                        r.enabled = true;


            _blinkCoroutine = null;
        }

        // [ObserversRpc]
        // public void Observers_OnDead() { }

        [ObserversRpc]
        public void Observers_OnRespawn()
        {
            if (TankController != null && TankController.IsOwner)
                TankController.SetInputEnabled(true);

            // _cachedRenderers ??= GetComponentsInChildren<Renderer>(true);
            if (_cachedRenderers != null)
                foreach (var r in _cachedRenderers)
                    if (r != null)
                        r.enabled = true;


            var clientUI = FindAnyObjectByType<ClientRoundUI>();
            if (clientUI != null)
                clientUI.HideAll();
        }

        [ObserversRpc(RunLocally = true)]
        private void Observers_Teleport(Vector3 pos)
        {
            transform.position = pos;

            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.angularVelocity = 0f;
            }
        }

        [ObserversRpc]
        public void Observers_UpdateHP(float hp)
        {
            HP = hp;
            OnHPChanged?.Invoke(hp);
        }
        #endregion

        #region TargetRpc
        [TargetRpc]
        public void Target_ShowRoundEnd(NetworkConnection conn, string message, float seconds)
        {
            var clientUI = ClientRoundUI.Instance;
            if (clientUI != null)
                clientUI.ShowDeathAndStartCountdown(message, seconds);
        }
        #endregion
    }
}

