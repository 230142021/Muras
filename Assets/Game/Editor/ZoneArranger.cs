using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Аккуратно расставляет зоны Чүкө атуу и кухни в долине (на ровном лугу, не задевая село и юрту),
/// наводит порядок внутри зон и ставит модели эже, девочки и мальчиков вместо капсул.
/// Запуск: Tools > Diploma > Arrange Zones + NPC Models.
/// </summary>
public static class ZoneArranger
{
    private const string ScenePath = "Assets/Game/Scenes/Village.unity";
    private const string NpcDir = "Assets/Game/Models/NPC/";
    private const string ItemsDir = "Assets/Game/Models/Items/";

    [MenuItem("Tools/Diploma/Arrange Zones + NPC Models")]
    public static void Arrange()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform games = Find(scene, "Zone_Games");
        Transform food = Find(scene, "Zone_Food");
        Transform yurt = Find(scene, "Zone_Yurt");
        Transform env = Find(scene, "Environment");
        if (games == null || food == null)
        {
            Debug.LogError("[ZoneArranger] Нет Zone_Games / Zone_Food — сначала Merge Games + Food Zones Into Village");
            return;
        }

        Physics.SyncTransforms();

        // 1. Порядок внутри зон (в координатах зоны)
        LayoutGames(games);
        LayoutFood(food);

        // 2. Где свободно: село и юрта — препятствия
        var obstacles = new List<Bounds>();
        AddBounds(obstacles, env, t => !t.name.StartsWith("Ground"));
        Bounds village = obstacles.Count > 0 ? obstacles[0] : new Bounds(Vector3.zero, Vector3.one * 40f);
        foreach (Bounds b in obstacles) village.Encapsulate(b);

        AddBounds(obstacles, yurt, _ => true);
        foreach (string n in new[] { "NPC_Aibek", "Apple_01", "Apple_01 (1)", "Apple_01 (2)" })
            AddBounds(obstacles, Find(scene, n), _ => true);

        Vector3 vc = village.center;
        float reach = Mathf.Max(village.extents.x, village.extents.z);

        // 3. Ищем ровное свободное место: игры — к северу, кухня — к западу
        Vector3 gamesPos = FindSpot(games, 12f, vc + Vector3.forward * (reach + 16f), obstacles, scene);
        Place(games, gamesPos);
        AddBounds(obstacles, games, _ => true);

        Vector3 foodPos = FindSpot(food, 12f, vc + Vector3.left * (reach + 16f), obstacles, scene);
        Place(food, foodPos);

        // 4. Модели NPC
        var chuko = games.GetComponentInChildren<ChukoGameManager>(true);
        Transform boy = chuko != null && chuko.childNPC != null ? chuko.childNPC : Deep(games, "Child_NPC");
        if (boy != null) YurtZoneDresser.InstallNpcModel(boy.gameObject, NpcDir + "Child_Boy.obj", "ChildModel");
        Transform girl = Deep(games, "Child_NPC (1)");
        if (girl != null) YurtZoneDresser.InstallNpcModel(girl.gameObject, NpcDir + "Child_Girl.obj", "ChildModel");
        Transform boy2 = Deep(games, "Child_NPC (2)");
        if (boy2 != null) YurtZoneDresser.InstallNpcModel(boy2.gameObject, NpcDir + "Child_Boy2.obj", "ChildModel");

