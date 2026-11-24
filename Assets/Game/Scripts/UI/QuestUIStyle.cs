using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Новый стиль квестового интерфейса (как окно диалога):
///  • окна с вариантами ответа (Айбек и викторины юрты / игр / кухни) — тёмная полоса внизу и NPC в полроста;
///  • список заданий слева сверху — тёмная карточка с золотой полоской;
///  • баннер «квест выполнен» — лента с золотыми линиями.
/// Срабатывает сам при загрузке сцены, сцену менять не нужно. Картинки — Resources/MURAS/Dialog и Busts.
/// </summary>
public static class QuestUIStyle
{
    static readonly Color Gold = new Color(0.91f, 0.69f, 0.29f);
    static readonly Color Cream = new Color(1f, 0.97f, 0.92f);
    static readonly Color Muted = new Color(0.80f, 0.77f, 0.72f);
    static readonly Color Dark = new Color(0.07f, 0.055f, 0.05f, 0.86f);   // тёплый тёмный, как войлок ночью

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        Apply();
    }

    static void OnLoaded(Scene s, LoadSceneMode m) => Apply();

    static void Apply()
    {
        foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c.transform.parent != null) continue;
            Transform root = c.transform;
            if (root.Find("__MurasStyled") != null) continue;
            new GameObject("__MurasStyled").transform.SetParent(root, false);

            Answer(root.Find("DialoguePanel"), "aibek", null, null);
            Answer(root.Find("QuizPanel"), "aksakal", "Аксакал", Loc.T("старейшина села", "village elder"));
            Answer(root.Find("QuizPanel_Games"), "bala", "Бала", Loc.T("мальчик с чүкө", "boy who plays чүкө"));
            Answer(root.Find("QuizPanel_Food"), "ezhe", "Эже", Loc.T("мастерица кухни", "master cook"));
            Banner(root.Find("QuestCompleteBanner"));
            Hud(root.Find("LeftHud"));
            Chips(root);
        }
    }

    // ------------------------------------------------------------------ спрайты

    static Sprite Load(string path, Vector4 border = default)
    {
        Texture2D t = Resources.Load<Texture2D>(path);
        if (t == null) return null;
        t.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    static RectTransform R(Transform t) => t as RectTransform;

    static void Place(Transform t, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        if (t == null) return;
        RectTransform r = R(t);
        r.anchorMin = r.anchorMax = anchor; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size;
    }

    static void Style(Transform t, float size, Color color, FontStyles style = FontStyles.Normal, TextAlignmentOptions? align = null)
    {
        if (t == null || !t.TryGetComponent(out TMP_Text x)) return;
        x.fontSize = size; x.color = color; x.fontStyle = style; x.enableAutoSizing = false;
        if (align.HasValue) x.alignment = align.Value;
    }

    static void NoFrame(GameObject go)
    {
        foreach (Outline o in go.GetComponents<Outline>()) o.enabled = false;
        foreach (Shadow s in go.GetComponents<Shadow>()) s.enabled = false;
    }

    static Image Line(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, float y)
    {
        var rt = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(0f, 3f); rt.anchoredPosition = new Vector2(0f, y);
        var img = rt.GetComponent<Image>(); img.sprite = Load("MURAS/Dialog/line"); img.color = new Color(Gold.r, Gold.g, Gold.b, 0.85f); img.raycastTarget = false;
        return img;
    }

    // ------------------------------------------------------------------ окна с ответами

    const float BustCenter = 290f, ContentX = 590f;

    static void Answer(Transform panel, string bust, string npcName, string role)
    {
        if (panel == null) return;
        GameObject go = panel.gameObject;
        RectTransform pr = R(panel);
        pr.anchorMin = new Vector2(0f, 0f); pr.anchorMax = new Vector2(1f, 0f); pr.pivot = new Vector2(0.5f, 0f);
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        pr.anchoredPosition = new Vector2(0f, 22f); pr.sizeDelta = new Vector2(0f, 330f);

        if (go.TryGetComponent(out Image bg)) { bg.sprite = Load("MURAS/Dialog/strip"); bg.type = Image.Type.Simple; bg.color = Dark; }
        NoFrame(go);
        Line(panel, "TopLine", new Vector2(0.08f, 1f), new Vector2(0.92f, 1f), 0f).transform.SetAsFirstSibling();

        // NPC в полроста
        Image portrait = panel.Find("Portrait")?.GetComponent<Image>();
        if (portrait == null)
        {
            portrait = new GameObject("Portrait", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            portrait.transform.SetParent(panel, false);
        }
        Texture2D bt = Resources.Load<Texture2D>("MURAS/Busts/" + bust);
        if (bt != null) portrait.sprite = Sprite.Create(bt, new Rect(0, 0, bt.width, bt.height), new Vector2(0.5f, 0f), 100f);
        portrait.preserveAspect = true; portrait.raycastTarget = false; portrait.color = Color.white;
        NoFrame(portrait.gameObject);
        foreach (Transform c in portrait.transform) c.gameObject.SetActive(false);
        Place(portrait.transform, new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(BustCenter, 0f), new Vector2(480f, 640f));
        if (portrait.GetComponent<BustMotion>() == null) portrait.gameObject.AddComponent<BustMotion>();

        // табличка с именем
        Transform tag = panel.Find("NameTag");
        TMP_Text nameText = null;
        if (tag == null)
        {
            tag = new GameObject("NameTag", typeof(RectTransform), typeof(Image)).transform;
            tag.SetParent(panel, false);
            nameText = new GameObject("Name", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            nameText.transform.SetParent(tag, false);
            TMP_Text any = panel.GetComponentInChildren<TMP_Text>(true);
            if (any != null) nameText.font = any.font;
            nameText.text = npcName;
        }
        else nameText = tag.GetComponentInChildren<TMP_Text>(true);
        if (tag.TryGetComponent(out Image ti)) { ti.sprite = Load("MURAS/Dialog/nametag"); ti.type = Image.Type.Simple; ti.color = Color.white; ti.raycastTarget = false; }
        NoFrame(tag.gameObject);
        Place(tag, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(ContentX - 30f, 2f), new Vector2(250f, 54f));
        if (nameText != null)
        {
            RectTransform nr = nameText.rectTransform;
            nr.anchorMin = Vector2.zero; nr.anchorMax = Vector2.one; nr.offsetMin = new Vector2(22f, 0f); nr.offsetMax = new Vector2(-14f, 0f);
            nameText.fontSize = 28; nameText.fontStyle = FontStyles.Bold; nameText.color = Color.white;
            nameText.alignment = TextAlignmentOptions.Center; nameText.enableAutoSizing = false; nameText.characterSpacing = 0f;
        }

        // роль (у Айбека — своя строка, у викторин — заголовок «СУРОО · ВОПРОС»)
        Transform roleT = panel.Find("NPCRoleText");
        if (roleT != null && roleT.TryGetComponent(out TMP_Text rt)) rt.text = Loc.T("друг · гид", "friend · guide");
        Transform header = panel.Find("QuizHeader");
        foreach (Transform t in new[] { roleT, header })
        {
            if (t == null) continue;
            Place(t, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(ContentX + 248f, 6f), new Vector2(520f, 30f));
            Style(t, 19, Gold, FontStyles.Italic, TextAlignmentOptions.BottomLeft);
        }

        // вопрос и перевод
        Transform q = panel.Find("QuestionText") ?? panel.Find("QuizQuestion");
        Place(q, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ContentX, -40f), new Vector2(1060f, 48f));
        Style(q, 34, Cream, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        Transform tr = panel.Find("TranslationText");
        Place(tr, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ContentX, -88f), new Vector2(1060f, 30f));
        Style(tr, 22, Muted, FontStyles.Italic, TextAlignmentOptions.MidlineLeft);
        if (tr == null && q != null) { R(q).sizeDelta = new Vector2(1060f, 76f); if (q.TryGetComponent(out TMP_Text qt)) qt.alignment = TextAlignmentOptions.TopLeft; }

        Transform div = panel.Find("Divider");
        if (div != null) div.gameObject.SetActive(false);

        // варианты ответа — строки с золотым номером
        Sprite row = Load("MURAS/Dialog/row");
        int i = 0;
        foreach (Transform c in panel)
        {
            if (!c.name.StartsWith("AnswerSlot") && !c.name.StartsWith("QuizAnswer")) continue;
            Place(c, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ContentX, -124f - i * 52f), new Vector2(860f, 46f));
            if (c.TryGetComponent(out Image ri)) { ri.sprite = row; ri.type = Image.Type.Simple; ri.color = Color.white; }
            NoFrame(c.gameObject);
            Style(c.Find("Number"), 24, Gold, FontStyles.Bold);
            foreach (TMP_Text lbl in c.GetComponentsInChildren<TMP_Text>(true))
                if (lbl.name != "Number") { lbl.fontSize = 25; lbl.color = Cream; }
            i++;
        }

        // ответ «правильно / нет», подсказки и крестик
        Transform fb = panel.Find("FeedbackText") ?? panel.Find("QuizFeedback");
        Place(fb, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ContentX, -282f), new Vector2(640f, 36f));
        if (fb != null && fb.TryGetComponent(out TMP_Text ft)) { ft.fontSize = 21; ft.alignment = TextAlignmentOptions.MidlineLeft; }
        foreach (string n in new[] { "CloseHint", "QuizKeys" })
        {
            Transform h = panel.Find(n);
            Place(h, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-190f, 16f), new Vector2(460f, 28f));
            Style(h, 17, Muted, FontStyles.Normal, TextAlignmentOptions.BottomRight);
        }
        Transform close = panel.Find("CloseButton");
        Place(close, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-150f, -14f), new Vector2(44f, 44f));

        portrait.transform.SetAsLastSibling();
        tag.SetAsLastSibling();
        if (close != null) close.SetAsLastSibling();
    }

    // ------------------------------------------------------------------ баннер «квест выполнен»

    static void Banner(Transform b)
    {
        if (b == null) return;
        RectTransform r = R(b);
        r.sizeDelta = new Vector2(960f, 170f);
        if (b.TryGetComponent(out Image bg)) { bg.sprite = Load("MURAS/Dialog/strip_soft"); bg.type = Image.Type.Simple; bg.color = Dark; }
        NoFrame(b.gameObject);
        Line(b, "TopLine", new Vector2(0.12f, 1f), new Vector2(0.88f, 1f), 0f);
        Line(b, "BottomLine", new Vector2(0.12f, 0f), new Vector2(0.88f, 0f), 0f);
        Transform div = b.Find("Divider"); if (div != null) div.gameObject.SetActive(false);
        Style(b.Find("Header"), 18, Gold, FontStyles.Bold);
        Transform name = b.Find("QuestName");
        Style(name, 44, Cream, FontStyles.Bold);
        if (name != null) R(name).anchoredPosition = new Vector2(0f, -56f);
        Style(b.Find("Reward"), 28, Gold, FontStyles.Bold);
    }

    // ------------------------------------------------------------------ список заданий и плашки слева

    static void Hud(Transform hud)
    {
        if (hud == null) return;
        Sprite card = Load("MURAS/Dialog/card", new Vector4(14f, 14f, 40f, 14f));
        foreach (Transform c in hud)
        {
            if (!c.TryGetComponent(out Image img)) continue;
            img.sprite = card; img.type = Image.Type.Sliced; img.color = new Color(1f, 1f, 1f, 0.92f);
            NoFrame(c.gameObject);
        }
        foreach (TMP_Text t in hud.GetComponentsInChildren<TMP_Text>(true)) t.color = Cream;
        Transform qc = hud.Find("QuestCard");
        if (qc == null) return;
        Transform head = qc.Find("QuestHeader");
        if (head != null && head.TryGetComponent(out TMP_Text ht)) { ht.color = Gold; ht.fontStyle = FontStyles.Bold; ht.characterSpacing = 6f; }
        if (qc.GetComponent<QuestCardToggle>() == null) qc.gameObject.AddComponent<QuestCardToggle>();
    }

    // ------------------------------------------------------------------ остальные плашки (XP, часы, сообщения, подсказка E)

    static void Chips(Transform root)
    {
        Sprite chip = Load("MURAS/Dialog/chip", new Vector4(22f, 18f, 22f, 18f));
        foreach (string n in new[] { "XPBadge", "Clock", "MessageToast", "InteractionHintPanel" })
        {
            Transform t = root.Find(n);
            if (t == null || !t.TryGetComponent(out Image img)) continue;
            img.sprite = chip; img.type = Image.Type.Sliced; img.color = Color.white;
            NoFrame(t.gameObject);
            foreach (TMP_Text x in t.GetComponentsInChildren<TMP_Text>(true)) x.color = Cream;
        }
        Transform xp = root.Find("XPBadge/XPText");
        if (xp != null && xp.TryGetComponent(out TMP_Text xt)) xt.color = new Color(1f, 0.86f, 0.55f);
    }
}

