using System.Threading.Tasks;
using FishNet;
using FishNet.Transporting.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Networking.Transport.Relay;
using UnityEngine;

public class RelayManager : MonoBehaviour
{
    public static RelayManager Instance { get; private set; }

    private Task _initTask;

    private void Awake()
    {
        Instance = this;
        _initTask = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        if (UnityServices.State == ServicesInitializationState.Initialized)
            return;

        try
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"RelayManager: Signed in {AuthenticationService.Instance.PlayerId}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RelayManager: Initialization failed: {e.Message}");
        }
    }

    public async Task<string> CreateRelay(int maxConnections)
    {
        await _initTask;

        try
        {
            var alloc = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            var joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

            string host = null; ushort port = 0; bool secure = false;
            foreach (var ep in alloc.ServerEndpoints)
                if (ep.ConnectionType == "dtls")
                {
                    host = ep.Host;
                    port = (ushort)ep.Port;
                    secure = ep.Secure;
                    break;
                }

            if (!string.IsNullOrEmpty(host))
            {
                var transport = InstanceFinder.NetworkManager.TransportManager.Transport as UnityTransport;
                if (transport != null)
                {
                    var data = new RelayServerData(host, port,
                        alloc.AllocationIdBytes, alloc.ConnectionData, alloc.ConnectionData, alloc.Key, secure);
                    transport.SetRelayServerData(data);
                    Debug.Log("RelayManager: Relay set on transport (host)");
                }
                else
                {
                    Debug.LogError("RelayManager: Transport is NOT UnityTransport!");
                }
            }

            return joinCode;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RelayManager: CreateRelay failed: {e.Message}");
            return null;
        }
    }

    public async Task<bool> JoinRelay(string joinCode)
    {
        await _initTask;

        try
        {
            var joinAlloc = await RelayService.Instance.JoinAllocationAsync(joinCode);

            string host = null; ushort port = 0; bool secure = false;
            foreach (var ep in joinAlloc.ServerEndpoints)
                if (ep.ConnectionType == "dtls") { host = ep.Host; port = (ushort)ep.Port; secure = ep.Secure; break; }

            if (string.IsNullOrEmpty(host))
            {
                Debug.LogError("RelayManager: No DTLS endpoint found.");
                return false;
            }

            var transport = InstanceFinder.NetworkManager.TransportManager.Transport as UnityTransport;
            if (transport == null)
            {
                Debug.LogError("RelayManager: Transport is NOT UnityTransport!");
                return false;
            }

            var data = new RelayServerData(host, port, joinAlloc.AllocationIdBytes, joinAlloc.ConnectionData, joinAlloc.HostConnectionData, joinAlloc.Key, secure);
            transport.SetRelayServerData(data);
            Debug.Log("RelayManager: Relay set on transport (client)");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RelayManager: JoinRelay failed: {e.Message}");
            return false;
        }
    }
}
