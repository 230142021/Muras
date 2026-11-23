using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Время суток в игре: утро → полдень → вечер → ночь. Двигает солнце и луну, меняет цвет света,
/// небо, окружающий свет и туман, проявляет звёзды ночью. Часы в углу экрана.
/// Удерживай T, чтобы ускорить время (удобно показывать на защите).
/// </summary>
[DefaultExecutionOrder(-50)]
public class DayNightCycle : MonoBehaviour
{
    [Header("Время")]
    [Range(0f, 24f)] public float hour = 9f;
    [Tooltip("Сколько реальных минут длятся игровые сутки")]
    public float dayLengthMinutes = 16f;
    public bool running = true;
    public KeyCode fastForwardKey = KeyCode.T;
    public float fastForwardMultiplier = 60f;

    [Header("Свет")]
    public Light sun;
    public Light moon;
    public float sunMaxIntensity = 1.55f;
    public float moonIntensity = 0.42f;
    public float sunYaw = -35f;

    [Header("Небо и окружение")]
    public Material skybox;
    public Gradient sunColor;
    public Gradient skyAmbient;
    public Gradient equatorAmbient;
    public Gradient groundAmbient;
    public Gradient fogColor;
    public AnimationCurve skyExposure;

    [Header("Звёзды и часы")]
    public Renderer stars;

    [Tooltip("Версия цветовой схемы. Если меньше текущей — при запуске подставляются новые цвета вечера/ночи.")]
    public int colorVersion;
    public const int CurrentColorVersion = 2;
    public TMP_Text clockText;

    public static DayNightCycle Instance { get; private set; }
    /// <summary>Текущий час 0..24.</summary>
    public static float Hour => Instance != null ? Instance.hour : 12f;
    /// <summary>0 днём → 1 ночью, с плавными сумерками.</summary>
    public static float NightFactor { get; private set; }
    public static bool IsNight => NightFactor > 0.5f;

    /// <summary>Погода (WeatherSystem): 1 — ясно, меньше — тусклее свет; WeatherGrey — насколько серым стал туман.</summary>
    public static float WeatherLight = 1f;
    public static float WeatherGrey = 0f;
    public static event Action<bool> NightChanged;

    private bool wasNight;
    private MaterialPropertyBlock starBlock;

    void Awake()
    {
        Instance = this;
        if (sunColor == null || sunColor.colorKeys.Length < 2) SetDefaults();
        starBlock = new MaterialPropertyBlock();
        if (colorVersion < CurrentColorVersion) { SetDefaults(); colorVersion = CurrentColorVersion; }
        if (skybox != null)
        {
            skybox = new Material(skybox);   // копия, чтобы не менять файл материала
            RenderSettings.skybox = skybox;
        }
        Apply();
        wasNight = IsNight;
    }

    void OnDestroy()
    {
        if (Instance == this) { Instance = null; NightFactor = 0f; }
    }

    void Update()
    {
        if (running && !PauseMenu.IsPaused)
        {
            float speed = 24f / Mathf.Max(0.1f, dayLengthMinutes * 60f);
            if (Input.GetKey(fastForwardKey)) speed *= fastForwardMultiplier;
            hour = Mathf.Repeat(hour + Time.deltaTime * speed, 24f);
        }
        Apply();

        if (IsNight != wasNight)
        {
            wasNight = IsNight;
            NightChanged?.Invoke(wasNight);
        }
    }

