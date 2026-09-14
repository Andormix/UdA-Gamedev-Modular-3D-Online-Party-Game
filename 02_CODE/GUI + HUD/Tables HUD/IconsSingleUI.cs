using UnityEngine;
using UnityEngine.UI;

public sealed class IconsSingleUI : MonoBehaviour
{
    [SerializeField] private Image image;

    public void SetSceneObjectSO(SceneObjectSO sceneObjectSO)
    {
        if (image == null) return;
        image.sprite = sceneObjectSO.sprite;
    }
}