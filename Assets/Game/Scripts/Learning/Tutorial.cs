using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Үйрөнүү — короткое обучение при первом входе в село. Карточка сверху экрана подсказывает по одному действию
/// и сама переходит дальше, когда игрок его выполнил: ходьба → камера → бег/прыжок → разговор (E) →
/// список заданий (K) → словарь (J) → капчык (I) → что такое XP и базар. В конце +20 XP.
/// Обучение можно пропустить кнопкой. Шаги уходят в аналитику (tutorial_step / tutorial_done).
/// </summary>
public class Tutorial : MonoBehaviour
{
    public static Tutorial Instance { get; private set; }
    public static bool Done => SaveData.Get("tut_done") > 0;
    public const int RewardXp = 20;

    struct Step { public string id, ru, en; }

    static readonly Step[] Steps =
    {
        new Step { id = "move", ru = "Ходи клавишами <b>W A S D</b> (или стрелками).", en = "Walk with <b>W A S D</b> (or the arrow keys)." },
        new Step { id = "camera", ru = "Осмотрись: зажми <b>правую кнопку мыши</b> и веди мышью. Колесо — приблизить/отдалить.",
                   en = "Look around: hold the <b>right mouse button</b> and move the mouse. Wheel — zoom." },
        new Step { id = "run", ru = "<b>Shift</b> — бег, <b>Space</b> — прыжок. Попробуй!", en = "<b>Shift</b> — run, <b>Space</b> — jump. Try it!" },
        new Step { id = "talk", ru = "Подойди к <b>Айбеку</b> — он рядом — и нажми <b>E</b>, чтобы поговорить. <b>E</b> работает со всеми жителями и предметами.",
                   en = "Walk up to <b>Aibek</b> — he is nearby — and press <b>E</b> to talk. <b>E</b> works with all villagers and objects." },
        new Step { id = "quests", ru = "Задания — слева сверху. Клавиша <b>K</b> сворачивает и разворачивает список. Нажми <b>K</b>.",
                   en = "Quests are in the top left. <b>K</b> collapses and expands the list. Press <b>K</b>." },
        new Step { id = "dictionary", ru = "<b>J</b> — Сөздүк, словарь. Сюда попадают кыргызские слова, которые ты встречаешь (+5 XP за каждое). Открой его.",
                   en = "<b>J</b> — Sözdük, the dictionary. Kyrgyz words you meet go here (+5 XP each). Open it." },
        new Step { id = "inventory", ru = "<b>I</b> — Капчык: еда, рыба и находки. Открой его.", en = "<b>I</b> — Kapchyk, your bag: food, fish and keepsakes. Open it." },
        new Step { id = "xp", ru = "<b>XP</b> (справа сверху) дают за задания, слова и мини-игры. Трать его на базаре у Асана. <b>H</b> — подсказка, куда идти, <b>Esc</b> — пауза и настройки.",
                   en = "<b>XP</b> (top right) comes from quests, words and mini-games. Spend it at Asan's bazaar. <b>H</b> — a hint where to go, <b>Esc</b> — pause and settings." },
    };

    int step;
    float started, mouseAcc, runTime;
    Vector3 startPos;
    Transform player;
    GameObject canvas, card;
    TMP_Text title, text;
    Image fill;
    GameObject okButton;

    public static void Begin()
    {
        if (Instance != null) return;
        new GameObject("Tutorial").AddComponent<Tutorial>();
    }

