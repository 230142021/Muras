using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Этап 12.2: заменяет кубы и цилиндры в YurtArea на модели частей юрты
/// (кереге, түндүк, уук) и собирает каркас на строительной площадке.
/// Запуск: Tools > Diploma > Install Yurt Models.
/// </summary>
public static class YurtModelsInstaller
{
    private const string ScenePath = "Assets/Game/Scenes/YurtArea.unity";
    private const string ModelFolder = "Assets/Game/Models/Yurt/";

    [MenuItem("Tools/Diploma/Install Yurt Models")]
    public static void Install()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string previousScene = SceneManager.GetActiveScene().path;
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Dictionary<string, Transform> all = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .GroupBy(t => t.name)
            .ToDictionary(g => g.Key, g => g.First());

        Transform buildPoint = Find(all, "BuildPoint");
        Transform ground = Find(all, "Ground");
        if (buildPoint == null || ground == null)
            return;

        Renderer groundRenderer = ground.GetComponent<Renderer>();
        float groundY = groundRenderer != null ? groundRenderer.bounds.max.y : buildPoint.position.y;

        int done = 0;

        // Каркас юрты собирается в одной точке — на строительной площадке.
        Vector3 center = new Vector3(buildPoint.position.x, groundY, buildPoint.position.z);
        done += Apply(all, "BuiltKerege", "Kerege_Built", center, ColliderKind.Mesh);
        done += Apply(all, "BuiltUuk", "Uuk_Built", center, ColliderKind.None);
        done += Apply(all, "BuiltTunduk", "Tunduk_Built", center, ColliderKind.None);

        // Детали для подбора остаются на своих местах, просто ставим их на землю.
        done += Apply(all, "PickupKerege", "Kerege_Pickup", null, ColliderKind.Box, groundY);
        done += Apply(all, "PickupTunduk", "Tunduk_Pickup", null, ColliderKind.Box, groundY);
        done += Apply(all, "PickupUuk", "Uuk_Pickup", null, ColliderKind.Box, groundY);

        // Юрта шире прежних кубов: даём дотянуться до центра площадки снаружи стены.
        var quest = Object.FindAnyObjectByType<YurtQuestManager>(FindObjectsInactive.Include);
        if (quest != null && quest.interactionDistance < 4.5f)
        {
            Undo.RecordObject(quest, "Yurt interaction distance");
            quest.interactionDistance = 4.5f;
            EditorUtility.SetDirty(quest);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[YurtModelsInstaller] Готово: заменено объектов {done}/6, сцена YurtArea сохранена.");

        if (!string.IsNullOrEmpty(previousScene) && previousScene != ScenePath)
            EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
    }

    private enum ColliderKind { None, Box, Mesh }

    private static Transform Find(Dictionary<string, Transform> all, string name)
    {
        if (all.TryGetValue(name, out Transform t))
            return t;
        Debug.LogWarning($"[YurtModelsInstaller] В YurtArea нет объекта {name}");
        return null;
    }

    private static int Apply(Dictionary<string, Transform> all, string objectName, string modelName,
        Vector3? worldPosition, ColliderKind collider, float groundY = float.NaN)
    {
        Transform t = Find(all, objectName);
        if (t == null)
            return 0;

        Mesh mesh = AssetDatabase.LoadAllAssetsAtPath(ModelFolder + modelName + ".obj")
            .OfType<Mesh>()
            .FirstOrDefault();
        if (mesh == null)
        {
            Debug.LogWarning($"[YurtModelsInstaller] Не найдена модель {ModelFolder}{modelName}.obj");
            return 0;
        }

        MeshFilter filter = t.GetComponent<MeshFilter>();
        if (filter == null)
            filter = t.gameObject.AddComponent<MeshFilter>();
        if (t.GetComponent<MeshRenderer>() == null)
            t.gameObject.AddComponent<MeshRenderer>();

        filter.sharedMesh = mesh;          // материал (KeregeMat / TundukMat / UukMat) остаётся прежним
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;

        if (worldPosition.HasValue)
            t.position = worldPosition.Value;
        else if (!float.IsNaN(groundY))
            t.position = new Vector3(t.position.x, groundY, t.position.z);

        foreach (Collider c in t.GetComponents<Collider>())
            Object.DestroyImmediate(c);

        if (collider == ColliderKind.Box)
            t.gameObject.AddComponent<BoxCollider>();
        else if (collider == ColliderKind.Mesh)
            t.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;

        EditorUtility.SetDirty(t.gameObject);
        return 1;
    }
}
