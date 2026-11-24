using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Окно, которое закрывается по Esc (базар, словарь, мини-игры).</summary>
public interface IModal { void CloseModal(); }

/// <summary>
/// Одно открытое окно за раз: пока оно открыто, другие окна и разговоры с NPC не открываются,
/// а Esc сначала закрывает его (см. PauseMenu).
/// </summary>
public static class Modal
{
    public static IModal Current { get; private set; }
    public static bool IsOpen => Current != null && !(Current is Object o && o == null);

    public static void Open(IModal m) => Current = m;
    public static void Close(IModal m) { if (Current == m) Current = null; }

    public static bool TryClose()
    {
        if (!IsOpen) { Current = null; return false; }
        IModal m = Current;
        m.CloseModal();
        if (Current == m) Current = null;
        return true;
    }

    /// <summary>Можно ли сейчас начинать новое действие (нажать E у NPC, открыть окно).</summary>
    public static bool Free => !IsOpen && !PauseMenu.IsPaused && !VillageShop.IsOpen && !RaceManager.Active && !KokBoruGame.Active;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Current = null;
}

/// <summary>Общие кирпичики интерфейса в стиле игры (тёмное дерево и золото).</summary>
public static class MurasUI
{
    public static readonly Color Gold = new Color(0.84f, 0.66f, 0.29f);
    public static readonly Color Cream = new Color(0.96f, 0.93f, 0.85f);
    public static readonly Color Muted = new Color(0.72f, 0.68f, 0.58f);
    public static readonly Color Window = new Color(0.11f, 0.085f, 0.065f, 0.97f);
    public static readonly Color Card = new Color(0.19f, 0.145f, 0.105f, 1f);
    public static readonly Color ButtonCol = new Color(0.55f, 0.36f, 0.16f);
    public static readonly Color Green = new Color(0.45f, 0.72f, 0.38f);
    public static readonly Color Red = new Color(0.86f, 0.36f, 0.30f);

    public static GameObject Canvas(string name, Transform parent, int order)
    {
        if (Object.FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(parent, false);
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = order;
        var s = go.GetComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080);
        s.matchWidthOrHeight = 0.5f;
        return go;
    }

    public static GameObject Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return go;
    }

    public static GameObject Box(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        var go = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        var img = go.AddComponent<Image>(); img.color = color;
        return go;
    }

    public static GameObject Overlay(Transform parent, float alpha = 0.62f)
    {
        var go = Rect("Overlay", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        go.AddComponent<Image>().color = new Color(0f, 0f, 0f, alpha);
        return go;
    }

    public static GameObject WindowBox(Transform parent, Vector2 size)
    {
        var w = Box("Window", parent, Vector2.zero, size, Window);
        var top = Rect("TopLine", w.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -3), new Vector2(0, 6));
        top.AddComponent<Image>().color = Gold;
        return w;
    }

    public static TMP_Text Text(Transform parent, string text, float size, Color color, Vector2 pos, Vector2 box,
                                FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var go = Rect("Text", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, box);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = color; t.fontStyle = style;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        return t;
    }

    public static Button Button(Transform parent, string label, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction action, float fontSize = 22f)
    {
        var go = Box("Button_" + label, parent, pos, size, ButtonCol);
        var b = go.AddComponent<Button>();
        var cb = b.colors; cb.highlightedColor = new Color(1.25f, 1.2f, 1.1f); cb.pressedColor = new Color(0.8f, 0.8f, 0.8f); cb.disabledColor = new Color(0.5f, 0.5f, 0.5f);
        b.colors = cb;
        if (action != null) b.onClick.AddListener(action);
        Text(go.transform, label, fontSize, Cream, Vector2.zero, size, FontStyles.Bold);
        return b;
    }

    public static Button CloseButton(Transform window, UnityEngine.Events.UnityAction action)
    {
        var go = Rect("Close", window, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -30), new Vector2(44, 44));
        go.AddComponent<Image>().color = new Color(0.30f, 0.20f, 0.13f);
        var b = go.AddComponent<Button>(); b.onClick.AddListener(action);
        Text(go.transform, "X", 26, Cream, Vector2.zero, new Vector2(44, 44), FontStyles.Bold);
        return b;
    }

    public static AudioClip Clip(string name) => Resources.Load<AudioClip>("MURAS/Audio/" + name);

    private static AudioSource uiSource;
    /// <summary>2D-звук интерфейса из Resources/MURAS/Audio.</summary>
    public static void Play(string clip, float volume = 0.8f, float pitch = 1f)
    {
        AudioClip c = Clip(clip);
        if (c == null) return;
        if (uiSource == null)
        {
            var go = new GameObject("MurasUIAudio");
            Object.DontDestroyOnLoad(go);
            uiSource = go.AddComponent<AudioSource>();
            uiSource.playOnAwake = false; uiSource.spatialBlend = 0f; uiSource.ignoreListenerPause = true;
        }
        uiSource.pitch = pitch;
        uiSource.PlayOneShot(c, volume);
    }
}

