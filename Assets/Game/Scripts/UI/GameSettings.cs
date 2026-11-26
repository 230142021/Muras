using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Настройки игры (громкость, чувствительность мыши, полный экран, качество графики).
/// Хранятся в PlayerPrefs и применяются автоматически при каждой загрузке сцены.
/// </summary>
public static class GameSettings
{
    private const string VolumeKey = "MURAS_Volume";
    private const string SensKey = "MURAS_MouseSens";
    private const string FullscreenKey = "MURAS_Fullscreen";
    private const string QualityKey = "MURAS_Quality";

    public const float MinSensitivity = 0.5f;
    public const float MaxSensitivity = 8f;

    public static float Volume
    {
        get => PlayerPrefs.GetFloat(VolumeKey, 0.8f);
        set
        {
            PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
            AudioListener.volume = Mathf.Clamp01(value);
        }
    }

    public static float MouseSensitivity
    {
        get => PlayerPrefs.GetFloat(SensKey, 3f);
        set
        {
            PlayerPrefs.SetFloat(SensKey, Mathf.Clamp(value, MinSensitivity, MaxSensitivity));
            ApplyCamera();
        }
    }

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        set
        {
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
            Screen.fullScreen = value;
        }
    }

    // ---- Графика: 0 = Низкое, 1 = Среднее, 2 = Высокое (свои пресеты поверх URP) ----
    public const int QualityLevels = 3;
    public const int DefaultQuality = 1;

    public static int Quality
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(QualityKey + "_v2", DefaultQuality), 0, QualityLevels - 1);
        set
        {
            PlayerPrefs.SetInt(QualityKey + "_v2", Mathf.Clamp(value, 0, QualityLevels - 1));
            ApplyGraphics();
        }
    }

    public static string QualityName => Quality switch
    {
        0 => Loc.T("Низкое", "Low"),
        1 => Loc.T("Среднее", "Medium"),
        _ => Loc.T("Высокое", "High"),
    };

    public static int TargetFps => Quality == 0 ? 30 : 60;

    // исходные значения URP-ассета: в редакторе ассет — файл проекта, поэтому при выходе возвращаем как было
    private static bool saved;
    private static float baseScale, baseShadow; private static int baseMsaa;

    public static void ApplyGraphics()
    {
        int q = Quality;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFps;
        QualitySettings.lodBias = q == 0 ? 0.6f : q == 1 ? 1f : 1.6f;
        StreetLamp.GlobalCull = q == 0 ? 25f : q == 1 ? 40f : 60f;

        var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        if (urp == null) return;
        if (!saved)
        {
            saved = true;
            baseScale = urp.renderScale; baseShadow = urp.shadowDistance; baseMsaa = urp.msaaSampleCount;
            Application.quitting += RestoreAsset;
        }
        urp.renderScale = q == 0 ? 0.7f : q == 1 ? 0.85f : 1f;
        urp.shadowDistance = q == 0 ? 0f : q == 1 ? Mathf.Min(baseShadow, 45f) : Mathf.Max(baseShadow, 80f);
        urp.msaaSampleCount = q == 2 ? 4 : 1;
        GraphicsPolish.ApplyQuality();      // туман, тонемаппинг, свечение, сглаживание, ветер
    }

    private static void RestoreAsset()
    {
        var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        if (urp == null || !saved) return;
        urp.renderScale = baseScale; urp.shadowDistance = baseShadow; urp.msaaSampleCount = baseMsaa;
    }

    public static void Save() => PlayerPrefs.Save();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        AudioListener.volume = Volume;
        ApplyGraphics();
        if (PlayerPrefs.HasKey(FullscreenKey)) Screen.fullScreen = Fullscreen;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AudioListener.volume = Volume;
        ApplyCamera();
    }

    public static void ApplyCamera()
    {
        if (!PlayerPrefs.HasKey(SensKey)) return;   // пока игрок не трогал настройку — значение из сцены
        foreach (CameraFollow cam in Object.FindObjectsByType<CameraFollow>(FindObjectsSortMode.None))
            cam.sensitivity = MouseSensitivity;
    }
}
