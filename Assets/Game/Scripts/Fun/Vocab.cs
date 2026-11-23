using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Сөздүк — словарь кыргызских слов. Слова открываются по ходу игры: за задания, разговоры,
/// мини-игры, покупки на базаре и посещение мест (ферма, река…). За каждое новое слово +5 XP.
/// Окно словаря — клавиша J (JournalWindow).
/// </summary>
public static class Vocab
{
    public enum Cat { Salam, Yurt, Food, Games, Clothes, Nature, Ornament }

    public class Word
    {
        public string id, ky, latin, ru, en, hintRu, hintEn;
        public Cat cat;
    }

    public const int XpPerWord = 5;

    static Word W(string id, Cat cat, string ky, string latin, string ru, string en, string hintRu, string hintEn) =>
        new Word { id = id, cat = cat, ky = ky, latin = latin, ru = ru, en = en, hintRu = hintRu, hintEn = hintEn };

    public static readonly List<Word> Words = new List<Word>
    {
        // Салам — люди и вежливость
        W("salam", Cat.Salam, "Салам", "salam", "Привет", "Hello", "Поздоровайся с Айбеком", "Greet Aibek"),
        W("rahmat", Cat.Salam, "Рахмат", "rakhmat", "Спасибо", "Thank you", "Помоги Айбеку", "Help Aibek"),
        W("kosh", Cat.Salam, "Кош келиңиз", "kosh keliŋiz", "Добро пожаловать", "Welcome", "Начни игру в айыле", "Start the game in the village"),
        W("apa", Cat.Salam, "Апа", "apa", "Мама; так уважительно зовут пожилую женщину", "Mother; also a respectful word for an elderly woman", "Поговори с Апой", "Talk to Apa"),
        W("baike", Cat.Salam, "Байке", "baike", "Старший брат; обращение к мужчине старше тебя", "Elder brother; how you address an older man", "Поговори с Байке", "Talk to Baike"),
        W("ezhe", Cat.Salam, "Эже", "ezhe", "Старшая сестра; обращение к женщине старше", "Elder sister; how you address an older woman", "Готовь с эже на кухне", "Cook with Ezhe"),
        W("aksakal", Cat.Salam, "Аксакал", "aksakal", "Старейшина (букв. «белая борода»)", "Village elder (lit. \"white beard\")", "Построй юрту с аксакалом", "Build the yurt with the aksakal"),
        W("bala", Cat.Salam, "Бала", "bala", "Ребёнок", "Child", "Сыграй с детьми в чүкө", "Play chүkө with the children"),
        W("soodager", Cat.Salam, "Соодагер", "soodager", "Торговец", "Trader", "Загляни на базар", "Visit the bazaar"),

        // Боз үй — юрта
        W("boz_uy", Cat.Yurt, "Боз үй", "boz üy", "Юрта (букв. «серый дом»)", "Yurt (lit. \"grey house\")", "Задание аксакала", "The aksakal's quest"),
        W("kerege", Cat.Yurt, "Кереге", "kerege", "Решётчатые стены юрты", "The lattice walls of a yurt", "Задание аксакала", "The aksakal's quest"),
        W("uuk", Cat.Yurt, "Уук", "uuk", "Жерди купола юрты", "The roof poles of a yurt", "Задание аксакала", "The aksakal's quest"),
        W("tunduk", Cat.Yurt, "Түндүк", "tündük", "Круглый верх юрты — он изображён на флаге Кыргызстана", "The crown ring of a yurt — it is on the flag of Kyrgyzstan", "Задание аксакала", "The aksakal's quest"),
        W("kiyiz", Cat.Yurt, "Кийиз", "kiyiz", "Войлок, которым покрывают юрту", "Felt that covers the yurt", "Задание аксакала", "The aksakal's quest"),
        W("tushkiyiz", Cat.Yurt, "Түшкийиз", "tüshkiyiz", "Вышитый настенный ковёр", "Embroidered wall hanging", "Зайди в дом", "Go inside a house"),
        W("juk", Cat.Yurt, "Жүк", "jük", "Стопка одеял и подушек у стены", "A stack of blankets and pillows by the wall", "Зайди в дом", "Go inside a house"),

        // Тамак — еда
        W("alma", Cat.Food, "Алма", "alma", "Яблоко", "Apple", "Собери яблоки для Айбека", "Collect apples for Aibek"),
        W("boorsok", Cat.Food, "Боорсок", "boorsok", "Жареные кусочки теста", "Fried pieces of dough", "Готовь с эже", "Cook with Ezhe"),
        W("kuurdak", Cat.Food, "Куурдак", "kuurdak", "Жареное мясо с луком и картошкой", "Fried meat with onion and potatoes", "Готовь с эже", "Cook with Ezhe"),
        W("dastorkon", Cat.Food, "Дасторкон", "dastorkon", "Скатерть с угощением, накрытый стол", "A tablecloth laid with food for guests", "Готовь с эже", "Cook with Ezhe"),
        W("nan", Cat.Food, "Нан", "nan", "Хлеб, лепёшка", "Bread, flatbread", "Готовь с эже", "Cook with Ezhe"),
        W("chay", Cat.Food, "Чай", "chay", "Чай", "Tea", "Готовь с эже", "Cook with Ezhe"),
        W("kymyz", Cat.Food, "Кымыз", "kymyz", "Кобылье молоко — любимый напиток кочевников", "Fermented mare's milk, the nomads' favourite drink", "Выиграй скачки", "Win the horse race"),

        // Оюндар — игры и музыка
        W("chuko", Cat.Games, "Чүкө", "chükö", "Косточки для детской игры", "Knucklebones for a children's game", "Сыграй с детьми", "Play with the children"),
        W("at_chabysh", Cat.Games, "Ат чабыш", "at chabysh", "Скачки на лошадях", "Horse racing", "Поучаствуй в скачках", "Take part in the horse race"),
        W("chabandes", Cat.Games, "Чабандес", "chabandes", "Всадник на скачках", "Jockey, race rider", "Поговори с Эрмеком", "Talk to Ermek"),
        W("komuz", Cat.Games, "Комуз", "komuz", "Трёхструнный кыргызский инструмент", "Three-stringed Kyrgyz instrument", "Сыграй с Нурланом", "Play with Nurlan"),
        W("kuu", Cat.Games, "Күү", "küü", "Мелодия для комуза без слов", "An instrumental komuz piece", "Сыграй с Нурланом", "Play with Nurlan"),
        W("komuzchu", Cat.Games, "Комузчу", "komuzchu", "Музыкант, играющий на комузе", "A komuz player", "Сыграй с Нурланом", "Play with Nurlan"),

        // Кийим — одежда
        W("kalpak", Cat.Clothes, "Ак калпак", "ak kalpak", "Белая войлочная шапка", "White felt hat", "Загляни на базар", "Visit the bazaar"),
        W("tebetey", Cat.Clothes, "Тебетей", "tebetey", "Меховая шапка", "Fur hat", "Купи на базаре", "Buy it at the bazaar"),
        W("chapan", Cat.Clothes, "Чапан", "chapan", "Длинный халат", "Long robe", "Купи на базаре", "Buy it at the bazaar"),
        W("elechek", Cat.Clothes, "Элечек", "elechek", "Белый головной убор замужней женщины", "White headdress of a married woman", "Поговори с Апой", "Talk to Apa"),
        W("otuk", Cat.Clothes, "Өтүк", "ötük", "Сапоги", "Boots", "Купи на базаре", "Buy it at the bazaar"),

        // Табият — природа и животные
        W("ail", Cat.Nature, "Айыл", "ayyl", "Село", "Village", "Начни игру", "Start the game"),
        W("too", Cat.Nature, "Тоо", "too", "Гора", "Mountain", "Начни игру", "Start the game"),
        W("suu", Cat.Nature, "Суу", "suu", "Вода", "Water", "Подойди к реке", "Go to the river"),
        W("dariya", Cat.Nature, "Дарыя", "dariya", "Река", "River", "Подойди к реке", "Go to the river"),
        W("jailoo", Cat.Nature, "Жайлоо", "jayloo", "Летнее горное пастбище", "Summer mountain pasture", "Поговори с чабаном", "Talk to the shepherd"),
        W("koy", Cat.Nature, "Кой", "koy", "Овца", "Sheep", "Сходи на ферму", "Visit the farm"),
        W("echki", Cat.Nature, "Эчки", "echki", "Коза", "Goat", "Сходи на ферму", "Visit the farm"),
        W("uy", Cat.Nature, "Уй", "uy", "Корова", "Cow", "Сходи на ферму", "Visit the farm"),
        W("at", Cat.Nature, "Ат", "at", "Лошадь", "Horse", "Сходи на ферму", "Visit the farm"),
        W("it", Cat.Nature, "Ит", "it", "Собака", "Dog", "Сходи на ферму", "Visit the farm"),
        W("chaban", Cat.Nature, "Чабан", "chaban", "Пастух", "Shepherd", "Поговори с чабаном", "Talk to the shepherd"),
        W("burkut", Cat.Nature, "Бүркүт", "bürküt", "Беркут — горный орёл", "Golden eagle", "Поговори с Болотом", "Talk to Bolot"),
        W("burkutchu", Cat.Nature, "Бүркүтчү", "bürkütchü", "Охотник с беркутом", "Eagle hunter", "Поговори с Болотом", "Talk to Bolot"),
        W("salbuurun", Cat.Nature, "Салбуурун", "salbuurun", "Старинная охота с беркутом, собаками и луком", "Traditional hunting with eagles, hounds and bows", "Посмотри полёт беркута", "Watch the eagle fly"),
        W("balyk", Cat.Nature, "Балык", "balyk", "Рыба", "Fish", "Порыбачь на реке", "Go fishing"),
        W("monko", Cat.Nature, "Мөңкө", "möŋkö", "Горная форель", "Mountain trout", "Поймай форель", "Catch a trout"),
        W("jamgyr", Cat.Nature, "Жамгыр", "jamgyr", "Дождь", "Rain", "Попади под дождь", "Get caught in the rain"),
        W("kar", Cat.Nature, "Кар", "kar", "Снег", "Snow", "Дождись снега", "Wait for snow"),
        W("tun", Cat.Nature, "Түн", "tün", "Ночь", "Night", "Дождись ночи (T — ускорить время)", "Wait for night (T speeds up time)"),
        W("bazar", Cat.Nature, "Базар", "bazar", "Рынок", "Market", "Загляни на базар", "Visit the bazaar"),

        // Оюу — орнаменты
        W("shyrdak", Cat.Ornament, "Шырдак", "shyrdak", "Войлочный ковёр с мозаичным узором", "Felt carpet with a mosaic pattern", "Помоги Апе со шырдаком", "Help Apa with the shyrdak"),
        W("oyu", Cat.Ornament, "Оюу", "oyuu", "Узор, орнамент", "Pattern, ornament", "Помоги Апе со шырдаком", "Help Apa with the shyrdak"),
        W("tumar", Cat.Ornament, "Тумар", "tumar", "Оберег; треугольный узор, хранит от беды", "Amulet; a triangle pattern that protects from harm", "Шырдак: узор 1", "Shyrdak: pattern 1"),
        W("kochkor", Cat.Ornament, "Кочкор мүйүз", "kochkor müyüz", "«Рога барана» — узор богатства и силы", "\"Ram's horns\" — a pattern of wealth and strength", "Шырдак: узор 2", "Shyrdak: pattern 2"),
        W("it_kuyruk", Cat.Ornament, "Ит куйрук", "it kuyruk", "«Собачий хвост» — узор-волна, символ верности", "\"Dog's tail\" — a wave pattern of loyalty", "Шырдак: узор 3", "Shyrdak: pattern 3"),
    };

