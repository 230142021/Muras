using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Айбек — друг и гид по айылу (класс сохранил старое имя, чтобы не ломать ссылки в сцене).
/// 1) Саламдашуу: поздороваться с Айбеком (DialogueManager, вопрос «Атың ким?»).
/// 2) Айбек становится гидом: отправляет к жителям — аксакалу (юрта), детям (чүкө), эже (кухня)
///    и рассказывает о других занятиях (комуз, шырдак, скачки, рыбалка, беркут, базар).
/// 3) Возвращайся к Айбеку и рассказывай, что сделал: он следит за результатом, хвалит и даёт
///    бонус XP за каждое новое дело. Когда все главные задания выполнены — квест «Айылдын досу».
/// </summary>
public class AppleQuestManager : MonoBehaviour, IQuestHintSource
{
    [Tooltip("Айбек (для стрелки-подсказки). Если пусто — найдётся сам")]
    public Transform aibek;

    public TMP_Text questText;
    public TMP_Text messageText;

    [Tooltip("Не используется (раньше — сколько яблок нужно)")]
    public int requiredApples = 3;
    [Tooltip("Награда за весь квест гида")]
    public int rewardXP = 150;
    public float messageDuration = 4.5f;

    public const string QuestId = "guide";

    class Task
    {
        public string id, nameRu, nameEn, whoRu, whoEn, praiseRu, praiseEn;
        public bool main;
        public int bonus;
        public System.Func<bool> done;
        public System.Func<Transform> where;
    }

    private List<Task> tasks;
    private Coroutine hideRoutine;

    void OnEnable() { QuestHints.Register(this); Loc.Changed += UpdateQuestUI; GameManager.QuestCompleted += OnAnyQuest; }
    void OnDisable() { QuestHints.Unregister(this); Loc.Changed -= UpdateQuestUI; GameManager.QuestCompleted -= OnAnyQuest; }

    static bool Q(string id) => GameManager.Instance != null && GameManager.Instance.IsQuestCompleted(id);
    static Transform FindT<T>() where T : Component { var c = FindAnyObjectByType<T>(); return c != null ? c.transform : null; }

