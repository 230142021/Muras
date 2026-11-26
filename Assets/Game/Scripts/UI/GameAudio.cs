using System.Collections.Generic;
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

    // ================================================================== голоса NPC
    // Синтезированные слоги (согласная + гласная) пяти типов голоса: Resources/MURAS/Voice/<тип>/v_<гласная>_<n>.
    // Гласные берутся из самой реплики, поэтому «речь» повторяет ритм и звучание текста;
    // паузы — на пробелах и знаках препинания, вопрос звучит с подъёмом в конце.
    static readonly Dictionary<string, string> VoiceOf = new Dictionary<string, string>
    {
        { "Аксакал", "elder_m" }, { "Манасчы", "elder_m" }, { "Апа", "elder_f" },
        { "Эже", "female" }, { "Бала", "child" }, { "Кыз", "child" },
    };
    static readonly Dictionary<string, float> TypicalPitch = new Dictionary<string, float>
    {
        { "male", 0.9f }, { "elder_m", 0.76f }, { "female", 1.12f }, { "elder_f", 0.85f }, { "child", 1.5f },
    };
    private readonly Dictionary<string, Dictionary<char, AudioClip[]>> voices = new Dictionary<string, Dictionary<char, AudioClip[]>>();

    Dictionary<char, AudioClip[]> VoiceClips(string type)
    {
        if (voices.TryGetValue(type, out var d)) return d;
        d = new Dictionary<char, AudioClip[]>();
        var all = Resources.LoadAll<AudioClip>("MURAS/Voice/" + type);
        foreach (char v in "aouei y".Replace(" ", ""))
            d[v] = System.Array.FindAll(all, c => c.name.StartsWith("v_" + v + "_"));
        voices[type] = d;
        return d;
    }

    static char VowelClass(char ch)
    {
        switch (char.ToLowerInvariant(ch))
        {
            case 'а': case 'я': case 'a': return 'a';
            case 'о': case 'ө': case 'ё': case 'o': return 'o';
            case 'у': case 'ү': case 'ю': case 'u': return 'u';
            case 'е': case 'э': case 'e': return 'e';
            case 'и': case 'i': return 'i';
            case 'ы': case 'y': return 'y';
            default: return '\0';
        }
    }

    struct Syl { public char vowel; public float pauseAfter; public int sentence; }

    /// <summary>Речь персонажа. pitch — высота голоса из SpeechPanel, speakerKey — кто говорит (выбирает тип голоса).</summary>
    public static void Babble(string text, float pitch, string speakerKey = null)
    {
        if (Instance == null || string.IsNullOrEmpty(text)) return;
        StopBabble();
        string type = speakerKey != null && VoiceOf.TryGetValue(speakerKey, out string vt) ? vt : "male";
        var clips = Instance.VoiceClips(type);
        bool any = false;
        foreach (var kv in clips) if (kv.Value.Length > 0) any = true;
        if (!any)
        {
            // запасной вариант: старые «блипы»
            if (Instance.voiceBlips == null || Instance.voiceBlips.Length == 0) return;
            Instance.babble = Instance.StartCoroutine(Instance.BlipRoutine(Mathf.Clamp(text.Length / 3, 3, 45), pitch));
            return;
        }
        // разбор реплики на слоги и паузы
        var syl = new List<Syl>();
        var sentenceEnd = new List<char>();
        bool tag = false;
        foreach (char ch in text)
        {
            if (ch == '<') { tag = true; continue; }
            if (ch == '>') { tag = false; continue; }
            if (tag) continue;
            char v = VowelClass(ch);
            if (v != '\0') { syl.Add(new Syl { vowel = v, sentence = sentenceEnd.Count }); continue; }
            if (syl.Count == 0) continue;
            var last = syl[syl.Count - 1];
            float p = ch == ' ' ? 0.045f : (ch == ',' || ch == ';' || ch == ':' || ch == '—' || ch == '–') ? 0.17f
                    : (ch == '.' || ch == '!' || ch == '?' || ch == '…') ? 0.3f : 0f;
            if (p > last.pauseAfter) { last.pauseAfter = p; syl[syl.Count - 1] = last; }
            if ((ch == '.' || ch == '!' || ch == '?') && (sentenceEnd.Count == 0 || sentenceEnd.Count <= last.sentence)) sentenceEnd.Add(ch);
        }
        if (syl.Count == 0) return;
        if (syl.Count > 60) syl.RemoveRange(60, syl.Count - 60);     // длинные реплики не «бубнят» бесконечно
        float rel = Mathf.Clamp(pitch / (TypicalPitch.TryGetValue(type, out float tp) ? tp : 1f), 0.86f, 1.18f);
        Instance.babble = Instance.StartCoroutine(Instance.SpeakRoutine(syl, sentenceEnd, clips, rel));
    }

    public static void StopBabble()
    {
        if (Instance == null || Instance.babble == null) return;
        Instance.StopCoroutine(Instance.babble);
        Instance.babble = null;
    }

    private IEnumerator SpeakRoutine(List<Syl> syl, List<char> ends, Dictionary<char, AudioClip[]> clips, float rel)
    {
        int i = 0;
        while (i < syl.Count)
        {
            // границы текущего предложения — для интонации
            int sent = syl[i].sentence, start = i, end = i;
            while (end + 1 < syl.Count && syl[end + 1].sentence == sent) end++;
            char kind = sent < ends.Count ? ends[sent] : '.';
            int len = end - start + 1;
            for (int k = 0; k < len; k++, i++)
            {
                float pos = len > 1 ? k / (float)(len - 1) : 0f;
                float melody = Mathf.Lerp(1.05f, 0.95f, pos);                 // фраза плавно опускается
                if (kind == '?' && k >= len - 2) melody = 1.06f + 0.08f * (k - (len - 2));   // вопрос — подъём в конце
                if (kind == '!') melody += 0.04f;
                AudioClip[] set = clips.TryGetValue(syl[i].vowel, out var s) && s.Length > 0 ? s : clips['a'];
                if (set.Length == 0) foreach (var kv in clips) if (kv.Value.Length > 0) { set = kv.Value; break; }
                AudioClip c = set[Random.Range(0, set.Length)];
                voice.pitch = rel * melody * Random.Range(0.97f, 1.03f);
                voice.PlayOneShot(c, voiceVolume * (kind == '!' ? 1.12f : 1f) * Random.Range(0.85f, 1f));
                float wait = c.length / voice.pitch * 0.8f + syl[i].pauseAfter;
                yield return new WaitForSecondsRealtime(wait);
            }
        }
        babble = null;
    }

    private IEnumerator BlipRoutine(int syllables, float pitch)
    {
        for (int i = 0; i < syllables; i++)
        {
            voice.pitch = pitch * Random.Range(0.88f, 1.14f);
            voice.PlayOneShot(voiceBlips[Random.Range(0, voiceBlips.Length)], voiceVolume);
            yield return new WaitForSecondsRealtime(Random.Range(0.075f, 0.11f));
        }
        babble = null;
    }
}
