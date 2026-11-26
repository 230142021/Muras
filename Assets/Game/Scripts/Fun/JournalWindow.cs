using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Окно «Сөздүк» (клавиша J): выученные кыргызские слова по темам и вкладка достижений.
/// Неоткрытые слова показаны как «? ? ?» с подсказкой, где их найти. Создаётся само в игровых сценах.
/// </summary>
public class JournalWindow : MonoBehaviour, IModal
{
    public static JournalWindow Instance { get; private set; }
    public KeyCode key = KeyCode.J;

    private GameObject canvas, root, content;
    private TMP_Text counter;
    private int tab;                       // 0 — словарь, 1 — достижения
    private Vocab.Cat cat = Vocab.Cat.Salam;
    private bool open;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnLoaded(Scene s, LoadSceneMode m)
    {
        if (s.name == PauseMenu.MenuScene || FindAnyObjectByType<JournalWindow>() != null) return;
        new GameObject("JournalWindow").AddComponent<JournalWindow>();
    }

    void Awake() => Instance = this;

    void Start()
    {
        Build();
        Loc.Changed += Rebuild;
        Vocab.Changed += Refresh;
        Achievements.Changed += Refresh;
    }

    void OnDestroy()
    {
        Loc.Changed -= Rebuild; Vocab.Changed -= Refresh; Achievements.Changed -= Refresh;
        if (open) { Modal.Close(this); if (!PauseMenu.IsPaused) Time.timeScale = 1f; }
    }

    void Update()
    {
        if (!Input.GetKeyDown(key)) return;
        if (open) Close();
        else if (Modal.Free) Open();
    }

    public void Open()
    {
        open = true;
        Modal.Open(this);
        root.SetActive(true);
        Time.timeScale = 0f;
        MurasUI.Play("tile_turn", 0.6f);
        Refresh();
    }

    public void Close()
    {
        if (!open) return;
        open = false;
        root.SetActive(false);
        Modal.Close(this);
        if (!PauseMenu.IsPaused) Time.timeScale = 1f;
    }

    public void CloseModal() => Close();

    void Rebuild()
    {
        bool was = open;
        if (canvas != null) Destroy(canvas);
        Build();
        if (was) { root.SetActive(true); Refresh(); }
    }

