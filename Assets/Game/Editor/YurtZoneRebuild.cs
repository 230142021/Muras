using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Перестраивает зону юрты в Village:
/// • юрта в полный рост игрока (радиус 4 м, дверь 1,7 м) — можно зайти внутрь;
/// • стройплощадка и детали стоят в стороне от тропинки;
/// • четвёртая деталь — кийиз (войлочное покрытие с шырдаком внутри);
/// • кнопка «Подсказка» (H) в интерфейсе.
/// Запуск: Tools > Diploma > Rebuild Yurt Zone (big yurt + kiyiz + hint button).
/// </summary>
public static class YurtZoneRebuild
{
    private const string ScenePath = "Assets/Game/Scenes/Village.unity";
    private const string ModelDir = "Assets/Game/Models/Yurt/";

    // Позиции относительно центра Zone_Yurt (тропинка проходит примерно по x = 0)
    private static readonly Vector3 BuildLocal = new Vector3(12f, 0f, 4f);
    private static readonly Vector3 AksakalLocal = new Vector3(6.5f, 0f, -1.5f);
    private static readonly Vector3 KeregeLocal = new Vector3(19f, 0f, -5f);
    private static readonly Vector3 TundukLocal = new Vector3(22f, 0f, 9f);
    private static readonly Vector3 UukLocal = new Vector3(6f, 0f, 13f);
    private static readonly Vector3 KiyizLocal = new Vector3(16f, 0f, 16f);
    private static readonly Vector3 DecorYurtLocal = new Vector3(-14f, 0f, -10f);

    [MenuItem("Tools/Diploma/Rebuild Yurt Zone (big yurt + kiyiz + hint button)")]
    public static void Rebuild()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string previous = SceneManager.GetActiveScene().path;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Transform zone = Find(scene, "Zone_Yurt");
        if (zone == null)
        {
            Debug.LogError("[YurtZoneRebuild] Нет Zone_Yurt в Village");
            return;
        }

        var quest = zone.GetComponentInChildren<YurtQuestManager>(true);
        if (quest == null)
        {
            Debug.LogError("[YurtZoneRebuild] Нет YurtQuestManager в Zone_Yurt");
            return;
        }

        Transform construction = Deep(zone, "YurtConstruction");
        Transform items = Deep(zone, "YurtItems");
        Vector3 Z(Vector3 local) => zone.position + local;

        // --- стройплощадка и готовые части (все в одной точке)
        Transform buildPoint = quest.buildPoint != null ? quest.buildPoint : Deep(zone, "BuildPoint");
        buildPoint.position = Z(BuildLocal);
        quest.buildPoint = buildPoint;

        SetPart(quest.builtKerege, "Kerege_Built", Z(BuildLocal), Collide.Mesh);
        SetPart(quest.builtTunduk, "Tunduk_Built", Z(BuildLocal), Collide.None);
        SetPart(quest.builtUuk, "Uuk_Built", Z(BuildLocal), Collide.None);

        quest.builtKiyiz = Spawn(construction != null ? construction : zone, "BuiltKiyiz", "Kiyiz_Built", Z(BuildLocal), false);

        // --- детали для подбора — в стороне от тропинки
        SetPart(quest.pickupKerege, "Kerege_Pickup", Z(KeregeLocal), Collide.Box, 25f);
        SetPart(quest.pickupTunduk, "Tunduk_Pickup", Z(TundukLocal), Collide.Box);
        SetPart(quest.pickupUuk, "Uuk_Pickup", Z(UukLocal), Collide.Box, 35f);
        quest.pickupKiyiz = Spawn(items != null ? items : zone, "PickupKiyiz", "Kiyiz_Pickup", Z(KiyizLocal), true);

