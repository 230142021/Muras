using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Готовим бешбармак у казана. Нужны продукты из капчыка: эт (мясо), ун (мука), пияз (лук) —
/// их можно взять у базара. Пять шагов-мини-игр:
///  1. Этти кайнат — положить мясо и снять пену вовремя (останови стрелку в зелёной зоне);
///  2. Камыр жуур — замесить тесто (быстро жми пробел);
///  3. Камырды кес — нарезать тесто ромбиками (жми, когда нож над меткой);
///  4. Пиязды туура — нарезать лук (чередуй A и D);
///  5. Табакка сал — выложить бешбармак правильно (выбери порядок).
/// Награда — блюдо «Бешбармак» в капчык (сильная бодрость) и XP.
/// </summary>
public class CookingGame : MonoBehaviour, IModal
{
    public static CookingGame Instance { get; private set; }
    public const int FirstXp = 120, RepeatXp = 25;
    static readonly string[] Need = { "et", "un", "piyaz" };

    private GameObject canvas, root;
    private TMP_Text title, desc, status, stepNo;
    private RectTransform barZone, needle, knife, progressFill;
    private GameObject barRoot, choiceRoot, startBtn;
    private Button[] choices = new Button[3];
    private Image dish;
    private bool open;

    private int step = -1, hits, mistakes;
    private float t, needleDir = 1f, zoneMin, zoneMax, lastKey;
    private bool waitingNext;
    private int lastSide;

    public static CookingGame Ensure()
    {
        if (Instance == null) new GameObject("CookingGame").AddComponent<CookingGame>();
        return Instance;
    }

    void Awake() { Instance = this; }
    void OnDestroy() { if (open) { Modal.Close(this); if (!PauseMenu.IsPaused) Time.timeScale = 1f; } }

    public static bool HasIngredients() { foreach (string n in Need) if (Inventory.Count(n) < 1) return false; return true; }

    public void Open()
    {
        if (root == null) Build();
        open = true; Modal.Open(this); Time.timeScale = 0f; root.SetActive(true);
        step = -1; mistakes = 0; waitingNext = false;
        title.text = Loc.T("Бешбармак бышырабыз!", "Let's cook beshbarmak!");
        desc.text = Loc.T("Бешбармак — главное блюдо кыргызского дасторкона: варёное мясо на тонком тесте с луковым соусом «чык». «Беш бармак» значит «пять пальцев» — раньше его ели руками.\n\nПродукты: эт (мясо), ун (мука), пияз (лук).",
                          "Beshbarmak is the main dish of the Kyrgyz дасторкон: boiled meat on thin dough with an onion sauce called \"chyk\". \"Besh barmak\" means \"five fingers\" — it used to be eaten by hand.\n\nIngredients: эт (meat), ун (flour), пияз (onion).");
        status.text = ""; stepNo.text = "";
        barRoot.SetActive(false); choiceRoot.SetActive(false); startBtn.SetActive(true);
        dish.gameObject.SetActive(false);
        Vocab.Unlock("beshbarmak");
    }

    public void Close()
    {
        if (!open) return;
        open = false; root.SetActive(false); Modal.Close(this);
        if (!PauseMenu.IsPaused) Time.timeScale = 1f;
    }

    public void CloseModal() => Close();

