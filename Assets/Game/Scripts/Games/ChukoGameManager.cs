using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Квест «Чүкө атуу» — как в настоящей игре: чүкө (бараньи косточки) стоят в ряд в круге,
/// игрок встаёт у черты и бросает саку (тяжёлую биту-косточку), чтобы выбить чүкө за круг.
/// Зажми ЛКМ — бегунок силы ходит туда-сюда, отпусти в нужный момент. Направление — куда смотрит камера.
/// Выбей нужное число чүкө, пока не кончились саки, — затем короткий опрос от детей.
/// </summary>
public class ChukoGameManager : MonoBehaviour, IQuestHintSource
{
    [Header("Characters")]
    public Transform player;
    public Transform childNPC;
    public Transform playPoint;
    public Camera gameCamera;

    [Header("Чүкө в круге")]
    public ChukoTarget[] targets;
    public Transform ringCenter;
    public float ringRadius = 1.6f;

    [Header("Бросок")]
    [Tooltip("Объект-образец саки (выключен в сцене) с Rigidbody")]
    public GameObject sakaTemplate;
    public int throwsPerRound = 6;
    public int needKnockOut = 3;
    public float minThrowSpeed = 5f;
    public float maxThrowSpeed = 12f;
    [Tooltip("Как быстро бегает бегунок силы")]
    public float powerSpeed = 1.3f;
    public float throwZone = 2.5f;
    public GameObject powerPanel;
    public Image powerFill;
    public LineRenderer aimLine;

    [Header("Interface")]
    public GameObject storyBackground;
    public TMP_Text questText;
    public TMP_Text storyText;
    public TMP_Text scoreText;
    public TMP_Text interactionText;

    [Header("Quiz")]
    public GameObject quizPanel;
    public TMP_Text questionText;
    public TMP_Text feedbackText;
    public Button[] answerButtons;

    [Header("Settings")]
    public float interactionDistance = 3f;
    public float maxShotDistance = 100f;
    public int rewardXP = 250;

    private enum QuestState { TalkToChild, GoToPlayground, Playing, Quiz, Completed }

    private const string QuestId = "chuko";

    private QuestState state = QuestState.TalkToChild;
    private int hits;               // выбито чүкө в этом раунде
    private int throwsLeft;
    private bool charging;
    private float power;
    private bool sakaInFlight;
    private bool rewardGiven;
    private int questionIndex;

    private static readonly string[] QuestionsEn =
    {
        "What is the heavy knucklebone bat used to knock out the чүкө called?",
        "What are the four sides of a чүкө called?",
        "What are чүкө traditionally made of?"
    };
    private static readonly string[][] AnswersEn =
    {
        new[] { "Сака", "Алчы", "Түндүк" },
        new[] { "Кереге, уук, түндүк, кийиз", "Алчы, таа, чик, бүк", "Ун, суу, май, эт" },
        new[] { "Clay", "Iron", "Sheep knucklebones" }
    };

    private static readonly string[] Questions =
    {
        "Как называется тяжёлая косточка-бита, которой выбивают чүкө?",
        "Как называются четыре стороны чүкө?",
        "Из чего традиционно делают чүкө?"
    };
    private static readonly string[][] Answers =
    {
        new[] { "Сака", "Алчы", "Түндүк" },
        new[] { "Кереге, уук, түндүк, кийиз", "Алчы, таа, чик, бүк", "Ун, суу, май, эт" },
        new[] { "Из глины", "Из железа", "Из косточек барана" }
    };
    private static readonly int[] Correct = { 0, 1, 2 };

    void OnEnable() { QuestHints.Register(this); Loc.Changed += UpdateUI; }
    void OnDisable() { QuestHints.Unregister(this); Loc.Changed -= UpdateUI; }

    public bool GetHintTarget(out Transform target, out float height)
    {
        target = null;
        height = 2.4f;
        switch (state)
        {
            case QuestState.TalkToChild: target = childNPC; break;
            case QuestState.GoToPlayground: target = playPoint; break;
            case QuestState.Playing:
                target = IsAtThrowLine() ? ringCenter : playPoint;
                height = 1.6f;
                break;
        }
        return target != null;
    }

    public bool IsPlaying => state == QuestState.Playing;