    void Awake()
    {
        tasks = new List<Task>
        {
            new Task { id = "yurt", main = true, bonus = 30, done = () => Q("yurt"), where = FindT<YurtQuestManager>,
                       nameRu = "Боз үй — юрта", nameEn = "Boz üy — the yurt", whoRu = "аксакалу у юрты", whoEn = "the aksakal by the yurt",
                       praiseRu = "Ты собрал юрту с аксакалом — теперь знаешь, что такое түндүк и кереге!", praiseEn = "You built the yurt with the aksakal — now you know the tündük and kerege!" },
            new Task { id = "chuko", main = true, bonus = 30, done = () => Q("chuko"), where = FindT<ChukoGameManager>,
                       nameRu = "Чүкө атуу — игра", nameEn = "Chükö atuu — the game", whoRu = "детям на лугу", whoEn = "the children on the meadow",
                       praiseRu = "Дети говорят, ты метко выбиваешь чүкө!", praiseEn = "The kids say you knock out chükö like a pro!" },
            new Task { id = "boorsok", main = true, bonus = 30, done = () => Q("boorsok"), where = FindT<FoodQuestManager>,
                       nameRu = "Боорсок — эже на кухне", nameEn = "Boorsok — Ezhe's kitchen", whoRu = "эже на кухне", whoEn = "Ezhe in the kitchen",
                       praiseRu = "Пахнет боорсоками! Эже тебя хвалила.", praiseEn = "Smells like boorsok! Ezhe praised you." },
            new Task { id = "kuurdak", main = true, bonus = 30, done = () => Q("kuurdak"), where = FindT<FoodQuestManager>,
                       nameRu = "Куурдак — эже на кухне", nameEn = "Kuurdak — Ezhe's kitchen", whoRu = "эже на кухне", whoEn = "Ezhe in the kitchen",
                       praiseRu = "Куурдак у тебя получился как у настоящего ашпозчу!", praiseEn = "Your kuurdak came out like a real cook's!" },
            new Task { id = "komuz", bonus = 15, done = () => SaveData.Get("komuz_jailoo") > 0 || SaveData.Get("komuz_tulpar") > 0, where = FindT<KomuzMusician>,
                       nameRu = "Комуз с Нурланом", nameEn = "Komuz with Nurlan", whoRu = "комузчу Нурлану у юрты", whoEn = "Nurlan the komuz player by the yurt",
                       praiseRu = "Я слышал, как ты играл на комузе — жакшы!", praiseEn = "I heard you play the komuz — жакшы!" },
            new Task { id = "shyrdak", bonus = 15, done = () => SaveData.Get("shyrdak_1") == 1, where = FindT<ShyrdakStation>,
                       nameRu = "Шырдак с Апой", nameEn = "Shyrdak with Apa", whoRu = "Апе — она шьёт шырдак", whoEn = "Apa — she is sewing a shyrdak",
                       praiseRu = "Апа рассказала, что ты помог ей со шырдаком. Рахмат!", praiseEn = "Apa told me you helped with the shyrdak. Рахмат!" },
            new Task { id = "race", bonus = 15, done = () => SaveData.Has("race_best_ms"), where = FindT<RaceStarter>,
                       nameRu = "Ат чабыш — скачки", nameEn = "At chabysh — horse race", whoRu = "чабандесу Эрмеку на поле", whoEn = "Ermek the jockey on the field",
                       praiseRu = "Ты скакал на коне в ат чабыш — вот это джигит!", praiseEn = "You rode in the at chabysh — what a jigit!" },
            new Task { id = "fish", bonus = 15, done = () => SaveData.Get("fish") > 0, where = FindT<FishingSpot>,
                       nameRu = "Рыбалка на реке", nameEn = "Fishing at the river", whoRu = "на мостик у реки", whoEn = "the pier by the river",
                       praiseRu = "Ого, ты поймал рыбу! Продай её Асану на базаре.", praiseEn = "Wow, you caught a fish! Sell it to Asan at the bazaar." },
            new Task { id = "eagle", bonus = 15, done = () => SaveData.Get("eagle") == 1, where = FindT<EagleHunter>,
                       nameRu = "Беркут Болота", nameEn = "Bolot's eagle", whoRu = "бүркүтчү Болоту у фермы", whoEn = "Bolot the eagle hunter by the farm",
                       praiseRu = "Ты видел, как летает бүркүт? Это гордость кыргызов!", praiseEn = "You saw the bürküt fly? It's the pride of the Kyrgyz!" },
            new Task { id = "bazaar", bonus = 15, done = () => ShopCatalog.OwnedCount > 0, where = FindT<ShopTrader>,
                       nameRu = "Покупка на базаре", nameEn = "A purchase at the bazaar", whoRu = "торговцу Асану на базар", whoEn = "Asan the trader at the bazaar",
                       praiseRu = "Обновка с базара тебе идёт!", praiseEn = "Your new things from the bazaar suit you!" },
        };
    }

    // ------------------------------------------------------------------ состояние

    bool Greeted => Q("greeting");
    bool Started => SaveData.Get("guide_started") == 1;
    bool Finished => Q(QuestId);
    bool Reported(Task t) => SaveData.Get("guide_rep_" + t.id) == 1;
    int MainDone { get { int n = 0; foreach (Task t in tasks) if (t.main && t.done()) n++; return n; } }
    int MainTotal { get { int n = 0; foreach (Task t in tasks) if (t.main) n++; return n; } }
    bool HasNews { get { foreach (Task t in tasks) if (t.done() && !Reported(t)) return true; return false; } }

    /// <summary>Яблоки в саду можно собирать, когда ты поздоровался с Айбеком.</summary>
    public bool CanCollectApples => Greeted;

    void Start()
    {
        if (messageText != null)
        {
            messageText.text = "";
            if (messageText.transform.parent != null && messageText.transform.parent.name == "MessageToast")
                messageText.transform.parent.gameObject.SetActive(false);
        }
        HideOldAppleCounter();
        UpdateQuestUI();
    }

