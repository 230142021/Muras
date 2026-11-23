using UnityEngine;

/// <summary>
/// Погода в горах: ясно, облачно, дождь, снег. Меняется сама каждые несколько минут.
/// Дождь и снег — частицы вокруг камеры (не проходят сквозь крыши), свет становится тусклее,
/// туман гуще, слышен шум дождя или ветра.
/// </summary>
public class WeatherSystem : MonoBehaviour
{
    public enum Kind { Clear, Cloudy, Rain, Snow }
    public static WeatherSystem Instance { get; private set; }
    public static Kind Current => Instance != null ? Instance.current : Kind.Clear;

    public Material rainMaterial, snowMaterial;
    public AudioClip rainLoop, windLoop;
    public Vector2 changeEverySeconds = new Vector2(100f, 200f);
    [Range(0f, 1f)] public float rainChance = 0.18f, snowChance = 0.07f, cloudyChance = 0.22f;

    private Kind current = Kind.Clear;
    private float nextChange, blend;   // blend 0..1 — сила текущей непогоды
    private ParticleSystem rain, snow;
    private AudioSource rainAudio, windAudio;
    private float baseFog = -1f;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) { Instance = null; DayNightCycle.WeatherLight = 1f; DayNightCycle.WeatherGrey = 0f; } }

    void Start()
    {
        rain = MakeParticles("Rain", rainMaterial, true);
        snow = MakeParticles("Snow", snowMaterial, false);
        rainAudio = Loop(rainLoop); windAudio = Loop(windLoop);
        nextChange = Time.time + Random.Range(60f, 120f);
        if (RenderSettings.fog) baseFog = RenderSettings.fogDensity;
    }

    AudioSource Loop(AudioClip c)
    {
        if (c == null) return null;
        var s = gameObject.AddComponent<AudioSource>();
        s.clip = c; s.loop = true; s.spatialBlend = 0f; s.volume = 0f; s.Play();
        return s;
    }

    public void Set(Kind k)
    {
        if (k == current) return;
        current = k; blend = 0f;
        nextChange = Time.time + Random.Range(changeEverySeconds.x, changeEverySeconds.y);
        if (k == Kind.Rain) { Vocab.Unlock("jamgyr"); Achievements.Give("weather"); }
        if (k == Kind.Snow) { Vocab.Unlock("kar"); Achievements.Give("weather"); }
    }

    void Update()
    {
        if (Time.time > nextChange)
        {
            float r = Random.value;
            Kind k = r < rainChance ? Kind.Rain : r < rainChance + snowChance ? Kind.Snow : r < rainChance + snowChance + cloudyChance ? Kind.Cloudy : Kind.Clear;
            if (k == current) k = Kind.Clear;
            Set(k);
        }
        blend = Mathf.MoveTowards(blend, 1f, Time.deltaTime / 6f);

        float wet = current == Kind.Rain ? blend : 0f, cold = current == Kind.Snow ? blend : 0f, cloud = current == Kind.Cloudy ? blend : 0f;
        float gloom = Mathf.Max(wet * 0.55f, cold * 0.4f, cloud * 0.3f);
        // плавно уходим от прошлой погоды
        DayNightCycle.WeatherLight = Mathf.MoveTowards(DayNightCycle.WeatherLight, 1f - gloom, Time.deltaTime * 0.15f);
        DayNightCycle.WeatherGrey = Mathf.MoveTowards(DayNightCycle.WeatherGrey, gloom, Time.deltaTime * 0.15f);
        if (baseFog > 0f) RenderSettings.fogDensity = baseFog * (1f + DayNightCycle.WeatherGrey * 1.8f);

        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 p = cam.transform.position;
            if (rain != null) rain.transform.position = p + Vector3.up * 14f;
            if (snow != null) snow.transform.position = p + Vector3.up * 10f;
        }
        Emit(rain, wet * 900f);
        Emit(snow, cold * 350f);
        if (rainAudio != null) rainAudio.volume = Mathf.MoveTowards(rainAudio.volume, wet * 0.55f, Time.deltaTime * 0.2f);
        if (windAudio != null) windAudio.volume = Mathf.MoveTowards(windAudio.volume, Mathf.Max(cold, cloud * 0.4f) * 0.4f, Time.deltaTime * 0.2f);
    }

    static void Emit(ParticleSystem ps, float rate)
    {
        if (ps == null) return;
        var e = ps.emission; e.rateOverTime = rate;
        if (rate > 1f && !ps.isPlaying) ps.Play();
    }

    ParticleSystem MakeParticles(string name, Material mat, bool isRain)
    {
        if (mat == null) return null;
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = isRain ? 1.4f : 9f;
        main.startSpeed = 0f;
        main.startSize = isRain ? 0.05f : new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.startColor = isRain ? new Color(0.8f, 0.85f, 0.95f, 0.55f) : new Color(1f, 1f, 1f, 0.9f);
        main.maxParticles = isRain ? 2500 : 3500;
        main.gravityModifier = 0f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(36f, 1f, 36f);
        var vel = ps.velocityOverLifetime;
        vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(isRain ? -0.6f : -0.6f, isRain ? -0.3f : 0.6f);
        vel.y = isRain ? new ParticleSystem.MinMaxCurve(-17f, -15f) : new ParticleSystem.MinMaxCurve(-1.6f, -1.0f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        if (!isRain)
        {
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.3f;
        }
        var col = ps.collision;
        col.enabled = true; col.type = ParticleSystemCollisionType.World; col.mode = ParticleSystemCollisionMode.Collision3D;
        col.quality = ParticleSystemCollisionQuality.Low; col.lifetimeLoss = 1f; col.bounce = 0f;
        var em = ps.emission; em.rateOverTime = 0f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        if (isRain) { r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.04f; r.lengthScale = 2.5f; }
        else r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        return ps;
    }
}
