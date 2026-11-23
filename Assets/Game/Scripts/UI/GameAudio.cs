using System.Collections;
using UnityEngine;

/// <summary>
/// Звуки игры: выполнение квеста, правильный/неправильный ответ, подбор предмета
/// и «болтовня» NPC (короткие слоги разной высоты — у каждого персонажа свой голос).
/// </summary>
public class GameAudio : MonoBehaviour
{
    public static GameAudio Instance { get; private set; }

    [Header("Звуки")]
    public AudioClip questComplete;
    public AudioClip correct;
    public AudioClip wrong;
    public AudioClip pickup;
    public AudioClip interact;
    public AudioClip[] voiceBlips;

    [Header("Громкость")]
    [Range(0f, 1f)] public float sfxVolume = 0.8f;
    [Range(0f, 1f)] public float voiceVolume = 0.45f;

    private AudioSource sfx;
    private AudioSource voice;
    private Coroutine babble;

    private void Awake()
    {
        Instance = this;
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;
        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void QuestComplete() => PlaySfx(i => i.questComplete);
    public static void Correct() => PlaySfx(i => i.correct);
    public static void Wrong() => PlaySfx(i => i.wrong);
    public static void Pickup() => PlaySfx(i => i.pickup);
    public static void Interact() => PlaySfx(i => i.interact);

    private static void PlaySfx(System.Func<GameAudio, AudioClip> clip)
    {
        if (Instance == null) return;
        AudioClip c = clip(Instance);
        if (c != null) Instance.sfx.PlayOneShot(c, Instance.sfxVolume);
    }

    /// <summary>Болтовня персонажа: число слогов зависит от длины фразы, pitch — высота голоса.</summary>
    public static void Babble(string text, float pitch)
    {
        if (Instance == null || Instance.voiceBlips == null || Instance.voiceBlips.Length == 0 || string.IsNullOrEmpty(text))
            return;
        StopBabble();
        int letters = 0;
        bool tag = false;
        foreach (char ch in text)
        {
            if (ch == '<') tag = true;
            else if (ch == '>') tag = false;
            else if (!tag && char.IsLetter(ch)) letters++;
        }
        int syllables = Mathf.Clamp(letters / 3, 3, 45);
        Instance.babble = Instance.StartCoroutine(Instance.BabbleRoutine(syllables, pitch));
    }

    public static void StopBabble()
    {
        if (Instance == null || Instance.babble == null) return;
        Instance.StopCoroutine(Instance.babble);
        Instance.babble = null;
    }

    private IEnumerator BabbleRoutine(int syllables, float pitch)
    {
        for (int i = 0; i < syllables; i++)
        {
            voice.pitch = pitch * Random.Range(0.88f, 1.14f);
            voice.PlayOneShot(voiceBlips[Random.Range(0, voiceBlips.Length)], voiceVolume);
            float pause = Random.Range(0.075f, 0.11f);
            if (Random.value < 0.12f) pause += 0.12f;   // паузы между «словами»
            yield return new WaitForSeconds(pause);
        }
        babble = null;
    }
}
