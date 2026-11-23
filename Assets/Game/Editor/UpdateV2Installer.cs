using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Обновление v2: облака, новая цветокоррекция вечера/ночи, тише и мягче шаги, Айбек-гид
/// (портрет в панели речи), иконки капчыка и новая трасса ат чабыш.
/// Запуск: Tools > Diploma > Install Update v2. Можно запускать повторно.
/// </summary>
public static class UpdateV2Installer
{
    private const string ScenePath = "Assets/Game/Scenes/Village.unity";
    private const string Clouds = "Assets/Game/Models/Clouds/";
    private const string MatDir = "Assets/Game/Materials/";
    private const string Sprites = "Assets/Game/UI/Sprites/";

    [MenuItem("Tools/Diploma/Install Update v2 (inventory, Aibek guide, clouds, evening, race)")]
    public static void Install()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var log = new List<string>();
        IEnumerable<Transform> All() => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true));

        // иконки капчыка: прозрачный фон без ореола
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Game/Resources/MURAS/Icons" }))
            if (AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) is TextureImporter ti && (!ti.alphaIsTransparency || ti.mipmapEnabled))
            { ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport(); }

        GameObject player = All().Select(t => t.GetComponent<PlayerMovement>()).Where(p => p != null).Select(p => p.gameObject).FirstOrDefault();

        // ---- облака
        foreach (Transform t in All().Where(t => t.name == "Clouds" && t.GetComponent<CloudLayer>() != null).ToList()) Object.DestroyImmediate(t.gameObject);
        var cgo = new GameObject("Clouds");
        if (player != null) cgo.transform.position = new Vector3(player.transform.position.x, 0f, player.transform.position.z);
        var cl = cgo.AddComponent<CloudLayer>();
        cl.cloudModels = Enumerable.Range(1, 4).Select(i => AssetDatabase.LoadAssetAtPath<GameObject>(Clouds + "Cloud_" + i + ".obj")).Where(g => g != null).ToArray();
        cl.material = CloudMat();
        log.Add("облака: " + cl.cloudModels.Length + " формы × " + cl.count);

        // ---- день/ночь: новые цвета и цветокоррекция
        DayNightCycle dn = All().Select(t => t.GetComponent<DayNightCycle>()).FirstOrDefault(d => d != null);
        if (dn != null)
        {
            dn.SetDefaults();
            EditorUtility.SetDirty(dn);
            if (dn.GetComponent<TimeGrading>() == null) dn.gameObject.AddComponent<TimeGrading>();
            log.Add("вечер и ночь: новые цвета + цветокоррекция по времени");
        }

        // ---- шаги
        if (player != null && player.TryGetComponent(out PlayerFootsteps fs)) { fs.volume = 0.42f; EditorUtility.SetDirty(fs); log.Add("шаги: новые звуки, тише"); }

        // ---- Айбек и Болот в панели речи (с английскими ключами тоже)
        SpeechPanel sp = All().Select(t => t.GetComponent<SpeechPanel>()).FirstOrDefault(s => s != null);
        if (sp != null)
        {
            Sprite aibek = AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + "ui_portrait_aibek.png");
            if (aibek == null && AssetImporter.GetAtPath(Sprites + "ui_portrait_aibek.png") is TextureImporter ti)
            { ti.textureType = TextureImporterType.Sprite; ti.SaveAndReimport(); aibek = AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + "ui_portrait_aibek.png"); }
            Speaker(sp, "Айбек", "Айбек", "друг · гид", "friend · guide", aibek, 1.05f);
            Speaker(sp, "Aibek", "Айбек", "друг · гид", "friend · guide", aibek, 1.05f);
            var bolot = sp.speakers.FirstOrDefault(s => s.key == "Болот");
            if (bolot != null) Speaker(sp, "Bolot", "Болот", bolot.role, bolot.roleEn, bolot.portrait, bolot.voicePitch);
            EditorUtility.SetDirty(sp);
            log.Add("Айбек: портрет в панели речи");
        }

        // ---- яблоки: регулярно отрастают, собираются в капчык
        foreach (ApplePickup a in All().Select(t => t.GetComponent<ApplePickup>()).Where(a => a != null))
        {
            a.gameObject.SetActive(true);
            if (a.regrowSeconds <= 0f) a.regrowSeconds = 180f;
            EditorUtility.SetDirty(a);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Update v2] " + string.Join("; ", log));

        FunInstaller.RebuildRace();   // новая трасса (просторнее, ближе к селу, без реки)
    }

    static void Speaker(SpeechPanel sp, string key, string name, string role, string roleEn, Sprite portrait, float pitch)
    {
        var s = sp.speakers.FirstOrDefault(x => x.key == key);
        if (s == null) { s = new SpeechPanel.Speaker(); sp.speakers.Add(s); }
        s.key = key; s.displayName = name; s.role = role; s.roleEn = roleEn; s.voicePitch = pitch;
        if (portrait != null) s.portrait = portrait;
    }

    static Material CloudMat()
    {
        string path = MatDir + "Clouds.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", new Color(0.97f, 0.97f, 1f));
        m.SetFloat("_Smoothness", 0f);
        m.SetFloat("_Cull", 0f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(0.5f, 0.5f, 0.55f));
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(m);
        return m;
    }
}
