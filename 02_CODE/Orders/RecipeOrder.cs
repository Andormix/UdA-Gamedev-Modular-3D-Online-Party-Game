using System;

[Serializable]
public class RecipeOrder
{
    public int id;
    public RecipeSO recipe;
    public float duration;
    public bool assignedToWorkstation;
    public double endTime;
    public ulong tableNetId;
    public string tableDisplayName;
    public int ownerTeamId;

    public RecipeOrder(int id, RecipeSO recipe, float duration, double endTime)
    {
        this.id = id;
        this.recipe = recipe;
        this.duration = duration;
        this.endTime = endTime;
        this.assignedToWorkstation = false;

        this.tableNetId = 0;
        this.tableDisplayName = string.Empty;
        this.ownerTeamId = -1;
    }
}