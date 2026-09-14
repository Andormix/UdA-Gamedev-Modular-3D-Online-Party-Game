using Unity.Netcode;
using UnityEngine;

public class TableNpcClientView : NetworkBehaviour
{
    [SerializeField] private TableNpcNetState netState;

    private void Awake()
    {
        if (netState == null) netState = GetComponent<TableNpcNetState>();
    }

    private void Update()
    {
        if (IsServer) return;
        if (netState == null) return;

        // REMEMBER ERIC FROM THE FUTURE:
        // With NetworkTransform on root, do NOT set transform.position/rotation here.

    }
}