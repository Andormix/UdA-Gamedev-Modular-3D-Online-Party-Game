using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class RecipeSO : ScriptableObject
{
    public string recipeName;
    public SceneObjectSO outputObjectSO;
    public List<SceneObjectSO> sceneObjectSOList;
}
