using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Тест знаний для исследования «до / после» (pre-test / post-test).
///  • При первом входе в село — 10 вопросов о кыргызских словах и обычаях (ответы не показываются, чтобы тест не учил).
///  • После основных заданий (или ~40 минут игры) игра предлагает пройти тот же тест ещё раз и показывает прирост.
///  • Результаты: на сервер (событие test_result → панель учителя) и в локальный файл muras_research.csv.
/// Тот же экран викторины используется для самопроверки слов в словаре (WordReview).
/// </summary>
public static class KnowledgeTest
{
    public class Question
    {
        public string id, word, ru, en;
        public string[] optRu, optEn;          // [0] — правильный ответ
    }

    static Question Q(string id, string word, string ru, string en, string[] optRu, string[] optEn) =>
        new Question { id = id, word = word, ru = ru, en = en, optRu = optRu, optEn = optEn };

    public static readonly List<Question> Questions = new List<Question>
    {
        Q("q1", "tunduk", "«Түндүк» — это…", "\"Tündük\" is…",
          new[] { "Круглый верх юрты (он изображён на флаге Кыргызстана)", "Решётчатая стена юрты", "Войлочный ковёр", "Праздничная шапка" },
          new[] { "The crown ring of a yurt (it is on the flag of Kyrgyzstan)", "The lattice wall of a yurt", "A felt carpet", "A festive hat" }),
        Q("q2", "rahmat", "Как по-кыргызски «спасибо»?", "How do you say \"thank you\" in Kyrgyz?",
          new[] { "Рахмат", "Салам", "Кечирим", "Тынчтык" }, new[] { "Rakhmat", "Salam", "Kechirim", "Tynchtyk" }),
        Q("q3", "kymyz", "Что такое «кымыз»?", "What is \"kymyz\"?",
          new[] { "Напиток из кобыльего молока", "Суп из баранины", "Жареные кусочки теста", "Сладкий чай с молоком" },
          new[] { "A drink made from mare's milk", "Mutton soup", "Fried pieces of dough", "Sweet tea with milk" }),
        Q("q4", "kerege", "Как называются решётчатые стены юрты?", "What are the lattice walls of a yurt called?",
          new[] { "Кереге", "Уук", "Түндүк", "Төр" }, new[] { "Kerege", "Uuk", "Tündük", "Tör" }),
        Q("q5", "beshbarmak", "Что буквально значит «бешбармак»?", "What does \"beshbarmak\" literally mean?",
          new[] { "«Пять пальцев»", "«Белая гора»", "«Серый волк»", "«Быстрый конь»" },
          new[] { "\"Five fingers\"", "\"White mountain\"", "\"Grey wolf\"", "\"Fast horse\"" }),
        Q("q6", "kok_boru", "Көк бөрү — это…", "Kök börü is…",
          new[] { "Конная игра: всадники борются за тушу козла", "Игра в альчики в круге", "Скачки на длинную дистанцию", "Борьба на поясах" },
          new[] { "A horseback game: riders fight over a goat carcass", "A knucklebone game in a circle", "A long-distance horse race", "Belt wrestling" }),
        Q("q7", "tor", "Что такое «төр» в юрте?", "What is the \"tör\" in a yurt?",
          new[] { "Почётное место напротив входа", "Место у самого входа", "Очаг в центре", "Угол для посуды" },
          new[] { "The place of honour opposite the door", "The spot right by the door", "The hearth in the centre", "The corner for dishes" }),
        Q("q8", "baike", "Как вежливо обратиться к мужчине старше тебя?", "How do you politely address a man older than you?",
          new[] { "Байке", "Эже", "Апа", "Бала" }, new[] { "Baike", "Ezhe", "Apa", "Bala" }),
        Q("q9", "burkutchu", "Кто такой бүркүтчү?", "Who is a bürkütchü?",
          new[] { "Охотник с беркутом", "Музыкант, играющий на комузе", "Пастух овец", "Сказитель «Манаса»" },
          new[] { "A hunter with a golden eagle", "A komuz player", "A shepherd", "A Manas storyteller" }),
        Q("q10", "shyrdak", "Что такое «шырдак»?", "What is a \"shyrdak\"?",
          new[] { "Войлочный ковёр с мозаичным узором", "Вышитый настенный ковёр", "Стопка одеял у стены", "Белая войлочная шапка" },
          new[] { "A felt carpet with a mosaic pattern", "An embroidered wall hanging", "A stack of blankets by the wall", "A white felt hat" }),
    };

