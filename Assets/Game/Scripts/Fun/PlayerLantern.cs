using UnityEngine;

/// <summary>
/// Фонарь-чырак героя (предмет «Чырак» из капчыка): клавиша F включает тёплый луч перед героем
/// и мягкий свет вокруг, в руке светится огонёк. Когда стемнеет, игра один раз напомнит про F.
/// </summary>
public class PlayerLantern : MonoBehaviour
{
    public static PlayerLantern Instance { get; private set; }
    public static bool IsOn => Instance != null && Instance.on;

    public Material glowMaterial;
    public KeyCode key = KeyCode.F;
    public Color color = new Color(1f, 0.82f, 0.55f);

    private Light beam, aura;
    private Transform glow;
    private bool on;
    private bool hinted;
    private CharacterController cc;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        cc = GetComponent<CharacterController>();
        on = SaveData.Get("lantern_on") == 1 && Inventory.Count("lantern") > 0;
        Apply();
    }

    public static void Toggle()
    {
        if (Instance == null || Inventory.Count("lantern") == 0) return;
        Instance.on = !Instance.on;
        SaveData.Set("lantern_on", Instance.on ? 1 : 0);
        MurasUI.Play("reel_tick", 0.6f, Instance.on ? 1.2f : 0.9f);
        Instance.Apply();
        Inventory.Notify();
    }

    void Update()
    {
        if (Input.GetKeyDown(key) && !Modal.IsOpen && !PauseMenu.IsPaused && Inventory.Count("lantern") > 0) Toggle();

        if (!hinted && !on && DayNightCycle.IsNight && Inventory.Count("lantern") > 0)
        {
            hinted = true;
            MurasToast.Push(Loc.T("Стемнело", "It's getting dark"), Loc.T("Нажми <b>F</b>, чтобы включить чырак.", "Press <b>F</b> to light your lantern."), MurasUI.Gold, null);
        }
        if (on && aura != null)   // живой огонёк
        {
            float f = 1f + Mathf.Sin(Time.time * 9f) * 0.03f + Mathf.Sin(Time.time * 23f) * 0.02f;
            aura.intensity = 1.1f * f;
            beam.intensity = 3.2f * f;
        }
    }

    void Build()
    {
        float h = cc != null ? cc.height * 0.5f : 1f;
        var b = new GameObject("LanternBeam");
        b.transform.SetParent(transform, false);
        b.transform.localPosition = new Vector3(0.25f, h * 0.35f, 0.35f);
        b.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
        beam = b.AddComponent<Light>();
        beam.type = LightType.Spot; beam.spotAngle = 72f; beam.innerSpotAngle = 35f; beam.range = 22f;
        beam.color = color; beam.shadows = LightShadows.None;

        var a = new GameObject("LanternAura");
        a.transform.SetParent(transform, false);
        a.transform.localPosition = new Vector3(0.3f, h * 0.2f, 0.25f);
        aura = a.AddComponent<Light>();
        aura.type = LightType.Point; aura.range = 5f; aura.color = color; aura.shadows = LightShadows.None;

        if (glowMaterial != null)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(g.GetComponent<Collider>());
            g.name = "LanternGlow";
            g.transform.SetParent(a.transform, false);
            g.transform.localScale = Vector3.one * 0.18f / Mathf.Max(0.01f, transform.lossyScale.x);
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = glowMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glow = g.transform;
        }
    }

    void Apply()
    {
        if (on && beam == null) Build();
        if (beam != null) beam.enabled = on;
        if (aura != null) aura.enabled = on;
        if (glow != null) glow.gameObject.SetActive(on);
    }
}
