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

    private void Awake()
    {
        _netManager = InstanceFinder.NetworkManager;
        if (_netManager == null)
            Debug.LogError("FishNet NetworkManager not found.");
        else
        {
            _netManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
            _netManager.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
            _netManager.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState_Limit;
        }
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
                _netManager.ClientManager.StartConnection(ip, port);
            }
            else
            {
                ushort portToUse = DefaultGamePort;
                try
                {
                    ushort configured = _netManager.TransportManager.Transport.GetPort();
                    if (configured != 0)
                        portToUse = configured;
                }
                catch { }

                if (DebugLogs)
                    Debug.Log($"No host discovered - attempting to start host on port {portToUse}");

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
                int waitInterval = 100;
                int maxWait = 1000;
                while (waited < maxWait && !_netManager.IsServerStarted && !_netManager.IsHostStarted)
                {
                    await Task.Delay(waitInterval);
                    waited += waitInterval;
                }

                if (_netManager.IsServerStarted || _netManager.IsHostStarted)
                {
                    if (DebugLogs)
                        Debug.Log($"Server started successfully on port {portToUse}; starting local client.");

                    _netManager.ClientManager.StartConnection("127.0.0.1", portToUse);

                    _responderCts = new CancellationTokenSource();
                    _ = Task.Run(() => DiscoveryResponderAsync(portToUse, _responderCts.Token));
                }
                else
                {
                    if (DebugLogs)
                        Debug.LogWarning($"Server failed to start on port {portToUse}. Trying to join existing host at 127.0.0.1:{portToUse}");
                    _netManager.ClientManager.StartConnection("127.0.0.1", portToUse);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"AutoStart error: {ex}");
        }
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
            catch (SocketException) { }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"DiscoverHost exception: {ex.Message}");
        }

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
            catch (SocketException sx)
            {
                if (DebugLogs)
                    Debug.LogWarning($"Discovery responder failed to bind UDP {DiscoveryPort}: {sx.Message}");
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
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"DiscoveryResponder error: {ex}");
        }
        finally
        {
            try { listener?.Close(); listener?.Dispose(); } catch { }
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
            try { _netManager.ClientManager.OnClientConnectionState -= ClientManager_OnClientConnectionState; } catch { }
            try { _netManager.ServerManager.OnServerConnectionState -= ServerManager_OnServerConnectionState; } catch { }
            try { _netManager.ServerManager.OnRemoteConnectionState -= ServerManager_OnRemoteConnectionState_Limit; } catch { }
        }
    }

    private void ServerManager_OnRemoteConnectionState_Limit(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (_netManager == null) return;

        if (args.ConnectionState != RemoteConnectionState.Started)
            return;

        int connected = _netManager.ServerManager.Clients?.Count ?? 0;

        if (MaxClients > 0 && connected > MaxClients)
        {
            try
            {
                _netManager.ServerManager.Kick(conn, KickReason.UnusualActivity, FishNet.Managing.Logging.LoggingType.Common, "Server full");
                if (DebugLogs)
                    Debug.Log($"Server is full ({connected}/{MaxClients})");
            }
            catch (Exception) { }
        }
    }

    #region Manual Controls
    public void StartHost(ushort port)
    {
        if (_netManager == null) return;
        _netManager.ServerManager.StartConnection(port);
        _netManager.ClientManager.StartConnection("127.0.0.1", port);
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
        try { _netManager.ClientManager.StopConnection(); } catch { }
        try { _netManager.ServerManager.StopConnection(true); } catch { }

        if (_responderCts != null && !_responderCts.IsCancellationRequested)
        {
            _responderCts.Cancel();
            _responderCts.Dispose();
            _responderCts = null;
        }
    }
    #endregion 
}
