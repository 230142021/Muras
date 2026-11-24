using UnityEngine;

/// <summary>Живой огонь костра: свет дрожит и мерцает, языки пламени чуть «дышат».</summary>
public class FireFlicker : MonoBehaviour
{
    public Light fireLight;
    public float baseIntensity = 3f;
    public float flicker = 0.9f;
    private float seed;
    private Vector3 lightHome;

    static Material flameMat, coreMat, emberMat;

    static Material Glow(Color c, float k)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        m.SetColor("_BaseColor", c * k);
        return m;
    }

    void Start()
    {
        seed = Random.value * 100f;
        // пламя светится само (без освещения), даже если в сцене материалы обычные
        if (flameMat == null) { flameMat = Glow(new Color(1f, 0.5f, 0.12f), 1.4f); coreMat = Glow(new Color(1f, 0.88f, 0.45f), 1.6f); emberMat = Glow(new Color(0.95f, 0.25f, 0.06f), 1.2f); }
        foreach (var r in GetComponentsInChildren<MeshRenderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string n = mats[i].name;
                if (n.Contains("FlameCore") || n.Contains("Fire_Core")) mats[i] = coreMat;
                else if (n.Contains("Flame")) mats[i] = flameMat;
                else if (n.Contains("Ember")) mats[i] = emberMat;
            }
            r.sharedMaterials = mats;
        }
        if (fireLight != null) { baseIntensity = fireLight.intensity; lightHome = fireLight.transform.localPosition; }
    }

    void Update()
    {
        float t = Time.time * 7f + seed;
        float n = Mathf.PerlinNoise(t, seed) * 0.7f + Mathf.PerlinNoise(t * 2.3f, seed + 5f) * 0.3f;
        // ночью костёр светит ярче, днём — скромнее
        float night = Mathf.Lerp(0.55f, 1.25f, DayNightCycle.NightFactor);
        if (fireLight != null)
        {
            fireLight.intensity = (baseIntensity + (n - 0.5f) * 2f * flicker) * night;
            fireLight.transform.localPosition = lightHome + new Vector3((Mathf.PerlinNoise(t, 1f) - 0.5f) * 0.12f, 0f, (Mathf.PerlinNoise(1f, t) - 0.5f) * 0.12f);
        }
        transform.localScale = new Vector3(1f, 0.94f + n * 0.12f, 1f);
    }
}