    public static int Total => Questions.Count;

    // SaveData: kt_pre_done (1 — пройден, 2 — пропущен), kt_pre, kt_post_done, kt_post, kt_post_offered
    public static bool PreDone => SaveData.Get("kt_pre_done") > 0;
    public static bool PreTaken => SaveData.Get("kt_pre_done") == 1;
    public static bool PostDone => SaveData.Get("kt_post_done") > 0;
    public static int PreScore => SaveData.Get("kt_pre");
    public static int PostScore => SaveData.Get("kt_post");

    /// <summary>Автозапуск теста и обучения. В собранной игре — всегда; в редакторе — только если включено
    /// в меню Tools → Diploma → Learning (иначе при каждом Play прогресс сбрасывается и тест мешал бы разработке).</summary>
    public static bool AutoRun => !Application.isEditor || PlayerPrefs.GetInt("MURAS_LearnInEditor", 0) == 1;

    public static bool IsOpen => QuizWindow.Current != null;

    // ------------------------------------------------------------------ запуск
    public static void StartPre()
    {
        QuizWindow.Show(new QuizWindow.Config
        {
            title = Loc.T("ТЕСТ ЗНАНИЙ · до игры", "KNOWLEDGE TEST · before playing"),
            intro = Loc.T("10 коротких вопросов о кыргызских словах и обычаях — около 2 минут.\n\n" +
                          "Это нужно для исследования: после игры ты пройдёшь тот же тест ещё раз и увидишь, сколько узнал.\n" +
                          "Если не знаешь ответ — выбирай наугад, это нормально.",
                          "10 short questions about Kyrgyz words and customs — about 2 minutes.\n\n" +
                          "It is part of a study: after playing you will take the same test again and see how much you learned.\n" +
                          "If you don't know an answer, just guess — that's fine."),
            items = BuildItems(Questions),
            showFeedback = false,
            allowSkip = true,
            activity = "test",
            onDone = (r) => Finish("pre", r),
            resultText = (r) => ResultText("pre", r),
            onSkip = () => { SaveData.Set("kt_pre_done", 2); MurasCloud.Track("tutorial_step", CloudJson.Object("step", "pretest_skipped", "n", 0)); },
        });
    }

    public static void StartPost()
    {
        QuizWindow.Show(new QuizWindow.Config
        {
            title = Loc.T("ТЕСТ ЗНАНИЙ · после игры", "KNOWLEDGE TEST · after playing"),
            intro = PreTaken
                ? Loc.T("Те же 10 вопросов, что и в начале. До игры у тебя было " + PreScore + " из " + Total + ". Посмотрим, сколько ты узнал!",
                        "The same 10 questions as at the start. Before playing you scored " + PreScore + " of " + Total + ". Let's see how much you learned!")
                : Loc.T("10 вопросов о кыргызских словах и обычаях, которые встречались в игре.", "10 questions about the Kyrgyz words and customs you met in the game."),
            items = BuildItems(Questions),
            showFeedback = false,
            allowSkip = true,
            activity = "test",
            onDone = (r) => Finish("post", r),
            resultText = (r) => ResultText("post", r),
            showReview = true,
            onSkip = () => SaveData.Set("kt_post_offered", 1),
        });
    }

