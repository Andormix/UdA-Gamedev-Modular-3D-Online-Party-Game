using Unity.Netcode;


//Compact customization snapshot: 10 bytes total (1 byte per index × 5 parts × 2 indices each).
public struct CustomizationData : INetworkSerializable
{
    // meshIndex per part (màx 255 variants per part, més que suficient)
    public byte hairMesh;
    public byte hairAccMesh;
    public byte beardMesh;
    public byte topMesh;
    public byte legsMesh;

    // colorIndex per part
    public byte hairColor;
    public byte hairAccColor;
    public byte beardColor;
    public byte topColor;
    public byte legsColor;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref hairMesh);
        serializer.SerializeValue(ref hairAccMesh);
        serializer.SerializeValue(ref beardMesh);
        serializer.SerializeValue(ref topMesh);
        serializer.SerializeValue(ref legsMesh);
        serializer.SerializeValue(ref hairColor);
        serializer.SerializeValue(ref hairAccColor);
        serializer.SerializeValue(ref beardColor);
        serializer.SerializeValue(ref topColor);
        serializer.SerializeValue(ref legsColor);
    }
}