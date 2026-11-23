using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Переносит площадку Чүкө атуу (GamesArea) и кухню (FoodArea) в общую долину Village
/// и подключает их к общему интерфейсу. Добавляет ингредиенты куурдака и общие стрелки-подсказки
/// для всех квестов. Сцены GamesArea и FoodArea не меняются (остаются резервными копиями).
/// Запуск: Tools > Diploma > Merge Games + Food Zones Into Village.
/// </summary>
public static class ZoneMergerGamesFood
{
    private const string VillagePath = "Assets/Game/Scenes/Village.unity";
    private const string GamesPath = "Assets/Game/Scenes/GamesArea.unity";
    private const string FoodPath = "Assets/Game/Scenes/FoodArea.unity";
    private const string ItemsDir = "Assets/Game/Models/Items/";

    private static readonly Vector3 GamesCenter = new Vector3(10f, 0f, 46f);   // север, за селом, справа от тропинки
    private static readonly Vector3 FoodCenter = new Vector3(-26f, 0f, -38f);  // юго-запад, напротив юрты

    private static readonly string[] SkipRoots =
        { "Portal_Back", "Directional Light", "Main Camera", "EventSystem", "GameCanvas", "GameManager", "Player", "Cube", "r", "Ground" };

    private static readonly Color Cream = new Color(0.96f, 0.93f, 0.85f);

    [MenuItem("Tools/Diploma/Merge Games + Food Zones Into Village")]
    public static void Merge()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene village = EditorSceneManager.OpenScene(VillagePath, OpenSceneMode.Single);

        foreach (string zoneName in new[] { "Zone_Games", "Zone_Food" })
        {
            Transform existing = Find(village, zoneName);
            if (existing == null) continue;
            if (!EditorUtility.DisplayDialog("Зона уже есть", zoneName + " уже есть в Village. Удалить и перенести заново?", "Да", "Отмена"))
                return;
            Object.DestroyImmediate(existing.gameObject);
        }

        Transform games = MoveZone(village, GamesPath, "Zone_Games", GamesCenter, "Cube");
        Transform food = MoveZone(village, FoodPath, "Zone_Food", FoodCenter, "r");

        Transform canvas = Find(village, "GameCanvas");
        Transform card = Find(village, "QuestCard");
        Transform storyT = Find(village, "StoryText");
        TMP_Text story = storyT != null ? storyT.GetComponent<TMP_Text>() : null;
        Transform player = Find(village, "Player");
        Camera mainCam = village.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
        GameObject quizTemplate = Find(village, "QuizPanel")?.gameObject;

        // ---------------- Чүкө атуу
        var chuko = games != null ? games.GetComponentInChildren<ChukoGameManager>(true) : null;
        if (chuko != null)
        {
            chuko.player = player;
            chuko.gameCamera = mainCam;
            chuko.storyBackground = null;            // панелью рассказа управляет HideWhenEmpty
            chuko.storyText = story;
            chuko.scoreText = null;                  // счёт теперь в строке задания
            chuko.interactionText = null;            // подсказки через общий InteractionHint
            chuko.questText = QuestLine(card, "GamesQuestText", "<b>Чүкө атуу</b>\n<size=85%><color=#C9BFA6>Поговори с ребёнком на лугу</color></size>");
            WireQuiz(canvas, quizTemplate, "QuizPanel_Games", "БАЛА  ·  СУРОО",
                out chuko.quizPanel, out chuko.questionText, out chuko.feedbackText, out chuko.answerButtons);
            EditorUtility.SetDirty(chuko);
        }
        else Debug.LogWarning("[ZoneMerger] ChukoGameManager не найден");