    static List<QuizWindow.Item> BuildItems(List<Question> qs)
    {
        var list = new List<QuizWindow.Item>();
        foreach (Question q in qs)
            list.Add(new QuizWindow.Item { id = q.id, word = q.word, question = Loc.T(q.ru, q.en), options = Loc.IsEnglish ? q.optEn : q.optRu });
        return list;
    }

    static void Finish(string phase, QuizWindow.Result r)
    {
        SaveData.Set("kt_" + phase, r.score);
        SaveData.Set("kt_" + phase + "_done", 1);
        if (phase == "post") SaveData.Set("kt_post_offered", 1);

        var answers = new StringBuilder("[");
        for (int i = 0; i < r.items.Count; i++)
        {
            if (i > 0) answers.Append(',');
            answers.Append(CloudJson.Object("q", r.items[i].id, "word", r.items[i].word ?? "", "ok", r.correct[i]));
        }
        answers.Append(']');
        MurasCloud.Track("test_result", CloudJson.Object("phase", phase, "score", r.score, "total", r.items.Count,
                                                         "seconds", Mathf.RoundToInt(r.seconds), "answers", CloudJson.Raw(answers.ToString())));
        ResearchLog.Append(phase, r);
    }

    /// <summary>Итоговый экран: для pre — только балл, для post — прирост и разбор ответов.</summary>
    public static string ResultText(string phase, QuizWindow.Result r)
    {
        if (phase == "pre")
            return Loc.T("Твой результат: <b>" + r.score + " из " + r.items.Count + "</b>.\n\nТеперь — в айыл! Слушай жителей, играй и собирай слова в словарь (J). " +
                         "После заданий тест можно пройти снова — в словаре, кнопка «Тест знаний».",
                         "Your score: <b>" + r.score + " of " + r.items.Count + "</b>.\n\nNow — to the village! Listen to the villagers, play and collect words in the dictionary (J). " +
                         "After the quests you can retake the test — in the dictionary, \"Knowledge test\" button.");
        int gain = r.score - PreScore;
        string g = PreTaken ? (gain > 0 ? "<color=#8FD27A>+" + gain + "</color>" : gain.ToString()) : "";
        return PreTaken
            ? Loc.T("До игры: <b>" + PreScore + "</b>   →   после игры: <b>" + r.score + "</b>   (" + g + ")", "Before: <b>" + PreScore + "</b>   →   after: <b>" + r.score + "</b>   (" + g + ")")
            : Loc.T("Твой результат: <b>" + r.score + " из " + r.items.Count + "</b>", "Your score: <b>" + r.score + " of " + r.items.Count + "</b>");
    }

    // ------------------------------------------------------------------ когда предлагать тест после игры
    public static bool ShouldOfferPost()
    {
        if (!PreDone || PostDone || SaveData.Get("kt_post_offered") == 1) return false;
        bool quests = GameManager.SavedQuestCount >= GameManager.TotalQuests;
        bool time = SaveData.Get("play_min") >= 40;
        return quests || time;
    }
}

/// <summary>Журнал исследования на этом компьютере: muras_research.csv (одна строка на прохождение теста).
/// Пригодится, если тест проходят без аккаунта / без сервера.</summary>
public static class ResearchLog
{
    public static string PathCsv => Path.Combine(Application.persistentDataPath, "muras_research.csv");

    public static string PlayerId
    {
        get
        {
            if (MurasCloud.LoggedIn && !string.IsNullOrEmpty(MurasCloud.Username)) return MurasCloud.Username;
            string id = PlayerPrefs.GetString("MURAS_LocalPlayerId", "");
            if (string.IsNullOrEmpty(id)) { id = "local-" + Guid.NewGuid().ToString("N").Substring(0, 8); PlayerPrefs.SetString("MURAS_LocalPlayerId", id); PlayerPrefs.Save(); }
            return id;
        }
    }

