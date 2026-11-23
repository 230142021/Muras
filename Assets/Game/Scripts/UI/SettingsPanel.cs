using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Панель настроек: громкость, мышь, полный экран, графика, язык. Значения сразу применяются и сохраняются.</summary>
public class SettingsPanel : MonoBehaviour
{
    public Slider volumeSlider;
    public TMP_Text volumeValue;
    public Slider sensitivitySlider;
    public TMP_Text sensitivityValue;
    public Toggle fullscreenToggle;
    public TMP_Text qualityValue;
    public Button qualityPrev;
    public Button qualityNext;

    [Header("Язык")]
    public TMP_Text languageValue;
    public Button languagePrev;
    public Button languageNext;

    private bool wired;

    void OnEnable()
    {
        // Подписываемся, только когда ссылки уже заполнены (в паузе панель создаётся из кода)
        if (volumeSlider == null && sensitivitySlider == null) return;
        Wire();

        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.SetValueWithoutNotify(GameSettings.Volume);
        }
        if (sensitivitySlider != null)
        {
            sensitivitySlider.minValue = GameSettings.MinSensitivity;
            sensitivitySlider.maxValue = GameSettings.MaxSensitivity;
            sensitivitySlider.SetValueWithoutNotify(GameSettings.MouseSensitivity);
        }
        if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(GameSettings.Fullscreen);
        Refresh();
    }

    void OnDisable() => GameSettings.Save();

    void Wire()
    {
        if (wired) return;
        wired = true;
        if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(v => { GameSettings.Volume = v; Refresh(); });
        if (sensitivitySlider != null) sensitivitySlider.onValueChanged.AddListener(v => { GameSettings.MouseSensitivity = v; Refresh(); });
        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(v => { GameSettings.Fullscreen = v; MenuSound.Click(); });
        if (qualityPrev != null) qualityPrev.onClick.AddListener(() => { GameSettings.Quality--; Refresh(); MenuSound.Click(); });
        if (qualityNext != null) qualityNext.onClick.AddListener(() => { GameSettings.Quality++; Refresh(); MenuSound.Click(); });
        if (languagePrev != null) languagePrev.onClick.AddListener(SwitchLanguage);
        if (languageNext != null) languageNext.onClick.AddListener(SwitchLanguage);
    }

    void SwitchLanguage()
    {
        MenuSound.Click();
        Loc.Next();
        Refresh();
    }

    void Refresh()
    {
        if (volumeValue != null) volumeValue.text = Mathf.RoundToInt(GameSettings.Volume * 100f) + "%";
        if (sensitivityValue != null) sensitivityValue.text = GameSettings.MouseSensitivity.ToString("0.0");
        if (qualityValue != null) qualityValue.text = GameSettings.QualityName;
        if (languageValue != null) languageValue.text = Loc.LanguageName;
    }
}
