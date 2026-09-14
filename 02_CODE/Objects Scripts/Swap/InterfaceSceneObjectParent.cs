using UnityEngine;

public interface InterfaceSceneObjectParent
{
    public Transform GetSceneObjectSpawnReference();
    public void SetSceneObject(SceneObject sceneObject);
    public SceneObject GetSceneObject();
    public void ClearSceneObject();
    public bool HasSceneObject();
    bool CanAccept(SceneObject sceneObject);

}
    