    void Apply()
    {
        float t = hour / 24f;

        // Ночь: плавно 19:00–21:00 и 5:00–6:30
        float n;
        if (hour >= 21f || hour < 5f) n = 1f;
        else if (hour >= 19f) n = Mathf.SmoothStep(0f, 1f, (hour - 19f) / 2f);
        else if (hour < 6.5f) n = Mathf.SmoothStep(1f, 0f, (hour - 5f) / 1.5f);
        else n = 0f;
        NightFactor = n;

        // Солнце: восход 6:00, закат 18:30, в зените ~65°
        float dayT = Mathf.InverseLerp(6f, 18.5f, hour);
        float elev = Mathf.Sin(dayT * Mathf.PI) * 65f;
        bool sunUp = hour > 5.6f && hour < 18.9f;
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(Mathf.Max(elev, -10f), sunYaw - 90f + dayT * 180f, 0f);
            // мягкий закат: свет и тени слабеют плавно, низкое солнце не рисует резких полос
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elev / 22f));
            sun.intensity = sunMaxIntensity * k * WeatherLight;
            sun.color = sunColor.Evaluate(t);
            sun.shadowStrength = Mathf.Lerp(0.35f, 0.9f, Mathf.Clamp01(elev / 25f));
            sun.enabled = sunUp && elev > 0.5f && sun.intensity > 0.02f;
        }
        if (moon != null)
        {
            float nightT = Mathf.Repeat(hour - 18.5f, 24f) / 11.5f;   // 18:30 → 6:00
            float mel = Mathf.Sin(Mathf.Clamp01(nightT) * Mathf.PI) * 55f + 8f;
            moon.transform.rotation = Quaternion.Euler(mel, sunYaw + 90f - nightT * 180f, 0f);
            // луна появляется заранее, чтобы между закатом и ночью не было «провала» света
            float dusk = hour >= 12f ? Mathf.InverseLerp(18.5f, 20f, hour) : Mathf.InverseLerp(6.4f, 5f, hour);
            float m = Mathf.SmoothStep(0f, 1f, Mathf.Max(dusk, n));
            moon.intensity = moonIntensity * m * Mathf.Lerp(1f, WeatherLight, 0.6f);
            moon.enabled = m > 0.03f;
        }
        RenderSettings.sun = (sun != null && sun.enabled) ? sun : moon;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        float wl = Mathf.Lerp(1f, WeatherLight, 0.7f);
        RenderSettings.ambientSkyColor = skyAmbient.Evaluate(t) * wl;
        RenderSettings.ambientEquatorColor = equatorAmbient.Evaluate(t) * wl;
        RenderSettings.ambientGroundColor = groundAmbient.Evaluate(t) * wl;
        Color fog = fogColor.Evaluate(t);
        float g = fog.grayscale;
        RenderSettings.fogColor = Color.Lerp(fog, new Color(g, g, g) * 0.85f, WeatherGrey);

        if (skybox != null)
        {
            skybox.SetFloat("_Exposure", skyExposure.Evaluate(hour));
            float sunset = Mathf.Max(Mathf.Exp(-Mathf.Pow((hour - 18.6f) / 1.1f, 2f)), Mathf.Exp(-Mathf.Pow((hour - 6.2f) / 1.0f, 2f)));
            skybox.SetFloat("_AtmosphereThickness", Mathf.Lerp(0.72f, 1.2f, sunset));
            skybox.SetFloat("_SunSize", Mathf.Lerp(0.035f, 0.05f, n));   // ночью «солнце» на небе — это луна
        }

        if (stars != null)
        {
            if (starBlock == null) starBlock = new MaterialPropertyBlock();   // после перекомпиляции в Play Mode
            stars.GetPropertyBlock(starBlock);
            starBlock.SetFloat("_Alpha", n);
            stars.SetPropertyBlock(starBlock);
            stars.enabled = n > 0.01f;
        }

        if (clockText != null)
        {
            int h = Mathf.FloorToInt(hour), m = Mathf.FloorToInt((hour - h) * 60f);
            clockText.text = $"<b>{h:00}:{m:00}</b>  <color=#C9BFA6>{PhaseName()}</color>";
        }
    }

    public static string PhaseName()
    {
        float h = Hour;
        if (h >= 5f && h < 11f) return Loc.T("Таң · Утро", "Таң · Morning");
        if (h >= 11f && h < 16f) return Loc.T("Түш · День", "Түш · Noon");
        if (h >= 16f && h < 20.5f) return Loc.T("Кеч · Вечер", "Кеч · Evening");
        return Loc.T("Түн · Ночь", "Түн · Night");
    }

    // ---------------------------------------------------------------- значения по умолчанию

    static Gradient G(params (float t, Color c)[] keys)
    {
        var g = new Gradient();
        var ck = new GradientColorKey[keys.Length];
        for (int i = 0; i < keys.Length; i++) ck[i] = new GradientColorKey(keys[i].c, keys[i].t / 24f);
        g.SetKeys(ck, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    static Color C(float r, float g, float b) => new Color(r, g, b);

    [ContextMenu("Reset colors to defaults")]
    public void SetDefaults()
    {
        // Не больше 8 ключей на градиент (ограничение Unity). Вечер — тёплое золото, затем спокойные синие сумерки.
        sunColor = G((0, C(0.62f, 0.70f, 1f)), (6f, C(1f, 0.66f, 0.45f)), (8f, C(1f, 0.90f, 0.78f)), (12f, C(1f, 0.99f, 0.96f)),
                     (16.5f, C(1f, 0.94f, 0.84f)), (18f, C(1f, 0.76f, 0.52f)), (19f, C(1f, 0.62f, 0.44f)), (24f, C(0.62f, 0.70f, 1f)));
        skyAmbient = G((0, C(0.13f, 0.16f, 0.28f)), (5f, C(0.16f, 0.18f, 0.30f)), (7f, C(0.52f, 0.54f, 0.62f)), (11f, C(0.61f, 0.72f, 0.87f)),
                       (17f, C(0.60f, 0.68f, 0.82f)), (18.8f, C(0.56f, 0.52f, 0.58f)), (20.3f, C(0.22f, 0.24f, 0.38f)), (24f, C(0.13f, 0.16f, 0.28f)));
        equatorAmbient = G((0, C(0.09f, 0.11f, 0.19f)), (5f, C(0.11f, 0.12f, 0.21f)), (7f, C(0.50f, 0.46f, 0.44f)), (11f, C(0.55f, 0.61f, 0.57f)),
                           (17f, C(0.55f, 0.57f, 0.52f)), (18.8f, C(0.56f, 0.44f, 0.38f)), (20.3f, C(0.16f, 0.16f, 0.26f)), (24f, C(0.09f, 0.11f, 0.19f)));
        groundAmbient = G((0, C(0.05f, 0.05f, 0.08f)), (6.5f, C(0.24f, 0.21f, 0.19f)), (12f, C(0.36f, 0.34f, 0.29f)), (18.6f, C(0.30f, 0.24f, 0.20f)), (20.5f, C(0.07f, 0.07f, 0.10f)), (24f, C(0.05f, 0.05f, 0.08f)));
        fogColor = G((0, C(0.08f, 0.10f, 0.18f)), (5f, C(0.12f, 0.14f, 0.24f)), (6.8f, C(0.80f, 0.70f, 0.66f)), (9.5f, C(0.73f, 0.82f, 0.93f)),
                     (17f, C(0.75f, 0.80f, 0.88f)), (18.7f, C(0.86f, 0.68f, 0.56f)), (20.2f, C(0.26f, 0.26f, 0.38f)), (24f, C(0.08f, 0.10f, 0.18f)));
        skyExposure = new AnimationCurve(new Keyframe(0f, 0.12f), new Keyframe(5f, 0.15f), new Keyframe(6.5f, 0.85f), new Keyframe(9f, 1.2f),
                                         new Keyframe(17f, 1.2f), new Keyframe(18.8f, 0.95f), new Keyframe(20.3f, 0.3f), new Keyframe(24f, 0.12f));
        for (int i = 0; i < skyExposure.length; i++) skyExposure.SmoothTangents(i, 0f);
        colorVersion = CurrentColorVersion;
    }
}
