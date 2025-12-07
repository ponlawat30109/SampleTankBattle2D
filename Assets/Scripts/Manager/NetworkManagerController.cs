using FishNet;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Connection;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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
    public int DiscoveryPort = 47777;
    public int DiscoveryTimeout = 1000;
    public ushort DefaultGamePort = 7777;
    public bool DebugLogs = false;

    [Header("Server Limits")]
    public int MaxClients = 2;

    private const string REQ = "TANKBATTLE_DISCOVERY_REQUEST_v1";
    private const string RESP_PREFIX = "TANKBATTLE_HOST_v1|";

    private CancellationTokenSource _responderCts;
    private NetworkManager _netManager;
    private float _lastClientConnectAttemptTime = -10f;
    private const float ConnectFailureUiWindow = 5f;

    private void Awake()
    {
        _netManager = InstanceFinder.NetworkManager;
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
        try
        {
            var host = await Task.Run(() => DiscoverHost(DiscoveryPort, DiscoveryTimeout));
            if (host != null)
            {
                string ip = host.Item1;
                ushort port = host.Item2;
                if (DebugLogs)
                    Debug.Log($"Discovered host {ip}:{port} - joining...");
                StartLocalClientAndStamp(ip, port);
                return;
            }

            ushort portToUse = GetConfiguredPort() ?? DefaultGamePort;
            if (DebugLogs)
                Debug.Log($"No host discovered - attempting to start host on port {portToUse}");

            await StartServerAndResponder(portToUse);
        }
        catch (Exception ex)
        {
            Debug.LogError($"AutoStart error: {ex}");
        }
    }

    private ushort? GetConfiguredPort()
    {
        ushort configured = _netManager.TransportManager.Transport.GetPort();
        if (configured != 0)
            return configured;
        return null;
    }

    private async Task StartServerAndResponder(ushort portToUse)
    {
        try
        {
            _netManager.ServerManager.StartConnection(portToUse);
        }
        catch (Exception ex)
        {
            if (DebugLogs)
                Debug.LogWarning($"Start server threw exception: {ex.Message}");
        }
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

            StartLocalClientAndStamp("127.0.0.1", portToUse);
            _responderCts = new CancellationTokenSource();
            _ = Task.Run(() => DiscoveryResponderAsync(portToUse, _responderCts.Token));
        }
        else
        {
            if (DebugLogs)
                Debug.LogWarning($"Server failed to start on port {portToUse}. Trying to join existing host at 127.0.0.1:{portToUse}");
            StartLocalClientAndStamp("127.0.0.1", portToUse);
        }
    }

    private void StartLocalClientAndStamp(string ip, ushort port)
    {
        _netManager.ClientManager.StartConnection(ip, port);
        _lastClientConnectAttemptTime = Time.realtimeSinceStartup;
    }

    private Tuple<string, ushort> DiscoverHost(int discoveryPort, int timeoutMs)
    {
        try
        {
            using var client = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            client.EnableBroadcast = true;
            client.Client.ReceiveTimeout = timeoutMs;

            var req = Encoding.UTF8.GetBytes(REQ);
            var broadcastEP = new IPEndPoint(IPAddress.Broadcast, discoveryPort);
            client.Send(req, req.Length, broadcastEP);

            var remote = new IPEndPoint(IPAddress.Any, 0);
            try
            {
                var data = client.Receive(ref remote);
                var s = Encoding.UTF8.GetString(data);
                if (s.StartsWith(RESP_PREFIX))
                {
                    var portStr = s.Substring(RESP_PREFIX.Length);
                    if (ushort.TryParse(portStr, out ushort hostPort))
                    {
                        return Tuple.Create(remote.Address.ToString(), hostPort);
                    }
                }
            }
            catch (Exception) { }
        }
        catch (Exception) { }

        return null;
    }

    private async Task DiscoveryResponderAsync(ushort serverPort, CancellationToken token)
    {
        UdpClient listener = null;
        try
        {
            try
            {
                listener = new UdpClient(DiscoveryPort);
                listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            }
            catch (Exception)
            {
                if (DebugLogs)
                    Debug.LogWarning($"Discovery responder failed to bind UDP {DiscoveryPort}");
                return;
            }

            if (DebugLogs)
                Debug.Log($"Discovery responder listening on UDP {DiscoveryPort}");

            while (!token.IsCancellationRequested)
            {
                UdpReceiveResult result;
                try
                {
                    result = await listener.ReceiveAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { break; }

                string msg = Encoding.UTF8.GetString(result.Buffer);
                if (msg == REQ)
                {
                    var resp = RESP_PREFIX + serverPort.ToString();
                    var bytes = Encoding.UTF8.GetBytes(resp);
                    try
                    {
                        await listener.SendAsync(bytes, bytes.Length, result.RemoteEndPoint).ConfigureAwait(false);
                        if (DebugLogs)
                            Debug.Log($"Replied discovery to {result.RemoteEndPoint}");
                    }
                    catch (Exception ex)
                    {
                        if (DebugLogs)
                            Debug.LogWarning($"Discovery responder send failed: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"DiscoveryResponder error: {ex}");
        }
        finally
        {
            listener?.Close(); listener?.Dispose();
            if (DebugLogs)
                Debug.Log("Discovery responder stopped");
        }
    }

    private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs args)
    {
        if (_netManager == null)
            return;

        if (args.ConnectionState == LocalConnectionState.Started)
        {
            Debug.Log("Connection state: Client Started");
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            Debug.Log("Connection state: Client Stopped");

            float delta = Time.realtimeSinceStartup - _lastClientConnectAttemptTime;
            if (delta <= ConnectFailureUiWindow)
            {
                if (DebugLogs)
                    Debug.Log($"Recent connect attempt failed ({delta:F2}s) - showing session full UI.");

                // var ui = FindAnyObjectByType<ClientRoundUI>();
                // if (ui != null)
                //     ui.ShowDeathAndStartCountdown("Session full — please try again later", 5f);
                // else if (ClientRoundUI.Instance != null)
                ClientRoundUI.Instance.ShowDeathAndStartCountdown("Session full — please try again later", 999f);
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
        if (_responderCts != null && !_responderCts.IsCancellationRequested)
        {
            _responderCts.Cancel();
            _responderCts.Dispose();
            _responderCts = null;
        }

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

    #region Manual Controls
    public void StartHost(ushort port)
    {
        if (_netManager == null) return;
        _netManager.ServerManager.StartConnection(port);
        _netManager.ClientManager.StartConnection("127.0.0.1", port);
        _lastClientConnectAttemptTime = Time.realtimeSinceStartup;
        _responderCts = new CancellationTokenSource();
        _ = Task.Run(() => DiscoveryResponderAsync(port, _responderCts.Token));
    }

    public void StartClient(string ip, ushort port)
    {
        if (_netManager == null) return;
        _netManager.ClientManager.StartConnection(ip, port);
    }

    public void StopAll()
    {
        if (_netManager == null) return;
        _netManager.ClientManager.StopConnection();
        _netManager.ServerManager.StopConnection(true);

        if (_responderCts != null && !_responderCts.IsCancellationRequested)
        {
            _responderCts.Cancel();
            _responderCts.Dispose();
            _responderCts = null;
        }
    }
    #endregion 
}
