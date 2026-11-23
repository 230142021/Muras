using System.Collections;
using UnityEngine;

/// <summary>
/// Зацикленный фоновый звук (фон села, журчание реки).
/// Плавно появляется, может начинаться со случайного места (чтобы несколько источников не звучали одинаково)
/// и приглушается, пока играет мелодия выполненного квеста.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AmbientLoop : MonoBehaviour
{
    [Range(0f, 1f)] public float volume = 0.35f;
    public enum TimeOfDay { Always, DayOnly, NightOnly }
    [Tooltip("Звучит всегда, только днём (кузнечики) или только ночью (сверчки)")]
    public TimeOfDay playAt = TimeOfDay.Always;
    public float fadeInTime = 4f;
    public bool randomStart = true;

    [Header("Приглушение при выполнении квеста")]
    public bool duckOnQuestComplete = true;
    [Range(0f, 1f)] public float duckLevel = 0.25f;
    public float duckTime = 4.5f;

    private AudioSource source;
    private float duck = 1f;
    private float fade;
    private Coroutine duckRoutine;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
    }

    void OnEnable()
    {
        if (duckOnQuestComplete) GameManager.QuestCompleted += OnQuestCompleted;
    }

    void OnDisable()
    {
        GameManager.QuestCompleted -= OnQuestCompleted;
    }

    void Start()
    {
        if (source.clip == null) return;
        if (randomStart) source.time = Random.Range(0f, source.clip.length * 0.95f);
        source.volume = 0f;
        source.Play();
    }

    void Update()
    {
        fade = fadeInTime > 0f ? Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime / fadeInTime) : 1f;
        float n = DayNightCycle.NightFactor;
        float timeK = playAt == TimeOfDay.DayOnly ? 1f - n : playAt == TimeOfDay.NightOnly ? n : 1f;
        source.volume = volume * fade * duck * timeK;
    }

    void OnQuestCompleted(string id, int xp)
    {
        if (duckRoutine != null) StopCoroutine(duckRoutine);
        duckRoutine = StartCoroutine(Duck());
    }

    IEnumerator Duck()
    {
        for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime) { duck = Mathf.Lerp(1f, duckLevel, t / 0.4f); yield return null; }
        duck = duckLevel;
        yield return new WaitForSecondsRealtime(duckTime);
        for (float t = 0f; t < 2f; t += Time.unscaledDeltaTime) { duck = Mathf.Lerp(duckLevel, 1f, t / 2f); yield return null; }
        duck = 1f;
        duckRoutine = null;
    }
}
