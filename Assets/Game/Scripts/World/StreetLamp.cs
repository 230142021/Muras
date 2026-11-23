using UnityEngine;

/// <summary>
/// Уличный фонарь: зажигается на закате и гаснет на рассвете (плавно, с лёгким мерцанием).
/// Свет включён только у фонарей рядом с камерой — так деревня освещена, а игра не тормозит.
/// </summary>
public class StreetLamp : MonoBehaviour
{
    public Light lamp;
    public Renderer glow;
    public float maxIntensity = 4f;
    public float cullDistance = 55f;
    public static float GlobalCull = 55f;   // меняется настройкой графики
    public Color color = new Color(1f, 0.78f, 0.48f);

    private float level, seed, nextCheck;
    private bool near = true;
    private MaterialPropertyBlock mpb;

    void Start()
    {
        seed = Random.Range(0f, 100f);
        mpb = new MaterialPropertyBlock();
        level = Target();
        Apply();
    }

    float Target()
    {
        float h = DayNightCycle.Hour;
        // вечером включаются чуть раньше, чем гаснет солнце; утром гаснут после восхода
        float evening = Mathf.InverseLerp(18.2f, 18.9f, h + Mathf.Repeat(seed, 1f) * 0.3f);
        float morning = Mathf.InverseLerp(6.6f, 6.0f, h);
        return h > 12f ? evening : morning;
    }

    void Update()
    {
        if (Time.time > nextCheck)
        {
            nextCheck = Time.time + 0.5f + Mathf.Repeat(seed, 0.3f);
            Camera cam = Camera.main;
            near = cam == null || (cam.transform.position - transform.position).sqrMagnitude < Mathf.Min(cullDistance, GlobalCull) * Mathf.Min(cullDistance, GlobalCull);
        }
        level = Target();   // Target() уже плавный: растёт за ~40 игровых минут
        Apply();
    }

    void Apply()
    {
        float flicker = 1f + Mathf.Sin(Time.time * 7f + seed) * 0.03f + Mathf.Sin(Time.time * 17f + seed * 2f) * 0.02f;
        if (lamp != null)
        {
            lamp.intensity = maxIntensity * level * flicker;
            lamp.enabled = near && level > 0.02f;
        }
        if (glow != null)
        {
            glow.GetPropertyBlock(mpb);
            Color off = new Color(0.35f, 0.32f, 0.26f);
            mpb.SetColor("_BaseColor", Color.Lerp(off, color, level));
            mpb.SetColor("_EmissionColor", color * (level * 3.2f * flicker));
            glow.SetPropertyBlock(mpb);
        }
    }
}
