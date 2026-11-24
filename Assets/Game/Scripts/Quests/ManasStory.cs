using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Манасчы у костра: вечером (с 18:00 до 5:00) рассказывает «Манас» — 6 страниц с картинками,
/// текстом и кыргызской строкой, под напев сказителя и комуз. В конце — викторина из 5 вопросов.
/// Днём просит прийти вечером. Окно закрывается по Esc (IModal), игра на паузе, пока окно открыто.
/// </summary>
public class ManasStory : MonoBehaviour, IModal
{
    public float talkDistance = 3.2f;
    public const int Reward = 150;

    class Page { public string img, titleRu, titleEn, textRu, textEn, ky, kyRu, kyEn; }
    class Q { public string ru, en; public string[] aRu, aEn; public int right; }

    static readonly Page[] Pages =
    {
        new Page { img = "manas_1", titleRu = "Жакыптын тилеги · Мечта Жакыпа", titleEn = "Жакыптын тилеги · Jakyp's wish",
            textRu = "Давным-давно жил богатый Жакып. Были у него стада и юрты, но не было сына. Долгие годы он и его жена Чыйырды просили небо о ребёнке — о наследнике, который защитит народ.",
            textEn = "Long ago there lived the rich Jakyp. He had herds and yurts, but no son. For many years he and his wife Chyiyrdy prayed to the sky for a child — an heir who would protect the people.",
            ky = "Жакып байдын тилеги — уулдуу болуу.", kyRu = "Мечта Жакыпа — иметь сына.", kyEn = "Jakyp's dream is to have a son." },
        new Page { img = "manas_2", titleRu = "Манастын төрөлүшү · Рождение Манаса", titleEn = "Манастын төрөлүшү · The birth of Manas",
            textRu = "И вот родился мальчик. Говорят, в ту ночь над юртой кружил беркут, а свет из түндүка поднимался до самых звёзд. Мальчика назвали Манас. Он рос не по дням, а по часам и уже ребёнком был сильнее взрослых джигитов.",
            textEn = "At last a boy was born. They say that night an eagle circled over the yurt, and light from the түндүк rose up to the stars. The boy was named Manas. He grew by the hour, and even as a child he was stronger than grown men.",
            ky = "Манас — эл үчүн төрөлгөн баатыр.", kyRu = "Манас — богатырь, рождённый для народа.", kyEn = "Manas is a hero born for his people." },
        new Page { img = "manas_3", titleRu = "Ак-Кула · Верный конь", titleEn = "Ак-Кула · The faithful horse",
            textRu = "У каждого баатыра есть верный конь. Конь Манаса — Ак-Кула: быстрый, как ветер, и умный, как человек. Вместе они прошли горы и степи, и ни один враг не мог их догнать.",
            textEn = "Every hero has a faithful horse. Manas's horse is Ak-Kula: fast as the wind and wise as a man. Together they crossed mountains and steppes, and no enemy could catch them.",
            ky = "Ак-Кула — Манастын тулпары.", kyRu = "Ак-Кула — тулпар (крылатый конь) Манаса.", kyEn = "Ak-Kula is Manas's тулпар — winged steed." },
        new Page { img = "manas_4", titleRu = "Кырк чоро · Сорок дружинников", titleEn = "Кырк чоро · The forty warriors",
            textRu = "Манас собрал вокруг себя кырк чоро — сорок верных дружинников. Он объединил кыргызские роды в один народ и защищал его от врагов. Сорок лучей солнца на флаге Кыргызстана тоже напоминают о сорока родах.",
            textEn = "Manas gathered кырк чоро — forty loyal warriors. He united the Kyrgyz clans into one people and defended it from enemies. The forty rays of the sun on Kyrgyzstan's flag also recall the forty clans.",
            ky = "Кырк чоро — Манастын кырк жигити.", kyRu = "Кырк чоро — сорок джигитов Манаса.", kyEn = "Кырк чоро are the forty young men of Manas." },
        new Page { img = "manas_5", titleRu = "Акылман Каныкей · Мудрая Каныкей", titleEn = "Акылман Каныкей · Wise Kanykei",
            textRu = "Жена Манаса — мудрая Каныкей. Она давала мужу добрые советы, своими руками сшила ему боевой ак олпок, а после ухода Манаса вырастила их сына Семетея достойным человеком.",
            textEn = "Manas's wife is wise Kanykei. She gave her husband good counsel, sewed his battle coat — the ак олпок — with her own hands, and after Manas was gone she raised their son Semetei to be a worthy man.",
            ky = "Каныкей — акылдуу аял.", kyRu = "Каныкей — мудрая женщина.", kyEn = "Kanykei is a wise woman." },
        new Page { img = "manas_6", titleRu = "Манас — биздин мурас · Наше наследие", titleEn = "Манас — биздин мурас · Our heritage",
            textRu = "«Манас» — одна из самых длинных поэм в мире: в варианте Саякбая Каралаева около полумиллиона строк. Это трилогия: «Манас», «Семетей» и «Сейтек». Веками её пели по памяти манасчы — сказители. С 2013 года трилогия в списке нематериального наследия ЮНЕСКО.",
            textEn = "Manas is one of the longest poems in the world: Sayakbay Karalaev's version has about half a million lines. It is a trilogy — Manas, Semetei and Seitek. For centuries it was sung from memory by the manaschy, the storytellers. Since 2013 the trilogy is on UNESCO's intangible heritage list.",
            ky = "Манас — кыргыз элинин мурасы.", kyRu = "«Манас» — наследие кыргызского народа.", kyEn = "Manas is the heritage of the Kyrgyz people." },
    };

