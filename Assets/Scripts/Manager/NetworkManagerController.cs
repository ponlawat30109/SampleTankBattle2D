using FishNet;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Connection;
using System;
using System.Threading.Tasks;
using FishNet.Transporting;
using UnityEngine;
using FishNet.Managing.Observing;


[RequireComponent(typeof(NetworkManager))]
[RequireComponent(typeof(ObserverManager))]
public class NetworkManagerController : MonoBehaviour
{
    [Header("Auto Host/Join")]
    public bool AutoStartOnLaunch = true;
    public bool AutoStartLocalClient = true;
    public ushort DefaultGamePort = 7777;
    public bool DebugLogs = false;

    [Header("Server Limits")]
    public int MaxClients = 2;

    private NetworkManager _netManager;
    private float _lastClientConnectAttemptTime = -10f;
    private const float ConnectFailureUiWindow = 5f;

    [Header("Client Timeout")]
    public float ConnectTimeoutSeconds = 10f;
    public int ConnectRetryCount = 3;
    public float ConnectRetryDelaySeconds = 2f;

    [Header("Auto Network")]
    public string RemoteHostIP;
    public bool AutoDetectPublicIP = true;
    public bool AutoFallbackToHost = false;

    private void Awake()
    {
        _netManager = InstanceFinder.NetworkManager;

        var transport = _netManager.TransportManager.Transport;
        if (transport != null)
        {
            transport.SetMaximumClients(MaxClients);
            if (transport.GetPort() == 0)
                transport.SetPort(DefaultGamePort);
            transport.SetServerBindAddress(string.Empty, IPAddressType.IPv4);
            if (DebugLogs)
                Debug.Log($"Transport configured: port={transport.GetPort()}, maxClients={transport.GetMaximumClients()}");
        }

        _netManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
        _netManager.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
        _netManager.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState_Limit;
    }

    private void Start()
    {
        if (AutoStartOnLaunch)
            _ = AutoStartAsync();
    }

    private async Task AutoStartAsync()
    {
        if (_netManager == null)
            return;

        ushort portToUse = GetConfiguredPort() ?? DefaultGamePort;

        if (!string.IsNullOrEmpty(RemoteHostIP))
        {
            if (DebugLogs)
                Debug.Log($"AutoStart: RemoteHostIP set to '{RemoteHostIP}'. Starting as Client.");
            StartClient(RemoteHostIP, portToUse);
            return;
        }

        if (NetworkAddressUtils.IsPortInUse(portToUse))
        {
            if (DebugLogs)
                Debug.Log($"AutoStart: Port {portToUse} is already in use. Starting as Client only.");
            if (AutoStartLocalClient)
                StartLocalClientAndStamp(NetworkAddressUtils.DefaultLocalAddress, portToUse);
            return;
        }

        if (DebugLogs)
            Debug.Log($"AutoStart: starting host on port {portToUse}");

        await StartServerAndResponder(portToUse);
    }



    private ushort? GetConfiguredPort()
    {
        var transport = _netManager.TransportManager.Transport;
        if (transport == null) return null;
        ushort configured = transport.GetPort();
        if (configured != 0)
            return configured;
        return null;
    }



    private async Task StartServerAndResponder(ushort portToUse)
    {
        var transport = _netManager.TransportManager.Transport;
        if (transport != null)
        {
            transport.SetPort(portToUse);
            transport.SetMaximumClients(MaxClients);
            transport.SetServerBindAddress(string.Empty, IPAddressType.IPv4);
        }

        _netManager.ServerManager.StartConnection(portToUse);

        int waited = 0;
        const int waitInterval = 100;
        const int maxWait = 1000;
        while (waited < maxWait && !_netManager.IsServerStarted && !_netManager.IsHostStarted)
        {
            await Task.Delay(waitInterval);
            waited += waitInterval;
        }

        if (_netManager.IsServerStarted || _netManager.IsHostStarted)
        {
            if (DebugLogs)
                Debug.Log($"Server started successfully on port {portToUse}; starting local client.");

            var ips = NetworkAddressUtils.GetLocalIPv4Addresses();
            Debug.Log($"Local IPs: {string.Join(", ", ips)} (listening port {portToUse})");

            // Display Public IP for Host
            if (AutoDetectPublicIP)
            {
                if (ClientRoundUI.Instance != null)
                    ClientRoundUI.Instance.ShowServerIP("Fetching...");

                // We are already on the Unity Context here because we are in an async Task started from Start()
                try
                {
                    string pubIp = await NetworkAddressUtils.GetPublicIPAddressAsync();
                    if (!string.IsNullOrEmpty(pubIp) && ClientRoundUI.Instance != null)
                        ClientRoundUI.Instance.ShowServerIP(pubIp);
                }
                catch (Exception ex) 
                {
                    if (DebugLogs) Debug.LogWarning($"Could not fetch/show IP: {ex.Message}");
                    if (ClientRoundUI.Instance != null)
                         ClientRoundUI.Instance.ShowServerIP("Error fetching IP");
                }
            }

            if (AutoStartLocalClient)
                StartLocalClientAndStamp(NetworkAddressUtils.DefaultLocalAddress, portToUse);
        }
        else
        {
            if (DebugLogs)
                Debug.LogWarning($"Server failed to start on port {portToUse}. Trying to start local client anyway.");
            if (AutoStartLocalClient)
                StartLocalClientAndStamp(NetworkAddressUtils.DefaultLocalAddress, portToUse);
        }
    }

