using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Панель реплик NPC (новый стиль): тёмная полоса внизу экрана, NPC в полроста слева или справа,
/// косая табличка с именем и текст, который печатается по буквам под «болтовню» персонажа.
/// Квесты пишут в текст как раньше: «Аксакал: ...», «Эже: ...», «Бала: ...» —
/// панель сама узнаёт, кто говорит, по началу фразы.
/// Когда в разговоре сменяется собеседник, второй встаёт с другой стороны.
/// Картинки NPC — Resources/MURAS/Busts/*.png, оформление — Resources/MURAS/Dialog/*.png.
/// </summary>
public class SpeechPanel : MonoBehaviour
{
    [System.Serializable]
    public class Speaker
    {
        public string key = "Аксакал";
        public string displayName = "Аксакал";
        public string role = "старейшина";
        public string roleEn = "elder";
        public Sprite portrait;
        [Range(0.5f, 2f)] public float voicePitch = 1f;
        [Tooltip("NPC в полроста (если пусто — берётся из Resources/MURAS/Busts)")]
        public Sprite bust;
    }

    public TMP_Text text;
    public Image portrait;            // теперь это NPC в полроста
    public GameObject nameTag;
    public TMP_Text nameText;
    public TMP_Text roleText;
    public float charactersPerSecond = 45f;
    public float textLeftWithPortrait = 200f;   // не используется в новом стиле, оставлено для совместимости
    public float textLeftWithout = 36f;
    public List<Speaker> speakers = new List<Speaker>();

    [Header("Новый стиль")]
    [System.NonSerialized] public float panelHeight = 215f;
    [System.NonSerialized] public Vector2 bustSize = new Vector2(480f, 640f);
    [System.NonSerialized] public float sideSwitchSeconds = 25f;

    public static SpeechPanel Instance { get; private set; }

    /// <summary>Сейчас на экране есть реплика.</summary>
    public bool IsShowing => text != null && !string.IsNullOrEmpty(text.text) && text.gameObject.activeInHierarchy;

    /// <summary>Закрыть реплику (кнопка ✕ или Esc).</summary>
    public void Close()
    {
        if (text != null) text.text = "";
        GameAudio.StopBabble();
    }

    // имя говорящего → картинка в Resources/MURAS/Busts
    private static readonly Dictionary<string, string> BustFiles = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
    {
        { "Аксакал", "aksakal" }, { "Эже", "ezhe" }, { "Бала", "bala" }, { "Кыз", "kyz" },
        { "Айбек", "aibek" }, { "Aibek", "aibek" }, { "Апа", "apa" }, { "Байке", "baike" },
        { "Чабан", "chaban" }, { "Болот", "bolot" }, { "Bolot", "bolot" },
        { "Асан", "asan" }, { "Нурлан", "nurlan" }, { "Эрмек", "ermek" }, { "Актан", "aktan" }, { "Бунтарь", "buntar" }, { "Анас", "buntar" }, { "Манасчы", "manaschy" },
    };

    private string shown;
    private float reveal;
    private bool typing;

    private RectTransform closeRt;
    private RectTransform panelRt, bustRt, tagRt, roleRt, textRt, chevronRt;
    private Image chevron;
    private TMP_Text hint;
    private Speaker lastSpeaker;
    private float lastLineTime = -100f;
    private bool rightSide;
    private float bustAnim, bustIn = 1f;
    private float tagWidth = 250f;

    private void Awake()
    {
        Instance = this;
        AddMissingSpeakers();
        BuildLayout();
    }

    // ------------------------------------------------------------------ оформление

