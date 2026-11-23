using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Пауза по Esc в игровых сценах: Продолжить / Настройки / Главное меню / Выход.
/// Создаётся сама при загрузке любой сцены, кроме главного меню, — ничего настраивать не нужно.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public const string MenuScene = "MainMenu";

    public static bool IsPaused { get; private set; }

    private GameObject root;
    private GameObject mainWindow;
    private GameObject settingsWindow;
    private GameObject canvasRoot;

    private static readonly Color Gold = new Color(0.84f, 0.66f, 0.29f);
    private static readonly Color Cream = new Color(0.96f, 0.93f, 0.85f);
    private static readonly Color Muted = new Color(0.79f, 0.75f, 0.65f);
    private static readonly Color Window = new Color(0.11f, 0.085f, 0.065f, 0.97f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        IsPaused = false;
        Time.timeScale = 1f;
        if (scene.name == MenuScene || FindAnyObjectByType<PauseMenu>() != null) return;
        new GameObject("PauseMenu").AddComponent<PauseMenu>();
    }

    void Start()
    {
        Build();
        Loc.Changed += Rebuild;
    }

    // Смена языка прямо в паузе: пересобираем окна на новом языке и остаёмся в настройках
    void Rebuild()
    {
        bool wasPaused = IsPaused;
        bool inSettings = settingsWindow != null && settingsWindow.activeSelf;
        if (canvasRoot != null) Destroy(canvasRoot);
        Build();
        if (wasPaused)
        {
            root.SetActive(true);
            if (inSettings) ShowSettings(); else ShowMain();
        }
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (!IsPaused)
        {
            if (VillageShop.CloseIfOpen()) return;   // сначала закрываем окно базара
            if (Modal.TryClose()) return;            // словарь, комуз, шырдак, рыбалка
            // Esc сначала закрывает диалог или реплику, и только потом открывает паузу
            var dm = FindAnyObjectByType<DialogueManager>();
            if (dm != null && dm.IsOpen) { dm.CloseDialogue(); return; }
            if (SpeechPanel.Instance != null && SpeechPanel.Instance.IsShowing) { SpeechPanel.Instance.Close(); return; }
        }
        if (IsPaused && settingsWindow.activeSelf) ShowMain();
        else SetPaused(!IsPaused);
    }

    void OnDestroy()
    {
        Loc.Changed -= Rebuild;
        if (IsPaused) { IsPaused = false; Time.timeScale = 1f; AudioListener.pause = false; }
    }

    public void SetPaused(bool paused)
    {
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;
        root.SetActive(paused);
        if (paused) ShowMain();
        MenuSound.Click();
    }

    void ShowMain() { mainWindow.SetActive(true); settingsWindow.SetActive(false); }
    void ShowSettings() { mainWindow.SetActive(false); settingsWindow.SetActive(true); }

    void ToMenu()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        GameSettings.Save();
        SceneManager.LoadScene(MenuScene);
    }

    void Quit()
    {
        GameSettings.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------------------------------------------------- построение UI

    void Build()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var canvasGo = new GameObject("PauseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        canvasRoot = canvasGo;
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        root = Rect("Overlay", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        // Главное окно паузы
        mainWindow = WindowBox(root.transform, new Vector2(560, 560));
        Text(mainWindow.transform, Loc.T("ПАУЗА", "PAUSE"), 52, Gold, new Vector2(0, 205), new Vector2(500, 70), FontStyles.Bold);
        Text(mainWindow.transform, Loc.T("Тыныгуу  ·  Esc — вернуться в игру", "Тыныгуу  ·  Esc — back to the game"), 20, Muted, new Vector2(0, 155), new Vector2(500, 30), FontStyles.Italic);
        Button(mainWindow.transform, "Улантуу", Loc.T("Продолжить", "Continue"), new Vector2(0, 75), () => SetPaused(false));
        Button(mainWindow.transform, "Жөндөөлөр", Loc.T("Настройки", "Settings"), new Vector2(0, -15), ShowSettings);
        Button(mainWindow.transform, "Башкы меню", Loc.T("Главное меню (прогресс сохранён)", "Main menu (progress is saved)"), new Vector2(0, -105), ToMenu);
        Button(mainWindow.transform, "Чыгуу", Loc.T("Выйти из игры", "Quit the game"), new Vector2(0, -195), Quit);

        // Окно настроек
        settingsWindow = WindowBox(root.transform, new Vector2(700, 600));
        Text(settingsWindow.transform, "ЖӨНДӨӨЛӨР", 44, Gold, new Vector2(0, 240), new Vector2(640, 60), FontStyles.Bold);
        var panel = settingsWindow.AddComponent<SettingsPanel>();
        panel.volumeSlider = SliderRow(settingsWindow.transform, Loc.T("Үн  ·  Громкость", "Үн  ·  Volume"), new Vector2(0, 150), out panel.volumeValue);
        panel.sensitivitySlider = SliderRow(settingsWindow.transform, Loc.T("Чычкан  ·  Мышь", "Чычкан  ·  Mouse"), new Vector2(0, 70), out panel.sensitivityValue);
        QualityRow(settingsWindow.transform, new Vector2(0, -10), panel);
        LanguageRow(settingsWindow.transform, new Vector2(0, -90), panel);
        Button(settingsWindow.transform, "Артка", Loc.T("Назад", "Back"), new Vector2(0, -215), ShowMain);

        settingsWindow.SetActive(false);
        root.SetActive(false);
    }

    static GameObject Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return go;
    }

    static GameObject WindowBox(Transform parent, Vector2 size)
    {
        var w = Rect("Window", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        w.AddComponent<Image>().color = Window;
        var top = Rect("TopLine", w.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -3), new Vector2(0, 6));
        top.AddComponent<Image>().color = Gold;
        return w;
    }

    static TMP_Text Text(Transform parent, string text, float size, Color color, Vector2 pos, Vector2 box, FontStyles style = FontStyles.Normal)
    {
        var go = Rect("Text", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, box);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = color; t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    static void Button(Transform parent, string title, string sub, Vector2 pos, UnityEngine.Events.UnityAction action)
    {
        var go = Rect("Button_" + title, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(420, 74));
        var bg = go.AddComponent<Image>();
        var b = go.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(action);

        var acc = Rect("Accent", go.transform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(3, 0), new Vector2(6, -14));
        var accImg = acc.AddComponent<Image>();
        accImg.color = new Color(Gold.r, Gold.g, Gold.b, 0f);
        accImg.raycastTarget = false;

        var label = Text(go.transform, title, 28, Cream, new Vector2(0, 11), new Vector2(400, 36), FontStyles.Bold);
        Text(go.transform, sub, 17, Muted, new Vector2(0, -18), new Vector2(400, 24));

        var fx = go.AddComponent<MenuButtonFX>();
        fx.accent = accImg; fx.label = label; fx.background = bg;
    }

    static Slider SliderRow(Transform parent, string title, Vector2 pos, out TMP_Text value)
    {
        var t = Text(parent, title, 24, Cream, pos + new Vector2(-150, 0), new Vector2(300, 40));
        t.alignment = TextAlignmentOptions.MidlineLeft;
        var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos + new Vector2(130, 0);
        rt.sizeDelta = new Vector2(240, 22);
        StyleSlider(go);
        value = Text(parent, "", 22, Gold, pos + new Vector2(300, 0), new Vector2(80, 40), FontStyles.Bold);
        return go.GetComponent<Slider>();
    }

    public static void StyleSlider(GameObject go)
    {
        foreach (Image img in go.GetComponentsInChildren<Image>(true))
        {
            if (img.name == "Background") img.color = new Color(0.25f, 0.21f, 0.17f);
            else if (img.name == "Fill") img.color = Gold;
            else if (img.name == "Handle") img.color = Cream;
        }
    }

    static void QualityRow(Transform parent, Vector2 pos, SettingsPanel panel)
    {
        var t = Text(parent, Loc.T("Графика", "Graphics"), 24, Cream, pos + new Vector2(-150, 0), new Vector2(300, 40));
        t.alignment = TextAlignmentOptions.MidlineLeft;
        panel.qualityPrev = SmallButton(parent, "<", pos + new Vector2(30, 0));
        panel.qualityValue = Text(parent, "", 22, Gold, pos + new Vector2(130, 0), new Vector2(160, 40), FontStyles.Bold);
        panel.qualityNext = SmallButton(parent, ">", pos + new Vector2(230, 0));
    }

    static void LanguageRow(Transform parent, Vector2 pos, SettingsPanel panel)
    {
        var t = Text(parent, Loc.T("Тил  ·  Язык", "Тил  ·  Language"), 24, Cream, pos + new Vector2(-150, 0), new Vector2(300, 40));
        t.alignment = TextAlignmentOptions.MidlineLeft;
        panel.languagePrev = SmallButton(parent, "<", pos + new Vector2(30, 0));
        panel.languageValue = Text(parent, Loc.LanguageName, 22, Gold, pos + new Vector2(130, 0), new Vector2(160, 40), FontStyles.Bold);
        panel.languageNext = SmallButton(parent, ">", pos + new Vector2(230, 0));
    }

    static Button SmallButton(Transform parent, string text, Vector2 pos)
    {
        var go = Rect("Btn" + text, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(46, 46));
        go.AddComponent<Image>().color = new Color(0.24f, 0.17f, 0.11f);
        var b = go.AddComponent<Button>();
        Text(go.transform, text, 28, Cream, Vector2.zero, new Vector2(46, 46), FontStyles.Bold);
        return b;
    }
}
