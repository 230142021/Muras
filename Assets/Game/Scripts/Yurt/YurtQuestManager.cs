using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Квест «Боз үй»: аксакал учит собирать юрту.
/// Порядок: кереге → түндүк → уук → кийиз (войлок), затем опрос.
/// Подсказки (стрелка и полупрозрачная деталь на площадке) появляются только по кнопке «Подсказка» / H.
/// </summary>
public class YurtQuestManager : MonoBehaviour, IQuestHintSource
{
    [Header("Characters")]
    public Transform player;
    public Transform aksakal;

    [Header("Construction")]
    public Transform buildPoint;

    [Header("Pickup Objects")]
    public GameObject pickupKerege;
    public GameObject pickupTunduk;
    public GameObject pickupUuk;
    public GameObject pickupKiyiz;

    [Header("Built Objects")]
    public GameObject builtKerege;
    public GameObject builtTunduk;
    public GameObject builtUuk;
    public GameObject builtKiyiz;

    [Header("UI")]
    public TMP_Text questText;
    public TMP_Text storyText;
    public TMP_Text interactionText;
    public TMP_Text carryText;

    [Header("Quiz")]
    public GameObject quizPanel;
    public TMP_Text questionText;
    public TMP_Text feedbackText;
    public Button[] answerButtons;

    [Header("Hints")]
    [Tooltip("Устарело: стрелками теперь управляет HintDirector")]
    public QuestArrow arrow;
    [Tooltip("Полупрозрачный материал: показывает, где встанет деталь")]
    public Material ghostMaterial;

    [Header("Settings")]
    public float interactionDistance = 3f;
    [Tooltip("С какого расстояния (м) можно подобрать деталь юрты")]
    public float pickupRadius = 1.6f;
    [Tooltip("С какого расстояния от центра площадки можно установить деталь")]
    public float buildDistance = 6.5f;
    public int rewardXP = 300;

    // 0 = поговорить с аксакалом
    // 1..4 = найти и установить деталь (кереге, түндүк, уук, кийиз)
    // 5 = вернуться к аксакалу (опрос)
    // 6 = квест выполнен
    private const int PartCount = 4;
    private const int ReturnStage = PartCount + 1;
    private const int DoneStage = PartCount + 2;

    private const string QuestId = "yurt";

    private int stage = 0;
    private bool carrying = false;
    private bool quizActive = false;
    private bool completed = false;
    private int questionIndex = 0;
    private GameObject[] ghosts;

    private static readonly string[] PartNames = { "Кереге", "Түндүк", "Уук", "Кийиз" };

    private static readonly string[] PartStoriesEn =
    {
        "Аксакал: Бул — кереге.\nThese are the lattice walls of the yurt — they fold up for moving. Now find the түндүк.",
        "Аксакал: Бул — түндүк.\nThe crown ring of the yurt — a symbol of home and family unity. Now find the уук.",
        "Аксакал: Бул — уук.\nThe roof poles join the кереге and the түндүк. The frame is ready! Now cover it with felt — find the кийиз.",
        "Аксакал: Бул — кийиз.\nFelt made of sheep wool keeps warmth in winter and coolness in summer. Боз үй даяр! Come back to me."
    };

    private static readonly string[] QuestionsEn =
    {
        "What are the lattice walls of the yurt called?",
        "What is the crown ring of the yurt called?",
        "What are the roof poles called?",
        "What is the felt that covers the yurt called?"
    };

    private static readonly string[] PartStories =
    {
        "Аксакал: Бул — кереге.\nЭто решётчатые стены юрты, их можно сложить и перевезти. Теперь найди түндүк.",
        "Аксакал: Бул — түндүк.\nВерхнее кольцо юрты — символ дома и единства семьи. Теперь найди уук.",
        "Аксакал: Бул — уук.\nЖерди соединяют кереге и түндүк. Каркас готов! Осталось покрыть его войлоком — найди кийиз.",
        "Аксакал: Бул — кийиз.\nВойлок из овечьей шерсти хранит тепло зимой и прохладу летом. Боз үй даяр! Возвращайся ко мне."
    };

