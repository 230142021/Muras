using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// «Боз үйдүн жасалгасы» — убранство юрты. Когда юрта собрана, внутри можно начать задание:
/// собрать у жителей три вещи и украсить юрту, узнавая значение каждой:
///  • түш кийиз — вышитый настенный ковёр (у апы), вешают на почётной стороне — төр;
///  • ала кийиз — войлочный ковёр с вкатанным узором (у байке), стелют у входа;
///  • шырдак — мозаичный войлочный ковёр (у чабана), стелют в центре.
/// Поставленные вещи появляются внутри юрты и сохраняются.
/// </summary>
public static class YurtDecor
{
    public const int Reward = 120;
    public class Item { public string id, inv, ky, ru, en, giverKey, lessonRu, lessonEn; }

    public static readonly Item[] Items =
    {
        new Item { id = "tush", inv = "tush_kiyiz", ky = "Түш кийиз", ru = "Түш кийиз — вышитый ковёр", en = "Түш кийиз — embroidered wall hanging", giverKey = "Апа",
            lessonRu = "Түш кийиз вышивают долгие месяцы — обычно его готовят к свадьбе дочери. Вешают на төр — почётную сторону напротив входа, где сажают самых уважаемых гостей. Цветы и узоры — пожелания счастья и долгой жизни.",
            lessonEn = "A түш кийиз takes many months to embroider — it is usually made for a daughter's wedding. It hangs on the төр, the place of honour opposite the door, where the most respected guests sit. Flowers and patterns are wishes of happiness and long life." },
        new Item { id = "ala", inv = "ala_kiyiz", ky = "Ала кийиз", ru = "Ала кийиз — войлочный ковёр", en = "Ала кийиз — pressed felt rug", giverKey = "Байке",
            lessonRu = "Ала кийиз делают так: на белую шерсть выкладывают узор из крашеной шерсти и долго катают — узор вваливается в войлок, края у него мягкие, «расплывчатые». Он тёплый и стелется на пол у входа.",
            lessonEn = "An ала кийиз is made by laying dyed wool in a pattern over white wool and rolling it for a long time — the pattern felts in with soft, blurred edges. It is warm and covers the floor by the entrance." },
        new Item { id = "shyr", inv = "shyrdak_big", ky = "Шырдак", ru = "Шырдак — мозаичный ковёр", en = "Шырдак — mosaic felt rug", giverKey = "Чабан",
            lessonRu = "Шырдак сшивают из вырезанных кусков войлока двух цветов — узор одного куска становится фоном другого, ничего не пропадает. Хороший шырдак служит сто лет, поэтому говорят: «Шырдак — байлык», шырдак — это богатство.",
            lessonEn = "A шырдак is sewn from cut pieces of felt in two colours — the pattern of one piece becomes the background of the other, nothing is wasted. A good shyrdak lasts a hundred years, which is why they say \"Шырдак — байлык\": a shyrdak is wealth." },
    };

