using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Капчык — инвентарь героя. Предметы: еда (даёт бодрость — бег быстрее на время), рыба (продаётся
/// торговцу Асану за XP) и памятные вещи (перо беркута, чүкө, кусочек шырдака).
/// Хранится в SaveData и стирается при «Новой игре». Окно — клавиша I или Tab (InventoryWindow).
/// </summary>
public static class Inventory
{
    public enum Kind { Food, Fish, Keepsake, Tool }

    public class Item
    {
        public string id, ky, ru, en, descRu, descEn;
        public Kind kind;
        public int sell;          // сколько XP даёт Асан (0 — не продаётся)
        public float buff;        // еда: ускорение бега (0.15 = +15%)
        public float buffTime;    // секунд
    }

    static Item I(string id, Kind k, string ky, string ru, string en, string dRu, string dEn, int sell = 0, float buff = 0f, float time = 0f) =>
        new Item { id = id, kind = k, ky = ky, ru = ru, en = en, descRu = dRu, descEn = dEn, sell = sell, buff = buff, buffTime = time };

    public static readonly List<Item> Items = new List<Item>
    {
        I("lantern", Kind.Tool, "Чырак", "Фонарь", "Lantern", "Подарок Айбека. Освещает дорогу ночью. Включить или выключить — клавиша F.", "A gift from Aibek. Lights your way at night. Turn it on or off with F."),
        I("alma", Kind.Food, "Алма", "Яблоко", "Apple", "Сочное яблоко из сада. Съешь — бег +10% на минуту.", "A juicy apple from the orchard. Eat it — run +10% for a minute.", 2, 0.10f, 60f),
        I("boorsok", Kind.Food, "Боорсок", "Боорсок", "Boorsok", "Горячие боорсоки от эже. Бег +15% на минуту.", "Hot boorsok from Ezhe. Run +15% for a minute.", 5, 0.15f, 60f),
        I("nan", Kind.Food, "Нан", "Лепёшка", "Flatbread", "Свежая лепёшка из тандыра. Бег +10% на полторы минуты.", "Fresh flatbread from the tandoor. Run +10% for a minute and a half.", 4, 0.10f, 90f),
        I("kymyz", Kind.Food, "Кымыз", "Кумыс", "Kymyz", "Кобылье молоко — награда победителю скачек. Бег +25% на минуту.", "Mare's milk — the race winner's prize. Run +25% for a minute.", 15, 0.25f, 60f),
        I("monko", Kind.Fish, "Мөңкө", "Горная форель", "Mountain trout", "Пятнистая форель из горной реки. Асан купит её.", "A spotted trout from the mountain river. Asan will buy it.", 10),
        I("chabak", Kind.Fish, "Чабак", "Чабак", "Chebak", "Серебристая рыбка. Асан купит её.", "A small silver fish. Asan will buy it.", 6),
        I("altyn", Kind.Fish, "Алтын мөңкө", "Золотая форель", "Golden trout", "Редчайшая золотая форель! Асан заплатит щедро.", "The rarest golden trout! Asan pays generously.", 40),
        I("feather", Kind.Keepsake, "Бүркүт канаты", "Перо беркута", "Eagle feather", "Подарок Болота. Перо беркута — оберег охотника.", "A gift from Bolot. An eagle feather is a hunter's charm."),
        I("chuko", Kind.Keepsake, "Чүкө", "Косточки чүкө", "Chükö bones", "Подарок детей: ты теперь умеешь играть в чүкө.", "A gift from the children: now you know how to play chükö."),
        I("kulakchyn", Kind.Keepsake, "Кулакчын", "Наушники Анаса", "Anas's headphones", "Нашлись в гнезде сороки. Верни их Анасу!", "Found in a magpie's nest. Return them to Anas!"),
        I("chaka", Kind.Keepsake, "Чака суу", "Ведро воды", "Bucket of water", "Вода из горной реки — для апы.", "Water from the mountain river — for Apa."),
        I("otun", Kind.Keepsake, "Отун", "Дрова", "Firewood", "Вязанка дров для байке. Нужно три.", "A bundle of firewood for Baike. Three are needed."),
        I("felt", Kind.Keepsake, "Кийиз кесими", "Кусочек войлока", "Piece of felt", "Апа подарила кусочек войлока с узором кочкор мүйүз.", "Apa gave you a piece of felt with the kochkor müyüz pattern."),
    };

    public static event Action Changed;

    public static Item Get(string id) => Items.Find(i => i.id == id);
    public static int Count(string id) => SaveData.Get("inv_" + id);
    public static int Total { get { int n = 0; foreach (Item it in Items) n += Count(it.id); return n; } }

    public static void Add(string id, int n = 1, bool toast = true)
    {
        Item it = Get(id);
        if (it == null || n <= 0) return;
        if (SceneManager.GetActiveScene().name == PauseMenu.MenuScene) return;
        SaveData.Add("inv_" + id, n);
        if (toast) MurasToast.Push(Loc.T("Капчыкка: ", "To your bag: ") + it.ky + (n > 1 ? " ×" + n : ""),
                                   Loc.T(it.ru, it.en) + "\n<size=85%><color=#C9BFA6>" + Loc.T("I — открыть капчык", "I — open your bag") + "</color></size>",
                                   new Color(0.62f, 0.44f, 0.24f), "tile_turn");
        Changed?.Invoke();
    }

    public static bool Remove(string id, int n = 1)
    {
        if (Count(id) < n) return false;
        SaveData.Set("inv_" + id, Count(id) - n);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Съесть / выпить.</summary>
    public static bool Use(string id)
    {
        Item it = Get(id);
        if (it != null && it.kind == Kind.Tool && Count(id) > 0)
        {
            if (id == "lantern") PlayerLantern.Toggle();
            return true;
        }
        if (it == null || it.kind != Kind.Food || !Remove(id)) return false;
        PlayerBuff.Give(1f + it.buff, it.buffTime, it.ky);
        MurasUI.Play("word_chime", 0.5f, 0.8f);
        return true;
    }

    /// <summary>Продать Асану за XP.</summary>
    public static bool Sell(string id)
    {
        Item it = Get(id);
        if (it == null || it.sell <= 0 || !Remove(id)) return false;
        if (GameManager.Instance != null) GameManager.Instance.AddXP(it.sell);
        MurasUI.Play("reel_tick", 0.8f, 1.3f);
        return true;
    }

    public static void ClearSaved() => Changed?.Invoke();

    /// <summary>Обновить окна (например, фонарь включили клавишей F).</summary>
    public static void Notify() => Changed?.Invoke();

    // ------------------------------------------------------------------ награды-предметы за задания

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        GameManager.QuestCompleted -= OnQuest;
        GameManager.QuestCompleted += OnQuest;
    }

    static void OnQuest(string id, int xp)
    {
        switch (id)
        {
            case "chuko": Add("chuko", 3); break;
            case "boorsok": Add("boorsok", 5); break;
            case "kuurdak": Add("nan", 2); break;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Changed = null;
}

/// <summary>Временная бодрость от еды: множитель скорости бега и ходьбы.</summary>
public static class PlayerBuff
{
    public static float Multiplier => Time.time < until ? mult : 1f;
    public static float SecondsLeft => Mathf.Max(0f, until - Time.time);
    public static string Source { get; private set; }

    private static float until, mult = 1f;

    public static void Give(float multiplier, float seconds, string source)
    {
        mult = Mathf.Max(Time.time < until ? mult : 1f, multiplier);
        until = Mathf.Max(until, Time.time) + seconds * (Time.time < until ? 0.5f : 1f);
        if (until - Time.time > 180f) until = Time.time + 180f;
        Source = source;
        MurasToast.Push(Loc.T("Бодрость!", "Energy!"), "<b>" + source + "</b> — " + Loc.T("бег быстрее на ", "faster running for ") + Mathf.RoundToInt(SecondsLeft) + Loc.T(" с", " s"),
                        new Color(0.45f, 0.72f, 0.38f), null);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { until = 0f; mult = 1f; Source = null; }
}
