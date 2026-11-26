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

    [MenuItem("Tools/Diploma/Play Mode: Fun/Graphics: capture sky + river")]
    static void SkyRiver()
    {
        if (!Application.isPlaying || Camera.main == null || DayNightCycle.Instance == null) return;
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "shots"));
        Directory.CreateDirectory(dir);
        Camera cam = Camera.main;
        Vector3 p0 = cam.transform.position; Quaternion r0 = cam.transform.rotation;
        float keep = DayNightCycle.Instance.hour;
        void Shot(string name, float hour)
        {
            DayNightCycle.Instance.hour = hour;
            DayNightCycle.Instance.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
            foreach (var tg in Object.FindObjectsByType<TimeGrading>(FindObjectsSortMode.None)) tg.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
            GraphicsPolish.Instance.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
            foreach (var cl in Object.FindObjectsByType<CloudLayer>(FindObjectsSortMode.None)) cl.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
            Save(cam, Path.Combine(dir, name + ".png"));
        }
        // небо: камера смотрит выше горизонта на солнце / луну
        foreach (var (n, h, lightName) in new[] { ("sky_noon", 12.5f, "sun"), ("sky_sunset", 18.55f, "sun"), ("sky_night", 23f, "moon") })
        {
            Light l = lightName == "sun" ? DayNightCycle.Instance.sun : DayNightCycle.Instance.moon;
            DayNightCycle.Instance.hour = h; DayNightCycle.Instance.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
            Vector3 d = l != null ? -l.transform.forward : Vector3.forward;
            d.y = Mathf.Clamp(d.y, 0.05f, 0.35f);
            cam.transform.rotation = Quaternion.LookRotation(d.normalized);
            Shot(n, h);
        }
        // река: с пирса вдоль русла
        var spot = Object.FindAnyObjectByType<FishingSpot>();
        if (spot != null)
        {
            cam.transform.position = spot.transform.position + Vector3.up * 2.2f - spot.transform.forward * 1.5f;
            cam.transform.rotation = Quaternion.LookRotation(spot.transform.forward + Vector3.down * 0.45f);
            Shot("river_noon", 12.5f);
            Shot("river_sunset", 18.4f);
            Shot("river_night", 22.5f);
        }
        cam.transform.position = p0; cam.transform.rotation = r0;
        DayNightCycle.Instance.hour = keep;
        Debug.Log("[Graphics] небо и река: " + dir);
    }

    [MenuItem("Tools/Diploma/Play Mode: Fun/Graphics: capture reflections + shadows")]
    static void ReflShadows()
    {
        if (!Application.isPlaying || Camera.main == null || DayNightCycle.Instance == null || GraphicsPolish.Instance == null) return;
        GraphicsPolish.Instance.StartCoroutine(ReflShadowsRoutine());
    }

    static System.Collections.IEnumerator ReflShadowsRoutine()
    {
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "shots"));
        Directory.CreateDirectory(dir);
        Camera cam = Camera.main;
        Vector3 p0 = cam.transform.position; Quaternion r0 = cam.transform.rotation;
        float keep = DayNightCycle.Instance.hour;
        var pm = Object.FindAnyObjectByType<PlayerMovement>();
        GameObject player = pm != null ? pm.gameObject : GameObject.FindWithTag("Player");
        Vector3 pp = player != null ? player.transform.position : p0;
        // скрипты слежения камеры на время съёмки выключаем (иначе вернут камеру за игрока)
        var paused = new System.Collections.Generic.List<MonoBehaviour>();
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (mb == null || !mb.enabled) continue;
            string ns = mb.GetType().Namespace ?? "";
            if (ns.StartsWith("UnityEngine")) continue;
            string tn = mb.GetType().Name;
            if (mb.transform == cam.transform || tn.Contains("Camera") || tn.Contains("Follow") || tn.Contains("Orbit"))
            { mb.enabled = false; paused.Add(mb); }
        }

        // окно снаружи дома: перебираем ближайшие окна и точки съёмки, берём ту, откуда стекло видно без препятствий
        var wins = new System.Collections.Generic.List<Renderer>();
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            foreach (var m in r.sharedMaterials)
                if (m != null && m.name.Contains("Window") && !m.name.Contains("Frame") && !m.name.Contains("Interior") && !m.name.Contains("Lamp")) { wins.Add(r); break; }
        wins.Sort((a, b) => (a.bounds.center - pp).sqrMagnitude.CompareTo((b.bounds.center - pp).sqrMagnitude));
        Vector3 camPos = Vector3.zero, look = Vector3.zero; bool found = false;
        foreach (var w in wins)
        {
            Vector3 c = w.bounds.center;
            var hl = w.GetComponentInParent<HouseLights>();
            Transform house = hl != null ? hl.transform : (w.transform.parent != null ? w.transform.parent : w.transform);
            Vector3 hc = house.position;
            var rends = house.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0) { Bounds hb = rends[0].bounds; foreach (var rr in rends) hb.Encapsulate(rr.bounds); hc = hb.center; }
            Vector3 n = c - hc; n.y = 0f;
            if (n.sqrMagnitude < 0.01f) continue;
            n.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, n);
            foreach (float s2 in new[] { 3.2f, -3.2f, 1.5f, -1.5f })
            {
                Vector3 p = c + n * 5f + side * s2 + Vector3.up * 0.2f;
                if (Physics.Linecast(p, c + n * 0.15f)) continue;            // что-то загораживает
                if (!Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out RaycastHit g, 10f) || g.point.y > p.y - 0.8f) continue;   // не внутри/не под землёй
                camPos = p; look = c; found = true; break;
            }
            if (found) break;
        }

        System.Collections.IEnumerator Shot(string name, float hour)
        {
            for (int f = 0; f < 4; f++)        // пара кадров, чтобы небо, проба и тени обновились
            {
                DayNightCycle.Instance.hour = hour;
                if (f == 1) GraphicsPolish.RefreshReflections();
                yield return null;
            }
            foreach (var cl in Object.FindObjectsByType<CloudLayer>(FindObjectsSortMode.None)) cl.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
            Save(cam, Path.Combine(dir, name + ".png"));
        }

        if (found)
        {
            cam.transform.position = camPos;
            cam.transform.rotation = Quaternion.LookRotation(look - camPos);
            yield return Shot("refl_noon", 12.5f);
            yield return Shot("refl_sunset", 18.5f);
            yield return Shot("refl_night", 22.5f);
        }

        // тени: сверху-сбоку на игрока и предметы вокруг, до и после
        if (player != null)
        {
            Vector3 fwd = player.transform.forward; fwd.y = 0f; fwd.Normalize();
            cam.transform.position = pp - fwd * 3.5f + Vector3.up * 3.2f + Vector3.Cross(Vector3.up, fwd) * 1.5f;
            cam.transform.rotation = Quaternion.LookRotation(pp + fwd * 2.5f - cam.transform.position);
            var sb = new System.Text.StringBuilder("[Graphics] рядом с игроком (тени): ");
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if ((r.bounds.center - pp).sqrMagnitude < 64f) sb.Append(r.name).Append('=').Append(r.shadowCastingMode).Append(r.enabled ? "" : "(off)").Append("; ");
            Debug.Log(sb.ToString());
            GraphicsPolish.SetShadowTweaks(false);
            yield return Shot("shadow_before", 16f);
            GraphicsPolish.SetShadowTweaks(true);
            yield return Shot("shadow_after", 16f);
        }

        cam.transform.position = p0; cam.transform.rotation = r0;
        foreach (var mb in paused) if (mb != null) mb.enabled = true;
        DayNightCycle.Instance.hour = keep;
        Debug.Log("[Graphics] отражения и тени: " + dir + "  (стёкол: " + GraphicsPolish.GlassMaterials + ")");
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