    public static int Stage => SaveData.Get("yd");                       // 0 — не начато, 1 — идёт, 2 — готово
    public static bool Placed(Item it) => SaveData.Get("yd_" + it.id) == 1;
    public static bool Given(Item it) => SaveData.Get("yd_g_" + it.id) == 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        QuestTalk.Register(Handle);
        SceneManager.sceneLoaded -= OnLoaded; SceneManager.sceneLoaded += OnLoaded;
        Refresh();
    }

    static void OnLoaded(Scene s, LoadSceneMode m) { QuestTalk.Register(Handle); Refresh(); }

    static void Refresh()
    {
        UpdateHud();
        var station = Object.FindAnyObjectByType<YurtDecorStation>();
        if (station != null) station.SpawnPlaced();
    }

    static bool Handle(string who, out string line)
    {
        line = null;
        if (Stage != 1) return false;
        foreach (Item it in Items)
        {
            if (it.giverKey != who || Given(it) || Placed(it)) continue;
            SaveData.Set("yd_g_" + it.id, 1);
            Inventory.Add(it.inv);
            GameAudio.Pickup();
            line = it.id switch
            {
                "tush" => Loc.T("Юрту украшаешь? Вот, возьми мой түш кийиз — я вышивала его всю зиму. Повесь на төр, напротив входа!", "Decorating the yurt? Here, take my түш кийиз — I embroidered it all winter. Hang it on the төр, opposite the door!"),
                "ala" => Loc.T("Держи ала кийиз! Сам катал — видишь, узор мягкий, как облако. Постели у входа, чтобы ногам было тепло.", "Here's an ала кийиз! I rolled it myself — see how soft the pattern is, like a cloud. Lay it by the entrance to keep feet warm."),
                _ => Loc.T("Шырдак из шерсти моих овец — жена шила. Постели в центре юрты, там ему место.", "A шырдак from my own sheep's wool — my wife sewed it. Lay it in the middle of the yurt, that's its place."),
            };
            UpdateHud();
            return true;
        }
        return false;
    }

    public static void UpdateHud()
    {
        if (Stage != 1) { SideQuestHud.Set("yurtdecor", null, null); return; }
        int placed = 0; var sb = new System.Text.StringBuilder();
        foreach (Item it in Items)
        {
            if (Placed(it)) { placed++; continue; }
            if (sb.Length > 0) sb.Append(" · ");
            sb.Append(Inventory.Count(it.inv) > 0 ? it.ky + Loc.T(" — неси в юрту", " — bring to the yurt") : it.ky + Loc.T(" у ", " from ") + it.giverKey.ToLower());
        }
        SideQuestHud.Set("yurtdecor", Loc.T($"Боз үйдүн жасалгасы  {placed}/3", $"Yurt decoration  {placed}/3"), sb.ToString());
    }
}

/// <summary>Точка в центре собранной юрты: E — начать украшать / разложить вещи. Здесь же появляются ковры.</summary>
public class YurtDecorStation : MonoBehaviour, IModal
{
    public Transform yurtBuilt;        // BuiltKerege — по нему понятно, что юрта собрана, и где её стены
    public Transform doorHint;         // аксакал стоит у входа — по нему находим дверь и төр
    public float radius = 2.6f;

    private Transform player;
    private readonly Dictionary<string, GameObject> spawned = new Dictionary<string, GameObject>();
    private GameObject canvas, root;
    private TMP_Text lessonTitle, lessonText;
    private readonly Button[] slotBtn = new Button[3];
    private readonly TMP_Text[] slotText = new TMP_Text[3];
    private bool open;

    void Start()
    {
        var pm = FindAnyObjectByType<PlayerMovement>(); if (pm != null) player = pm.transform;
        SpawnPlaced();
    }

    bool YurtReady => yurtBuilt == null || yurtBuilt.gameObject.activeInHierarchy;

    float nextCheck;
    void Update()
    {
        if (Time.unscaledTime > nextCheck) { nextCheck = Time.unscaledTime + 1f; if (YurtReady) SpawnPlaced(); }
        if (open || player == null || !Modal.Free || PauseMenu.IsPaused || !YurtReady || YurtDecor.Stage == 2) return;
        Vector3 d = player.position - transform.position; d.y = 0f;
        if (d.sqrMagnitude > radius * radius) return;
        InteractionHint.Request("<b><color=#E8B04A>E</color></b>  —  " + Loc.T("украсить юрту", "decorate the yurt"));
        if (!Input.GetKeyDown(KeyCode.E)) return;
        if (YurtDecor.Stage == 0)
        {
            SaveData.Set("yd", 1);
            Vocab.Unlock("tor");
            QuestTalk.Say("Аксакал", Loc.T("Юрта стоит, балам, но внутри пусто! Настоящий боз үй украшают: түш кийиз — у апы, ала кийиз — у байке, шырдак — у чабана. Собери их и разложи здесь.",
                                           "The yurt stands, child, but it's empty inside! A real боз үй is decorated: the түш кийиз is with Apa, the ала кийиз with Baike, the шырдак with the shepherd. Gather them and lay them out here."));
            MurasToast.Push(Loc.T("Жаңы тапшырма", "New quest"), Loc.T("<b>Боз үйдүн жасалгасы</b> — убранство юрты", "<b>Боз үйдүн жасалгасы</b> — decorate the yurt"), MurasUI.Gold, "word_chime");
            YurtDecor.UpdateHud();
            return;
        }
        OpenWindow();
    }