    void Build()
    {
        canvas = MurasUI.Canvas("CookingCanvas", transform, 470);
        root = MurasUI.Overlay(canvas.transform, 0.78f);
        var win = MurasUI.WindowBox(root.transform, new Vector2(1200, 760));
        MurasUI.CloseButton(win.transform, Close);
        MurasUI.Text(win.transform, Loc.T("КАЗАН  ·  бешбармак", "KAZAN  ·  beshbarmak"), 22, MurasUI.Gold, new Vector2(0, 340), new Vector2(900, 34), FontStyles.Bold);
        stepNo = MurasUI.Text(win.transform, "", 20, MurasUI.Muted, new Vector2(0, 300), new Vector2(900, 30));
        title = MurasUI.Text(win.transform, "", 40, MurasUI.Cream, new Vector2(0, 240), new Vector2(1000, 60), FontStyles.Bold);
        desc = MurasUI.Text(win.transform, "", 23, MurasUI.Cream, new Vector2(0, 110), new Vector2(980, 200));
        status = MurasUI.Text(win.transform, "", 26, MurasUI.Gold, new Vector2(0, -250), new Vector2(1000, 60), FontStyles.Bold);

        // полоса для таймингов
        barRoot = MurasUI.Rect("Bar", win.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(800, 60));
        barRoot.AddComponent<Image>().color = MurasUI.Card;
        barZone = (RectTransform)MurasUI.Box("Zone", barRoot.transform, Vector2.zero, new Vector2(120, 60), new Color(0.45f, 0.72f, 0.38f, 0.8f)).transform;
        needle = (RectTransform)MurasUI.Box("Needle", barRoot.transform, Vector2.zero, new Vector2(8, 86), MurasUI.Cream).transform;
        knife = (RectTransform)MurasUI.Box("Knife", barRoot.transform, new Vector2(0, 60), new Vector2(18, 50), new Color(0.8f, 0.8f, 0.85f)).transform;
        var pb = MurasUI.Box("Progress", barRoot.transform, new Vector2(0, -60), new Vector2(800, 14), new Color(0.2f, 0.15f, 0.1f));
        var pf = MurasUI.Rect("Fill", pb.transform, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, Vector2.zero);
        pf.AddComponent<Image>().color = MurasUI.Gold; progressFill = (RectTransform)pf.transform; progressFill.pivot = new Vector2(0, 0.5f);

        // выбор
        choiceRoot = MurasUI.Rect("Choices", win.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(1000, 260));
        for (int i = 0; i < 3; i++) { int k = i; choices[i] = MurasUI.Button(choiceRoot.transform, "", new Vector2(0, 80 - i * 80), new Vector2(900, 66), () => Choose(k), 22); }

        startBtn = MurasUI.Button(win.transform, Loc.T("Баштайбыз!", "Let's start!"), new Vector2(0, -150), new Vector2(320, 70), StartCooking, 28).gameObject;
        var d = MurasUI.Box("Dish", win.transform, new Vector2(0, -90), new Vector2(220, 220), Color.white);
        dish = d.GetComponent<Image>();
        Texture2D tx = Resources.Load<Texture2D>("MURAS/Icons/beshbarmak");
        if (tx != null) dish.sprite = Sprite.Create(tx, new Rect(0, 0, tx.width, tx.height), new Vector2(0.5f, 0.5f));
        root.SetActive(false);
    }

    void StartCooking()
    {
        if (!HasIngredients())
        {
            status.text = Loc.T("Нужны эт, ун и пияз! Корзина с продуктами — у базара.", "You need эт, ун and пияз! The grocery basket is by the bazaar.");
            GameAudio.Wrong();
            return;
        }
        foreach (string n in Need) Inventory.Remove(n);
        startBtn.SetActive(false);
        Begin(0);
    }

    void Begin(int s)
    {
        step = s; hits = 0; t = 0f; waitingNext = false; status.text = "";
        stepNo.text = Loc.T("Шаг ", "Step ") + (s + 1) + " / 5";
        barRoot.SetActive(s <= 3); choiceRoot.SetActive(s == 4);
        needle.gameObject.SetActive(s == 0 || s == 2); barZone.gameObject.SetActive(s == 0 || s == 2); knife.gameObject.SetActive(s == 2);
        switch (s)
        {
            case 0:
                title.text = Loc.T("1. Этти кайнат — вари мясо", "1. Этти кайнат — boil the meat");
                desc.text = Loc.T("Мясо кладут в холодную воду и долго варят. Когда закипит, поднимается пена — её нужно снять вовремя, тогда шорпо (бульон) будет прозрачным.\n\n<b>Пробел</b> — снять пену, когда стрелка в зелёной зоне. Нужно 3 раза.",
                                  "The meat goes into cold water and boils for a long time. Foam rises when it boils — skim it in time and the шорпо (broth) stays clear.\n\n<b>Space</b> — skim when the needle is in the green zone. 3 times.");
                Vocab.Unlock("et"); NewZone(140f); break;
            case 1:
                title.text = Loc.T("2. Камыр жуур — замеси тесто", "2. Камыр жуур — knead the dough");
                desc.text = Loc.T("Из муки, воды, яйца и щепотки соли месят крутое тесто — камыр.\n\n<b>Быстро жми пробел</b>, пока полоска не заполнится.",
                                  "Flour, water, an egg and a pinch of salt make a stiff dough — камыр.\n\n<b>Press Space quickly</b> until the bar is full.");
                Vocab.Unlock("un", "kamyr"); break;
            case 2:
                title.text = Loc.T("3. Камырды кес — нарежь тесто", "3. Камырды кес — cut the dough");
                desc.text = Loc.T("Тесто раскатывают очень тонко и режут ромбиками или квадратами, потом варят в шорпо.\n\n<b>Пробел</b> — резать, когда нож над зелёной меткой. 4 разреза.",
                                  "The dough is rolled very thin and cut into diamonds or squares, then boiled in the шорпо.\n\n<b>Space</b> — cut when the knife is over the green mark. 4 cuts.");
                NewZone(90f); break;
            case 3:
                title.text = Loc.T("4. Пиязды туура — нарежь лук", "4. Пиязды туура — slice the onion");
                desc.text = Loc.T("Лук режут кольцами и заливают горячим шорпо с перцем — получается соус «чык».\n\n<b>Чередуй A и D</b>, чтобы нарезать лук.",
                                  "Onion is sliced into rings and covered with hot шорпо and pepper — that makes the \"chyk\" sauce.\n\n<b>Alternate A and D</b> to slice the onion.");
                Vocab.Unlock("piyaz"); lastSide = 0; break;
            case 4:
                title.text = Loc.T("5. Табакка сал — подай бешбармак", "5. Табакка сал — serve the beshbarmak");
                desc.text = Loc.T("Как правильно выложить бешбармак на большое блюдо (табак)?", "How do you lay out beshbarmak on the big platter (табак)?");
                string[] ru = { "Внизу тесто, сверху нарезанное мясо, полить луковым соусом", "Внизу лук, сверху тесто, мясо подать отдельно", "Всё перемешать в казане и разлить по пиалам" };
                string[] en = { "Dough at the bottom, sliced meat on top, onion sauce poured over", "Onion at the bottom, dough on top, meat served separately", "Mix everything in the kazan and pour into bowls" };
                int[] order = Random.value < 0.5f ? new[] { 1, 0, 2 } : new[] { 2, 1, 0 };
                for (int i = 0; i < 3; i++)
                {
                    int src = order[i];
                    choices[i].GetComponentInChildren<TMP_Text>().text = Loc.T(ru[src], en[src]);
                    choices[i].GetComponent<Image>().color = MurasUI.ButtonCol;
                    choices[i].name = src == 0 ? "Right" : "Wrong";
                    choices[i].interactable = true;
                }
                break;
        }
    }

