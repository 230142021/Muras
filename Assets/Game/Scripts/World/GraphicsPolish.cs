using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Улучшение картинки поверх пресетов качества (Низкое / Среднее / Высокое):
///  • Воздушная перспектива — экспоненциальный туман под цвет неба. Плотность меняется по времени суток
///    (утренняя дымка гуще, днём прозрачнее) и погоде. Цвет тумана задаёт DayNightCycle.
///  • Тонемаппинг (мягкая передача ярких цветов) и Bloom — свечение фонарей, костра, окон, солнца.
///  • Сглаживание краёв: FXAA / SMAA / SMAA High.
///  • Ветер: трава и деревья качаются (шейдер MURAS/WindLit, материалы подменяются при загрузке сцены).
///  • Отражения: realtime Reflection Probe рисует небо MURAS/Sky (сменяется со временем суток) — окна и вода отражают небо.
///  • Тени: контактные тени (SSAO) под предметами и в траве, трава отбрасывает тени, уменьшен сдвиг теней (нет «висящих» объектов).
/// Работает только во время игры; сцену и материалы проекта не меняет.
/// </summary>
public class GraphicsPolish : MonoBehaviour
{
    public static GraphicsPolish Instance { get; private set; }

    // плотность тумана по часам: ночь — умеренно, рассвет — дымка в долине, день — прозрачно, закат — чуть гуще
    static readonly (float h, float d)[] FogKeys =
    {
        (0f, 0.0022f), (5f, 0.0026f), (6.5f, 0.0040f), (8.5f, 0.0028f), (11f, 0.0018f),
        (16.5f, 0.0018f), (18.5f, 0.0021f), (20.5f, 0.0022f), (24f, 0.0022f),
    };

    static readonly string[] TreeWords = { "Tree", "Birch", "Pine", "Fir", "Spruce", "Oak", "Willow", "Poplar" };
    static readonly string[] GrassWords = { "Grass", "Tuft", "Flower", "Sunflower", "Bush", "Fern", "Reed", "Plant", "Weed", "Shrub" };
    static readonly string[] NotPlants = { "Lamp", "House", "Firewood", "Fence", "Yard", "Saray", "Clue", "Daily", "Path", "Floor", "Road", "Bird" };

    Bloom bloom;
    Tonemapping tonemap;
    Vignette vignette;
    DepthOfField dof;
    float dofBlend;
    Material skyMat, oldSky;

    // цвет зенита по часам (горизонт берётся из цвета тумана DayNightCycle — горы тонут в той же дымке)
    static readonly (float h, Color c)[] ZenithKeys =
    {
        (0f, new Color(0.015f, 0.022f, 0.06f)), (5f, new Color(0.03f, 0.04f, 0.10f)), (6.3f, new Color(0.20f, 0.27f, 0.48f)),
        (8f, new Color(0.20f, 0.42f, 0.80f)), (17f, new Color(0.20f, 0.42f, 0.80f)), (18.6f, new Color(0.24f, 0.28f, 0.52f)),
        (19.8f, new Color(0.08f, 0.09f, 0.22f)), (21f, new Color(0.02f, 0.026f, 0.07f)), (24f, new Color(0.015f, 0.022f, 0.06f)),
    };
    Shader windShader;
    readonly Dictionary<(Material, bool), Material> windMats = new Dictionary<(Material, bool), Material>();
    float windTime;

    public static int WindRenderers { get; private set; }

    // отражения и тени
    ReflectionProbe skyProbe;
    float nextProbe;
    public static int GlassMaterials { get; private set; }
    readonly Dictionary<Material, Material> glassMats = new Dictionary<Material, Material>();
    readonly List<Renderer> grassRenderers = new List<Renderer>();
    static bool shadowSaved;
    static float baseDepthBias, baseNormalBias;
    static bool baseSsao;
    static UnityEngine.Rendering.Universal.ScriptableRendererFeature ssao;

    // поправки к цветокоррекции TimeGrading: тонемаппинг слегка гасит цвет и контраст — возвращаем их
    public static float ExposureAdd = 0.12f, SaturationAdd = 10f, ContrastAdd = 8f;
    public static TonemappingMode ToneMode = TonemappingMode.Neutral;

