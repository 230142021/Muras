using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Дополнительный прогресс (слова словаря, достижения, рекорды мини-игр) в одной записи PlayerPrefs.
/// Так его легко стереть целиком при «Новой игре».
/// </summary>
public static class SaveData
{
    private const string Key = "MURAS_Extra";
    private static Dictionary<string, int> data;

    private static Dictionary<string, int> D
    {
        get
        {
            if (data != null) return data;
            data = new Dictionary<string, int>();
            string raw = PlayerPrefs.GetString(Key, "");
            foreach (string pair in raw.Split(';'))
            {
                int eq = pair.IndexOf('=');
                if (eq > 0 && int.TryParse(pair.Substring(eq + 1), out int v)) data[pair.Substring(0, eq)] = v;
            }
            return data;
        }
    }

    public static int Get(string key, int def = 0) => D.TryGetValue(key, out int v) ? v : def;
    public static bool Has(string key) => D.ContainsKey(key);

    public static void Set(string key, int value)
    {
        D[key] = value;
        var sb = new StringBuilder();
        foreach (var kv in D) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(';');
        PlayerPrefs.SetString(Key, sb.ToString());
        PlayerPrefs.Save();
    }

    public static void Add(string key, int delta) => Set(key, Get(key) + delta);

    /// <summary>Записать, только если новое значение больше (рекорды).</summary>
    public static bool SetMax(string key, int value)
    {
        if (Has(key) && Get(key) >= value) return false;
        Set(key, value);
        return true;
    }

    public static void Clear()
    {
        data = new Dictionary<string, int>();
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => data = null;
}
