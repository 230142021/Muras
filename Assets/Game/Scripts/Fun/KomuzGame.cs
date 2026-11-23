using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ритм-игра на комузе: ноты падают по трём струнам, нажимай A / S / D (или ← ↓ →), когда нота
/// касается линии. Каждое попадание звучит струной комуза нужной высоты. Музыка синхронизирована
/// по аудио-часам (AudioSettings.dspTime), поэтому ноты не «уплывают».
/// Открывается у комузчу Нурлана (KomuzMusician). Награда — XP за первое прохождение и рекорды.
/// </summary>
public class KomuzGame : MonoBehaviour, IModal
{
    public static KomuzGame Instance { get; private set; }

    const float Travel = 1.6f;          // сколько секунд нота летит до линии
    const float Perfect = 0.075f, Good = 0.15f;
    static readonly KeyCode[][] Keys = { new[] { KeyCode.A, KeyCode.LeftArrow }, new[] { KeyCode.S, KeyCode.DownArrow }, new[] { KeyCode.D, KeyCode.RightArrow } };
    static readonly Color[] LaneCol = { new Color(0.86f, 0.36f, 0.30f), new Color(0.84f, 0.66f, 0.29f), new Color(0.36f, 0.56f, 0.86f) };

    private GameObject canvas, root, menu, play, result;
    private RectTransform[] laneRects = new RectTransform[3];
    private Image[] laneFlash = new Image[3];
    private TMP_Text scoreText, comboText, judgeText, titleText, resultText;
    private AudioSource music;
    private AudioSource[] plucks;
    private int pluckIdx;
    private AudioClip pluckClip, missClip;

    private KomuzCharts.Song song;
    private double startDsp;
    private int nextSpawn, score, combo, maxCombo, perfects, goods, misses;
    private readonly List<Note> active = new List<Note>();
    private bool playing, open;
    private float judgeAt = -10f;

    class Note { public int i; public RectTransform rt; public bool done; }

    void Awake()
    {
        Instance = this;
        music = gameObject.AddComponent<AudioSource>();
        music.playOnAwake = false; music.spatialBlend = 0f; music.ignoreListenerPause = true;
        plucks = new AudioSource[6];
        for (int i = 0; i < plucks.Length; i++)
        {
            plucks[i] = gameObject.AddComponent<AudioSource>();
            plucks[i].playOnAwake = false; plucks[i].spatialBlend = 0f; plucks[i].ignoreListenerPause = true;
        }
        pluckClip = MurasUI.Clip("komuz_pluck");
        missClip = MurasUI.Clip("komuz_miss");
    }

    void Start() { if (canvas == null) Build(); Loc.Changed += Rebuild; }
    void OnDestroy() { Loc.Changed -= Rebuild; if (open) { Modal.Close(this); if (!PauseMenu.IsPaused) Time.timeScale = 1f; } }

    public static KomuzGame Ensure()
    {
        if (Instance == null) new GameObject("KomuzGame").AddComponent<KomuzGame>();
        return Instance;
    }

    public void Open()
    {
        if (root == null) Build();
        open = true;
        Modal.Open(this);
        Time.timeScale = 0f;
        root.SetActive(true);
        ShowMenu();
    }

    public void Close()
    {
        if (!open) return;
        open = false; playing = false;
        music.Stop();
        ClearNotes();
        root.SetActive(false);
        Modal.Close(this);
        if (!PauseMenu.IsPaused) Time.timeScale = 1f;
    }

    public void CloseModal() => Close();

    void Rebuild() { bool was = open; if (canvas != null) Destroy(canvas); Build(); if (was) { root.SetActive(true); ShowMenu(); } }

    // ------------------------------------------------------------------ экраны