/// <summary>NPC в полроста в квестовом окне: выезжает сбоку при открытии и слегка «дышит».</summary>
public class BustMotion : MonoBehaviour
{
    private RectTransform rt;
    private Image img;
    private Vector2 home;
    private float t, appear;

    void Awake() { rt = (RectTransform)transform; img = GetComponent<Image>(); home = rt.anchoredPosition; }

    void OnEnable() { appear = 0f; t = 0f; }

    void Update()
    {
        if (rt == null) return;
        t += Time.unscaledDeltaTime;
        appear = Mathf.MoveTowards(appear, 1f, Time.unscaledDeltaTime * 5f);
        float ease = 1f - (1f - appear) * (1f - appear);
        float breathe = Mathf.Sin(t * 2f);
        rt.anchoredPosition = home + new Vector2(-(1f - ease) * 60f, breathe * 1.5f);
        rt.localScale = Vector3.one * (1f + breathe * 0.006f);
        if (img != null) { Color c = img.color; c.a = ease; img.color = c; }
    }
}

/// <summary>
/// Список заданий можно свернуть: клавиша K (квест) или клик по заголовку «ЗАДАНИЕ».
/// В свёрнутом виде остаётся только заголовок со счётчиком заданий. Состояние запоминается.
/// </summary>
public class QuestCardToggle : MonoBehaviour
{
    public KeyCode key = KeyCode.K;
    private const string Pref = "MURAS_QuestCardCollapsed";