    void NewZone(float width)
    {
        zoneMin = Random.Range(-360f, 360f - width); zoneMax = zoneMin + width;
        barZone.anchoredPosition = new Vector2((zoneMin + zoneMax) * 0.5f, 0f); barZone.sizeDelta = new Vector2(width, 60f);
    }

    void Update()
    {
        if (!open || step < 0 || waitingNext) return;
        float dt = Time.unscaledDeltaTime;
        t += dt;
        float target = step == 0 ? 3 : step == 1 ? 25 : step == 2 ? 4 : step == 3 ? 16 : 1;
        if (step <= 3) progressFill.sizeDelta = new Vector2(800f * Mathf.Clamp01(hits / target), 0f);

        if (step == 0 || step == 2)
        {
            float speed = step == 0 ? 520f : 640f;
            float x = needle.anchoredPosition.x + needleDir * speed * dt;
            if (x > 390f) { x = 390f; needleDir = -1f; } if (x < -390f) { x = -390f; needleDir = 1f; }
            needle.anchoredPosition = new Vector2(x, 0f);
            if (step == 2) knife.anchoredPosition = new Vector2(x, 60f);
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            {
                if (x >= zoneMin && x <= zoneMax) { hits++; MurasUI.Play("word_chime", 0.5f, 1.2f); NewZone(step == 0 ? 140f : 90f); }
                else { mistakes++; MurasUI.Play("komuz_miss", 0.6f); status.text = Loc.T("Мимо! Ещё раз.", "Missed! Again."); }
                if (hits >= target) Done();
            }
        }
        else if (step == 1)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)) { hits++; MurasUI.Play("tile_turn", 0.35f, 0.8f + hits * 0.02f); }
            if (hits >= target) Done();
        }
        else if (step == 3)
        {
            int side = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) ? 1 : Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) ? 2 : 0;
            if (side != 0 && side != lastSide) { hits++; lastSide = side; MurasUI.Play("tile_turn", 0.4f, 1.4f); }
            if (hits >= target) Done();
        }
    }

    void Choose(int i)
    {
        if (step != 4 || waitingNext) return;
        bool ok = choices[i].name == "Right";
        foreach (Button b in choices) { b.interactable = false; b.GetComponent<Image>().color = b.name == "Right" ? MurasUI.Green : MurasUI.ButtonCol; }
        if (!ok) { choices[i].GetComponent<Image>().color = MurasUI.Red; mistakes++; GameAudio.Wrong(); }
        else GameAudio.Correct();
        status.text = ok ? Loc.T("Туура! Мясо кладут сверху — самый почётный кусок подают старшему гостю.", "Туура! The meat goes on top — the most honoured piece goes to the eldest guest.")
                         : Loc.T("Не совсем: внизу тесто, сверху мясо и луковый соус.", "Not quite: dough at the bottom, meat and onion sauce on top.");
        waitingNext = true;
        StartCoroutine(Later(2.2f, Finish));
    }

    void Done()
    {
        waitingNext = true;
        status.text = Loc.T("Жакшы! Дальше…", "Жакшы! Next…");
        GameAudio.Correct();
        int next = step + 1;
        StartCoroutine(Later(1.1f, () => Begin(next)));
    }

    System.Collections.IEnumerator Later(float s, System.Action a) { yield return new WaitForSecondsRealtime(s); if (open) a(); }

    void Finish()
    {
        barRoot.SetActive(false); choiceRoot.SetActive(false);
        bool first = SaveData.Get("cook_done") == 0;
        int xp = first ? FirstXp : RepeatXp;
        SaveData.Add("cook_done", 1);
        if (GameManager.Instance != null) GameManager.Instance.AddXP(xp);
        Inventory.Add("beshbarmak");
        Achievements.Give("cook");
        GameAudio.QuestComplete();
        stepNo.text = "";
        title.text = Loc.T("Бешбармак даяр! Аш болсун!", "Beshbarmak is ready! Аш болсун!");
        desc.text = Loc.T($"Эже: «Азамат! Как у настоящей хозяйки. Возьми с собой — съешь, когда устанешь.»\n\nОшибок: {mistakes}   ·   <color=#5FC873>+{xp} XP</color>   ·   " + "«Бешбармак» в капчыке (I)",
                          $"Ezhe: \"Азамат! Just like a real host. Take it with you — eat when you're tired.\"\n\nMistakes: {mistakes}   ·   <color=#5FC873>+{xp} XP</color>   ·   \"Beshbarmak\" in your bag (I)");
        status.text = Loc.T("«Аш болсун» — «приятного аппетита».", "\"Аш болсун\" means \"enjoy your meal\".");
        dish.gameObject.SetActive(true);
        step = -1;
    }
}

