using UnityEngine;

public class MusicManager : MonoBehaviour
{

    private const string PLAYER_PREFERENCES_MUSIC_VOLUME = "MusicVolume";

    // TODO 73: Descacoblar SINGELTON
    public static MusicManager Instance { get; private set; }

    private AudioSource audioSource;
    private float volume = .5f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        audioSource = GetComponent<AudioSource>();
        volume = PlayerPrefs.GetFloat(PLAYER_PREFERENCES_MUSIC_VOLUME, .2f);
        audioSource.volume = volume;
    }

    public void ChangeVolume()
    {
        volume += .1f;
        if(volume > 1f)
        {
            volume = 0;
        }
        audioSource.volume = volume;

        PlayerPrefs.SetFloat(PLAYER_PREFERENCES_MUSIC_VOLUME, volume);
        PlayerPrefs.Save();
    }

    public float GetVolume()
    {
        return volume;
    }
}