        var cook = food.GetComponentInChildren<FoodQuestManager>(true);
        if (cook != null && cook.ezhe != null)
            InstallOverExistingModel(cook.ezhe.gameObject, NpcDir + "Ezhe.obj", "EzheModel");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.objects = new Object[] { games.gameObject, food.gameObject };
        Debug.Log($"[ZoneArranger] Готово. Чүкө атуу: {gamesPos}, кухня: {foodPos}. Модели эже и детей установлены. Village сохранена.");
    }

    // ------------------------------------------------------------------ Games

    private static void LayoutGames(Transform zone)
    {
        Vector3 Z(float x, float y, float z) => zone.position + new Vector3(x, y, z);
        var chuko = zone.GetComponentInChildren<ChukoGameManager>(true);

        Transform playground = Deep(zone, "ChukoPlayground");
        if (playground != null) playground.position = Z(0f, 0f, 2f);

        Transform playPoint = chuko != null && chuko.playPoint != null ? chuko.playPoint : Deep(zone, "PlayPoint");
        if (playPoint != null) playPoint.position = Z(0f, 0f, 0f);

        if (chuko != null && chuko.targets != null)
            for (int i = 0; i < chuko.targets.Length; i++)
                if (chuko.targets[i] != null)
                    chuko.targets[i].transform.position = Z(-1.6f + 1.6f * i, 0.7f, 5.5f + (i == 1 ? 0.6f : 0f));

        Transform boy = chuko != null && chuko.childNPC != null ? chuko.childNPC : Deep(zone, "Child_NPC");
        if (boy != null) StandOnGround(boy, Z(-3f, 0f, -1.5f));

        Transform girl = Deep(zone, "Child_NPC (1)");
        if (girl != null) StandOnGround(girl, Z(3.2f, 0f, -0.8f));

        // Ещё один ребёнок — болельщик у площадки
        Transform boy2 = Deep(zone, "Child_NPC (2)");
        if (boy2 == null && boy != null)
        {
            GameObject copy = Object.Instantiate(boy.gameObject, boy.parent);
            copy.name = "Child_NPC (2)";
            foreach (Transform c in copy.transform.Cast<Transform>().ToList())
                Object.DestroyImmediate(c.gameObject);              // без старой модели
            foreach (MonoBehaviour mb in copy.GetComponents<MonoBehaviour>())
                Object.DestroyImmediate(mb);
            if (copy.TryGetComponent(out MeshRenderer mr)) mr.enabled = true;
            boy2 = copy.transform;
        }
        if (boy2 != null) StandOnGround(boy2, Z(4.6f, 0f, 3.6f));
    }

    // ------------------------------------------------------------------- Food

    private static void LayoutFood(Transform zone)
    {
        var cook = zone.GetComponentInChildren<FoodQuestManager>(true);
        if (cook == null) return;

        Transform station = cook.cookingStation != null ? cook.cookingStation : Deep(zone, "CookingStation");
        Vector3 target = zone.position + new Vector3(0f, 0f, 3f);

        // Кухню (навес, стол, казан, полки) двигаем целиком, сохраняя взаимное расположение
        if (station != null)
        {
            Vector3 delta = target - station.position;
            delta.y = 0f;
            var skip = new HashSet<string> { "FoodQuestManager", "Ingredients", "BoorsokDish", "KuurdakDish" };
            foreach (Transform child in zone.Cast<Transform>().ToList())
                if (!skip.Contains(child.name)) child.position += delta;
        }

        Vector3 S = station != null ? station.position : target;
        S.y = zone.position.y;

        // Стол нормальной высоты, казан на столе
        Transform table = Deep(zone, "Table");
        if (table != null)
        {
            table.position = new Vector3(table.position.x, 0.7f, table.position.z);
            table.localScale = new Vector3(3f, 0.2f, 1.4f);
        }
        float top = table != null ? table.position.y + table.lossyScale.y * 0.5f : 0.8f;
        Vector3 tablePos = table != null ? table.position : S + new Vector3(0f, 0f, 2f);

        Transform kazan = Deep(zone, "Kazan");
        if (kazan != null)
            kazan.position = new Vector3(tablePos.x, top + kazan.lossyScale.y, tablePos.z);

        // Блюда — на стол
        if (cook.boorsokDish != null)
        {
            GameObject dish = cook.boorsokDish;
            foreach (Renderer r in dish.GetComponents<Renderer>()) r.enabled = false;
            foreach (Collider c in dish.GetComponents<Collider>()) Object.DestroyImmediate(c);
            dish.transform.localScale = Vector3.one;
            dish.transform.rotation = Quaternion.identity;
            dish.transform.position = new Vector3(tablePos.x - 0.95f, top, tablePos.z);
            AttachModel(dish.transform, ItemsDir + "BoorsokPlate.obj", "BoorsokPlateModel", 1f);
        }
        if (cook.kuurdakDish != null)
        {
            Transform kd = cook.kuurdakDish.transform;
            kd.rotation = Quaternion.identity;
            FitSize(kd, 0.45f);
            kd.position = new Vector3(tablePos.x + 0.95f, top, tablePos.z);
            SnapBottom(kd, top);
        }

        // Эже — перед кухней, лицом к лугу
        if (cook.ezhe != null)
        {
            Vector3 e = S + new Vector3(0f, 0f, -2.6f);
            foreach (Transform t in zone.GetComponentsInChildren<Transform>(true).Where(x => x.name == "Ezhe"))
                t.position = new Vector3(e.x, t.position.y, e.z);
            SnapBottom(cook.ezhe, zone.position.y);
        }

        // Продукты полукругом перед кухней: слева для боорсока, справа для куурдака
        Vector3[] spots =
        {
            new Vector3(-4.2f, 0f, -1.2f), new Vector3(-3.2f, 0f, -3.6f), new Vector3(-1.3f, 0f, -5.0f),
            new Vector3(1.3f, 0f, -5.0f), new Vector3(3.2f, 0f, -3.6f), new Vector3(4.2f, 0f, -1.2f)
        };
        GameObject[] all = (cook.boorsokIngredients ?? new GameObject[0]).Concat(cook.kuurdakIngredients ?? new GameObject[0]).ToArray();
        for (int i = 0; i < all.Length && i < spots.Length; i++)
        {
            if (all[i] == null) continue;
            Transform t = all[i].transform;
            t.position = S + spots[i];
            SnapBottom(t, zone.position.y);
        }
        EditorUtility.SetDirty(cook);
    }

    // ---------------------------------------------------------------- placing

    private static Vector3 FindSpot(Transform zone, float half, Vector3 preferred, List<Bounds> obstacles, Scene scene)
    {
        Transform valley = Find(scene, "MountainValley");
        Vector3 best = preferred;
        float bestScore = float.MaxValue;

        for (float dx = -60f; dx <= 60f; dx += 3f)
            for (float dz = -60f; dz <= 60f; dz += 3f)
            {
                Vector3 c = new Vector3(preferred.x + dx, 0f, preferred.z + dz);
                float score = dx * dx + dz * dz;
                if (score >= bestScore) continue;
                if (!IsFree(c, half, obstacles)) continue;
                if (!IsFlat(c, half, valley)) continue;
                bestScore = score;
                best = c;
            }

        if (bestScore == float.MaxValue)
            Debug.LogWarning($"[ZoneArranger] Не нашёл идеального места для {zone.name}, ставлю в {preferred}");
        return best;
    }

    private static bool IsFree(Vector3 c, float half, List<Bounds> obstacles)
    {
        var area = new Bounds(c + Vector3.up * 2f, new Vector3(half * 2f + 6f, 10f, half * 2f + 6f));
        return obstacles.All(b => !b.Intersects(area));
    }

    private static bool IsFlat(Vector3 c, float half, Transform valley)
    {
        for (int ix = -2; ix <= 2; ix++)
            for (int iz = -2; iz <= 2; iz++)
            {
                Vector3 p = c + new Vector3(ix * half / 2f, 120f, iz * half / 2f);
                if (!Physics.Raycast(p, Vector3.down, out RaycastHit hit, 300f, ~0, QueryTriggerInteraction.Ignore))
                    return false;
                if (valley != null && !hit.collider.transform.IsChildOf(valley)) return false;
                if (Mathf.Abs(hit.point.y) > 0.25f) return false;
            }
        return true;
    }

    private static void Place(Transform zone, Vector3 position)
    {
        Vector3 delta = position - zone.position;
        delta.y = -zone.position.y;
        zone.position += delta;
    }

    private static void AddBounds(List<Bounds> list, Transform root, System.Func<Transform, bool> filter)
    {
        if (root == null) return;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || !filter(r.transform) || r.transform.GetComponentsInParent<Transform>().Any(p => p.name == "MountainValley"))
                continue;
            if (r.bounds.size.x > 80f || r.bounds.size.z > 80f) continue;   // огромные декорации-горы не считаем
            list.Add(r.bounds);
        }
    }

    private static void StandOnGround(Transform npc, Vector3 groundPoint)
    {
        npc.position = new Vector3(groundPoint.x, npc.position.y, groundPoint.z);
        SnapBottom(npc, groundPoint.y);
    }

    // Ставит объект так, чтобы низ его видимой части/коллайдера был на высоте y
    private static void SnapBottom(Transform t, float y)
    {
        Bounds? b = null;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
            b = b.HasValue ? Enc(b.Value, r.bounds) : r.bounds;
        if (!b.HasValue)
            foreach (Collider c in t.GetComponentsInChildren<Collider>(true))
                b = b.HasValue ? Enc(b.Value, c.bounds) : c.bounds;
        if (b.HasValue)
            t.position += Vector3.up * (y - b.Value.min.y);
    }

    private static Bounds Enc(Bounds a, Bounds b) { a.Encapsulate(b); return a; }

    private static void FitSize(Transform t, float width)
    {
        Renderer[] rs = t.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return;
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        float size = Mathf.Max(b.size.x, b.size.z);
        if (size > 0.001f) t.localScale *= width / size;
    }

    private static void AttachModel(Transform parent, string path, string name, float scale)
    {
        Transform old = parent.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) { Debug.LogWarning("[ZoneArranger] Нет модели " + path); return; }
        var m = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        m.name = name;
        m.transform.localPosition = Vector3.zero;
        m.transform.localRotation = Quaternion.identity;
        m.transform.localScale = Vector3.one * scale;
    }

    // Для эже: у неё уже была своя модель — прячем её и ставим новую на то же место
    private static void InstallOverExistingModel(GameObject npc, string path, string name)
    {
        Transform old = npc.transform.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        Renderer[] oldRenderers = npc.GetComponentsInChildren<Renderer>(true);
        float feet = npc.transform.position.y;
        if (oldRenderers.Length > 0)
        {
            Bounds b = oldRenderers[0].bounds;
            foreach (Renderer r in oldRenderers) b.Encapsulate(r.bounds);
            feet = b.min.y;
        }
        foreach (Renderer r in oldRenderers) r.enabled = false;

        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) { Debug.LogWarning("[ZoneArranger] Нет модели " + path); return; }
        var m = (GameObject)PrefabUtility.InstantiatePrefab(asset, npc.transform);
        m.name = name;
        m.transform.localRotation = Quaternion.identity;
        Vector3 s = npc.transform.lossyScale;
        m.transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, s.x), 1f / Mathf.Max(0.0001f, s.y), 1f / Mathf.Max(0.0001f, s.z));
        m.transform.position = new Vector3(npc.transform.position.x, Mathf.Max(0f, feet), npc.transform.position.z);

        if (!npc.TryGetComponent(out NPCLookAtPlayer look)) look = npc.AddComponent<NPCLookAtPlayer>();
        look.model = m.transform;
        EditorUtility.SetDirty(npc);
    }

    private static Transform Find(Scene scene, string name) =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == name);

    private static Transform Deep(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
}
