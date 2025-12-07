using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Connection;
using FishNet;
using UnityEngine;
using TankBattle.Tank;

public class GameRoundController : MonoBehaviour
{
    public static GameRoundController Instance { get; private set; }

    [SerializeField] private float restartDelay = 5f;

    private readonly List<TankCore> _registeredTanks = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        RegisterExistingTanks();
    }

    private void RegisterExistingTanks()
    {
        var existing = FindObjectsByType<TankCore>(FindObjectsSortMode.None);
        foreach (var t in existing)
        {
            if (t != null)
                RegisterTank(t);
        }
    }

    public void RegisterTank(TankCore tank)
    {
        if (tank == null)
            return;
        if (!_registeredTanks.Contains(tank))
            _registeredTanks.Add(tank);
    }

    public void UnregisterTank(TankCore tank)
    {
        if (tank == null)
            return;
        _registeredTanks.Remove(tank);
    }

    public void Server_PlayerDied(NetworkConnection attacker, NetworkObject core)
    {
        const string msg = "Round End!";

        if (InstanceFinder.IsServerStarted && attacker != null)
        {
            var winner = FindWinner(attacker);
            NotifyPlayersOfRoundEnd(winner);
            StartCoroutine(ServerRestartAfter(restartDelay));
            return;
        }

        Observers_ShowDeathAndCountdown(msg, restartDelay);
        if (InstanceFinder.IsServerStarted)
            StartCoroutine(ServerRestartAfter(restartDelay));
    }

    private TankCore FindWinner(NetworkConnection attacker)
    {
        TankCore winner = null;
        for (int i = _registeredTanks.Count - 1; i >= 0; i--)
        {
            var t = _registeredTanks[i];
            if (t == null)
            {
                _registeredTanks.RemoveAt(i);
                continue;
            }

            if (t.Owner == attacker)
            {
                winner = t;
                break;
            }
        }

        return winner;
    }

    private void NotifyPlayersOfRoundEnd(TankCore winner)
    {
        for (int i = _registeredTanks.Count - 1; i >= 0; i--)
        {
            var t = _registeredTanks[i];
            if (t == null)
            {
                _registeredTanks.RemoveAt(i);
                continue;
            }

            if (t == winner)
                t.Target_ShowRoundEnd(t.Owner, "You Win!", restartDelay);
            else
                t.Target_ShowRoundEnd(t.Owner, "You Died!", restartDelay);

            t.SetStagger(true);
        }
    }

    private IEnumerator ServerRestartAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (!InstanceFinder.IsServerStarted)
            yield break;

        for (int i = _registeredTanks.Count - 1; i >= 0; i--)
        {
            var t = _registeredTanks[i];

            if (t == null)
            {
                _registeredTanks.RemoveAt(i);
                continue;
            }

            t.Server_RespawnAt(t.SpawnPosition);
        }
    }

    private void Observers_ShowDeathAndCountdown(string message, float seconds)
    {
        if (ClientRoundUI.Instance != null)
            ClientRoundUI.Instance.ShowDeathAndStartCountdown(message, seconds);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
