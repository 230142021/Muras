using UnityEngine;
using TMPro;

/// <summary>
/// Подсказка взаимодействия внизу экрана («E — поговорить», «E — подобрать»).
/// Любой скрипт в Update вызывает InteractionHint.Request("..."); если в кадре запросов нет, подсказка плавно гаснет.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class InteractionHint : MonoBehaviour
{
    public TMP_Text label;
    public float fadeSpeed = 10f;

    private static InteractionHint instance;
    private static string requested;
    private static int requestedFrame = -1;

    private CanvasGroup group;

    public static void Request(string text)
    {
        requested = text;
        requestedFrame = Time.frameCount;
    }

    private void Awake()
    {
        instance = this;
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void LateUpdate()
    {
        bool visible = requestedFrame == Time.frameCount && !string.IsNullOrEmpty(requested);

        if (visible && label != null && label.text != requested)
            label.text = requested;

        // Мягкий звук, когда игрок нажимает E рядом с чем-то интерактивным
        if (visible && Input.GetKeyDown(KeyCode.E))
            GameAudio.Interact();

        group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, fadeSpeed * Time.deltaTime);
    }
}
