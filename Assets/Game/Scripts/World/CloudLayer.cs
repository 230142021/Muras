using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Облака над долиной: low-poly облака медленно плывут по ветру и по кругу возвращаются.
/// Цвет зависит от времени суток (белые днём, розово-золотые на закате, тёмно-синие ночью)
/// и погоды (в дождь — серые и темнее).
/// </summary>
public class CloudLayer : MonoBehaviour
{
    public GameObject[] cloudModels;
    public Material material;
    public int count = 26;
    public Vector2 radius = new Vector2(60f, 240f);
    public Vector2 height = new Vector2(60f, 95f);
    public Vector2 scale = new Vector2(1.4f, 2.8f);
    public Vector3 wind = new Vector3(1.2f, 0f, 0.5f);   // м/с
    public Transform center;

    private readonly List<Transform> clouds = new List<Transform>();
    private Material runtime;
    private Vector3 c;

    void Start()
    {
        c = center != null ? center.position : transform.position;
        if (material != null) runtime = new Material(material);
        var rng = new System.Random(7);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        for (int i = 0; i < count && cloudModels != null && cloudModels.Length > 0; i++)
        {
            GameObject m = cloudModels[i % cloudModels.Length];
            if (m == null) continue;
            var go = Instantiate(m, transform);
            go.name = "Cloud_" + i;
            float a = R(0f, Mathf.PI * 2f), r = Mathf.Sqrt(R(0f, 1f)) * (radius.y - radius.x) + radius.x;
            go.transform.position = c + new Vector3(Mathf.Cos(a) * r, R(height.x, height.y), Mathf.Sin(a) * r);
            go.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
            float s = R(scale.x, scale.y);
            go.transform.localScale = new Vector3(s, s * R(0.8f, 1.1f), s);
            foreach (Renderer rd in go.GetComponentsInChildren<Renderer>())
            {
                if (runtime != null) rd.sharedMaterial = runtime;
                rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rd.receiveShadows = false;
            }
            foreach (Collider col in go.GetComponentsInChildren<Collider>()) Destroy(col);
            clouds.Add(go.transform);
        }
    }

    void Update()
    {
        float lim = radius.y;
        Vector3 step = wind * Time.deltaTime;
        foreach (Transform t in clouds)
        {
            Vector3 p = t.position + step;
            Vector3 d = p - c; d.y = 0f;
            if (d.magnitude > lim) p -= new Vector3(d.x, 0f, d.z) * 1.95f;   // улетело за край — появляется с другой стороны
            t.position = p;
        }

        if (runtime == null) return;
        float h = DayNightCycle.Hour, n = DayNightCycle.NightFactor;
        float sunset = Mathf.Max(Mathf.Exp(-Mathf.Pow((h - 18.6f) / 1.0f, 2f)), Mathf.Exp(-Mathf.Pow((h - 6.2f) / 0.9f, 2f)));
        Color day = new Color(0.97f, 0.97f, 1f), warm = new Color(1f, 0.72f, 0.58f), night = new Color(0.16f, 0.18f, 0.28f);
        Color col = Color.Lerp(Color.Lerp(day, warm, sunset), night, n);
        float grey = DayNightCycle.WeatherGrey;
        col = Color.Lerp(col, new Color(0.45f, 0.47f, 0.52f) * (1f - n * 0.6f), grey * 0.85f);
        if (runtime.HasProperty("_BaseColor")) runtime.SetColor("_BaseColor", col);
        if (runtime.HasProperty("_EmissionColor")) runtime.SetColor("_EmissionColor", col * Mathf.Lerp(0.55f, 0.12f, n));
    }
}
