using UnityEngine;

/// <summary>
/// Жилой дом: вечером в окнах загорается тёплый свет и включается фонарь у крыльца,
/// утром гаснут. Каждый дом зажигается в своё время — как будто хозяева вернулись домой.
/// </summary>
public class HouseLights : MonoBehaviour
{
    public Light porchLight;
    public Light interiorLight;
    public float interiorDay = 0.55f;
    public float interiorNight = 1.8f;
    public Color windowColor = new Color(1f, 0.72f, 0.38f);
    public float windowGlow = 2.2f;
    public float lightIntensity = 1.6f;

    private Material[] windows;
    private float delay;
    private float current;

    void Start()
    {
        delay = Random.Range(-0.15f, 0.25f);
        var list = new System.Collections.Generic.List<Material>();
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            Material[] mats = r.sharedMaterials;
            bool has = false;
            foreach (Material m in mats) if (m != null && m.name.Contains("Window")) has = true;
            if (!has) continue;
            mats = r.materials;   // копии только у окон этого дома
            foreach (Material m in mats)
                if (m != null && m.name.Contains("Window"))
                {
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    list.Add(m);
                }
        }
        windows = list.ToArray();
        current = -1f;
    }

    void Update()
    {
        float target = Mathf.Clamp01((DayNightCycle.NightFactor - 0.25f - delay) * 2.5f);
        if (Mathf.Abs(target - current) < 0.002f) return;
        current = current < 0f ? target : Mathf.MoveTowards(current, target, Time.deltaTime * 0.8f);

        Color e = windowColor * (windowGlow * current);
        foreach (Material m in windows) m.SetColor("_EmissionColor", e);
        if (interiorLight != null) interiorLight.intensity = Mathf.Lerp(interiorDay, interiorNight, current);
        if (porchLight != null)
        {
            porchLight.intensity = lightIntensity * current;
            porchLight.enabled = current > 0.01f;
        }
    }
}