        // ---------------- Кухня
        var cook = food != null ? food.GetComponentInChildren<FoodQuestManager>(true) : null;
        if (cook != null)
        {
            cook.player = player;
            if (cook.ezhe == null)
                cook.ezhe = food.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name == "Ezhe")
                    .OrderByDescending(t => t.GetComponentsInChildren<Renderer>(true).Length)
                    .FirstOrDefault();
            cook.storyText = story;
            cook.storyPanel = null;
            cook.scoreText = null;
            cook.interactionText = null;
            cook.interactionDistance = 3f;
            cook.questText = QuestLine(card, "FoodQuestText", "<b>Улуттук тамак</b>\n<size=85%><color=#C9BFA6>Поговори с эже на кухне</color></size>");
            WireQuiz(canvas, quizTemplate, "QuizPanel_Food", "ЭЖЕ  ·  СУРОО",
                out cook.quizPanel, out cook.questionText, out cook.feedbackText, out cook.answerButtons);

            // Понятные названия продуктов (видны в подсказке «E — подобрать: ...»)
            RenameIn(food, "Flour", "Ун (мука)");
            RenameIn(food, "Water", "Суу (вода)");
            RenameIn(food, "OilForBoorsok", "Май (масло)");

            // Продукты для куурдака и само блюдо
            Transform station = cook.cookingStation != null ? cook.cookingStation : food;
            Transform ingredientsRoot = food.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Ingredients") ?? food;
            Vector3 basePos = new Vector3(station.position.x, 0f, station.position.z);
            cook.kuurdakIngredients = new[]
            {
                SpawnItem(ingredientsRoot, "Эт (мясо)", "Meat", basePos + new Vector3(-3.2f, 0f, -2.6f)),
                SpawnItem(ingredientsRoot, "Пияз (лук)", "Onion", basePos + new Vector3(3.4f, 0f, -2.2f)),
                SpawnItem(ingredientsRoot, "Картошка", "Potato", basePos + new Vector3(0.4f, 0f, -4.2f)),
            };

