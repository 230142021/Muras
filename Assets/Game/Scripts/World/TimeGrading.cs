using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Цветокоррекция по времени суток (пост-обработка Global Volume): днём — сочно, на закате — тёплое
/// мягкое золото без кислотной пересветки, в сумерках — спокойный синий, ночью — прохладный лунный
/// свет и чуть светлее, чтобы было видно дорогу. Плавно перетекает между фазами.
/// </summary>
public class TimeGrading : MonoBehaviour
{
    public Volume volume;

    struct Look { public float sat, contrast, exposure, temp, tint; public Look(float s, float c, float e, float t, float ti) { sat = s; contrast = c; exposure = e; temp = t; tint = ti; } }

    // час → «вид»
    static readonly (float h, Look l)[] Keys =
    {
        (0f,    new Look(-22f, 6f, 0.30f, -16f, 4f)),
        (4.8f,  new Look(-20f, 6f, 0.30f, -14f, 4f)),
        (6.5f,  new Look(8f, 7f, 0.10f, 7f, 0f)),
        (9f,    new Look(12f, 10f, 0.10f, -4f, 0f)),
        (16.5f, new Look(12f, 10f, 0.10f, -4f, 0f)),
        (18.3f, new Look(14f, 8f, 0.05f, 9f, 2f)),
        (19.4f, new Look(-2f, 5f, 0.15f, -8f, 4f)),
        (20.8f, new Look(-20f, 6f, 0.30f, -16f, 4f)),
        (24f,   new Look(-22f, 6f, 0.30f, -16f, 4f)),
    };

    private ColorAdjustments ca;
    private WhiteBalance wb;

    void Start()
    {
        if (volume == null) foreach (Volume v in FindObjectsByType<Volume>(FindObjectsSortMode.None)) if (v.isGlobal) { volume = v; break; }
        if (volume == null) { enabled = false; return; }
        VolumeProfile p = volume.profile;   // копия профиля только на время игры
        if (!p.TryGet(out ca)) ca = p.Add<ColorAdjustments>(true);
        if (!p.TryGet(out wb)) wb = p.Add<WhiteBalance>(true);
        ca.saturation.overrideState = ca.contrast.overrideState = ca.postExposure.overrideState = true;
        wb.temperature.overrideState = wb.tint.overrideState = true;
    }

    void Update()
    {
        float h = DayNightCycle.Hour;
        Look l = Keys[0].l;
        for (int i = 0; i < Keys.Length - 1; i++)
        {
            if (h < Keys[i].h || h > Keys[i + 1].h) continue;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Keys[i].h, Keys[i + 1].h, h));
            Look a = Keys[i].l, b = Keys[i + 1].l;
            l = new Look(Mathf.Lerp(a.sat, b.sat, t), Mathf.Lerp(a.contrast, b.contrast, t), Mathf.Lerp(a.exposure, b.exposure, t), Mathf.Lerp(a.temp, b.temp, t), Mathf.Lerp(a.tint, b.tint, t));
            break;
        }
        float grey = DayNightCycle.WeatherGrey;   // в непогоду цвета приглушённее
        bool fx = GraphicsPolish.Enhanced;        // поправки под тонемаппинг (см. GraphicsPolish)
        ca.saturation.value = l.sat - grey * 15f + (fx ? GraphicsPolish.SaturationAdd : 0f);
        ca.contrast.value = l.contrast + (fx ? GraphicsPolish.ContrastAdd : 0f);
        ca.postExposure.value = l.exposure + (fx ? GraphicsPolish.ExposureAdd : 0f);
        wb.temperature.value = l.temp;
        wb.tint.value = l.tint;
    }
}
