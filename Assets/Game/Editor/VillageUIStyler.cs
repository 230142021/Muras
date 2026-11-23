using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Делает красивый интерфейс в сцене Village: панель разговора с Айбеком, карточку квеста,
/// счётчики яблок и XP, всплывающие сообщения, подсказку «E — ...» и 3D-яблоки вместо шаров.
/// Все старые объекты и ссылки скриптов сохраняются — меняется только оформление.
/// Запуск: Tools > Diploma > Style Village UI + Apples. Можно запускать повторно.
/// </summary>
public static class VillageUIStyler
{
    private const string ScenePath = "Assets/Game/Scenes/Village.unity";
    private const string SpriteDir = "Assets/Game/UI/Sprites/";
    private const string AppleModelPath = "Assets/Game/Models/Items/Apple.obj";

    private static readonly Color Gold = new Color(0.84f, 0.66f, 0.29f);
    private static readonly Color Cream = new Color(0.96f, 0.93f, 0.85f);
    private static readonly Color Muted = new Color(0.79f, 0.75f, 0.65f);
    private static readonly Color DarkText = new Color(0.23f, 0.14f, 0.06f);

    [MenuItem("Tools/Diploma/Style Village UI + Apples")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        PrepareSprites();

        string previous = SceneManager.GetActiveScene().path;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Transform canvas = FindInScene(scene, "GameCanvas");
        if (canvas == null)
        {
            Debug.LogError("[VillageUIStyler] Не найден GameCanvas в Village");
            return;
        }

        StyleDialogue(canvas);
        StyleHud(canvas);
        CreateHint(canvas);
        int apples = ReplaceApples(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[VillageUIStyler] Готово: интерфейс оформлен, яблок заменено: {apples}. Сцена Village сохранена.");

        if (!string.IsNullOrEmpty(previous) && previous != ScenePath)
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
    }

    // ---------------------------------------------------------------- sprites

    private static void PrepareSprites()
    {
        SetupSprite("ui_panel.png", 34);
        SetupSprite("ui_card.png", 24);
        SetupSprite("ui_button.png", 18);
        SetupSprite("ui_nametag.png", 16);
        SetupSprite("ui_divider.png", 0);
        SetupSprite("ui_apple_icon.png", 0);
        SetupSprite("ui_xp_icon.png", 0);
        SetupSprite("ui_portrait_aibek.png", 0);
    }

    private static void SetupSprite(string file, int border)
    {
        string path = SpriteDir + file;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("[VillageUIStyler] Нет спрайта " + path);
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.spriteBorder = new Vector4(border, border, border, border);
        importer.SaveAndReimport();
    }

    private static Sprite LoadSprite(string file) =>
        AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + file);

    // ---------------------------------------------------------------- dialogue

    private static void StyleDialogue(Transform canvas)
    {
        Transform panel = canvas.Find("DialoguePanel");
        if (panel == null)
        {
            Debug.LogWarning("[VillageUIStyler] Нет DialoguePanel");
            return;
        }

        var panelRt = (RectTransform)panel;
        SetRect(panelRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 28f), new Vector2(1180f, 380f));
        Image panelImg = panel.GetComponent<Image>();
        panelImg.sprite = LoadSprite("ui_panel.png");
        panelImg.type = Image.Type.Sliced;
        panelImg.color = Color.white;

        // Портрет и табличка с именем
        Image portrait = GetOrCreateImage(panel, "Portrait");
        portrait.sprite = LoadSprite("ui_portrait_aibek.png");
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
        SetTopLeft(portrait.rectTransform, 34f, -30f, 156f, 156f);

        Image tag = GetOrCreateImage(panel, "NameTag");
        tag.sprite = LoadSprite("ui_nametag.png");
        tag.type = Image.Type.Sliced;
        tag.raycastTarget = false;
        SetTopLeft(tag.rectTransform, 26f, -196f, 172f, 46f);