    // ------------------------------------------------------------------ окно

    void OpenWindow()
    {
        if (root == null) Build();
        open = true; Modal.Open(this); Time.timeScale = 0f; root.SetActive(true);
        lessonTitle.text = Loc.T("Боз үйдүн жасалгасы", "Decorating the yurt");
        lessonText.text = Loc.T("Выбери вещь из капчыка, чтобы поставить её на место. Внутри юрты у каждой вещи своё место — и своё значение.",
                                "Pick an item from your bag to put it in its place. Inside the yurt every item has its own place — and its own meaning.");
        RefreshSlots();
    }

    public void CloseModal() => Close();
    void Close()
    {
        if (!open) return;
        open = false; root.SetActive(false); Modal.Close(this);
        if (!PauseMenu.IsPaused) Time.timeScale = 1f;
    }

    void Build()
    {
        canvas = MurasUI.Canvas("YurtDecorCanvas", transform, 470);
        root = MurasUI.Overlay(canvas.transform, 0.75f);
        var win = MurasUI.WindowBox(root.transform, new Vector2(1240, 720));
        MurasUI.CloseButton(win.transform, Close);
        MurasUI.Text(win.transform, Loc.T("БОЗ ҮЙ  ·  убранство юрты", "БОЗ ҮЙ  ·  yurt decoration"), 22, MurasUI.Gold, new Vector2(0, 320), new Vector2(900, 34), FontStyles.Bold);
        for (int i = 0; i < 3; i++)
        {
            int k = i;
            var card = MurasUI.Box("Slot" + i, win.transform, new Vector2(-400 + i * 400, 110), new Vector2(360, 300), MurasUI.Card);
            var icon = MurasUI.Box("Icon", card.transform, new Vector2(0, 50), new Vector2(150, 150), Color.white).GetComponent<Image>();
            Texture2D t = Resources.Load<Texture2D>("MURAS/Icons/" + YurtDecor.Items[i].inv);
            if (t != null) icon.sprite = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));
            icon.preserveAspect = true;
            slotText[i] = MurasUI.Text(card.transform, "", 20, MurasUI.Cream, new Vector2(0, -60), new Vector2(340, 60));
            slotBtn[i] = MurasUI.Button(card.transform, Loc.T("Коюу — поставить", "Place it"), new Vector2(0, -115), new Vector2(260, 50), () => Place(k), 20);
        }
        lessonTitle = MurasUI.Text(win.transform, "", 30, MurasUI.Gold, new Vector2(0, -95), new Vector2(1100, 44), FontStyles.Bold);
        lessonText = MurasUI.Text(win.transform, "", 22, MurasUI.Cream, new Vector2(0, -200), new Vector2(1100, 170));
        root.SetActive(false);
    }

    void RefreshSlots()
    {
        for (int i = 0; i < 3; i++)
        {
            var it = YurtDecor.Items[i];
            bool placed = YurtDecor.Placed(it), has = Inventory.Count(it.inv) > 0;
            slotText[i].text = "<b>" + it.ky + "</b>\n<size=85%>" + (placed ? Loc.T("<color=#7FBF6A>на месте</color>", "<color=#7FBF6A>in place</color>") : has ? Loc.T("в капчыке", "in your bag") : Loc.T("спроси: ", "ask: ") + it.giverKey.ToLower()) + "</size>";
            slotBtn[i].interactable = !placed && has;
        }
    }

    void Place(int i)
    {
        var it = YurtDecor.Items[i];
        if (YurtDecor.Placed(it) || !Inventory.Remove(it.inv)) return;
        SaveData.Set("yd_" + it.id, 1);
        GameAudio.Correct();
        lessonTitle.text = Loc.T(it.ru, it.en);
        lessonText.text = Loc.T(it.lessonRu, it.lessonEn);
        Vocab.Unlock(it.id == "tush" ? "tushkiyiz" : it.id == "ala" ? "ala_kiyiz" : "shyrdak");
        SpawnPlaced();
        RefreshSlots();
        YurtDecor.UpdateHud();
        bool all = true; foreach (var x in YurtDecor.Items) if (!YurtDecor.Placed(x)) all = false;
        if (all && YurtDecor.Stage == 1)
        {
            SaveData.Set("yd", 2);
            if (GameManager.Instance != null) GameManager.Instance.AddXP(YurtDecor.Reward);
            Achievements.Give("yurt_decor");
            GameAudio.QuestComplete();
            lessonText.text += "\n\n" + Loc.T($"<color=#E8B04A>Юрта украшена! Аксакал: «Бул чыныгы боз үй! Вот теперь это настоящий дом.»  +{YurtDecor.Reward} XP</color>",
                                             $"<color=#E8B04A>The yurt is decorated! Aksakal: \"Бул чыныгы боз үй! Now this is a real home.\"  +{YurtDecor.Reward} XP</color>");
            YurtDecor.UpdateHud();
        }
    }

    // ------------------------------------------------------------------ ковры внутри юрты

    public void SpawnPlaced()
    {
        if (!YurtReady) return;
        bool need = false; foreach (var it in YurtDecor.Items) if (YurtDecor.Placed(it) && !spawned.ContainsKey(it.id)) need = true;
        if (!need) return;
        Vector3 c = transform.position; float floor = c.y, r = 3.2f;
        if (yurtBuilt != null)
        {
            var rs = yurtBuilt.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds; foreach (var x in rs) b.Encapsulate(x.bounds);
                c = new Vector3(b.center.x, b.min.y, b.center.z); floor = b.min.y; r = Mathf.Min(b.extents.x, b.extents.z) * 0.92f;
            }
        }
        Vector3 door = doorHint != null ? doorHint.position - c : Vector3.forward; door.y = 0f;
        door = door.sqrMagnitude > 0.01f ? door.normalized : Vector3.forward;
        foreach (var it in YurtDecor.Items)
        {
            if (!YurtDecor.Placed(it) || spawned.ContainsKey(it.id)) continue;
            GameObject go;
            switch (it.id)
            {
                case "tush":   // на стене напротив входа (төр)
                    go = Panel("TushKiyiz", "MURAS/Decor/tush_kiyiz", new Vector2(1.7f, 1.27f));
                    go.transform.position = c - door * (r - 0.12f) + Vector3.up * 1.25f;
                    go.transform.rotation = Quaternion.LookRotation(-door, Vector3.up);   // лицом к центру
                    break;
                case "ala":    // на полу у входа
                    go = Panel("AlaKiyiz", "MURAS/Decor/ala_kiyiz", new Vector2(1.8f, 1.5f));
                    go.transform.position = new Vector3(c.x, floor + 0.035f, c.z) + door * (r * 0.5f);
                    go.transform.rotation = Quaternion.LookRotation(Vector3.down, door);
                    break;
                default:       // шырдак — в центре, ближе к төр
                    go = Panel("Shyrdak", "MURAS/shyrdak_1", new Vector2(2.3f, 2.3f));
                    go.transform.position = new Vector3(c.x, floor + 0.04f, c.z) - door * (r * 0.2f);
                    go.transform.rotation = Quaternion.LookRotation(Vector3.down, door);
                    break;
            }
            spawned[it.id] = go;
        }
    }

    GameObject Panel(string name, string tex, Vector2 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Decor_" + name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, true);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var mr = go.GetComponent<MeshRenderer>();
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        Texture2D t = Resources.Load<Texture2D>(tex);
        if (t != null) m.SetTexture("_BaseMap", t);
        m.SetFloat("_Smoothness", 0.05f);
        m.SetFloat("_Cull", 0f);   // видно с обеих сторон
        mr.sharedMaterial = m;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }
}
