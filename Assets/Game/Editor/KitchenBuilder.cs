using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Заново собирает кухню (Zone_Food) из готовых моделей папки FoodIngredients:
/// навес с тандыром и казаном, два рабочих стола (боорсок слева, куурдак справа),
/// полка с посудой, дасторкон, где появляются готовые блюда. Квест кухни переподключается.
/// Кухня разворачивается лицом к селу. Остальная сцена не меняется.
/// Запуск: Tools > Diploma > Rebuild Kitchen (Food Zone).
/// </summary>
public static class KitchenBuilder
{
    private const string ScenePath = "Assets/Game/Scenes/Village.unity";
    private const string F = "Assets/Game/Models/FoodIngredients/";
    private const string I = "Assets/Game/Models/Items/";

    private const float TableTop = 0.96f;   // высота rolling_table
    private const float LowTop = 0.42f;     // высота столика дасторкона

    private static Transform kitchen;

    [MenuItem("Tools/Diploma/Rebuild Kitchen (Food Zone)")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform zone = Find(scene, "Zone_Food");
        if (zone == null) { Debug.LogError("[Kitchen] Нет Zone_Food"); return; }

        var cook = zone.GetComponentInChildren<FoodQuestManager>(true);
        if (cook == null) { Debug.LogError("[Kitchen] Нет FoodQuestManager"); return; }

        // 1. Убираем старое (оставляем квест и эже)
        var keep = new HashSet<Transform> { cook.transform };
        foreach (Transform t in zone.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Ezhe" && t.parent == zone))
            keep.Add(t);
        foreach (Transform child in zone.Cast<Transform>().ToList())
            if (!keep.Contains(child)) Object.DestroyImmediate(child.gameObject);

        // 2. Корень кухни, развёрнутый лицом к селу (к Айбеку)
        var root = new GameObject("Kitchen");
        root.transform.SetParent(zone, false);
        root.transform.localPosition = Vector3.zero;
        Transform aibek = Find(scene, "NPC_Aibek");
        if (aibek != null)
        {
            Vector3 d = aibek.position - zone.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f)
                root.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        }
        kitchen = root.transform;

        // 3. Навес с тандыром и казаном (в модели: тандыр слева, казан справа, лицом +Z)
        Put("KitchenShelter", F + "kitchen_shelter.obj", 0f, 0f, -1.8f, 0f, collider: true);
        Transform station = Empty("CookingStation", 1.0f, 0f, -0.4f);

        // Полка с посудой — за навесом
        Put("DishShelf", F + "dish_shelf.obj", 0f, 0f, -3.65f, 0f, collider: true);

        // Стол для боорсока — слева, если смотреть со стороны села
        Put("BoorsokTable", F + "rolling_table.obj", 3.8f, 0f, -1.2f, -90f, collider: true);
        GameObject water = Pickup("Суу (вода)", F + "water_bottle.obj", 3.75f, TableTop, -1.85f, 0f);
        GameObject oil = Pickup("Май (масло)", F + "oil_bottle.obj", 3.75f, TableTop, -1.55f, 0f);
        Put("DoughBoard", F + "dough_board.obj", 3.8f, TableTop, -0.85f, 0f);
        Put("RollingPin", F + "rolling_pin.obj", 3.8f, TableTop + 0.15f, -0.85f, 20f);
        GameObject flour = Pickup("Ун (мука)", F + "flour_sack.obj", 3.7f, 0f, 0.3f, 15f, groundCollider: true);

        // Стол для куурдака — справа, если смотреть со стороны села
        Put("KuurdakTable", F + "rolling_table.obj", -3.8f, 0f, -1.2f, 90f, collider: true);
        Put("CuttingBoard", F + "cutting_board.obj", -3.8f, TableTop, -1.75f, 90f);
        GameObject meat = Pickup("Эт (мясо)", F + "meat_plate.obj", -3.8f, TableTop + 0.02f, -1.75f, 0f);
        GameObject onion = Pickup("Пияз (лук)", I + "Onion.obj", -3.75f, TableTop, -1.05f, 0f, scale: 0.6f);
        GameObject potato = Pickup("Картошка", I + "Potato.obj", -3.85f, TableTop, -0.7f, 30f, scale: 0.6f);
        Put("Ladle", F + "ladle.obj", -3.6f, TableTop, -0.35f, 0f);
        Put("SlottedLadle", F + "slotted_ladle.obj", -3.95f, TableTop, -0.35f, 0f);

        // Дасторкон перед кухней — сюда ставятся готовые блюда
        Put("Dastorkon", F + "low_table_set.obj", 0f, 0f, 2.4f, 0f, collider: true);
        GameObject boorsokDish = Put("BoorsokDish", F + "pastry_plate.obj", -0.45f, LowTop, 2.4f, 0f);
        GameObject kuurdakDish = Put("KuurdakDish", F + "stew_bowl.obj", 0.45f, LowTop, 2.4f, 0f);
        Put("Mug1", F + "wooden_mug.obj", -0.95f, LowTop, 2.05f, 0f);
        Put("Mug2", F + "wooden_mug.obj", 0.95f, LowTop, 2.05f, 180f);
        Put("Bowl1", F + "clay_bowl.obj", 0f, LowTop, 2.85f, 0f);
        Put("Bowl2", F + "wooden_bowl.obj", -0.9f, LowTop, 2.8f, 0f);
        Put("VegPlate", F + "vegetables_plate.obj", 0.9f, LowTop, 2.8f, 0f);

        // 4. Эже — перед навесом, рядом с казаном
        Vector3 ezhePos = kitchen.TransformPoint(new Vector3(0f, 0f, 0.6f));
        foreach (Transform e in zone.Cast<Transform>().Where(t => t.name == "Ezhe"))
        {
            e.position = new Vector3(ezhePos.x, e.position.y, ezhePos.z);
            SnapBottom(e, zone.position.y);
            foreach (Renderer r in e.GetComponentsInChildren<Renderer>(true))
                if (!r.GetComponentsInParent<Transform>(true).Any(p => p.name == "EzheModel"))
                    r.enabled = false;   // старая модель эже
        }

        // 5. Подключаем квест
        cook.cookingStation = station;
        cook.boorsokIngredients = new[] { flour, water, oil };
        cook.kuurdakIngredients = new[] { meat, onion, potato };
        cook.boorsokDish = boorsokDish;
        cook.kuurdakDish = kuurdakDish;
        cook.interactionDistance = 2.8f;
        cook.cookingTime = 3f;
        if (cook.ezhe == null)
            cook.ezhe = zone.Cast<Transform>().FirstOrDefault(t => t.name == "Ezhe");
        EditorUtility.SetDirty(cook);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeTransform = kitchen;
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log("[Kitchen] Кухня собрана заново из моделей FoodIngredients, квест переподключён. Village сохранена.");
    }

