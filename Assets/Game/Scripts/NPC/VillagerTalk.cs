using TMPro;
using UnityEngine;

/// <summary>
/// Житель села: подойди и нажми E — расскажет что-нибудь о жизни в горах и кыргызских традициях.
/// Реплики идут по кругу, есть русский и английский варианты. Фраза показывается в общей панели речи
/// (начинается с ключа говорящего, например «Апа: …» — по нему панель ставит портрет и имя).
/// Днём и ночью говорит разное приветствие.
/// </summary>
public class VillagerTalk : MonoBehaviour
{
    [Tooltip("Ключ говорящего в SpeechPanel: «Апа», «Байке», «Чабан»")]
    public string speakerKey = "Апа";
    public string nameRu = "апой";
    public string nameEn = "Apa";

    [TextArea(2, 4)] public string[] linesRu;
    [TextArea(2, 4)] public string[] linesEn;
    [TextArea(1, 3)] public string nightGreetingRu = "Түн жакшы болсун! Уже поздно — скоро все пойдут спать.";
    [TextArea(1, 3)] public string nightGreetingEn = "Түн жакшы болсун! It's late — everyone will go to sleep soon.";

    public TMP_Text storyText;
    public float talkDistance = 2.6f;
    [Tooltip("Слова словаря, которые житель «учит» при разговоре")]
    public string[] vocabWords;
    public float showSeconds = 7f;

    private Transform player;
    private int index;
    private string shownText;
    private float hideAt;

    void Start()
    {
        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null) player = pm.transform;
    }

    void Update()
    {
        if (shownText != null && Time.time > hideAt)
        {
            if (storyText != null && storyText.text == shownText) storyText.text = "";
            shownText = null;
        }

        if (player == null || PauseMenu.IsPaused || !Modal.Free) return;
        Vector3 d = player.position - transform.position; d.y = 0f;
        if (d.sqrMagnitude > talkDistance * talkDistance) return;

        InteractionHint.Request("<b><color=#E8B04A>E</color></b>  —  " + Loc.T("поговорить с " + nameRu, "talk to " + nameEn));
        if (Input.GetKeyDown(KeyCode.E)) Say();
    }

    void Say()
    {
        if (storyText == null) return;
        string line;
        if (QuestTalk.TryHandle(speakerKey, out string questLine))      // побочный квест хочет особую фразу
        {
            shownText = speakerKey + ": " + questLine;
            storyText.text = shownText;
            hideAt = Time.time + showSeconds + questLine.Length * 0.03f;
            return;
        }
        if (DayNightCycle.IsNight && index == 0)
            line = Loc.T(nightGreetingRu, nightGreetingEn);
        else
        {
            string[] lines = Loc.IsEnglish && linesEn != null && linesEn.Length > 0 ? linesEn : linesRu;
            if (lines == null || lines.Length == 0) return;
            line = lines[index % lines.Length];
        }
        index++;
        if (vocabWords != null && vocabWords.Length > 0) Vocab.Unlock(vocabWords);
        shownText = speakerKey + ": " + line;
        storyText.text = shownText;
        hideAt = Time.time + showSeconds + line.Length * 0.02f;
    }
}
