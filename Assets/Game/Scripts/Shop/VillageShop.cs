using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Базар: окно, где заработанный опыт (XP) тратится на одежду, шапки, умения и спутников.
/// Открывается у торговца Асана (E) — там можно покупать; клавиша C открывает «гардероб»
/// в любом месте — там можно надеть уже купленное. Esc закрывает окно.
/// Создаётся само в игровых сценах, как и меню паузы.
/// </summary>
public class VillageShop : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    public static VillageShop Instance { get; private set; }

    public KeyCode wardrobeKey = KeyCode.C;

    private GameObject canvasRoot, root, grid;
    private TMP_Text balanceText, subtitleText, toastText, hudToast;
    private Image portrait;
    private GameObject sellButton;
    private CanvasGroup hudToastGroup;
    private float hudToastUntil = -1f, toastUntil = -1f;
    private bool traderMode;
    private string greeting;

    static readonly Color Gold = new Color(0.84f, 0.66f, 0.29f);
    static readonly Color Cream = new Color(0.96f, 0.93f, 0.85f);
    static readonly Color Muted = new Color(0.72f, 0.68f, 0.58f);
    static readonly Color WindowCol = new Color(0.11f, 0.085f, 0.065f, 0.97f);
    static readonly Color CardCol = new Color(0.19f, 0.145f, 0.105f, 1f);
    static readonly Color Green = new Color(0.45f, 0.72f, 0.38f);
    static readonly Color Red = new Color(0.86f, 0.36f, 0.30f);

    static readonly string[] GreetRu =
    {
        "Асан: Кош келиңиз! Заходи, смотри — всё сделано руками мастеров нашего села.",
        "Асан: Салам, досум! За честный опыт отдам лучший товар.",
        "Асан: Калпак, чапан, конь — у Асана есть всё, что нужно джигиту!"
    };
    static readonly string[] GreetEn =
    {
        "Asan: Кош келиңиз! Come in and look — everything is made by our village craftsmen.",
        "Asan: Салам, досум! For honest experience I'll give you the best goods.",
        "Asan: A kalpak, a chapan, a horse — Asan has all a young jigit needs!"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        IsOpen = false;
        if (scene.name == PauseMenu.MenuScene || FindAnyObjectByType<VillageShop>() != null) return;
        new GameObject("VillageShop").AddComponent<VillageShop>();
    }

    void Awake() => Instance = this;

    void Start()
    {
        Build();
        Loc.Changed += Rebuild;
        ShopCatalog.Changed += Refresh;
        GameManager.QuestCompleted += OnQuestCompleted;
    }

    void OnDestroy()
    {
        Loc.Changed -= Rebuild;
        ShopCatalog.Changed -= Refresh;
        GameManager.QuestCompleted -= OnQuestCompleted;
        if (IsOpen) { IsOpen = false; if (!PauseMenu.IsPaused) Time.timeScale = 1f; }
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (Input.GetKeyDown(wardrobeKey) && !PauseMenu.IsPaused)
        {
            if (IsOpen) Close();
            else if (!InDialogue() && !Modal.IsOpen && !RaceManager.Active) Open(false);
        }

        // Сообщение внутри окна
        if (toastText != null && toastUntil > 0f && Time.unscaledTime > toastUntil) { toastText.text = ""; toastUntil = -1f; }

        // Подсказка на экране после квеста
        if (hudToastGroup != null)
        {
            float a = Time.unscaledTime < hudToastUntil ? 1f : 0f;
            if (Time.unscaledTime < hudToastUntil - 6f) a = 0f;   // ждём, пока уйдёт плашка «Квест выполнен»
            hudToastGroup.alpha = Mathf.MoveTowards(hudToastGroup.alpha, IsOpen ? 0f : a, Time.unscaledDeltaTime * 3f);
        }
    }

    static bool InDialogue()
    {
        var dm = FindAnyObjectByType<DialogueManager>();
        return dm != null && dm.IsOpen;
    }

    // ------------------------------------------------------------------ открыть / закрыть

    /// <summary>trader = true — у торговца (можно покупать), false — гардероб.</summary>
    public void Open(bool trader)
    {
        if (root == null) return;
        traderMode = trader;
        greeting = Loc.IsEnglish ? GreetEn[Random.Range(0, GreetEn.Length)] : GreetRu[Random.Range(0, GreetRu.Length)];
        IsOpen = true;
        root.SetActive(true);
        Time.timeScale = 0f;
        GameAudio.Interact();
        Refresh();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        root.SetActive(false);
        if (!PauseMenu.IsPaused) Time.timeScale = 1f;
        GameAudio.Interact();
    }

    /// <summary>Закрыть, если открыто (для Esc в PauseMenu).</summary>
    public static bool CloseIfOpen()
    {
        if (!IsOpen || Instance == null) return false;
        Instance.Close();
        return true;
    }

    void OnQuestCompleted(string id, int xp)
    {
        if (hudToast == null) return;
        hudToast.text = Loc.T("Опыт можно потратить на <b><color=#E8B04A>базаре</color></b> у торговца Асана  ·  <b><color=#E8B04A>C</color></b> — гардероб",
                              "Spend your XP at the <b><color=#E8B04A>bazaar</color></b> with trader Asan  ·  <b><color=#E8B04A>C</color></b> — wardrobe");
        hudToastUntil = Time.unscaledTime + 3.6f + 6f;
    }

    // ------------------------------------------------------------------ действия

    void Click(ShopCatalog.Item it)
    {
        if (ShopCatalog.Owns(it.id))
        {
            ShopCatalog.Use(it.id);
            GameAudio.Interact();
            Toast(it.kind == ShopCatalog.Kind.Outfit || it.kind == ShopCatalog.Kind.Hat
                ? Loc.T("Надето: ", "Equipped: ") + it.nameKy
                : (ShopCatalog.IsActive(it.id) ? Loc.T("Включено: ", "On: ") : Loc.T("Выключено: ", "Off: ")) + it.nameKy, Cream);
            return;
        }

        if (!traderMode)
        {
            GameAudio.Wrong();
            Toast(Loc.T("Купить можно только у торговца Асана на базаре.", "You can only buy from trader Asan at the bazaar."), Red);
            return;
        }

        if (ShopCatalog.Balance < it.price)
        {
            GameAudio.Wrong();
            Toast(Loc.T("Не хватает опыта: нужно ещё ", "Not enough XP: you need ") + (it.price - ShopCatalog.Balance) + " XP" +
                  Loc.T(". Выполняй задания!", " more. Complete quests!"), Red);
            return;
        }

        if (ShopCatalog.Buy(it.id))
        {
            GameAudio.Correct();
            Toast(Loc.T("Куплено: ", "Bought: ") + it.nameKy + "  −" + it.price + " XP", Green);
        }
    }

    void Toast(string msg, Color c)
    {
        if (toastText == null) return;
        toastText.text = msg; toastText.color = c;
        toastUntil = Time.unscaledTime + 3.5f;
    }

    // ------------------------------------------------------------------ интерфейс

    void Rebuild()
    {
        bool was = IsOpen;
        if (canvasRoot != null) Destroy(canvasRoot);
        Build();
        if (was) { root.SetActive(true); Refresh(); }
    }

    void Build()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        canvasRoot = new GameObject("ShopCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasRoot.transform.SetParent(transform, false);
        var canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 450;
        var scaler = canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Подсказка после квеста (видна и при закрытом окне)
        var toast = Rect("QuestToast", canvasRoot.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 190), new Vector2(980, 54));
        toast.AddComponent<Image>().color = new Color(0.10f, 0.08f, 0.06f, 0.85f);
        hudToastGroup = toast.AddComponent<CanvasGroup>();
        hudToastGroup.alpha = 0f; hudToastGroup.blocksRaycasts = false;
        hudToast = Text(toast.transform, "", 22, Cream, Vector2.zero, new Vector2(960, 50));

        root = Rect("Overlay", canvasRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);

        var win = Rect("Window", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 860));
        win.AddComponent<Image>().color = WindowCol;
        var top = Rect("TopLine", win.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -3), new Vector2(0, 6));
        top.AddComponent<Image>().color = Gold;

        // Портрет торговца (Resources/MURAS/ui_portrait_asan)
        var pg = Rect("Portrait", win.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-528, 352), new Vector2(104, 104));
        portrait = pg.AddComponent<Image>();
        portrait.sprite = Resources.Load<Sprite>("MURAS/ui_portrait_asan");
        portrait.preserveAspect = true; portrait.raycastTarget = false;
        if (portrait.sprite == null) portrait.enabled = false;

        var title = Text(win.transform, "БАЗАР", 54, Gold, new Vector2(-250, 378), new Vector2(400, 64), FontStyles.Bold);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        subtitleText = Text(win.transform, "", 18, Muted, new Vector2(-210, 318), new Vector2(480, 56), FontStyles.Italic);
        subtitleText.alignment = TextAlignmentOptions.MidlineLeft;
        subtitleText.textWrappingMode = TextWrappingModes.Normal;

        var bal = Rect("Balance", win.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(440, 362), new Vector2(260, 70));
        bal.AddComponent<Image>().color = new Color(0.24f, 0.17f, 0.10f);
        Text(bal.transform, Loc.T("твой опыт", "your XP"), 15, Muted, new Vector2(0, 20), new Vector2(240, 22));
        balanceText = Text(bal.transform, "", 34, Gold, new Vector2(0, -8), new Vector2(240, 44), FontStyles.Bold);

        // продать из капчыка (рыба, еда) — только у торговца
        sellButton = Rect("SellFromBag", win.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(170, 362), new Vector2(250, 56));
        sellButton.AddComponent<Image>().color = new Color(0.36f, 0.50f, 0.28f);
        sellButton.AddComponent<Button>().onClick.AddListener(() => { Close(); if (InventoryWindow.Instance != null) InventoryWindow.Instance.Open(true); });
        Text(sellButton.transform, Loc.T("Сатуу · продать из капчыка", "Сатуу · sell from bag"), 18, Cream, Vector2.zero, new Vector2(240, 50), FontStyles.Bold);

        var close = Rect("Close", win.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -30), new Vector2(44, 44));
        close.AddComponent<Image>().color = new Color(0.30f, 0.20f, 0.13f);
        close.AddComponent<Button>().onClick.AddListener(Close);
        Text(close.transform, "X", 26, Cream, Vector2.zero, new Vector2(44, 44), FontStyles.Bold);

        grid = Rect("Grid", win.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -34), new Vector2(1140, 656));

        toastText = Text(win.transform, "", 22, Cream, new Vector2(0, -385), new Vector2(1100, 32), FontStyles.Bold);
        var foot = Text(win.transform, Loc.T("Esc — закрыть   ·   C — гардероб: купленное можно надеть где угодно", "Esc — close   ·   C — wardrobe: wear what you bought anywhere"),
                        16, Muted, new Vector2(0, -415), new Vector2(1100, 24));
        foot.fontStyle = FontStyles.Italic;

        root.SetActive(false);
    }

    void Refresh()
    {
        if (grid == null) return;
        foreach (Transform c in grid.transform) Destroy(c.gameObject);

        balanceText.text = ShopCatalog.Balance + " XP";
        if (portrait != null) portrait.gameObject.SetActive(traderMode && portrait.sprite != null);
        if (sellButton != null) sellButton.SetActive(traderMode);
        subtitleText.text = traderMode
            ? greeting
            : Loc.T("Гардероб · надевай купленное. Новые вещи — у торговца Асана на базаре.", "Wardrobe · wear what you own. New things are sold by trader Asan at the bazaar.");

        const int cols = 4; const float w = 272f, h = 212f, gap = 10f;
        float x0 = -(cols * w + (cols - 1) * gap) / 2f + w / 2f;
        float y0 = 656f / 2f - h / 2f;
        for (int i = 0; i < ShopCatalog.Items.Count; i++)
        {
            int col = i % cols, row = i / cols;
            Card(ShopCatalog.Items[i], new Vector2(x0 + col * (w + gap), y0 - row * (h + gap)), new Vector2(w, h));
        }
    }

    void Card(ShopCatalog.Item it, Vector2 pos, Vector2 size)
    {
        bool owned = ShopCatalog.Owns(it.id);
        bool equipped = owned && ShopCatalog.IsEquipped(it);
        bool wearable = it.kind == ShopCatalog.Kind.Outfit || it.kind == ShopCatalog.Kind.Hat;

        var card = Rect("Card_" + it.id, grid.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        var bg = card.AddComponent<Image>();
        bg.color = equipped ? Gold : new Color(0.30f, 0.23f, 0.16f);
        var inner = Rect("Inner", card.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-6, -6));
        inner.AddComponent<Image>().color = CardCol;
        var t = inner.transform;

        var strip = Rect("Swatch", t, new Vector2(0, 0), new Vector2(0, 1), new Vector2(7, 0), new Vector2(14, 0));
        strip.AddComponent<Image>().color = it.swatch;

        Left(Text(t, ShopCatalog.KindName(it.kind), 14, Muted, new Vector2(10, 84), new Vector2(230, 20)));
        Left(Text(t, it.nameKy, 27, Cream, new Vector2(10, 59), new Vector2(230, 34), FontStyles.Bold));
        Left(Text(t, Loc.T(it.nameRu, it.nameEn), 17, Gold, new Vector2(10, 34), new Vector2(230, 24)));
        var desc = Left(Text(t, Loc.T(it.descRu, it.descEn), 16, Muted, new Vector2(10, -10), new Vector2(230, 64)));
        desc.alignment = TextAlignmentOptions.TopLeft;
        desc.textWrappingMode = TextWrappingModes.Normal;

        string status; Color statusCol;
        string btn; bool interactable = true;
        if (!owned)
        {
            status = it.price + " XP";
            statusCol = ShopCatalog.Balance >= it.price ? Gold : Red;
            btn = traderMode ? Loc.T("Купить", "Buy") : Loc.T("У Асана", "At Asan's");
            interactable = traderMode;
        }
        else if (wearable)
        {
            status = Loc.T("Твоё", "Yours");
            statusCol = equipped ? Green : Cream;
            btn = equipped ? Loc.T("Надето", "Worn") : Loc.T("Надеть", "Wear");
            interactable = !equipped;
        }
        else
        {
            bool on = ShopCatalog.IsActive(it.id);
            status = on ? Loc.T("Активно", "Active") : Loc.T("Выключено", "Off");
            statusCol = on ? Green : Muted;
            btn = it.kind == ShopCatalog.Kind.Companion
                ? (on ? Loc.T("Отпустить", "Send home") : Loc.T("Позвать", "Call"))
                : (on ? Loc.T("Выключить", "Turn off") : Loc.T("Включить", "Turn on"));
        }

        var st = Left(Text(t, status, 22, statusCol, new Vector2(-56, -76), new Vector2(108, 34), FontStyles.Bold));
        st.enableAutoSizing = true; st.fontSizeMin = 12f; st.fontSizeMax = 22f;

        var b = Rect("Button", t, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(66, -76), new Vector2(120, 40));
        var bimg = b.AddComponent<Image>();
        bimg.color = interactable ? new Color(0.55f, 0.36f, 0.16f) : new Color(0.26f, 0.21f, 0.16f);
        var button = b.AddComponent<Button>();
        button.interactable = interactable;
        var colors = button.colors;
        colors.highlightedColor = new Color(1.25f, 1.2f, 1.1f); colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        colors.disabledColor = Color.white;
        button.colors = colors;
        ShopCatalog.Item captured = it;
        button.onClick.AddListener(() => Click(captured));
        Text(b.transform, btn, 18, interactable ? Cream : Muted, Vector2.zero, new Vector2(116, 38), FontStyles.Bold);
    }

    static TMP_Text Left(TMP_Text t) { t.alignment = TextAlignmentOptions.MidlineLeft; return t; }

    static GameObject Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return go;
    }

    static TMP_Text Text(Transform parent, string text, float size, Color color, Vector2 pos, Vector2 box, FontStyles style = FontStyles.Normal)
    {
        var go = Rect("Text", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, box);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = color; t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }
}
