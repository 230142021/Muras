using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// Минимальный JSON для сетевого клиента: JsonUtility не умеет словари, а сохранение — это словарь ключ→число.
/// Parse возвращает Dictionary&lt;string, object&gt;, List&lt;object&gt;, string, double, bool или null.
/// </summary>
public static class CloudJson
{
    // ------------------------------------------------------------------ запись
    public static string Str(string s)
    {
        if (s == null) return "null";
        var sb = new StringBuilder(s.Length + 2);
        sb.Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }

    public static string Value(object v)
    {
        switch (v)
        {
            case null: return "null";
            case string s: return Str(s);
            case bool b: return b ? "true" : "false";
            case int i: return i.ToString(CultureInfo.InvariantCulture);
            case long l: return l.ToString(CultureInfo.InvariantCulture);
            case float f: return f.ToString("0.###", CultureInfo.InvariantCulture);
            case double d: return d.ToString("0.###", CultureInfo.InvariantCulture);
            case IDictionary<string, int> di:
            {
                var sb = new StringBuilder("{");
                bool first = true;
                foreach (var kv in di) { if (!first) sb.Append(','); first = false; sb.Append(Str(kv.Key)).Append(':').Append(kv.Value.ToString(CultureInfo.InvariantCulture)); }
                return sb.Append('}').ToString();
            }
            case IEnumerable<string> list:
            {
                var sb = new StringBuilder("[");
                bool first = true;
                foreach (var s in list) { if (!first) sb.Append(','); first = false; sb.Append(Str(s)); }
                return sb.Append(']').ToString();
            }
            default: return Str(v.ToString());
        }
    }

    /// <summary>Object("id", "salam", "xp", 5) → {"id":"salam","xp":5}</summary>
    public static string Object(params object[] kv)
    {
        var sb = new StringBuilder("{");
        for (int i = 0; i + 1 < kv.Length; i += 2)
        {
            if (i > 0) sb.Append(',');
            sb.Append(Str(kv[i].ToString())).Append(':');
            if (kv[i + 1] is RawJson raw) sb.Append(raw.json); else sb.Append(Value(kv[i + 1]));
        }
        return sb.Append('}').ToString();
    }

    public sealed class RawJson { public readonly string json; public RawJson(string j) { json = j; } }
    public static RawJson Raw(string json) => new RawJson(json);

    // ------------------------------------------------------------------ чтение
    public static object Parse(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        int i = 0;
        try { return ParseValue(json, ref i); }
        catch (System.Exception) { return null; }
    }

    public static Dictionary<string, object> Obj(object o) => o as Dictionary<string, object>;
    public static List<object> Arr(object o) => o as List<object>;
    public static string S(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) && v is string s ? s : null;
    public static int I(Dictionary<string, object> d, string k, int def = 0) => d != null && d.TryGetValue(k, out var v) && v is double x ? (int)System.Math.Round(x) : def;
    public static bool B(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) && v is bool b && b;
    public static Dictionary<string, object> O(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) ? v as Dictionary<string, object> : null;
    public static List<object> A(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) ? v as List<object> : null;

    static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

    static object ParseValue(string s, ref int i)
    {
        Ws(s, ref i);
        char c = s[i];
        if (c == '{')
        {
            var d = new Dictionary<string, object>(); i++; Ws(s, ref i);
            if (s[i] == '}') { i++; return d; }
            while (true)
            {
                Ws(s, ref i); string key = ParseString(s, ref i); Ws(s, ref i);
                i++;                                   // ':'
                d[key] = ParseValue(s, ref i); Ws(s, ref i);
                if (s[i] == ',') { i++; continue; }
                i++; return d;                         // '}'
            }
        }
        if (c == '[')
        {
            var l = new List<object>(); i++; Ws(s, ref i);
            if (s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(ParseValue(s, ref i)); Ws(s, ref i);
                if (s[i] == ',') { i++; continue; }
                i++; return l;
            }
        }
        if (c == '"') return ParseString(s, ref i);
        if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
        if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
        if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
        int start = i;
        while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
        return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
    }

    static string ParseString(string s, ref int i)
    {
        var sb = new StringBuilder();
        i++;                                           // opening quote
        while (s[i] != '"')
        {
            char c = s[i++];
            if (c != '\\') { sb.Append(c); continue; }
            char e = s[i++];
            switch (e)
            {
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'u': sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber)); i += 4; break;
                default: sb.Append(e); break;
            }
        }
        i++;
        return sb.ToString();
    }
}