    /// <summary>Для подбора вида: режим тонемаппинга и поправки (используется меню сравнения).</summary>
    public static void SetLook(TonemappingMode mode, float exposure, float saturation, float contrast)
    {
        ToneMode = mode; ExposureAdd = exposure; SaturationAdd = saturation; ContrastAdd = contrast;
        if (Instance != null && Instance.tonemap != null) Instance.tonemap.mode.Override(mode);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance == null)
        {
            var go = new GameObject("GraphicsPolish");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<GraphicsPolish>();
        }
    }

    void Awake()
    {
        Instance = this;
        windShader = Shader.Find("MURAS/WindLit");
        CreateSkyProbe();
        SceneManager.sceneLoaded += OnSceneLoaded;
        Setup(SceneManager.GetActiveScene());
    }

    void OnDestroy() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    void OnSceneLoaded(Scene s, LoadSceneMode m) => Setup(s);

    void Setup(Scene scene)
    {
        bloom = null; tonemap = null; vignette = null; dof = null; dofBlend = 0f;
        SetupVolume();
        if (FindAnyObjectByType<DayNightCycle>() != null)
        {
            SetupSky();
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            ApplyWindMaterials(scene);
            ApplyGlass(scene);
            nextProbe = 0f;
        }
        ApplyQuality();
    }

    // ------------------------------------------------------------------ небо
    void SetupSky()
    {
        Shader sh = Shader.Find("MURAS/Sky");
        if (sh == null || !sh.isSupported) { Debug.LogWarning("MURAS: шейдер MURAS/Sky не найден"); return; }
        oldSky = RenderSettings.skybox;
        skyMat = new Material(sh) { name = "MURAS Sky (runtime)" };
        RenderSettings.skybox = skyMat;
    }

    void UpdateSky()
    {
        var dn = DayNightCycle.Instance;
        if (skyMat == null || dn == null || RenderSettings.skybox != skyMat) return;
        float h = dn.hour, t = h / 24f;
        Color zen = ZenithKeys[0].c;
        for (int i = 0; i < ZenithKeys.Length - 1; i++)
            if (h >= ZenithKeys[i].h && h <= ZenithKeys[i + 1].h)
            { zen = Color.Lerp(ZenithKeys[i].c, ZenithKeys[i + 1].c, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ZenithKeys[i].h, ZenithKeys[i + 1].h, h))); break; }
        Color hor = RenderSettings.fogColor;
        float grey = DayNightCycle.WeatherGrey;
        float gz = zen.grayscale;
        zen = Color.Lerp(zen, new Color(gz, gz, gz) * 1.1f + hor * 0.2f, grey * 0.8f);
        skyMat.SetColor("_ZenithColor", zen);
        skyMat.SetColor("_HorizonColor", hor);
        skyMat.SetColor("_GroundColor", dn.groundAmbient.Evaluate(t) * 0.9f);
        Shader.SetGlobalColor("_MurasSkyZenith", new Color(zen.r, zen.g, zen.b, 1f));
        Shader.SetGlobalColor("_MurasSkyHorizon", new Color(hor.r, hor.g, hor.b, 1f));

        float sunset = Mathf.Max(Mathf.Exp(-Mathf.Pow((h - 18.6f) / 1.1f, 2f)), Mathf.Exp(-Mathf.Pow((h - 6.2f) / 1.0f, 2f)));
        if (dn.sun != null)
        {
            Vector3 sd = -dn.sun.transform.forward;
            skyMat.SetVector("_SunDir", sd);
            Color sc = dn.sunColor.Evaluate(t);
            skyMat.SetColor("_SunColor", sc * Mathf.Lerp(7f, 4f, sunset) * (1f - grey * 0.85f));
            skyMat.SetFloat("_SunAlpha", Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.06f, 0.02f, sd.y)) * (1f - DayNightCycle.NightFactor));
            skyMat.SetFloat("_SunSize", 0.032f + sunset * 0.012f);
        }
        skyMat.SetColor("_SunsetColor", new Color(1.5f, 0.62f, 0.3f) * (1f - grey * 0.7f));
        skyMat.SetFloat("_Sunset", sunset);
        if (dn.moon != null)
        {
            skyMat.SetVector("_MoonDir", -dn.moon.transform.forward);
            float moonA = Mathf.Max(DayNightCycle.NightFactor, dn.moon.enabled ? 0.35f : 0f);
            skyMat.SetFloat("_MoonAlpha", moonA * (1f - grey * 0.9f));
        }
        skyMat.SetFloat("_StarAlpha", DayNightCycle.NightFactor * (1f - grey));
    }

    // ------------------------------------------------------------------ размытие фона в диалоге
    void UpdateDialogBlur()
    {
        if (dof == null) return;
        bool talking = (SpeechPanel.Instance != null && SpeechPanel.Instance.IsShowing);
        if (dialogue == null && Time.unscaledTime > nextDialogueSearch) { dialogue = FindAnyObjectByType<DialogueManager>(); nextDialogueSearch = Time.unscaledTime + 3f; }
        if (dialogue != null && dialogue.IsOpen) talking = true;
        bool allowed = Enhanced && GameSettings.Quality >= 1;
        dofBlend = Mathf.MoveTowards(dofBlend, talking && allowed ? 1f : 0f, Time.unscaledDeltaTime * 2.5f);
        dof.active = dofBlend > 0.01f;
        if (!dof.active) return;
        // резкость — на герое и собеседнике рядом с ним, дальше мягко размывается
        Camera cam = Camera.main;
        float focus = 6f;
        if (cam != null && player == null) { var pm = FindAnyObjectByType<PlayerMovement>(); if (pm != null) player = pm.transform; }
        if (cam != null && player != null) focus = Vector3.Distance(cam.transform.position, player.position);
        dof.gaussianStart.Override(focus + 2.5f);
        dof.gaussianEnd.Override(focus + 14f);
        dof.gaussianMaxRadius.Override(Mathf.Lerp(0f, GameSettings.Quality == 2 ? 1.4f : 1.0f, dofBlend));
        dof.highQualitySampling.Override(GameSettings.Quality == 2);
    }
    Transform player;
    DialogueManager dialogue;
    float nextDialogueSearch;

    // ------------------------------------------------------------------ пост-обработка
    void SetupVolume()
    {
        Volume global = null;
        foreach (Volume v in FindObjectsByType<Volume>(FindObjectsSortMode.None)) if (v.isGlobal) { global = v; break; }
        if (global == null) return;
        VolumeProfile p = global.profile;           // во время игры — копия профиля
        if (!p.TryGet(out tonemap)) tonemap = p.Add<Tonemapping>(true);
        tonemap.mode.Override(ToneMode);
        if (!p.TryGet(out bloom)) bloom = p.Add<Bloom>(true);
        bloom.threshold.Override(1.05f);
        bloom.intensity.Override(0.55f);
        bloom.scatter.Override(0.62f);
        bloom.tint.Override(new Color(1f, 0.93f, 0.82f));
        bloom.highQualityFiltering.Override(false);
        if (!p.TryGet(out dof)) dof = p.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Gaussian);
        dof.gaussianMaxRadius.Override(0f);
        dof.active = false;
        if (!p.TryGet(out vignette)) vignette = p.Add<Vignette>(true);
        vignette.intensity.Override(0.2f);
        vignette.smoothness.Override(0.45f);
        vignette.color.Override(new Color(0.08f, 0.05f, 0.03f));
    }

    /// <summary>Для сравнения «до/после»: выключить все улучшения (туман снова линейный, без пост-эффектов и сглаживания).</summary>
    public static bool Enhanced { get; private set; } = true;
    public static void SetEnhanced(bool on)
    {
        if (Instance == null) return;
        Enhanced = on;
        var me = Instance;
        if (me.tonemap != null) me.tonemap.active = on;
        if (me.vignette != null) me.vignette.active = on;
        if (me.skyMat != null) RenderSettings.skybox = on ? me.skyMat : (me.oldSky != null ? me.oldSky : RenderSettings.skybox);
        if (!on)
        {
            Shader.SetGlobalColor("_MurasSkyZenith", Color.clear);
            Shader.SetGlobalColor("_MurasSkyHorizon", Color.clear);
            if (me.bloom != null) me.bloom.active = false;
            RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 70f; RenderSettings.fogEndDistance = 480f;
            Camera cam = Camera.main;
            if (cam != null) cam.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.None;
            Shader.SetGlobalFloat("_MurasWind", 0f);
            if (me.skyProbe != null) me.skyProbe.enabled = false;
            RestoreShadows();
        }
        else { RenderSettings.fogMode = FogMode.Exponential; ApplyQuality(); }
    }

    /// <summary>Вызывается при смене пресета качества (GameSettings.ApplyGraphics).</summary>
    public static void ApplyQuality()
    {
        if (Instance == null || !Enhanced) return;
        int q = GameSettings.Quality;
        var me = Instance;
        if (me.bloom != null)
        {
            me.bloom.active = q >= 1;
            me.bloom.highQualityFiltering.Override(q == 2);
        }
        if (me.vignette != null) me.vignette.active = true;

        Camera cam = Camera.main;
        if (cam != null)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = q == 0 ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = q == 2 ? AntialiasingQuality.High : AntialiasingQuality.Medium;
        }
        Shader.SetGlobalFloat("_MurasWind", q == 0 ? 0f : 1f);
        me.ApplyShadows(q);
        if (me.skyProbe != null) { me.skyProbe.enabled = true; me.skyProbe.resolution = q == 2 ? 256 : 128; me.nextProbe = 0f; }
    }

    // ------------------------------------------------------------------ туман и ветер по времени
    void Update()
    {
        if (Enhanced && DayNightCycle.Instance != null && RenderSettings.fog)
        {
            float h = DayNightCycle.Hour, d = FogKeys[0].d;
            for (int i = 0; i < FogKeys.Length - 1; i++)
                if (h >= FogKeys[i].h && h <= FogKeys[i + 1].h)
                {
                    d = Mathf.Lerp(FogKeys[i].d, FogKeys[i + 1].d, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FogKeys[i].h, FogKeys[i + 1].h, h)));
                    break;
                }
            RenderSettings.fogDensity = d * (1f + DayNightCycle.WeatherGrey * 1.6f);   // в дождь и снег — гуще
        }
        if (Enhanced) { UpdateSky(); UpdateSkyProbe(); }
        UpdateDialogBlur();
        // ветер: направление медленно гуляет, в непогоду сильнее
        windTime += Time.deltaTime;
        float a = 0.6f + Mathf.Sin(windTime * 0.02f) * 0.5f;
        float strength = 1f + DayNightCycle.WeatherGrey * 1.2f;
        Shader.SetGlobalVector("_MurasWindDir", new Vector4(Mathf.Cos(a) * strength, Mathf.Sin(a) * strength, 0f, 0f));
    }

    // ------------------------------------------------------------------ отражения неба (Reflection Probe)
    void CreateSkyProbe()
    {
        var go = new GameObject("MURAS Sky Reflection");
        go.transform.SetParent(transform, false);
        skyProbe = go.AddComponent<ReflectionProbe>();
        skyProbe.mode = ReflectionProbeMode.Realtime;
        skyProbe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
        skyProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        skyProbe.clearFlags = ReflectionProbeClearFlags.Skybox;
        skyProbe.cullingMask = 0;                     // рисует только небо — дёшево (6 граней неба)
        skyProbe.size = new Vector3(6000f, 3000f, 6000f);
        skyProbe.boxProjection = false;
        skyProbe.importance = 0;                      // запечённые пробы в сцене (если есть) важнее
        skyProbe.hdr = true;
        skyProbe.resolution = 128;
        skyProbe.intensity = 1f;
    }

    void UpdateSkyProbe()
    {
        if (skyProbe == null || !skyProbe.enabled) return;
        Camera cam = Camera.main;
        if (cam != null) skyProbe.transform.position = cam.transform.position;
        if (Time.unscaledTime < nextProbe) return;
        // небо меняется медленно: High — 2 раза в секунду, иначе раз в 1.5 с
        nextProbe = Time.unscaledTime + (GameSettings.Quality == 2 ? 0.5f : 1.5f);
        skyProbe.RenderProbe();
    }

    /// <summary>Стёкла окон становятся гладкими — в них отражается небо из пробы.</summary>
    void ApplyGlass(Scene scene)
    {
        GlassMaterials = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null || !IsGlass(m)) continue;
                    if (m.name.Contains("(Instance)") || m.name.Contains("(MURAS)"))   // уже копия (например, у HouseLights) — правим её
                    { MakeGlossy(m); continue; }
                    if (!glassMats.TryGetValue(m, out Material g))
                    {
                        g = new Material(m) { name = m.name + " (MURAS)" };
                        MakeGlossy(g);
                        glassMats[m] = g;
                        GlassMaterials++;
                    }
                    mats[i] = g; changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
    }

    static bool IsGlass(Material m)
    {
        string n = m.name;
        if (!(n.Contains("Window") || n.Contains("Glass"))) return false;
        if (n.Contains("Frame") || n.Contains("Lamp") || n.Contains("Sill")) return false;
        string sh = m.shader != null ? m.shader.name : "";
        return sh.StartsWith("Universal Render Pipeline/");
    }

    static void MakeGlossy(Material m)
    {
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.93f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.93f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.25f);
        if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 1f);
        if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 1f);
        m.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        m.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
    }

    // ------------------------------------------------------------------ тени
    void ApplyShadows(int q)
    {
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) return;
        if (!shadowSaved)
        {
            shadowSaved = true;
            baseDepthBias = urp.shadowDepthBias; baseNormalBias = urp.shadowNormalBias;
            ssao = FindSsao(urp);
            baseSsao = ssao != null && ssao.isActive;
            Application.quitting += RestoreShadows;    // в редакторе ассеты не должны остаться изменёнными
        }
        // меньший сдвиг — тень начинается прямо у основания предмета, без «зазора» (peter-panning)
        urp.shadowDepthBias = 0.35f;
        urp.shadowNormalBias = 0.2f;
        if (ssao != null) ssao.SetActive(q >= 1);     // контактные тени под предметами и в траве
        foreach (Renderer r in grassRenderers)
            if (r != null) r.shadowCastingMode = q >= 1 ? ShadowCastingMode.On : ShadowCastingMode.Off;
    }

    /// <summary>Для сравнения «до/после» теней (инструмент захвата кадров).</summary>
    public static void SetShadowTweaks(bool on)
    {
        if (Instance == null) return;
        if (on) Instance.ApplyShadows(GameSettings.Quality);
        else { RestoreShadows(); if (ssao != null) ssao.SetActive(false); }
    }

    /// <summary>Перерисовать пробу неба сейчас (после резкой смены времени суток).</summary>
    public static void RefreshReflections()
    {
        if (Instance != null && Instance.skyProbe != null && Instance.skyProbe.enabled) Instance.skyProbe.RenderProbe();
    }

    static void RestoreShadows()
    {
        if (!shadowSaved) return;
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null) { urp.shadowDepthBias = baseDepthBias; urp.shadowNormalBias = baseNormalBias; }
        if (ssao != null) ssao.SetActive(baseSsao);
        shadowSaved = false;
    }

    static ScriptableRendererFeature FindSsao(UniversalRenderPipelineAsset urp)
    {
        foreach (ScriptableRendererData data in urp.rendererDataList)
        {
            if (data == null) continue;
            foreach (ScriptableRendererFeature f in data.rendererFeatures)
                if (f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion") return f;
        }
        return null;
    }

    // ------------------------------------------------------------------ материалы с ветром
    void ApplyWindMaterials(Scene scene)
    {
        WindRenderers = 0;
        grassRenderers.Clear();
        if (windShader == null || !windShader.isSupported) { Debug.LogWarning("MURAS: шейдер MURAS/WindLit не найден — ветер выключен"); return; }
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                int kind = Classify(r.transform);
                if (kind == 0) continue;
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material src = mats[i];
                    if (src == null || src.shader == null) continue;
                    string sh = src.shader.name;
                    if (sh != "Universal Render Pipeline/Lit" && sh != "Universal Render Pipeline/Simple Lit") continue;
                    if (src.HasProperty("_Surface") && src.GetFloat("_Surface") > 0.5f) continue;     // прозрачные не трогаем
                    mats[i] = WindMaterial(src, kind == 2);
                    changed = true;
                }
                if (changed) { r.sharedMaterials = mats; WindRenderers++; }
                if (kind == 2) grassRenderers.Add(r);
            }
    }

    /// <summary>1 — дерево, 2 — трава/цветы, 0 — не растение (по именам объекта и его родителей).</summary>
    static int Classify(Transform t)
    {
        for (int depth = 0; t != null && depth < 4; depth++, t = t.parent)
        {
            string n = t.name;
            foreach (string w in NotPlants) if (n.Contains(w)) return 0;
            if (n == "Trees" || n == "Trees (1)") return 0;               // папки, а не деревья
            foreach (string w in GrassWords) if (n.Contains(w)) return 2;
            foreach (string w in TreeWords) if (n.Contains(w)) return 1;
        }
        return 0;
    }

    Material WindMaterial(Material src, bool grass)
    {
        if (windMats.TryGetValue((src, grass), out Material m)) return m;
        m = new Material(src) { name = src.name + (grass ? " (Wind Grass)" : " (Wind Tree)") };
        m.shader = windShader;                       // _BaseMap, _BaseColor, _Smoothness, _Cutoff, _Cull сохраняются
        if (src.HasProperty("_AlphaClip") && src.GetFloat("_AlphaClip") > 0.5f) m.EnableKeyword("_ALPHATEST_ON");
        if (grass)
        {
            m.SetFloat("_WindStrength", 0.34f);      // трава низкая — гнётся сильнее на метр
            m.SetFloat("_WindBendStart", 0.02f);
            m.SetFloat("_WindMaxHeight", 1.6f);
            m.SetFloat("_WindSpeed", 1.7f);
            m.SetFloat("_WindFlutter", 0.01f);
        }
        else
        {
            m.SetFloat("_WindStrength", 0.018f);
            m.SetFloat("_WindBendStart", 1.2f);      // ствол у земли неподвижен
            m.SetFloat("_WindMaxHeight", 12f);
            m.SetFloat("_WindSpeed", 1.0f);
            m.SetFloat("_WindFlutter", 0.025f);
        }
        windMats[(src, grass)] = m;
        return m;
    }
}