    void HideOldAppleCounter()
    {
        if (InventoryManager.Instance == null || InventoryManager.Instance.inventoryText == null) return;
        Transform t = InventoryManager.Instance.inventoryText.transform;
        (t.parent != null && t.parent.name.Contains("Inventory") ? t.parent.gameObject : t.gameObject).SetActive(false);
    }

    void OnAnyQuest(string id, int xp) => UpdateQuestUI();

    float nextUiRefresh;
    void Update()
    {
        if (Time.time > nextUiRefresh) { nextUiRefresh = Time.time + 1f; UpdateQuestUI(); }
    }

    // ------------------------------------------------------------------ разговор (вызывает NPCInteraction после «Саламдашуу»)

    public void InteractWithAibek()
    {
        if (!Greeted) return;

        if (!Started)
        {
            SaveData.Set("guide_started", 1);
            GiveLantern();
            Say(Loc.T("Айбек: Эми биз доспуз! Теперь мы друзья — я буду твоим гидом по айылу. Жители ждут помощи: <b>аксакал у юрты</b>, <b>дети на лугу</b> и <b>эже на кухне</b>. Помогай им и возвращайся ко мне — я слежу за твоими успехами!",
                      "Aibek: Эми биз доспуз! Now we're friends — I'll be your guide around the ayil. The villagers need help: <b>the aksakal by the yurt</b>, <b>the children on the meadow</b> and <b>Ezhe in the kitchen</b>. Help them and come back to me — I'm keeping track of your progress!"));
            StartCoroutine(SayLater(7.5f, Loc.T("Айбек: А ещё в айыле есть комузчу Нурлан, Апа со шырдаком, скачки у Эрмека, рыбалка, беркут Болота и базар Асана. Яблоки в саду можно рвать — они попадут в капчык (I).",
                                                "Aibek: There's also Nurlan the komuz player, Apa with her shyrdak, Ermek's horse race, fishing, Bolot's eagle and Asan's bazaar. You can pick apples in the orchard — they go into your bag (I).")));
            UpdateQuestUI();
            return;
        }

        GiveLantern();
        // новости: что сделано с прошлого разговора
        var fresh = new List<Task>();
        foreach (Task t in tasks) if (t.done() && !Reported(t)) fresh.Add(t);
        int bonus = 0;
        foreach (Task t in fresh) { SaveData.Set("guide_rep_" + t.id, 1); bonus += t.bonus; }
        if (bonus > 0 && GameManager.Instance != null) GameManager.Instance.AddXP(bonus);

        string line;
        if (fresh.Count > 0)
        {
            line = "Айбек: " + Loc.T(fresh[0].praiseRu, fresh[0].praiseEn);
            if (fresh.Count > 1) line += Loc.T(" И ещё: ", " And also: ") + string.Join(", ", fresh.GetRange(1, fresh.Count - 1).ConvertAll(t => Loc.T(t.nameRu, t.nameEn))) + "!";
            line += "  <color=#5FC873>+" + bonus + " XP</color>";
            GameAudio.Correct();
        }
        else line = Loc.T("Айбек: Кандайсың, досум?", "Aibek: Кандайсың, досум? How are you, friend?");

        if (!Finished && MainDone >= MainTotal)
        {
            GameManager.Instance?.CompleteQuest(QuestId, rewardXP);
            line += "\n" + Loc.T("Айбек: Азамат! Ты помог всем — теперь ты настоящий <b>айылдын досу</b>, друг айыла!",
                                 "Aibek: Азамат! You helped everyone — now you're a true <b>ayyldyn dosu</b>, a friend of the village!");
        }
        else
        {
            Task next = NextTask();
            if (next != null)
                line += "\n" + Loc.T("Айбек: Дальше сходи к ", "Aibek: Next, go to ") + Loc.T(next.whoRu, next.whoEn) + ". " +
                        Loc.T("Главных заданий: ", "Main quests: ") + MainDone + "/" + MainTotal + ".";
            else line += "\n" + Loc.T("Айбек: Ты сделал в айыле всё! Можешь просто гулять, играть на комузе и скакать на коне.", "Aibek: You've done everything in the ayil! Now just enjoy — play the komuz and ride horses.");
        }
        Say(line);
        UpdateQuestUI();
    }

