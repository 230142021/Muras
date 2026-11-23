using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Главное меню MURAS: Продолжить / Новая игра / Как играть / Настройки / Об игре / Выход.
/// Плавное появление, экран загрузки с советами о кыргызской культуре, Esc закрывает окна.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    public string gameScene = "Village";

    [Header("Кнопки")]
    public Button continueButton;
    public TMP_Text continueInfo;

    [Header("Окна")]
    public GameObject settingsPanel;
    public GameObject howToPanel;
    public GameObject aboutPanel;
    public GameObject confirmPanel;
    public TMP_Text confirmText;

    [Header("Переходы")]
    public CanvasGroup fade;
    public CanvasGroup loading;
    public Image loadingBar;
    public TMP_Text loadingTip;

    [Header("Звуки")]
    public AudioClip hoverClip;
    public AudioClip clickClip;

    private bool busy;

    private static readonly string[] TipsEn =
    {
        "Түндүк is the ring at the top of the yurt. It is on the flag of Kyrgyzstan.",
        "Nomads could put up a боз үй (yurt) as a family in less than an hour.",
        "Чүкө are sheep knucklebones. Each has four sides: алчы, таа, чик and бүк.",
        "Боорсок is fried for every celebration — its smell means guests are expected.",
        "Кийиз is felt made of sheep wool. It protects the yurt from cold and wind.",
        "Аксакал means \"white beard\" — a respected elder of the clan.",
        "\"Салам алейкум!\" — this is how you greet elders in a Kyrgyz village."
    };

    private static readonly string[] Tips =
    {
        "Түндүк — круг на вершине юрты. Он есть на флаге Кыргызстана.",
        "Боз үй (юрту) кочевники собирали всей семьёй меньше чем за час.",
        "Чүкө — бараньи косточки. У каждой четыре стороны: алчы, таа, чик и бүк.",
        "Боорсок пекут к каждому празднику — его запах означает, что гостей ждут.",
        "Кийиз — войлок из овечьей шерсти. Он защищает юрту от холода и ветра.",
        "Аксакал — «белая борода», уважаемый старейшина рода.",
        "«Салам алейкум!» — так здороваются со старшими в кыргызском селе."
    };

    void Start()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        MenuSound.hoverClip = hoverClip;
        MenuSound.clickClip = clickClip;

        CloseAll();
        if (loading != null) { loading.alpha = 0f; loading.gameObject.SetActive(false); }
        RefreshContinue();
        Loc.Changed += RefreshContinue;

        if (fade != null) StartCoroutine(Fade(fade, 1f, 0f, 0.8f, true));
    }

    void OnDestroy() => Loc.Changed -= RefreshContinue;

    void Update()
    {
        if (busy) return;
        if (Input.GetKeyDown(KeyCode.Escape)) CloseAll();
    }

    void RefreshContinue()
    {
        bool has = GameManager.HasSave;
        if (continueButton != null) continueButton.interactable = has;
        if (continueInfo != null)
            continueInfo.text = has
                ? Loc.T("Продолжить  ·  квесты ", "Continue  ·  quests ") + GameManager.SavedQuestCount + "/" + GameManager.TotalQuests + "  ·  " + GameManager.SavedXP + " XP"
                : Loc.T("Продолжить  ·  сохранений пока нет", "Continue  ·  no saved game yet");
    }

    // ---------------------------------------------------------------- кнопки

    /// <summary>«Улантуу / Продолжить» — загружает село с сохранённым прогрессом.</summary>
    public void PlayGame()
    {
        if (busy) return;
        StartCoroutine(LoadGame());
    }

    /// <summary>«Жаңы оюн / Новая игра» — если есть прогресс, сначала спрашивает.</summary>
    public void NewGame()
    {
        if (busy) return;
        if (GameManager.HasSave && confirmPanel != null)
        {
            if (confirmText != null)
                confirmText.text = Loc.T("Начать заново?\n<size=80%><color=#C9BFA6>Прогресс будет удалён: квесты ",
                                         "Start over?\n<size=80%><color=#C9BFA6>Your progress will be deleted: quests ") +
                                   GameManager.SavedQuestCount + "/" + GameManager.TotalQuests + ", " + GameManager.SavedXP + " XP.</color></size>";
            Open(confirmPanel);
        }
        else ConfirmNewGame();
    }

    public void ConfirmNewGame()
    {
        if (busy) return;
        GameManager.ClearSavedProgress();
        CloseAll();
        StartCoroutine(LoadGame());
    }

    public void OpenSettings() => Open(settingsPanel);
    public void OpenHowTo() => Open(howToPanel);
    public void OpenAbout() => Open(aboutPanel);

    public void CloseAll()
    {
        foreach (GameObject p in new[] { settingsPanel, howToPanel, aboutPanel, confirmPanel })
            if (p != null) p.SetActive(false);
    }

    public void ExitGame()
    {
        if (busy) return;
        GameSettings.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void Open(GameObject panel)
    {
        CloseAll();
        if (panel == null) return;
        panel.SetActive(true);
        var g = panel.GetComponent<CanvasGroup>();
        if (g != null) StartCoroutine(Fade(g, 0f, 1f, 0.18f, false));
    }

    // ---------------------------------------------------------------- загрузка

    IEnumerator LoadGame()
    {
        busy = true;
        GameManager.LaunchedFromMenu = true;
        GameSettings.Save();
        MenuMusic.FadeOutAll(1.2f);

        if (loading != null)
        {
            loading.gameObject.SetActive(true);
            if (loadingTip != null) { int i = Random.Range(0, Tips.Length); loadingTip.text = Loc.IsEnglish ? TipsEn[i] : Tips[i]; }
            if (loadingBar != null) loadingBar.fillAmount = 0f;
            yield return Fade(loading, 0f, 1f, 0.45f, false);
        }

        AsyncOperation op = SceneManager.LoadSceneAsync(gameScene);
        op.allowSceneActivation = false;
        float shown = 0f;
        float minTime = Time.unscaledTime + 1.2f;   // чтобы успеть прочитать совет
        while (op.progress < 0.9f || Time.unscaledTime < minTime || shown < 0.99f)
        {
            float real = Mathf.Clamp01(op.progress / 0.9f);
            shown = Mathf.MoveTowards(shown, real, Time.unscaledDeltaTime * 1.2f);
            if (loadingBar != null) loadingBar.fillAmount = shown;
            yield return null;
        }
        op.allowSceneActivation = true;
    }

    IEnumerator Fade(CanvasGroup g, float from, float to, float time, bool disableAtEnd)
    {
        g.gameObject.SetActive(true);
        g.blocksRaycasts = true;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            g.alpha = Mathf.Lerp(from, to, t / time);
            yield return null;
        }
        g.alpha = to;
        if (disableAtEnd && to <= 0f) g.gameObject.SetActive(false);
    }
}