    void Start()
    {
        if (gameCamera == null) gameCamera = Camera.main;
        if (quizPanel != null) quizPanel.SetActive(false);
        if (storyBackground != null) storyBackground.SetActive(false);
        if (powerPanel != null) powerPanel.SetActive(false);
        if (aimLine != null) aimLine.enabled = false;
        if (sakaTemplate != null) sakaTemplate.SetActive(false);

        if (answerButtons != null)
            for (int i = 0; i < answerButtons.Length; i++)
            {
                if (answerButtons[i] == null) continue;
                int index = i;
                answerButtons[i].onClick.AddListener(() => CheckAnswer(index));
            }

        ResetPieces(true);

        if (GameManager.Instance != null && GameManager.Instance.IsQuestCompleted(QuestId))
        {
            state = QuestState.Completed;
            rewardGiven = true;
        }

        UpdateUI();
    }

    void Update()
    {
        if (interactionText != null) interactionText.text = "";
        if (player == null) return;

        switch (state)
        {
            case QuestState.TalkToChild:
                if (IsNear(childNPC))
                {
                    ShowHint(Key(Loc.T("поговорить с ребёнком", "talk to the child")));
                    if (Input.GetKeyDown(KeyCode.E)) StartQuest();
                }
                break;

            case QuestState.GoToPlayground:
                if (IsNear(playPoint))
                {
                    ShowHint(Key(Loc.T("встать у черты и начать Чүкө атуу", "stand at the line and start Чүкө атуу")));
                    if (Input.GetKeyDown(KeyCode.E)) StartGame();
                }
                break;

            case QuestState.Playing:
                UpdatePlaying();
                break;

            case QuestState.Quiz:
                for (int i = 0; i < 3; i++)
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                    {
                        CheckAnswer(i);
                        break;
                    }
                break;
        }
    }

    // ------------------------------------------------------------- игра

    void StartQuest()
    {
        state = QuestState.GoToPlayground;
        SetStory("Бала: Салам! Кел, чүкө ойнойлу!\n" + Loc.T(
                 "Привет! Это чүкө — косточки барана, наша древняя игра. Подойди к черте у круга — научу бросать саку!",
                 "Hi! These are чүкө — sheep knucklebones, our ancient game. Come to the line by the circle — I'll teach you to throw the сака!"));
        UpdateUI();
    }

    void StartGame()
    {
        state = QuestState.Playing;
        throwsLeft = throwsPerRound;
        hits = 0;
        ResetPieces(false);
        SetStory(Loc.IsEnglish
            ? "Бала: Knock " + needKnockOut + " чүкө out of the rope circle! You have " + throwsPerRound + " throws.\n" +
              "Point the camera at the circle, hold LMB and release when the power bar is just right."
            : "Бала: Выбей " + needKnockOut + " чүкө за верёвку! У тебя " + throwsPerRound + " саки.\n" +
              "Смотри камерой на круг, зажми ЛКМ и отпусти, когда бегунок силы будет в нужном месте.");
        UpdateUI();
    }

    void UpdatePlaying()
    {
        CountKnockedOut();

        bool atLine = IsAtThrowLine();
        if (powerPanel != null) powerPanel.SetActive(atLine && !sakaInFlight);

        if (sakaInFlight) { SetAim(false); return; }

        if (!atLine)
        {
            charging = false;
            SetAim(false);
            ShowHint(Loc.T("Вернись к черте, чтобы бросать саку", "Go back to the line to throw the сака"));
            return;
        }

        ShowHint(Loc.T("<b><color=#E8B04A>ЛКМ</color></b> — зажми и отпусти: бросок саки   ·   саки: ", "<b><color=#E8B04A>LMB</color></b> — hold and release to throw   ·   throws: ") + throwsLeft);

        bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (Input.GetMouseButtonDown(0) && !overUi)
        {
            charging = true;
            power = 0f;
        }

        if (charging)
        {
            power = Mathf.PingPong(Time.time * powerSpeed, 1f);
            if (powerFill != null)
            {
                powerFill.fillAmount = power;
                powerFill.color = Color.Lerp(new Color(0.37f, 0.78f, 0.45f), new Color(0.93f, 0.40f, 0.36f), power);
            }
            DrawAim(ThrowVelocity(power));

            if (Input.GetMouseButtonUp(0))
            {
                charging = false;
                SetAim(false);
                StartCoroutine(Throw(ThrowVelocity(power)));
            }
        }
        else
        {
            if (powerFill != null) powerFill.fillAmount = 0f;
            SetAim(false);
        }
    }

