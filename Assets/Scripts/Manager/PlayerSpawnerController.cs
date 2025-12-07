using UnityEngine;
using FishNet.Managing;
using FishNet.Managing.Server;
using FishNet.Connection;
using FishNet.Component.Spawning;
using FishNet.Object;
using FishNet;

public class PlayerSpawnerController : MonoBehaviour
{
    [field: SerializeField] public GameObject PlayerPrefab { get; set; }
    [field: SerializeField] public Transform[] SpawnPoints { get; set; }
    // public PlayerSpawner FishnetPlayerSpawner { get; set; }

    public Vector2 ViewportMin { get; set; } = new(0.1f, 0.1f);
    public Vector2 ViewportMax { get; set; } = new(0.9f, 0.9f);

    private NetworkManager _netManager;

    private void Awake()
    {
        _netManager = InstanceFinder.NetworkManager;

        if (_netManager == null)
            return;

        // if (FishnetPlayerSpawner == null)
        // {
        //     FishnetPlayerSpawner = GetComponent<PlayerSpawner>();
        // }

        // if (FishnetPlayerSpawner != null)
        // {
        //     if (PlayerPrefab != null)
        //     {
        //         if (PlayerPrefab.TryGetComponent<NetworkObject>(out var nob))
        //             FishnetPlayerSpawner.SetPlayerPrefab(nob);
        //     }
        // }
        // else
        // {
        //     _netManager.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
        // }

        _netManager.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
        // _netManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;

    }

    private void OnDestroy()
    {
        if (_netManager != null)
        {
            try { _netManager.ServerManager.OnRemoteConnectionState -= ServerManager_OnRemoteConnectionState; } catch { }
            // try { _netManager.ClientManager.OnClientConnectionState -= ClientManager_OnClientConnectionState; } catch { }
        }
    }

    private void ServerManager_OnRemoteConnectionState(NetworkConnection conn, FishNet.Transporting.RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Started)
        {
            if (_netManager.IsServerStarted)
            {
                SpawnPlayer(conn);
            }
        }
    }

    // private void ClientManager_OnClientConnectionState(FishNet.Transporting.ClientConnectionStateArgs args)
    // {
    //     if (args.ConnectionState == FishNet.Transporting.LocalConnectionState.Started)
    //     {
    //         if (_netManager.IsServerStarted)
    //         {
    //             SpawnPlayer(_netManager.ClientManager.Connection);
    //         }
    //     }
    // }

    private void SpawnPlayer(NetworkConnection conn)
    {
        if (PlayerPrefab == null)
            return;

        Vector3 spawnPos = Vector3.zero;

        if (SpawnPoints != null && SpawnPoints.Length > 0)
        {
            int spawnCount = SpawnPoints.Length;
            int clientId = conn.ClientId;
            int spawnIndex;

            if (spawnCount >= 2)
            {
                int half = spawnCount / 2;

                if (spawnCount % 2 == 0)
                {
                    int primary = clientId / 2;
                    int primaryMod = (half > 0) ? (primary % half) : 0;
                    spawnIndex = (clientId % 2 == 0) ? primaryMod : (primaryMod + half);
                }
                else
                {
                    spawnIndex = clientId % spawnCount;
                }
            }
            else
            {
                spawnIndex = 0;
            }

            spawnIndex = Mathf.Clamp(spawnIndex, 0, spawnCount - 1);

            Transform t = SpawnPoints[spawnIndex];
            if (t != null)
            {
                spawnPos = t.position;
            }
            else
            {
                for (int i = 0; i < spawnCount; i++)
                {
                    if (SpawnPoints[i] != null)
                    {
                        spawnPos = SpawnPoints[i].position;
                        break;
                    }
                }
            }
        }

        GameObject go = Instantiate(PlayerPrefab, spawnPos, Quaternion.identity);

        if (!go.TryGetComponent<NetworkObject>(out var nob))
        {
            Destroy(go);
            return;
        }

        _netManager.ServerManager.Spawn(go, conn);
    }
}
