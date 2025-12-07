using System.Collections;
using FishNet;
using FishNet.Object;
using FishNet.Connection;
using UnityEngine;
using TankBattle.Tank;

[RequireComponent(typeof(NetworkObject))]
public class BulletController : NetworkBehaviour
{
    [SerializeField] private float Speed = 10f;
    [SerializeField] private float LifeTime = 3f;

    private float _defaultSpeed;
    private float _defaultLifeTime;

    private Rigidbody2D _rb;
    private Coroutine _lifeCoroutine;
    private Collider2D[] _myColliders;
    private Collider2D[] _ignoredOwnerColliders;

    // private Renderer[] _renderers;
    private Color[] _defaultRendererColors;
    // attacker connection set when spawned
    private NetworkConnection _attackerConnection;

    [Header("Damage")]
    [SerializeField] private int Damage = 10;
    [SerializeField] private float KnockbackStrength = 2f;
    [SerializeField] private float StaggerDuration = 0.4f;
    [SerializeField] private float IFrameDuration = 0.6f;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _myColliders = GetComponentsInChildren<Collider2D>();
        // _renderers = GetComponentsInChildren<Renderer>();

        _defaultSpeed = Speed;
        _defaultLifeTime = LifeTime;

        // if (_renderers != null && _renderers.Length > 0)
        // {
        //     _defaultRendererColors = new Color[_renderers.Length];
        //     for (int i = 0; i < _renderers.Length; i++)
        //     {
        //         var sr = _renderers[i] as SpriteRenderer;
        //         if (sr != null)
        //             _defaultRendererColors[i] = sr.color;
        //         else
        //         {
        //             try { _defaultRendererColors[i] = _renderers[i].sharedMaterial.color; }
        //             catch { _defaultRendererColors[i] = Color.white; }
        //         }
        //     }
        // }
    }

    private void OnEnable()
    {
        if (!IsServerInitialized)
            return;

        if (_lifeCoroutine != null)
        {
            StopCoroutine(_lifeCoroutine);
            _lifeCoroutine = null;
        }
        _lifeCoroutine = StartCoroutine(ReturnAfter(LifeTime));
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        // if (!IsServerInitialized)
        // {
        //     SetRenderersEnabled(false);
        // }
    }

    private void OnDisable()
    {
        if (_lifeCoroutine != null)
        {
            StopCoroutine(_lifeCoroutine);
            _lifeCoroutine = null;
        }
    }

    private IEnumerator ReturnAfter(float t)
    {
        yield return new WaitForSeconds(t);
        if (IsServerInitialized)
            ReturnToPool();
    }

    private void FixedUpdate()
    {
        if (!IsServerInitialized)
            return;

        Vector2 direction = (Vector2)transform.up;
        _rb.linearVelocity = direction * Speed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServerInitialized)
            return;

        var tank = other.GetComponentInParent<TankCore>();
        if (tank != null)
        {
            Vector2 knockback = (tank.transform.position - transform.position).normalized * KnockbackStrength;
            tank.Server_ApplyDamage(_attackerConnection, Damage, knockback, StaggerDuration, IFrameDuration);
        }

        ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (BulletPoolController.Instance != null)
            BulletPoolController.Instance.ReturnNetworkBullet(gameObject);
        else if (IsServerInitialized)
        {
            InstanceFinder.NetworkManager.ServerManager.Despawn(gameObject, DespawnType.Pool);
            var nob = gameObject.GetComponent<NetworkObject>();
            if (nob != null)
            {
                try
                {
                    nob.ResetState(asServer: true);
                    nob.ResetState(asServer: false);
                }
                catch { }
            }
        }
    }

    public void Init(float speed, float lifeTime)
    {
        if (speed > 0f)
            Speed = speed;
        if (lifeTime > 0f)
            LifeTime = lifeTime;

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }
    }

    public void SetAttackerConnection(NetworkConnection conn)
    {
        _attackerConnection = conn;
    }

    public void ResetState()
    {
        Speed = _defaultSpeed;
        LifeTime = _defaultLifeTime;
        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }

        if (_ignoredOwnerColliders != null && _myColliders != null)
        {
            foreach (var oc in _ignoredOwnerColliders)
            {
                if (oc == null)
                    continue;
                foreach (var mc in _myColliders)
                {
                    if (mc == null)
                        continue;
                    Physics2D.IgnoreCollision(mc, oc, false);
                }
            }
        }

        _ignoredOwnerColliders = null;

        // if (_renderers != null && _defaultRendererColors != null)
        // {
        //     for (int i = 0; i < _renderers.Length; i++)
        //     {
        //         if (_renderers[i] == null)
        //             continue;
        //         var sr = _renderers[i] as SpriteRenderer;
        //         if (sr != null)
        //             sr.color = _defaultRendererColors[i];
        //         else
        //         {
        //             var renderer = _renderers[i];
        //             var mpb = new MaterialPropertyBlock();
        //             mpb.SetColor("_BaseColor", _defaultRendererColors[i]);
        //             mpb.SetColor("_Color", _defaultRendererColors[i]);
        //             renderer.SetPropertyBlock(mpb);
        //         }
        //     }
        //     // SetRenderersEnabled(true);
        // }
    }

    public void IgnoreCollisionWith(GameObject owner)
    {
        if (owner == null)
            return;
        var ownerCols = owner.GetComponentsInChildren<Collider2D>();
        if (ownerCols == null || ownerCols.Length == 0)
            return;
        _ignoredOwnerColliders = ownerCols;

        _myColliders ??= GetComponentsInChildren<Collider2D>();
        foreach (var oc in ownerCols)
        {
            if (oc == null)
                continue;
            foreach (var mc in _myColliders)
            {
                if (mc == null)
                    continue;
                Physics2D.IgnoreCollision(mc, oc, true);
            }
        }
    }

    // public void SetColor(Color color)
    // {
    //     _renderers ??= GetComponentsInChildren<Renderer>();
    //     if (_renderers == null)
    //         return;

    //     for (int i = 0; i < _renderers.Length; i++)
    //     {
    //         var r = _renderers[i];
    //         if (r == null)
    //             continue;

    //         var sr = r as SpriteRenderer;
    //         if (sr != null)
    //         {
    //             float a = sr.color.a;
    //             sr.color = new Color(color.r, color.g, color.b, a);
    //         }
    //         else
    //         {
    //             var mpb = new MaterialPropertyBlock();
    //             r.GetPropertyBlock(mpb);
    //             float a = 1f;
    //             if (_defaultRendererColors != null && i < _defaultRendererColors.Length)
    //                 a = _defaultRendererColors[i].a;
    //             Color setColor = new Color(color.r, color.g, color.b, a);
    //             mpb.SetColor("_BaseColor", setColor);
    //             mpb.SetColor("_Color", setColor);
    //             r.SetPropertyBlock(mpb);
    //         }
    //     }

    //     // SetRenderersEnabled(true);
    // }

    // [ObserversRpc]
    // public void Observers_SetColor(Color color)
    // {
    //     SetColor(color);
    // }

    // private void SetRenderersEnabled(bool enabled)
    // {
    //     _renderers ??= GetComponentsInChildren<Renderer>();
    //     if (_renderers == null)
    //         return;
    //     for (int i = 0; i < _renderers.Length; i++)
    //     {
    //         if (_renderers[i] == null)
    //             continue;
    //         _renderers[i].enabled = enabled;
    //     }
    // }
}
