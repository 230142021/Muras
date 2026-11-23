using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Пересобирает сцену MainMenu: на фоне — настоящая долина из Village (горы, река, деревья, юрта, NPC)
/// с медленно плывущей камерой, слева — меню в стиле игры. Окна: Настройки, Как играть, Об игре,
/// подтверждение новой игры, экран загрузки.
/// Запуск: Tools > Diploma > Build Main Menu.
/// Фон копируется из Village при каждом запуске, поэтому после изменений села меню можно пересобрать.
/// </summary>
public static class MainMenuBuilder
{
    private const string MenuPath = "Assets/Game/Scenes/MainMenu.unity";
    private const string VillagePath = "Assets/Game/Scenes/Village.unity";
    private const string SpriteDir = "Assets/Game/UI/Sprites/";
    private const string AudioDir = "Assets/Game/Audio/";

    private static readonly Color Gold = new Color(0.84f, 0.66f, 0.29f);
    private static readonly Color Cream = new Color(0.96f, 0.93f, 0.85f);
    private static readonly Color Muted = new Color(0.79f, 0.75f, 0.65f);
    private static readonly Color WindowCol = new Color(0.11f, 0.085f, 0.065f, 0.97f);
    private static readonly Color ButtonCol = new Color(0.14f, 0.105f, 0.08f, 0.88f);

    // Английские варианты текстов меню (кыргызские заголовки остаются как есть)
    private static readonly Dictionary<string, string> En = new Dictionary<string, string>
    {
        { "Путешествие в мир кыргызской культуры", "A journey into Kyrgyz culture" },
        { "Продолжить", "Continue" }, { "Новая игра", "New game" }, { "Как играть", "How to play" },
        { "Настройки", "Settings" }, { "Об игре", "About" }, { "Выход", "Quit" },
        { "Дипломдук долбоор  ·  2026", "Дипломдук долбоор  ·  Diploma project  ·  2026" },
        { "Esc — закрыть окно", "Esc — close window" },
        { "ЖӨНДӨӨЛӨР  ·  НАСТРОЙКИ", "ЖӨНДӨӨЛӨР  ·  SETTINGS" },
        { "КАНТИП ОЙНОЙТ  ·  КАК ИГРАТЬ", "КАНТИП ОЙНОЙТ  ·  HOW TO PLAY" },
        { "ОЮН ЖӨНҮНДӨ  ·  ОБ ИГРЕ", "ОЮН ЖӨНҮНДӨ  ·  ABOUT" },
        { "ЖАҢЫ ОЮН", "ЖАҢЫ ОЮН  ·  NEW GAME" },
        { "Назад", "Back" }, { "Понятно", "Got it" }, { "Да, начать заново", "Yes, start over" }, { "Отмена", "Cancel" },
        { "Начать заново?", "Start over?" },
        { "Жүктөлүүдө…", "Жүктөлүүдө…  ·  Loading…" },
        { HowKeysRu, HowKeysEn }, { HowQuestsRu, HowQuestsEn }, { AboutRu, AboutEn },
    };

    private const string Author = "Arsen Torobekov";

