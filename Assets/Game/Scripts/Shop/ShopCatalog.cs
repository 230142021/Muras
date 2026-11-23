using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Товары базара, на которые тратится заработанный опыт (XP), и сохранение покупок.
/// Кийим — одежда героя, Баш кийим — головной убор, Жөндөм — улучшения, Дос — спутники.
/// Всё хранится в PlayerPrefs и стирается вместе с прогрессом («Новая игра»).
/// </summary>
public static class ShopCatalog
{
    public enum Kind { Outfit, Hat, Perk, Companion }

    public class Item
    {
        public string id;
        public Kind kind;
        public string nameKy;      // кыргызское название (одно на оба языка)
        public string nameRu, nameEn;
        public string descRu, descEn;
        public int price;
        public Color swatch;       // цвет карточки
        public Color[] colors;     // для одежды: Jacket, Accent, Pants, Boots, Belt, Pack
        public bool coat;          // длинные полы (чапан)
    }

    public const string DefaultOutfit = "traveler";
    public const string DefaultHat = "kalpak";

    // Порядок цветов в Item.colors
    public static readonly string[] OutfitSlots = { "Jacket", "Accent", "Pants", "Boots", "Belt", "Pack" };

    static Color C(float r, float g, float b) => new Color(r, g, b);

    public static readonly List<Item> Items = new List<Item>
    {
        // ---------------- Кийим (одежда)
        new Item { id = "traveler", kind = Kind.Outfit, nameKy = "Саякатчы", nameRu = "Путешественник", nameEn = "Traveler", price = 0,
                   descRu = "Куртка и рюкзак — с этим ты пришёл в село.", descEn = "Jacket and backpack — what you came to the village in.",
                   swatch = C(0.16f, 0.42f, 0.45f),
                   colors = new[] { C(0.16f, 0.42f, 0.45f), C(0.86f, 0.64f, 0.22f), C(0.20f, 0.24f, 0.32f), C(0.32f, 0.20f, 0.12f), C(0.28f, 0.18f, 0.10f), C(0.55f, 0.18f, 0.14f) } },
        new Item { id = "shepherd", kind = Kind.Outfit, nameKy = "Чабан", nameRu = "Одежда чабана", nameEn = "Shepherd's clothes", price = 100,
                   descRu = "Тёплая куртка из овчины и красный кушак, как у пастухов на жайлоо.", descEn = "Warm sheepskin jacket and a red sash, like herders on the jailoo.",
                   swatch = C(0.56f, 0.42f, 0.28f),
                   colors = new[] { C(0.56f, 0.42f, 0.28f), C(0.92f, 0.88f, 0.78f), C(0.28f, 0.22f, 0.16f), C(0.14f, 0.10f, 0.07f), C(0.70f, 0.16f, 0.14f), C(0.42f, 0.40f, 0.36f) } },
        new Item { id = "chapan", kind = Kind.Outfit, nameKy = "Чапан", nameRu = "Праздничный чапан", nameEn = "Festive chapan", price = 150,
                   descRu = "Длинный бордовый халат с золотой вышивкой — его надевают на той.", descEn = "Long maroon robe with gold embroidery, worn for a celebration (той).",
                   swatch = C(0.42f, 0.08f, 0.12f), coat = true,
                   colors = new[] { C(0.42f, 0.08f, 0.12f), C(0.90f, 0.70f, 0.25f), C(0.12f, 0.12f, 0.14f), C(0.10f, 0.08f, 0.07f), C(0.85f, 0.66f, 0.22f), C(0.30f, 0.20f, 0.12f) } },
        new Item { id = "batyr", kind = Kind.Outfit, nameKy = "Баатыр", nameRu = "Одежда батыра", nameEn = "Batyr's attire", price = 250,
                   descRu = "Синий чапан с золотом, как у героев эпоса «Манас».", descEn = "Blue and gold chapan, like the heroes of the Manas epic.",
                   swatch = C(0.12f, 0.20f, 0.46f), coat = true,
                   colors = new[] { C(0.12f, 0.20f, 0.46f), C(0.92f, 0.72f, 0.26f), C(0.32f, 0.09f, 0.08f), C(0.38f, 0.12f, 0.08f), C(0.70f, 0.12f, 0.10f), C(0.25f, 0.17f, 0.10f) } },

        // ---------------- Баш кийим (головные уборы)
        new Item { id = "kalpak", kind = Kind.Hat, nameKy = "Ак калпак", nameRu = "Белый калпак", nameEn = "White kalpak", price = 0,
                   descRu = "Войлочная шапка — символ Кыргызстана.", descEn = "Felt hat, the symbol of Kyrgyzstan.", swatch = C(0.95f, 0.94f, 0.90f) },
        new Item { id = "tebetey", kind = Kind.Hat, nameKy = "Тебетей", nameRu = "Меховой тебетей", nameEn = "Fur tebetey", price = 60,
                   descRu = "Тёплая шапка с меховым околышем для холодных гор.", descEn = "Warm hat with a fur brim for the cold mountains.", swatch = C(0.62f, 0.14f, 0.14f) },
        new Item { id = "cap", kind = Kind.Hat, nameKy = "Кепка", nameRu = "Кепка туриста", nameEn = "Tourist cap", price = 30,
                   descRu = "Простая кепка от солнца.", descEn = "A simple cap against the sun.", swatch = C(0.85f, 0.62f, 0.20f) },

        // ---------------- Жөндөм (улучшения)
        new Item { id = "boots", kind = Kind.Perk, nameKy = "Жеңил өтүк", nameRu = "Лёгкие сапоги", nameEn = "Light boots", price = 120,
                   descRu = "Ходьба +15%, бег +25%.", descEn = "Walk +15%, run +25%.", swatch = C(0.45f, 0.30f, 0.18f) },
        new Item { id = "jump", kind = Kind.Perk, nameKy = "Тоо секирик", nameRu = "Горный прыжок", nameEn = "Mountain jump", price = 90,
                   descRu = "Прыгаешь в 1.6 раза выше, легко забираешься на камни.", descEn = "Jump 1.6× higher and climb rocks easily.", swatch = C(0.40f, 0.55f, 0.35f) },

        // ---------------- Дос (спутники)
        new Item { id = "dog", kind = Kind.Companion, nameKy = "Көкжал", nameRu = "Пёс Кёкжал", nameEn = "Kokjal the dog", price = 120,
                   descRu = "Верный пёс-тайган ходит за тобой по селу.", descEn = "A loyal taigan hound that follows you around.", swatch = C(0.55f, 0.42f, 0.30f) },
        new Item { id = "horse", kind = Kind.Companion, nameKy = "Тулпар", nameRu = "Конь Тулпар", nameEn = "Tulpar the horse", price = 180,
                   descRu = "Свой конь — гордость любого кыргыза. Идёт рядом с тобой.", descEn = "Your own horse — every Kyrgyz's pride. Walks beside you.", swatch = C(0.38f, 0.24f, 0.14f) },
    };

