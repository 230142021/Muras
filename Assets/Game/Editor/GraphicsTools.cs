using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Меню графики: HDR-цветокоррекция в URP и снимки «до / после» из текущей камеры (Play Mode).</summary>
public static class GraphicsTools
{
    [MenuItem("Tools/Diploma/Graphics: Setup URP (HDR grading)")]
    static void SetupUrp()
    {
        var urp = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) { Debug.LogWarning("URP-ассет не найден"); return; }
        urp.colorGradingMode = ColorGradingMode.HighDynamicRange;   // тонемаппинг работает в HDR — мягче переходы в ярком
        urp.supportsHDR = true;
        EditorUtility.SetDirty(urp);
        AssetDatabase.SaveAssets();
        Debug.Log("[Graphics] URP: HDR color grading включён");
    }

    [MenuItem("Tools/Diploma/Play Mode: Fun/Graphics: capture before-after (noon, sunset, night)")]
    static void Capture()
    {
        if (!Application.isPlaying || Camera.main == null || DayNightCycle.Instance == null) { Debug.LogWarning("Нажми Play в сцене Village"); return; }
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "shots"));
        Directory.CreateDirectory(dir);
        float keep = DayNightCycle.Instance.hour;
        foreach (var (label, hour) in new[] { ("1_noon", 12.5f), ("2_sunset", 18.6f), ("3_night", 22.5f) })
            foreach (bool on in new[] { false, true })
            {
                GraphicsPolish.SetEnhanced(on);
                DayNightCycle.Instance.hour = hour;
                DayNightCycle.Instance.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
                foreach (var tg in Object.FindObjectsByType<TimeGrading>(FindObjectsSortMode.None)) tg.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
                if (GraphicsPolish.Instance != null) GraphicsPolish.Instance.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
                Save(Camera.main, Path.Combine(dir, label + (on ? "_after" : "_before") + ".png"));
            }
        GraphicsPolish.SetEnhanced(true);
        DayNightCycle.Instance.hour = keep;
        Debug.Log("[Graphics] снимки: " + dir + "  · растений с ветром: " + GraphicsPolish.WindRenderers);
    }

    [MenuItem("Tools/Diploma/Play Mode: Fun/Graphics: capture look variants")]
    static void Variants()
    {
        if (!Application.isPlaying || Camera.main == null || DayNightCycle.Instance == null) { Debug.LogWarning("Нажми Play в сцене Village"); return; }
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "shots"));
        Directory.CreateDirectory(dir);
        float keep = DayNightCycle.Instance.hour;
        var looks = new (string n, UnityEngine.Rendering.Universal.TonemappingMode m, float e, float s, float c)[]
        {
            ("A_neutral", UnityEngine.Rendering.Universal.TonemappingMode.Neutral, 0.2f, 10f, 8f),
            ("B_neutral_strong", UnityEngine.Rendering.Universal.TonemappingMode.Neutral, 0.25f, 18f, 14f),
            ("C_aces", UnityEngine.Rendering.Universal.TonemappingMode.ACES, 0.45f, 0f, 0f),
            ("D_aces_soft", UnityEngine.Rendering.Universal.TonemappingMode.ACES, 0.6f, -8f, -6f),
        };
        foreach (var (label, hour) in new[] { ("1_noon", 12.5f), ("2_sunset", 18.6f), ("3_night", 22.5f) })
        {
            DayNightCycle.Instance.hour = hour;
            foreach (var l in looks)
            {
                GraphicsPolish.SetLook(l.m, l.e, l.s, l.c);
                DayNightCycle.Instance.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
                foreach (var tg in Object.FindObjectsByType<TimeGrading>(FindObjectsSortMode.None)) tg.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
                GraphicsPolish.Instance.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
                Save(Camera.main, Path.Combine(dir, label + "_" + l.n + ".png"));
            }
        }
        GraphicsPolish.SetLook(UnityEngine.Rendering.Universal.TonemappingMode.Neutral, 0.12f, 10f, 8f);
        DayNightCycle.Instance.hour = keep;
        Debug.Log("[Graphics] варианты: " + dir);
    }

    [MenuItem("Tools/Diploma/Play Mode: Fun/Graphics: wind test (still vs exaggerated)")]
    static void WindTest()
    {
        if (!Application.isPlaying || Camera.main == null) return;
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "shots"));
        Directory.CreateDirectory(dir);
        Shader.SetGlobalFloat("_MurasWind", 0f);
        Save(Camera.main, Path.Combine(dir, "wind_0.png"));
        Shader.SetGlobalFloat("_MurasWind", 6f);
        Save(Camera.main, Path.Combine(dir, "wind_6.png"));
        GraphicsPolish.ApplyQuality();
        Debug.Log("[Graphics] wind test saved");
    }

    static void Save(Camera cam, string path)
    {
        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prev;
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release(); Object.DestroyImmediate(rt);
    }
}
