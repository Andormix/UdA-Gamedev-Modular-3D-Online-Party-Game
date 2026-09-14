using UnityEngine;

public enum NarratorLineKind
{
    Instruction, // persistent until step completes
    Hint         // auto-hide after minShowSeconds
}

[CreateAssetMenu(menuName = "Game/Tutorial/Narrator Line")]
public class NarratorLineSO : ScriptableObject
{
    public string id;
    public LocalizedStringSO text;

    public NarratorLineKind kind = NarratorLineKind.Instruction;

    [Range(0, 100)] public int priority = 10;
    public bool canInterrupt = false;

    public bool oneShot = true;
    public float minShowSeconds = 2.5f;  // used for Hint; ignored for Instruction
    public float cooldownSeconds = 6f;

    public AudioClip voiceClip;
}