    /// <summary>Айбек дарит фонарь-чырак (один раз), чтобы ночью было видно дорогу.</summary>
    void GiveLantern()
    {
        if (Inventory.Count("lantern") > 0 || SaveData.Get("lantern_given") == 1) return;
        SaveData.Set("lantern_given", 1);
        Inventory.Add("lantern");
        StartCoroutine(SayLater(15f, Loc.T("Айбек: Держи чырак — фонарь. Ночью в горах темно, включай его клавишей <b>F</b>.",
                                           "Aibek: Take this chyrak — a lantern. Mountain nights are dark, turn it on with <b>F</b>.")));
    }

    Task NextTask()
    {
        foreach (Task t in tasks) if (t.main && !t.done()) return t;
        foreach (Task t in tasks) if (!t.main && !t.done()) return t;
        return null;
    }

    IEnumerator SayLater(float s, string line) { yield return new WaitForSeconds(s); Say(line); }

    void Say(string line)
    {
        if (SpeechPanel.Instance != null && SpeechPanel.Instance.text != null) { SpeechPanel.Instance.text.text = line; return; }
        ShowMessage(line);
    }

    // Яблоко подобрано (ApplePickup)
    public void OnAppleCollected() { }

    // ------------------------------------------------------------------ панель заданий и стрелка

    public bool GetHintTarget(out Transform target, out float height)
    {
        target = null; height = 2.7f;
        if (aibek == null)
        {
            NPCInteraction npc = FindAnyObjectByType<NPCInteraction>();
            if (npc != null) aibek = npc.transform;
        }
        if (!Greeted || !Started || HasNews) { target = aibek; return target != null; }
        return false;   // остальные стрелки показывают сами задания жителей
    }

    private void UpdateQuestUI()
    {
        if (questText == null || tasks == null) return;
        string sub;
        if (!Greeted)
        {
            questText.text = "<b>Саламдашуу</b>\n<size=85%><color=#C9BFA6>" + Loc.T("Подойди к Айбеку и поздоровайся", "Go to Aibek and say hello") + "</color></size>";
            return;
        }
        if (!Started) sub = Loc.T("Поговори с Айбеком ещё раз (E)", "Talk to Aibek again (E)");
        else if (Finished) sub = HasNews ? Loc.T("Расскажи Айбеку новости", "Tell Aibek your news") : Loc.T("Айылдын досу — друг айыла!", "Ayyldyn dosu — friend of the village!");
        else if (HasNews) sub = Loc.T("Вернись к Айбеку — расскажи новости!", "Go back to Aibek — tell him the news!");
        else sub = Loc.T("Помогай жителям и рассказывай Айбеку", "Help the villagers and report to Aibek");
        string count = Started ? "  <color=" + (MainDone >= MainTotal ? "#5FC873" : "#E8B04A") + ">" + MainDone + "/" + MainTotal + "</color>" : "";
        questText.text = "<b>" + Loc.T("Айбек — гид", "Aibek — guide") + "</b>" + count + "\n<size=85%><color=#C9BFA6>" + sub + "</color></size>";
    }

    private void ShowMessage(string message)
    {
        if (message.StartsWith("Айбек") || message.StartsWith("Aibek")) GameAudio.Babble(message, 1.0f);
        if (messageText == null) return;
        messageText.text = message;
        Transform toast = messageText.transform.parent;
        if (toast != null && toast.name == "MessageToast")
        {
            toast.gameObject.SetActive(true);
            if (hideRoutine != null) StopCoroutine(hideRoutine);
            hideRoutine = StartCoroutine(HideAfter(toast.gameObject));
        }
    }

    private IEnumerator HideAfter(GameObject toast)
    {
        yield return new WaitForSeconds(messageDuration);
        toast.SetActive(false);
        hideRoutine = null;
    }
}