    public static void Append(string phase, QuizWindow.Result r)
    {
        try
        {
            bool fresh = !File.Exists(PathCsv);
            var sb = new StringBuilder();
            if (fresh)
            {
                sb.Append("time_utc,player,phase,score,total,seconds");
                foreach (var q in KnowledgeTest.Questions) sb.Append(',').Append(q.id);
                sb.Append('\n');
            }
            sb.Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")).Append(',').Append(PlayerId).Append(',').Append(phase).Append(',')
              .Append(r.score).Append(',').Append(r.items.Count).Append(',').Append(Mathf.RoundToInt(r.seconds));
            foreach (var q in KnowledgeTest.Questions)
            {
                int i = r.items.FindIndex(x => x.id == q.id);
                sb.Append(',').Append(i < 0 ? "" : (r.correct[i] ? "1" : "0"));
            }
            sb.Append('\n');
            File.AppendAllText(PathCsv, sb.ToString(), Encoding.UTF8);
        }
        catch (Exception e) { Debug.LogWarning("MURAS: не удалось записать muras_research.csv: " + e.Message); }
    }
}

/// <summary>
/// Окно викторины в стиле игры: вступление → вопросы с 4 вариантами (клик или клавиши 1–4) → итог.
/// Варианты перемешиваются. С обратной связью (самопроверка) — подсветка верного ответа, без неё (тест) — сразу дальше.
/// </summary>
public class QuizWindow : MonoBehaviour, IModal
{
    public class Item { public string id, word, question; public string[] options; }
    public class Result { public List<Item> items; public bool[] correct; public int score; public float seconds; }

    public class Config
    {
        public string title, intro, activity;
        public List<Item> items;
        public bool showFeedback, allowSkip, showReview;
        public Action<Result> onDone;
        public Action onSkip;
        public Func<Result, string> resultText;
        public Action<Item, bool> onAnswer;
    }

    public static QuizWindow Current { get; private set; }
    public string Activity => cfg != null ? cfg.activity : null;

    Config cfg;
    GameObject canvas, body;
    TMP_Text titleText, progressText;
    Image progressFill;
    int index = -1;
    bool[] correct;
    int[] order;               // order[i] — позиция правильного ответа после перемешивания
    string[][] shuffled;
    float startTime;
    bool locked;
    readonly List<Button> optionButtons = new List<Button>();

    public static void Show(Config cfg)
    {
        if (Current != null) return;
        var go = new GameObject("QuizWindow");
        var w = go.AddComponent<QuizWindow>();
        w.cfg = cfg;
        w.Build();
    }

    void Build()
    {
        Current = this;
        Modal.Open(this);
        Time.timeScale = 0f;
        canvas = MurasUI.Canvas("QuizCanvas", transform, 480);
        var root = MurasUI.Overlay(canvas.transform, 0.72f);
        var win = MurasUI.WindowBox(root.transform, new Vector2(1180, 760)).transform;
        titleText = MurasUI.Text(win, cfg.title, 40, MurasUI.Gold, new Vector2(0, 330), new Vector2(1100, 56), FontStyles.Bold);
        progressText = MurasUI.Text(win, "", 18, MurasUI.Muted, new Vector2(0, 290), new Vector2(1100, 28), FontStyles.Italic);
        var track = MurasUI.Box("Progress", win, new Vector2(0, 268), new Vector2(1000, 8), new Color(0.2f, 0.16f, 0.12f));
        var fill = MurasUI.Rect("Fill", track.transform, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, Vector2.zero);
        ((RectTransform)fill.transform).pivot = new Vector2(0, 0.5f);
        progressFill = fill.AddComponent<Image>(); progressFill.color = MurasUI.Gold;
        body = MurasUI.Rect("Body", win, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(1100, 600));
        correct = new bool[cfg.items.Count];
        order = new int[cfg.items.Count];
        shuffled = new string[cfg.items.Count][];
        for (int i = 0; i < cfg.items.Count; i++) Shuffle(i);
        if (!string.IsNullOrEmpty(cfg.intro)) ShowIntro(); else Next();
        MurasUI.Play("tile_turn", 0.6f);
    }

