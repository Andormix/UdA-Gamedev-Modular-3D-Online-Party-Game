using System;
using Unity.Collections;
using Unity.Netcode;

public struct PlayerData : IEquatable<PlayerData>, INetworkSerializable
{
    public ulong clientId;
    public FixedString64Bytes playerName;
    public FixedString64Bytes playerId;
    public CustomizationData customization; // la millora a 10 bytes (documentada a la memo)

    public bool Equals(PlayerData other)
    {
        return clientId == other.clientId &&
               playerName == other.playerName &&
               playerId == other.playerId; 
               // No cal comparar customization per al NetworkList (clientId és suficient per identitat)
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref playerName);
        serializer.SerializeValue(ref playerId);
        serializer.SerializeValue(ref customization); 
    }
}