    private const string HowKeysRu =
            "<color=#E8B04A><b>W A S D</b></color>   ходьба\n" +
            "<color=#E8B04A><b>Shift</b></color>   бег\n" +
            "<color=#E8B04A><b>Space</b></color>   прыжок\n" +
            "<color=#E8B04A><b>ПКМ + мышь</b></color>   повернуть камеру\n" +
            "<color=#E8B04A><b>Колесо</b></color>   приблизить камеру\n" +
            "<color=#E8B04A><b>E</b></color>   поговорить / взять\n" +
            "<color=#E8B04A><b>1 2 3</b></color>   ответы в вопросах\n" +
            "<color=#E8B04A><b>H</b></color>   подсказка-стрелка\n" +
            "<color=#E8B04A><b>C</b></color>   гардероб (купленная одежда)\n" +
            "<color=#E8B04A><b>J</b></color>   сөздүк: словарь и достижения\n" +
            "<color=#E8B04A><b>I / Tab</b></color>   капчык: инвентарь\n" +
            "<color=#E8B04A><b>T</b></color>   ускорить время суток\n" +
            "<color=#E8B04A><b>Esc</b></color>   пауза";
    private const string HowKeysEn =
            "<color=#E8B04A><b>W A S D</b></color>   walk\n" +
            "<color=#E8B04A><b>Shift</b></color>   run\n" +
            "<color=#E8B04A><b>Space</b></color>   jump\n" +
            "<color=#E8B04A><b>RMB + mouse</b></color>   rotate camera\n" +
            "<color=#E8B04A><b>Wheel</b></color>   zoom camera\n" +
            "<color=#E8B04A><b>E</b></color>   talk / pick up\n" +
            "<color=#E8B04A><b>1 2 3</b></color>   quiz answers\n" +
            "<color=#E8B04A><b>H</b></color>   hint arrow\n" +
            "<color=#E8B04A><b>C</b></color>   wardrobe (clothes you bought)\n" +
            "<color=#E8B04A><b>J</b></color>   сөздүк: dictionary and achievements\n" +
            "<color=#E8B04A><b>I / Tab</b></color>   kapchyk: inventory\n" +
            "<color=#E8B04A><b>T</b></color>   speed up the time of day\n" +
            "<color=#E8B04A><b>Esc</b></color>   pause";
    private const string HowQuestsRu =
            "<b>Задания в айыле</b>\n<size=85%><color=#C9BFA6>" +
            "• <b>Айбек</b> — твой друг и гид: поздоровайся, помогай жителям и рассказывай ему об успехах\n" +
            "• <b>Боз үй</b> — помоги аксакалу собрать юрту: кереге, уук, түндүк, кийиз\n" +
            "• <b>Чүкө атуу</b> — сыграй с детьми, выбей косточки из круга\n" +
            "• <b>Улуттук тамак</b> — приготовь с эже боорсок и куурдак\n\n" +
            "За каждое задание — XP. После каждого — короткий вопрос о культуре.\n" +
            "XP тратится на <b>базаре</b> у торговца Асана: одежда, шапки, умения, пёс и конь.\n" +
            "А ещё: комуз с Нурланом, шырдак с Апой, скачки ат чабыш, рыбалка и беркут Болота.</color></size>";
    private const string HowQuestsEn =
            "<b>Quests in the айыл</b>\n<size=85%><color=#C9BFA6>" +
            "• <b>Aibek</b> — your friend and guide: say hello, help the villagers and report back to him\n" +
            "• <b>Боз үй</b> — help the aksakal build a yurt: кереге, уук, түндүк, кийиз\n" +
            "• <b>Чүкө атуу</b> — play with the children, knock the knucklebones out of the circle\n" +
            "• <b>Улуттук тамак</b> — cook боорсок and куурдак with эже\n\n" +
            "Each quest gives XP and ends with a short question about the culture.\n" +
            "Spend XP at the <b>bazaar</b> with trader Asan: clothes, hats, skills, a dog and a horse.\n" +
            "Also: komuz with Nurlan, shyrdak with Apa, the at chabysh race, fishing and Bolot's eagle.</color></size>";
    private const string AboutRu =
            "<b><color=#E8B04A>MURAS</color></b> — по-кыргызски «наследие».\n\n" +
            "Образовательная 3D-игра о кыргызской культуре. Игрок попадает в горный айыл, " +
            "знакомится с жителями, учит кыргызские слова и традиции: приветствие старших, " +
            "сборку юрты, народную игру чүкө атуу и национальную кухню.\n\n" +
            "<size=85%><color=#C9BFA6>Дипломный проект  ·  2026\n" +
            "Автор: <b>" + Author + "</b>\n" +
            "Движок: Unity 6 (URP)</color></size>";
    private const string AboutEn =
            "<b><color=#E8B04A>MURAS</color></b> means \"heritage\" in Kyrgyz.\n\n" +
            "An educational 3D game about Kyrgyz culture. The player arrives in a mountain айыл, " +
            "meets the villagers and learns Kyrgyz words and traditions: greeting elders, " +
            "building a yurt, the folk game чүкө атуу and the national cuisine.\n\n" +
            "<size=85%><color=#C9BFA6>Diploma project  ·  2026\n" +
            "Author: <b>" + Author + "</b>\n" +
            "Engine: Unity 6 (URP)</color></size>";

    private static readonly string[] SkipRootNames =
        { "Player", "GameCanvas", "EventSystem", "Main Camera", "HintDirector", "PauseMenu" };

