using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Добавляет в Village звуки: тихий фон села (ветер, птицы, далёкий комуз) и журчание реки —
/// несколько 3D-источников вдоль русла, слышно только рядом с водой.
/// Запуск: Tools > Diploma > Add Village Sounds. Можно запускать повторно.
/// </summary>
public static class VillageSoundInstaller
{
    private const string ScenePath = "Assets/Game/Scenes/Village.unity";
    private const string AudioDir = "Assets/Game/Audio/";

    [MenuItem("Tools/Diploma/Add Village Sounds")]
    public static void Install()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        SetImport(AudioDir + "village_ambience.ogg", AudioClipLoadType.Streaming);
        SetImport(AudioDir + "river_loop.ogg", AudioClipLoadType.CompressedInMemory);
        SetImport(AudioDir + "quest_complete.wav", AudioClipLoadType.DecompressOnLoad);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // 1. Фон села
        var ambClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "village_ambience.ogg");
        GameObject amb = Root(scene, "VillageAmbience") ?? new GameObject("VillageAmbience");
        var src = GetOrAdd<AudioSource>(amb);
        src.clip = ambClip;
        src.spatialBlend = 0f;
        src.loop = true;
        src.playOnAwake = false;
        var loop = GetOrAdd<AmbientLoop>(amb);
        loop.volume = 0.35f;
        loop.duckOnQuestComplete = true;

        // 2. Река
        var riverClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "river_loop.ogg");
        GameObject oldRiver = Root(scene, "RiverSounds");
        if (oldRiver != null) Object.DestroyImmediate(oldRiver);
        Transform river = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == "River");
        int count = 0;
        if (river != null && riverClip != null)
        {
            var root = new GameObject("RiverSounds");
            foreach (Vector3 p in RiverPoints(river, 7))
            {
                var go = new GameObject("RiverSound_" + (++count));
                go.transform.SetParent(root.transform, false);
                go.transform.position = p + Vector3.up * 0.3f;
                var s = go.AddComponent<AudioSource>();
                s.clip = riverClip;
                s.loop = true;
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 3f;
                s.maxDistance = 22f;
                s.dopplerLevel = 0f;
                s.spread = 120f;
                var l = go.AddComponent<AmbientLoop>();
                l.volume = 0.6f;
                l.duckOnQuestComplete = false;
                l.fadeInTime = 2f;
            }
        }
        else Debug.LogWarning("[VillageSounds] Не найден объект River — журчание не добавлено.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[VillageSounds] Фон села добавлен, источников у реки: {count}. Мелодия квеста обновлена. Village сохранена.");
    }

    // Точки вдоль русла: делим вершины воды по длинной оси и берём центр каждой части
    private static List<Vector3> RiverPoints(Transform river, int parts)
    {
        var verts = new List<Vector3>();
        foreach (MeshFilter mf in river.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Matrix4x4 m = mf.transform.localToWorldMatrix;
            foreach (Vector3 v in mf.sharedMesh.vertices) verts.Add(m.MultiplyPoint3x4(v));
        }
        var result = new List<Vector3>();
        if (verts.Count == 0) { result.Add(river.position); return result; }

        Vector3 min = verts[0], max = verts[0];
        foreach (Vector3 v in verts) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }
        bool alongX = max.x - min.x > max.z - min.z;
        float lo = alongX ? min.x : min.z, hi = alongX ? max.x : max.z;
        for (int i = 0; i < parts; i++)
        {
            float a = Mathf.Lerp(lo, hi, (i + 0.0f) / parts), b = Mathf.Lerp(lo, hi, (i + 1f) / parts);
            var bin = verts.Where(v => { float k = alongX ? v.x : v.z; return k >= a && k <= b; }).ToList();
            if (bin.Count == 0) continue;
            Vector3 c = Vector3.zero;
            foreach (Vector3 v in bin) c += v;
            c /= bin.Count;
            c.y = bin.Max(v => v.y);
            result.Add(c);
        }
        return result;
    }

    private static void SetImport(string path, AudioClipLoadType load)
    {
        if (!(AssetImporter.GetAtPath(path) is AudioImporter ai)) { Debug.LogWarning("[VillageSounds] Нет файла " + path); return; }
        var s = ai.defaultSampleSettings;
        s.loadType = load;
        s.compressionFormat = load == AudioClipLoadType.DecompressOnLoad ? AudioCompressionFormat.PCM : AudioCompressionFormat.Vorbis;
        s.quality = 0.6f;
        ai.defaultSampleSettings = s;
        ai.loadInBackground = load == AudioClipLoadType.Streaming;
        ai.SaveAndReimport();
    }

    private static GameObject Root(Scene scene, string name) =>
        scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);

    private static T GetOrAdd<T>(GameObject go) where T : Component =>
        go.TryGetComponent(out T c) ? c : go.AddComponent<T>();
}
