using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Меню для теста знаний, обучения и аналитики (Tools → Diploma → Learning).</summary>
public static class LearningTools
{
    const string AutoKey = "MURAS_LearnInEditor";
    const string AutoMenu = "Tools/Diploma/Learning/Auto test + tutorial in Editor";

    [MenuItem(AutoMenu)]
    static void ToggleAuto()
    {
        bool on = PlayerPrefs.GetInt(AutoKey, 0) == 0;
        PlayerPrefs.SetInt(AutoKey, on ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log("[Learning] автозапуск теста и обучения в редакторе: " + (on ? "включён" : "выключен") + " (в собранной игре — всегда включён)");
    }

    [MenuItem(AutoMenu, true)]
    static bool ToggleAutoCheck() { Menu.SetChecked(AutoMenu, PlayerPrefs.GetInt(AutoKey, 0) == 1); return true; }

    [MenuItem("Tools/Diploma/Learning/Open research log (muras_research.csv)")]
    static void OpenLog()
    {
        if (!File.Exists(ResearchLog.PathCsv)) { Debug.Log("[Learning] файла ещё нет: " + ResearchLog.PathCsv); EditorUtility.RevealInFinder(Application.persistentDataPath); return; }
        EditorUtility.RevealInFinder(ResearchLog.PathCsv);
    }

    [MenuItem("Tools/Diploma/Learning/Word Recorder (record pronunciations)")]
    static void Recorder() => WordRecorderWindow.Open();

    [MenuItem("Tools/Diploma/Play Mode: Fun/Learning: pre-test")]
    static void Pre() { if (Application.isPlaying) KnowledgeTest.StartPre(); }

    [MenuItem("Tools/Diploma/Play Mode: Fun/Learning: post-test")]
    static void Post() { if (Application.isPlaying) KnowledgeTest.StartPost(); }

    [MenuItem("Tools/Diploma/Play Mode: Fun/Learning: tutorial")]
    static void Tut() { if (Application.isPlaying) { SaveData.Set("tut_done", 0); Tutorial.Begin(); } }

    [MenuItem("Tools/Diploma/Play Mode: Fun/Learning: check yourself (word review)")]
    static void Review() { if (Application.isPlaying) WordReview.Start(); }

    // ---------- отладка без мыши: нажать кнопку окна и снять кадр вместе с интерфейсом
    static void Call(object o, string method, params object[] args) =>
        o?.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.Invoke(o, args);

    [MenuItem("Tools/Diploma/Play Mode: Fun/Learning (debug): quiz — start or answer")]
    static void DebugQuiz()
    {
        var q = QuizWindow.Current;
        if (q == null) return;
        int index = (int)typeof(QuizWindow).GetField("index", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(q);
        var items = ((QuizWindow.Config)typeof(QuizWindow).GetField("cfg", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(q)).items;
        if (index < 0) Call(q, "Begin");
        else if (index >= items.Count) Call(q, "Close");
        else Call(q, "Answer", UnityEngine.Random.Range(0, 4));
    }

    [MenuItem("Tools/Diploma/Play Mode: Fun/Learning (debug): quiz — answer all")]
    static void DebugQuizAll()
    {
        var q = QuizWindow.Current;
        if (q == null) return;
        var t = typeof(QuizWindow);
        var cfg = (QuizWindow.Config)t.GetField("cfg", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(q);
        var order = (int[])t.GetField("order", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(q);
        cfg.showFeedback = false;                                // без пауз между вопросами
        if ((int)t.GetField("index", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(q) < 0) Call(q, "Begin");
        for (int guard = 0; guard < 40; guard++)
        {
            int i = (int)t.GetField("index", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(q);
            if (i >= cfg.items.Count) break;
            t.GetField("locked", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(q, false);
            Call(q, "Answer", i % 3 == 0 ? (order[i] + 1) % 4 : order[i]);        // ~70% верно
            q.StopAllCoroutines();
            Call(q, "Next");
        }
    }

    [MenuItem("Tools/Diploma/Play Mode: Fun/Learning (debug): tutorial — next step")]
    static void DebugTutorial() { if (Tutorial.Instance != null) Call(Tutorial.Instance, "Complete"); }

    [MenuItem("Tools/Diploma/Play Mode: Fun/Learning (debug): open dictionary")]
    static void DebugJournal() { if (JournalWindow.Instance != null) JournalWindow.Instance.Open(); }

    static int shot;
    [MenuItem("Tools/Diploma/Play Mode: Fun/Learning (debug): screenshot with UI")]
    static void DebugShot()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "shots"));
        Directory.CreateDirectory(dir);
        var changed = new List<Canvas>();
        foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.activeInHierarchy) changed.Add(c);
        changed.Sort((a, b) => a.sortingOrder.CompareTo(b.sortingOrder));
        var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        float dist = 0.5f;
        foreach (var c in changed) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = dist; dist -= 0.002f; }
        Canvas.ForceUpdateCanvases();
        cam.Render();
        foreach (var c in changed) c.renderMode = RenderMode.ScreenSpaceOverlay;
        cam.targetTexture = prev;
        RenderTexture.active = rt;
        var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
        RenderTexture.active = null;
        string path = Path.Combine(dir, "ui_" + (shot++) + ".png");
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        Debug.Log("[Learning] кадр: " + path);
    }

    [MenuItem("Tools/Diploma/Play Mode: Fun/Learning: reset test + tutorial")]
    static void ResetLearning()
    {
        foreach (string k in new[] { "kt_pre_done", "kt_pre", "kt_post_done", "kt_post", "kt_post_offered", "tut_done", "play_min" }) SaveData.Set(k, 0);
        Debug.Log("[Learning] тест и обучение сброшены");
    }
}

/// <summary>
/// Запись произношения слов словаря с микрофона прямо в Unity. Выбери слово → «Запись» (или пробел) → скажи слово →
/// «Стоп» (или пробел). Тишина по краям обрезается, громкость выравнивается, файл сохраняется в
/// Assets/Game/Resources/MURAS/Words/&lt;id&gt;.wav. Можно также импортировать папку с файлами, записанными на телефон.
/// </summary>
public class WordRecorderWindow : EditorWindow
{
    const string Dir = "Assets/Game/Resources/MURAS/Words";
    const int Rate = 44100, MaxSeconds = 4;

    int selected;
    bool recording;
    AudioClip micClip;
    string device;
    Vector2 scroll;
    string filter = "";
    bool autoNext = true;

    public static void Open()
    {
        var w = GetWindow<WordRecorderWindow>("Word Recorder");
        w.minSize = new Vector2(560, 520);
    }

    void OnEnable() { string[] d = Microphone.devices; device = d.Length > 0 ? d[0] : null; }

    static string PathFor(string id) => Dir + "/" + id + ".wav";
    static bool Has(string id) => File.Exists(PathFor(id)) || Directory.Exists(Dir) && Directory.GetFiles(Dir, id + ".*").Any(f => !f.EndsWith(".meta"));

    void OnGUI()
    {
        var words = Vocab.Words;
        int done = words.Count(w => Has(w.id));
        EditorGUILayout.LabelField("Озвучка словаря", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Выбери слово и нажми «Запись» (или Пробел), скажи слово чётко, затем «Стоп» (или Пробел). " +
                                "Тишина обрезается автоматически. Озвучено: " + done + " из " + words.Count + ".", MessageType.Info);

        string[] devices = Microphone.devices;
        if (devices.Length == 0) { EditorGUILayout.HelpBox("Микрофон не найден. На macOS разреши Unity доступ к микрофону: Системные настройки → Конфиденциальность → Микрофон.", MessageType.Warning); }
        else
        {
            int di = Mathf.Max(0, Array.IndexOf(devices, device));
            di = EditorGUILayout.Popup("Микрофон", di, devices);
            device = devices[di];
        }
        autoNext = EditorGUILayout.Toggle("После записи — к следующему", autoNext);
        filter = EditorGUILayout.TextField("Поиск", filter);

        EditorGUILayout.Space();
        var w = words[Mathf.Clamp(selected, 0, words.Count - 1)];
        using (new EditorGUILayout.HorizontalScope())
        {
            var big = new GUIStyle(EditorStyles.boldLabel) { fontSize = 22 };
            EditorGUILayout.LabelField(w.ky + "   [" + w.latin + "]", big, GUILayout.Height(30));
        }
        EditorGUILayout.LabelField(w.ru + " · " + w.en, EditorStyles.miniLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = devices.Length > 0;
            GUI.backgroundColor = recording ? new Color(1f, 0.45f, 0.4f) : Color.white;
            if (GUILayout.Button(recording ? "■ Стоп (Пробел)" : "● Запись (Пробел)", GUILayout.Height(36))) ToggleRecord();
            GUI.backgroundColor = Color.white;
            GUI.enabled = Has(w.id) && !recording;
            if (GUILayout.Button("► Прослушать", GUILayout.Height(36))) Preview(w.id);
            if (GUILayout.Button("Удалить", GUILayout.Height(36), GUILayout.Width(80)))
                if (EditorUtility.DisplayDialog("Удалить запись", "Удалить запись слова «" + w.ky + "»?", "Удалить", "Отмена")) Delete(w.id);
            GUI.enabled = true;
        }
        if (recording) EditorGUILayout.HelpBox("Идёт запись… (до " + MaxSeconds + " с)", MessageType.None);

        EditorGUILayout.Space();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < words.Count; i++)
        {
            var x = words[i];
            if (filter.Length > 0 && !(x.ky + x.latin + x.ru + x.en + x.id).ToLowerInvariant().Contains(filter.ToLowerInvariant())) continue;
            using (new EditorGUILayout.HorizontalScope())
            {
                bool has = Has(x.id);
                GUI.backgroundColor = i == selected ? new Color(0.9f, 0.75f, 0.4f) : Color.white;
                if (GUILayout.Button((has ? "✓  " : "    ") + x.ky + "   —   " + x.ru, EditorStyles.miniButtonLeft)) selected = i;
                GUI.backgroundColor = Color.white;
                GUILayout.Label(x.id, EditorStyles.miniLabel, GUILayout.Width(110));
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Импорт папки с записями…")) ImportFolder();
            if (GUILayout.Button("Список слов для диктора (в буфер)")) CopyList();
        }

        var e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Space && devices.Length > 0) { ToggleRecord(); e.Use(); }
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.DownArrow) { selected = Mathf.Min(words.Count - 1, selected + 1); e.Use(); Repaint(); }
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.UpArrow) { selected = Mathf.Max(0, selected - 1); e.Use(); Repaint(); }
    }

