using UnityEngine;

public class TableIdentity : MonoBehaviour
{
    [SerializeField] private string displayName = "Table";

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Table" : displayName.Trim();
}