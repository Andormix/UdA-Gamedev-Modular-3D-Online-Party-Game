using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class TableNpcNetState : NetworkBehaviour
{
    private readonly NetworkVariable<byte> state =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    // 0=Idle, 1=WalkingToSeat, 2=Seated, 3=WalkingAway

    private readonly NetworkVariable<Vector3> snapPosition =
        new(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<Quaternion> snapRotation =
        new(Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public byte State => state.Value;
    public Vector3 SnapPosition => snapPosition.Value;
    public Quaternion SnapRotation => snapRotation.Value;

    public void ServerSetState(byte s)
    {
        if (!IsServer) return;
        state.Value = s;
    }

    public void ServerSnap(Transform t)
    {
        if (!IsServer || t == null) return;
        snapPosition.Value = t.position;
        snapRotation.Value = t.rotation;
    }
}