    public static event System.Action Changed;

    public static Word Get(string id) => Words.Find(w => w.id == id);
    public static bool Known(string id) => SaveData.Get("w_" + id) == 1;
    public static int KnownCount { get { int n = 0; foreach (Word w in Words) if (Known(w.id)) n++; return n; } }
    public static int Total => Words.Count;

    public static string CatName(Cat c) => c switch
    {
        Cat.Salam => Loc.T("Салам · люди", "Салам · people"),
        Cat.Yurt => Loc.T("Боз үй · юрта", "Боз үй · yurt"),
        Cat.Food => Loc.T("Тамак · еда", "Тамак · food"),
        Cat.Games => Loc.T("Оюндар · игры", "Оюндар · games"),
        Cat.Clothes => Loc.T("Кийим · одежда", "Кийим · clothes"),
        Cat.Nature => Loc.T("Табият · природа", "Табият · nature"),
        _ => Loc.T("Оюу · узоры", "Оюу · patterns"),
    };

    /// <summary>Открыть слова. Новые показываются плашкой, за каждое +5 XP.</summary>
    public static void Unlock(params string[] ids)
    {
        var fresh = new List<Word>();
        foreach (string id in ids)
        {
            Word w = Get(id);
            if (w == null || Known(id)) continue;
            SaveData.Set("w_" + id, 1);
            fresh.Add(w);
        }
        if (fresh.Count == 0) return;

        if (GameManager.Instance != null) GameManager.Instance.AddXP(XpPerWord * fresh.Count);
        string title = (fresh.Count == 1 ? "Жаңы сөз" : "Жаңы сөздөр") + "  +" + XpPerWord * fresh.Count + " XP";
        string body;
        if (fresh.Count == 1) body = "<b>" + fresh[0].ky + "</b> — " + Loc.T(fresh[0].ru, fresh[0].en);
        else
        {
            var names = new List<string>();
            foreach (Word w in fresh) names.Add("<b>" + w.ky + "</b>");
            body = string.Join(", ", names) + "\n<size=85%><color=#C9BFA6>" + Loc.T("Открой словарь — J", "Open the dictionary — J") + "</color></size>";
        }
        MurasToast.Push(title, body, MurasUI.Gold, "word_chime");
        Changed?.Invoke();
        Achievements.CheckWords();
    }

