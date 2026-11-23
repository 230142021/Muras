using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Окно «Капчык» (I или Tab): ячейки с предметами, справа — описание и кнопки
/// «Съесть» (еда даёт бодрость) и «Продать Асану» (рядом с торговцем). Создаётся само.
/// </summary>
public class InventoryWindow : MonoBehaviour, IModal
{
    public static InventoryWindow Instance { get; private set; }

    private GameObject canvas, root, grid, detail;
    private TMP_Text title, buffHud, info, nameText, countText;
    private RawImage bigIcon;
    private Button useBtn, sellBtn;
    private TMP_Text useLabel, sellLabel;
    private string selected;
    private bool open, sellMode;
    private readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnLoaded(Scene s, LoadSceneMode m)
    {
        if (s.name == PauseMenu.MenuScene || FindAnyObjectByType<InventoryWindow>() != null) return;
        new GameObject("InventoryWindow").AddComponent<InventoryWindow>();
    }

    void Awake() => Instance = this;
    void Start() { Build(); Loc.Changed += Rebuild; Inventory.Changed += Refresh; }
    void OnDestroy() { Loc.Changed -= Rebuild; Inventory.Changed -= Refresh; if (open) { Modal.Close(this); if (!PauseMenu.IsPaused) Time.timeScale = 1f; } }

    Texture2D Icon(string id)
    {
        if (!icons.TryGetValue(id, out Texture2D t)) { t = Resources.Load<Texture2D>("MURAS/Icons/" + id); icons[id] = t; }
        return t;
    }

