using System.Collections.Generic;
using UnityEngine;
using FishNet;

public class BulletPoolController : MonoBehaviour
{
    public static BulletPoolController Instance { get; private set; }

    private Transform _poolParent;

    [Header("Network Pool Settings")]
    [SerializeField] private GameObject _networkBulletPrefab;
    [SerializeField] private int _networkInitialSize;
    [SerializeField] private bool _networkExpandIfNeeded = true;
    [field: SerializeField] public Color[] BulletColor { get; set; }

    private readonly List<GameObject> _networkPool = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _poolParent = new GameObject("BulletPool").transform;
        _poolParent.SetParent(transform, false);
    }

    public void EnsureNetworkPrefab(GameObject prefab)
    {
        if (_networkBulletPrefab == null && prefab != null)
            _networkBulletPrefab = prefab;
    }

    public void PrewarmNetworkPool(int count)
    {
        if (!InstanceFinder.IsServerStarted || count <= 0)
            return;

        for (int i = 0; i < count; i++)
            CreatePooledNetworkBullet();
    }

    private GameObject CreatePooledNetworkBullet()
    {
        if (_networkBulletPrefab == null)
            return null;
        GameObject go = Instantiate(_networkBulletPrefab, _poolParent);
        go.SetActive(false);

        var nob = go.GetComponent<FishNet.Object.NetworkObject>();
        if (nob != null)
        {
            try
            {
                nob.ResetState(asServer: true);
                nob.ResetState(asServer: false);
            }
            catch { }
        }
        _networkPool.Add(go);
        return go;
    }

    public GameObject SpawnNetworkBullet(Vector3 position, Quaternion rotation, FishNet.Connection.NetworkConnection owner = null, float speed = 0f, float lifeTime = 0f, GameObject ownerObject = null, Color color = default)
    {
        if (!InstanceFinder.IsServerStarted)
            return null;

        GameObject go = null;
        for (int i = 0; i < _networkPool.Count; i++)
        {
            if (!_networkPool[i].activeInHierarchy)
            {
                go = _networkPool[i];
                _networkPool.RemoveAt(i);
                break;
            }
        }

        if (go == null)
        {
            if (_networkExpandIfNeeded && _networkBulletPrefab != null)
            {
                go = Instantiate(_networkBulletPrefab, _poolParent);
                go.SetActive(false);
            }
            else
                return null;
        }

        go.transform.SetParent(null);
        go.transform.position = position;
        go.transform.rotation = rotation;

        if (go.TryGetComponent<BulletController>(out var bc))
        {
            bc.Init(speed, lifeTime);
            if (ownerObject != null)
                bc.IgnoreCollisionWith(ownerObject);

            if (owner != null)
                bc.SetAttackerConnection(owner);
        }

        var nm = InstanceFinder.NetworkManager;
        if (nm == null || nm.ServerManager == null)
        {
            Destroy(go);
            return null;
        }

        var nob = go.GetComponent<FishNet.Object.NetworkObject>();
        if (nob == null || !nob.IsSpawned)
        {
            if (nob != null && nob.ObjectId != FishNet.Object.NetworkObject.UNSET_OBJECTID_VALUE)
            {
                try
                {
                    nob.ResetState(asServer: true);
                    nob.ResetState(asServer: false);
                }
                catch { }
            }

            nm.ServerManager.Spawn(go);
        }

        go.SetActive(true);

        // if (bc != null && color.a > 0f)
        // {
        //     bc.SetColor(color);
        //     bc.Observers_SetColor(color);
        // }

        return go;
    }

    public void ReturnNetworkBullet(GameObject bullet)
    {
        if (bullet == null)
            return;

        var nm = InstanceFinder.NetworkManager;
        if (nm == null || nm.ServerManager == null)
        {
            Destroy(bullet);
            return;
        }

        nm.ServerManager.Despawn(bullet, global::FishNet.Object.DespawnType.Pool);

        if (bullet.TryGetComponent<FishNet.Object.NetworkObject>(out var nob))
        {
            try
            {
                nob.ResetState(asServer: true);
                nob.ResetState(asServer: false);
            }
            catch { }
        }

        if (!_networkPool.Contains(bullet))
        {
            var bc = bullet.GetComponent<global::BulletController>();
            if (bc != null)
                bc.ResetState();

            bullet.SetActive(false);
            bullet.transform.SetParent(_poolParent, false);
            _networkPool.Add(bullet);
        }
    }
}
