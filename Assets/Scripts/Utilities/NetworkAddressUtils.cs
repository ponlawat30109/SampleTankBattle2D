using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public static class NetworkAddressUtils
{
    public const string DefaultLocalAddress = "127.0.0.1";

    public static bool IsPortInUse(ushort port)
    {
        var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
        var udpListeners = ipGlobalProperties.GetActiveUdpListeners();
        return udpListeners.Any(e => e.Port == port);
    }

    public static string[] GetLocalIPv4Addresses()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var list = new List<string>();
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    list.Add(ip.ToString());
            }
            if (list.Count == 0)
                list.Add(DefaultLocalAddress);
            return list.ToArray();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"GetLocalIPv4Addresses failed: {ex.Message}");
            return new string[] { DefaultLocalAddress };
        }
    }

    public static async Task<string> GetPublicIPAddressAsync(int timeoutSeconds = 5)
    {
        try
        {
            using (var uwr = UnityWebRequest.Get("https://api.ipify.org"))
            {
                uwr.timeout = timeoutSeconds;
                await uwr.SendWebRequest();
                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    return uwr.downloadHandler.text.Trim();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Public IP detection failed: {ex.Message}");
        }
        return null;
    }

    public static string[] GetAutoConnectCandidates(string providedIp)
    {
        var list = new List<string>();
        if (!string.IsNullOrEmpty(providedIp)) list.Add(providedIp);
        list.Add(DefaultLocalAddress);
        try
        {
            var locals = GetLocalIPv4Addresses();
            foreach (var l in locals) list.Add(l);
            if (locals.Length > 0)
            {
                var parts = locals[0].Split('.');
                if (parts.Length == 4)
                {
                    parts[3] = "1"; list.Add(string.Join('.', parts));
                    parts[3] = "254"; list.Add(string.Join('.', parts));
                }
            }
        }
        catch { }

        return list.Distinct().ToArray();
    }
}