            if (cook.kuurdakDish == null)
            {
                GameObject bowl = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Models/FoodIngredients/stew_bowl.obj");
                if (bowl != null)
                {
                    var dish = (GameObject)PrefabUtility.InstantiatePrefab(bowl, food);
                    dish.name = "KuurdakDish";
                    Vector3 p = cook.boorsokDish != null ? cook.boorsokDish.transform.position + new Vector3(0.9f, 0f, 0f) : basePos + Vector3.up;
                    dish.transform.position = p;
                    cook.kuurdakDish = dish;
                }
            }
            EditorUtility.SetDirty(cook);
        }
        else Debug.LogWarning("[ZoneMerger] FoodQuestManager не найден");

        // ---------------- Общие стрелки-подсказки для всех квестов
        SetupHintDirector(village);

        // Айбек для стрелки квеста с яблоками
        var apples = village.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AppleQuestManager>(true)).FirstOrDefault();
        Transform aibek = Find(village, "NPC_Aibek");
        if (apples != null && aibek != null) { apples.aibek = aibek; EditorUtility.SetDirty(apples); }

        foreach (string portal in new[] { "Portal_Games", "Portal_Food", "Portal_Yurt" })
            Find(village, portal)?.gameObject.SetActive(false);

        canvas.Find("DialoguePanel")?.SetAsLastSibling();

        EditorSceneManager.MarkSceneDirty(village);
        EditorSceneManager.SaveScene(village);
        Debug.Log("[ZoneMerger] Чүкө атуу (север) и кухня (юго-запад) перенесены в Village. Порталы выключены, " +
                  "подсказки работают для всех квестов. GamesArea и FoodArea не изменены.");
    }

    // ===================================================================== helpers

    private static Transform MoveZone(Scene village, string scenePath, string zoneName, Vector3 center, string groundName)
    {
        Scene src = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        Transform ground = src.GetRootGameObjects().Select(g => g.transform).FirstOrDefault(t => t.name == groundName);
        Vector3 anchor = ground != null ? ground.position : Vector3.zero;
        float groundTop = ground != null && ground.TryGetComponent(out Renderer r) ? r.bounds.max.y : 0f;

        var zone = new GameObject(zoneName);
        SceneManager.MoveGameObjectToScene(zone, village);
        zone.transform.position = center;

        int moved = 0;
        foreach (GameObject go in src.GetRootGameObjects())
        {
            if (SkipRoots.Contains(go.name)) continue;
            Vector3 offset = go.transform.position - anchor;
            offset.y = go.transform.position.y - groundTop;
            SceneManager.MoveGameObjectToScene(go, village);
            go.transform.SetParent(zone.transform, false);
            go.transform.position = center + offset;
            moved++;
        }

        EditorSceneManager.CloseScene(src, true);   // без сохранения
        Debug.Log($"[ZoneMerger] {zoneName}: перенесено объектов {moved}");
        return zone.transform;
    }

    private static TMP_Text QuestLine(Transform card, string name, string text)
    {
        if (card == null) return null;
        Transform t = card.Find(name);
        TextMeshProUGUI tmp;
        if (t == null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = card.gameObject.layer;
            go.transform.SetParent(card, false);
            tmp = go.AddComponent<TextMeshProUGUI>();
        }
        else tmp = t.GetComponent<TextMeshProUGUI>();

        tmp.fontSize = 25f;
        tmp.color = Cream;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.richText = true;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.text = text;
        tmp.transform.SetAsLastSibling();
        EditorUtility.SetDirty(tmp);
        return tmp;
    }

    // Отдельная копия окна опроса для каждого квеста (у каждого свои кнопки и обработчики)
    private static void WireQuiz(Transform canvas, GameObject template, string name, string header,
        out GameObject panel, out TMP_Text question, out TMP_Text feedback, out Button[] buttons)
    {
        panel = null; question = null; feedback = null; buttons = new Button[0];
        if (canvas == null || template == null)
        {
            Debug.LogWarning("[ZoneMerger] Нет шаблона QuizPanel — сначала перенеси юрту");
            return;
        }

        Transform old = canvas.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        panel = Object.Instantiate(template, canvas);
        panel.name = name;
        panel.SetActive(false);

        question = panel.transform.Find("QuizQuestion")?.GetComponent<TMP_Text>();
        feedback = panel.transform.Find("QuizFeedback")?.GetComponent<TMP_Text>();
        TMP_Text head = panel.transform.Find("QuizHeader")?.GetComponent<TMP_Text>();
        if (head != null) head.text = header;
        Transform p = panel.transform;
        buttons = Enumerable.Range(1, 3)
            .Select(i => p.Find("QuizAnswer" + i)?.GetComponent<Button>())
            .Where(b => b != null).ToArray();
    }

    private static void RenameIn(Transform root, string from, string to)
    {
        Transform t = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == from);
        if (t != null) t.name = to;
    }

    private static GameObject SpawnItem(Transform parent, string name, string model, Vector3 position)
    {
        Transform old = parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = position;

        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ItemsDir + model + ".obj");
        if (asset != null)
        {
            var m = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
            m.name = model + "Model";
            m.transform.localPosition = Vector3.zero;
            m.transform.localScale = Vector3.one * 1.3f;
            root.AddComponent<PickupBob>().model = m.transform;
        }
        var box = root.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.25f, 0f);
        box.size = new Vector3(0.6f, 0.5f, 0.6f);
        return root;
    }

    private static void SetupHintDirector(Scene village)
    {
        Transform holder = Find(village, "QuestHints");
        if (holder == null)
        {
            var go = new GameObject("QuestHints");
            SceneManager.MoveGameObjectToScene(go, village);
            holder = go.transform;
        }

        if (!holder.TryGetComponent(out HintDirector director))
            director = holder.gameObject.AddComponent<HintDirector>();

        QuestArrow template = village.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<QuestArrow>(true))
            .FirstOrDefault(a => a.name == "QuestArrow");
        if (template != null)
        {
            template.transform.SetParent(holder, true);
            director.arrowTemplate = template;
        }
        else Debug.LogWarning("[ZoneMerger] Нет стрелки QuestArrow (её ставит Install Aksakal + Yurt Hints)");

        var yurt = village.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<YurtQuestManager>(true)).FirstOrDefault();
        if (yurt != null) { yurt.arrow = null; EditorUtility.SetDirty(yurt); }
        EditorUtility.SetDirty(director);
    }

    private static Transform Find(Scene scene, string name) =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == name);
}
