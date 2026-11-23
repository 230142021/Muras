using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Пересобирает игровую площадку (Zone_Games): круг из верёвки с чүкө, черта-бревно для броска,
/// сака, скамейки, флажки, вывеска «ЧҮКӨ АТУУ», чайный столик, корзина с чүкө, забор.
/// Дети расставляются вокруг круга. Создаёт полоску силы броска и линию прицела, подключает квест.
/// Запуск: Tools > Diploma > Rebuild Chuko Arena (Games Zone).
/// ВНИМАНИЕ: повторный запуск пересоздаёт площадку — ручные правки внутри ChukoArena пропадут.
/// </summary>
public static class ChukoArenaBuilder
{
    private const string ScenePath = "Assets/Game/Scenes/Village.unity";
    private const string G = "Assets/Game/Models/Games/";
    private const string F = "Assets/Game/Models/FoodIngredients/";
    private const string PhysDir = "Assets/Game/Physics/";

    private const float PieceScale = 1.5f;   // чүкө крупнее настоящих, чтобы было видно
    private static readonly Vector3 RingLocal = new Vector3(0f, 0f, 6f);

    private static Transform arena;
    private static Transform valley;

    [MenuItem("Tools/Diploma/Rebuild Chuko Arena (Games Zone)")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform zone = Find(scene, "Zone_Games");
        if (zone == null) { Debug.LogError("[Chuko] Нет Zone_Games"); return; }
        var game = zone.GetComponentInChildren<ChukoGameManager>(true);
        if (game == null) { Debug.LogError("[Chuko] Нет ChukoGameManager"); return; }
        valley = Find(scene, "MountainValley");
        Physics.SyncTransforms();
        if (!AssetDatabase.IsValidFolder("Assets/Game/Physics"))
            AssetDatabase.CreateFolder("Assets/Game", "Physics");

        // 1. Убираем старую площадку
        Transform oldPlay = zone.Cast<Transform>().FirstOrDefault(t => t.name == "ChukoPlayground");
        if (oldPlay != null) Object.DestroyImmediate(oldPlay.gameObject);
        Transform oldArena = zone.Cast<Transform>().FirstOrDefault(t => t.name == "ChukoArena");
        if (oldArena != null) Object.DestroyImmediate(oldArena.gameObject);

        // 2. Корень площадки: локальная -Z смотрит в сторону села (к Айбеку)
        var root = new GameObject("ChukoArena");
        root.transform.SetParent(zone, false);
        root.transform.localPosition = Vector3.zero;
        Transform aibek = Find(scene, "NPC_Aibek");
        if (aibek != null)
        {
            Vector3 d = zone.position - aibek.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f)
                root.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        }
        arena = root.transform;

        // 3. Круг, черта, точка броска
        Put("ChukoRing", G + "ChukoRing.obj", RingLocal.x, RingLocal.z, 0f);
        Transform ringCenter = Empty("RingCenter", RingLocal.x, RingLocal.z);
        Put("ThrowLine", G + "ThrowLog.obj", 0f, 0.4f, 0f);
        Transform playPoint = Empty("PlayPoint", 0f, -0.8f);

