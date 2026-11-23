using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Реплики всех NPC — в стиле диалога с Айбеком (портрет, имя, печать по буквам, «болтовня»),
/// звуки (квест выполнен, правильно/неправильно, подбор) и плашка «Квест выполнен».
/// Запуск: Tools > Diploma > Setup NPC Speech + Sounds.
/// </summary>
public static class SpeechAndSoundSetup
{
    private const string ScenePath = "Assets/Game/Scenes/Village.unity";
    private const string Sprites = "Assets/Game/UI/Sprites/";
    private const string Audio = "Assets/Game/Audio/";

    private static readonly Color Cream = new Color(0.96f, 0.93f, 0.85f);
    private static readonly Color Muted = new Color(0.79f, 0.75f, 0.65f);
    private static readonly Color Gold = new Color(0.84f, 0.66f, 0.29f);

    [MenuItem("Tools/Diploma/Setup NPC Speech + Sounds")]
    public static void Setup()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        foreach (string p in new[] { "aksakal", "ezhe", "bala", "kyz", "aibek" })
            MakeSprite(Sprites + "ui_portrait_" + p + ".png");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform canvas = Find(scene, "GameCanvas");
        if (canvas == null) { Debug.LogError("[Speech] Нет GameCanvas"); return; }

        // ---------- Звук
        Transform audioT = Find(scene, "GameAudio");
        GameObject audioGo = audioT != null ? audioT.gameObject : new GameObject("GameAudio");
        if (audioT == null) SceneManager.MoveGameObjectToScene(audioGo, scene);
        if (!audioGo.TryGetComponent(out GameAudio ga)) ga = audioGo.AddComponent<GameAudio>();
        ga.questComplete = Clip("quest_complete");
        ga.correct = Clip("correct");
        ga.wrong = Clip("wrong");
        ga.pickup = Clip("pickup");
        ga.voiceBlips = Enumerable.Range(1, 5).Select(i => Clip("blip_" + i)).Where(c => c != null).ToArray();
        EditorUtility.SetDirty(ga);

        // ---------- Панель реплик (на основе StoryPanel)
        Transform storyPanel = canvas.Find("StoryPanel");
        Transform storyTextT = storyPanel != null ? storyPanel.Find("StoryText") : null;
        if (storyPanel == null || storyTextT == null)
        {
            Debug.LogError("[Speech] Нет StoryPanel/StoryText — сначала перенеси юрту (Merge Yurt Zone)");
            return;
        }

        var panelRt = (RectTransform)storyPanel;
        SetRect(panelRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(1180f, 200f));

        Image portrait = GetOrCreateImage(storyPanel, "Portrait");
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
        SetRect(portrait.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(146f, 146f));

