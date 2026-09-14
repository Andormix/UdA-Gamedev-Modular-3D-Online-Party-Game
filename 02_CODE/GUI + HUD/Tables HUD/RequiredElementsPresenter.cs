using System;
using UnityEngine;

public sealed class RequiredElementsPresenter : MonoBehaviour
{
    [SerializeField] private Table_StatePattern table;
    [SerializeField] private Transform iconTemplate;

    private void Awake()
    {
        if (table == null || iconTemplate == null)
        {
            Debug.LogError($"{nameof(RequiredElementsPresenter)} missing references in Inspector.", this);
            enabled = false;
            return;
        }

        iconTemplate.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        table.OnThrownElement += table_OnThrownElement;
        UpdateVisual();
    }

    private void OnDisable()
    {
        if (table != null)
            table.OnThrownElement -= table_OnThrownElement;
    }

    private void table_OnThrownElement(object sender, EventArgs e)
    {
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        foreach (Transform child in transform)
        {
            if (child == iconTemplate) continue;
            Destroy(child.gameObject);
        }

        foreach (SceneObjectSO sceneObjectSO in table.GetRemainingRequiredSceneObjectsSO())
        {
            Transform iconTransform = Instantiate(iconTemplate, transform);
            iconTransform.gameObject.SetActive(true);
            iconTransform.GetComponent<IconsSingleUI>().SetSceneObjectSO(sceneObjectSO);
        }
    }
}