    private readonly string[] questions =
    {
        "Как называются решётчатые стены юрты?",
        "Как называется верхнее кольцо юрты?",
        "Как называются жерди крыши?",
        "Как называется войлок, которым покрывают юрту?"
    };

    private readonly string[][] answers =
    {
        new[] { "Кереге", "Түндүк", "Уук" },
        new[] { "Уук", "Кереге", "Түндүк" },
        new[] { "Түндүк", "Уук", "Кереге" },
        new[] { "Шырдак", "Кийиз", "Кереге" }
    };

    private readonly int[] correctAnswers = { 0, 2, 1, 1 };

    private GameObject[] Pickups => new[] { pickupKerege, pickupTunduk, pickupUuk, pickupKiyiz };
    private GameObject[] Builts => new[] { builtKerege, builtTunduk, builtUuk, builtKiyiz };

    void Start()
    {
        foreach (GameObject built in Builts)
            if (built != null) built.SetActive(false);

        if (quizPanel != null)
            quizPanel.SetActive(false);

        // Войлок должен останавливать камеру и игрока (вход — только через дверь)
        if (builtKiyiz != null)
            foreach (MeshFilter f in builtKiyiz.GetComponentsInChildren<MeshFilter>(true))
                if (f.sharedMesh != null && !f.TryGetComponent(out Collider _))
                    f.gameObject.AddComponent<MeshCollider>().sharedMesh = f.sharedMesh;

        ghosts = Builts.Select(MakeGhost).ToArray();

        SetStory("");

        if (feedbackText != null)
            feedbackText.text = "";

        if (answerButtons != null)
        {
            for (int i = 0; i < answerButtons.Length; i++)
            {
                if (answerButtons[i] == null)
                    continue;

                int index = i;
                answerButtons[i].onClick.AddListener(() => CheckAnswer(index));
            }
        }

        if (GameManager.Instance != null &&
            GameManager.Instance.IsQuestCompleted(QuestId))
            RestoreCompletedState();

        UpdateUI();
    }

    // Квест уже пройден раньше: юрта стоит, детали не лежат, XP повторно не даётся.
    void RestoreCompletedState()
    {
        completed = true;
        stage = DoneStage;

        foreach (GameObject built in Builts)
            if (built != null) built.SetActive(true);

        foreach (GameObject pickup in Pickups)
            if (pickup != null) pickup.SetActive(false);

        if (interactionText != null)
            interactionText.text = "";
    }