        TMP_Text npcName = Text(panel, "NPCNameText");
        if (npcName != null)
        {
            npcName.transform.SetParent(tag.transform, false);
            Stretch(npcName.rectTransform, 6f, 6f, 2f, 2f);
            StyleText(npcName, 24f, DarkText, TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.UpperCase);
            npcName.characterSpacing = 4f;
        }

        TMP_Text roleText = GetOrCreateText(panel, "NPCRoleText");
        roleText.text = "житель села";
        SetTopLeft(roleText.rectTransform, 26f, -246f, 172f, 26f);
        StyleText(roleText, 17f, Muted, TextAlignmentOptions.Center, FontStyles.Italic);

        const float left = 228f;
        const float width = 900f;

        TMP_Text question = Text(panel, "QuestionText");
        if (question != null)
        {
            SetTopLeft(question.rectTransform, left, -30f, width, 52f);
            StyleText(question, 36f, Cream, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        }

        TMP_Text translation = Text(panel, "TranslationText");
        if (translation != null)
        {
            SetTopLeft(translation.rectTransform, left, -82f, width, 32f);
            StyleText(translation, 22f, Muted, TextAlignmentOptions.MidlineLeft, FontStyles.Italic);
        }

        Image divider = GetOrCreateImage(panel, "Divider");
        divider.sprite = LoadSprite("ui_divider.png");
        divider.raycastTarget = false;
        divider.color = new Color(1f, 1f, 1f, 0.9f);
        SetTopLeft(divider.rectTransform, left, -120f, width, 26f);

        // Кнопки ответов в подложках
        for (int i = 1; i <= 3; i++)
        {
            Transform buttonT = FindDeep(panel, "AnswerButton" + i);
            if (buttonT == null) continue;

            Image slot = GetOrCreateImage(panel, "AnswerSlot" + i);
            slot.sprite = LoadSprite("ui_button.png");
            slot.type = Image.Type.Sliced;
            slot.color = Color.white;
            slot.raycastTarget = true;
            SetTopLeft(slot.rectTransform, left, -156f - (i - 1) * 56f, width, 48f);

            Image accent = GetOrCreateImage(slot.transform, "Accent");
            accent.color = Gold;
            accent.raycastTarget = false;
            SetRect(accent.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(8f, 0f), new Vector2(5f, -16f));

            TMP_Text number = GetOrCreateText(slot.transform, "Number");
            number.text = i.ToString();
            StyleText(number, 24f, Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            SetRect(number.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(18f, 0f), new Vector2(34f, 0f));

            buttonT.SetParent(slot.transform, false);
            var buttonRt = (RectTransform)buttonT;
            Stretch(buttonRt, 62f, 18f, 0f, 0f);
            buttonT.SetAsLastSibling();

            TMP_Text label = buttonT.GetComponent<TMP_Text>();
            if (label != null)
            {
                StyleText(label, 25f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
                label.raycastTarget = true;
            }

            Button button = buttonT.GetComponent<Button>();
            if (button != null)
            {
                button.transition = Selectable.Transition.ColorTint;
                button.targetGraphic = slot;
                ColorBlock colors = button.colors;
                colors.normalColor = new Color(0.15f, 0.24f, 0.40f, 0.96f);
                colors.highlightedColor = new Color(0.25f, 0.37f, 0.58f, 1f);
                colors.pressedColor = new Color(0.62f, 0.48f, 0.20f, 1f);
                colors.selectedColor = colors.normalColor;
                colors.disabledColor = new Color(0.15f, 0.20f, 0.28f, 0.85f);
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.08f;
                button.colors = colors;
                EditorUtility.SetDirty(button);
            }
        }

        TMP_Text feedback = Text(panel, "FeedbackText");
        if (feedback != null)
        {
            SetTopLeft(feedback.rectTransform, left, -322f, width - 120f, 48f);
            StyleText(feedback, 21f, Muted, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
        }

        TMP_Text closeHint = GetOrCreateText(panel, "CloseHint");
        closeHint.text = "<color=#E8B04A><b>E</b></color> — закрыть";
        StyleText(closeHint, 17f, Muted, TextAlignmentOptions.MidlineRight, FontStyles.Normal);
        SetRect(closeHint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-28f, 18f), new Vector2(200f, 30f));

        Transform close = FindDeep(panel, "CloseButton");
        if (close != null)
        {
            SetRect((RectTransform)close, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-14f, -12f), new Vector2(46f, 46f));
            TMP_Text own = close.GetComponent<TMP_Text>();
            if (own != null)
            {
                own.text = "";
                own.raycastTarget = true;
            }
            TMP_Text child = close.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.transform != close);
            if (child != null)
            {
                child.text = "×";
                StyleText(child, 40f, Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            }
            close.SetAsLastSibling();
        }
    }

    // ---------------------------------------------------------------- HUD

    private static void StyleHud(Transform canvas)
    {
        // Карточка задания (слева сверху)
        Image card = GetOrCreateImage(canvas, "QuestCard");
        card.sprite = LoadSprite("ui_card.png");
        card.type = Image.Type.Sliced;
        card.raycastTarget = false;
        SetTopLeft(card.rectTransform, 24f, -24f, 460f, 118f);

        TMP_Text header = GetOrCreateText(card.transform, "QuestHeader");
        header.text = "ЗАДАНИЕ";
        StyleText(header, 17f, Gold, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        header.characterSpacing = 6f;
        SetTopLeft(header.rectTransform, 22f, -12f, 300f, 26f);

        TMP_Text quest = Text(canvas, "QuestText");
        if (quest != null)
        {
            quest.transform.SetParent(card.transform, false);
            SetTopLeft(quest.rectTransform, 22f, -40f, 420f, 68f);
            StyleText(quest, 25f, Cream, TextAlignmentOptions.TopLeft, FontStyles.Normal);
            quest.richText = true;
        }

        // Счётчик яблок
        Image chip = GetOrCreateImage(canvas, "InventoryChip");
        chip.sprite = LoadSprite("ui_card.png");
        chip.type = Image.Type.Sliced;
        chip.raycastTarget = false;
        SetTopLeft(chip.rectTransform, 24f, -152f, 190f, 54f);

        Image appleIcon = GetOrCreateImage(chip.transform, "AppleIcon");
        appleIcon.sprite = LoadSprite("ui_apple_icon.png");
        appleIcon.preserveAspect = true;
        appleIcon.raycastTarget = false;
        SetRect(appleIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(12f, 0f), new Vector2(36f, 36f));

        TMP_Text inventory = Text(canvas, "InventoryText");
        if (inventory != null)
        {
            inventory.transform.SetParent(chip.transform, false);
            Stretch(inventory.rectTransform, 56f, 12f, 4f, 4f);
            StyleText(inventory, 23f, Cream, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            if (!inventory.text.Contains("×"))
                inventory.text = "Алма  <b>× 0</b>";
        }

        // XP (справа сверху)
        Image xpBadge = GetOrCreateImage(canvas, "XPBadge");
        xpBadge.sprite = LoadSprite("ui_card.png");
        xpBadge.type = Image.Type.Sliced;
        xpBadge.raycastTarget = false;
        SetRect(xpBadge.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-24f, -24f), new Vector2(200f, 58f));

        Image star = GetOrCreateImage(xpBadge.transform, "XPIcon");
        star.sprite = LoadSprite("ui_xp_icon.png");
        star.preserveAspect = true;
        star.raycastTarget = false;
        SetRect(star.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(14f, 0f), new Vector2(34f, 34f));

        TMP_Text xp = Text(canvas, "XPText");
        if (xp != null)
        {
            xp.transform.SetParent(xpBadge.transform, false);
            Stretch(xp.rectTransform, 56f, 14f, 4f, 4f);
            StyleText(xp, 26f, Cream, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        }

        // Всплывающее сообщение (сверху по центру)
        Image toast = GetOrCreateImage(canvas, "MessageToast");
        toast.sprite = LoadSprite("ui_card.png");
        toast.type = Image.Type.Sliced;
        toast.raycastTarget = false;
        SetRect(toast.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -28f), new Vector2(760f, 104f));

        TMP_Text message = Text(canvas, "QuestMessageText");
        if (message != null)
        {
            message.transform.SetParent(toast.transform, false);
            Stretch(message.rectTransform, 24f, 24f, 10f, 10f);
            StyleText(message, 24f, Cream, TextAlignmentOptions.Center, FontStyles.Normal);
        }
        toast.gameObject.SetActive(false);
    }

    private static void CreateHint(Transform canvas)
    {
        Image hint = GetOrCreateImage(canvas, "InteractionHintPanel");
        hint.sprite = LoadSprite("ui_card.png");
        hint.type = Image.Type.Sliced;
        hint.raycastTarget = false;
        SetRect(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 150f), new Vector2(460f, 56f));

        TMP_Text label = GetOrCreateText(hint.transform, "InteractionHintText");
        Stretch(label.rectTransform, 16f, 16f, 4f, 4f);
        StyleText(label, 23f, Cream, TextAlignmentOptions.Center, FontStyles.Normal);
        label.text = "E — поговорить";

        if (!hint.TryGetComponent(out CanvasGroup group))
            group = hint.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        if (!hint.TryGetComponent(out InteractionHint component))
            component = hint.gameObject.AddComponent<InteractionHint>();
        component.label = label;
        EditorUtility.SetDirty(component);

        // Подсказка не должна перекрывать панель разговора.
        Transform panel = canvas.Find("DialoguePanel");
        if (panel != null)
            panel.SetAsLastSibling();
    }

    // ---------------------------------------------------------------- apples

    private static int ReplaceApples(Scene scene)
    {
        GameObject appleAsset = AssetDatabase.LoadAssetAtPath<GameObject>(AppleModelPath);
        if (appleAsset == null)
        {
            Debug.LogWarning("[VillageUIStyler] Не найдена модель " + AppleModelPath);
            return 0;
        }

        int count = 0;
        foreach (ApplePickup pickup in scene.GetRootGameObjects()
                     .SelectMany(r => r.GetComponentsInChildren<ApplePickup>(true)))
        {
            Transform t = pickup.transform;

            Transform old = t.Find("AppleModel");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(appleAsset, t);
            model.name = "AppleModel";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            float parentScale = Mathf.Max(0.0001f, t.lossyScale.x);
            model.transform.localScale = Vector3.one * (0.36f / parentScale);

            if (t.TryGetComponent(out MeshRenderer sphere))
                sphere.enabled = false;

            if (!t.TryGetComponent(out PickupBob bob))
                bob = t.gameObject.AddComponent<PickupBob>();
            bob.model = model.transform;

            EditorUtility.SetDirty(t.gameObject);
            count++;
        }
        return count;
    }

    // ---------------------------------------------------------------- helpers

    private static Transform FindInScene(Scene scene, string name) =>
        scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == name);

    private static Transform FindDeep(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

    private static TMP_Text Text(Transform root, string name)
    {
        Transform t = FindDeep(root, name);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    private static RectTransform GetOrCreate(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return (RectTransform)existing;

        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image GetOrCreateImage(Transform parent, string name)
    {
        RectTransform rt = GetOrCreate(parent, name);
        if (!rt.TryGetComponent(out Image img))
            img = rt.gameObject.AddComponent<Image>();
        return img;
    }

    private static TMP_Text GetOrCreateText(Transform parent, string name)
    {
        RectTransform rt = GetOrCreate(parent, name);
        if (!rt.TryGetComponent(out TextMeshProUGUI txt))
            txt = rt.gameObject.AddComponent<TextMeshProUGUI>();
        txt.raycastTarget = false;
        return txt;
    }

    private static void StyleText(TMP_Text t, float size, Color color, TextAlignmentOptions align, FontStyles style)
    {
        t.enableAutoSizing = false;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = style;
        t.richText = true;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
        EditorUtility.SetDirty(t);
    }

    private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    private static void SetTopLeft(RectTransform rt, float x, float y, float w, float h) =>
        SetRect(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(w, h));

    private static void Stretch(RectTransform rt, float left, float right, float top, float bottom)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }
}
