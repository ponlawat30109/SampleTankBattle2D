using FishNet.Object;
using UnityEngine;

[RequireComponent(typeof(TankBody))]
public class TankBodyRotationSync : NetworkBehaviour
{
    private TankBody _tankBody;

    private void Awake()
    {
        _tankBody = GetComponent<TankBody>();
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        if (_tankBody == null)
            _tankBody = GetComponent<TankBody>();
    }

    public void RequestRotation(float angle)
    {
        if (!IsOwner)
            return;

        Server_SendRotation(angle);
    }

    [ServerRpc]
    private void Server_SendRotation(float angle)
    {
        Observers_SetRotation(angle);
    }

    [ObserversRpc]
    private void Observers_SetRotation(float angle)
    {
        if (_tankBody == null || _tankBody.BodyTransform == null)
            return;

        _tankBody.BodyTransform.transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}
