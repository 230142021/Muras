using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Жетишкендиктер — достижения. За каждое +25 XP и плашка с фанфарой. Список — во вкладке словаря (J).</summary>
public static class Achievements
{
    public class Ach { public string id, ky, ru, en, descRu, descEn; }
    public const int Xp = 25;

    static Ach A(string id, string ky, string ru, string en, string dRu, string dEn) => new Ach { id = id, ky = ky, ru = ru, en = en, descRu = dRu, descEn = dEn };

    public static readonly List<Ach> All = new List<Ach>
    {
        A("word1", "Биринчи сөз", "Первое слово", "First word", "Выучи первое кыргызское слово", "Learn your first Kyrgyz word"),
        A("word25", "Сөз жыйноочу", "Собиратель слов", "Word collector", "Выучи 25 слов", "Learn 25 words"),
        A("wordall", "Тил билгич", "Знаток языка", "Language expert", "Открой весь словарь", "Fill the whole dictionary"),
        A("quests", "Айылдын досу", "Друг айыла", "Friend of the village", "Выполни все задания", "Complete every quest"),
        A("buy1", "Биринчи соода", "Первая покупка", "First purchase", "Купи что-нибудь на базаре", "Buy something at the bazaar"),
        A("buyall", "Бай", "Богач", "Rich man", "Купи на базаре всё", "Buy everything at the bazaar"),
        A("race", "Чабандес", "Наездник", "Jockey", "Выиграй ат чабыш", "Win the horse race"),
        A("komuz", "Күүчү", "Мастер күү", "Küü master", "Сыграй мелодию на 3 звезды", "Play a melody with 3 stars"),
        A("shyrdak", "Шырдакчы", "Мастер шырдака", "Shyrdak maker", "Собери все три узора", "Complete all three patterns"),
        A("fish5", "Балыкчы", "Рыбак", "Fisher", "Поймай 5 рыб", "Catch 5 fish"),
        A("eagle", "Салбуурун", "Охота с беркутом", "Eagle hunt", "Посмотри полёт беркута", "Watch the golden eagle fly"),
        A("night", "Түнкү айыл", "Ночной айыл", "Village at night", "Встреть ночь в селе", "See the village at night"),
        A("weather", "Жамгыр астында", "Под дождём", "In the rain", "Попади под дождь или снег", "Get caught in rain or snow"),
        A("detective", "Тергөөчү", "Сыщик", "Detective", "Найди наушники Анаса и помири его с участковым", "Find Anas's headphones and reconcile him with the officer"),
        A("manas", "Манас таануучу", "Знаток «Манаса»", "Manas expert", "Дослушай манасчы и ответь на викторину", "Listen to the manaschy and pass the quiz"),
        A("daily", "Эмгекчил", "Трудяга", "Hard worker", "Выполни все задания дня", "Complete all daily tasks of a day"),
        A("kokboru", "Көк бөрү", "Кок-бору", "Kok-boru", "Выиграй в кок-бору", "Win a game of kok-boru"),
        A("ordo", "Ордочу", "Мастер ордо", "Ordo master", "Выиграй турнир по ордо", "Win the ordo tournament"),
        A("cook", "Ашпозчу", "Повар", "Cook", "Свари бешбармак", "Cook beshbarmak"),
        A("yurt_decor", "Боз үйдүн ээси", "Хозяин юрты", "Master of the yurt", "Укрась юрту коврами", "Decorate the yurt with rugs"),
    };

    public static event System.Action Changed;

    public static bool Has(string id) => SaveData.Get("a_" + id) == 1;
    public static int Count { get { int n = 0; foreach (Ach a in All) if (Has(a.id)) n++; return n; } }

    public static void Give(string id)
    {
        Ach a = All.Find(x => x.id == id);
        if (a == null || Has(id)) return;
        if (SceneManager.GetActiveScene().name == PauseMenu.MenuScene) return;
        SaveData.Set("a_" + id, 1);
        if (GameManager.Instance != null) GameManager.Instance.AddXP(Xp);
        MurasToast.Push("Жетишкендик!  +" + Xp + " XP", "<b>" + a.ky + "</b> — " + Loc.T(a.ru, a.en) + "\n<size=85%><color=#C9BFA6>" + Loc.T(a.descRu, a.descEn) + "</color></size>",
                        new Color(0.45f, 0.72f, 0.38f), "achievement");
        Changed?.Invoke();
    }

    public static void CheckWords()
    {
        int n = Vocab.KnownCount;
        if (n >= 1) Give("word1");
        if (n >= 25) Give("word25");
        if (n >= Vocab.Total) Give("wordall");
    }

    public static void CheckQuests()
    {
        if (GameManager.SavedQuestCount >= GameManager.TotalQuests) Give("quests");
    }

    public static void CheckShop()
    {
        int owned = 0, paid = 0;
        foreach (ShopCatalog.Item it in ShopCatalog.Items)
        {
            if (it.price == 0) continue;
            paid++;
            if (ShopCatalog.Owns(it.id)) owned++;
        }
        if (owned >= 1) Give("buy1");
        if (owned >= paid) Give("buyall");
    }

    public static void ClearSaved() => Changed?.Invoke();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Changed = null;
}

