using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Панель реплик NPC в стиле диалога с Айбеком: портрет, табличка с именем и текст,
/// который печатается по буквам под «болтовню» персонажа.
/// Квесты пишут в текст как раньше: «Аксакал: ...», «Эже: ...», «Бала: ...» —
/// панель сама узнаёт, кто говорит, по началу фразы.
/// </summary>
public class SpeechPanel : MonoBehaviour
{
    [System.Serializable]
    public class Speaker
    {
        public string key = "Аксакал";
        public string displayName = "Аксакал";
        public string role = "старейшина";
        public string roleEn = "elder";
        public Sprite portrait;
        [Range(0.5f, 2f)] public float voicePitch = 1f;
    }

    public TMP_Text text;
    public Image portrait;
    public GameObject nameTag;
    public TMP_Text nameText;
    public TMP_Text roleText;
    public float charactersPerSecond = 45f;
    public float textLeftWithPortrait = 200f;
    public float textLeftWithout = 36f;
    public List<Speaker> speakers = new List<Speaker>();

    public static SpeechPanel Instance { get; private set; }

    /// <summary>Сейчас на экране есть реплика.</summary>
    public bool IsShowing => text != null && !string.IsNullOrEmpty(text.text) && text.gameObject.activeInHierarchy;

    /// <summary>Закрыть реплику (кнопка ✕ или Esc).</summary>
    public void Close()
    {
        if (text != null) text.text = "";
        GameAudio.StopBabble();
    }

    private void Awake() => Instance = this;

    private string shown;
    private float reveal;
    private bool typing;

    private void LateUpdate()
    {
        if (text == null) return;

        if (text.text != shown)
            OnNewText(text.text);

        if (typing)
        {
            reveal += charactersPerSecond * Time.deltaTime;
            int total = text.textInfo != null ? text.textInfo.characterCount : shown.Length;
            if (reveal >= total && total > 0)
            {
                typing = false;
                text.maxVisibleCharacters = 99999;
            }
            else text.maxVisibleCharacters = Mathf.FloorToInt(reveal);
        }
    }

    private void OnNewText(string raw)
    {
        Speaker speaker = null;
        string body = raw ?? "";
        int colon = body.IndexOf(':');
        if (colon > 0 && colon < 18)
        {
            string key = body.Substring(0, colon).Trim();
            speaker = speakers.Find(s => string.Equals(s.key, key, System.StringComparison.OrdinalIgnoreCase));
            if (speaker != null) body = body.Substring(colon + 1).TrimStart();
        }

        shown = body;
        text.text = body;

        bool hasSpeaker = speaker != null && !string.IsNullOrEmpty(body);
        if (portrait != null)
        {
            portrait.enabled = hasSpeaker && speaker.portrait != null;
            if (portrait.enabled) portrait.sprite = speaker.portrait;
        }
        if (nameTag != null) nameTag.SetActive(hasSpeaker);
        if (hasSpeaker)
        {
            if (nameText != null) nameText.text = speaker.displayName;
            if (roleText != null) roleText.text = Loc.T(speaker.role, speaker.roleEn);
        }
        else if (roleText != null) roleText.text = "";

        RectTransform rt = text.rectTransform;
        rt.offsetMin = new Vector2(hasSpeaker ? textLeftWithPortrait : textLeftWithout, rt.offsetMin.y);

        if (string.IsNullOrEmpty(body))
        {
            typing = false;
            GameAudio.StopBabble();
            return;
        }

        reveal = 0f;
        typing = true;
        text.maxVisibleCharacters = 0;
        if (hasSpeaker) GameAudio.Babble(body, speaker.voicePitch);
    }
}
