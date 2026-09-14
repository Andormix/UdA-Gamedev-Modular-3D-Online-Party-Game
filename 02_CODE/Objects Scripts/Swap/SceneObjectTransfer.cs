using UnityEngine;

public static class SceneObjectTransfer
{
    // Tries to transfer one SceneObject between two parents using consistent rules:
    // - If target has object and source empty => move target->source
    // - Else if source has object and target empty => move source->target
    // - Else do nothing
    public static bool TryTransferEitherDirection(InterfaceSceneObjectParent a, InterfaceSceneObjectParent b)
    {
        if (a == null || b == null) return false;

        // 1) b -> a
        if (!a.HasSceneObject() && b.HasSceneObject())
        {
            var obj = b.GetSceneObject();
            if (a.CanAccept(obj))
            {
                obj.SetSceneObjectParent(a);
                return true;
            }
        }

        // 2) a -> b
        if (!b.HasSceneObject() && a.HasSceneObject())
        {
            var obj = a.GetSceneObject();
            if (b.CanAccept(obj))
            {
                obj.SetSceneObjectParent(b);
                return true;
            }
        }

        return false;
    }


    // One-way transfer: from to (only if from has object and to can accept).
    public static bool TryTransfer(InterfaceSceneObjectParent from, InterfaceSceneObjectParent to)
    {
        if (from == null || to == null) return false;
        if (!from.HasSceneObject()) return false;

        var obj = from.GetSceneObject();
        if (!to.CanAccept(obj)) return false;

        obj.SetSceneObjectParent(to);
        return true;
    }
}