    // ------------------------------------------------------------------ helpers

    private static Transform Empty(string name, float x, float y, float z)
    {
        var go = new GameObject(name);
        go.transform.SetParent(kitchen, false);
        go.transform.localPosition = new Vector3(x, y, z);
        return go.transform;
    }

    private static GameObject Put(string name, string path, float x, float y, float z, float yaw,
        bool collider = false, float scale = 1f)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            Debug.LogWarning("[Kitchen] Нет модели " + path);
            return Empty(name, x, y, z).gameObject;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, kitchen);
        go.name = name;
        go.transform.localPosition = new Vector3(x, y, z);
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one * scale;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);

        if (collider)
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
                if (!mf.TryGetComponent(out Collider _))
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        return go;
    }

    // Предмет для подбора: пустой объект с понятным именем + модель внутри
    private static GameObject Pickup(string name, string path, float x, float y, float z, float yaw,
        float scale = 1f, bool groundCollider = false)
    {
        var holder = Empty(name, x, y, z);
        holder.localRotation = Quaternion.Euler(0f, yaw, 0f);

        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset != null)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, holder);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * scale;
        }
        else Debug.LogWarning("[Kitchen] Нет модели " + path);

        if (groundCollider)
        {
            var box = holder.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.44f, 0f);
            box.size = new Vector3(0.7f, 0.88f, 0.7f);
        }
        return holder.gameObject;
    }

    private static void SnapBottom(Transform t, float y)
    {
        Renderer[] rs = t.GetComponentsInChildren<Renderer>(true).Where(r => r.GetComponentsInParent<Transform>(true).Any(p => p.name == "EzheModel")).ToArray();
        if (rs.Length == 0) return;
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        t.position += Vector3.up * (y - b.min.y);
    }

    private static Transform Find(Scene scene, string name) =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == name);
}