    static readonly Q[] Quiz =
    {
        new Q { ru = "Как звали коня Манаса?", en = "What was the name of Manas's horse?", aRu = new[] { "Ак-Кула", "Тулпар Жылдыз", "Көк-Бору" }, aEn = new[] { "Ak-Kula", "Tulpar Jyldyz", "Kök-Böru" }, right = 0 },
        new Q { ru = "Сколько верных чоро было у Манаса?", en = "How many loyal chоro did Manas have?", aRu = new[] { "Семь", "Сорок", "Сто" }, aEn = new[] { "Seven", "Forty", "One hundred" }, right = 1 },
        new Q { ru = "Как звали мудрую жену Манаса?", en = "What was the name of Manas's wise wife?", aRu = new[] { "Чыйырды", "Айчүрөк", "Каныкей" }, aEn = new[] { "Chyiyrdy", "Aichürök", "Kanykei" }, right = 2 },
        new Q { ru = "Кто такой манасчы?", en = "Who is a manaschy?", aRu = new[] { "Сказитель эпоса «Манас»", "Кузнец", "Пастух" }, aEn = new[] { "A storyteller of the Manas epic", "A blacksmith", "A shepherd" }, right = 0 },
        new Q { ru = "Как зовут сына Манаса?", en = "What is the name of Manas's son?", aRu = new[] { "Бакай", "Семетей", "Жакып" }, aEn = new[] { "Bakai", "Semetei", "Jakyp" }, right = 1 },
    };

    private Transform player;
    private GameObject canvas, root, storyView, quizView, resultView;
    private Image picture;
    private TMP_Text title, body, quote, pageNo, qText, qFeedback, resultText;
    private Button[] answers = new Button[3];
    private Button nextBtn, backBtn;
    private AudioSource chant;
    private int page, qIndex, correct;
    private bool open, answered;

