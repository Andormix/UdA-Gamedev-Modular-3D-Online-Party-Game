using System;
using Unity.Collections;
using Unity.Netcode;

[Serializable]
public struct OrderUiData : INetworkSerializable
{
    public int orderId;
    public int recipeIndex;
    public float duration;
    public double endTime;
    public bool assignedToWorkstation;
    public ulong tableNetId;
    public FixedString64Bytes tableDisplayName;
    public int ownerTeamId; // -1 none, 0 blue, 1 red

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref orderId);
        serializer.SerializeValue(ref recipeIndex);
        serializer.SerializeValue(ref duration);
        serializer.SerializeValue(ref endTime);
        serializer.SerializeValue(ref assignedToWorkstation);
        serializer.SerializeValue(ref tableNetId);
        serializer.SerializeValue(ref tableDisplayName);
        serializer.SerializeValue(ref ownerTeamId);
    }
}