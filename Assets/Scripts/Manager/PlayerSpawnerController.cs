using UnityEngine;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using System.Collections.Generic;

public class PlayerSpawnerController : MonoBehaviour
{
    [SerializeField] private GameObject PlayerPrefab;
    [SerializeField] private Transform[] SpawnPoints;

    private NetworkManager _netmanager;
    private Dictionary<int, int> _assigned = new();

    private void Awake()
    {
        _netmanager = InstanceFinder.NetworkManager;
        if (_netmanager != null)
        {
            _netmanager.ServerManager.OnRemoteConnectionState += OnRemoteState;
        }
    }

    private void OnDestroy()
    {
        if (_netmanager != null)
        {
            _netmanager.ServerManager.OnRemoteConnectionState -= OnRemoteState;
        }
    }

    private void OnRemoteState(NetworkConnection conn, FishNet.Transporting.RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Started)
        {
            if (_netmanager.IsServerStarted)
            {
                Spawn(conn);
            }
        }
        else if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Stopped)
        {
            _assigned.Remove(conn.ClientId);
        }
    }

    private void Spawn(NetworkConnection conn)
    {
        if (PlayerPrefab == null)
            return;

        Vector3 pos = Vector3.zero;
        int count = SpawnPoints?.Length ?? 0;
        int client = conn.ClientId;

        int index = 0;
        if (count > 0)
        {
            if (_assigned.TryGetValue(client, out var e))
            {
                index = Mathf.Clamp(e, 0, count - 1);
            }
            else if (_assigned.Count == 0)
            {
                index = Assign(client, Random.Range(0, count));
            }
            else if (_assigned.Count == 1 && count % 2 == 0)
            {
                int other = 0;
                foreach (var v in _assigned.Values)
                {
                    other = v;
                    break;
                }

                int opp = (other + count / 2) % count;
                index = _assigned.ContainsValue(opp) ? Assign(client, NearestFree(client % count, count)) : Assign(client, opp);
            }
            else
            {
                index = Assign(client, NearestFree(client % count, count));
            }

            pos = SpawnPoints[index]?.position ?? SpawnPoints[0].position;
        }

        var go = Instantiate(PlayerPrefab, pos, Quaternion.identity);
        if (!go.TryGetComponent<NetworkObject>(out var nob))
        {
            Destroy(go);
            return;
        }

        _netmanager.ServerManager.Spawn(go, conn);
    }

    private int Assign(int client, int idx)
    {
        _assigned[client] = idx;
        return idx;
    }

    private int NearestFree(int start, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int x = (start + i) % count;
            if (!_assigned.ContainsValue(x))
                return x;
        }

        return Mathf.Clamp(start, 0, count - 1);
    }
}
