using UnityEngine;

public class TableSeatPoint : MonoBehaviour
{
    [SerializeField] private Transform sitAnchor;
    [SerializeField] private Transform sitFinalAnchor;
    [SerializeField] private bool occupied;

    public Transform SitAnchor => sitAnchor != null ? sitAnchor : transform;
    public Transform SitFinalAnchor => sitFinalAnchor != null ? sitFinalAnchor : SitAnchor;
    public bool IsOccupied => occupied;

    public void SetOccupied(bool value) => occupied = value;
}