    void Update() { if (recording) { Repaint(); if (micClip != null && Microphone.GetPosition(device) >= Rate * MaxSeconds - 1) ToggleRecord(); } }

    void ToggleRecord()
    {
        if (!recording)
        {
            micClip = Microphone.Start(device, false, MaxSeconds, Rate);
            recording = micClip != null;
            return;
        }
        int pos = Microphone.GetPosition(device);
        Microphone.End(device);
        recording = false;
        if (micClip == null) return;
        if (pos <= 0) pos = micClip.samples;
        var data = new float[micClip.samples * micClip.channels];
        micClip.GetData(data, 0);
        var mono = new float[Mathf.Min(pos, micClip.samples)];
        for (int i = 0; i < mono.Length; i++) mono[i] = data[i * micClip.channels];
        float[] clean = Process(mono);
        if (clean == null) { EditorUtility.DisplayDialog("Тихо", "Голос не услышан — проверь микрофон и говори громче.", "OK"); return; }
        var w = Vocab.Words[selected];
        Directory.CreateDirectory(Dir);
        foreach (var f in Directory.GetFiles(Dir, w.id + ".*")) if (!f.EndsWith(".meta") && !f.EndsWith(".wav")) AssetDatabase.DeleteAsset(f.Replace('\\', '/'));
        WriteWav(PathFor(w.id), clean, Rate);
        AssetDatabase.ImportAsset(PathFor(w.id));
        Preview(w.id);
        if (autoNext && selected < Vocab.Words.Count - 1) selected++;
        Repaint();
    }

