using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Красивая реакция кнопки меню: при наведении чуть увеличивается, загорается золотая полоска
/// слева, текст становится золотым; звуки наведения и нажатия. Работает и на паузе (unscaled time).
/// </summary>
[RequireComponent(typeof(Button))]
public class MenuButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, IPointerClickHandler
{
    public Graphic accent;
    public TMP_Text label;
    public Image background;
    public float hoverScale = 1.035f;
    public Color labelNormal = new Color(0.96f, 0.93f, 0.85f);
    public Color labelHover = new Color(0.93f, 0.73f, 0.33f);
    public Color bgNormal = new Color(0.14f, 0.105f, 0.08f, 0.88f);
    public Color bgHover = new Color(0.24f, 0.17f, 0.11f, 0.95f);

    private Button button;
    private bool hovered;
    private float t;

    void Awake() => button = GetComponent<Button>();

    void OnEnable()
    {
        hovered = false;
        t = 0f;
        Apply();
    }

    void Update()
    {
        float target = hovered && button.interactable ? 1f : 0f;
        if (Mathf.Approximately(t, target)) return;
        t = Mathf.MoveTowards(t, target, Time.unscaledDeltaTime * 8f);
        Apply();
    }

    void Apply()
    {
        float e = t * t * (3f - 2f * t);
        transform.localScale = Vector3.one * Mathf.Lerp(1f, hoverScale, e);
        if (accent != null) { Color c = accent.color; c.a = e; accent.color = c; }
        bool on = button == null || button.interactable;
        if (label != null) label.color = on ? Color.Lerp(labelNormal, labelHover, e) : new Color(labelNormal.r, labelNormal.g, labelNormal.b, 0.35f);
        if (background != null) background.color = Color.Lerp(bgNormal, bgHover, e);
    }

    public void OnPointerEnter(PointerEventData e) => SetHover(true);
    public void OnPointerExit(PointerEventData e) => SetHover(false);
    public void OnSelect(BaseEventData e) => SetHover(true);
    public void OnDeselect(BaseEventData e) => SetHover(false);

    public void OnPointerClick(PointerEventData e)
    {
        if (button != null && button.interactable) MenuSound.Click();
    }

    void SetHover(bool on)
    {
        if (on && !hovered && button != null && button.interactable) MenuSound.Hover();
        hovered = on;
    }
}

/// <summary>Звуки интерфейса меню (работают на паузе и между сценами).</summary>
public static class MenuSound
{
    public static AudioClip hoverClip;
    public static AudioClip clickClip;
    private static AudioSource source;

    public static void Hover() => Play(hoverClip, 0.35f, 1.15f);
    public static void Click() => Play(clickClip, 0.6f, 1f);

    private static void Play(AudioClip clip, float volume, float pitch)
    {
        if (clip == null) return;
        if (source == null)
        {
            var go = new GameObject("MenuSound");
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.ignoreListenerPause = true;
            source.spatialBlend = 0f;
        }
        source.pitch = pitch;
        source.PlayOneShot(clip, volume);
    }
}