    private bool collapsed;
    private TMP_Text header;

    void Start()
    {
        collapsed = PlayerPrefs.GetInt(Pref, 0) == 1;
        Transform h = transform.Find("QuestHeader");
        if (h != null)
        {
            header = h.GetComponent<TMP_Text>();
            foreach (var lt in h.GetComponents<LocalizedText>()) lt.enabled = false;   // заголовок теперь пишем сами
            header.raycastTarget = true;
            var b = h.GetComponent<Button>() ?? h.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(Toggle);
        }
    }

    public void Toggle()
    {
        collapsed = !collapsed;
        PlayerPrefs.SetInt(Pref, collapsed ? 1 : 0);
        GameAudio.Interact();
    }

    void Update()
    {
        if (Input.GetKeyDown(key) && Modal.Free && !TypingInField()) Toggle();
    }

    static bool TypingInField()
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        return es != null && es.currentSelectedGameObject != null && es.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;
    }

    void LateUpdate()
    {
        int count = 0;
        foreach (Transform c in transform)
        {
            if (c.name == "QuestHeader") continue;
            TMP_Text t = c.GetComponent<TMP_Text>();
            bool hasText = t != null && c.gameObject.activeSelf && !string.IsNullOrWhiteSpace(t.text);
            if (hasText) count++;

            var le = c.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            if (le.ignoreLayout != collapsed) le.ignoreLayout = collapsed;
            if (t != null && t.enabled == collapsed) t.enabled = !collapsed;
        }

        if (header != null)
        {
            string title = Loc.T("ЗАДАНИЯ", "QUESTS");
            string state = collapsed ? (count > 0 ? "  <color=#F5EBDA>" + count + "</color>" : "") : "";
            header.text = title + state + "   <size=75%><color=#9C9283>[K] " + (collapsed ? "+" : "–") + "</color></size>";
        }
    }
}