    void Start()
    {
        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null) player = pm.transform;
        chant = gameObject.AddComponent<AudioSource>();
        chant.clip = MurasUI.Clip("manas_chant"); chant.loop = true; chant.spatialBlend = 0f; chant.volume = 0.5f; chant.playOnAwake = false; chant.ignoreListenerPause = true;
        Loc.Changed += Rebuild;
    }

    void OnDestroy() { Loc.Changed -= Rebuild; if (open) { Modal.Close(this); if (!PauseMenu.IsPaused) Time.timeScale = 1f; } }

    public static bool Evening => DayNightCycle.Hour >= 18f || DayNightCycle.Hour < 5f;

    void Update()
    {
        if (open)
        {
            if (storyView != null && storyView.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Next();
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Back();
            }
            else if (quizView != null && quizView.activeSelf && !answered)
            {
                for (int i = 0; i < 3; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Answer(i);
            }
            else if (quizView != null && quizView.activeSelf && answered && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))) NextQuestion();
            return;
        }

        if (player == null || !Modal.Free) return;
        Vector3 d = player.position - transform.position; d.y = 0f;
        if (d.sqrMagnitude > talkDistance * talkDistance) return;
        InteractionHint.Request("<b><color=#E8B04A>E</color></b>  —  " + (Evening ? Loc.T("послушать «Манас» у костра", "listen to Manas by the fire") : Loc.T("поговорить с манасчы", "talk to the manaschy")));
        if (!Input.GetKeyDown(KeyCode.E)) return;
        if (Evening) Open();
        else
        {
            Vocab.Unlock("manaschy");
            QuestTalk.Say("Манасчы", Loc.T("Саламатсыңбы, балам! Мен манасчымын — сказитель «Манаса». «Манасты» кечинде, от жанында айтышат — приходи вечером, когда зажгутся звёзды, к костру. Расскажу тебе о великом баатыре.",
                                          "Саламатсыңбы, child! I am a manaschy — a teller of Manas. Manas is told in the evening by the fire — come back when the stars light up, and I'll tell you about the great hero."));
        }
    }

    // ------------------------------------------------------------------ окно

    public void Open()
    {
        if (root == null) Build();
        open = true;
        Modal.Open(this);
        Time.timeScale = 0f;
        root.SetActive(true);
        page = Mathf.Clamp(SaveData.Get("manas_page"), 0, Pages.Length - 1);
        if (SaveData.Get("manas_done") == 1) page = 0;
        Vocab.Unlock("manaschy", "baatyr");
        ShowPage();
        if (chant.clip != null) chant.Play();
    }

    public void Close()
    {
        if (!open) return;
        open = false;
        chant.Stop();
        root.SetActive(false);
        Modal.Close(this);
        if (!PauseMenu.IsPaused) Time.timeScale = 1f;
    }

    public void CloseModal() => Close();

    void Rebuild() { bool was = open; if (canvas != null) Destroy(canvas); canvas = null; root = null; if (was) { open = false; Modal.Close(this); Open(); } }

    void Build()
    {
        canvas = MurasUI.Canvas("ManasCanvas", transform, 470);
        root = MurasUI.Overlay(canvas.transform, 0.78f);
        var win = MurasUI.WindowBox(root.transform, new Vector2(1400, 780));
        MurasUI.CloseButton(win.transform, Close);
        MurasUI.Text(win.transform, Loc.T("МАНАС  ·  эпос сказителя", "MANAS  ·  the storyteller's epic"), 22, MurasUI.Gold, new Vector2(0, 352), new Vector2(1000, 34), FontStyles.Bold);

        // рассказ
        storyView = MurasUI.Rect("Story", win.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 780));
        var pic = MurasUI.Box("Picture", storyView.transform, new Vector2(-330, 10), new Vector2(660, 440), Color.white);
        picture = pic.GetComponent<Image>(); picture.preserveAspect = true;
        title = MurasUI.Text(storyView.transform, "", 32, MurasUI.Gold, new Vector2(340, 250), new Vector2(640, 90), FontStyles.Bold, TextAlignmentOptions.TopLeft);
        body = MurasUI.Text(storyView.transform, "", 24, MurasUI.Cream, new Vector2(340, 40), new Vector2(640, 320), FontStyles.Normal, TextAlignmentOptions.TopLeft);
        var qbox = MurasUI.Box("Quote", storyView.transform, new Vector2(340, -190), new Vector2(640, 96), MurasUI.Card);
        quote = MurasUI.Text(qbox.transform, "", 22, MurasUI.Gold, Vector2.zero, new Vector2(600, 86), FontStyles.Italic);
        pageNo = MurasUI.Text(storyView.transform, "", 20, MurasUI.Muted, new Vector2(-330, -260), new Vector2(400, 30));
        backBtn = MurasUI.Button(storyView.transform, Loc.T("Артка", "Back"), new Vector2(160, -310), new Vector2(220, 58), Back);
        nextBtn = MurasUI.Button(storyView.transform, Loc.T("Кийинки", "Next"), new Vector2(460, -310), new Vector2(260, 58), Next);
        MurasUI.Text(storyView.transform, Loc.T("Пробел — дальше  ·  Esc — закрыть", "Space — next  ·  Esc — close"), 17, MurasUI.Muted, new Vector2(-330, -310), new Vector2(500, 28));

        // викторина
        quizView = MurasUI.Rect("Quiz", win.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 780));
        qText = MurasUI.Text(quizView.transform, "", 36, MurasUI.Cream, new Vector2(0, 200), new Vector2(1100, 120), FontStyles.Bold);
        for (int i = 0; i < 3; i++)
        {
            int k = i;
            answers[i] = MurasUI.Button(quizView.transform, "", new Vector2(0, 60 - i * 86), new Vector2(760, 70), () => Answer(k), 26);
        }
        qFeedback = MurasUI.Text(quizView.transform, "", 26, MurasUI.Gold, new Vector2(0, -230), new Vector2(1100, 80));
        MurasUI.Text(quizView.transform, Loc.T("Ответ — мышью или клавишами 1, 2, 3", "Answer with the mouse or keys 1, 2, 3"), 18, MurasUI.Muted, new Vector2(0, -320), new Vector2(800, 28));

        // итог
        resultView = MurasUI.Rect("Result", win.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 780));
        resultText = MurasUI.Text(resultView.transform, "", 30, MurasUI.Cream, new Vector2(0, 60), new Vector2(1100, 360));
        MurasUI.Button(resultView.transform, Loc.T("Рахмат, ата!", "Thank you, ata!"), new Vector2(0, -230), new Vector2(360, 64), Close);
        root.SetActive(false);
    }

    void ShowPage()
    {
        storyView.SetActive(true); quizView.SetActive(false); resultView.SetActive(false);
        Page p = Pages[page];
        Texture2D t = Resources.Load<Texture2D>("MURAS/Manas/" + p.img);
        if (t != null) picture.sprite = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));
        title.text = Loc.T(p.titleRu, p.titleEn);
        body.text = Loc.T(p.textRu, p.textEn);
        quote.text = "<b>" + p.ky + "</b>\n<size=85%><color=#C9BFA6>" + Loc.T(p.kyRu, p.kyEn) + "</color></size>";
        pageNo.text = (page + 1) + " / " + Pages.Length;
        backBtn.interactable = page > 0;
        nextBtn.GetComponentInChildren<TMP_Text>().text = page == Pages.Length - 1 ? Loc.T("Суроолор", "Quiz") : Loc.T("Кийинки", "Next");
        SaveData.SetMax("manas_page", page);
        if (page == 3) Vocab.Unlock("choro");
        if (page == 5) Vocab.Unlock("el");
        MurasUI.Play("komuz_pluck", 0.6f, 0.9f + page * 0.03f);
    }

    void Next()
    {
        if (page < Pages.Length - 1) { page++; ShowPage(); }
        else StartQuiz();
    }

    void Back() { if (page > 0) { page--; ShowPage(); } }

    void StartQuiz()
    {
        qIndex = 0; correct = 0;
        storyView.SetActive(false); quizView.SetActive(true); resultView.SetActive(false);
        ShowQuestion();
    }

    void ShowQuestion()
    {
        answered = false;
        Q q = Quiz[qIndex];
        qText.text = (qIndex + 1) + ". " + Loc.T(q.ru, q.en);
        string[] a = Loc.IsEnglish ? q.aEn : q.aRu;
        for (int i = 0; i < 3; i++)
        {
            answers[i].GetComponentInChildren<TMP_Text>().text = (i + 1) + "   " + a[i];
            answers[i].GetComponent<Image>().color = MurasUI.ButtonCol;
            answers[i].interactable = true;
        }
        qFeedback.text = "";
    }

    void Answer(int i)
    {
        if (answered) return;
        answered = true;
        Q q = Quiz[qIndex];
        bool ok = i == q.right;
        if (ok) correct++;
        answers[q.right].GetComponent<Image>().color = MurasUI.Green;
        if (!ok) answers[i].GetComponent<Image>().color = MurasUI.Red;
        foreach (Button b in answers) b.interactable = false;
        if (ok) GameAudio.Correct(); else GameAudio.Wrong();
        qFeedback.text = (ok ? Loc.T("Туура! Правильно!", "Туура! Correct!") : Loc.T("Туура эмес... Неверно.", "Туура эмес... Not quite.")) +
                         "  <size=80%><color=#C9BFA6>" + Loc.T("Пробел — дальше", "Space — next") + "</color></size>";
        StopAllCoroutines();
        StartCoroutine(AutoNext(qIndex));
    }

    System.Collections.IEnumerator AutoNext(int at)
    {
        yield return new WaitForSecondsRealtime(1.6f);   // игра на паузе — ждём по реальным часам
        if (open && answered && qIndex == at) NextQuestion();
    }

    void NextQuestion()
    {
        if (!answered) return;
        StopAllCoroutines();
        answered = false;
        qIndex++;
        if (qIndex < Quiz.Length) ShowQuestion();
        else ShowResult();
    }

    void ShowResult()
    {
        storyView.SetActive(false); quizView.SetActive(false); resultView.SetActive(true);
        bool first = SaveData.Get("manas_done") == 0;
        bool pass = correct >= 3;
        string txt = Loc.T($"Туура жооптор: <b>{correct} / {Quiz.Length}</b>\n\n", $"Correct answers: <b>{correct} / {Quiz.Length}</b>\n\n");
        if (pass && first)
        {
            SaveData.Set("manas_done", 1);
            if (GameManager.Instance != null) GameManager.Instance.AddXP(Reward);
            Achievements.Give("manas");
            GameAudio.QuestComplete();
            txt += Loc.T($"Манасчы: «Азаматсың! Молодец! Теперь ты тоже хранишь наш мурас.»\n\n<color=#E8B04A>+{Reward} XP</color>",
                         $"Manaschy: \"Азаматсың! Well done! Now you too keep our мурас.\"\n\n<color=#E8B04A>+{Reward} XP</color>");
        }
        else if (pass) txt += Loc.T("Манасчы: «Жакшы! Ты хорошо помнишь сказание.»", "Manaschy: \"Жакшы! You remember the tale well.\"");
        else txt += Loc.T("Манасчы: «Ничего, балам. Послушай ещё раз — и всё запомнишь.» (нужно 3 правильных ответа)", "Manaschy: \"No matter, child. Listen once more and you'll remember.\" (3 correct answers needed)");
        resultText.text = txt;
    }

    /// <summary>Для теста из меню редактора.</summary>
    public static void OpenNearest() { var m = FindAnyObjectByType<ManasStory>(); if (m != null) m.Open(); }
}