    Vector3 ThrowOrigin() => player.position + Vector3.up * 0.4f + AimDirection() * 0.6f;

    Vector3 AimDirection()
    {
        Vector3 f = gameCamera != null ? gameCamera.transform.forward : player.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 0.01f && ringCenter != null) f = ringCenter.position - player.position;
        f.y = 0f;
        return f.normalized;
    }

    Vector3 ThrowVelocity(float p)
    {
        float speed = Mathf.Lerp(minThrowSpeed, maxThrowSpeed, p);
        return AimDirection() * speed + Vector3.up * Mathf.Lerp(1.2f, 2.6f, p);
    }

    IEnumerator Throw(Vector3 velocity)
    {
        if (sakaTemplate == null) yield break;

        throwsLeft--;
        sakaInFlight = true;
        UpdateUI();

        GameObject saka = Instantiate(sakaTemplate, ThrowOrigin(), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        saka.SetActive(true);
        foreach (Collider sc in saka.GetComponentsInChildren<Collider>())
            foreach (Collider pc in player.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(sc, pc);

        var rb = saka.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = velocity;
            rb.angularVelocity = Random.insideUnitSphere * 8f;
        }

        float t = 0f;
        yield return new WaitForSeconds(0.4f);
        while (t < 4f && rb != null && rb.linearVelocity.magnitude > 0.15f)
        {
            t += Time.deltaTime;
            CountKnockedOut();
            yield return null;
        }
        yield return new WaitForSeconds(0.6f);   // дать чүкө докатиться
        CountKnockedOut();

        Destroy(saka);
        sakaInFlight = false;

        if (hits >= needKnockOut)
        {
            WinRound();
        }
        else if (throwsLeft <= 0)
        {
            SetStory("Бала: Эч нерсе эмес! " + Loc.T("Ничего страшного — давай ещё раз!\nПодойди к черте и нажми E, чтобы начать заново.",
                     "No worries — let's try again!\nGo to the line and press E to start over."));
            state = QuestState.GoToPlayground;
            if (powerPanel != null) powerPanel.SetActive(false);
            GameAudio.Wrong();
        }
        UpdateUI();
    }

    void CountKnockedOut()
    {
        if (targets == null || ringCenter == null) return;
        foreach (ChukoTarget t in targets)
        {
            if (t == null || t.Counted) continue;
            Vector3 d = t.transform.position - ringCenter.position;
            d.y = 0f;
            if (d.magnitude > ringRadius)
            {
                t.MarkCounted();
                hits++;
                GameAudio.Pickup();
                if (hits < needKnockOut)
                    SetStory(hits == 1 ? Loc.T("Кыз: Жарайсың! Молодец, первый чүкө!", "Кыз: Жарайсың! Well done, the first чүкө!") : Loc.T("Бала: Азамат! Ещё один!", "Бала: Азамат! One more!"));
                UpdateUI();
            }
        }
    }

    void WinRound()
    {
        state = QuestState.Quiz;
        if (powerPanel != null) powerPanel.SetActive(false);
        SetStory("");
        GameAudio.Correct();
        StartQuiz();
    }

    void ResetPieces(bool kinematic)
    {
        if (targets == null) return;
        foreach (ChukoTarget t in targets)
            if (t != null) t.ResetPiece(kinematic);
    }

    bool IsAtThrowLine()
    {
        if (playPoint == null || player == null) return false;
        Vector3 a = player.position, b = playPoint.position;
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b) <= throwZone;
    }

    void DrawAim(Vector3 v)
    {
        if (aimLine == null) return;
        aimLine.enabled = true;
        const int n = 24;
        aimLine.positionCount = n;
        Vector3 p = ThrowOrigin();
        Vector3 vel = v;
        for (int i = 0; i < n; i++)
        {
            aimLine.SetPosition(i, p);
            vel += Physics.gravity * 0.05f;
            p += vel * 0.05f;
        }
    }

    void SetAim(bool on)
    {
        if (aimLine != null && aimLine.enabled != on) aimLine.enabled = on;
    }

    // ------------------------------------------------------------- опрос

    void StartQuiz()
    {
        questionIndex = 0;
        if (storyText != null) storyText.gameObject.SetActive(false);
        if (quizPanel != null) quizPanel.SetActive(true);
        ShowQuestion();
        UpdateUI();
    }

    void ShowQuestion()
    {
        if (questionText != null) questionText.text = Loc.IsEnglish ? QuestionsEn[questionIndex] : Questions[questionIndex];
        if (feedbackText != null) feedbackText.text = Loc.T("Вопрос ", "Question ") + (questionIndex + 1) + Loc.T(" из ", " of ") + Questions.Length + Loc.T("  ·  мышь или клавиши 1, 2, 3", "  ·  mouse or keys 1, 2, 3");
        for (int i = 0; answerButtons != null && i < answerButtons.Length; i++)
        {
            if (answerButtons[i] == null) continue;
            bool has = i < 3;
            answerButtons[i].gameObject.SetActive(has);
            answerButtons[i].interactable = true;
            TMP_Text label = answerButtons[i].GetComponentInChildren<TMP_Text>();
            if (has && label != null) label.text = Loc.IsEnglish ? AnswersEn[questionIndex][i] : Answers[questionIndex][i];
        }
    }

    public void CheckAnswer(int index)
    {
        if (state != QuestState.Quiz) return;

        if (index != Correct[questionIndex])
        {
            if (feedbackText != null) feedbackText.text = "<color=#ED665C>" + Loc.T("Туура эмес! Попробуй ещё раз.", "Туура эмес! Try again.") + "</color>";
            GameAudio.Wrong();
            return;
        }

        GameAudio.Correct();
        questionIndex++;
        if (questionIndex >= Questions.Length) CompleteQuest();
        else ShowQuestion();
    }

    void CompleteQuest()
    {
        state = QuestState.Completed;
        if (quizPanel != null) quizPanel.SetActive(false);
        if (storyText != null) storyText.gameObject.SetActive(true);

        if (!rewardGiven && GameManager.Instance != null)
        {
            GameManager.Instance.CompleteQuest(QuestId, rewardXP);
            rewardGiven = true;
        }

        SetStory("Бала: Азаматсың! Жарайсың!\n" + (Loc.IsEnglish
                 ? "You knocked out " + hits + " чүкө and know the game like a real Kyrgyz! +" + rewardXP + " XP"
                 : "Ты выбил " + hits + " чүкө и знаешь игру как настоящий кыргыз! +" + rewardXP + " XP"));
        UpdateUI();
    }

    // ------------------------------------------------------------- интерфейс

    // Оставлено для совместимости со старыми мишенями
    public void RegisterHit()
    {
        hits++;
        GameAudio.Pickup();
        UpdateUI();
    }

    bool IsNear(Transform target)
    {
        if (target == null || player == null) return false;
        Vector3 a = player.position, b = target.position;
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b) <= interactionDistance;
    }

    static string Key(string action) => "<b><color=#E8B04A>E</color></b>  —  " + action;

    void ShowHint(string message)
    {
        if (interactionText != null) interactionText.text = message;
        else InteractionHint.Request(message);
    }

    void SetStory(string message)
    {
        if (storyBackground != null) storyBackground.SetActive(!string.IsNullOrEmpty(message));
        if (storyText != null) storyText.text = message;
    }

    void UpdateUI()
    {
        if (scoreText != null) scoreText.text = "Чүкө: " + hits + "/" + needKnockOut;
        if (questText == null) return;

        const string title = "<b>Чүкө атуу</b>";
        string Sub(string text) => "\n<size=85%><color=#C9BFA6>" + text + "</color></size>";
        string score = "  <color=#E8B04A>" + hits + "/" + needKnockOut + "</color>";

        switch (state)
        {
            case QuestState.TalkToChild: questText.text = title + Sub(Loc.T("Поговори с ребёнком на лугу", "Talk to the child in the meadow")); break;
            case QuestState.GoToPlayground: questText.text = title + Sub(Loc.T("Подойди к черте у круга и нажми E", "Go to the line by the circle and press E")); break;
            case QuestState.Playing: questText.text = title + score + Sub(Loc.T("Выбей чүкө из круга · саки: ", "Knock чүкө out of the circle · throws: ") + throwsLeft); break;
            case QuestState.Quiz: questText.text = title + "  <color=#5FC873>" + hits + "/" + needKnockOut + "</color>" + Sub(Loc.T("Ответь на вопросы детей", "Answer the children's questions")); break;
            case QuestState.Completed: questText.text = title + "  <color=#5FC873>✓</color>" + Sub(Loc.T("Квест выполнен", "Quest complete")); break;
        }
    }
}
