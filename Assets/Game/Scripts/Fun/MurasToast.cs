using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Всплывающие плашки справа: «Жаңы сөз», «Жетишкендик», награды мини-игр.
/// Создаются сами, показываются по очереди.
/// </summary>
public class MurasToast : MonoBehaviour
{
    private struct Item { public string title, body, sound; public Color accent; }
    private static MurasToast instance;
    private static readonly Queue<Item> queue = new Queue<Item>();

    private CanvasGroup group;
    private TMP_Text title, body;
    private Image accent;
    private float shownAt = -100f;
    private const float Show = 3.4f;

    public static void Push(string title, string body, Color accent, string sound = "word_chime")
    {
        queue.Enqueue(new Item { title = title, body = body, accent = accent, sound = sound });
        if (instance == null)
        {
            var go = new GameObject("MurasToast");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<MurasToast>();
        }
    }

    void Awake()
    {
        var canvas = MurasUI.Canvas("ToastCanvas", transform, 470);
        var panel = MurasUI.Rect("Toast", canvas.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-250, -200), new Vector2(460, 110));
        panel.AddComponent<Image>().color = new Color(0.10f, 0.08f, 0.06f, 0.93f);
        group = panel.AddComponent<CanvasGroup>(); group.alpha = 0f; group.blocksRaycasts = false;
        var bar = MurasUI.Rect("Accent", panel.transform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(5, 0), new Vector2(10, 0));
        accent = bar.AddComponent<Image>();
        title = MurasUI.Text(panel.transform, "", 24, MurasUI.Gold, new Vector2(12, 28), new Vector2(410, 34), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        body = MurasUI.Text(panel.transform, "", 19, MurasUI.Cream, new Vector2(12, -16), new Vector2(410, 56), FontStyles.Normal, TextAlignmentOptions.TopLeft);
    }

    void Update()
    {
        float t = Time.unscaledTime - shownAt;
        if (t > Show + 0.4f && queue.Count > 0)
        {
            Item it = queue.Dequeue();
            title.text = it.title; body.text = it.body; accent.color = it.accent;
            shownAt = Time.unscaledTime; t = 0f;
            if (!string.IsNullOrEmpty(it.sound)) MurasUI.Play(it.sound, 0.7f);
        }
        float a = t < 0.25f ? t / 0.25f : (t > Show ? Mathf.Clamp01(1f - (t - Show) / 0.4f) : 1f);
        group.alpha = a;
        ((RectTransform)group.transform).anchoredPosition = new Vector2(-250 + (1f - Mathf.Clamp01(t / 0.25f)) * 60f, -200);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; queue.Clear(); }
}
