using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Кнопка «Подсказка» (и клавиша H): на несколько секунд включает подсказки квестов —
/// стрелку над целью и полупрозрачную деталь на месте установки.
/// Квесты проверяют HintButton.IsActive.
/// </summary>
public class HintButton : MonoBehaviour
{
    public float duration = 8f;
    public KeyCode key = KeyCode.H;
    public TMP_Text label;

    private static float activeUntil = -1f;

    public static bool IsActive => Time.time < activeUntil;

    private void Awake()
    {
        if (TryGetComponent(out Button button))
            button.onClick.AddListener(Activate);
    }

    public void Activate()
    {
        activeUntil = Time.time + duration;
    }

    private void Update()
    {
        if (Input.GetKeyDown(key))
            Activate();

        if (label == null)
            return;

        label.text = IsActive
            ? Loc.T("Подсказка", "Hint") + "  <color=#E8B04A>" + Mathf.CeilToInt(activeUntil - Time.time) + "</color>"
            : Loc.T("Подсказка", "Hint") + "  <color=#E8B04A><b>[" + key + "]</b></color>";
    }
}