    /// <summary>Что-то купили, надели или переключили.</summary>
    public static event Action Changed;

    const string OwnKey = "MURAS_Own_";
    const string OffKey = "MURAS_Off_";
    const string OutfitKey = "MURAS_EquipOutfit";
    const string HatKey = "MURAS_EquipHat";

    public static Item Get(string id) => Items.Find(i => i.id == id);

    public static bool Owns(string id)
    {
        Item it = Get(id);
        return it != null && (it.price == 0 || PlayerPrefs.GetInt(OwnKey + id, 0) == 1);
    }

    public static string EquippedOutfit
    {
        get { string id = PlayerPrefs.GetString(OutfitKey, DefaultOutfit); return Owns(id) ? id : DefaultOutfit; }
    }

    public static string EquippedHat
    {
        get { string id = PlayerPrefs.GetString(HatKey, DefaultHat); return Owns(id) ? id : DefaultHat; }
    }

    /// <summary>Улучшение или спутник куплен и включён.</summary>
    public static bool IsActive(string id) => Owns(id) && PlayerPrefs.GetInt(OffKey + id, 0) == 0;

    public static bool IsEquipped(Item it) =>
        it.kind == Kind.Outfit ? EquippedOutfit == it.id :
        it.kind == Kind.Hat ? EquippedHat == it.id : IsActive(it.id);

    public static int Balance => GameManager.Instance != null ? GameManager.Instance.CurrentXP : GameManager.SavedXP;

    /// <summary>Купить за XP. Одежда и шапка сразу надеваются.</summary>
    public static bool Buy(string id)
    {
        Item it = Get(id);
        if (it == null || Owns(id)) return false;
        if (GameManager.Instance == null || !GameManager.Instance.SpendXP(it.price)) return false;
        PlayerPrefs.SetInt(OwnKey + id, 1);
        PlayerPrefs.DeleteKey(OffKey + id);
        if (it.kind == Kind.Outfit) PlayerPrefs.SetString(OutfitKey, id);
        if (it.kind == Kind.Hat) PlayerPrefs.SetString(HatKey, id);
        PlayerPrefs.Save();
        Changed?.Invoke();
        return true;
    }

    /// <summary>Надеть одежду/шапку или включить/выключить улучшение и спутника.</summary>
    public static void Use(string id)
    {
        Item it = Get(id);
        if (it == null || !Owns(id)) return;
        switch (it.kind)
        {
            case Kind.Outfit: PlayerPrefs.SetString(OutfitKey, id); break;
            case Kind.Hat: PlayerPrefs.SetString(HatKey, id); break;
            default: PlayerPrefs.SetInt(OffKey + id, IsActive(id) ? 1 : 0); break;
        }
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static int OwnedCount
    {
        get { int n = 0; foreach (Item it in Items) if (it.price > 0 && Owns(it.id)) n++; return n; }
    }

    public static string KindName(Kind k) => k switch
    {
        Kind.Outfit => Loc.T("Кийим · одежда", "Кийим · outfit"),
        Kind.Hat => Loc.T("Баш кийим · шапка", "Баш кийим · hat"),
        Kind.Perk => Loc.T("Жөндөм · умение", "Жөндөм · skill"),
        _ => Loc.T("Дос · спутник", "Дос · companion"),
    };

    /// <summary>Стирает покупки (вызывается из GameManager.ClearSavedProgress).</summary>
    public static void ClearSaved()
    {
        foreach (Item it in Items)
        {
            PlayerPrefs.DeleteKey(OwnKey + it.id);
            PlayerPrefs.DeleteKey(OffKey + it.id);
        }
        PlayerPrefs.DeleteKey(OutfitKey);
        PlayerPrefs.DeleteKey(HatKey);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Changed = null;
}