        // --- аксакал рядом с площадкой, декоративная юрта — по другую сторону тропинки
        if (quest.aksakal != null)
        {
            float y = quest.aksakal.position.y;
            Vector3 p = Z(AksakalLocal);
            quest.aksakal.position = new Vector3(p.x, y, p.z);
        }
        Transform decor = zone.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "yurt" && t.parent == zone);
        if (decor != null)
        {
            Vector3 p = Z(DecorYurtLocal);
            decor.position = new Vector3(p.x, decor.position.y, p.z);
        }

        quest.interactionDistance = 3.5f;
        quest.buildDistance = 6.5f;
        EditorUtility.SetDirty(quest);

        // --- кнопка «Подсказка»
        CreateHintButton(Find(scene, "GameCanvas"));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeTransform = buildPoint;
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log("[YurtZoneRebuild] Готово: большая юрта, кийиз, новые позиции, кнопка «Подсказка». Village сохранена.");

        if (!string.IsNullOrEmpty(previous) && previous != ScenePath)
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
    }

    private enum Collide { None, Box, Mesh }

    private static void SetPart(GameObject go, string model, Vector3 position, Collide collide, float yaw = 0f)
    {
        if (go == null)
        {
            Debug.LogWarning("[YurtZoneRebuild] Не назначена деталь для " + model);
            return;
        }

        Mesh mesh = AssetDatabase.LoadAllAssetsAtPath(ModelDir + model + ".obj").OfType<Mesh>().FirstOrDefault();
        if (mesh != null && go.TryGetComponent(out MeshFilter mf))
            mf.sharedMesh = mesh;

        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one;

        foreach (Collider c in go.GetComponents<Collider>())
            Object.DestroyImmediate(c);
        if (collide == Collide.Box) go.AddComponent<BoxCollider>();
        if (collide == Collide.Mesh && mesh != null) go.AddComponent<MeshCollider>().sharedMesh = mesh;

        EditorUtility.SetDirty(go);
    }

    // Модели с несколькими материалами (кийиз) ставим целиком, с материалами из .mtl
    private static GameObject Spawn(Transform parent, string name, string model, Vector3 position, bool boxCollider)
    {
        Transform old = parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + model + ".obj");
        if (asset == null)
        {
            Debug.LogError("[YurtZoneRebuild] Нет модели " + ModelDir + model + ".obj");
            return null;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.name = name;
        go.transform.position = position;
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        if (boxCollider)
            foreach (MeshFilter f in go.GetComponentsInChildren<MeshFilter>(true))
                if (!f.TryGetComponent(out BoxCollider _)) f.gameObject.AddComponent<BoxCollider>();

        return go;
    }

    private static void CreateHintButton(Transform canvas)
    {
        if (canvas == null) return;
        Transform left = canvas.Find("LeftHud");
        Transform parent = left != null ? left : canvas;

        Transform existing = parent.Find("HintButton");
        GameObject go = existing != null ? existing.gameObject : new GameObject("HintButton", typeof(RectTransform));
        go.layer = canvas.gameObject.layer;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(230f, 48f);
        if (left == null)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -220f);
        }

        if (!go.TryGetComponent(out LayoutElement le)) le = go.AddComponent<LayoutElement>();
        le.preferredWidth = 230f;
        le.preferredHeight = 48f;

        if (!go.TryGetComponent(out Image img)) img = go.AddComponent<Image>();
        img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/UI/Sprites/ui_card.png");
        img.type = Image.Type.Sliced;
        img.color = Color.white;

        if (!go.TryGetComponent(out Button button)) button = go.AddComponent<Button>();
        button.targetGraphic = img;
        ColorBlock cb = button.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, 0.92f, 0.7f);
        cb.pressedColor = new Color(0.85f, 0.7f, 0.4f);
        cb.selectedColor = Color.white;
        button.colors = cb;
        var nav = new Navigation { mode = Navigation.Mode.None };
        button.navigation = nav;   // чтобы пробел (прыжок) не нажимал кнопку повторно

        Transform labelT = go.transform.Find("Label");
        TextMeshProUGUI label = labelT != null ? labelT.GetComponent<TextMeshProUGUI>() : null;
        if (label == null)
        {
            var lgo = new GameObject("Label", typeof(RectTransform));
            lgo.layer = go.layer;
            lgo.transform.SetParent(go.transform, false);
            label = lgo.AddComponent<TextMeshProUGUI>();
        }
        var lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(18f, 4f); lrt.offsetMax = new Vector2(-14f, -4f);
        label.fontSize = 22f;
        label.color = new Color(0.96f, 0.93f, 0.85f);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;
        label.text = "Подсказка  <color=#E8B04A><b>[H]</b></color>";

        if (!go.TryGetComponent(out HintButton hint)) hint = go.AddComponent<HintButton>();
        hint.label = label;
        EditorUtility.SetDirty(hint);

        // Порядок в левой колонке: задания, яблоки, подсказка, «в руках»
        if (left != null)
        {
            Transform chip = left.Find("InventoryChip");
            go.transform.SetSiblingIndex(chip != null ? chip.GetSiblingIndex() + 1 : left.childCount - 1);
        }
    }

    private static Transform Find(Scene scene, string name) =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == name);

    private static Transform Deep(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
}
