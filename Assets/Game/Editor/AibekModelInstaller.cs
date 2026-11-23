using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ставит модель Айбека вместо капсулы NPC_Aibek в сцене Village.
/// Капсула остаётся (коллайдер и скрипты квеста работают), выключается только её отображение.
/// Запуск: Tools > Diploma > Install Aibek Model.
/// </summary>
public static class AibekModelInstaller
{
    private const string ScenePath = "Assets/Game/Scenes/Village.unity";
    private const string ModelPath = "Assets/Game/Models/NPC/Aibek.obj";
    private const string ModelChildName = "AibekModel";

    [MenuItem("Tools/Diploma/Install Aibek Model")]
    public static void Install()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string previousScene = SceneManager.GetActiveScene().path;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject npc = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "NPC_Aibek") { npc = t.gameObject; break; }
            if (npc != null) break;
        }

        if (npc == null)
        {
            Debug.LogError("[AibekModelInstaller] В Village нет объекта NPC_Aibek");
            return;
        }

        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset == null)
        {
            Debug.LogError("[AibekModelInstaller] Не найдена модель " + ModelPath);
            return;
        }

        // Уровень земли под NPC: низ капсулы.
        MeshRenderer capsuleRenderer = npc.GetComponent<MeshRenderer>();
        float feetY = capsuleRenderer != null ? capsuleRenderer.bounds.min.y : npc.transform.position.y - 1f;

        Transform old = npc.transform.Find(ModelChildName);
        if (old != null)
            Object.DestroyImmediate(old.gameObject);

        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, npc.transform);
        model.name = ModelChildName;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;
        model.transform.position = new Vector3(npc.transform.position.x, feetY, npc.transform.position.z);

        if (capsuleRenderer != null)
            capsuleRenderer.enabled = false;

        NPCLookAtPlayer look = npc.GetComponent<NPCLookAtPlayer>();
        if (look == null)
            look = npc.AddComponent<NPCLookAtPlayer>();
        look.model = model.transform;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = model;
        Debug.Log("[AibekModelInstaller] Айбек установлен вместо капсулы, сцена Village сохранена.");

        if (!string.IsNullOrEmpty(previousScene) && previousScene != ScenePath)
            EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
    }
}
