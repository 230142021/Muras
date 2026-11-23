using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Базар в селе + новый герой: ставит прилавок торговца Асана у дороги (там тратится XP),
/// пересобирает модель героя (HeroModelBuilder) и вешает на игрока одежду, умения и спутников.
/// Запуск: Tools > Diploma > Install Bazaar + New Hero (XP shop). Можно запускать повторно.
/// </summary>
public static class BazaarInstaller
{
    private const string ScenePath = "Assets/Game/Scenes/Village.unity";
    private const string Dir = "Assets/Game/Models/Bazaar/";
    private const string Farm = "Assets/Game/Models/Farm/";
    private const string Sfx = "Assets/Game/Audio/SFX/";
    private const string PortraitSrc = "Assets/Game/UI/Sprites/ui_portrait_asan.png";
    private const string PortraitRes = "Assets/Game/Resources/MURAS/ui_portrait_asan.png";

    private static Transform valley;

    [MenuItem("Tools/Diploma/Install Bazaar + New Hero (XP shop)")]
    public static void Install()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previous = SceneManager.GetActiveScene().path;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        PreparePortrait();

        GameObject player = Roots(scene).SelectMany(r => r.GetComponentsInChildren<PlayerMovement>(true)).Select(p => p.gameObject).FirstOrDefault();
        if (player == null) { Debug.LogError("[Bazaar] В сцене Village нет игрока."); return; }
        Animator anim = player.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a.isHuman && a.gameObject.activeInHierarchy)
                        ?? player.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a.isHuman);

        // ---- 1. Новый герой
        if (anim != null) HeroModelBuilder.Install(scene, player, anim);
        else Debug.LogWarning("[Bazaar] У игрока нет гуманоидного Animator — модель героя не пересобрана.");

        var outfit = player.GetComponent<HeroOutfit>() ?? player.AddComponent<HeroOutfit>();
        outfit.modelRoot = anim != null ? anim.transform : player.transform;
        if (player.GetComponent<PlayerPerks>() == null) player.AddComponent<PlayerPerks>();
        var comp = player.GetComponent<CompanionManager>() ?? player.AddComponent<CompanionManager>();
        comp.dogModel = AssetDatabase.LoadAssetAtPath<GameObject>(Farm + "Animal_Dog.obj");
        comp.horseModel = AssetDatabase.LoadAssetAtPath<GameObject>(Farm + "Animal_Horse.obj");
        comp.dogCalls = Clips("dog_1");
        comp.horseCalls = Clips("horse_1", "horse_snort");
        EditorUtility.SetDirty(player);

        // ---- 2. Базар
        foreach (Transform t in Roots(scene).SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Where(t => t.name == "Bazaar").ToList())
            if (t != null) Object.DestroyImmediate(t.gameObject);

        valley = Roots(scene).SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "MountainValley");
        Physics.SyncTransforms();
        string where = PlaceBazaar(scene, player.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Bazaar] Готово. " + where + " Подойди к торговцу и нажми E, C — гардероб.");

        if (!string.IsNullOrEmpty(previous) && previous != ScenePath) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
    }

    // ================================================================== проверка в Play Mode (удобно для защиты)

    [MenuItem("Tools/Diploma/Play Mode: +500 XP (test)")]
    private static void GiveXP()
    {
        if (!Application.isPlaying || GameManager.Instance == null) { Debug.LogWarning("Запусти игру (Play), потом нажми этот пункт."); return; }
        GameManager.Instance.AddXP(500);
    }

    [MenuItem("Tools/Diploma/Play Mode: Open Bazaar (test)")]
    private static void OpenShop()
    {
        if (!Application.isPlaying || VillageShop.Instance == null) { Debug.LogWarning("Запусти игру (Play), потом нажми этот пункт."); return; }
        VillageShop.Instance.Open(true);
    }

    [MenuItem("Tools/Diploma/Play Mode: Close Bazaar (test)")]
    private static void CloseShop() => VillageShop.CloseIfOpen();

    [MenuItem("Tools/Diploma/Play Mode: Buy Item (test)/Chapan")] private static void BuyChapan() => TestBuy("chapan");
    [MenuItem("Tools/Diploma/Play Mode: Buy Item (test)/Batyr")] private static void BuyBatyr() => TestBuy("batyr");
    [MenuItem("Tools/Diploma/Play Mode: Buy Item (test)/Shepherd")] private static void BuyShepherd() => TestBuy("shepherd");
    [MenuItem("Tools/Diploma/Play Mode: Buy Item (test)/Tebetey")] private static void BuyTebetey() => TestBuy("tebetey");
    [MenuItem("Tools/Diploma/Play Mode: Buy Item (test)/Cap")] private static void BuyCap() => TestBuy("cap");
    [MenuItem("Tools/Diploma/Play Mode: Buy Item (test)/Dog")] private static void BuyDog() => TestBuy("dog");
    [MenuItem("Tools/Diploma/Play Mode: Buy Item (test)/Horse")] private static void BuyHorse() => TestBuy("horse");

    private static void TestBuy(string id)
    {
        if (!Application.isPlaying || GameManager.Instance == null) { Debug.LogWarning("Запусти игру (Play), потом нажми этот пункт."); return; }
        if (ShopCatalog.Balance < ShopCatalog.Get(id).price) GameManager.Instance.AddXP(ShopCatalog.Get(id).price);
        if (!ShopCatalog.Owns(id)) ShopCatalog.Buy(id); else ShopCatalog.Use(id);
        Debug.Log("[Bazaar test] " + id + " — куплено/надето. Баланс: " + ShopCatalog.Balance + " XP");
    }

    // ================================================================== прилавок

    private static string PlaceBazaar(Scene scene, Transform player)
    {
        GameObject stallAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "Bazaar_Stall.obj");
        GameObject traderAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "Villager_Asan.obj");
        if (stallAsset == null) { Debug.LogError("[Bazaar] Нет модели " + Dir + "Bazaar_Stall.obj"); return ""; }

        // Ищем ровное свободное место у дороги: восточная сторона смотрит на запад (yaw -90), западная — на восток
        var candidates = new List<(Vector3 p, float yaw)>();
        foreach (float z in new[] { 0f, 3f, -3f, 6f, -6f, 9f, -9f, 24f, -22f, 27f })
        {
            foreach (float x in new[] { 4.5f, 5.5f, 3.5f, 6.5f }) candidates.Add((new Vector3(x, 0, z), -90f));
            foreach (float x in new[] { -13.5f, -14.5f, -12.5f }) candidates.Add((new Vector3(x, 0, z), 90f));
        }

        Vector3 pos = Vector3.zero; float yaw = -90f; bool found = false;
        foreach (var c in candidates)
            if (Free(c.p, c.yaw, out float gy)) { pos = new Vector3(c.p.x, gy, c.p.z); yaw = c.yaw; found = true; break; }
        if (!found)
        {
            Vector3 p = player.position + player.right * 4f;
            GroundY(p, out float gy);
            pos = new Vector3(p.x, gy, p.z); yaw = player.eulerAngles.y - 90f;
            Debug.LogWarning("[Bazaar] Свободное место не найдено — прилавок поставлен рядом с игроком, передвинь объект Bazaar вручную.");
        }

        var root = new GameObject("Bazaar");
        Transform env = Roots(scene).SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "Environment");
        if (env != null) root.transform.SetParent(env, true);
        root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        var stall = (GameObject)PrefabUtility.InstantiatePrefab(stallAsset, root.transform);
        stall.transform.localPosition = Vector3.zero; stall.transform.localRotation = Quaternion.identity;
        foreach (MeshFilter mf in stall.GetComponentsInChildren<MeshFilter>())
            if (mf.sharedMesh != null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;

        // Надпись на вывеске
        var sign = new GameObject("SignText", typeof(RectTransform));
        sign.transform.SetParent(root.transform, false);
        sign.transform.localPosition = new Vector3(0f, 2.78f, -0.88f + 0.05f);
        sign.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var tmp = sign.AddComponent<TextMeshPro>();
        tmp.text = "БАЗАР"; tmp.alignment = TextAlignmentOptions.Center; tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(0.30f, 0.18f, 0.08f);
        tmp.enableAutoSizing = true; tmp.fontSizeMin = 0.5f; tmp.fontSizeMax = 6f;
        ((RectTransform)sign.transform).sizeDelta = new Vector2(1.05f, 0.24f);

        // Торговец за прилавком
        var npc = new GameObject("NPC_Asan");
        npc.transform.SetParent(root.transform, false);
        npc.transform.localPosition = new Vector3(0.35f, 0f, -0.52f);
        npc.transform.localRotation = Quaternion.identity;
        var cap = npc.AddComponent<CapsuleCollider>(); cap.center = new Vector3(0f, 0.9f, 0f); cap.height = 1.8f; cap.radius = 0.25f;
        if (traderAsset != null)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(traderAsset, npc.transform);
            model.name = "Villager_Asan";
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            var look = npc.AddComponent<NPCLookAtPlayer>();
            look.model = model.transform;
        }

        var front = new GameObject("CounterFront").transform;
        front.SetParent(root.transform, false);
        front.localPosition = new Vector3(0f, 0f, 1.4f);
        var trader = npc.AddComponent<ShopTrader>();
        trader.counterFront = front;
        trader.talkDistance = 2.6f;

        // Тёплый фонарь над прилавком — виден вечером
        var lampGo = new GameObject("Lantern");
        lampGo.transform.SetParent(root.transform, false);
        lampGo.transform.localPosition = new Vector3(0f, 2.0f, 0.3f);
        var lamp = lampGo.AddComponent<Light>();
        lamp.type = LightType.Point; lamp.color = new Color(1f, 0.78f, 0.45f); lamp.range = 6f; lamp.intensity = 1.2f;
        lamp.shadows = LightShadows.None;

        return $"Базар поставлен в ({pos.x:0.0}, {pos.z:0.0}){(found ? "" : " [место не найдено]")}.";
    }

    private static bool Free(Vector3 p, float yaw, out float gy)
    {
        gy = 0f;
        if (!GroundY(p, out gy)) return false;
        Quaternion r = Quaternion.Euler(0f, yaw, 0f);
        // ровно ли
        foreach (Vector3 c in new[] { new Vector3(-1.9f, 0, -1.1f), new Vector3(1.9f, 0, -1.1f), new Vector3(-1.9f, 0, 1.4f), new Vector3(1.9f, 0, 1.4f) })
        {
            Vector3 q = p + r * c;
            if (!GroundY(q, out float y) || Mathf.Abs(y - gy) > 0.35f) return false;
        }
        Vector3 center = new Vector3(p.x, gy + 1.3f, p.z) + r * new Vector3(0f, 0f, 0.15f);
        foreach (Collider col in Physics.OverlapBox(center, new Vector3(2.1f, 1.1f, 1.5f), r, ~0, QueryTriggerInteraction.Ignore))
        {
            if (IsGround(col)) continue;
            return false;   // дом, дерево, дорога, забор…
        }
        // перед прилавком тоже должно быть место для игрока
        foreach (Collider col in Physics.OverlapBox(center + r * new Vector3(0f, 0f, 1.6f), new Vector3(1.2f, 0.8f, 0.6f), r, ~0, QueryTriggerInteraction.Ignore))
            if (!IsGround(col) && !IsPath(col)) return false;
        return true;
    }

    // ================================================================== helpers

    private static void PreparePortrait()
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(PortraitSrc) == null) return;
        Directory.CreateDirectory(Path.GetDirectoryName(PortraitRes));
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(PortraitRes) == null) AssetDatabase.CopyAsset(PortraitSrc, PortraitRes);
        foreach (string path in new[] { PortraitSrc, PortraitRes })
            if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.textureType != TextureImporterType.Sprite)
            {
                ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.alphaIsTransparency = true;
                ti.SaveAndReimport();
            }
    }

    private static AudioClip[] Clips(params string[] names) =>
        names.Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + n + ".wav")).Where(c => c != null).ToArray();

    private static IEnumerable<GameObject> Roots(Scene s) => s.GetRootGameObjects();

    private static bool GroundY(Vector3 p, out float y)
    {
        y = 0f; bool ok = false;
        foreach (RaycastHit h in Physics.RaycastAll(new Vector3(p.x, p.y + 80f, p.z), Vector3.down, 250f, ~0, QueryTriggerInteraction.Ignore))
            if (IsGround(h.collider) && (!ok || h.point.y > y)) { y = h.point.y; ok = true; }
        return ok;
    }

    private static bool IsGround(Collider c) =>
        (valley != null ? c.transform.IsChildOf(valley) : c.name.Contains("Ground")) || c.name.Contains("PP_Meadow_0") || c.name.Contains("Lake_Ground");
    private static bool IsPath(Collider c) => c.name.Contains("Road") || c.name.Contains("Meadow_Path") || c.name.Contains("Floor_Tile");
}
