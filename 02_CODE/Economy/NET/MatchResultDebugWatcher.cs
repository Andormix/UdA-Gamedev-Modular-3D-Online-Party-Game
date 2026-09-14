using UnityEngine;

public class MatchResultDebugWatcher : MonoBehaviour
{
    [SerializeField] private bool logOnEnable = true;
    [SerializeField] private bool logOnResultEvent = true;

    private void OnEnable()
    {
        if (MatchResultNetSync.Instance != null)
        {
            MatchResultNetSync.Instance.OnResultsUpdated += OnResultsUpdated;
        }

        if (logOnEnable)
            PrintNow("OnEnable");
    }

    private void OnDisable()
    {
        if (MatchResultNetSync.Instance != null)
            MatchResultNetSync.Instance.OnResultsUpdated -= OnResultsUpdated;
    }

    [ContextMenu("Print MatchResult Snapshot Now")]
    public void PrintNow()
    {
        PrintNow("Manual");
    }

    private void PrintNow(string source)
    {
        if (MatchResultNetSync.Instance == null)
        {
            Debug.Log($"[MatchResultDebugWatcher] ({source}) Instance is null");
            return;
        }

        int count = MatchResultNetSync.Instance.GetRanking().Count;
        var summary = MatchResultNetSync.Instance.GetSummary();

        Debug.Log($"[MatchResultDebugWatcher] ({source}) Count={count}, topPlayer={summary.topPlayerName}, topScore={summary.topScore}, localScore={summary.localScore}, stars={summary.localStars}, coins={summary.localCoinsEarned}, diamonds={summary.localDiamondsEarned}");
    }

    private void OnResultsUpdated()
    {
        if (!logOnResultEvent) return;
        PrintNow("OnResultsUpdated");
    }
}