    /// <summary>Обрезать тишину, выровнять громкость (пик 0.9), плавные края 15 мс.</summary>
    static float[] Process(float[] s)
    {
        float peak = 0f;
        foreach (float v in s) peak = Mathf.Max(peak, Mathf.Abs(v));
        if (peak < 0.02f) return null;
        float thr = peak * 0.08f;
        int a = 0, b = s.Length - 1;
        while (a < s.Length && Mathf.Abs(s[a]) < thr) a++;
        while (b > a && Mathf.Abs(s[b]) < thr) b--;
        a = Mathf.Max(0, a - Rate / 20); b = Mathf.Min(s.Length - 1, b + Rate / 8);   // запас: 50 мс до и 125 мс после
        var o = new float[b - a + 1];
        float g = 0.9f / peak;
        int fade = Rate * 15 / 1000;
        for (int i = 0; i < o.Length; i++)
        {
            float env = Mathf.Min(1f, Mathf.Min(i, o.Length - 1 - i) / (float)fade);
            o[i] = s[a + i] * g * env;
        }
        return o;
    }

    static void WriteWav(string path, float[] samples, int rate)
    {
        using (var fs = new FileStream(path, FileMode.Create))
        using (var bw = new BinaryWriter(fs))
        {
            int bytes = samples.Length * 2;
            bw.Write(new[] { 'R', 'I', 'F', 'F' }); bw.Write(36 + bytes); bw.Write(new[] { 'W', 'A', 'V', 'E' });
            bw.Write(new[] { 'f', 'm', 't', ' ' }); bw.Write(16); bw.Write((short)1); bw.Write((short)1);
            bw.Write(rate); bw.Write(rate * 2); bw.Write((short)2); bw.Write((short)16);
            bw.Write(new[] { 'd', 'a', 't', 'a' }); bw.Write(bytes);
            foreach (float v in samples) bw.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32768, 32767));
        }
    }

    static void Preview(string id)
    {
        var file = Directory.Exists(Dir) ? Directory.GetFiles(Dir, id + ".*").FirstOrDefault(f => !f.EndsWith(".meta")) : null;
        if (file == null) return;
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(file.Replace('\\', '/'));
        if (clip == null) return;
        // внутренний AudioUtil — проигрывание в редакторе без Play Mode
        var util = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        var stop = util?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public);
        stop?.Invoke(null, null);
        var play = util?.GetMethods(BindingFlags.Static | BindingFlags.Public).FirstOrDefault(m => m.Name == "PlayPreviewClip");
        if (play != null)
        {
            var ps = play.GetParameters();
            var args = new object[ps.Length];
            args[0] = clip;
            for (int i = 1; i < ps.Length; i++) args[i] = ps[i].ParameterType == typeof(int) ? (object)0 : (object)false;
            play.Invoke(null, args);
        }
    }

    static void Delete(string id)
    {
        foreach (var f in Directory.GetFiles(Dir, id + ".*")) if (!f.EndsWith(".meta")) AssetDatabase.DeleteAsset(f.Replace('\\', '/'));
        WordAudio.ClearCache();
    }

    /// <summary>Файлы с именами вида «tunduk.m4a→wav», «Түндүк.wav», «tündük.mp3» — по id, кыргызскому слову или латинице.</summary>
    static void ImportFolder()
    {
        string src = EditorUtility.OpenFolderPanel("Папка с записями слов (wav, mp3, ogg, aiff)", "", "");
        if (string.IsNullOrEmpty(src)) return;
        Directory.CreateDirectory(Dir);
        var byName = new Dictionary<string, string>();
        foreach (var w in Vocab.Words)
        {
            byName[Norm(w.id)] = w.id; byName[Norm(w.ky)] = w.id; byName[Norm(w.latin)] = w.id;
        }
        int ok = 0; var skipped = new List<string>();
        foreach (var f in Directory.GetFiles(src))
        {
            string ext = Path.GetExtension(f).ToLowerInvariant();
            if (ext != ".wav" && ext != ".mp3" && ext != ".ogg" && ext != ".aiff" && ext != ".aif") { skipped.Add(Path.GetFileName(f)); continue; }
            if (!byName.TryGetValue(Norm(Path.GetFileNameWithoutExtension(f)), out string id)) { skipped.Add(Path.GetFileName(f)); continue; }
            foreach (var old in Directory.GetFiles(Dir, id + ".*")) if (!old.EndsWith(".meta")) AssetDatabase.DeleteAsset(old.Replace('\\', '/'));
            File.Copy(f, Dir + "/" + id + ext, true);
            ok++;
        }
        AssetDatabase.Refresh();
        WordAudio.ClearCache();
        EditorUtility.DisplayDialog("Импорт", "Импортировано: " + ok + (skipped.Count > 0 ? "\nНе распознаны (" + skipped.Count + "): " + string.Join(", ", skipped.Take(12)) : ""), "OK");
    }

    static string Norm(string s) => new string((s ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    static void CopyList()
    {
        var sb = new System.Text.StringBuilder("Озвучка словаря MURAS — назовите файл как id (например tunduk.wav)\n\n");
        foreach (var w in Vocab.Words) sb.Append(w.id).Append('\t').Append(w.ky).Append('\t').Append(w.ru).Append('\n');
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log("[Learning] список из " + Vocab.Words.Count + " слов скопирован в буфер обмена");
    }
}