    [MenuItem("Tools/Diploma/Build Main Menu")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        PrepareSprites();

        // 1. Открываем Village и запоминаем освещение
        Scene village = EditorSceneManager.OpenScene(VillagePath, OpenSceneMode.Single);
        Material skybox = RenderSettings.skybox;
        AmbientMode ambientMode = RenderSettings.ambientMode;
        Color ambSky = RenderSettings.ambientSkyColor, ambEq = RenderSettings.ambientEquatorColor,
              ambGround = RenderSettings.ambientGroundColor, ambLight = RenderSettings.ambientLight;
        float ambIntensity = RenderSettings.ambientIntensity;
        bool fog = RenderSettings.fog;
        Color fogColor = RenderSettings.fogColor;
        FogMode fogMode = RenderSettings.fogMode;
        float fogDensity = RenderSettings.fogDensity, fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance;

        // 2. Новая пустая сцена меню
        Scene menu = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        // 3. Копируем декорации села
        var backdrop = new GameObject("Backdrop");
        SceneManager.MoveGameObjectToScene(backdrop, menu);
        Transform yurtFocus = null;
        Transform aibekCopy = null;
        GameObject eventSystemCopy = null;

        foreach (GameObject src in village.GetRootGameObjects())
        {
            if (src.name == "EventSystem")
            {
                eventSystemCopy = Object.Instantiate(src);
                eventSystemCopy.name = "EventSystem";
                SceneManager.MoveGameObjectToScene(eventSystemCopy, menu);
                continue;
            }
            if (SkipRootNames.Contains(src.name) || src.name.StartsWith("Apple") || src.name.StartsWith("Portal") ||
                src.name.Contains("Manager") || src.name.Contains("Audio")) continue;
            if (src.GetComponentInChildren<Camera>(true) != null || src.GetComponentInChildren<Canvas>(true) != null ||
                src.GetComponentInChildren<CharacterController>(true) != null) continue;

            GameObject copy = Object.Instantiate(src, src.transform.position, src.transform.rotation);
            copy.name = src.name;
            copy.SetActive(src.activeSelf);
            SceneManager.MoveGameObjectToScene(copy, menu);
            copy.transform.SetParent(backdrop.transform, true);

            // Юрта в меню стоит собранной
            foreach (YurtQuestManager yq in copy.GetComponentsInChildren<YurtQuestManager>(true))
            {
                foreach (GameObject b in new[] { yq.builtKerege, yq.builtUuk, yq.builtTunduk, yq.builtKiyiz })
                    if (b != null) b.SetActive(true);
                if (yq.builtKerege != null) yurtFocus = yq.builtKerege.transform;
            }
            if (src.name == "NPC_Aibek") aibekCopy = copy.transform;
        }

        // Убираем все скрипты, физику и звуки у копий (оставляем тексты TMP и Volume)
        StripComponents(backdrop);

        // 4. Освещение как в селе
        SceneManager.SetActiveScene(menu);
        RenderSettings.skybox = skybox;
        RenderSettings.ambientMode = ambientMode;
        RenderSettings.ambientSkyColor = ambSky;
        RenderSettings.ambientEquatorColor = ambEq;
        RenderSettings.ambientGroundColor = ambGround;
        RenderSettings.ambientLight = ambLight;
        RenderSettings.ambientIntensity = ambIntensity;
        RenderSettings.fog = fog;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogMode = fogMode;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;
        if (backdrop.GetComponentInChildren<Light>(true) == null)
        {
            var l = new GameObject("Directional Light").AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.2f;
            l.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            l.transform.SetParent(backdrop.transform, true);
        }

        // 5. Камера
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 50f;
        cam.nearClipPlane = 0.2f;
        cam.farClipPlane = 1500f;
        camGo.AddComponent<AudioListener>();
        var urp = cam.GetUniversalAdditionalCameraData();
        if (urp != null) urp.renderPostProcessing = true;
        var drift = camGo.AddComponent<MenuCameraDrift>();
        Transform focus = yurtFocus != null ? yurtFocus : aibekCopy;
        if (focus != null)
        {
            var pivot = new GameObject("CameraFocus").transform;
            pivot.position = focus.position;
            drift.target = pivot;
            if (aibekCopy != null && yurtFocus != null)
            {
                Vector3 d = aibekCopy.position - yurtFocus.position;   // смотрим со стороны села
                drift.baseAngle = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + 30f;
            }
        }
        Physics.SyncTransforms();

        // 6. EventSystem
        if (eventSystemCopy == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // 7. Интерфейс и музыка
        BuildUI();
        AddMenuMusic();

        // 8. Закрываем Village (без сохранения) и сохраняем меню
        EditorSceneManager.CloseScene(village, true);
        EditorSceneManager.SaveScene(menu, MenuPath);
        DynamicGI.UpdateEnvironment();
        EnsureBuildOrder();
        Debug.Log("[MainMenu] Главное меню собрано: фон из долины, кнопки, настройки, «Как играть», «Об игре», загрузка. Нажми Play в MainMenu.");
    }

    // Починка уже собранного меню без пересборки (сохраняет ручные правки, например имя автора)
    [MenuItem("Tools/Diploma/Fix Main Menu Buttons")]
    public static void FixButtons()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        Scene menu = EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
        int n = 0;
        foreach (Button b in menu.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Button>(true)))
            if (b.targetGraphic != null && !b.targetGraphic.raycastTarget) { b.targetGraphic.raycastTarget = true; EditorUtility.SetDirty(b.targetGraphic); n++; }
            else if (b.targetGraphic == null && b.TryGetComponent(out Image img) && !img.raycastTarget) { img.raycastTarget = true; EditorUtility.SetDirty(img); n++; }
        EditorSceneManager.MarkSceneDirty(menu);
        EditorSceneManager.SaveScene(menu);
        Debug.Log("[MainMenu] Кнопок исправлено: " + n);
    }

    [MenuItem("Tools/Diploma/Apply Author Name")]
    public static void ApplyAuthor()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        Scene menu = EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
        foreach (LocalizedText lt in menu.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LocalizedText>(true)))
        {
            if (lt.ru == null || !lt.ru.Contains("Автор:")) continue;
            lt.ru = AboutRu;
            lt.en = AboutEn;
            lt.Apply();
            EditorUtility.SetDirty(lt);
            EditorUtility.SetDirty(lt.GetComponent<TMP_Text>());
        }
        EditorSceneManager.MarkSceneDirty(menu);
        EditorSceneManager.SaveScene(menu);
        Debug.Log("[MainMenu] Автор: " + Author);
    }

    // ================================================================= музыка

    private const string MusicPath = AudioDir + "muras_menu_theme.ogg";

    [MenuItem("Tools/Diploma/Add Menu Music")]
    public static void AddMusicToMenuScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        Scene menu = EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
        AddMenuMusic();
        EditorSceneManager.MarkSceneDirty(menu);
        EditorSceneManager.SaveScene(menu);
        Debug.Log("[MainMenu] Музыка добавлена в главное меню (объект MenuMusic).");
    }

    private static void AddMenuMusic()
    {
        if (AssetImporter.GetAtPath(MusicPath) is AudioImporter ai)
        {
            var s = ai.defaultSampleSettings;
            s.loadType = AudioClipLoadType.Streaming;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            ai.defaultSampleSettings = s;
            ai.loadInBackground = true;
            ai.SaveAndReimport();
        }
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
        if (clip == null) { Debug.LogWarning("[MainMenu] Нет музыки " + MusicPath); return; }

        GameObject go = GameObject.Find("MenuMusic");
        if (go == null) go = new GameObject("MenuMusic");
        if (!go.TryGetComponent(out AudioSource src)) src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        if (go.GetComponent<MenuMusic>() == null) go.AddComponent<MenuMusic>();
    }

    // ================================================================= UI

    private static void BuildUI()
    {
        var canvasGo = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        Transform c = canvasGo.transform;

        var ctrl = canvasGo.AddComponent<MainMenuController>();
        ctrl.hoverClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "blip_2.wav");
        ctrl.clickClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "pickup.wav");

        // Затемнения
        Img("SideGradient", c, LoadSprite("ui_menu_gradient.png"), Color.white, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(1100, 0));
        Img("BottomShade", c, LoadSprite("ui_menu_vignette.png"), Color.white, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 260));

        // Заголовок
        var title = Txt("Title", c, "MURAS", 128, Gold, TL(new Vector2(110, -95), new Vector2(700, 150)), FontStyles.Bold, TextAlignmentOptions.TopLeft);
        title.characterSpacing = 14f;
        title.outlineWidth = 0.08f;
        title.outlineColor = new Color32(40, 25, 12, 255);
        Txt("Subtitle", c, "Кыргыз маданиятына саякат", 34, Cream, TL(new Vector2(116, -245), new Vector2(800, 50)), FontStyles.Normal, TextAlignmentOptions.TopLeft);
        Txt("SubtitleRu", c, "Путешествие в мир кыргызской культуры", 23, Muted, TL(new Vector2(117, -292), new Vector2(800, 36)), FontStyles.Italic, TextAlignmentOptions.TopLeft);
        var div = Img("Divider", c, LoadSprite("ui_divider.png"), new Color(1, 1, 1, 0.9f), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(112, -345), new Vector2(430, 18));
        div.preserveAspect = true;

        // Кнопки
        var list = new GameObject("Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup));
        list.transform.SetParent(c, false);
        var lrt = (RectTransform)list.transform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0, 1);
        lrt.pivot = new Vector2(0, 1);
        lrt.anchoredPosition = new Vector2(110, -390);
        lrt.sizeDelta = new Vector2(470, 560);
        var vlg = list.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 12f;
        vlg.childControlHeight = false; vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = true;
        vlg.childAlignment = TextAnchor.UpperLeft;

        Button bContinue = MenuButton(list.transform, "Улантуу", "Продолжить", out TMP_Text continueSub);
        Button bNew = MenuButton(list.transform, "Жаңы оюн", "Новая игра", out _);
        Button bHow = MenuButton(list.transform, "Кантип ойнойт", "Как играть", out _);
        Button bSettings = MenuButton(list.transform, "Жөндөөлөр", "Настройки", out _);
        Button bAbout = MenuButton(list.transform, "Оюн жөнүндө", "Об игре", out _);
        Button bExit = MenuButton(list.transform, "Чыгуу", "Выход", out _);
        if (continueSub.TryGetComponent(out LocalizedText cl)) Object.DestroyImmediate(cl);   // этот текст пишет контроллер
        ctrl.continueButton = bContinue;
        ctrl.continueInfo = continueSub;
        UnityEventTools.AddPersistentListener(bContinue.onClick, ctrl.PlayGame);
        UnityEventTools.AddPersistentListener(bNew.onClick, ctrl.NewGame);
        UnityEventTools.AddPersistentListener(bHow.onClick, ctrl.OpenHowTo);
        UnityEventTools.AddPersistentListener(bSettings.onClick, ctrl.OpenSettings);
        UnityEventTools.AddPersistentListener(bAbout.onClick, ctrl.OpenAbout);
        UnityEventTools.AddPersistentListener(bExit.onClick, ctrl.ExitGame);

        // Подвал
        var footer = Txt("Footer", c, "Дипломдук долбоор  ·  2026", 18, Muted, new Rect(0, 0, 0, 0), FontStyles.Normal, TextAlignmentOptions.BottomLeft);
        Place(footer.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(112, 40), new Vector2(700, 30));
        var keys = Txt("KeysHint", c, "Esc — закрыть окно", 18, Muted, new Rect(0, 0, 0, 0), FontStyles.Normal, TextAlignmentOptions.BottomRight);
        Place(keys.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-50, 40), new Vector2(500, 30));

        // Окна
        ctrl.settingsPanel = BuildSettings(c, ctrl);
        ctrl.howToPanel = BuildHowTo(c, ctrl);
        ctrl.aboutPanel = BuildAbout(c, ctrl);
        ctrl.confirmPanel = BuildConfirm(c, ctrl, out ctrl.confirmText);

        // Загрузка и затемнение
        ctrl.loading = BuildLoading(c, out ctrl.loadingBar, out ctrl.loadingTip);
        var fadeGo = new GameObject("Fade", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        fadeGo.transform.SetParent(c, false);
        Stretch((RectTransform)fadeGo.transform);
        fadeGo.GetComponent<Image>().color = Color.black;
        ctrl.fade = fadeGo.GetComponent<CanvasGroup>();
        ctrl.fade.alpha = 0f;
        fadeGo.SetActive(false);
    }

    private static Button MenuButton(Transform parent, string title, string sub, out TMP_Text subText)
    {
        var go = new GameObject("Button_" + sub.Replace(" ", ""), typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        ((RectTransform)go.transform).sizeDelta = new Vector2(470, 76);
        var bg = go.GetComponent<Image>();
        bg.sprite = LoadSprite("ui_button.png");
        bg.type = bg.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        bg.color = ButtonCol;
        var b = go.GetComponent<Button>();
        b.transition = Selectable.Transition.None;
        var nav = b.navigation; nav.mode = Navigation.Mode.Vertical; b.navigation = nav;

        var acc = Img("Accent", go.transform, null, new Color(Gold.r, Gold.g, Gold.b, 0f), new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(6, 0), new Vector2(6, -18));
        acc.raycastTarget = false;

        var label = Txt("Title", go.transform, title, 30, Cream, new Rect(0, 0, 0, 0), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        Place(label.rectTransform, new Vector2(0, 0.42f), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(30, 0), Vector2.zero);
        label.rectTransform.offsetMin = new Vector2(30, 0); label.rectTransform.offsetMax = new Vector2(-20, -4);
        subText = Txt("Sub", go.transform, sub, 18, Muted, new Rect(0, 0, 0, 0), FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        Place(subText.rectTransform, new Vector2(0, 0), new Vector2(1, 0.45f), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
        subText.rectTransform.offsetMin = new Vector2(31, 6); subText.rectTransform.offsetMax = new Vector2(-20, 0);

        var fx = go.AddComponent<MenuButtonFX>();
        fx.accent = acc; fx.label = label; fx.background = bg;
        fx.bgNormal = ButtonCol;
        return b;
    }

    // ---------------------------------------------------------------- окна

    private static GameObject Window(Transform canvas, string name, string title, Vector2 size, out Transform body)
    {
        var overlay = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        overlay.transform.SetParent(canvas, false);
        Stretch((RectTransform)overlay.transform);
        overlay.GetComponent<Image>().color = new Color(0, 0, 0, 0.45f);

        var w = Img("Window", overlay.transform, LoadSprite("ui_panel.png"), WindowCol, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(230, 0), size);
        if (w.sprite != null) w.type = Image.Type.Sliced;
        Img("TopLine", w.transform, null, Gold, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -2), new Vector2(-60, 5));
        var t = Txt("Header", w.transform, title, 40, Gold, new Rect(0, 0, 0, 0), FontStyles.Bold, TextAlignmentOptions.Center);
        Place(t.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(-60, 56));
        body = w.transform;
        overlay.SetActive(false);
        return overlay;
    }

    private static Button WindowButton(Transform window, string title, string sub, Vector2 pos, float width = 300f)
    {
        var b = MenuButton(window, title, sub, out _);
        var rt = (RectTransform)b.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(width, 72);
        return b;
    }

    private static GameObject BuildSettings(Transform canvas, MainMenuController ctrl)
    {
        GameObject panel = Window(canvas, "SettingsPanel", "ЖӨНДӨӨЛӨР  ·  НАСТРОЙКИ", new Vector2(820, 720), out Transform w);
        var sp = panel.AddComponent<SettingsPanel>();

        sp.volumeSlider = SliderRow(w, "Үн", "Громкость", "Volume", 190, out sp.volumeValue);
        sp.sensitivitySlider = SliderRow(w, "Чычкан", "Чувствительность мыши", "Mouse sensitivity", 105, out sp.sensitivityValue);

        // Полный экран
        RowLabel(w, "Толук экран", "Полный экран", "Fullscreen", 20);
        var tgo = DefaultControls.CreateToggle(new DefaultControls.Resources());
        tgo.transform.SetParent(w, false);
        Object.DestroyImmediate(tgo.transform.Find("Label").gameObject);
        var trt = (RectTransform)tgo.transform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
        trt.anchoredPosition = new Vector2(150, 20);
        trt.sizeDelta = new Vector2(40, 40);
        var bgT = tgo.transform.Find("Background");
        var bgRt = (RectTransform)bgT;
        bgRt.anchorMin = bgRt.anchorMax = new Vector2(0, 0.5f);
        bgRt.anchoredPosition = new Vector2(20, 0); bgRt.sizeDelta = new Vector2(40, 40);
        bgT.GetComponent<Image>().color = new Color(0.25f, 0.21f, 0.17f);
        var check = bgT.Find("Checkmark");
        ((RectTransform)check).sizeDelta = new Vector2(26, 26);
        check.GetComponent<Image>().color = Gold;
        check.GetComponent<Image>().sprite = null;
        sp.fullscreenToggle = tgo.GetComponent<Toggle>();

        // Графика
        RowLabel(w, "Графика", "Качество", "Quality", -65);
        sp.qualityPrev = SmallButton(w, "<", new Vector2(150, -65));
        sp.qualityValue = Txt("QualityValue", w, "Medium", 24, Gold, Center(new Vector2(250, -65), new Vector2(150, 40)), FontStyles.Bold, TextAlignmentOptions.Center);
        sp.qualityNext = SmallButton(w, ">", new Vector2(350, -65));

        // Язык
        RowLabel(w, "Тил", "Язык", "Language", -150);
        sp.languagePrev = SmallButton(w, "<", new Vector2(150, -150));
        sp.languageValue = Txt("LanguageValue", w, "Русский", 24, Gold, Center(new Vector2(250, -150), new Vector2(150, 40)), FontStyles.Bold, TextAlignmentOptions.Center);
        sp.languageNext = SmallButton(w, ">", new Vector2(350, -150));

        Button back = WindowButton(w, "Артка", "Назад", new Vector2(0, 34));
        UnityEventTools.AddPersistentListener(back.onClick, ctrl.CloseAll);
        return panel;
    }

    private static GameObject BuildHowTo(Transform canvas, MainMenuController ctrl)
    {
        GameObject panel = Window(canvas, "HowToPanel", "КАНТИП ОЙНОЙТ  ·  КАК ИГРАТЬ", new Vector2(980, 700), out Transform w);
        string keys = HowKeysRu;
        string quests = HowQuestsRu;
        var left = Txt("Keys", w, keys, 24, Cream, new Rect(0, 0, 0, 0), FontStyles.Normal, TextAlignmentOptions.TopLeft);
        Place(left.rectTransform, new Vector2(0, 0), new Vector2(0.45f, 1), new Vector2(0, 1), Vector2.zero, Vector2.zero);
        left.rectTransform.offsetMin = new Vector2(60, 130); left.rectTransform.offsetMax = new Vector2(0, -105);
        left.lineSpacing = 18f;
        var right = Txt("Quests", w, quests, 25, Cream, new Rect(0, 0, 0, 0), FontStyles.Normal, TextAlignmentOptions.TopLeft);
        Place(right.rectTransform, new Vector2(0.45f, 0), new Vector2(1, 1), new Vector2(0, 1), Vector2.zero, Vector2.zero);
        right.rectTransform.offsetMin = new Vector2(20, 130); right.rectTransform.offsetMax = new Vector2(-60, -105);
        right.lineSpacing = 12f;

        Button back = WindowButton(w, "Түшүндүм", "Понятно", new Vector2(0, 34));
        UnityEventTools.AddPersistentListener(back.onClick, ctrl.CloseAll);
        return panel;
    }

    private static GameObject BuildAbout(Transform canvas, MainMenuController ctrl)
    {
        GameObject panel = Window(canvas, "AboutPanel", "ОЮН ЖӨНҮНДӨ  ·  ОБ ИГРЕ", new Vector2(900, 620), out Transform w);
        string text = AboutRu;
        var t = Txt("AboutText", w, text, 26, Cream, new Rect(0, 0, 0, 0), FontStyles.Normal, TextAlignmentOptions.TopLeft);
        Place(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        t.rectTransform.offsetMin = new Vector2(70, 130); t.rectTransform.offsetMax = new Vector2(-70, -110);

        Button back = WindowButton(w, "Артка", "Назад", new Vector2(0, 34));
        UnityEventTools.AddPersistentListener(back.onClick, ctrl.CloseAll);
        return panel;
    }

    private static GameObject BuildConfirm(Transform canvas, MainMenuController ctrl, out TMP_Text text)
    {
        GameObject panel = Window(canvas, "ConfirmPanel", "ЖАҢЫ ОЮН", new Vector2(680, 400), out Transform w);
        text = Txt("ConfirmText", w, "Начать заново?", 30, Cream, Center(new Vector2(0, 25), new Vector2(580, 130)), FontStyles.Normal, TextAlignmentOptions.Center);
        Button yes = WindowButton(w, "Ооба", "Да, начать заново", new Vector2(-150, 34), 270f);
        Button no = WindowButton(w, "Жок", "Отмена", new Vector2(150, 34), 270f);
        UnityEventTools.AddPersistentListener(yes.onClick, ctrl.ConfirmNewGame);
        UnityEventTools.AddPersistentListener(no.onClick, ctrl.CloseAll);
        return panel;
    }

    private static CanvasGroup BuildLoading(Transform canvas, out Image bar, out TMP_Text tip)
    {
        var go = new GameObject("Loading", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        go.transform.SetParent(canvas, false);
        Stretch((RectTransform)go.transform);
        go.GetComponent<Image>().color = new Color(0.07f, 0.055f, 0.04f, 1f);

        var title = Txt("Title", go.transform, "MURAS", 96, Gold, Center(new Vector2(0, 120), new Vector2(900, 130)), FontStyles.Bold, TextAlignmentOptions.Center);
        title.characterSpacing = 14f;
        tip = Txt("Tip", go.transform, "", 28, Cream, Center(new Vector2(0, -10), new Vector2(1200, 90)), FontStyles.Italic, TextAlignmentOptions.Center);

        var track = Img("Track", go.transform, null, new Color(0.25f, 0.21f, 0.17f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -110), new Vector2(620, 12));
        bar = Img("Bar", track.transform, null, Gold, Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
        bar.type = Image.Type.Filled;
        bar.fillMethod = Image.FillMethod.Horizontal;
        bar.fillAmount = 0f;
        Txt("Label", go.transform, "Жүктөлүүдө…", 22, Muted, Center(new Vector2(0, -150), new Vector2(600, 40)), FontStyles.Normal, TextAlignmentOptions.Center);

        go.SetActive(false);
        return go.GetComponent<CanvasGroup>();
    }

    private static Slider SliderRow(Transform w, string kg, string ru, string en, float y, out TMP_Text value)
    {
        RowLabel(w, kg, ru, en, y);
        var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
        go.transform.SetParent(w, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(205, y);
        rt.sizeDelta = new Vector2(270, 22);
        PauseMenu.StyleSlider(go);
        value = Txt("Value", w, "", 24, Gold, Center(new Vector2(345, y), new Vector2(110, 40)), FontStyles.Bold, TextAlignmentOptions.Right);
        ((RectTransform)value.transform).pivot = new Vector2(0, 0.5f);
        return go.GetComponent<Slider>();
    }

    private static void RowLabel(Transform w, string kg, string ru, string en, float y)
    {
        string Line(string s) => kg + "  <size=75%><color=#C9BFA6>" + s + "</color></size>";
        var t = Txt("Label_" + en, w, Line(ru), 28, Cream,
            Center(new Vector2(-170, y), new Vector2(420, 50)), FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        AddLoc(t, Line(ru), Line(en));
    }

    private static Button SmallButton(Transform parent, string text, Vector2 pos)
    {
        var img = Img("Btn" + (text == "<" ? "Prev" : "Next"), parent, LoadSprite("ui_button.png"), new Color(0.24f, 0.17f, 0.11f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(50, 50));
        if (img.sprite != null) img.type = Image.Type.Sliced;
        img.raycastTarget = true;   // иначе кнопка не ловит клики
        var b = img.gameObject.AddComponent<Button>();
        var colors = b.colors; colors.highlightedColor = new Color(1.3f, 1.2f, 1f); b.colors = colors;
        Txt("Text", img.transform, text, 30, Cream, Center(Vector2.zero, new Vector2(50, 50)), FontStyles.Bold, TextAlignmentOptions.Center);
        return b;
    }

    // ---------------------------------------------------------------- helpers

    private static Rect TL(Vector2 pos, Vector2 size) => new Rect(pos.x, pos.y, size.x, size.y);
    private static Rect Center(Vector2 pos, Vector2 size) => new Rect(pos.x, pos.y, -size.x, -size.y);   // отрицательный размер = якорь в центре

    private static TMP_Text Txt(string name, Transform parent, string text, float size, Color color, Rect r, FontStyles style, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = color; t.fontStyle = style; t.alignment = align;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        if (text != null && En.TryGetValue(text, out string en)) AddLoc(t, text, en);
        var rt = t.rectTransform;
        if (r.width < 0)   // по центру родителя
            Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(r.x, r.y), new Vector2(-r.width, -r.height));
        else if (r.width > 0)   // от левого верхнего угла
            Place(rt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(r.x, r.y), new Vector2(r.width, r.height));
        return t;
    }

    private static void AddLoc(TMP_Text t, string ru, string en)
    {
        if (!t.TryGetComponent(out LocalizedText lt)) lt = t.gameObject.AddComponent<LocalizedText>();
        lt.ru = ru;
        lt.en = en;
    }

    private static Image Img(string name, Transform parent, Sprite sprite, Color color, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        Place((RectTransform)go.transform, aMin, aMax, pivot, pos, size);
        return img;
    }

    private static void Place(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static Sprite LoadSprite(string file) => AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + file);

    private static void PrepareSprites()
    {
        foreach (string file in new[] { "ui_menu_gradient.png", "ui_menu_vignette.png" })
            if (AssetImporter.GetAtPath(SpriteDir + file) is TextureImporter ti && ti.textureType != TextureImporterType.Sprite)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
    }

    private static void StripComponents(GameObject root)
    {
        // Несколько проходов: сначала скрипты (они могут требовать физику), потом физику и звук
        for (int pass = 0; pass < 3; pass++)
        {
            foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb is TMP_Text || mb is Volume) continue;
                Object.DestroyImmediate(mb);
            }
            foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
            foreach (AudioSource a in root.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(a);
            foreach (CharacterController cc in root.GetComponentsInChildren<CharacterController>(true)) Object.DestroyImmediate(cc);
            foreach (LineRenderer lr in root.GetComponentsInChildren<LineRenderer>(true)) Object.DestroyImmediate(lr);
        }
    }

    private static void EnsureBuildOrder()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        scenes.RemoveAll(s => s.path == MenuPath || s.path == VillagePath || !System.IO.File.Exists(s.path));
        scenes.Insert(0, new EditorBuildSettingsScene(VillagePath, true));
        scenes.Insert(0, new EditorBuildSettingsScene(MenuPath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
