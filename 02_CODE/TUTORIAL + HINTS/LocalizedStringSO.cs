using UnityEngine;

public enum Language { English, Spanish, Catalan }

[CreateAssetMenu(menuName = "Game/Localization/Localized String")]
public class LocalizedStringSO : ScriptableObject
{
    [TextArea] public string en;
    [TextArea] public string es;
    [TextArea] public string ca;

    // Primer farem eng
    public string Get(Language lang) => lang switch
    {
        Language.Spanish => string.IsNullOrWhiteSpace(es) ? en : es,
        Language.Catalan => string.IsNullOrWhiteSpace(ca) ? en : ca,
        _ => en,
    };
}