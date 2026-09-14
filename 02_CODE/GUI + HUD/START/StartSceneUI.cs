using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class StartSceneUI : MonoBehaviour
{
    [Header("Configuració de Botons")]
    [SerializeField] private Button playButton;
    [SerializeField] private float pulseSpeed = 2.0f;
    [SerializeField] private float pulseSize = 1.1f;

    [Header("Elements Professionals")]
    [SerializeField] private CanvasGroup legalInfoGroup;
    [SerializeField] private TextMeshProUGUI unityVersionText;

    private Vector3 _initialScale;

    private void Awake()
    {
        playButton.onClick.AddListener(OnPressPlay);
        _initialScale = playButton.transform.localScale;

        // Setup inicial de textos
        if (legalInfoGroup != null) legalInfoGroup.alpha = 0;
        if (unityVersionText != null)
        {
            unityVersionText.text = $"Powered by Unity {Application.unityVersion} LTS";
        }
    }

    private void Start()
    {
        StartCoroutine(FadeInLegalInfo());
        // Iniciem l'animació d'incitació al clic
        StartCoroutine(PulseButton());
    }

    private void OnDestroy()
    {
        playButton.onClick.RemoveListener(OnPressPlay);
    }

    public void OnPressPlay()
    {
        // Feedback visual instantáneo al clicar
        playButton.transform.localScale = _initialScale; 
        Loader.Load(Loader.Scene.MenuScene);
    }

    private IEnumerator PulseButton()
    {
        while (true) // Bucle infinito mientras la escena esté activa
        {
            float targetScale = Mathf.PingPong(Time.time * pulseSpeed, pulseSize - 1.0f) + 1.0f;
            playButton.transform.localScale = _initialScale * targetScale;
            yield return null;
        }
    }

    private IEnumerator FadeInLegalInfo()
    {
        yield return new WaitForSeconds(0.5f);
        float elapsed = 0;
        while (elapsed < 1.2f)
        {
            elapsed += Time.deltaTime;
            legalInfoGroup.alpha = Mathf.Lerp(0, 1, elapsed / 1.2f);
            yield return null;
        }
    }
}