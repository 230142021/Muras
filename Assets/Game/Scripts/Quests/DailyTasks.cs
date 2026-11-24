using UnityEngine.SceneManagement;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Күнүмдүк иштер — утренние задания жителей. Каждое игровое утро (в 6:00) выпадают 3 задания из 4:
///  • суу  — набрать ведро воды у реки и принести апе;
///  • кой  — пересчитать трёх овец чабана (подойти к овце, E);
///  • нан  — взять лепёшку из корзины у кухни и отнести аксакалу;
///  • отун — собрать три вязанки дров и отнести байке.
/// Доставка срабатывает сама, когда подходишь к жителю с нужной вещью. За задание +20 XP, за все три — ещё +30.
/// </summary>
public class DailyTasks : MonoBehaviour
{
    public static DailyTasks Instance { get; private set; }

    public enum T { Water, Sheep, Bread, Wood }
    public const int XpTask = 20, XpAll = 30;
    static readonly int[] Need = { 1, 3, 1, 3 };

    private readonly List<Transform> sheep = new List<Transform>();
    private readonly HashSet<Transform> counted = new HashSet<Transform>();
    private Transform player, apa, aksakal, baike;
    private float lastHour = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        Spawn();
    }

    static void OnLoaded(Scene s, LoadSceneMode m) => Spawn();

    static void Spawn()
    {
        if (Object.FindAnyObjectByType<DayNightCycle>() == null || Instance != null) return;     // только в сцене села
        new GameObject("DailyTasks").AddComponent<DailyTasks>();
    }

    void Awake() { Instance = this; }

    void Start()
    {
        var pm = FindAnyObjectByType<PlayerMovement>(); if (pm != null) player = pm.transform;
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.name.StartsWith("Sheep") && t.GetComponent<AnimalWander>() != null) sheep.Add(t);
            if (t.name == "Aksakal") aksakal = t;
        }
        foreach (var v in FindObjectsByType<VillagerTalk>(FindObjectsSortMode.None))
        {
            if (v.speakerKey == "Апа") apa = v.transform;
            if (v.speakerKey == "Байке") baike = v.transform;
        }
        if (SaveData.Get("daily_day") == 0) NewDay(false);
        UpdateHud();
    }

    // ------------------------------------------------------------------ день

    public static int Day => SaveData.Get("daily_day");
    public static T TaskAt(int i) => (T)SaveData.Get("daily_t" + i);
    public static int Progress(int i) => SaveData.Get("daily_p" + i);
    public static bool Has(T t) { for (int i = 0; i < 3; i++) if (TaskAt(i) == t) return true; return false; }
    static int Slot(T t) { for (int i = 0; i < 3; i++) if (TaskAt(i) == t) return i; return -1; }
    public static bool Done(T t) { int s = Slot(t); return s >= 0 && Progress(s) >= Need[(int)t]; }

    public void NewDay(bool announce)
    {
        SaveData.Set("daily_day", Day + 1);
        var pool = new List<int> { 0, 1, 2, 3 };
        var rnd = new System.Random(Day * 7919 + 13);
        pool.RemoveAt(rnd.Next(pool.Count));            // одно из четырёх сегодня отдыхает
        for (int i = 0; i < 3; i++) { SaveData.Set("daily_t" + i, pool[i]); SaveData.Set("daily_p" + i, 0); }
        SaveData.Set("daily_bonus", 0);
        SaveData.Set("daily_spots", 0);                  // вязанки и ведро появляются снова
        Inventory.Remove("chaka", Inventory.Count("chaka"));
        Inventory.Remove("otun", Inventory.Count("otun"));
        counted.Clear();
        foreach (var s in FindObjectsByType<DailySpot>(FindObjectsInactive.Include, FindObjectsSortMode.None)) s.Respawn();
        if (announce)
        {
            GameAudio.Interact();
            MurasToast.Push(Loc.T("Жаңы күн!  Күнүмдүк иштер", "A new day!  Daily tasks"),
                            Loc.T("Жители просят о помощи — смотри список заданий слева", "Villagers need help — see the quest list on the left"), new Color(0.84f, 0.66f, 0.29f), "word_chime");
        }
        UpdateHud();
    }

    void Update()
    {
        float h = DayNightCycle.Hour;
        if (lastHour >= 0f && lastHour < 6f && h >= 6f) NewDay(true);
        lastHour = h;

        if (player == null || !Modal.Free || PauseMenu.IsPaused) return;

        // овцы: подойти и нажать E
        if (Has(T.Sheep) && !Done(T.Sheep))
            foreach (Transform s in sheep)
            {
                if (s == null || counted.Contains(s)) continue;
                Vector3 d = s.position - player.position; d.y = 0f;
                if (d.sqrMagnitude > 2.4f * 2.4f) continue;
                InteractionHint.Request("<b><color=#E8B04A>E</color></b>  —  " + Loc.T("пересчитать овцу (кой)", "count the sheep (кой)"));
                if (Input.GetKeyDown(KeyCode.E))
                {
                    counted.Add(s);
                    var aw = s.GetComponent<AnimalWander>();
                    if (aw != null && aw.calls != null && aw.calls.Length > 0) AudioSource.PlayClipAtPoint(aw.calls[Random.Range(0, aw.calls.Length)], s.position, 0.8f);
                    Vocab.Unlock("koy");
                    Advance(T.Sheep, 1);
                }
                break;
            }

        // доставка: подошёл с вещью к жителю — отдал
        Deliver(T.Water, apa, "chaka", "Апа", Loc.T("Ой, рахмат, балам! Свежая суу из горной реки — сейчас чай заварю.", "Oh, рахмат, child! Fresh суу from the mountain river — I'll make tea now."));
        Deliver(T.Bread, aksakal, "nan", "Аксакал", Loc.T("Ыракмат, балам! Нан — святой хлеб, его всегда несут двумя руками. Пусть твой дастаркон будет полным.", "Ыракмат, child! Нан is sacred bread — always carried with both hands. May your дасторкон be full."));
        Deliver(T.Wood, baike, "otun", "Байке", Loc.T("Мына, азамат! Отун на зиму — дело серьёзное. Теперь в юрте будет тепло.", "There, well done! Отун for the winter is serious business. Now the yurt will be warm."), 3);
    }

    void Deliver(T task, Transform who, string item, string speaker, string thanks, int count = 1)
    {
        if (who == null || !Has(task) || Done(task) || Inventory.Count(item) < count) return;
        Vector3 d = who.position - player.position; d.y = 0f;
        if (d.sqrMagnitude > 2.8f * 2.8f) return;
        Inventory.Remove(item, count);
        QuestTalk.Say(speaker, thanks);
        Advance(task, Need[(int)task]);
    }

    public void Advance(T task, int n)
    {
        int s = Slot(task);
        if (s < 0) return;
        int before = Progress(s);
        SaveData.Set("daily_p" + s, Mathf.Min(Need[(int)task], before + n));
        if (before < Need[(int)task] && Progress(s) >= Need[(int)task])
        {
            if (GameManager.Instance != null) GameManager.Instance.AddXP(XpTask);
            GameAudio.Correct();
            MurasToast.Push(Loc.T("Күнүмдүк иш аткарылды  +", "Daily task done  +") + XpTask + " XP", Name(task), new Color(0.45f, 0.72f, 0.38f), "word_chime");
            if (Done(TaskAt(0)) && Done(TaskAt(1)) && Done(TaskAt(2)) && SaveData.Get("daily_bonus") == 0)
            {
                SaveData.Set("daily_bonus", 1);
                SaveData.Add("daily_total", 1);
                if (GameManager.Instance != null) GameManager.Instance.AddXP(XpAll);
                Achievements.Give("daily");
                GameAudio.QuestComplete();
                MurasToast.Push(Loc.T("Бардык иштер бүттү!  +", "All tasks done!  +") + XpAll + " XP", Loc.T("Айыл сага ыраазы — село тебе благодарно. Новые задания — завтра утром.", "The village is grateful. New tasks tomorrow morning."), new Color(0.84f, 0.66f, 0.29f), "achievement");
            }
        }
        UpdateHud();
    }

    // ------------------------------------------------------------------ панель заданий

    static string Name(T t) => t switch
    {
        T.Water => Loc.T("Апага суу алып бер — принеси апе воды", "Апага суу — bring Apa water"),
        T.Sheep => Loc.T("Чабанга жардам — пересчитай 3 овец", "Help the shepherd — count 3 sheep"),
        T.Bread => Loc.T("Аксакалга нан — отнеси аксакалу лепёшку", "Аксакалга нан — take bread to the aksakal"),
        _ => Loc.T("Байкеге отун — собери 3 вязанки дров", "Байкеге отун — gather 3 bundles of firewood"),
    };

    static string Hint(T t, int p) => t switch
    {
        T.Water => Inventory.Count("chaka") > 0 ? Loc.T("неси ведро апе", "carry the bucket to Apa") : Loc.T("ведро — у реки, у пирса", "the bucket is by the river, at the pier"),
        T.Sheep => Loc.T($"овцы на ферме  {p}/3", $"sheep at the farm  {p}/3"),
        T.Bread => Inventory.Count("nan") > 0 ? Loc.T("неси нан аксакалу у юрты", "take the bread to the aksakal by the yurt") : Loc.T("корзина с нан — у кухни", "the bread basket is by the kitchen"),
        _ => Loc.T($"вязанки по селу  {Mathf.Min(3, Inventory.Count("otun"))}/3", $"bundles around the village  {Mathf.Min(3, Inventory.Count("otun"))}/3"),
    };

    public static void UpdateHud()
    {
        if (Day == 0) { SideQuestHud.Set("daily", null, null); return; }
        var sb = new System.Text.StringBuilder();
        int done = 0;
        for (int i = 0; i < 3; i++)
        {
            T t = TaskAt(i);
            bool ok = Done(t); if (ok) done++;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(ok ? "<color=#7FBF6A>+ </color><s>" + Name(t) + "</s>" : "• " + Name(t) + " <color=#9C9283>(" + Hint(t, Progress(i)) + ")</color>");
        }
        SideQuestHud.Set("daily", Loc.T($"Күнүмдүк иштер  {done}/3", $"Daily tasks  {done}/3"), sb.ToString());
    }

    public static void TestNewDay() { if (Instance != null) Instance.NewDay(true); }
}