    void Build()
    {
        canvas = MurasUI.Canvas("JournalCanvas", transform, 455);
        root = MurasUI.Overlay(canvas.transform);
        var win = MurasUI.WindowBox(root.transform, new Vector2(1320, 880));
        var w = win.transform;
        MurasUI.Text(w, "СӨЗДҮК", 52, MurasUI.Gold, new Vector2(-430, 392), new Vector2(420, 64), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        counter = MurasUI.Text(w, "", 20, MurasUI.Muted, new Vector2(-180, 350), new Vector2(920, 30), FontStyles.Italic, TextAlignmentOptions.MidlineLeft);
        MurasUI.CloseButton(w, Close);

        MurasUI.Button(w, Loc.T("Слова", "Words"), new Vector2(330, 392), new Vector2(170, 46), () => { tab = 0; Refresh(); }, 20);
        MurasUI.Button(w, Loc.T("Достижения", "Achievements"), new Vector2(510, 392), new Vector2(170, 46), () => { tab = 1; Refresh(); }, 18);

        content = MurasUI.Rect("Content", w, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(1260, 740));
        MurasUI.Text(w, Loc.T("J или Esc — закрыть  ·  новые слова дают +5 XP, достижения +25 XP", "J or Esc — close  ·  new words give +5 XP, achievements +25 XP"),
                     16, MurasUI.Muted, new Vector2(0, -418), new Vector2(1200, 26), FontStyles.Italic);
        root.SetActive(false);
    }

    void Refresh()
    {
        if (content == null) return;
        foreach (Transform c in content.transform) Destroy(c.gameObject);
        counter.text = Loc.T("Сөздөр: ", "Words: ") + Vocab.KnownCount + "/" + Vocab.Total +
                       "   ·   " + Loc.T("Жетишкендиктер: ", "Achievements: ") + Achievements.Count + "/" + Achievements.All.Count;
        if (tab == 0) WordsTab(); else AchTab();
    }

    void WordsTab()
    {
        var t = content.transform;
        int i = 0;
        foreach (Vocab.Cat c in System.Enum.GetValues(typeof(Vocab.Cat)))
        {
            int known = 0, total = 0;
            foreach (Vocab.Word wd in Vocab.Words) if (wd.cat == c) { total++; if (Vocab.Known(wd.id)) known++; }
            Vocab.Cat captured = c;
            var b = MurasUI.Button(t, Vocab.CatName(c) + "  " + known + "/" + total, new Vector2(-500, 300 - i * 62), new Vector2(250, 54), () => { cat = captured; Refresh(); }, 17);
            if (c == cat) b.GetComponent<Image>().color = MurasUI.Gold * 0.9f;
            i++;
        }

        // самопроверка и тест знаний (до / после игры)
        var review = MurasUI.Button(t, Loc.T("Проверь себя", "Check yourself"), new Vector2(-500, -228), new Vector2(250, 54), () => { Close(); WordReview.Start(); }, 18);
        review.GetComponent<Image>().color = new Color(0.36f, 0.50f, 0.27f);
        review.interactable = WordReview.CanStart;
        string testLabel = !KnowledgeTest.PreDone ? Loc.T("Тест знаний", "Knowledge test")
                         : KnowledgeTest.PostDone ? Loc.T("Тест знаний ещё раз", "Retake the test") : Loc.T("Тест после игры", "Test after playing");
        MurasUI.Button(t, testLabel, new Vector2(-500, -292), new Vector2(250, 54), () =>
        {
            Close();
            if (!KnowledgeTest.PreDone) KnowledgeTest.StartPre(); else KnowledgeTest.StartPost();
        }, 17).GetComponent<Image>().color = new Color(0.42f, 0.30f, 0.18f);
        string scores = KnowledgeTest.PreTaken ? Loc.T("Тест: до ", "Test: before ") + KnowledgeTest.PreScore + (KnowledgeTest.PostDone ? Loc.T(" → после ", " → after ") + KnowledgeTest.PostScore : "") + " / " + KnowledgeTest.Total : "";
        MurasUI.Text(t, scores + (scores != "" ? "\n" : "") + Loc.T("Озвучено слов: ", "Words with audio: ") + WordAudio.RecordedCount + "/" + Vocab.Total,
                     15, MurasUI.Muted, new Vector2(-500, -350), new Vector2(250, 48), FontStyles.Italic);

        const int cols = 4; const float cw = 232f, ch = 104f, gap = 10f;
        float x0 = -630f + 280f + cw / 2f, y0 = 300f;
        int k = 0;
        foreach (Vocab.Word wd in Vocab.Words)
        {
            if (wd.cat != cat) continue;
            int col = k % cols, row = k / cols; k++;
            bool known = Vocab.Known(wd.id);
            var card = MurasUI.Box("Word_" + wd.id, t, new Vector2(x0 + col * (cw + gap), y0 - row * (ch + gap)), new Vector2(cw, ch), known ? MurasUI.Card : new Color(0.14f, 0.11f, 0.08f));
            var ct = card.transform;
            var strip = MurasUI.Rect("Strip", ct, new Vector2(0, 0), new Vector2(0, 1), new Vector2(4, 0), new Vector2(8, 0));
            strip.AddComponent<Image>().color = known ? MurasUI.Gold : new Color(0.3f, 0.25f, 0.2f);
            if (known)
            {
                bool audio = WordAudio.Has(wd.id);
                var kyText = MurasUI.Text(ct, wd.ky, 24, MurasUI.Cream, audio ? new Vector2(-12, 30) : new Vector2(8, 30), audio ? new Vector2(160, 32) : new Vector2(200, 32), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
                kyText.enableAutoSizing = true; kyText.fontSizeMin = 16; kyText.fontSizeMax = 24;
                if (audio)
                {
                    string wid = wd.id;
                    var play = MurasUI.Button(ct, "►", new Vector2(92, 30), new Vector2(34, 30), () => WordAudio.Play(wid), 16);
                    play.GetComponent<Image>().color = new Color(0.42f, 0.30f, 0.16f);
                }
                MurasUI.Text(ct, "[" + wd.latin + "]", 14, MurasUI.Muted, new Vector2(8, 9), new Vector2(200, 20), FontStyles.Italic, TextAlignmentOptions.MidlineLeft);
                var tr = MurasUI.Text(ct, Loc.T(wd.ru, wd.en), 15, MurasUI.Gold, new Vector2(8, -28), new Vector2(206, 46), FontStyles.Normal, TextAlignmentOptions.TopLeft);
                tr.enableAutoSizing = true; tr.fontSizeMin = 11; tr.fontSizeMax = 15;
            }
            else
            {
                MurasUI.Text(ct, "? ? ?", 26, new Color(0.45f, 0.4f, 0.33f), new Vector2(8, 22), new Vector2(200, 34), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
                var h = MurasUI.Text(ct, Loc.T(wd.hintRu, wd.hintEn), 14, MurasUI.Muted, new Vector2(8, -22), new Vector2(206, 44), FontStyles.Italic, TextAlignmentOptions.TopLeft);
                h.enableAutoSizing = true; h.fontSizeMin = 10; h.fontSizeMax = 14;
            }
        }
    }

    void AchTab()
    {
        var t = content.transform;
        for (int i = 0; i < Achievements.All.Count; i++)
        {
            Achievements.Ach a = Achievements.All[i];
            bool has = Achievements.Has(a.id);
            int col = i % 2, row = i / 2;
            var card = MurasUI.Box("Ach_" + a.id, t, new Vector2(-305 + col * 620, 300 - row * 100), new Vector2(600, 88), has ? MurasUI.Card : new Color(0.14f, 0.11f, 0.08f));
            var ct = card.transform;
            var icon = MurasUI.Box("Icon", ct, new Vector2(-255, 0), new Vector2(60, 60), has ? MurasUI.Green : new Color(0.3f, 0.25f, 0.2f));
            MurasUI.Text(icon.transform, has ? "+" : "?", 34, MurasUI.Cream, Vector2.zero, new Vector2(60, 60), FontStyles.Bold);
            MurasUI.Text(ct, a.ky + "  <size=70%><color=#C9BFA6>" + Loc.T(a.ru, a.en) + "</color></size>", 24, has ? MurasUI.Cream : MurasUI.Muted,
                         new Vector2(45, 16), new Vector2(480, 34), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            MurasUI.Text(ct, Loc.T(a.descRu, a.descEn), 16, has ? MurasUI.Gold : MurasUI.Muted, new Vector2(45, -18), new Vector2(480, 26), FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        }
    }
}
