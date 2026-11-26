using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Перехват разговоров с жителями для побочных квестов: если квест хочет, чтобы житель сказал
/// особую фразу (например, во время расследования), VillagerTalk сначала спрашивает QuestTalk.
/// </summary>
public static class QuestTalk
{
    public delegate bool Handler(string speakerKey, out string line);
    private static readonly List<Handler> handlers = new List<Handler>();

    public static void Register(Handler h) { if (!handlers.Contains(h)) handlers.Add(h); }

    public static bool TryHandle(string speakerKey, out string line)
    {
        foreach (Handler h in handlers)
            if (h(speakerKey, out line)) return true;
        line = null;
        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => handlers.Clear();

    /// <summary>Показать реплику в общей панели речи (как будто говорит житель).</summary>
    public static void Say(string speakerKey, string line)
    {
        if (SpeechPanel.Instance != null && SpeechPanel.Instance.text != null)
            SpeechPanel.Instance.text.text = string.IsNullOrEmpty(speakerKey) ? line : speakerKey + ": " + line;
    }
}

/// <summary>
/// Строки побочных квестов в панели «ЗАДАНИЯ» слева сверху. Каждый квест кладёт свою строку по id;
/// пустая строка — убрать. Строка копирует стиль остальных заданий.
/// </summary>
public static class SideQuestHud
{
    private static readonly Dictionary<string, string> lines = new Dictionary<string, string>();
    private static readonly Dictionary<string, TMP_Text> labels = new Dictionary<string, TMP_Text>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { lines.Clear(); labels.Clear(); }

    public static void Set(string id, string title, string desc)
    {
        string text = string.IsNullOrEmpty(title) ? null : "<b>" + title + "</b>\n<size=80%><color=#C9BFA6>" + desc + "</color></size>";
        if (text == null) lines.Remove(id); else lines[id] = text;
        if (text == null) GameAnalytics.QuestCleared(id); else GameAnalytics.QuestStep(id, title + ": " + desc);   // аналитика: шаги заданий
        Apply(id);
    }

    static void Apply(string id)
    {
        if (!labels.TryGetValue(id, out TMP_Text label) || label == null)
        {
            label = null;
            if (!lines.ContainsKey(id)) return;
            GameObject card = GameObject.Find("QuestCard");
            if (card == null) return;
            TMP_Text sample = null;
            foreach (TMP_Text t in card.GetComponentsInChildren<TMP_Text>(true))
                if (t.name != "QuestHeader") { sample = t; break; }
            if (sample == null) return;
            label = Object.Instantiate(sample, card.transform);
            label.name = "Side_" + id;
            foreach (var c in label.GetComponents<MonoBehaviour>())
                if (!(c is TMP_Text)) Object.Destroy(c);       // без чужих скриптов (локализация, скрытие)
            labels[id] = label;
        }
        bool on = lines.TryGetValue(id, out string text);
        label.gameObject.SetActive(on);
        if (on) label.text = text;
    }

    /// <summary>Повторно применить все строки (после перезагрузки сцены).</summary>
    public static void Refresh() { foreach (string id in new List<string>(lines.Keys)) { labels.Remove(id); Apply(id); } }
}
