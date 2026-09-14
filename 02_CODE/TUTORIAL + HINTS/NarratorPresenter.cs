using TMPro;
using UnityEngine;

public sealed class NarratorPresenter : MonoBehaviour
{
    [SerializeField] private UserInterfaceManager ui;
    [SerializeField] private TextMeshProUGUI narratorText;

    private float hideAt;
    private bool showing;
    private bool interruptibleCurrent;
    private bool persistentCurrent;

    private void Awake()
    {
        if (ui == null || narratorText == null)
        {
            //Debug.LogError("NarratorPresenter missing refs", this);
            enabled = false;
            return;
        }

        ui.Hide(UserInterfaceManager.UIPage.Narrator);
    }

    // NEW: persistent parameter
    public void Show(string text, float minShowSeconds, bool canInterrupt, bool persistent)
    {
        // if current line is not interruptible, refuse replacement
        if (showing && !interruptibleCurrent) return;

        Debug.Log($"[NarratorPresenter] Show: {text} | persistent={persistent}");

        narratorText.text = text;
        interruptibleCurrent = canInterrupt;
        persistentCurrent = persistent;

        ui.Show(UserInterfaceManager.UIPage.Narrator);
        showing = true;

        if (persistentCurrent)
        {
            hideAt = float.PositiveInfinity; // never auto-hide
        }
        else
        {
            hideAt = Time.time + Mathf.Max(0.25f, minShowSeconds);
        }
    }

    public void Hide()
    {
        showing = false;
        persistentCurrent = false;
        ui.Hide(UserInterfaceManager.UIPage.Narrator);
    }

    private void Update()
    {
        if (!showing) return;
        if (persistentCurrent) return;

        if (Time.time >= hideAt)
        {
            showing = false;
            ui.Hide(UserInterfaceManager.UIPage.Narrator);
        }
    }


    public void ForceShow(string text, float minShowSeconds, bool canInterrupt, bool persistent)
    {
        Debug.Log($"[NarratorPresenter] ForceShow: {text} | persistent={persistent}");

        narratorText.text = text;
        interruptibleCurrent = canInterrupt;
        persistentCurrent = persistent;

        ui.Show(UserInterfaceManager.UIPage.Narrator);
        showing = true;

        hideAt = persistentCurrent
            ? float.PositiveInfinity
            : Time.time + Mathf.Max(0.25f, minShowSeconds);
    }
}