    void Shuffle(int i)
    {
        string[] opts = cfg.items[i].options;
        var idx = new List<int>();
        for (int k = 0; k < opts.Length; k++) idx.Add(k);
        for (int k = idx.Count - 1; k > 0; k--) { int j = UnityEngine.Random.Range(0, k + 1); (idx[k], idx[j]) = (idx[j], idx[k]); }
        shuffled[i] = new string[opts.Length];
        for (int k = 0; k < idx.Count; k++) { shuffled[i][k] = opts[idx[k]]; if (idx[k] == 0) order[i] = k; }
    }

    void Clear()
    {
        optionButtons.Clear();
        foreach (Transform c in body.transform) Destroy(c.gameObject);
    }

    void SetProgress(float t, string label)
    {
        progressText.text = label;
        ((RectTransform)progressFill.transform).anchorMax = new Vector2(Mathf.Clamp01(t), 1);
    }

    void ShowIntro()
    {
        Clear();
        SetProgress(0, Loc.T("Вопросов: ", "Questions: ") + cfg.items.Count);
        var t = MurasUI.Text(body.transform, cfg.intro, 26, MurasUI.Cream, new Vector2(0, 90), new Vector2(980, 330));
        t.lineSpacing = 8;
        MurasUI.Button(body.transform, Loc.T("Начать", "Start"), new Vector2(cfg.allowSkip ? -150 : 0, -190), new Vector2(260, 64), Begin, 26);
        if (cfg.allowSkip)
            MurasUI.Button(body.transform, Loc.T("Пропустить", "Skip"), new Vector2(150, -190), new Vector2(260, 64), Skip, 22)
                   .GetComponent<Image>().color = new Color(0.32f, 0.22f, 0.15f);
        MurasUI.Text(body.transform, Loc.T("Enter — начать", "Enter — start"), 16, MurasUI.Muted, new Vector2(0, -255), new Vector2(600, 24), FontStyles.Italic);
    }

    void Begin() { startTime = Time.unscaledTime; Next(); }

    void Skip()
    {
        cfg.onSkip?.Invoke();
        Close();
    }

    void Next()
    {
        if (index < 0 && startTime <= 0f) startTime = Time.unscaledTime;
        index++;
        if (index >= cfg.items.Count) { ShowResult(); return; }
        Clear();
        locked = false;
        Item it = cfg.items[index];
        SetProgress(index / (float)cfg.items.Count, Loc.T("Вопрос ", "Question ") + (index + 1) + " / " + cfg.items.Count);
        MurasUI.Text(body.transform, it.question, 34, MurasUI.Cream, new Vector2(0, 200), new Vector2(1040, 120), FontStyles.Bold);
        string[] opts = shuffled[index];
        for (int k = 0; k < opts.Length; k++)
        {
            int captured = k;
            var b = MurasUI.Button(body.transform, (k + 1) + ".  " + opts[k], new Vector2(0, 70 - k * 86), new Vector2(940, 74), () => Answer(captured), 23);
            var label = b.GetComponentInChildren<TMP_Text>();
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.margin = new Vector4(28, 0, 20, 0);
            label.enableAutoSizing = true; label.fontSizeMin = 16; label.fontSizeMax = 23;
            optionButtons.Add(b);
        }
        MurasUI.Text(body.transform, Loc.T("Клавиши 1–4 или клик мышью", "Keys 1–4 or click"), 16, MurasUI.Muted, new Vector2(0, -275), new Vector2(600, 24), FontStyles.Italic);
    }

