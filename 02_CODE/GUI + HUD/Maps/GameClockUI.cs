using UnityEngine;
using UnityEngine.UI;

public class GameClockUI : MonoBehaviour
{
    [SerializeField] private Image timerImage;
    [SerializeField] private GameManager gamemanager;

    private void Awake()
    {
        if(gamemanager == null) Debug.Log("GameManager not set in GameClockUI");
        if(timerImage == null) Debug.Log("timerImage not set in GameClockUI");
    }

    private void Update()
    {
        timerImage.fillAmount = gamemanager.GetClockTimerNormalized();
    }
}