    void Update()
    {
        if (buffHud != null)
        {
            float left = PlayerBuff.SecondsLeft;
            buffHud.text = left > 0.5f ? Loc.T("Бодрость: ", "Energy: ") + "<b>" + PlayerBuff.Source + "</b>  " + Mathf.CeilToInt(left) + Loc.T(" с", " s") : "";
        }
        if (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab))
        {
            if (open) Close();
            else if (Modal.Free) Open(false);
        }
    }

    /// <summary>sell = true — открыто у торговца, можно продавать.</summary>
    public void Open(bool sell)
    {
        sellMode = sell || ShopTrader.PlayerNear;
        open = true; Modal.Open(this);
        root.SetActive(true);
        Time.timeScale = 0f;
        MurasUI.Play("tile_turn", 0.6f, 0.9f);
        if (selected == null || Inventory.Count(selected) == 0) selected = FirstOwned();
        Refresh();
    }

    public void Close()
    {
        if (!open) return;
        open = false; root.SetActive(false); Modal.Close(this);
        if (!PauseMenu.IsPaused) Time.timeScale = 1f;
    }
    public void CloseModal() => Close();

    static string FirstOwned() { foreach (Inventory.Item it in Inventory.Items) if (Inventory.Count(it.id) > 0) return it.id; return null; }

    void Rebuild() { bool was = open; if (canvas != null) Destroy(canvas); Build(); if (was) { root.SetActive(true); Refresh(); } }

    void Build()
    {
        canvas = MurasUI.Canvas("InventoryCanvas", transform, 452);
        var hud = MurasUI.Rect("BuffHud", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(600, 30));
        buffHud = MurasUI.Text(hud.transform, "", 19, MurasUI.Green, Vector2.zero, new Vector2(600, 30), FontStyles.Bold);

        root = MurasUI.Overlay(canvas.transform);
        var win = MurasUI.WindowBox(root.transform, new Vector2(1160, 760));
        var w = win.transform;
        title = MurasUI.Text(w, "КАПЧЫК", 50, MurasUI.Gold, new Vector2(-360, 330), new Vector2(400, 64), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        info = MurasUI.Text(w, "", 19, MurasUI.Muted, new Vector2(-120, 288), new Vector2(880, 30), FontStyles.Italic, TextAlignmentOptions.MidlineLeft);
        MurasUI.CloseButton(w, Close);
        grid = MurasUI.Rect("Grid", w, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-250, -40), new Vector2(600, 480));

        detail = MurasUI.Box("Detail", w, new Vector2(320, -40), new Vector2(420, 560), MurasUI.Card);
        var d = detail.transform;
        bigIcon = MurasUI.Rect("Icon", d, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(170, 170)).AddComponent<RawImage>();
        nameText = MurasUI.Text(d, "", 30, MurasUI.Cream, new Vector2(0, 55), new Vector2(390, 80), FontStyles.Bold);
        countText = MurasUI.Text(d, "", 20, MurasUI.Gold, new Vector2(0, 5), new Vector2(390, 30));
        var desc = MurasUI.Text(d, "", 19, MurasUI.Muted, new Vector2(0, -70), new Vector2(380, 110), FontStyles.Normal, TextAlignmentOptions.Top);
        desc.name = "Desc";
        useBtn = MurasUI.Button(d, "", new Vector2(0, -170), new Vector2(320, 56), () => { if (selected != null && Inventory.Use(selected)) AfterAction(); }, 22);
        useLabel = useBtn.GetComponentInChildren<TMP_Text>();
        sellBtn = MurasUI.Button(d, "", new Vector2(0, -238), new Vector2(320, 56), () => { if (selected != null && Inventory.Sell(selected)) AfterAction(); }, 20);
        sellLabel = sellBtn.GetComponentInChildren<TMP_Text>();

        MurasUI.Text(w, Loc.T("I / Tab / Esc — закрыть   ·   рыбу продавай торговцу Асану на базаре", "I / Tab / Esc — close   ·   sell fish to trader Asan at the bazaar"),
                     16, MurasUI.Muted, new Vector2(0, -350), new Vector2(1100, 26), FontStyles.Italic);
        root.SetActive(false);
    }

    void AfterAction()
    {
        if (Inventory.Count(selected) == 0) selected = FirstOwned();
        Refresh();
    }

    void Refresh()
    {
        if (grid == null) return;
        foreach (Transform c in grid.transform) Destroy(c.gameObject);
        int total = Inventory.Total;
        info.text = total == 0
            ? Loc.T("Пока пусто. Собирай яблоки, лови рыбу, помогай жителям — подарки попадут сюда.", "Empty for now. Pick apples, fish, help villagers — their gifts end up here.")
            : Loc.T("Предметов: ", "Items: ") + total + (sellMode ? Loc.T("   ·   Асан рядом — можно продавать", "   ·   Asan is near — you can sell") : "");

        const int cols = 5; const float cell = 112f, gap = 10f;
        int k = 0;
        foreach (Inventory.Item it in Inventory.Items)
        {
            int n = Inventory.Count(it.id);
            if (n <= 0) continue;
            AddSlot(it, n, k++);
        }
        for (; k < 20; k++) AddSlot(null, 0, k);

        void AddSlot(Inventory.Item it, int n, int index)
        {
            int col = index % cols, row = index / cols;
            Vector2 pos = new Vector2(-300 + cell / 2f + col * (cell + gap), 240 - cell / 2f - row * (cell + gap));
            bool sel = it != null && it.id == selected;
            var slot = MurasUI.Box("Slot" + index, grid.transform, pos, new Vector2(cell, cell), sel ? MurasUI.Gold : new Color(0.24f, 0.18f, 0.13f));
            var inner = MurasUI.Box("Inner", slot.transform, Vector2.zero, new Vector2(cell - 6, cell - 6), new Color(0.15f, 0.115f, 0.085f));
            if (it == null) return;
            var ic = MurasUI.Rect("Icon", inner.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(86, 86)).AddComponent<RawImage>();
            ic.texture = Icon(it.id); ic.raycastTarget = false;
            MurasUI.Text(inner.transform, "×" + n, 20, MurasUI.Cream, new Vector2(30, -38), new Vector2(60, 26), FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            var b = slot.AddComponent<Button>();
            string id = it.id;
            b.onClick.AddListener(() => { selected = id; MurasUI.Play("tile_turn", 0.4f, 1.2f); Refresh(); });
        }

        Inventory.Item s = selected != null ? Inventory.Get(selected) : null;
        bool has = s != null && Inventory.Count(s.id) > 0;
        detail.SetActive(has);
        if (!has) return;
        bigIcon.texture = Icon(s.id);
        nameText.text = s.ky + "\n<size=65%><color=#C9BFA6>" + Loc.T(s.ru, s.en) + "</color></size>";
        countText.text = Loc.T("В капчыке: ", "In the bag: ") + Inventory.Count(s.id);
        detail.transform.Find("Desc").GetComponent<TMP_Text>().text = Loc.T(s.descRu, s.descEn);
        useBtn.gameObject.SetActive(s.kind == Inventory.Kind.Food || s.kind == Inventory.Kind.Tool);
        useLabel.text = s.kind == Inventory.Kind.Tool ? (PlayerLantern.IsOn ? Loc.T("Выключить (F)", "Turn off (F)") : Loc.T("Включить (F)", "Turn on (F)"))
                      : s.id == "kymyz" ? Loc.T("Выпить", "Drink") : Loc.T("Съесть", "Eat");
        sellBtn.gameObject.SetActive(s.sell > 0);
        sellBtn.interactable = sellMode;
        sellLabel.text = sellMode ? Loc.T("Продать Асану  +", "Sell to Asan  +") + s.sell + " XP" : Loc.T("Продать можно у Асана", "Sell at Asan's stall");
    }
}
