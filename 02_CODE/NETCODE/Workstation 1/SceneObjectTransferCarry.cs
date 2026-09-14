using UnityEngine;

public static class SceneObjectTransferCarry
{
    // Move the first acceptable carried item from player's stack into 'to'.(LIFO preference).
    public static bool TryTransferAnyFromPlayerTo(Player player, InterfaceSceneObjectParent to)
    {
        if (player == null || to == null) return false;

        var carry = player.GetComponent<PlayerCarryNet>();
        if (carry == null) return false;

        if (to.HasSceneObject()) return false;

        // Scan from top -> bottom
        for (int i = carry.Count - 1; i >= 0; i--)
        {
            if (!carry.TryGetHeldAt(i, out SceneObject candidate) || candidate == null)
                continue;

            if (!to.CanAccept(candidate))
                continue;

            // Remove that exact item from stack (this will re-stack sockets)
            if (!carry.ServerTryRemoveAt(i, out SceneObject removed) || removed == null)
                return false;

            // Parent into workstation/counter/etc using your existing single-slot system
            removed.SetSceneObjectParent(to);
            return true;
        }

        return false;
    }

    public static bool TryTransferFromParentToPlayer(InterfaceSceneObjectParent from, Player player)
    {
        if (from == null || player == null) return false;
        if (!from.HasSceneObject()) return false;

        var carry = player.GetComponent<PlayerCarryNet>();
        if (carry == null) return false;
        if (!carry.HasFreeSlot) return false;

        SceneObject obj = from.GetSceneObject();
        if (obj == null) return false;

        obj.ClearSceneObjectParent();

        return carry.ServerTryPush(obj);
    }
}