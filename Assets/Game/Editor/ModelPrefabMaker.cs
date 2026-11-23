using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Делает из каждой модели игры отдельный префаб, который можно просто перетащить в сцену:
/// Assets/Game/Prefabs/MURAS/{NPC, Yurt, Items, Food, Environment}/имя.prefab
/// У NPC — капсула-коллайдер и поворот к игроку, у предметов — коллайдер по форме.
/// Запуск: Tools > Diploma > Make Prefabs From All Models.
/// </summary>
public static class ModelPrefabMaker
{
    private const string Root = "Assets/Game/Prefabs/MURAS/";

    private static readonly (string folder, string category)[] Sources =
    {
        ("Assets/Game/Models/NPC", "NPC"),
        ("Assets/Game/Models/Yurt", "Yurt"),
        ("Assets/Game/Models/Items", "Items"),
        ("Assets/Game/Models/FoodIngredients", "Food"),
        ("Assets/Game/Models/Environment", "Environment"),
    };

    [MenuItem("Tools/Diploma/Make Prefabs From All Models")]
    public static void Make()
    {
        int made = 0;
        foreach (var (folder, category) in Sources)
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            string outDir = Root + category;
            Directory.CreateDirectory(outDir);

            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetDirectoryName(path)?.Replace('\\', '/') != folder) continue;   // только сама папка
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;

                string name = Path.GetFileNameWithoutExtension(path);
                string prefabPath = $"{outDir}/{name}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) continue; // не перезаписываем твои правки

                var root = new GameObject(name);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                inst.name = "Model";
                inst.transform.localPosition = Vector3.zero;

                Bounds b = Measure(root);
                if (category == "NPC")
                {
                    var cap = root.AddComponent<CapsuleCollider>();
                    cap.height = b.size.y;
                    cap.radius = Mathf.Max(0.15f, Mathf.Min(b.extents.x, b.extents.z) * 0.6f);
                    cap.center = new Vector3(0f, b.size.y * 0.5f, 0f);
                    root.AddComponent<NPCLookAtPlayer>().model = inst.transform;
                }
                else if (category == "Environment" || name.Contains("shelter") || name.Contains("table") ||
                         name.Contains("shelf") || name.StartsWith("Kerege"))
                {
                    foreach (MeshFilter mf in inst.GetComponentsInChildren<MeshFilter>())
                        mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                }
                else
                {
                    var box = root.AddComponent<BoxCollider>();
                    box.center = b.center - root.transform.position;
                    box.size = b.size;
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Object.DestroyImmediate(root);
                made++;
            }
        }

        AssetDatabase.SaveAssets();
        EditorUtility.RevealInFinder(Root);
        Debug.Log($"[ModelPrefabMaker] Создано префабов: {made}. Папка: {Root} (уже существующие не трогались).");
    }

    private static Bounds Measure(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(Vector3.up * 0.5f, Vector3.one);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs.Skip(1)) b.Encapsulate(r.bounds);
        return b;
    }
}
