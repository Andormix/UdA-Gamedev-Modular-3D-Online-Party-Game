using UnityEngine;

public class EmptyCounter : InteractableAsset
{
    [SerializeField] private SceneObjectSO sceneObjectSO;

    public override void Interact(Player player)
    {
        SceneObjectTransfer.TryTransferEitherDirection(player, this);
    }
}