    void Answer(int k)
    {
        if (locked || index < 0 || index >= cfg.items.Count) return;
        locked = true;
        bool ok = k == order[index];
        correct[index] = ok;
        cfg.onAnswer?.Invoke(cfg.items[index], ok);
        if (!cfg.showFeedback)
        {
            MurasUI.Play("tile_turn", 0.5f, 1.1f);
            optionButtons[k].GetComponent<Image>().color = MurasUI.Gold * 0.85f;
            StartCoroutine(After(0.25f, Next));
            return;
        }
        MurasUI.Play(ok ? "komuz_pluck" : "komuz_miss", 0.8f);
        optionButtons[order[index]].GetComponent<Image>().color = new Color(0.30f, 0.55f, 0.25f);
        if (!ok) optionButtons[k].GetComponent<Image>().color = new Color(0.62f, 0.22f, 0.18f);
        StartCoroutine(After(ok ? 0.7f : 1.5f, Next));
    }

    IEnumerator After(float t, Action a) { yield return new WaitForSecondsRealtime(t); a(); }

    void ShowResult()
    {
        Clear();
        int score = 0;
        foreach (bool c in correct) if (c) score++;
        var r = new Result { items = cfg.items, correct = correct, score = score, seconds = Time.unscaledTime - startTime };
        index = cfg.items.Count;
        SetProgress(1, Loc.T("Готово!", "Done!"));
        cfg.onDone?.Invoke(r);
        MurasUI.Play("achievement", 0.8f);
        string text = cfg.resultText != null ? cfg.resultText(r) : Loc.T("Результат: <b>" + score + " из " + r.items.Count + "</b>", "Score: <b>" + score + " of " + r.items.Count + "</b>");
        var t = MurasUI.Text(body.transform, text, 30, MurasUI.Cream, new Vector2(0, 150), new Vector2(1000, 230));
        t.lineSpacing = 8;

        // разбор: только если нужна обратная связь или это повторный тест (после игры показываем правильные ответы)
        if (cfg.showReview)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < r.items.Count; i++)
            {
                if (correct[i]) continue;
                sb.Append("<color=#E07A62>×</color> ").Append(r.items[i].question).Append("  →  <color=#E8B04A>").Append(r.items[i].options[0]).Append("</color>\n");
            }
            if (sb.Length > 0)
            {
                MurasUI.Text(body.transform, Loc.T("Правильные ответы на ошибки:", "Correct answers to your mistakes:"), 20, MurasUI.Gold,
                             new Vector2(0, 30), new Vector2(1040, 30), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
                var m = MurasUI.Text(body.transform, sb.ToString(), 20, MurasUI.Cream, new Vector2(0, -85), new Vector2(1040, 200), FontStyles.Normal, TextAlignmentOptions.TopLeft);
                m.enableAutoSizing = true; m.fontSizeMin = 14; m.fontSizeMax = 20;
            }
        }
        MurasUI.Button(body.transform, Loc.T("Продолжить", "Continue"), new Vector2(0, -240), new Vector2(300, 64), Close, 26);
    }

    void Update()
    {
        if (!ReferenceEquals(Modal.Current, this)) Modal.Open(this);      // Esc посреди теста не должен «отвязать» окно
        if (index < 0 && Input.GetKeyDown(KeyCode.Return)) { Begin(); return; }
        if (index >= 0 && index < cfg.items.Count && !locked)
            for (int k = 0; k < 4 && k < optionButtons.Count; k++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + k) || Input.GetKeyDown(KeyCode.Keypad1 + k)) { Answer(k); return; }
        if (index >= cfg.items.Count && Input.GetKeyDown(KeyCode.Return)) Close();
    }

    public void CloseModal()
    {
        // Esc: во время теста — спросить нельзя, просто считаем пропуском только до начала
        if (index < 0) { if (cfg.allowSkip) Skip(); else Close(); }
        else if (index >= cfg.items.Count) Close();
    }

    void Close()
    {
        Modal.Close(this);
        if (!PauseMenu.IsPaused) Time.timeScale = 1f;
        Current = null;
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (Current == this) { Current = null; Modal.Close(this); if (!PauseMenu.IsPaused) Time.timeScale = 1f; }
    }
}
