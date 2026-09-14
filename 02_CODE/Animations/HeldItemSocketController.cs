using UnityEngine;

public class HeldItemSocketController : MonoBehaviour
{
    public enum HoldType
    {
        None = 0,
        Tray = 1,
        DrinkFood = 2,
        Plate = 3
    }

    [Header("Animated sockets under PlayerVisual/Root")]
    [SerializeField] private Transform socketTray;       // Root/held_item_tray
    [SerializeField] private Transform socketDrinkFood;  // Root/held_item_drink_food
    [SerializeField] private Transform socketPlate;      // Root/held_item_plate

    [Header("Pool container (kept separate from rig)")]
    [SerializeField] private Transform itemsPoolRoot;    // Items_All

    [Header("Items from pool")]
    [SerializeField] private Transform heldTray;         // Items_All/held_Tray
    [SerializeField] private Transform heldDrinkFood;    // Items_All/held_Coffee_Full
    [SerializeField] private Transform heldPlate;        // Items_All/held_set_1_Plate

    [Header("Optional animator drive")]
    [SerializeField] private Animator animator;
    [SerializeField] private string holdTypeParam = "HeldItemId";

    private int _holdTypeHash;
    private int _lastAnimatorValue = int.MinValue;

    private void Awake()
    {
        _holdTypeHash = Animator.StringToHash(holdTypeParam);
        ReturnAllToPool(); // start clean
    }

    private void LateUpdate()
    {
        if (animator == null) return;

        int value = animator.GetInteger(_holdTypeHash);
        if (value == _lastAnimatorValue) return;

        _lastAnimatorValue = value;
        SetHoldType((HoldType)value);
    }

    public void SetHoldType(HoldType holdType)
    {
        // First: unequip everything
        ReturnAllToPool();

        // Then equip only one
        switch (holdType)
        {
            case HoldType.Tray:
                AttachToSocket(heldTray, socketTray);
                break;

            case HoldType.DrinkFood:
                AttachToSocket(heldDrinkFood, socketDrinkFood);
                break;

            case HoldType.Plate:
                AttachToSocket(heldPlate, socketPlate);
                break;

            case HoldType.None:
            default:
                break;
        }
    }

    private void ReturnAllToPool()
    {
        ReturnToPool(heldTray);
        ReturnToPool(heldDrinkFood);
        ReturnToPool(heldPlate);
    }

    private void ReturnToPool(Transform item)
    {
        if (item == null) return;

        if (itemsPoolRoot != null)
            item.SetParent(itemsPoolRoot, false);

        item.gameObject.SetActive(false);
    }

    public static void AttachToSocket(Transform item, Transform socket)
    {
        if (item == null || socket == null) return;

        item.SetParent(socket, false);
        item.localPosition = Vector3.zero;
        item.localRotation = Quaternion.identity;
        item.localScale = Vector3.one;
        item.gameObject.SetActive(true);
    }
}