using UnityEngine.SceneManagement;
using UnityEngine;

/// <summary>
/// Детектив «Жоголгон кулакчын» — пропавшие наушники Анаса.
///  1. Анас жалуется: наушники пропали, участковый думает на детей.
///  2. Актан Турдакунович велит опросить свидетелей: ким? кайда? качан? (кто? где? когда?)
///  3. Апа, байке и чабан рассказывают, что видели; чабан видел сороку у реки за пирсом.
///  4. В гнезде сороки (сагызган) — наушники. Вернуть Анасу, рассказать участковому — и помирить обоих.
/// Состояние хранится в SaveData ("hq"), реплики подменяются через QuestTalk.
/// </summary>
public static class HeadphonesQuest
{
    public const string Key = "hq";
    public const int Reward = 120;
    public static int Stage => SaveData.Get(Key);
    static bool W(string who) => SaveData.Get("hq_" + who) == 1;
    static int Witnesses => (W("apa") ? 1 : 0) + (W("baike") ? 1 : 0) + (W("chaban") ? 1 : 0);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        QuestTalk.Register(Handle);
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        UpdateHud();
    }

    static void OnLoaded(Scene s, LoadSceneMode m) { QuestTalk.Register(Handle); UpdateHud(); }

    static void Set(int stage) { SaveData.Set(Key, stage); UpdateHud(); }

    static bool Handle(string who, out string line)
    {
        line = null;
        int s = Stage;
        switch (who)
        {
            case "Бунтарь":
                if (s == 0)
                {
                    Set(1);
                    Vocab.Unlock("kim", "kayda", "kachan");
                    MurasToast.Push(Loc.T("Жаңы тапшырма", "New quest"), Loc.T("<b>Жоголгон кулакчын</b> — пропали наушники Анаса", "<b>Жоголгон кулакчын</b> — Anas's headphones are missing"), new Color(0.84f, 0.66f, 0.29f), "word_chime");
                    line = Loc.T("Эй... беда, досум. Мой кулакчын — наушники — пропали! Актан Турдакунович говорит, что их стащили дети. Сходи к нему, а? Ким? Кайда? Качан? — кто, где, когда — разберись!",
                                 "Hey... trouble, friend. My кулакчын — headphones — are gone! Aktan Turdakunovich says the kids took them. Go see him, will you? Ким? Кайда? Качан? — who, where, when — figure it out!");
                    return true;
                }
                if (s == 1) { line = Loc.T("Сходи к участковому — он у базара стоит.", "Go to the officer — he stands by the bazaar."); return true; }
                if (s == 2) { line = Loc.T("Ну что? Спроси апу, байке и чабана — кто что видел. Без музыки мне грустно...", "So? Ask Apa, Baike and the shepherd what they saw. It's sad without music..."); return true; }
                if (s == 3)
                {
                    Inventory.Remove("kulakchyn");
                    Set(4);
                    GameAudio.Correct();
                    line = Loc.T("Менин кулакчыным! Рахмат, досум! В гнезде сагызгана?! Сорока — вот кто вор... Значит, дети ни при чём. Расскажи Актану Турдакуновичу, пусть знает.",
                                 "My headphones! Рахмат, friend! In a сагызган's nest?! A magpie was the thief... So the kids had nothing to do with it. Tell Aktan Turdakunovich, he should know.");
                    return true;
                }
                if (s == 4) { line = Loc.T("Иди к участковому, скажи, что всё нашлось!", "Go tell the officer everything's found!"); return true; }
                if (s == 5 && SaveData.Get("hq_thanks") == 0)
                {
                    SaveData.Set("hq_thanks", 1);
                    line = Loc.T("Мы с Актаном Турдакуновичем помирились! Он даже сказал, что музыка у меня нормальная. Почти.",
                                 "Aktan Turdakunovich and I made peace! He even said my music is fine. Almost.");
                    return true;
                }
                return false;

            case "Актан":
                if (s == 1)
                {
                    Set(2);
                    line = Loc.T("Опять бунтарь шумит? Пропал кулакчын? Уверен — дети с луга утащили. Но тартип есть тартип: сначала свидетели! Спроси: ким — кто? кайда — где? качан — когда? Поговори с апой, байке и чабаном.",
                                 "The rebel's making noise again? Headphones gone? I'm sure the kids from the meadow took them. But тартип is тартип: witnesses first! Ask: ким — who? кайда — where? качан — when? Talk to Apa, Baike and the shepherd.");
                    return true;
                }
                if (s == 2 || s == 3)
                {
                    line = Witnesses < 3
                        ? Loc.T($"Свидетели — это главное. Опрошено: {Witnesses} из 3. Апа, байке и чабан — кто что видел?", $"Witnesses are what matter. Questioned: {Witnesses} of 3. Apa, Baike and the shepherd — who saw what?")
                        : Loc.T("Сорока у реки, говоришь? Проверь гнездо за пирсом. А наушники — сразу хозяину!", "A magpie by the river, you say? Check the nest past the pier. And the headphones go straight to the owner!");
                    return true;
                }
                if (s == 4)
                {
                    Set(5);
                    Vocab.Unlock("kechirim", "sagyzgan");
                    if (GameManager.Instance != null) GameManager.Instance.AddXP(Reward);
                    Achievements.Give("detective");
                    GameAudio.QuestComplete();
                    MurasToast.Push(Loc.T("Тапшырма аткарылды!  +", "Quest complete!  +") + Reward + " XP", Loc.T("<b>Жоголгон кулакчын</b> — Анас и участковый помирились", "<b>Жоголгон кулакчын</b> — Anas and the officer made peace"), new Color(0.45f, 0.72f, 0.38f), "achievement");
                    line = Loc.T("Сагызган?! Мен жаңылыптырмын — я ошибся, дети не виноваты. Анас, кечирип кой — прости! Кечирим — прощение — тоже часть тартипа. Ну а музыку, бунтарь, всё-таки потише!",
                                 "A сагызган?! Мен жаңылыптырмын — I was wrong, the kids are innocent. Anas, кечирип кой — forgive me! Кечирим — forgiveness — is part of тартип too. But the music, rebel — a bit quieter, please!");
                    return true;
                }
                return false;

            case "Апа": return Witness("apa", out line,
                Loc.T("Качан? Эртең менен — рано утром — Анас сидел у реки, слушал музыку и задремал на солнышке. Наушники лежали рядом на траве. А потом — я уже не видела.",
                      "Качан? Early in the morning Anas sat by the river listening to music and dozed off in the sun. The headphones lay beside him on the grass. After that — I didn't see."));
            case "Байке": return Witness("baike", out line,
                Loc.T("Ким? Дети? Нет-нет, дети весь день играли в чүкө у меня на глазах. К реке они не ходили — я бы заметил.",
                      "Ким? The kids? No, no — they played чүкө in front of me all day. They never went to the river — I'd have noticed."));
            case "Чабан": return Witness("chaban", out line,
                Loc.T("Кайда? Утром я видел у реки сагызгана — сороку. Она тащила что-то блестящее к дереву за пирсом. Сороки обожают всё блестящее! Поищи там гнездо.",
                      "Кайда? This morning I saw a сагызган — a magpie — by the river. It was dragging something shiny to a tree past the pier. Magpies love anything shiny! Look for its nest there."));
        }
        return false;
    }

    static bool Witness(string who, out string line, string text)
    {
        line = null;
        if (Stage != 2 && Stage != 3) return false;
        if (W(who)) return false;                 // уже рассказал — дальше обычные разговоры
        SaveData.Set("hq_" + who, 1);
        if (who == "chaban") Vocab.Unlock("sagyzgan");
        GameAudio.Interact();
        UpdateHud();
        line = text;
        return true;
    }

    public static void FoundHeadphones()
    {
        if (Stage != 2) return;
        Inventory.Add("kulakchyn");
        Set(3);
        GameAudio.Pickup();
        QuestTalk.Say(null, Loc.T("(Сагызгандын уясы — гнездо сороки! А в нём блестят наушники Анаса.)", "(Сагызгандын уясы — a magpie's nest! And Anas's headphones are shining inside.)"));
    }

    public static void UpdateHud()
    {
        const string id = "headphones";
        string title = Loc.T("Жоголгон кулакчын", "Жоголгон кулакчын · lost headphones");
        switch (Stage)
        {
            case 1: SideQuestHud.Set(id, title, Loc.T("Поговори с участковым у базара", "Talk to the officer by the bazaar")); break;
            case 2:
                SideQuestHud.Set(id, title, Witnesses < 3
                    ? Loc.T($"Опроси свидетелей: апа, байке, чабан ({Witnesses}/3)", $"Question the witnesses: Apa, Baike, shepherd ({Witnesses}/3)")
                    : Loc.T("Найди гнездо сороки у реки за пирсом", "Find the magpie's nest by the river past the pier"));
                break;
            case 3: SideQuestHud.Set(id, title, Loc.T("Верни кулакчын Анасу", "Return the headphones to Anas")); break;
            case 4: SideQuestHud.Set(id, title, Loc.T("Расскажи Актану Турдакуновичу, что всё нашлось", "Tell Aktan Turdakunovich everything was found")); break;
            default: SideQuestHud.Set(id, null, null); break;
        }
    }
}

/// <summary>Гнездо сороки у реки: наушники видны и берутся только во время расследования.</summary>
public class HeadphonesClue : MonoBehaviour
{
    public GameObject headphones;
    public float radius = 2.2f;
    private Transform player;

    void Start() { var pm = FindAnyObjectByType<PlayerMovement>(); if (pm != null) player = pm.transform; }

    void Update()
    {
        int s = HeadphonesQuest.Stage;
        if (headphones != null)
        {
            headphones.SetActive(s == 2);
            if (s == 2) headphones.transform.localRotation = Quaternion.Euler(0f, Time.time * 30f, 0f);
        }
        if (player == null || s != 2 || !Modal.Free || PauseMenu.IsPaused) return;
        Vector3 d = player.position - transform.position; d.y = 0f;
        if (d.sqrMagnitude > radius * radius) return;
        InteractionHint.Request("<b><color=#E8B04A>E</color></b>  —  " + Loc.T("заглянуть в гнездо сороки", "look into the magpie's nest"));
        if (Input.GetKeyDown(KeyCode.E)) HeadphonesQuest.FoundHeadphones();
    }
}