        Image tag = GetOrCreateImage(storyPanel, "NameTag");
        tag.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + "ui_nametag.png");
        tag.type = Image.Type.Sliced;
        tag.raycastTarget = false;
        SetRect(tag.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(200f, -16f), new Vector2(190f, 40f));

        TMP_Text nameText = GetOrCreateText(tag.transform, "Name");
        Stretch(nameText.rectTransform, 8f, 8f, 2f, 2f);
        Style(nameText, 21f, new Color(0.23f, 0.14f, 0.06f), TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.UpperCase);
        nameText.characterSpacing = 4f;

        TMP_Text role = GetOrCreateText(storyPanel, "Role");
        SetRect(role.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(402f, -20f), new Vector2(300f, 32f));
        Style(role, 18f, Muted, TextAlignmentOptions.MidlineLeft, FontStyles.Italic);

        var storyText = storyTextT.GetComponent<TMP_Text>();
        var str = storyText.rectTransform;
        str.anchorMin = Vector2.zero; str.anchorMax = Vector2.one; str.pivot = new Vector2(0.5f, 0.5f);
        str.offsetMin = new Vector2(200f, 18f);
        str.offsetMax = new Vector2(-36f, -64f);
        Style(storyText, 24f, Cream, TextAlignmentOptions.TopLeft, FontStyles.Normal);

        if (!storyPanel.TryGetComponent(out SpeechPanel speech)) speech = storyPanel.gameObject.AddComponent<SpeechPanel>();
        speech.text = storyText;
        speech.portrait = portrait;
        speech.nameTag = tag.gameObject;
        speech.nameText = nameText;
        speech.roleText = role;
        speech.speakers = new System.Collections.Generic.List<SpeechPanel.Speaker>
        {
            Speaker("Аксакал", "Аксакал", "старейшина села", "aksakal", 0.78f),
            Speaker("Эже", "Эже", "мастерица кухни", "ezhe", 1.12f),
            Speaker("Бала", "Бала", "мальчик с чүкө", "bala", 1.45f),
            Speaker("Кыз", "Кыз", "девочка", "kyz", 1.6f),
            Speaker("Айбек", "Айбек", "житель села", "aibek", 1.0f),
        };
        EditorUtility.SetDirty(speech);

        if (storyPanel.TryGetComponent(out HideWhenEmpty hide)) { hide.autoHideAfter = 14f; EditorUtility.SetDirty(hide); }

        // Подсказку «E — ...» — над панелью
        Transform hint = canvas.Find("InteractionHintPanel");
        if (hint != null) ((RectTransform)hint).anchoredPosition = new Vector2(0f, 246f);

        // ---------- Плашка «Квест выполнен»
        Image banner = GetOrCreateImage(canvas, "QuestCompleteBanner");
        banner.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + "ui_panel.png");
        banner.type = Image.Type.Sliced;
        banner.raycastTarget = false;
        SetRect(banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(620f, 170f));

        TMP_Text head = GetOrCreateText(banner.transform, "Header");
        head.text = "ТАПШЫРМА АТКАРЫЛДЫ  ·  КВЕСТ ВЫПОЛНЕН";
        Style(head, 18f, Gold, TextAlignmentOptions.Center, FontStyles.Bold);
        head.characterSpacing = 4f;
        SetRect(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(580f, 28f));

        Image divider = GetOrCreateImage(banner.transform, "Divider");
        divider.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + "ui_divider.png");
        divider.raycastTarget = false;
        SetRect(divider.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(460f, 22f));

        TMP_Text qName = GetOrCreateText(banner.transform, "QuestName");
        Style(qName, 40f, Cream, TextAlignmentOptions.Center, FontStyles.Bold);
        qName.text = "Боз үй";
        SetRect(qName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(580f, 50f));

        TMP_Text xp = GetOrCreateText(banner.transform, "Reward");
        Style(xp, 28f, new Color(0.37f, 0.78f, 0.45f), TextAlignmentOptions.Center, FontStyles.Bold);
        xp.text = "+300 XP";
        SetRect(xp.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(300f, 36f));

        if (!banner.TryGetComponent(out CanvasGroup cg)) cg = banner.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        if (!banner.TryGetComponent(out QuestCompleteBanner qcb)) qcb = banner.gameObject.AddComponent<QuestCompleteBanner>();
        qcb.questName = qName;
        qcb.reward = xp;
        EditorUtility.SetDirty(qcb);

        canvas.Find("DialoguePanel")?.SetAsLastSibling();
        foreach (Transform t in canvas.Cast<Transform>().Where(t => t.name.StartsWith("QuizPanel")).ToList())
            t.SetAsLastSibling();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Speech] Реплики NPC с портретами и болтовнёй, звуки и плашка «Квест выполнен» готовы. Village сохранена.");
    }

    private static SpeechPanel.Speaker Speaker(string key, string name, string role, string portrait, float pitch) =>
        new SpeechPanel.Speaker
        {
            key = key, displayName = name, role = role, voicePitch = pitch,
            portrait = AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + "ui_portrait_" + portrait + ".png")
        };

    private static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(Audio + name + ".wav");

    private static void MakeSprite(string path)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return;
        if (ti.textureType == TextureImporterType.Sprite) return;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.SaveAndReimport();
    }

    private static Transform Find(Scene scene, string name) =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == name);

    private static RectTransform GetOrCreate(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null) return (RectTransform)t;
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image GetOrCreateImage(Transform parent, string name)
    {
        RectTransform rt = GetOrCreate(parent, name);
        if (!rt.TryGetComponent(out Image img)) img = rt.gameObject.AddComponent<Image>();
        return img;
    }

    private static TMP_Text GetOrCreateText(Transform parent, string name)
    {
        RectTransform rt = GetOrCreate(parent, name);
        if (!rt.TryGetComponent(out TextMeshProUGUI t)) t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.raycastTarget = false;
        return t;
    }

    private static void Style(TMP_Text t, float size, Color c, TextAlignmentOptions a, FontStyles s)
    {
        t.enableAutoSizing = false; t.fontSize = size; t.color = c; t.alignment = a; t.fontStyle = s;
        t.richText = true; t.textWrappingMode = TextWrappingModes.Normal;
        EditorUtility.SetDirty(t);
    }

    private static void SetRect(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
        rt.localScale = Vector3.one;
    }

    private static void Stretch(RectTransform rt, float l, float r, float t, float b)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
    }
}
