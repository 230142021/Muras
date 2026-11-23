using UnityEngine;

/// <summary>
/// Птица на дереве: время от времени поёт одну из песен (3D-звук, слышно рядом с деревом).
/// Если поставить flyAroundPlayer — это беркут в небе: кричит редко, издалека, каждый раз с новой стороны.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class BirdSinger : MonoBehaviour
{
    public AudioClip[] songs;
    public Vector2 pause = new Vector2(5f, 16f);
    public Vector2 pitch = new Vector2(0.92f, 1.12f);
    [Range(0f, 1f)] public float volume = 0.6f;
    [Tooltip("Не петь, если игрок дальше (экономия)")]
    public float hearDistance = 45f;

    [Tooltip("Поёт только ночью (сова). Остальные птицы ночью молчат.")]
    public bool nightOnly;

    [Header("Беркут в небе")]
    public bool flyAroundPlayer;
    public float skyHeight = 45f;
    public float skyRadius = 60f;

    private AudioSource source;
    private Transform player;
    private float next;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        next = Time.time + Random.Range(1f, pause.y);
    }

    void Start()
    {
        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null) player = pm.transform;
    }

    void Update()
    {
        if (Time.time < next || songs == null || songs.Length == 0) return;
        next = Time.time + Random.Range(pause.x, pause.y);
        bool night = DayNightCycle.IsNight;
        if (night != nightOnly) return;   // дневные птицы молчат ночью, сова — днём

        if (flyAroundPlayer && player != null)
        {
            Vector2 d = Random.insideUnitCircle.normalized * skyRadius;
            transform.position = player.position + new Vector3(d.x, skyHeight, d.y);
        }
        else if (player != null && Vector3.Distance(player.position, transform.position) > hearDistance)
            return;

        source.pitch = Random.Range(pitch.x, pitch.y);
        source.PlayOneShot(songs[Random.Range(0, songs.Length)], volume);
    }
}
