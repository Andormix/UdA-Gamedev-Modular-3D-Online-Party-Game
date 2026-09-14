using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UITutorialColorRotation : MonoBehaviour
{
    [Header("Configuración de Colores")]
    [SerializeField] private Color highlightColor = Color.yellow;
    [SerializeField] private Color baseColor = Color.white;
    
    [Header("Configuración de Tiempo")]
    [SerializeField] private float changeInterval = 1.0f;

    [Header("Imágenes (Si está vacío, busca en hijos)")]
    [SerializeField] private List<Image> targetImages = new List<Image>();

    private float timer;
    private int currentIndex = 0;

    void Awake()
    {
        // Si no asignaste imágenes manualmente, las busca automáticamente en los hijos
        if (targetImages.Count == 0)
        {
            targetImages.AddRange(GetComponentsInChildren<Image>());
        }
    }

    void OnEnable()
    {
        timer = 0f;
        currentIndex = 0;
        ResetAllColors();
        ApplyHighlight();
    }

    void Update()
    {
        if (targetImages.Count == 0) return;

        timer += Time.deltaTime;

        if (timer >= changeInterval)
        {
            timer = 0f;
            RotateColor();
        }
    }

    private void RotateColor()
    {
        // Volvemos la imagen actual al color base
        if (currentIndex < targetImages.Count)
            targetImages[currentIndex].color = baseColor;

        // Avanzamos al siguiente índice
        currentIndex = (currentIndex + 1) % targetImages.Count;

        // Aplicamos el color resaltado a la nueva imagen
        ApplyHighlight();
    }

    private void ApplyHighlight()
    {
        if (targetImages.Count > 0 && currentIndex < targetImages.Count)
        {
            targetImages[currentIndex].color = highlightColor;
        }
    }

    private void ResetAllColors()
    {
        foreach (Image img in targetImages)
        {
            if (img != null) img.color = baseColor;
        }
    }

    void OnDisable()
    {
        ResetAllColors();
    }
}