using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Переносит локации из отдельных сцен в общую долину Village.
/// Шаг 1 — юрта: аксакал, стройплощадка, детали и квест переезжают в зону к югу от села
/// и подключаются к общему интерфейсу (список заданий, панель рассказа, опрос, подсказки).
/// Сцена YurtArea НЕ меняется — она остаётся как резервная копия.
/// Запуск: Tools > Diploma > Merge Yurt Zone Into Village.
/// </summary>
public static class ZoneMerger
{
    private const string VillagePath = "Assets/Game/Scenes/Village.unity";
    private const string YurtPath = "Assets/Game/Scenes/YurtArea.unity";
    private const string SpriteDir = "Assets/Game/UI/Sprites/";

    // Центр зоны юрты в долине (юг села, ровная поляна)
    private static readonly Vector3 YurtZoneCenter = new Vector3(-2f, 0f, -50f);

    private static readonly string[] YurtRoots = { "Aksakal", "YurtConstruction", "YurtItems", "YurtQuestManager", "yurt" };

    private static readonly Color Gold = new Color(0.84f, 0.66f, 0.29f);
    private static readonly Color Cream = new Color(0.96f, 0.93f, 0.85f);
    private static readonly Color Muted = new Color(0.79f, 0.75f, 0.65f);

    [MenuItem("Tools/Diploma/Merge Yurt Zone Into Village")]
    public static void MergeYurt()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // 1. Долина с длинной ровной поляной (место под зоны)
        MountainValleyInstaller.Install();

        Scene village = EditorSceneManager.OpenScene(VillagePath, OpenSceneMode.Single);
        if (FindIn(village, "Zone_Yurt") != null)
        {
            if (!EditorUtility.DisplayDialog("Зона юрты уже есть",
                    "В Village уже есть Zone_Yurt. Удалить её и перенести заново из YurtArea?", "Да", "Отмена"))
                return;
            Object.DestroyImmediate(FindIn(village, "Zone_Yurt").gameObject);
        }

        Scene yurt = EditorSceneManager.OpenScene(YurtPath, OpenSceneMode.Additive);

        // 2. Точка привязки: центр пола YurtArea
        Transform oldGround = FindIn(yurt, "Ground");
        Vector3 anchor = oldGround != null ? oldGround.position : Vector3.zero;
        float groundTop = 0f;
        if (oldGround != null && oldGround.TryGetComponent(out Renderer gr))
            groundTop = gr.bounds.max.y;

        var zone = new GameObject("Zone_Yurt");
        SceneManager.MoveGameObjectToScene(zone, village);
        zone.transform.position = YurtZoneCenter;

        int moved = 0;
        foreach (string rootName in YurtRoots)
        {
            GameObject go = yurt.GetRootGameObjects().FirstOrDefault(g => g.name == rootName);
            if (go == null)
            {
                Debug.LogWarning("[ZoneMerger] В YurtArea нет корневого объекта " + rootName);
                continue;
            }

            Vector3 offset = go.transform.position - anchor;
            offset.y = go.transform.position.y - groundTop;
            SceneManager.MoveGameObjectToScene(go, village);
            go.transform.SetParent(zone.transform, false);
            go.transform.position = YurtZoneCenter + offset;
            moved++;
        }

        // YurtArea закрываем без сохранения — файл сцены остаётся нетронутым
        EditorSceneManager.CloseScene(yurt, true);

        // 3. Общий интерфейс
        Transform canvas = FindIn(village, "GameCanvas");
        UiRefs ui = BuildSharedUi(canvas);

        // 4. Подключаем квест юрты
        var quest = zone.GetComponentInChildren<YurtQuestManager>(true);
        Transform player = FindIn(village, "Player");
        if (quest != null)
        {
            quest.player = player;
            quest.questText = ui.yurtQuestLine;
            quest.storyText = ui.storyText;
            quest.interactionText = null;          // подсказки идут через общий InteractionHint
            quest.carryText = ui.carryText;
            quest.quizPanel = ui.quizPanel;
            quest.questionText = ui.quizQuestion;
            quest.feedbackText = ui.quizFeedback;
            quest.answerButtons = ui.quizButtons;
            if (quest.aksakal == null) quest.aksakal = FindDeep(zone.transform, "Aksakal");
            EditorUtility.SetDirty(quest);
        }
        else
        {
            Debug.LogError("[ZoneMerger] YurtQuestManager не найден после переноса");
        }