    void Build()
    {
        canvas = MurasUI.Canvas("KomuzCanvas", transform, 460);
        root = MurasUI.Overlay(canvas.transform, 0.7f);
        var win = MurasUI.WindowBox(root.transform, new Vector2(900, 900));
        MurasUI.CloseButton(win.transform, Close);
        titleText = MurasUI.Text(win.transform, "КОМУЗ", 48, MurasUI.Gold, new Vector2(0, 400), new Vector2(800, 60), FontStyles.Bold);

        // меню выбора мелодии
        menu = MurasUI.Rect("Menu", win.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860, 760));
        MurasUI.Text(menu.transform, Loc.T("Нурлан: Комуз — душа кыргыза. Сыграй со мной күү! Выбери мелодию:",
                                           "Nurlan: The komuz is the soul of a Kyrgyz. Play a küü with me! Choose a melody:"),
                     22, MurasUI.Cream, new Vector2(0, 300), new Vector2(780, 70), FontStyles.Italic);
        for (int s = 0; s < KomuzCharts.Songs.Length; s++)
        {
            KomuzCharts.Song sg = KomuzCharts.Songs[s];
            int stars = SaveData.Get("komuz_" + sg.id);
            string label = sg.nameKy + "\n<size=60%>" + Loc.T(sg.nameRu, sg.nameEn) + "   " + Stars(stars) + "</size>";
            int captured = s;
            MurasUI.Button(menu.transform, label, new Vector2(0, 160 - s * 140), new Vector2(560, 116), () => StartSong(captured), 30);
        }
        MurasUI.Text(menu.transform, Loc.T("Как играть: ноты падают по трём струнам.\nНажимай <b><color=#E8B04A>A  S  D</color></b> (или стрелки <b><color=#E8B04A>влево · вниз · вправо</color></b>), когда нота касается золотой линии.\n1 / 2 — выбрать мелодию, Esc — выйти.",
                                           "How to play: notes fall along three strings.\nPress <b><color=#E8B04A>A  S  D</color></b> (or the <b><color=#E8B04A>left · down · right</color></b> arrows) when a note touches the gold line.\n1 / 2 — choose a melody, Esc — leave."),
                     20, MurasUI.Muted, new Vector2(0, -190), new Vector2(780, 120));
        MurasUI.Text(menu.transform, Loc.T("<b>Комуз</b> — трёхструнный инструмент из абрикосового дерева. <b>Күү</b> — мелодия без слов, которая «рассказывает» историю: о коне, о горах, о жайлоо.",
                                           "The <b>komuz</b> is a three-stringed instrument carved from apricot wood. A <b>küü</b> is a melody without words that \"tells\" a story: about a horse, the mountains, the jailoo."),
                     18, MurasUI.Gold, new Vector2(0, -310), new Vector2(780, 80), FontStyles.Italic);

        // игровое поле
        play = MurasUI.Rect("Play", win.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(860, 780));
        for (int l = 0; l < 3; l++)
        {
            var lane = MurasUI.Box("Lane" + l, play.transform, new Vector2((l - 1) * 150, 20), new Vector2(130, 680), new Color(0.16f, 0.12f, 0.09f));
            laneRects[l] = (RectTransform)lane.transform;
            MurasUI.Box("String", lane.transform, Vector2.zero, new Vector2(4, 680), new Color(0.9f, 0.88f, 0.8f, 0.35f));
            var flash = MurasUI.Box("Flash", lane.transform, new Vector2(0, -300), new Vector2(130, 40), LaneCol[l]);
            laneFlash[l] = flash.GetComponent<Image>();
            laneFlash[l].color = new Color(LaneCol[l].r, LaneCol[l].g, LaneCol[l].b, 0f);
            string[] k = { "A", "S", "D" };
            MurasUI.Text(lane.transform, k[l], 26, MurasUI.Cream, new Vector2(0, -360), new Vector2(120, 34), FontStyles.Bold);
        }
        MurasUI.Box("HitLine", play.transform, new Vector2(0, -280), new Vector2(460, 6), MurasUI.Gold);
        scoreText = MurasUI.Text(play.transform, "", 30, MurasUI.Gold, new Vector2(330, 300), new Vector2(200, 40), FontStyles.Bold);
        comboText = MurasUI.Text(play.transform, "", 24, MurasUI.Cream, new Vector2(330, 255), new Vector2(200, 34));
        judgeText = MurasUI.Text(play.transform, "", 36, MurasUI.Cream, new Vector2(0, 120), new Vector2(400, 50), FontStyles.Bold);