    public static void ClearSaved() => Changed?.Invoke();

    // ------------------------------------------------------------------ откуда берутся слова

    static readonly Dictionary<string, string[]> QuestWords = new Dictionary<string, string[]>
    {
        { "greeting", new[] { "salam", "rahmat" } },
        { "apples", new[] { "alma" } },
        { "yurt", new[] { "boz_uy", "kerege", "uuk", "tunduk", "kiyiz", "aksakal" } },
        { "chuko", new[] { "chuko", "bala" } },
        { "boorsok", new[] { "boorsok", "ezhe", "dastorkon" } },
        { "kuurdak", new[] { "kuurdak", "nan", "chay" } },
    };

    static readonly Dictionary<string, string> ShopWords = new Dictionary<string, string>
    {
        { "chapan", "chapan" }, { "batyr", "chapan" }, { "tebetey", "tebetey" }, { "boots", "otuk" }, { "horse", "at" }, { "dog", "it" },
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        GameManager.QuestCompleted -= OnQuest;
        GameManager.QuestCompleted += OnQuest;
        ShopCatalog.Changed -= OnShop;
        ShopCatalog.Changed += OnShop;
    }

    static void OnQuest(string id, int xp)
    {
        if (QuestWords.TryGetValue(id, out string[] w)) Unlock(w);
        Achievements.CheckQuests();
    }

    static void OnShop()
    {
        if (SceneManager.GetActiveScene().name == PauseMenu.MenuScene) return;
        foreach (var kv in ShopWords)
            if (ShopCatalog.Owns(kv.Key) && !Known(kv.Value)) Unlock(kv.Value);
        Achievements.CheckShop();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Changed = null;
}