        // 4. Чүкө в ряд внутри круга
        PhysicsMaterial boneMat = GetPhysMat("ChukoBone", 0.55f, 0.15f);
        var pieces = new List<ChukoTarget>();
        for (int i = 0; i < 5; i++)
        {
            float x = (i - 2) * 0.42f;
            string model = i % 2 == 0 ? "Chuko.obj" : "Chuko_Red.obj";
            GameObject p = Put("Chuko_" + (i + 1), G + model, RingLocal.x + x, RingLocal.z, 90f + Random.Range(-8f, 8f),
                scale: PieceScale, isStatic: false, lift: 0.02f);
            var box = p.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.05f, 0f);
            box.size = new Vector3(0.22f, 0.11f, 0.12f);
            box.sharedMaterial = boneMat;
            var rb = p.AddComponent<Rigidbody>();
            rb.mass = 0.12f;
            rb.linearDamping = 0.25f;
            rb.angularDamping = 0.4f;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var t = p.AddComponent<ChukoTarget>();
            t.gameManager = game;
            pieces.Add(t);
        }

        // 5. Сака — образец (выключен), менеджер клонирует его на каждый бросок
        GameObject saka = Put("SakaTemplate", G + "Saka.obj", -0.6f, -1.1f, 0f, scale: PieceScale, isStatic: false);
        var sbox = saka.AddComponent<BoxCollider>();
        sbox.center = new Vector3(0f, 0.07f, 0f);
        sbox.size = new Vector3(0.28f, 0.15f, 0.14f);
        sbox.sharedMaterial = GetPhysMat("Saka", 0.45f, 0.2f);
        var srb = saka.AddComponent<Rigidbody>();
        srb.mass = 0.6f;
        srb.linearDamping = 0.1f;
        srb.angularDamping = 0.3f;
        srb.isKinematic = true;
        srb.interpolation = RigidbodyInterpolation.Interpolate;
        saka.SetActive(false);

        // 6. Декорации
        Put("Bench_Left", G + "GameBench.obj", -4.5f, 4f, 90f, collider: true);
        Put("Bench_Right", G + "GameBench.obj", 4.5f, 4.5f, -90f, collider: true);
        Put("Pennant_1", G + "PennantPole.obj", -3f, 9.5f, 0f, collider: true);
        Put("Pennant_2", G + "PennantPole.obj", 3f, 9.5f, 180f, collider: true);
        Put("Pennant_3", G + "PennantPole.obj", -2.2f, -2.6f, 0f, collider: true);
        Put("Pennant_4", G + "PennantPole.obj", 2.2f, -2.6f, 180f, collider: true);

        GameObject sign = Put("GameSign", G + "GameSign.obj", -3.6f, -3.0f, 20f, collider: true);
        AddSignText(sign.transform);

        // Чайный уголок для зрителей
        Put("TeaTable", F + "low_table_set.obj", 5.6f, 0.2f, -30f, collider: true);
        Put("TeaMug1", F + "wooden_mug.obj", 5.35f, 0.05f, 0f, extraY: 0.42f);
        Put("TeaMug2", F + "wooden_mug.obj", 5.85f, 0.35f, 120f, extraY: 0.42f);
        Put("TeaBowl", F + "wooden_bowl.obj", 5.6f, 0.2f, 0f, extraY: 0.42f);

        // Корзина-миска с запасными чүкө
        Put("ChukoBowl", F + "wooden_bowl.obj", -3.4f, 0.8f, 0f, scale: 1.6f);
        for (int i = 0; i < 4; i++)
            Put("SpareChuko_" + (i + 1), G + (i % 2 == 0 ? "Chuko.obj" : "Chuko_Red.obj"),
                -3.4f + (i - 1.5f) * 0.09f, 0.8f + (i % 2) * 0.06f, i * 47f, extraY: 0.08f);

        // Невысокий забор за кругом
        string fence = FindPrefab("PP_Small_Fence") ?? FindPrefab("PP_Fence");
        if (fence != null)
            for (int i = 0; i < 4; i++)
                Put("Fence_" + (i + 1), fence, -4.5f + i * 3f, 11.2f, 0f, collider: true);

        // 7. Дети вокруг круга, лицом к игре
        PlaceKid(zone, "Child_NPC", -2.6f, -1.3f);
        PlaceKid(zone, "Child_NPC (1)", 3.0f, 3.0f);
        PlaceKid(zone, "Child_NPC (2)", 3.3f, 7.6f);

        // 8. UI: полоса силы и линия прицела
        Transform canvas = Find(scene, "GameCanvas");
        Image fill = null;
        GameObject powerPanel = null;
        if (canvas != null) powerPanel = BuildPowerBar(canvas, out fill);

        var lineGo = new GameObject("AimLine");
        lineGo.transform.SetParent(arena, false);
        var line = lineGo.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.widthMultiplier = 0.05f;
        line.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.3f));
        line.numCapVertices = 2;
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        string matPath = PhysDir + "AimLine.mat";
        var lineMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (lineMat == null)
        {
            lineMat = new Material(unlit);
            AssetDatabase.CreateAsset(lineMat, matPath);
        }
        lineMat.color = new Color(0.98f, 0.85f, 0.35f);
        EditorUtility.SetDirty(lineMat);
        line.sharedMaterial = lineMat;
        line.enabled = false;

        // 9. Подключаем квест
        game.targets = pieces.ToArray();
        game.ringCenter = ringCenter;
        game.ringRadius = 1.6f;
        game.playPoint = playPoint;
        game.sakaTemplate = saka;
        game.powerPanel = powerPanel;
        game.powerFill = fill;
        game.aimLine = line;
        game.throwsPerRound = 6;
        game.needKnockOut = 3;
        game.throwZone = 2.5f;
        Transform boy = zone.Cast<Transform>().FirstOrDefault(t => t.name == "Child_NPC");
        if (boy != null) game.childNPC = boy;
        if (canvas != null)
        {
            Transform quiz = FindUnder(canvas, "QuizPanel_Games");
            if (quiz != null)
            {
                var buttons = new List<Button>();
                for (int i = 1; i <= 3; i++)
                {
                    Transform b = FindUnder(quiz, "QuizAnswer" + i);
                    if (b != null && b.TryGetComponent(out Button btn)) buttons.Add(btn);
                }
                if (buttons.Count > 0) game.answerButtons = buttons.ToArray();
            }
        }
        EditorUtility.SetDirty(game);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeTransform = arena;
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log("[Chuko] Площадка Чүкө атуу собрана: круг, 5 чүкө, сака, декорации, полоса силы. Village сохранена.");
    }

    // ------------------------------------------------------------------ UI

    private static GameObject BuildPowerBar(Transform canvas, out Image fill)
    {
        Transform old = FindUnder(canvas, "ThrowPower");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        Sprite card = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/UI/Sprites/ui_card.png");
        Sprite button = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/UI/Sprites/ui_button.png");

        var panel = new GameObject("ThrowPower", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas, false);
        var rt = (RectTransform)panel.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 150f);
        rt.sizeDelta = new Vector2(460f, 74f);
        var bg = panel.GetComponent<Image>();
        bg.sprite = card;
        bg.type = card != null ? Image.Type.Sliced : Image.Type.Simple;
        bg.color = new Color(0.10f, 0.08f, 0.06f, 0.88f);
        bg.raycastTarget = false;

        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(panel.transform, false);
        var lrt = (RectTransform)label.transform;
        lrt.anchorMin = new Vector2(0f, 0.55f); lrt.anchorMax = new Vector2(1f, 1f);
        lrt.offsetMin = new Vector2(18f, 0f); lrt.offsetMax = new Vector2(-18f, -4f);
        var tmp = label.GetComponent<TextMeshProUGUI>();
        tmp.text = "<b>КҮЧ</b>  ·  сила броска";
        tmp.fontSize = 19f;
        tmp.color = new Color(0.84f, 0.66f, 0.29f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        var track = new GameObject("Track", typeof(RectTransform), typeof(Image));
        track.transform.SetParent(panel.transform, false);
        var trt = (RectTransform)track.transform;
        trt.anchorMin = new Vector2(0f, 0f); trt.anchorMax = new Vector2(1f, 0.55f);
        trt.offsetMin = new Vector2(20f, 14f); trt.offsetMax = new Vector2(-20f, -2f);
        var ti = track.GetComponent<Image>();
        ti.sprite = button;
        ti.type = button != null ? Image.Type.Sliced : Image.Type.Simple;
        ti.color = new Color(0.25f, 0.21f, 0.17f, 1f);
        ti.raycastTarget = false;

        var f = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        f.transform.SetParent(track.transform, false);
        var frt = (RectTransform)f.transform;
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = new Vector2(3f, 3f); frt.offsetMax = new Vector2(-3f, -3f);
        fill = f.GetComponent<Image>();
        fill.sprite = null;            // Filled работает только с простым спрайтом
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 0f;
        fill.color = new Color(0.37f, 0.78f, 0.45f);
        fill.raycastTarget = false;

        panel.SetActive(false);
        return panel;
    }

    private static void AddSignText(Transform sign)
    {
        var go = new GameObject("SignText", typeof(TextMeshPro));
        go.transform.SetParent(sign, false);
        // доска 1.8 × 0.6 на высоте ~1.25, текст на стороне -Z (к селу)
        go.transform.localPosition = new Vector3(0f, 1.27f, -0.07f);
        go.transform.localRotation = Quaternion.identity;
        var tmp = go.GetComponent<TextMeshPro>();
        tmp.text = "ЧҮКӨ АТУУ";
        tmp.fontSize = 4.2f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.98f, 0.93f, 0.80f);
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = new Color32(70, 35, 20, 255);
        tmp.rectTransform.sizeDelta = new Vector2(1.7f, 0.5f);
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
    }

    // ------------------------------------------------------------------ helpers

    private static void PlaceKid(Transform zone, string name, float x, float z)
    {
        Transform kid = zone.Cast<Transform>().FirstOrDefault(t => t.name == name);
        if (kid == null) return;
        Vector3 p = arena.TransformPoint(new Vector3(x, 0f, z));
        float yOffset = kid.position.y - GroundY(kid.position);   // сохраняем, на какой высоте стоял
        kid.position = new Vector3(p.x, GroundY(p) + Mathf.Max(0f, yOffset), p.z);
        Vector3 look = arena.TransformPoint(RingLocal) - kid.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f) kid.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        EditorUtility.SetDirty(kid);
    }

    private static Transform Empty(string name, float x, float z)
    {
        var go = new GameObject(name);
        go.transform.SetParent(arena, false);
        Vector3 w = arena.TransformPoint(new Vector3(x, 0f, z));
        go.transform.position = new Vector3(w.x, GroundY(w), w.z);
        return go.transform;
    }

    private static GameObject Put(string name, string path, float x, float z, float yaw,
        bool collider = false, float scale = 1f, bool isStatic = true, float lift = 0f, float extraY = 0f)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            Debug.LogWarning("[Chuko] Нет модели " + path);
            return Empty(name, x, z).gameObject;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, arena);
        go.name = name;
        Vector3 w = arena.TransformPoint(new Vector3(x, 0f, z));
        go.transform.position = new Vector3(w.x, GroundY(w) + lift + extraY, w.z);
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one * scale;
        if (isStatic) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);

        if (collider)
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
                if (!mf.TryGetComponent(out Collider _))
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        return go;
    }

    private static float GroundY(Vector3 p)
    {
        var hits = Physics.RaycastAll(new Vector3(p.x, p.y + 50f, p.z), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MinValue;
        foreach (RaycastHit h in hits)
        {
            bool isGround = valley != null ? h.collider.transform.IsChildOf(valley) : h.collider.name.Contains("Ground");
            if (isGround && h.point.y > best) best = h.point.y;
        }
        return best > float.MinValue ? best : (arena != null ? arena.position.y : 0f);
    }

    private static PhysicsMaterial GetPhysMat(string name, float friction, float bounce)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Game/Physics"))
            AssetDatabase.CreateFolder("Assets/Game", "Physics");
        string path = PhysDir + name + ".asset";
        var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (mat == null)
        {
            mat = new PhysicsMaterial(name);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.dynamicFriction = friction;
        mat.staticFriction = friction + 0.1f;
        mat.bounciness = bounce;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static string FindPrefab(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Prefab"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(p) == name) return p;
        }
        return null;
    }

    private static Transform FindUnder(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

    private static Transform Find(Scene scene, string name) =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == name);
}
