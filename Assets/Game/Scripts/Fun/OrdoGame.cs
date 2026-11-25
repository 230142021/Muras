using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ордо — древняя кыргызская игра в альчики. В большом круге (ордо) стоят в ряд альчики, в центре — «хан».
/// Игроки по очереди бросают свою битку (сака) из-за круга: альчики, выбитые за круг, достаются бросившему,
/// хан стоит 3 очка. Турнир с детьми Бала и Кыз: по 4 броска на каждого, у кого больше — победитель.
/// Управление: A/D — сдвинуть место броска, мышь — прицел, ЛКМ или пробел — бросок (сила по полоске).
/// </summary>
public class OrdoGame : MonoBehaviour, IModal
{
    public static OrdoGame Instance { get; private set; }
    public const int FirstWinXp = 100, RepeatWinXp = 20, Throws = 4;
    const float R = 300f, PieceR = 13f, SakaR = 18f, LaunchY = -370f;

    class Piece { public RectTransform rt; public Vector2 p, v; public float r, m; public int value; public bool alive = true, saka; public Image img; }

    private GameObject canvas, root;
    private RectTransform field, aimLine, powerFill, thrower;
    private TMP_Text turnText, scoreText, infoText;
    private Button closeBtn, againBtn;
    private readonly List<Piece> pieces = new List<Piece>();
    private Piece saka;
    private Sprite dot;

    private readonly string[] names = { "Сен", "Бала", "Кыз" };
    private readonly int[] score = new int[3], throwsLeft = new int[3];
    private int turn;
    private bool open, flying, over;
    private float launchX, power, powerDir = 1f, aiAt;
    private Vector2 aimDir = Vector2.up;

    public static OrdoGame Ensure()
    {
        if (Instance == null) new GameObject("OrdoGame").AddComponent<OrdoGame>();
        return Instance;
    }

    void Awake() { Instance = this; }
    void OnDestroy() { if (open) { Modal.Close(this); if (!PauseMenu.IsPaused) Time.timeScale = 1f; } }

    public void Open()
    {
        if (root == null) Build();
        open = true; Modal.Open(this); Time.timeScale = 0f;
        root.SetActive(true);
        NewGame();
        Vocab.Unlock("ordo", "saka", "han");
    }

    public void Close()
    {
        if (!open) return;
        open = false; root.SetActive(false); Modal.Close(this);
        if (!PauseMenu.IsPaused) Time.timeScale = 1f;
    }

    public void CloseModal() => Close();

    // ------------------------------------------------------------------ окно

