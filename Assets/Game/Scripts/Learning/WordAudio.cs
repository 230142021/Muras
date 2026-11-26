using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Озвучка слов словаря живым голосом. Записи лежат в Resources/MURAS/Words/&lt;id слова&gt; (wav/ogg/mp3),
/// их удобно сделать окном Tools → Diploma → Learning → Word Recorder (запись с микрофона прямо в Unity)
/// или импортом папки с файлами, записанными на телефон. Если записи нет, кнопка прослушивания не показывается.
/// </summary>
public static class WordAudio
{
    public const string Folder = "MURAS/Words/";
    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    static AudioSource source;

    public static AudioClip Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (!cache.TryGetValue(id, out AudioClip c)) { c = Resources.Load<AudioClip>(Folder + id); cache[id] = c; }
        return c;
    }

    public static bool Has(string id) => Get(id) != null;

    public static int RecordedCount { get { int n = 0; foreach (var w in Vocab.Words) if (Has(w.id)) n++; return n; } }

    public static void ClearCache() => cache.Clear();

    public static bool Play(string id, bool track = true)
    {
        AudioClip c = Get(id);
        if (c == null) return false;
        if (source == null)
        {
            var go = new GameObject("WordAudio");
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0f; source.ignoreListenerPause = true;
        }
        source.Stop();
        source.clip = c;
        source.volume = 1f;
        source.Play();
        if (track) MurasCloud.Track("word_heard", CloudJson.Object("id", id));
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { cache.Clear(); source = null; }
}

/// <summary>
/// «Проверь себя» в словаре: 5 уже открытых слов — выбери перевод. Чаще попадаются слова, в которых ошибался.
/// Ответы уходят в аналитику (word_review) — учитель видит слова, которые ученики забывают.
/// После ответа слово озвучивается, если есть запись.
/// </summary>
public static class WordReview
{
    public const int Count = 5;

    public static bool CanStart => Vocab.KnownCount >= 4;

    public static void Start()
    {
        var known = new List<Vocab.Word>();
        foreach (var w in Vocab.Words) if (Vocab.Known(w.id)) known.Add(w);
        if (known.Count < 4)
        {
            MurasToast.Push(Loc.T("Пока рано", "Too early"), Loc.T("Открой хотя бы 4 слова — и проверь себя.", "Unlock at least 4 words first."), MurasUI.Gold);
            return;
        }
        // вес слова: 1 + ошибки*2, недавно отвеченные верно — реже
        var pool = new List<Vocab.Word>(known);
        var picked = new List<Vocab.Word>();
        while (picked.Count < Mathf.Min(Count, known.Count) && pool.Count > 0)
        {
            float total = 0f;
            foreach (var w in pool) total += Weight(w);
            float r = Random.value * total;
            Vocab.Word choice = pool[pool.Count - 1];
            foreach (var w in pool) { r -= Weight(w); if (r <= 0f) { choice = w; break; } }
            picked.Add(choice);
            pool.Remove(choice);
        }

        var items = new List<QuizWindow.Item>();
        foreach (var w in picked)
        {
            var opts = new List<string> { Loc.T(w.ru, w.en) };
            var others = new List<Vocab.Word>(Vocab.Words);
            others.Remove(w);
            while (opts.Count < 4 && others.Count > 0)
            {
                var o = others[Random.Range(0, others.Count)];
                others.Remove(o);
                string t = Loc.T(o.ru, o.en);
                if (!opts.Contains(t)) opts.Add(t);
            }
            items.Add(new QuizWindow.Item
            {
                id = w.id, word = w.id,
                question = Loc.T("Что значит «<color=#E8B04A>" + w.ky + "</color>»?", "What does \"<color=#E8B04A>" + w.ky + "</color>\" mean?"),
                options = opts.ToArray(),
            });
        }

        QuizWindow.Show(new QuizWindow.Config
        {
            title = Loc.T("ӨЗҮҢДҮ ТЕКШЕР · Проверь себя", "ӨЗҮҢДҮ ТЕКШЕР · Check yourself"),
            items = items,
            showFeedback = true,
            showReview = true,
            activity = "review",
            onAnswer = (item, ok) =>
            {
                SaveData.Add(ok ? "wr_ok_" + item.id : "wr_err_" + item.id, 1);
                MurasCloud.Track("word_review", CloudJson.Object("id", item.id, "ok", ok));
                WordAudio.Play(item.id, false);
            },
            resultText = r => Loc.T("Правильно: <b>" + r.score + " из " + r.items.Count + "</b>", "Correct: <b>" + r.score + " of " + r.items.Count + "</b>") +
                              (r.score == r.items.Count ? Loc.T("\nМыкты! — Отлично!", "\nМыкты! — Excellent!") : ""),
        });
    }

    static float Weight(Vocab.Word w)
    {
        int err = SaveData.Get("wr_err_" + w.id), ok = SaveData.Get("wr_ok_" + w.id);
        return Mathf.Max(0.3f, 1f + err * 2f - ok * 0.5f);
    }
}
