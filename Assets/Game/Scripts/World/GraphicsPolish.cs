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
    Shader windShader;
    readonly Dictionary<(Material, bool), Material> windMats = new Dictionary<(Material, bool), Material>();
    float windTime;

    public static int WindRenderers { get; private set; }

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
        SceneManager.sceneLoaded += OnSceneLoaded;
        Setup(SceneManager.GetActiveScene());
    }

    void OnDestroy() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    void OnSceneLoaded(Scene s, LoadSceneMode m) => Setup(s);

    void Setup(Scene scene)
    {
        bloom = null; tonemap = null; vignette = null;
        SetupVolume();
        if (FindAnyObjectByType<DayNightCycle>() != null)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            ApplyWindMaterials(scene);
        }
        ApplyQuality();
    }

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
        if (!p.TryGet(out vignette)) vignette = p.Add<Vignette>(true);
        vignette.intensity.Override(0.16f);
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
        if (!on)
        {
            if (me.bloom != null) me.bloom.active = false;
            RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 70f; RenderSettings.fogEndDistance = 480f;
            Camera cam = Camera.main;
            if (cam != null) cam.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.None;
            Shader.SetGlobalFloat("_MurasWind", 0f);
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
        // ветер: направление медленно гуляет, в непогоду сильнее
        windTime += Time.deltaTime;
        float a = 0.6f + Mathf.Sin(windTime * 0.02f) * 0.5f;
        float strength = 1f + DayNightCycle.WeatherGrey * 1.2f;
        Shader.SetGlobalVector("_MurasWindDir", new Vector4(Mathf.Cos(a) * strength, Mathf.Sin(a) * strength, 0f, 0f));
    }

    // ------------------------------------------------------------------ материалы с ветром
    void ApplyWindMaterials(Scene scene)
    {
        WindRenderers = 0;
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
