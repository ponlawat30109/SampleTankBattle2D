using FishNet;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Connection;
using FishNet.Transporting;
using System.Threading.Tasks;
using UnityEngine;
using FishNet.Managing.Observing;

[RequireComponent(typeof(NetworkManager))]
[RequireComponent(typeof(ObserverManager))]
public class NetworkManagerController : MonoBehaviour
{
    [Header("Server Settings")]
    public int MaxClients = 2;
    public bool DebugLogs = false;

    private NetworkManager _netManager;

    private void Awake()
    {
        _netManager = InstanceFinder.NetworkManager;

        _netManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
        _netManager.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
        _netManager.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState_Limit;
    }

    public async void JoinRelayGame(string joinCode)
    {
        if (_netManager == null)
            return;

        if (_netManager.IsServerStarted || _netManager.IsClientStarted)
        {
            _netManager.ServerManager.StopConnection(true);
            _netManager.ClientManager.StopConnection();
            await Task.Delay(500);
        }

        if (ClientRoundUI.Instance != null)
            ClientRoundUI.Instance.ShowPersistent("Joining Relay...");

        bool success = await RelayManager.Instance.JoinRelay(joinCode);
        if (success)
        {
            _netManager.ClientManager.StartConnection();
        }
        else
        {
            if (ClientRoundUI.Instance != null)
                ClientRoundUI.Instance.ShowPersistent("Join Failed.");
        }
    }

    public async void HostRelayGame()
    {
        if (_netManager == null)
            return;

        if (_netManager.IsServerStarted || _netManager.IsClientStarted)
        {
            _netManager.ServerManager.StopConnection(true);
            _netManager.ClientManager.StopConnection();
            await Task.Delay(500);
        }

        if (ClientRoundUI.Instance != null)
            ClientRoundUI.Instance.ShowPersistent("Creating Host...");

        while (RelayManager.Instance == null) await Task.Delay(100);

        string joinCode = await RelayManager.Instance.CreateRelay(MaxClients);
        if (!string.IsNullOrEmpty(joinCode))
        {
            if (ClientRoundUI.Instance != null)
                ClientRoundUI.Instance.ShowServerIP(joinCode);

            _netManager.ServerManager.StartConnection();
            _netManager.ClientManager.StartConnection();
        }
        else
        {
            if (ClientRoundUI.Instance != null)
                ClientRoundUI.Instance.ShowPersistent("Failed to create host.");
            Debug.LogError("Failed to create Relay host.");
        }
    }

    private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs args)
    {
        if (_netManager == null)
            return;

        if (args.ConnectionState == LocalConnectionState.Started)
        {
            Debug.Log("Connection state: Client Started");
            if (ClientRoundUI.Instance != null)
                ClientRoundUI.Instance.HidePersistent();
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            Debug.Log("Connection state: Client Stopped");
        }
    }

    private void ServerManager_OnServerConnectionState(ServerConnectionStateArgs args)
    {
        if (_netManager == null)
            return;

        if (args.ConnectionState == LocalConnectionState.Started)
        {
            if (_netManager.IsHostStarted)
                Debug.Log("Connection state: Started as Host");
            else if (_netManager.IsServerStarted)
                Debug.Log("Connection state: Started as Server");
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            Debug.Log("Connection state: Server Stopped");
        }
    }

    private void OnDestroy()
    {
        if (_netManager != null)
        {
            _netManager.ClientManager.OnClientConnectionState -= ClientManager_OnClientConnectionState;
            _netManager.ServerManager.OnServerConnectionState -= ServerManager_OnServerConnectionState;
            _netManager.ServerManager.OnRemoteConnectionState -= ServerManager_OnRemoteConnectionState_Limit;
        }
    }

    private void ServerManager_OnRemoteConnectionState_Limit(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (_netManager == null)
            return;

        if (args.ConnectionState != RemoteConnectionState.Started)
            return;

        int connected = _netManager.ServerManager.Clients?.Count ?? 0;

        if (MaxClients > 0 && connected > MaxClients)
        {
            var relay = FindAnyObjectByType<ServerMessageRelay>();
            if (relay != null)
            {
                relay.NotifyAndKick(conn, "Session full — please try again later", 5f, 0.8f);
            }
            else
            {
                _netManager.ServerManager.Kick(conn, KickReason.UnusualActivity, FishNet.Managing.Logging.LoggingType.Common, "Server full");
            }

            if (DebugLogs)
                Debug.Log($"Server is full ({connected}/{MaxClients})");
        }
    }
}