    static Sprite Load(string path)
    {
        Texture2D t = Resources.Load<Texture2D>(path);
        if (t == null) return null;
        t.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0f), 100f);
    }

    void AddMissingSpeakers()
    {
        void Add(string key, string role, string roleEn, float pitch, string display = null)
        {
            if (speakers.Exists(s => string.Equals(s.key, key, System.StringComparison.OrdinalIgnoreCase))) return;
            speakers.Add(new Speaker { key = key, displayName = display ?? key, role = role, roleEn = roleEn, voicePitch = pitch });
        }
        Add("Асан", "торговец", "trader", 0.9f);
        Add("Нурлан", "комузчу", "komuz player", 0.95f);
        Add("Эрмек", "чабандес", "horse rider", 1.0f);
        Add("Болот", "бүркүтчү", "eagle hunter", 0.85f);
        Add("Апа", "мастерица шырдака", "shyrdak master", 0.85f);
        Add("Актан", "участковый", "district officer", 0.82f, "Актан Турдакунович");
        Add("Бунтарь", "местный бунтарь", "local rebel", 1.05f, "Анас");
        Add("Манасчы", "сказитель «Манаса»", "Manas storyteller", 0.72f);

        foreach (Speaker s in speakers)
            if (s.bust == null && BustFiles.TryGetValue(s.key, out string file))
                s.bust = Load("MURAS/Busts/" + file);
    }

    void BuildLayout()
    {
        panelRt = (RectTransform)transform;
        panelRt.anchorMin = new Vector2(0f, 0f);
        panelRt.anchorMax = new Vector2(1f, 0f);
        panelRt.pivot = new Vector2(0.5f, 0f);
        panelRt.offsetMin = new Vector2(0f, 0f);
        panelRt.offsetMax = new Vector2(0f, 0f);
        panelRt.anchoredPosition = new Vector2(0f, 22f);
        panelRt.sizeDelta = new Vector2(0f, panelHeight);

        // полоса
        Image bg = GetComponent<Image>();
        if (bg != null)
        {
            bg.sprite = Load("MURAS/Dialog/strip");
            bg.type = Image.Type.Simple;
            bg.color = new Color(0.07f, 0.055f, 0.05f, 0.86f);
        }
        foreach (Outline o in GetComponents<Outline>()) o.enabled = false;
        foreach (Shadow s in GetComponents<Shadow>()) s.enabled = false;

        // золотая линия сверху
        var line = new GameObject("TopLine", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        line.SetParent(transform, false);
        line.anchorMin = new Vector2(0.1f, 1f); line.anchorMax = new Vector2(0.9f, 1f);
        line.pivot = new Vector2(0.5f, 0.5f); line.sizeDelta = new Vector2(0f, 3f); line.anchoredPosition = Vector2.zero;
        var li = line.GetComponent<Image>(); li.sprite = Load("MURAS/Dialog/line"); li.color = new Color(0.91f, 0.69f, 0.29f, 0.85f); li.raycastTarget = false;
        line.SetAsFirstSibling();

        // NPC в полроста
        if (portrait != null)
        {
            bustRt = portrait.rectTransform;
            bustRt.sizeDelta = bustSize;
            bustRt.pivot = new Vector2(0.5f, 0f);
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            foreach (Outline o in portrait.GetComponents<Outline>()) o.enabled = false;
            foreach (Transform c in portrait.transform) c.gameObject.SetActive(false);   // старая круглая рамка, если была
            portrait.transform.SetAsLastSibling();
        }

        // табличка с именем
        if (nameTag != null)
        {
            tagRt = (RectTransform)nameTag.transform;
            tagRt.sizeDelta = new Vector2(250f, 54f);
            var ti = nameTag.GetComponent<Image>();
            if (ti != null) { ti.sprite = Load("MURAS/Dialog/nametag"); ti.type = Image.Type.Simple; ti.color = Color.white; ti.preserveAspect = false; }
            foreach (Outline o in nameTag.GetComponents<Outline>()) o.enabled = false;
            if (nameText != null)
            {
                RectTransform nr = nameText.rectTransform;
                nr.anchorMin = Vector2.zero; nr.anchorMax = Vector2.one;
                nr.offsetMin = new Vector2(22f, 0f); nr.offsetMax = new Vector2(-14f, 0f);
                nameText.alignment = TextAlignmentOptions.Center;
                nameText.fontSize = 30; nameText.fontStyle = FontStyles.Bold;
                nameText.color = Color.white;
                nameText.enableAutoSizing = false;
            }
            nameTag.transform.SetAsLastSibling();
        }
        if (roleText != null)
        {
            roleRt = roleText.rectTransform;
            roleRt.sizeDelta = new Vector2(360f, 30f);
            roleText.fontSize = 21; roleText.fontStyle = FontStyles.Italic;
            roleText.color = new Color(0.91f, 0.69f, 0.29f);
        }

        // текст
        if (text != null)
        {
            textRt = text.rectTransform;
            textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one;
            textRt.pivot = new Vector2(0.5f, 0.5f);
            text.fontSize = 31;
            text.color = new Color(1f, 0.98f, 0.94f);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
        }

        // стрелка «дальше» и подсказка
        chevronRt = new GameObject("NextArrow", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        chevronRt.SetParent(transform, false);
        chevronRt.anchorMin = chevronRt.anchorMax = new Vector2(0.5f, 0f);
        chevronRt.sizeDelta = new Vector2(34f, 20f);
        chevron = chevronRt.GetComponent<Image>(); chevron.sprite = Load("MURAS/Dialog/chevron"); chevron.raycastTarget = false;

        hint = new GameObject("Hint", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        hint.rectTransform.SetParent(transform, false);
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(1f, 0f);
        hint.rectTransform.pivot = new Vector2(1f, 0f);
        hint.rectTransform.sizeDelta = new Vector2(420f, 30f);
        if (text != null) hint.font = text.font;
        hint.fontSize = 17; hint.color = new Color(0.8f, 0.77f, 0.72f, 0.9f);
        hint.alignment = TextAlignmentOptions.BottomRight; hint.raycastTarget = false;

        // крестик — в правый верхний угол полосы
        Transform close = transform.Find("CloseButton");
        if (close != null)
        {
            var cr = (RectTransform)close; closeRt = cr;
            cr.anchorMin = cr.anchorMax = new Vector2(1f, 1f); cr.pivot = new Vector2(1f, 1f);
            cr.anchoredPosition = new Vector2(-150f, -14f);
            close.SetAsLastSibling();
        }

        ApplyLayout(false, false);
    }

    const float BustCenter = 290f;

    void ApplyLayout(bool hasSpeaker, bool right)
    {
        const float bustCenter = BustCenter, tagX = 560f, textInner = 590f, textOuter = 170f;
        float edge = right ? -1f : 1f;

        if (bustRt != null)
        {
            bustRt.anchorMin = bustRt.anchorMax = new Vector2(right ? 1f : 0f, 0f);
            bustRt.anchoredPosition = new Vector2(edge * bustCenter, 0f);
            bustRt.localScale = new Vector3(right ? -1f : 1f, 1f, 1f);       // справа NPC смотрит влево
        }
        if (tagRt != null)
        {
            tagRt.anchorMin = tagRt.anchorMax = new Vector2(right ? 1f : 0f, 1f);
            tagRt.pivot = new Vector2(right ? 1f : 0f, 0.5f);
            tagRt.anchoredPosition = new Vector2(edge * tagX, 2f);
        }
        if (roleRt != null)
        {
            roleRt.anchorMin = roleRt.anchorMax = new Vector2(right ? 1f : 0f, 1f);
            roleRt.pivot = new Vector2(right ? 1f : 0f, 0f);
            roleRt.anchoredPosition = new Vector2(edge * (tagX + tagWidth + 18f), 6f);
            roleText.alignment = right ? TextAlignmentOptions.BottomRight : TextAlignmentOptions.BottomLeft;
        }
        if (textRt != null)
        {
            float l = hasSpeaker ? (right ? textOuter : textInner) : 260f;
            float r = hasSpeaker ? (right ? textInner : textOuter) : 260f;
            textRt.offsetMin = new Vector2(l, 44f);
            textRt.offsetMax = new Vector2(-r, -46f);
        }
        if (closeRt != null)   // крестик — на стороне, свободной от NPC
        {
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(right ? 0f : 1f, 1f);
            closeRt.pivot = new Vector2(right ? 0f : 1f, 1f);
            closeRt.anchoredPosition = new Vector2(right ? 150f : -150f, -14f);
        }
        if (chevronRt != null) chevronRt.anchoredPosition = new Vector2(hasSpeaker ? edge * 110f : 0f, 26f);
        if (hint != null)
        {
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(right ? 0f : 1f, 0f);
            hint.rectTransform.pivot = new Vector2(right ? 0f : 1f, 0f);
            hint.rectTransform.anchoredPosition = new Vector2(right ? 170f : -170f, 16f);
            hint.alignment = right ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.BottomRight;
        }
    }

    // ------------------------------------------------------------------ работа

    private void LateUpdate()
    {
        if (text == null) return;

        if (text.text != shown)
            OnNewText(text.text);

        if (typing)
        {
            reveal += charactersPerSecond * Time.deltaTime;
            int total = text.textInfo != null ? text.textInfo.characterCount : shown.Length;
            if (reveal >= total && total > 0)
            {
                typing = false;
                text.maxVisibleCharacters = 99999;
            }
            else text.maxVisibleCharacters = Mathf.FloorToInt(reveal);
        }

        // NPC: появляется сбоку, покачивается, пока говорит, и «дышит», когда молчит
        if (bustRt != null && portrait.enabled)
        {
            bustAnim += Time.deltaTime;
            bustIn = Mathf.MoveTowards(bustIn, 1f, Time.deltaTime * 5f);
            float ease = 1f - (1f - bustIn) * (1f - bustIn);
            float edge = rightSide ? -1f : 1f;
            float bob = typing ? Mathf.Abs(Mathf.Sin(bustAnim * 9f)) * 6f : Mathf.Sin(bustAnim * 2f) * 1.5f;
            float tilt = typing ? Mathf.Sin(bustAnim * 4.5f) * 1.2f : 0f;
            bustRt.anchoredPosition = new Vector2(edge * (BustCenter - (1f - ease) * 60f), bob);
            bustRt.localRotation = Quaternion.Euler(0f, 0f, tilt);
            float breathe = 1f + Mathf.Sin(bustAnim * 2f) * 0.006f;
            bustRt.localScale = new Vector3((rightSide ? -1f : 1f) * breathe, breathe, 1f);
            Color c = portrait.color; c.a = ease; portrait.color = c;
        }

        if (chevron != null)
        {
            chevron.enabled = !typing && IsShowing;
            if (chevron.enabled) chevronRt.anchoredPosition = new Vector2(chevronRt.anchoredPosition.x, 24f + Mathf.Abs(Mathf.Sin(Time.time * 3f)) * 5f);
        }
        if (hint != null) hint.text = IsShowing ? Loc.T("Esc — закрыть", "Esc — close") : "";
    }

    private void OnNewText(string raw)
    {
        Speaker speaker = null;
        string body = raw ?? "";
        int colon = body.IndexOf(':');
        if (colon > 0 && colon < 18)
        {
            string key = body.Substring(0, colon).Trim();
            speaker = speakers.Find(s => string.Equals(s.key, key, System.StringComparison.OrdinalIgnoreCase));
            if (speaker != null) body = body.Substring(colon + 1).TrimStart();
        }

        shown = body;
        text.text = body;

        bool hasSpeaker = speaker != null && !string.IsNullOrEmpty(body);

        if (string.IsNullOrEmpty(body))
        {
            typing = false;
            GameAudio.StopBabble();
            return;
        }

        // сторона: сменился собеседник посреди разговора — встаёт с другой стороны
        if (hasSpeaker)
        {
            bool sameTalk = lastSpeaker != null && Time.time - lastLineTime < sideSwitchSeconds;
            bool newFace = lastSpeaker == null || lastSpeaker.displayName != speaker.displayName;
            if (!sameTalk) rightSide = false;
            else if (newFace) rightSide = !rightSide;
            if (newFace || !sameTalk) bustIn = 0f;
            lastSpeaker = speaker;
            lastLineTime = Time.time;
        }

        Sprite bust = hasSpeaker ? (speaker.bust != null ? speaker.bust : speaker.portrait) : null;
        if (portrait != null)
        {
            portrait.enabled = bust != null;
            if (bust != null) portrait.sprite = bust;
        }
        if (nameTag != null) nameTag.SetActive(hasSpeaker);
        if (hasSpeaker)
        {
            if (nameText != null)
            {
                nameText.text = speaker.displayName;
                tagWidth = Mathf.Max(250f, nameText.GetPreferredValues(speaker.displayName).x + 64f);   // длинное имя — шире табличка
                if (tagRt != null) tagRt.sizeDelta = new Vector2(tagWidth, 54f);
            }
            if (roleText != null) roleText.text = Loc.T(speaker.role, speaker.roleEn);
        }
        else if (roleText != null) roleText.text = "";

        ApplyLayout(hasSpeaker, rightSide && hasSpeaker);

        reveal = 0f;
        typing = true;
        text.maxVisibleCharacters = 0;
        if (hasSpeaker) GameAudio.Babble(body, speaker.voicePitch, speaker.key);
    }
}