    void Update()
    {
        UpdateHints();

        // Ответы в опросе клавишами 1, 2, 3
        if (quizActive && answerButtons != null)
        {
            for (int i = 0; i < answerButtons.Length && i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                {
                    CheckAnswer(i);
                    break;
                }
        }

        if (completed || quizActive || player == null)
            return;

        GameObject currentPickup = GetCurrentPickup();

        bool nearAksakal = aksakal != null && IsNear(aksakal.position, interactionDistance);
        bool nearBuildPoint = buildPoint != null && IsNear(buildPoint.position, buildDistance);
        bool nearPickup = currentPickup != null &&
                          currentPickup.activeInHierarchy &&
                          IsNear(currentPickup.transform.position, pickupRadius);

        bool building = stage >= 1 && stage <= PartCount;
        string hint = "";

        if (stage == 0 && nearAksakal)
            hint = Key(Loc.T("поговорить с аксакалом", "talk to the aksakal"));
        else if (building && !carrying && nearPickup)
            hint = Key(Loc.T("подобрать: ", "pick up: ") + GetPartName());
        else if (building && carrying && nearBuildPoint)
            hint = Key(stage == PartCount ? Loc.T("покрыть каркас войлоком", "cover the frame with felt") : Loc.T("установить: ", "place: ") + GetPartName());
        else if (stage == ReturnStage && nearAksakal)
            hint = Key(Loc.T("пройти опрос аксакала", "take the aksakal's quiz"));

        if (interactionText != null)
            interactionText.text = hint;
        else if (!string.IsNullOrEmpty(hint))
            InteractionHint.Request(hint);

        if (!Input.GetKeyDown(KeyCode.E))
            return;

        if (stage == 0 && nearAksakal)
        {
            StartQuest();
            return;
        }

        if (building)
        {
            if (!carrying && nearPickup)
            {
                PickupPart(currentPickup);
                return;
            }

            if (carrying && nearBuildPoint)
            {
                InstallPart();
                return;
            }
        }

        if (stage == ReturnStage && nearAksakal)
            StartQuiz();
    }

    static string Key(string action) => "<b><color=#E8B04A>E</color></b>  —  " + action;

    void OnEnable() { QuestHints.Register(this); Loc.Changed += UpdateUI; }
    void OnDisable() { QuestHints.Unregister(this); Loc.Changed -= UpdateUI; }

    // Куда показывать стрелку (её рисует HintDirector, когда нажата «Подсказка»)
    public bool GetHintTarget(out Transform target, out float height)
    {
        target = null;
        height = 2.7f;

        if (completed || quizActive)
            return false;

        if (stage == 0 || stage == ReturnStage)
        {
            target = aksakal;
        }
        else if (stage >= 1 && stage <= PartCount)
        {
            if (carrying)
            {
                target = buildPoint;
                height = 6.2f;
            }
            else
            {
                GameObject pickup = GetCurrentPickup();
                if (pickup != null && pickup.activeInHierarchy)
                {
                    target = pickup.transform;
                    height = 2.4f;
                }
            }
        }
        return target != null;
    }

    // Полупрозрачная «тень» детали на площадке — только пока активна подсказка
    void UpdateHints()
    {
        bool hintOn = HintButton.IsActive;

        if (ghosts != null)
            for (int i = 0; i < ghosts.Length; i++)
                if (ghosts[i] != null)
                {
                    bool show = hintOn && !completed && carrying && stage == i + 1;
                    if (ghosts[i].activeSelf != show) ghosts[i].SetActive(show);
                }
    }

    GameObject MakeGhost(GameObject source)
    {
        if (source == null || ghostMaterial == null)
            return null;

        GameObject ghost = Instantiate(source, source.transform.parent);
        ghost.name = "Ghost_" + source.name;

        foreach (Collider c in ghost.GetComponentsInChildren<Collider>(true))
            Destroy(c);

        foreach (Renderer r in ghost.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterials = Enumerable.Repeat(ghostMaterial, r.sharedMaterials.Length).ToArray();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        ghost.SetActive(false);
        return ghost;
    }

    bool IsNear(Vector3 target, float distance)
    {
        Vector3 a = player.position;
        Vector3 b = target;
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b) <= distance;
    }

    GameObject GetCurrentPickup() =>
        stage >= 1 && stage <= PartCount ? Pickups[stage - 1] : null;

    string GetPartName() =>
        stage >= 1 && stage <= PartCount ? PartNames[stage - 1] : "";

    void StartQuest()
    {
        stage = 1;

        SetStory(
            "Аксакал: Кош келдиң!\n" + Loc.T(
            "Добро пожаловать! Боз үй — традиционное переносное жилище кыргызского народа. " +
            "Начнём строительство с кереге. Если не найдёшь деталь — нажми «Подсказка» (H).",
            "Welcome! A боз үй is the traditional portable home of the Kyrgyz people. " +
            "Let's start building with the кереге. If you can't find a part, press \"Hint\" (H).")
        );

        UpdateUI();
    }

    void PickupPart(GameObject item)
    {
        if (item == null || carrying)
            return;

        carrying = true;
        item.SetActive(false);
        GameAudio.Pickup();

        SetStory(Loc.T("Ты подобрал: ", "You picked up: ") + GetPartName() + Loc.T(". Отнеси на строительную площадку.", ". Carry it to the building site."));

        UpdateUI();
    }

    void InstallPart()
    {
        if (!carrying)
            return;

        carrying = false;

        int index = stage - 1;
        GameAudio.Correct();
        GameObject built = Builts[index];
        if (built != null)
            built.SetActive(true);

        SetStory(Loc.IsEnglish ? PartStoriesEn[index] : PartStories[index]);
        stage++;

        UpdateUI();
    }

    void StartQuiz()
    {
        if (quizPanel == null || answerButtons == null || answerButtons.Length < 3)
        {
            Debug.LogError("Quiz UI is not configured!");
            return;
        }

        quizActive = true;
        questionIndex = 0;

        if (storyText != null)
            storyText.gameObject.SetActive(false);

        if (interactionText != null)
            interactionText.text = "";

        quizPanel.SetActive(true);

        ShowQuestion();
    }

    void ShowQuestion()
    {
        if (questionText != null)
            questionText.text = Loc.IsEnglish ? QuestionsEn[questionIndex] : questions[questionIndex];

        if (feedbackText != null)
            feedbackText.text = Loc.T("Вопрос ", "Question ") + (questionIndex + 1) + Loc.T(" из ", " of ") + questions.Length;

        for (int i = 0; i < 3; i++)
        {
            Button button = answerButtons[i];
            if (button == null)
                continue;

            button.gameObject.SetActive(true);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = answers[questionIndex][i];

            button.interactable = true;
        }
    }

    public void CheckAnswer(int selectedIndex)
    {
        if (!quizActive || completed)
            return;

        if (selectedIndex != correctAnswers[questionIndex])
        {
            if (feedbackText != null)
                feedbackText.text = "<color=#ED665C>" + Loc.T("Туура эмес! Попробуй ещё раз.", "Туура эмес! Try again.") + "</color>";
            GameAudio.Wrong();
            return;
        }

        GameAudio.Correct();
        questionIndex++;

        if (questionIndex >= questions.Length)
            CompleteQuest();
        else
            ShowQuestion();
    }

    void CompleteQuest()
    {
        completed = true;
        quizActive = false;
        stage = DoneStage;

        if (quizPanel != null)
            quizPanel.SetActive(false);

        if (storyText != null)
            storyText.gameObject.SetActive(true);

        if (GameManager.Instance != null)
            GameManager.Instance.CompleteQuest(QuestId, rewardXP);
        else
            Debug.LogWarning("GameManager is missing!");

        SetStory(
            "Аксакал: Азаматсың! Жарайсың!\n" +
            Loc.T("Ты построил юрту и прошёл опрос! +", "You built the yurt and passed the quiz! +") + rewardXP + " XP"
        );

        UpdateUI();
    }

    void UpdateUI()
    {
        if (questText != null)
        {
            const string title = "<b>Боз үй</b>";
            string done = Mathf.Clamp(stage - 1, 0, PartCount) + "/" + PartCount;

            if (stage == 0)
                questText.text = title + "\n<size=85%><color=#C9BFA6>" + Loc.T("Поговори с аксакалом у юрты", "Talk to the aksakal by the yurt") + "</color></size>";
            else if (stage <= PartCount)
                questText.text = title + "  <color=#E8B04A>" + done + "</color>\n<size=85%><color=#C9BFA6>" +
                                 (carrying ? Loc.T("Отнеси на площадку: ", "Carry to the site: ") : Loc.T("Найди: ", "Find: ")) + GetPartName() + "</color></size>";
            else if (stage == ReturnStage)
                questText.text = title + "  <color=#5FC873>" + done + "</color>\n<size=85%><color=#C9BFA6>" + Loc.T("Вернись к аксакалу", "Return to the aksakal") + "</color></size>";
            else
                questText.text = title + "  <color=#5FC873>" + done + "</color>\n<size=85%><color=#C9BFA6>" + Loc.T("Квест выполнен", "Quest complete") + "</color></size>";
        }

        if (carryText != null)
        {
            if (carrying)
                carryText.text = Loc.T("В руках: <b>", "Carrying: <b>") + GetPartName() + "</b>";
            else
                carryText.text = stage >= 1 && stage <= PartCount ? Loc.T("В руках: ничего", "Carrying: nothing") : "";
        }

        if (completed && interactionText != null)
            interactionText.text = "";
    }

    void SetStory(string message)
    {
        if (storyText != null)
            storyText.text = message;

        Debug.Log(message);
    }
}
