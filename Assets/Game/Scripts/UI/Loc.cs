using System;
using UnityEngine;

public enum GameLanguage { Russian = 0, English = 1 }

/// <summary>
/// Простая локализация RU / EN. Кыргызские слова и фразы (Салам, Боз үй, Чүкө атуу…) остаются
/// в обоих языках — меняются только пояснения и интерфейс.
/// В коде: Loc.T("по-русски", "in English"). Подписка на смену языка: Loc.Changed.
/// </summary>
public static class Loc
{
    private const string Key = "MURAS_Language";

    public static event Action Changed;

    private static bool loaded;
    private static GameLanguage current;

    public static GameLanguage Current
    {
        get
        {
            if (!loaded)
            {
                current = (GameLanguage)PlayerPrefs.GetInt(Key, 0);
                loaded = true;
            }
            return current;
        }
        set
        {
            if (loaded && current == value) return;
            current = value;
            loaded = true;
            PlayerPrefs.SetInt(Key, (int)value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    public static bool IsEnglish => Current == GameLanguage.English;

    /// <summary>Выбрать строку по текущему языку.</summary>
    public static string T(string ru, string en) => IsEnglish && !string.IsNullOrEmpty(en) ? en : ru;

    public static string LanguageName => IsEnglish ? "English" : "Русский";

    public static void Next() => Current = IsEnglish ? GameLanguage.Russian : GameLanguage.English;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        loaded = false;
        Changed = null;
    }
}