    void Build()
    {
        var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32, 32));
            tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(31.5f - d)));
        }
        tex.Apply(); dot = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));

        canvas = MurasUI.Canvas("OrdoCanvas", transform, 470);
        root = MurasUI.Overlay(canvas.transform, 0.78f);
        var win = MurasUI.WindowBox(root.transform, new Vector2(1400, 900));
        closeBtn = MurasUI.CloseButton(win.transform, Close);
        MurasUI.Text(win.transform, "ОРДО", 44, MurasUI.Gold, new Vector2(-260, 400), new Vector2(800, 60), FontStyles.Bold);

        // поле: утоптанная земля, белый круг
        var ground = MurasUI.Box("Field", win.transform, new Vector2(-260, -20), new Vector2(820, 820), new Color(0.42f, 0.33f, 0.22f));
        field = (RectTransform)ground.transform;
        var circle = MurasUI.Box("Circle", field, Vector2.zero, new Vector2(R * 2 + 8, R * 2 + 8), new Color(0.95f, 0.93f, 0.86f));
        circle.GetComponent<Image>().sprite = dot;
        var inner = MurasUI.Box("Inner", field, Vector2.zero, new Vector2(R * 2 - 8, R * 2 - 8), new Color(0.50f, 0.39f, 0.27f));
        inner.GetComponent<Image>().sprite = dot;
        var line = MurasUI.Box("LaunchLine", field, new Vector2(0, LaunchY), new Vector2(560, 4), new Color(0.95f, 0.93f, 0.86f, 0.5f));
        aimLine = (RectTransform)MurasUI.Box("Aim", field, Vector2.zero, new Vector2(4, 140), new Color(1f, 0.85f, 0.4f, 0.8f)).transform;
        aimLine.pivot = new Vector2(0.5f, 0f);
        thrower = (RectTransform)MurasUI.Box("Thrower", field, new Vector2(0, LaunchY), new Vector2(26, 26), new Color(0.9f, 0.3f, 0.25f)).transform;
        thrower.GetComponent<Image>().sprite = dot;

        // правая панель
        scoreText = MurasUI.Text(win.transform, "", 30, MurasUI.Cream, new Vector2(430, 220), new Vector2(460, 240), FontStyles.Normal, TextAlignmentOptions.TopLeft);
        turnText = MurasUI.Text(win.transform, "", 30, MurasUI.Gold, new Vector2(430, 340), new Vector2(460, 80), FontStyles.Bold, TextAlignmentOptions.TopLeft);
        var bar = MurasUI.Box("Power", win.transform, new Vector2(430, 40), new Vector2(420, 30), MurasUI.Card);
        var fill = MurasUI.Rect("Fill", bar.transform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(3, 0), new Vector2(0, -6));
        fill.AddComponent<Image>().color = MurasUI.Gold;
        powerFill = (RectTransform)fill.transform; powerFill.pivot = new Vector2(0f, 0.5f);
        MurasUI.Text(bar.transform, Loc.T("сила броска", "throw power"), 16, MurasUI.Cream, new Vector2(0, 28), new Vector2(420, 24));
        infoText = MurasUI.Text(win.transform, "", 21, MurasUI.Cream, new Vector2(430, -170), new Vector2(460, 330), FontStyles.Normal, TextAlignmentOptions.TopLeft);
        againBtn = MurasUI.Button(win.transform, Loc.T("Дагы бир жолу", "Play again"), new Vector2(430, -380), new Vector2(300, 60), NewGame);
        root.SetActive(false);
    }

    Piece Spawn(Vector2 p, float r, float m, int value, Color c, bool isSaka = false)
    {
        var go = MurasUI.Box(isSaka ? "Saka" : "Alchik", field, p, new Vector2(r * 2, r * 2), c);
        var img = go.GetComponent<Image>(); img.sprite = isSaka ? dot : null;
        var pc = new Piece { rt = (RectTransform)go.transform, p = p, r = r, m = m, value = value, saka = isSaka, img = img };
        pieces.Add(pc);
        return pc;
    }

    void NewGame()
    {
        foreach (Piece pc in pieces) if (pc.rt != null) Destroy(pc.rt.gameObject);
        pieces.Clear();
        for (int k = -4; k <= 4; k++)
        {
            if (k == 0) continue;
            Spawn(new Vector2(k * 46f, 0f), PieceR, 1f, 1, new Color(0.95f, 0.90f, 0.78f));
        }
        Spawn(Vector2.zero, PieceR + 4f, 1.4f, 3, new Color(0.85f, 0.22f, 0.20f));        // хан
        for (int i = 0; i < 3; i++) { score[i] = 0; throwsLeft[i] = Throws; }
        turn = 0; over = false; flying = false; saka = null; launchX = 0f;
        againBtn.gameObject.SetActive(false);
        infoText.text = Loc.T("<b>Как играть</b>\nA / D — встать левее или правее\nМышь — прицел\nЛКМ или пробел — бросить саку, когда полоска силы хорошая\n\nВыбей альчики за белый круг — они твои. <color=#E06050>Хан</color> в центре — 3 очка.\nУ каждого 4 броска.",
                              "<b>How to play</b>\nA / D — step left or right\nMouse — aim\nLMB or Space — throw the saka when the power bar is good\n\nKnock alchiks out of the white circle — they're yours. The <color=#E06050>khan</color> in the middle is worth 3.\nEveryone has 4 throws.");
        UpdateTexts();
    }

    void UpdateTexts()
    {
        string s = "";
        for (int i = 0; i < 3; i++)
            s += (i == turn && !over ? "<color=#E8B04A>▶ </color>" : "   ") + "<b>" + names[i] + "</b>  —  " + score[i] + Loc.T(" очк.", " pts") + "  <size=75%><color=#C9BFA6>(" + Loc.T("бросков: ", "throws: ") + throwsLeft[i] + ")</color></size>\n";
        scoreText.text = s.Replace("▶ ", "> ");
        turnText.text = over ? Loc.T("Турнир окончен", "Tournament over") : turn == 0 ? Loc.T("Твой бросок!", "Your throw!") : names[turn] + Loc.T(" бросает…", " throws…");
    }

    // ------------------------------------------------------------------ игра

    void Update()
    {
        if (!open) return;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.033f);

        if (flying) { Simulate(dt); return; }
        if (over) return;

        if (turn == 0)
        {
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) launchX -= 260f * dt;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) launchX += 260f * dt;
            launchX = Mathf.Clamp(launchX, -270f, 270f);
            Vector2 from = new Vector2(launchX, LaunchY);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(field, Input.mousePosition, null, out Vector2 m))
            {
                Vector2 d = m - from; if (d.y > 30f) aimDir = d.normalized;
            }
            power += powerDir * dt * 1.1f;
            if (power > 1f) { power = 1f; powerDir = -1f; }
            if (power < 0.15f) { power = 0.15f; powerDir = 1f; }
            if (Input.GetKeyDown(KeyCode.Space) || (Input.GetMouseButtonDown(0) && RectTransformUtility.RectangleContainsScreenPoint(field, Input.mousePosition, null))) Throw(from, aimDir, power);
        }
        else if (Time.unscaledTime > aiAt)
        {
            // ребёнок выбирает альчик и целится (с ошибкой)
            var targets = pieces.FindAll(p => p.alive && !p.saka);
            if (targets.Count == 0) { EndTournament(); return; }
            Piece t = targets[Random.Range(0, targets.Count)];
            if (Random.value < 0.35f) t = targets.Find(p => p.value == 3) ?? t;
            launchX = Mathf.Clamp(t.p.x + Random.Range(-120f, 120f), -270f, 270f);
            Vector2 from = new Vector2(launchX, LaunchY);
            float err = (turn == 1 ? 6f : 9f) * Mathf.Deg2Rad * Random.Range(-1f, 1f);
            Vector2 dir = (t.p - from).normalized;
            dir = new Vector2(dir.x * Mathf.Cos(err) - dir.y * Mathf.Sin(err), dir.x * Mathf.Sin(err) + dir.y * Mathf.Cos(err));
            Throw(from, dir, Random.Range(0.62f, 0.95f));
        }

        thrower.anchoredPosition = new Vector2(launchX, LaunchY);
        thrower.GetComponent<Image>().color = turn == 0 ? new Color(0.9f, 0.3f, 0.25f) : turn == 1 ? new Color(0.3f, 0.55f, 0.9f) : new Color(0.75f, 0.35f, 0.75f);
        aimLine.gameObject.SetActive(turn == 0);
        aimLine.anchoredPosition = new Vector2(launchX, LaunchY);
        aimLine.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(aimDir.x, aimDir.y) * Mathf.Rad2Deg);
        aimLine.sizeDelta = new Vector2(4f, 60f + power * 160f);
        powerFill.sizeDelta = new Vector2(414f * (turn == 0 ? power : 0f), -6f);
    }

    void Throw(Vector2 from, Vector2 dir, float p)
    {
        if (saka != null && saka.rt != null) { pieces.Remove(saka); Destroy(saka.rt.gameObject); }
        saka = Spawn(from, SakaR, 2.2f, 0, new Color(0.55f, 0.38f, 0.22f), true);
        saka.v = dir.normalized * Mathf.Lerp(420f, 1250f, p);
        throwsLeft[turn]--;
        flying = true;
        MurasUI.Play("tile_turn", 0.7f, 1.3f);
    }

    void Simulate(float dt)
    {
        int sub = 4; float h = dt / sub;
        for (int s = 0; s < sub; s++)
        {
            foreach (Piece a in pieces)
            {
                if (!a.alive && !a.saka) continue;
                a.p += a.v * h;
                float sp = a.v.magnitude;
                float dec = (a.saka ? 520f : 640f) * h;
                a.v = sp > dec ? a.v * ((sp - dec) / sp) : Vector2.zero;
            }
            for (int i = 0; i < pieces.Count; i++)
                for (int j = i + 1; j < pieces.Count; j++)
                {
                    Piece a = pieces[i], b = pieces[j];
                    if ((!a.alive && !a.saka) || (!b.alive && !b.saka)) continue;
                    Vector2 d = b.p - a.p; float dist = d.magnitude, min = a.r + b.r;
                    if (dist <= 0.01f || dist >= min) continue;
                    Vector2 n = d / dist;
                    float rel = Vector2.Dot(a.v - b.v, n);
                    float push = (min - dist) * 0.5f; a.p -= n * push; b.p += n * push;
                    if (rel <= 0f) continue;
                    float imp = 2f * rel / (a.m + b.m) * 0.92f;
                    a.v -= imp * b.m * n; b.v += imp * a.m * n;
                    MurasUI.Play("tile_turn", 0.35f, 1.6f);
                }
        }
        bool moving = false;
        foreach (Piece pc in pieces)
        {
            if (pc.rt != null) pc.rt.anchoredPosition = pc.p;
            if (pc.v.sqrMagnitude > 4f) moving = true;
            if (!pc.saka && pc.alive && pc.p.magnitude > R + pc.r * 0.3f)
            {
                pc.alive = false; score[turn] += pc.value;
                pc.img.color = new Color(pc.img.color.r, pc.img.color.g, pc.img.color.b, 0.35f);
                MurasUI.Play(pc.value == 3 ? "achievement" : "word_chime", 0.5f, 1.2f);
                UpdateTexts();
            }
            if (Mathf.Abs(pc.p.x) > 400f || Mathf.Abs(pc.p.y) > 400f) pc.v = Vector2.zero;
        }
        if (!moving) NextTurn();
    }

    void NextTurn()
    {
        flying = false;
        bool anyLeft = pieces.Exists(p => p.alive && !p.saka);
        if (!anyLeft || (throwsLeft[0] + throwsLeft[1] + throwsLeft[2]) <= 0) { EndTournament(); return; }
        do { turn = (turn + 1) % 3; } while (throwsLeft[turn] <= 0);
        aiAt = Time.unscaledTime + 1.1f;
        UpdateTexts();
    }

    void EndTournament()
    {
        over = true; flying = false;
        int best = Mathf.Max(score[0], Mathf.Max(score[1], score[2]));
        bool win = score[0] == best && score[0] > 0;
        bool first = win && SaveData.Get("ordo_win") == 0;
        int xp = win ? (first ? FirstWinXp : RepeatWinXp) : 8;
        if (win) { SaveData.Add("ordo_win", 1); Achievements.Give("ordo"); GameAudio.QuestComplete(); }
        MurasCloud.ReportScore("ordo", score[0]);
        if (GameManager.Instance != null) GameManager.Instance.AddXP(xp);
        infoText.text = (win ? Loc.T("<b>Жеңиш! Ты — хан ордо!</b>\n\nБала: «Ого, вот это бросок! Ты играешь как настоящий ордочу!»",
                                     "<b>Victory! You are the khan of ordo!</b>\n\nBala: \"Wow, what a throw! You play like a real ordo master!\"")
                             : Loc.T("<b>В этот раз победили дети.</b>\n\nКыз: «Ничего! Целься в хана — он стоит три очка.»",
                                     "<b>The kids won this time.</b>\n\nKyz: \"No matter! Aim at the khan — it's worth three points.\""))
                        + "\n\n<color=#5FC873>+" + xp + " XP</color>\n\n<size=85%><color=#C9BFA6>" +
                        Loc.T("Ордо — одна из древнейших кыргызских игр. «Ордо» значит «ставка хана»: потому и главный альчик в центре называется хан.",
                              "Ordo is one of the oldest Kyrgyz games. \"Ordo\" means \"the khan's camp\" — that's why the main alchik in the middle is called the khan.") + "</color></size>";
        againBtn.gameObject.SetActive(true);
        UpdateTexts();
    }
}

/// <summary>Круг ордо у игровой поляны: E — сыграть с детьми.</summary>
public class OrdoStation : MonoBehaviour
{
    public float radius = 3.2f;
    private Transform player;
    void Start() { var pm = FindAnyObjectByType<PlayerMovement>(); if (pm != null) player = pm.transform; }
    void Update()
    {
        if (player == null || !Modal.Free || PauseMenu.IsPaused) return;
        Vector3 d = player.position - transform.position; d.y = 0f;
        if (d.sqrMagnitude > radius * radius) return;
        InteractionHint.Request("<b><color=#E8B04A>E</color></b>  —  " + Loc.T("сыграть в ордо с детьми", "play ordo with the kids"));
        if (Input.GetKeyDown(KeyCode.E)) OrdoGame.Ensure().Open();
    }
}
