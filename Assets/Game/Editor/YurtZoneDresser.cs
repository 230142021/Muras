using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Зона юрты в Village: модель аксакала вместо капсулы, золотая стрелка-подсказка
/// и полупрозрачная «тень» детали юрты на площадке, пока игрок её несёт.
/// Запуск: Tools > Diploma > Install Aksakal + Yurt Hints.
/// </summary>
public static class YurtZoneDresser
{
    private const string ScenePath = "Assets/Game/Scenes/Village.unity";
    private const string AksakalModel = "Assets/Game/Models/NPC/Aksakal.obj";
    private const string ArrowModel = "Assets/Game/Models/Items/QuestArrow.obj";
    private const string MatDir = "Assets/Game/Materials/";

    [MenuItem("Tools/Diploma/Install Aksakal + Yurt Hints")]
    public static void Install()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string previous = SceneManager.GetActiveScene().path;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        Transform zone = all.FirstOrDefault(t => t.name == "Zone_Yurt");
        if (zone == null)
        {
            Debug.LogError("[YurtZoneDresser] В Village нет Zone_Yurt — сначала Tools > Diploma > Merge Yurt Zone Into Village");
            return;
        }

        // 1. Аксакал
        Transform aksakal = zone.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Aksakal");
        if (aksakal != null)
            InstallNpcModel(aksakal.gameObject, AksakalModel, "AksakalModel");
        else
            Debug.LogWarning("[YurtZoneDresser] Не найден Aksakal в Zone_Yurt");

        // 2. Материалы подсказок
        Material arrowMat = GetOrCreateMaterial("QuestArrow.mat", new Color(1f, 0.78f, 0.25f, 1f), false);
        Material ghostMat = GetOrCreateMaterial("YurtGhost.mat", new Color(1f, 0.85f, 0.4f, 0.35f), true);

        // 3. Стрелка
        Transform oldArrow = zone.Find("QuestArrow");
        if (oldArrow != null) Object.DestroyImmediate(oldArrow.gameObject);

        GameObject arrowAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowModel);
        QuestArrow arrow = null;
        if (arrowAsset != null)
        {
            var arrowGo = (GameObject)PrefabUtility.InstantiatePrefab(arrowAsset, zone);
            arrowGo.name = "QuestArrow";
            arrowGo.transform.localScale = Vector3.one * 1.4f;
            foreach (Renderer r in arrowGo.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterials = Enumerable.Repeat(arrowMat, r.sharedMaterials.Length).ToArray();
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            arrow = arrowGo.AddComponent<QuestArrow>();
        }
        else
        {
            Debug.LogWarning("[YurtZoneDresser] Нет модели " + ArrowModel);
        }

        // 4. Подключаем к квесту
        var quest = zone.GetComponentInChildren<YurtQuestManager>(true);
        if (quest != null)
        {
            quest.arrow = arrow;
            quest.ghostMaterial = ghostMat;
            EditorUtility.SetDirty(quest);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[YurtZoneDresser] Аксакал установлен, стрелка и подсказки установки деталей добавлены. Village сохранена.");

        if (!string.IsNullOrEmpty(previous) && previous != ScenePath)
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
    }

    /// <summary>Ставит модель внутрь объекта-капсулы NPC, выключает отображение капсулы, добавляет поворот к игроку.</summary>
    public static void InstallNpcModel(GameObject npc, string modelPath, string childName)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (asset == null)
        {
            Debug.LogError("[YurtZoneDresser] Нет модели " + modelPath);
            return;
        }

        MeshRenderer capsule = npc.GetComponent<MeshRenderer>();
        float feetY = capsule != null ? capsule.bounds.min.y : npc.transform.position.y - 1f;

        Transform old = npc.transform.Find(childName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, npc.transform);
        model.name = childName;
        model.transform.localRotation = Quaternion.identity;
        Vector3 s = npc.transform.lossyScale;
        model.transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, s.x), 1f / Mathf.Max(0.0001f, s.y), 1f / Mathf.Max(0.0001f, s.z));
        model.transform.position = new Vector3(npc.transform.position.x, feetY, npc.transform.position.z);

        if (capsule != null) capsule.enabled = false;

        if (!npc.TryGetComponent(out NPCLookAtPlayer look))
            look = npc.AddComponent<NPCLookAtPlayer>();
        look.model = model.transform;
        EditorUtility.SetDirty(npc);
    }

    private static Material GetOrCreateMaterial(string file, Color color, bool transparent)
    {
        System.IO.Directory.CreateDirectory(MatDir);
        string path = MatDir + file;
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

        if (transparent)
        {
            mat.SetFloat("_Surface", 1f);   // Transparent
            mat.SetFloat("_Blend", 0f);     // Alpha
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }
}