/// <summary>Точка для утренних заданий: ведро у реки, корзина с нан, вязанки дров. E — взять.</summary>
public class DailySpot : MonoBehaviour
{
    public DailyTasks.T task;
    public string item = "chaka";
    public int bit;                  // какой бит в "daily_spots" отмечает, что вещь уже взята сегодня
    public float radius = 2.2f;
    public bool reusable;            // корзина с нан — бери сколько нужно
    private Transform player;
    private Renderer[] rends;

    void Start() { var pm = FindAnyObjectByType<PlayerMovement>(); if (pm != null) player = pm.transform; rends = GetComponentsInChildren<Renderer>(true); }

    bool Taken => !reusable && (SaveData.Get("daily_spots") & (1 << bit)) != 0;

    public void Respawn() { if (rends != null) foreach (var r in rends) r.enabled = true; }

    void Update()
    {
        bool active = DailyTasks.Has(task) && !DailyTasks.Done(task);
        bool visible = reusable || (active && !Taken);
        if (rends != null) foreach (var r in rends) if (r.enabled != visible) r.enabled = visible;
        if (!active || Taken || player == null || !Modal.Free || PauseMenu.IsPaused) return;
        if (task == DailyTasks.T.Bread && Inventory.Count("nan") > 0) return;
        if (task == DailyTasks.T.Water && Inventory.Count("chaka") > 0) return;
        Vector3 d = player.position - transform.position; d.y = 0f;
        if (d.sqrMagnitude > radius * radius) return;
        string what = task == DailyTasks.T.Water ? Loc.T("набрать ведро воды", "fill a bucket of water")
                    : task == DailyTasks.T.Bread ? Loc.T("взять лепёшку (нан)", "take a flatbread (нан)")
                    : Loc.T("взять вязанку дров (отун)", "take a bundle of firewood (отун)");
        InteractionHint.Request("<b><color=#E8B04A>E</color></b>  —  " + what);
        if (!Input.GetKeyDown(KeyCode.E)) return;
        Inventory.Add(item);
        if (!reusable) SaveData.Set("daily_spots", SaveData.Get("daily_spots") | (1 << bit));
        Vocab.Unlock(task == DailyTasks.T.Water ? "chaka" : task == DailyTasks.T.Bread ? "nan" : "otun");
        GameAudio.Pickup();
        DailyTasks.UpdateHud();
    }
}