        // результат
        result = MurasUI.Rect("Result", win.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860, 760));
        resultText = MurasUI.Text(result.transform, "", 30, MurasUI.Cream, new Vector2(0, 90), new Vector2(780, 420));
        MurasUI.Button(result.transform, Loc.T("Ещё раз", "Again"), new Vector2(-150, -230), new Vector2(240, 64), () => ShowMenu());
        MurasUI.Button(result.transform, Loc.T("Выйти", "Leave"), new Vector2(150, -230), new Vector2(240, 64), Close);

        root.SetActive(false);
    }

    static string Stars(int n) => "<color=#E8B04A>" + new string('*', Mathf.Clamp(n, 0, 3)) + "</color><color=#7A6E5C>" + new string('*', 3 - Mathf.Clamp(n, 0, 3)) + "</color>";

    void ShowMenu()
    {
        playing = false; music.Stop(); ClearNotes();
        titleText.text = "КОМУЗ";
        menu.SetActive(true); play.SetActive(false); result.SetActive(false);
    }

    public void StartSong(int index)
    {
        song = KomuzCharts.Songs[index];
        AudioClip clip = MurasUI.Clip(song.clip);
        menu.SetActive(false); result.SetActive(false); play.SetActive(true);
        titleText.text = song.nameKy;
        score = combo = maxCombo = perfects = goods = misses = 0;
        nextSpawn = 0; ClearNotes();
        judgeText.text = "";
        UpdateHud();
        startDsp = AudioSettings.dspTime + 0.4;
        if (clip != null) { music.clip = clip; music.PlayScheduled(startDsp); }
        playing = true;
    }

    double SongTime => AudioSettings.dspTime - startDsp;

    void Update()
    {
        if (!open) return;
        if (menu.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) StartSong(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2) && KomuzCharts.Songs.Length > 1) StartSong(1);
            return;
        }
        if (!playing) return;

        double t = SongTime;
        // появление нот
        while (nextSpawn < song.time.Length && song.time[nextSpawn] - t <= Travel)
        {
            int l = song.lane[nextSpawn];
            var go = MurasUI.Box("Note", laneRects[l], new Vector2(0, 400), new Vector2(110, 30), LaneCol[l]);
            active.Add(new Note { i = nextSpawn, rt = (RectTransform)go.transform });
            nextSpawn++;
        }
        // движение и пропуски
        for (int n = active.Count - 1; n >= 0; n--)
        {
            Note note = active[n];
            float dt = (float)(song.time[note.i] - t);
            note.rt.anchoredPosition = new Vector2(0, -300 + dt / Travel * 640f);
            if (dt < -Good) { Miss(); Destroy(note.rt.gameObject); active.RemoveAt(n); }
        }
        // нажатия
        for (int l = 0; l < 3; l++)
            if (Input.GetKeyDown(Keys[l][0]) || Input.GetKeyDown(Keys[l][1])) Press(l, t);

        for (int l = 0; l < 3; l++)
        {
            Color c = laneFlash[l].color; c.a = Mathf.MoveTowards(c.a, 0f, Time.unscaledDeltaTime * 4f); laneFlash[l].color = c;
        }
        judgeText.transform.localScale = Vector3.one * (1f + Mathf.Max(0f, 0.25f - (Time.unscaledTime - judgeAt)) * 1.2f);

        if (nextSpawn >= song.time.Length && active.Count == 0 && t > song.time[song.time.Length - 1] + 1.5f) Finish();
    }

    void Press(int lane, double t)
    {
        laneFlash[lane].color = new Color(LaneCol[lane].r, LaneCol[lane].g, LaneCol[lane].b, 0.55f);
        Note best = null; double bestD = 99;
        foreach (Note n in active)
        {
            if (song.lane[n.i] != lane) continue;
            double d = System.Math.Abs(song.time[n.i] - t);
            if (d < bestD) { bestD = d; best = n; }
        }
        if (best == null || bestD > Good * 1.6)
        {
            PlayPluck(-12, 0.25f, missClip);   // мимо — глухой звук
            combo = 0; UpdateHud();
            return;
        }
        if (bestD <= Perfect) { perfects++; score += 100 + combo * 2; Judge(Loc.T("Мыкты!", "Perfect!"), MurasUI.Gold); }
        else if (bestD <= Good) { goods++; score += 50 + combo; Judge(Loc.T("Жакшы", "Good"), MurasUI.Cream); }
        else { Miss(); }
        if (bestD <= Good)
        {
            combo++; maxCombo = Mathf.Max(maxCombo, combo);
            PlayPluck(song.semi[best.i], 0.9f, pluckClip);
        }
        Destroy(best.rt.gameObject); active.Remove(best);
        UpdateHud();
    }

    void Miss()
    {
        misses++; combo = 0;
        Judge(Loc.T("Мимо", "Miss"), MurasUI.Red);
        UpdateHud();
    }

    void Judge(string txt, Color c) { judgeText.text = txt; judgeText.color = c; judgeAt = Time.unscaledTime; }

    void PlayPluck(int semi, float vol, AudioClip clip)
    {
        if (clip == null) return;
        AudioSource s = plucks[pluckIdx++ % plucks.Length];
        s.pitch = Mathf.Pow(2f, semi / 12f);
        s.PlayOneShot(clip, vol);
    }

    void UpdateHud()
    {
        scoreText.text = score.ToString();
        comboText.text = combo > 2 ? Loc.T("Комбо ×", "Combo ×") + combo : "";
    }

    void ClearNotes()
    {
        foreach (Note n in active) if (n.rt != null) Destroy(n.rt.gameObject);
        active.Clear();
    }

    void Finish()
    {
        playing = false;
        int total = song.time.Length;
        float acc = (perfects + goods * 0.6f) / Mathf.Max(1, total);
        int stars = acc >= 0.92f ? 3 : acc >= 0.78f ? 2 : acc >= 0.55f ? 1 : 0;
        string key = "komuz_" + song.id;
        int prev = SaveData.Get(key);
        int[] xpFor = { 0, 30, 60, 100 };
        int xp = Mathf.Max(0, xpFor[stars] - xpFor[Mathf.Clamp(prev, 0, 3)]);
        if (stars > prev) SaveData.Set(key, stars);
        if (xp > 0 && GameManager.Instance != null) GameManager.Instance.AddXP(xp);
        if (stars >= 1) Vocab.Unlock("komuz", "kuu", "komuzchu");
        if (stars >= 3) Achievements.Give("komuz");

        string verdict = stars == 3 ? Loc.T("Нурлан: Азаматсың! Ты настоящий күүчү!", "Nurlan: Азаматсың! You are a true küüchü!")
                       : stars >= 1 ? Loc.T("Нурлан: Жакшы! Ещё немного — и будешь играть как мастер.", "Nurlan: Жакшы! A little more and you'll play like a master.")
                       : Loc.T("Нурлан: Ничего, комуз любит терпеливых. Попробуй ещё!", "Nurlan: No worries, the komuz loves the patient. Try again!");
        resultText.text = "<size=130%><color=#E8B04A>" + new string('*', stars) + "</color><color=#5A5040>" + new string('*', 3 - stars) + "</color></size>\n" +
                          Loc.T("Очки: ", "Score: ") + score + "\n" +
                          "<size=80%>" + Loc.T("Мыкты: ", "Perfect: ") + perfects + "   " + Loc.T("Жакшы: ", "Good: ") + goods + "   " + Loc.T("Мимо: ", "Miss: ") + misses +
                          "   " + Loc.T("Лучшее комбо: ", "Best combo: ") + maxCombo + "</size>\n" +
                          (xp > 0 ? "<color=#5FC873>+" + xp + " XP</color>\n" : "") +
                          "<size=75%><i>" + verdict + "</i></size>";
        play.SetActive(false); result.SetActive(true);
        MurasUI.Play(stars >= 2 ? "achievement" : "word_chime", 0.6f);
    }
}
