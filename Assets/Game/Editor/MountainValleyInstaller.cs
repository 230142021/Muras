using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ставит в Village горную долину вместо плоского Ground: ровная зелёная поляна под селом,
/// холмы, лесной пояс, скалы и снежные вершины вокруг. Центр долины совпадает с центром старого пола,
/// поэтому дома, деревья и NPC остаются на своих местах.
/// Запуск: Tools > Diploma > Install Mountain Valley.
/// </summary>
public static class MountainValleyInstaller
{
    private const string ScenePath = "Assets/Game/Scenes/Village.unity";
    private const string Folder = "Assets/Game/Models/Environment/";
    private const string ModelPath = Folder + "MountainValley.obj";
    private const string PalettePath = Folder + "valley_palette.png";
    private const string MaterialPath = Folder + "MountainValley.mat";

    [MenuItem("Tools/Diploma/Install Mountain Valley")]
    public static void Install()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string previous = SceneManager.GetActiveScene().path;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        Transform ground = all.FirstOrDefault(t => t.name == "Ground");
        Transform environment = all.FirstOrDefault(t => t.name == "Environment");

        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset == null) { Debug.LogError("[MountainValley] Нет модели " + ModelPath); return; }

        // Палитра цветов: без сглаживания, чтобы грани были чёткими (low-poly)
        if (AssetImporter.GetAtPath(PalettePath) is TextureImporter ti)
        {
            ti.textureType = TextureImporterType.Default;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
        Texture2D palette = AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath);

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", palette);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", palette);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0f);
        EditorUtility.SetDirty(mat);

        // Центр — там же, где был старый пол
        Vector3 center = ground != null ? ground.position : Vector3.zero;

        Transform old = all.FirstOrDefault(t => t.name == "MountainValley" && t.parent == environment);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        GameObject valley = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, environment);
        valley.name = "MountainValley";
        valley.transform.position = center;
        valley.transform.rotation = Quaternion.identity;
        valley.transform.localScale = Vector3.one;
        GameObjectUtility.SetStaticEditorFlags(valley, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);

        foreach (MeshRenderer r in valley.GetComponentsInChildren<MeshRenderer>(true))
        {
            r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf != null && !r.TryGetComponent(out MeshCollider _))
                r.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }

        // Старый плоский пол прячем (не удаляем — можно вернуть, включив Ground)
        if (ground != null)
            ground.gameObject.SetActive(false);

        // Дальность камеры и лёгкая дымка, чтобы горы смотрелись далёкими
        // (берём камеры заново: список all содержит уже удалённую старую долину)
        foreach (Camera cam in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)))
            if (cam.farClipPlane < 800f) { cam.farClipPlane = 800f; EditorUtility.SetDirty(cam); }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.72f, 0.80f, 0.88f);
        RenderSettings.fogStartDistance = 90f;
        RenderSettings.fogEndDistance = 520f;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[MountainValley] Горная долина установлена, старый Ground выключен. Сцена Village сохранена.");

        if (!string.IsNullOrEmpty(previous) && previous != ScenePath)
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
    }
}
