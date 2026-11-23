using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Шаги персонажа. Звук зависит от поверхности: трава (по умолчанию), тропинка (земля/гравий),
/// камень (плитки, камни), дерево (настилы, скамейки, столы). Есть звук приземления после прыжка.
/// Тропинки определяются по названиям объектов (Road, Meadow_Path, Floor_Tile) — коллайдеры им не нужны.
/// </summary>
[RequireComponent(typeof(PlayerMovement))]
public class PlayerFootsteps : MonoBehaviour
{
    public AudioClip[] grass;
    public AudioClip[] dirt;
    public AudioClip[] stone;
    public AudioClip[] wood;
    public AudioClip land;

    [Range(0f, 1f)] public float volume = 0.42f;
    [Tooltip("Длина шага при ходьбе и беге (м)")]
    public float walkStride = 0.72f;
    public float runStride = 1.35f;

    private enum Surface { Grass, Dirt, Stone, Wood }

    private PlayerMovement movement;
    private AudioSource source;
    private float distance;
    private bool wasGrounded = true;
    private float airTime;
    private int lastIndex = -1;
    private readonly List<(Bounds b, Surface s)> zones = new List<(Bounds, Surface)>();

    private static readonly string[] DirtNames = { "Road", "Meadow_Path", "Path" };
    private static readonly string[] StoneNames = { "Floor_Tile", "Pebbles", "Rock", "Stone" };
    private static readonly string[] TileNames = { "Floor_Tile" };
    private static readonly string[] WoodNames = { "Wood", "Plank", "Deck", "Platform", "Bench", "Table", "Log", "Floor", "Shyrdak", "Carpet" };

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0.6f;
        source.minDistance = 2f;
        source.maxDistance = 20f;
    }

    void Start()
    {
        // Запоминаем, где лежат тропинки и плитки
        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            string n = r.gameObject.name;
            string root = r.transform.parent != null ? r.transform.parent.name : n;
            if (Has(n, TileNames) || Has(root, TileNames)) { if (r.bounds.size.y < 1.2f) zones.Add((Grow(r.bounds), Surface.Stone)); }
            else if ((Has(n, DirtNames) || Has(root, DirtNames)) && !n.StartsWith("Trail")) zones.Add((Grow(r.bounds), Surface.Dirt));
        }
    }

    static Bounds Grow(Bounds b) { b.Expand(new Vector3(0.1f, 1.5f, 0.1f)); return b; }

    static bool Has(string n, string[] keys)
    {
        foreach (string k in keys) if (n.IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    void Update()
    {
        bool grounded = movement.IsGrounded;
        if (!grounded) airTime += Time.deltaTime;

        if (grounded && !wasGrounded && airTime > 0.25f)
        {
            Play(land != null ? new[] { land } : Clips(Detect()), 1.1f);
            distance = 0f;
        }
        if (grounded) airTime = 0f;
        wasGrounded = grounded;

        if (!grounded || movement.CurrentSpeed < 0.3f) { distance = Mathf.Min(distance, 0.5f); return; }

        distance += movement.CurrentSpeed * Time.deltaTime;
        float stride = movement.IsRunning ? runStride : walkStride;
        if (distance >= stride)
        {
            distance -= stride;
            Play(Clips(Detect()), movement.IsRunning ? 1f : 0.8f);
        }
    }

    Surface Detect()
    {
        Vector3 p = transform.position;
        if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 1.5f, ~0, QueryTriggerInteraction.Ignore))
        {
            string n = hit.collider.name;
            if (Has(n, StoneNames)) return Surface.Stone;
            if (Has(n, WoodNames)) return Surface.Wood;
            if (Has(n, DirtNames)) return Surface.Dirt;
        }
        foreach (var z in zones)
            if (z.b.Contains(p)) return z.s;
        return Surface.Grass;
    }

    AudioClip[] Clips(Surface s)
    {
        AudioClip[] c = s switch { Surface.Dirt => dirt, Surface.Stone => stone, Surface.Wood => wood, _ => grass };
        return c != null && c.Length > 0 ? c : grass;
    }

    void Play(AudioClip[] clips, float gain)
    {
        if (clips == null || clips.Length == 0) return;
        int i = Random.Range(0, clips.Length);
        if (clips.Length > 1 && i == lastIndex) i = (i + 1) % clips.Length;
        lastIndex = i;
        source.pitch = Random.Range(0.94f, 1.06f);
        source.PlayOneShot(clips[i], volume * gain * Random.Range(0.85f, 1f));
    }
}
