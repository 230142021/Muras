using UnityEngine;
using TMPro;

/// <summary>Большая плашка «Квест выполнен! +XP» со звуком — появляется при завершении любого квеста.</summary>
[RequireComponent(typeof(CanvasGroup))]
public class QuestCompleteBanner : MonoBehaviour
{
    public TMP_Text questName;
    public TMP_Text reward;
    public float showTime = 3.2f;

    private CanvasGroup group;
    private float shownAt = -100f;

    private static readonly System.Collections.Generic.Dictionary<string, string> Names =
        new System.Collections.Generic.Dictionary<string, string>
        {
            { "greeting", "Саламдашуу" }, { "guide", "Айылдын досу" }, { "apples", "Үч алма" }, { "yurt", "Боз үй" },
            { "chuko", "Чүкө атуу" }, { "boorsok", "Боорсок" }, { "kuurdak", "Куурдак" }
        };

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
    }

    private void OnEnable() => GameManager.QuestCompleted += Show;
    private void OnDisable() => GameManager.QuestCompleted -= Show;

    private void Show(string id, int xp)
    {
        if (questName != null) questName.text = Names.TryGetValue(id, out string n) ? n : id;
        if (reward != null) reward.text = "+" + xp + " XP";
        shownAt = Time.time;
    }

    private void Update()
    {
        float t = Time.time - shownAt;
        float a = t < 0.25f ? t / 0.25f : (t > showTime ? Mathf.Clamp01(1f - (t - showTime) / 0.6f) : 1f);
        group.alpha = a;
        float s = t < 0.25f ? Mathf.Lerp(0.7f, 1.05f, t / 0.25f) : (t < 0.4f ? Mathf.Lerp(1.05f, 1f, (t - 0.25f) / 0.15f) : 1f);
        transform.localScale = Vector3.one * s;
    }
}