    private void StartLocalClientAndStamp(string ip, ushort port)
    {
        StartClient(ip, port);
    }

    private async void StartClient(string ip, ushort port)
    {
        if (_netManager == null)
            return;

        if (AutoDetectPublicIP)
        {
            try
            {
                string pub = await NetworkAddressUtils.GetPublicIPAddressAsync();
                if (!string.IsNullOrEmpty(pub) && string.IsNullOrEmpty(ip))
                    ip = pub;
            }
            catch { }
        }

        var candidates = NetworkAddressUtils.GetAutoConnectCandidates(ip);

        foreach (var candidate in candidates)
        {
            for (int r = 0; r < ConnectRetryCount && !_netManager.IsClientStarted; r++)
            {
                if (ClientRoundUI.Instance != null)
                    ClientRoundUI.Instance.ShowPersistent(r == 0 ? "Connecting..." : $"Reconnecting... (attempt {r + 1})");

                if (DebugLogs)
                    Debug.Log($"Attempting client connection to {candidate}:{port} (attempt {r + 1}/{ConnectRetryCount})");

                var transport = _netManager.TransportManager.Transport;
                if (transport != null)
                {
                    transport.SetClientAddress(candidate);
                    transport.SetPort(port);
                }

                _netManager.ClientManager.StartConnection(candidate, port);
                _lastClientConnectAttemptTime = Time.realtimeSinceStartup;

                bool ok = await WaitForClientStarted(ConnectTimeoutSeconds);
                if (ok)
                {
                    if (ClientRoundUI.Instance != null)
                        ClientRoundUI.Instance.HidePersistent();
                    if (DebugLogs)
                        Debug.Log($"Client connected to {candidate}:{port}");
                    _lastClientConnectAttemptTime = -10f;
                    return;
                }

                _netManager.ClientManager.StopConnection();

                if (r < ConnectRetryCount - 1)
                    await Task.Delay(TimeSpan.FromSeconds(ConnectRetryDelaySeconds));
            }
        }

        if (ClientRoundUI.Instance != null)
        {
            ClientRoundUI.Instance.HidePersistent();
            ClientRoundUI.Instance.ShowDeathAndStartCountdown("Could not connect — please try again later", 5f);
        }
        if (DebugLogs)
            Debug.LogWarning($"Failed to connect to any candidate for port {port}");

        if (AutoFallbackToHost)
        {
            if (DebugLogs) Debug.Log("No host found — falling back to start host on this machine.");
            await StartServerAndResponder(port);
        }
        _lastClientConnectAttemptTime = -10f;
    }



    private async Task<bool> WaitForClientStarted(float timeoutSeconds)
    {
        float waited = 0f;
        const float poll = 0.2f;
        while (waited < timeoutSeconds)
        {
            if (_netManager.IsClientStarted) return true;
            await Task.Delay(TimeSpan.FromSeconds(poll));
            waited += poll;
        }
        return _netManager.IsClientStarted;
    }



    private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs args)
    {
        if (_netManager == null)
            return;

        if (args.ConnectionState == LocalConnectionState.Started)
        {
            Debug.Log("Connection state: Client Started");
            ClientRoundUI.Instance.HidePersistent();
            _lastClientConnectAttemptTime = -10f;
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            Debug.Log("Connection state: Client Stopped");

            float delta = Time.realtimeSinceStartup - _lastClientConnectAttemptTime;
            if (delta <= ConnectFailureUiWindow)
            {
                if (DebugLogs)
                    Debug.Log($"Recent connect attempt failed ({delta:F2}s) - showing session full UI.");

                if (ClientRoundUI.Instance != null)
                    ClientRoundUI.Instance.HidePersistent();
                ClientRoundUI.Instance.ShowDeathAndStartCountdown("Session full — please try again later", 999f);

                _lastClientConnectAttemptTime = -10f;
            }
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
