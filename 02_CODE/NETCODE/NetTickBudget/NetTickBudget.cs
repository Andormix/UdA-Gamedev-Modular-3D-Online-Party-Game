using System.Collections.Generic;
using UnityEngine;

public static class NetTickBudget
{
    private static readonly Dictionary<string, float> nextAllowedByChannel = new();
    private static readonly Dictionary<ulong, float> nextAllowedByUlong = new();

    // Returns true if this channel can send now.
    public static bool CanSend(string channel, float minIntervalSeconds)
    {
        float now = Time.time;

        if (!nextAllowedByChannel.TryGetValue(channel, out float next))
        {
            nextAllowedByChannel[channel] = now + minIntervalSeconds;
            return true;
        }

        if (now < next) return false;

        nextAllowedByChannel[channel] = now + minIntervalSeconds;
        return true;
    }

    //Zero-allocation overload. Encodes (objectId, channel) into a single ulong key.
    public static bool CanSend(ulong objectId, byte channel, float minIntervalSeconds)
    {
        ulong key = MakeUlongKey(objectId, channel);
        float now = Time.time;

        if (!nextAllowedByUlong.TryGetValue(key, out float next))
        {
            nextAllowedByUlong[key] = now + minIntervalSeconds;
            return true;
        }

        if (now < next) return false;

        nextAllowedByUlong[key] = now + minIntervalSeconds;
        return true;
    }

    public static void ResetChannel(string channel)
    {
        if (nextAllowedByChannel.ContainsKey(channel))
            nextAllowedByChannel.Remove(channel);
    }

    /// <Zero-allocation reset for ulong-keyed channels.
    public static void ResetChannel(ulong objectId, byte channel)
    {
        nextAllowedByUlong.Remove(MakeUlongKey(objectId, channel));
    }

    public static void ResetAll()
    {
        nextAllowedByChannel.Clear();
        nextAllowedByUlong.Clear();
    }

    private static ulong MakeUlongKey(ulong objectId, byte channel)
    {
        // Pack: high 8 bits = channel, low 56 bits = objectId (NGO object IDs are well under 2^56).
        return ((ulong)channel << 56) | (objectId & 0x00FF_FFFF_FFFF_FFFFul);
    }
}