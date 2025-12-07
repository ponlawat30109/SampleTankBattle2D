using FishNet;
using FishNet.Connection;
using FishNet.Managing.Server;
using UnityEngine;
using System.Collections;

public class ServerMessageRelay : MonoBehaviour
{
    public void NotifyAndKick(NetworkConnection conn, string message, float seconds, float delayBeforeKick = 0.8f)
    {
        Debug.Log("NotifyAndKick called");
        if (conn != null && conn.IsLocalClient)
        {
            ClientRoundUI.Instance.ShowDeathAndStartCountdown(message, seconds);
            StartCoroutine(DelayKick(conn, delayBeforeKick));
            return;
        }

        TryKick(conn);
    }

    private IEnumerator DelayKick(NetworkConnection conn, float delay)
    {
        yield return new WaitForSeconds(delay);
        TryKick(conn);
    }

    private void TryKick(NetworkConnection conn)
    {
        InstanceFinder.NetworkManager.ServerManager.Kick(conn, KickReason.UnusualActivity, FishNet.Managing.Logging.LoggingType.Common, "Server full");
    }
}
