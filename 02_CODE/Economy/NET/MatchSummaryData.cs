using Unity.Collections;
using Unity.Netcode;

public struct MatchSummaryData : INetworkSerializable
{
    public int matchId; // unique id per published match result
    public float matchDurationSeconds;
    public int topScore;
    public FixedString64Bytes topPlayerName;

    public int localScore;
    public int localStars;
    public int localCoinsEarned;
    public int localDiamondsEarned;
    public FixedString128Bytes localDiamondReason;

    public byte mode;
    public int blueTeamScore;
    public int redTeamScore;
    public byte localTeam;

    public byte localOutcome;      // MatchOutcome
    public byte applyRewardLocally; // 0/1

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref matchId);
        serializer.SerializeValue(ref matchDurationSeconds);
        serializer.SerializeValue(ref topScore);
        serializer.SerializeValue(ref topPlayerName);
        serializer.SerializeValue(ref localScore);
        serializer.SerializeValue(ref localStars);
        serializer.SerializeValue(ref localCoinsEarned);
        serializer.SerializeValue(ref localDiamondsEarned);
        serializer.SerializeValue(ref localDiamondReason);
        serializer.SerializeValue(ref mode);

        serializer.SerializeValue(ref blueTeamScore);
        serializer.SerializeValue(ref redTeamScore);
        serializer.SerializeValue(ref localTeam);

        serializer.SerializeValue(ref localOutcome); 
        serializer.SerializeValue(ref applyRewardLocally);
    }
}