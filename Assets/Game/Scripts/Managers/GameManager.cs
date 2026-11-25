using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("UI")]
    public TMP_Text xpText;

    [Header("Отладка")]
    [Tooltip("Только в редакторе Unity: при каждом нажатии Play прогресс сбрасывается и игра начинается заново. " +
             "Сними галочку, чтобы проверить сохранение между запусками. В собранной игре прогресс всегда сохраняется.")]
    public bool resetProgressOnPlayInEditor = true;

    public int CurrentXP { get; private set; }

    /// <summary>Игра запущена из главного меню — в редакторе не сбрасываем прогресс (иначе «Продолжить» не работает).</summary>
    public static bool LaunchedFromMenu;

    /// <summary>Квест выполнен впервые: (id квеста, награда XP)</summary>
    public static event System.Action<string, int> QuestCompleted;

    private const string XPKey = "MURAS_TotalXP";
    private const string QuestKey = "MURAS_Quest_";

    private static readonly string[] QuestIds =
    {
        "greeting", "guide", "yurt", "chuko", "boorsok", "kuurdak"
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

#if UNITY_EDITOR
        if (resetProgressOnPlayInEditor && !LaunchedFromMenu)
        {
            ClearSavedProgress();
            Debug.Log("MURAS: прогресс сброшен для теста (GameManager → Reset Progress On Play In Editor).");
        }
#endif

        CurrentXP = PlayerPrefs.GetInt(XPKey, 0);

        UpdateXPText();
    }

    private void OnEnable()
    {
        if (Instance == this)
            SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        xpText = null;

        GameObject xpObject = GameObject.Find("XPText");

        if (xpObject != null)
            xpText = xpObject.GetComponent<TMP_Text>();

        UpdateXPText();
    }

    public void AddXP(int amount)
    {
        if (amount <= 0)
            return;

        CurrentXP += amount;

        PlayerPrefs.SetInt(XPKey, CurrentXP);
        PlayerPrefs.Save();

        UpdateXPText();

        Debug.Log(
            "XP added: " + amount +
            " | Total XP: " + CurrentXP
        );
    }

    /// <summary>Потратить опыт на базаре. false — если не хватает.</summary>
    public bool SpendXP(int amount)
    {
        if (amount < 0 || amount > CurrentXP)
            return false;

        CurrentXP -= amount;

        PlayerPrefs.SetInt(XPKey, CurrentXP);
        PlayerPrefs.Save();

        UpdateXPText();
        return true;
    }

    public bool IsQuestCompleted(string questId)
    {
        if (string.IsNullOrEmpty(questId))
            return false;

        return PlayerPrefs.GetInt(
            QuestKey + questId, 0
        ) == 1;
    }

    public bool CompleteQuest(string questId, int reward)
    {
        if (string.IsNullOrEmpty(questId))
            return false;

        if (IsQuestCompleted(questId))
        {
            Debug.Log(
                "Quest already completed: " + questId
            );

            return false;
        }

        PlayerPrefs.SetInt(
            QuestKey + questId, 1
        );

        AddXP(reward);

        PlayerPrefs.Save();

        GameAudio.QuestComplete();
        QuestCompleted?.Invoke(questId, reward);

        Debug.Log(
            "Quest completed: " + questId
        );

        return true;
    }

    public void UpdateXPText()
    {
        if (xpText != null)
            xpText.text = "XP: " + CurrentXP;
    }

    [ContextMenu("Reset Progress")]
    public void ResetProgress()
    {
        ClearSavedProgress();

        CurrentXP = 0;

        UpdateXPText();

        Debug.Log("MURAS progress reset.");
    }

    /// <summary>Сохранённый прогресс без загрузки села (для главного меню).</summary>
    public static int SavedXP => PlayerPrefs.GetInt(XPKey, 0);
    public static int TotalQuests => QuestIds.Length;
    public static int SavedQuestCount
    {
        get
        {
            int n = 0;
            foreach (string id in QuestIds)
                if (PlayerPrefs.GetInt(QuestKey + id, 0) == 1) n++;
            return n;
        }
    }
    public static bool HasSave => SavedXP > 0 || SavedQuestCount > 0;

    /// <summary>Выполненные основные задания (для облачного сохранения).</summary>
    public static List<string> CompletedQuestIds()
    {
        var list = new List<string>();
        foreach (string id in QuestIds)
            if (PlayerPrefs.GetInt(QuestKey + id, 0) == 1) list.Add(id);
        return list;
    }

    /// <summary>Применить прогресс из облака: XP и выполненные задания (только известные игре).</summary>
    public static void ApplyCloudProgress(int xp, IEnumerable<string> quests)
    {
        PlayerPrefs.SetInt(XPKey, Mathf.Max(0, xp));
        var set = new HashSet<string>(quests);
        foreach (string id in QuestIds)
            if (set.Contains(id)) PlayerPrefs.SetInt(QuestKey + id, 1);
        PlayerPrefs.Save();
        if (Instance != null) { Instance.CurrentXP = Mathf.Max(0, xp); Instance.UpdateXPText(); }
    }

    /// <summary>Стирает сохранённые XP и квесты. Можно вызывать без GameManager (например, из меню «Новая игра»).</summary>
    public static void ClearSavedProgress()
    {
        PlayerPrefs.DeleteKey(XPKey);

        foreach (string id in QuestIds)
            PlayerPrefs.DeleteKey(QuestKey + id);

        PlayerPrefs.Save();

        ShopCatalog.ClearSaved();   // покупки на базаре тоже сбрасываются
        SaveData.Clear();           // словарь, достижения, рекорды мини-игр
        Vocab.ClearSaved();
        Achievements.ClearSaved();
        Inventory.ClearSaved();

        if (Instance != null)
        {
            Instance.CurrentXP = 0;
            Instance.UpdateXPText();
        }
    }
}
