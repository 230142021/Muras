using TMPro;
using UnityEngine;

/// <summary>
/// Текст на экране с двумя вариантами — русским и английским. Сам переключается при смене языка.
/// Поставь на объект с TextMeshPro и заполни оба поля.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [TextArea(1, 6)] public string ru;
    [TextArea(1, 6)] public string en;

    private TMP_Text text;

    void Awake() => text = GetComponent<TMP_Text>();

    void OnEnable()
    {
        Loc.Changed += Apply;
        Apply();
    }

    void OnDisable() => Loc.Changed -= Apply;

    public void Apply()
    {
        if (text == null) text = GetComponent<TMP_Text>();
        if (text != null) text.text = Loc.T(ru, en);
    }
}
