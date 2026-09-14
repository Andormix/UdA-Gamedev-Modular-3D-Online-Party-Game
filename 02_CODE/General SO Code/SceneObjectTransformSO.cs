using UnityEngine;


[CreateAssetMenu()]
public class SceneObjectTransformSO : ScriptableObject
{
    public SceneObjectSO input;
    public SceneObjectSO output;
    public int workProgressMax;
}
