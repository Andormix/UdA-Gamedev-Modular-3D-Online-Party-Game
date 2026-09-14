using System;
using Unity.Collections;
using Unity.Netcode;

public struct PlayerMatchResultData : INetworkSerializable, IEquatable<PlayerMatchResultData>
{
    public ulong clientId;
    public FixedString64Bytes playerName;
    public int score;
    public byte team; // MatchTeam Index

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref playerName);
        serializer.SerializeValue(ref score);
        serializer.SerializeValue(ref team);
    }

    public bool Equals(PlayerMatchResultData other)
    {
        return clientId == other.clientId &&
               playerName.Equals(other.playerName) &&
               score == other.score &&
               team == other.team;
    }
}