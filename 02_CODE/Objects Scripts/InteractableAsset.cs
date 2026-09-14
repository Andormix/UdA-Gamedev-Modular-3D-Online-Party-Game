using UnityEngine;

public class InteractableAsset : MonoBehaviour, InterfaceSceneObjectParent
{
    // --------------------  VAR --------------------

    private SceneObject sceneObject; // Current Scene Object. (We could meke it protected instead)
    [SerializeField] private string interactTxT; 

    //TODO: MAKE ABSTRACT CLASS INSTEAD
    public virtual void Interact(Player player)
    {
        Debug.LogError("InteractableAsset.Interact should not be triggerd");
    }

    public virtual void InteractAct(Player player)
    {
        Debug.LogError("InteractableAsset.Interact should not be triggerd");
    }

    // ---------------- Interface Functions ----------------
    public virtual Transform GetSceneObjectSpawnReference()
    {
        return null;
    }

    public void SetSceneObject(SceneObject sceneObject)
    {
        this.sceneObject = sceneObject;
    }

    public SceneObject GetSceneObject()
    {
        return this.sceneObject;
    }

    public void ClearSceneObject()
    {
        this.sceneObject = null;
    }

    public bool HasSceneObject()
    {
        return sceneObject != null;
    }

    public string GetInteractTxT()
    {
        return interactTxT;
    }

    public bool IsSceneObjectFinalComponent()
    {
        return sceneObject != null && sceneObject.GetSceneObjectSO().finalComponent;
    }

    public virtual bool CanAccept(SceneObject sceneObject) => sceneObject != null && !HasSceneObject();
    
}
