using UnityEngine;

public class TrashBin : InteractableAsset
{
    // Rule: never accept/store items
    public override bool CanAccept(SceneObject sceneObject) => false;

    public override void Interact(Player player)
    {
        if (player == null) return;
        if (!player.HasSceneObject()) return;

        player.GetSceneObject().DeleteObject(); // deletes the held object, player gets cleared by DeleteObject()
    }
}