    void Awake()
    {
        Instance = this;
        started = Time.unscaledTime;
        var pm = FindAnyObjectByType<PlayerMovement>();
        player = pm != null ? pm.transform : null;
        if (player != null) startPos = player.position;
        Build();
        Show(0);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Build()
    {
        canvas = MurasUI.Canvas("TutorialCanvas", transform, 440);
        card = MurasUI.Rect("Card", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -175), new Vector2(860, 150));
        card.AddComponent<Image>().color = new Color(0.07f, 0.055f, 0.05f, 0.9f);
        var strip = MurasUI.Rect("Strip", card.transform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(3, 0), new Vector2(6, 0));
        strip.AddComponent<Image>().color = MurasUI.Gold;
        var c = card.transform;
        title = MurasUI.Text(c, "", 20, MurasUI.Gold, new Vector2(-110, 50), new Vector2(600, 30), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        text = MurasUI.Text(c, "", 22, MurasUI.Cream, new Vector2(-60, -8), new Vector2(700, 80), FontStyles.Normal, TextAlignmentOptions.TopLeft);
        text.enableAutoSizing = true; text.fontSizeMin = 15; text.fontSizeMax = 22;
        var track = MurasUI.Box("Track", c, new Vector2(-60, -62), new Vector2(700, 5), new Color(0.22f, 0.17f, 0.13f));
        var f = MurasUI.Rect("Fill", track.transform, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);
        ((RectTransform)f.transform).pivot = new Vector2(0, 0.5f);
        fill = f.AddComponent<Image>(); fill.color = MurasUI.Gold;
        var skip = MurasUI.Button(c, Loc.T("Пропустить", "Skip"), new Vector2(345, 45), new Vector2(140, 40), () => Finish(true), 16);
        skip.GetComponent<Image>().color = new Color(0.30f, 0.21f, 0.14f);
        okButton = MurasUI.Button(c, Loc.T("Понятно", "Got it"), new Vector2(345, -30), new Vector2(140, 50), () => Complete(), 20).gameObject;
    }

    void Show(int i)
    {
        step = i;
        mouseAcc = 0f; runTime = 0f;
        title.text = "ҮЙРӨНҮҮ  ·  " + Loc.T("Обучение ", "Tutorial ") + (i + 1) + "/" + Steps.Length;
        text.text = Loc.T(Steps[i].ru, Steps[i].en);
        ((RectTransform)fill.transform).anchorMax = new Vector2(i / (float)Steps.Length, 1);
        okButton.SetActive(Steps[i].id == "xp");
        MurasUI.Play("tile_turn", 0.45f, 1.15f);
    }

    void Update()
    {
        bool hidden = PauseMenu.IsPaused || Modal.IsOpen || QuizWindow.Current != null;
        string id = Steps[step].id;

        // окна, которые нужно открыть по заданию шага, засчитываем в момент открытия (карточка при этом прячется)
        if (Modal.IsOpen && Modal.Current != null)
        {
            string n = Modal.Current.GetType().Name;
            if ((id == "dictionary" && n == "JournalWindow") || (id == "inventory" && n == "InventoryWindow")) Complete();
        }
        card.SetActive(!hidden);
        if (hidden) return;

        switch (id)
        {
            case "move":
                if (player != null && (player.position - startPos).sqrMagnitude > 9f) Complete();
                break;
            case "camera":
                if (Input.GetMouseButton(1)) mouseAcc += Mathf.Abs(Input.GetAxis("Mouse X")) + Mathf.Abs(Input.GetAxis("Mouse Y"));
                if (Mathf.Abs(Input.GetAxis("Mouse ScrollWheel")) > 0.01f) mouseAcc += 3f;
                if (mouseAcc > 12f) Complete();
                break;
            case "run":
                bool moving = Mathf.Abs(Input.GetAxisRaw("Horizontal")) + Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f;
                if (moving && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))) runTime += Time.unscaledDeltaTime;
                if (runTime > 0.6f || Input.GetKeyDown(KeyCode.Space)) Complete();
                break;
            case "talk":
                if ((SpeechPanel.Instance != null && SpeechPanel.Instance.IsShowing) ||
                    (GameManager.Instance != null && GameManager.Instance.IsQuestCompleted("greeting"))) Complete();
                break;
            case "quests":
                if (Input.GetKeyDown(KeyCode.K)) Complete();
                break;
            case "xp":
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Complete();
                break;
        }
    }

    void Complete()
    {
        MurasCloud.Track("tutorial_step", CloudJson.Object("step", Steps[step].id, "n", step + 1));
        if (step + 1 < Steps.Length) Show(step + 1);
        else Finish(false);
    }

    void Finish(bool skipped)
    {
        SaveData.Set("tut_done", skipped ? 2 : 1);
        MurasCloud.Track("tutorial_done", CloudJson.Object("skipped", skipped, "seconds", Mathf.RoundToInt(Time.unscaledTime - started), "step", step + 1));
        if (!skipped)
        {
            if (GameManager.Instance != null) GameManager.Instance.AddXP(RewardXp);
            MurasToast.Push(Loc.T("Обучение пройдено", "Tutorial complete") + "  +" + RewardXp + " XP",
                            Loc.T("Теперь ты знаешь основы. Ийгилик — удачи!", "Now you know the basics. Iygilik — good luck!"), MurasUI.Gold, "achievement");
        }
        Destroy(gameObject);
    }
}
