using UnityEngine;

public class PriceBoard : InteractableAsset
{
    [SerializeField] private PriceBoardPresenter presenter;
    [SerializeField] private TutorialEventChannelSO tutorialEvents;
    [SerializeField] private string tutorialBoardId = "PriceBoard";

    public override void Interact(Player player)
    {
        if (presenter == null) return;

        bool opened = presenter.Toggle(player, transform);

        if (opened && TutorialRuntimeContext.IsTutorialRun && tutorialEvents != null)
            tutorialEvents.Raise(TutorialEventType.PriceBoardOpened, tutorialBoardId);
    }
}
