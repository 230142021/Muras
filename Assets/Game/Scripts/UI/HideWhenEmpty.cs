using UnityEngine;
using TMPro;

/// <summary>
/// Плашка интерфейса показывается, только когда в её тексте что-то написано.
/// Если задано autoHideAfter, плашка сама гаснет через столько секунд после последнего изменения текста.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class HideWhenEmpty : MonoBehaviour
{
    public TMP_Text target;
    [Tooltip("0 — не скрывать по времени")]
    public float autoHideAfter = 0f;
    public float fadeSpeed = 8f;

    private CanvasGroup group;
    private string lastText;
    private float lastChange;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    private void LateUpdate()
    {
        string text = target != null ? target.text : null;
        if (text != lastText)
        {
            lastText = text;
            lastChange = Time.time;
        }

        bool visible = target != null && target.gameObject.activeInHierarchy && !string.IsNullOrEmpty(text) &&
                       (autoHideAfter <= 0f || Time.time - lastChange < autoHideAfter);

        group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, fadeSpeed * Time.deltaTime);
    }
}
