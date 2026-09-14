using System;
using System.Collections.Generic;
using UnityEngine;

public class UserInterfaceManager : MonoBehaviour
{
    public enum UIPage
    {
        None = 0,

        // Gameplay
        HUD,
        Inventory,
        Pause,
        Interact,
        Countdown,
        GameOver,
        Waiting,
        Validating,
        TPV,

        // Menus
        MainMenu,
        CustomizationMenu,
        Settings,

        // TODO 227: Fix order. (L'ordre altera el funcionament dels components GO ja creats.)
        PriceBoard,
        Narrator,

        Campaign,
        LobbyOnline, 
        Credits,
    }

    [Serializable]
    public class PageEntry
    {
        public UIPage page;
        public GameObject root;         
        public bool disableOnAwake = true;
    }

    public static UserInterfaceManager Instance { get; private set; }

    [Header("Scene-specific pages (set in Inspector)")]
    [SerializeField] private List<PageEntry> pages = new();

    [Header("Optional defaults")]
    [SerializeField] private UIPage defaultPage = UIPage.None;

    private readonly Dictionary<UIPage, GameObject> map = new();

    private void Awake()
    {
        // Singleton (scene-local). If we want persistent UI tenmos que add DontDestroyOnLoad(gameObject).
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        map.Clear();

        foreach (var entry in pages)
        {
            if (entry == null || entry.root == null) continue;

            if (entry.page == UIPage.None)
            {
                Debug.LogWarning($"{name}: PageEntry has UIPage.None; skipping.");
                continue;
            }

            if (map.ContainsKey(entry.page))
            {
                Debug.LogWarning($"{name}: Duplicate page entry for {entry.page}; skipping duplicate.");
                continue;
            }

            map.Add(entry.page, entry.root);

            if (entry.disableOnAwake)
                entry.root.SetActive(false);
        }

        if (defaultPage != UIPage.None)
            ShowOnly(defaultPage);
    }

    public void ShowOnly(UIPage page)
    {
        foreach (var kv in map)
            kv.Value.SetActive(kv.Key == page);
    }

    public void HideAll()
    {
        foreach (var kv in map)
            kv.Value.SetActive(false);
    }

    public bool IsVisible(UIPage page)
    {
        return map.TryGetValue(page, out var go) && go != null && go.activeSelf;
    }

    public GameObject GetPageRoot(UIPage page)
    {
        map.TryGetValue(page, out var go);
        return go; // null if page not registered
    }

    public GameObject GetInteractUI()
    {
        return GetPageRoot(UIPage.Interact);
    }

    public void Show(UIPage page)
    {
        if (!map.TryGetValue(page, out var go) || go == null)
        {
            return;
        }

        go.SetActive(true);
    }

    public void Hide(UIPage page)
    {
        if (!map.TryGetValue(page, out var go) || go == null)
        {
            return;
        }

        go.SetActive(false);
    }
}