/// <summary>Казан у кухни: E — готовить бешбармак.</summary>
public class CookStation : MonoBehaviour
{
    public float radius = 2.8f;
    private Transform player;
    void Start() { var pm = FindAnyObjectByType<PlayerMovement>(); if (pm != null) player = pm.transform; }
    void Update()
    {
        if (player == null || !Modal.Free || PauseMenu.IsPaused) return;
        Vector3 d = player.position - transform.position; d.y = 0f;
        if (d.sqrMagnitude > radius * radius) return;
        InteractionHint.Request("<b><color=#E8B04A>E</color></b>  —  " + (CookingGame.HasIngredients() ? Loc.T("готовить бешбармак", "cook beshbarmak") : Loc.T("казан для бешбармака (нужны эт, ун, пияз)", "beshbarmak kazan (needs эт, ун, пияз)")));
        if (Input.GetKeyDown(KeyCode.E)) CookingGame.Ensure().Open();
    }
}

/// <summary>Корзина с продуктами у базара: E — взять эт, ун и пияз.</summary>
public class GroceriesSpot : MonoBehaviour
{
    public float radius = 2.4f;
    private Transform player;
    void Start() { var pm = FindAnyObjectByType<PlayerMovement>(); if (pm != null) player = pm.transform; }
    void Update()
    {
        if (player == null || !Modal.Free || PauseMenu.IsPaused || CookingGame.HasIngredients()) return;
        Vector3 d = player.position - transform.position; d.y = 0f;
        if (d.sqrMagnitude > radius * radius) return;
        InteractionHint.Request("<b><color=#E8B04A>E</color></b>  —  " + Loc.T("взять продукты для бешбармака", "take groceries for beshbarmak"));
        if (!Input.GetKeyDown(KeyCode.E)) return;
        foreach (string n in new[] { "et", "un", "piyaz" }) if (Inventory.Count(n) < 1) Inventory.Add(n, 1, n == "piyaz");
        Vocab.Unlock("et", "un", "piyaz");
        QuestTalk.Say("Асан", Loc.T("Бери, бери! Свежее эт, белый ун и сладкий пияз — для бешбармака лучше не найдёшь. Казан — у кухни эже.",
                                    "Take it! Fresh эт, white ун and sweet пияз — you won't find better for beshbarmak. The kazan is by Ezhe's kitchen."));
    }
}