        // 5. Портал в юрту больше не нужен
        Transform portal = FindIn(village, "Portal_Yurt");
        if (portal != null) portal.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(village);
        EditorSceneManager.SaveScene(village);
        Selection.activeGameObject = zone;
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log($"[ZoneMerger] Юрта перенесена в Village (объектов: {moved}). Зона: Zone_Yurt, к югу от села. " +
                  "Портал Portal_Yurt выключен. YurtArea не изменена.");
    }

    // ===================================================================== UI

    private struct UiRefs
    {
        public TMP_Text yurtQuestLine, storyText, carryText, quizQuestion, quizFeedback;
        public GameObject quizPanel;
        public Button[] quizButtons;
    }

    private static UiRefs BuildSharedUi(Transform canvas)
    {
        var refs = new UiRefs();

        // --- Левая колонка: карточка заданий (список), яблоки, «в руках»
        RectTransform left = GetOrCreate(canvas, "LeftHud");
        SetTopLeft(left, 24f, -24f, 470f, 700f);
        if (!left.TryGetComponent(out VerticalLayoutGroup leftLayout))
            leftLayout = left.gameObject.AddComponent<VerticalLayoutGroup>();
        leftLayout.spacing = 10f;
        leftLayout.childAlignment = TextAnchor.UpperLeft;
        leftLayout.childControlWidth = false;
        leftLayout.childControlHeight = true;
        leftLayout.childForceExpandWidth = false;
        leftLayout.childForceExpandHeight = false;

        Transform card = canvas.Find("QuestCard") ?? left.Find("QuestCard");
        Transform chip = canvas.Find("InventoryChip") ?? left.Find("InventoryChip");
        if (card != null) card.SetParent(left, false);
        if (chip != null) chip.SetParent(left, false);

        if (card != null)
        {
            ((RectTransform)card).sizeDelta = new Vector2(460f, 118f);
            if (!card.TryGetComponent(out VerticalLayoutGroup cardLayout))
                cardLayout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            cardLayout.padding = new RectOffset(22, 22, 12, 16);
            cardLayout.spacing = 8f;
            cardLayout.childAlignment = TextAnchor.UpperLeft;
            cardLayout.childControlWidth = true;
            cardLayout.childControlHeight = true;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;

            TMP_Text yurtLine = GetOrCreateText(card, "YurtQuestText");
            StyleText(yurtLine, 25f, Cream, TextAlignmentOptions.TopLeft, FontStyles.Normal);
            yurtLine.text = "<b>Боз үй</b>\n<size=85%><color=#C9BFA6>Поговори с аксакалом у юрты</color></size>";
            refs.yurtQuestLine = yurtLine;

            // Порядок: заголовок, яблоки, юрта
            Transform header = card.Find("QuestHeader");
            Transform apples = card.Find("QuestText");
            if (header != null) header.SetSiblingIndex(0);
            if (apples != null) apples.SetSiblingIndex(1);
            yurtLine.transform.SetAsLastSibling();
        }

        if (chip != null)
            Layout(chip, 190f, 54f);

        Image carry = GetOrCreateImage(left, "CarryChip");
        carry.sprite = Load("ui_card.png");
        carry.type = Image.Type.Sliced;
        carry.raycastTarget = false;
        carry.rectTransform.sizeDelta = new Vector2(260f, 50f);
        Layout(carry.transform, 260f, 50f);
        TMP_Text carryText = GetOrCreateText(carry.transform, "CarryText");
        Stretch(carryText.rectTransform, 18f, 14f, 4f, 4f);
        StyleText(carryText, 22f, Cream, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
        carryText.text = "";
        AddHideWhenEmpty(carry.gameObject, carryText, 0f);
        refs.carryText = carryText;

        // --- Панель рассказа (аксакал, эже, дети) внизу по центру
        Image story = GetOrCreateImage(canvas, "StoryPanel");
        story.sprite = Load("ui_panel.png");
        story.type = Image.Type.Sliced;
        story.raycastTarget = false;
        SetRect(story.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 28f), new Vector2(1060f, 150f));
        TMP_Text storyText = GetOrCreateText(story.transform, "StoryText");
        Stretch(storyText.rectTransform, 36f, 36f, 18f, 18f);
        StyleText(storyText, 24f, Cream, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
        storyText.text = "";
        AddHideWhenEmpty(story.gameObject, storyText, 12f);
        refs.storyText = storyText;

        // Подсказку «E — ...» поднимаем над панелью рассказа
        Transform hint = canvas.Find("InteractionHintPanel");
        if (hint != null)
            ((RectTransform)hint).anchoredPosition = new Vector2(0f, 196f);

        // --- Опрос (по центру экрана)
        Image quiz = GetOrCreateImage(canvas, "QuizPanel");
        quiz.sprite = Load("ui_panel.png");
        quiz.type = Image.Type.Sliced;
        quiz.raycastTarget = true;
        SetRect(quiz.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 20f), new Vector2(920f, 430f));

        TMP_Text qHeader = GetOrCreateText(quiz.transform, "QuizHeader");
        qHeader.text = "СУРОО  ·  ОПРОС";
        StyleText(qHeader, 18f, Gold, TextAlignmentOptions.Center, FontStyles.Bold);
        qHeader.characterSpacing = 6f;
        SetTop(qHeader.rectTransform, -22f, 860f, 28f);

        TMP_Text question = GetOrCreateText(quiz.transform, "QuizQuestion");
        StyleText(question, 32f, Cream, TextAlignmentOptions.Center, FontStyles.Bold);
        question.text = "Вопрос";
        SetTop(question.rectTransform, -58f, 840f, 80f);
        refs.quizQuestion = question;

        Image divider = GetOrCreateImage(quiz.transform, "Divider");
        divider.sprite = Load("ui_divider.png");
        divider.raycastTarget = false;
        SetTop(divider.rectTransform, -142f, 700f, 26f);

        var buttons = new List<Button>();
        for (int i = 0; i < 3; i++)
        {
            Image slot = GetOrCreateImage(quiz.transform, "QuizAnswer" + (i + 1));
            slot.sprite = Load("ui_button.png");
            slot.type = Image.Type.Sliced;
            slot.color = Color.white;
            SetTop(slot.rectTransform, -180f - i * 58f, 700f, 50f);

            // Текст ответа должен быть ПЕРВЫМ TMP в иерархии (его ищет GetComponentInChildren)
            TMP_Text label = GetOrCreateText(slot.transform, "Label");
            label.transform.SetSiblingIndex(0);
            Stretch(label.rectTransform, 62f, 18f, 0f, 0f);
            StyleText(label, 25f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);

            Image accent = GetOrCreateImage(slot.transform, "Accent");
            accent.color = Gold;
            accent.raycastTarget = false;
            SetRect(accent.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(8f, 0f), new Vector2(5f, -16f));

            TMP_Text number = GetOrCreateText(slot.transform, "Number");
            number.text = (i + 1).ToString();
            StyleText(number, 24f, Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            SetRect(number.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(18f, 0f), new Vector2(34f, 0f));

            if (!slot.TryGetComponent(out Button button))
                button = slot.gameObject.AddComponent<Button>();
            button.targetGraphic = slot;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.15f, 0.24f, 0.40f, 0.96f);
            colors.highlightedColor = new Color(0.25f, 0.37f, 0.58f, 1f);
            colors.pressedColor = new Color(0.62f, 0.48f, 0.20f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.15f, 0.20f, 0.28f, 0.85f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            EditorUtility.SetDirty(button);
            buttons.Add(button);
        }
        refs.quizButtons = buttons.ToArray();

        TMP_Text feedback = GetOrCreateText(quiz.transform, "QuizFeedback");
        StyleText(feedback, 22f, Muted, TextAlignmentOptions.Center, FontStyles.Normal);
        feedback.text = "";
        SetTop(feedback.rectTransform, -360f, 840f, 40f);
        refs.quizFeedback = feedback;

        TMP_Text keys = GetOrCreateText(quiz.transform, "QuizKeys");
        keys.text = "мышь или клавиши <color=#E8B04A><b>1  2  3</b></color>";
        StyleText(keys, 16f, Muted, TextAlignmentOptions.Center, FontStyles.Normal);
        SetRect(keys.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 14f), new Vector2(400f, 24f));

        quiz.gameObject.SetActive(false);
        refs.quizPanel = quiz.gameObject;

        // Панель разговора и опрос — поверх остального
        canvas.Find("DialoguePanel")?.SetAsLastSibling();
        quiz.transform.SetAsLastSibling();

        return refs;
    }

    // ================================================================ helpers

    private static Transform FindIn(Scene scene, string name) =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == name);

    private static Transform FindDeep(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

    private static Sprite Load(string file) => AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + file);

    private static void AddHideWhenEmpty(GameObject go, TMP_Text text, float autoHide)
    {
        if (!go.TryGetComponent(out CanvasGroup g)) g = go.AddComponent<CanvasGroup>();
        g.alpha = 0f;
        g.blocksRaycasts = false;
        if (!go.TryGetComponent(out HideWhenEmpty h)) h = go.AddComponent<HideWhenEmpty>();
        h.target = text;
        h.autoHideAfter = autoHide;
        EditorUtility.SetDirty(h);
    }

    private static void Layout(Transform t, float w, float h)
    {
        if (!t.TryGetComponent(out LayoutElement le)) le = t.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = w;
        le.preferredHeight = h;
        ((RectTransform)t).sizeDelta = new Vector2(w, h);
    }

    private static RectTransform GetOrCreate(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return (RectTransform)existing;
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
        if (!rt.TryGetComponent(out TextMeshProUGUI txt)) txt = rt.gameObject.AddComponent<TextMeshProUGUI>();
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

    private static void SetRect(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        rt.localScale = Vector3.one; rt.localRotation = Quaternion.identity;
    }

    private static void SetTopLeft(RectTransform rt, float x, float y, float w, float h) =>
        SetRect(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(w, h));

    // По центру по горизонтали, отсчёт от верхнего края родителя
    private static void SetTop(RectTransform rt, float y, float w, float h) =>
        SetRect(rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(w, h));

    private static void Stretch(RectTransform rt, float l, float r, float top, float bottom)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(l, bottom); rt.offsetMax = new Vector2(-r, -top);
        rt.localScale = Vector3.one; rt.localRotation = Quaternion.identity;
    }
}
