using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Игровая аналитика для панели учителя (события уходят через MurasCloud, только если игрок вошёл в аккаунт):
///  • activity_time — сколько секунд игрок провёл в каждой мини-игре / окне / просто гуляя по селу;
///  • quest_started / quest_step — какие задания начаты и на каком шаге игрок остановился (где бросают);
///  • word_review / word_heard / test_result — отправляют сами окна словаря и теста.
/// Также запускает тест «до игры», предлагает тест «после игры» и считает минуты игры.
/// </summary>
public class GameAnalytics : MonoBehaviour
{
    public static GameAnalytics Instance { get; private set; }

    const float FlushEvery = 120f;           // накопленное время отправляем раз в 2 минуты (и при выходе)
    readonly Dictionary<string, float> seconds = new Dictionary<string, float>();
    float nextFlush, nextQuestScan, minuteAcc;
    string current;

    // задания: что уже отмечено как начатое и последний отправленный шаг
    readonly Dictionary<string, string> lastStep = new Dictionary<string, string>();
    static readonly Regex Tags = new Regex("<[^>]+>");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        var go = new GameObject("GameAnalytics");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<GameAnalytics>();
    }

    void Awake()
    {
        SceneManager.sceneLoaded += OnScene;
        nextFlush = Time.unscaledTime + FlushEvery;
    }

    void OnDestroy() { SceneManager.sceneLoaded -= OnScene; }

    void OnScene(Scene s, LoadSceneMode m)
    {
        Flush();
        lastStep.Clear();
        if (InVillage) StartCoroutine(VillageStart());
    }

    void Start() { if (InVillage) StartCoroutine(VillageStart()); }

    static bool InVillage => SceneManager.GetActiveScene().name != PauseMenu.MenuScene && GameManager.Instance != null;

    System.Collections.IEnumerator VillageStart()
    {
        // даём сцене загрузиться и приветствию появиться
        yield return new WaitForSecondsRealtime(1.5f);
        if (!KnowledgeTest.AutoRun) yield break;
        if (!KnowledgeTest.PreDone)
        {
            while (!Modal.Free || (SpeechPanel.Instance != null && SpeechPanel.Instance.IsShowing)) yield return new WaitForSecondsRealtime(0.5f);
            KnowledgeTest.StartPre();
            while (KnowledgeTest.IsOpen) yield return new WaitForSecondsRealtime(0.3f);
        }
        if (!Tutorial.Done) Tutorial.Begin();
    }

    // ------------------------------------------------------------------ что сейчас делает игрок
    static readonly Dictionary<string, string> ModalNames = new Dictionary<string, string>
    {
        { "KomuzGame", "komuz" }, { "ShyrdakPuzzle", "shyrdak" }, { "OrdoGame", "ordo" }, { "CookingGame", "cooking" },
        { "FishingSpot", "fishing" }, { "ManasStory", "manas" }, { "YurtDecorStation", "yurt_decor" }, { "JournalWindow", "dictionary" },
        { "InventoryWindow", "inventory" }, { "RaceMenu", "race_menu" },
    };

    public static string CurrentActivity()
    {
        if (PauseMenu.IsPaused) return null;                       // пауза не считается
        if (QuizWindow.Current != null) return QuizWindow.Current.Activity ?? "test";
        if (Modal.IsOpen && Modal.Current != null)
        {
            string n = Modal.Current.GetType().Name;
            return ModalNames.TryGetValue(n, out string id) ? id : "window";
        }
        if (RaceManager.Active) return "race";
        if (KokBoruGame.Active) return "kokboru";
        if (VillageShop.IsOpen) return "shop";
        var chuko = Object.FindAnyObjectByType<ChukoGameManager>();
        if (chuko != null && chuko.IsPlaying) return "chuko";
        if (SpeechPanel.Instance != null && SpeechPanel.Instance.IsShowing) return "dialogue";
        return "explore";
    }

    float nextActivityCheck;

    void Update()
    {
        if (!InVillage) return;
        float dt = Time.unscaledDeltaTime;
        if (dt > 1f) dt = 0f;                                      // свёрнутое окно / отладчик — не считаем
        if (Time.unscaledTime >= nextActivityCheck)
        {
            nextActivityCheck = Time.unscaledTime + 0.5f;           // FindAnyObjectByType дорогой — проверяем 2 раза в секунду
            current = CurrentActivity();
        }
        if (current != null)
        {
            seconds.TryGetValue(current, out float s);
            seconds[current] = s + dt;
            minuteAcc += dt;
            if (minuteAcc >= 60f) { minuteAcc -= 60f; SaveData.Add("play_min", 1); }
        }

        if (Time.unscaledTime >= nextQuestScan) { nextQuestScan = Time.unscaledTime + 1f; ScanQuestCard(); OfferPostTest(); }
        if (Time.unscaledTime >= nextFlush) { nextFlush = Time.unscaledTime + FlushEvery; Flush(); }
    }

    void OnApplicationPause(bool paused) { if (paused) Flush(); }
    void OnApplicationQuit() => Flush();

    public void Flush()
    {
        foreach (var kv in seconds)
        {
            int s = Mathf.RoundToInt(kv.Value);
            if (s >= 2) MurasCloud.Track("activity_time", CloudJson.Object("activity", kv.Key, "seconds", s));
        }
        seconds.Clear();
    }

    // ------------------------------------------------------------------ тест после игры
    bool postOffering;

    void OfferPostTest()
    {
        if (postOffering || !KnowledgeTest.AutoRun || !KnowledgeTest.ShouldOfferPost() || !Modal.Free) return;
        if (SpeechPanel.Instance != null && SpeechPanel.Instance.IsShowing) return;
        postOffering = true;
        KnowledgeTest.StartPost();
    }

    // ------------------------------------------------------------------ задания
    /// <summary>Основные задания — строки в карточке «ЗАДАНИЯ» (QuestText, YurtQuestText…).</summary>
    void ScanQuestCard()
    {
        GameObject card = GameObject.Find("QuestCard");
        if (card == null || GameManager.Instance == null) return;
        foreach (Transform c in card.transform)
        {
            string id = MainQuestId(c.name);
            if (id == null) continue;
            var t = c.GetComponent<TMP_Text>();
            if (t == null || !c.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(t.text)) continue;
            if (GameManager.Instance.IsQuestCompleted(id)) continue;
            QuestStep(id, t.text);
        }
    }

    static string MainQuestId(string objectName)
    {
        var gm = GameManager.Instance;
        switch (objectName)
        {
            case "QuestText": return gm.IsQuestCompleted("greeting") ? "guide" : "greeting";
            case "YurtQuestText": return "yurt";
            case "GamesQuestText": return "chuko";
            case "FoodQuestText": return gm.IsQuestCompleted("boorsok") ? "kuurdak" : "boorsok";
            default: return null;
        }
    }

    /// <summary>Шаг задания изменился (вызывают карточка заданий и SideQuestHud).</summary>
    public static void QuestStep(string id, string rawText)
    {
        if (Instance == null || string.IsNullOrEmpty(id)) return;
        string step = Tags.Replace(rawText ?? "", " ").Replace('\n', ' ').Trim();
        while (step.Contains("  ")) step = step.Replace("  ", " ");
        if (step.Length > 90) step = step.Substring(0, 90);
        if (SaveData.Get("qs_" + id) == 0)
        {
            SaveData.Set("qs_" + id, 1);                                 // «начато» отправляем один раз за прохождение
            MurasCloud.Track("quest_started", CloudJson.Object("id", id));
        }
        if (Instance.lastStep.TryGetValue(id, out string prev) && prev == step) return;
        Instance.lastStep[id] = step;
        MurasCloud.Track("quest_step", CloudJson.Object("id", id, "step", step));
    }

    /// <summary>Побочное задание исчезло из списка после того, как было начато, — считаем выполненным.</summary>
    public static void QuestCleared(string id)
    {
        if (Instance == null || SaveData.Get("qs_" + id) != 1) return;
        SaveData.Set("qs_" + id, 2);
        Instance.lastStep.Remove(id);
        MurasCloud.Track("quest_completed", CloudJson.Object("id", id, "xp", 0));
    }
}
