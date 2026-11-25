using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Клиент сервера MURAS: аккаунт, облачное сохранение, события для учителя и рекорды мини-игр.
/// Игра полностью работает и без аккаунта — сеть подключается только после входа.
///
/// • Сохранение: раз в 20 с (и при выходе) сравнивает состояние с последним отправленным; если изменилось —
///   PUT /progress с base_version. Если сервер слил сохранения с другого устройства, применяет результат.
/// • События и рекорды пишутся в очередь на диске и отправляются пачками; без интернета копятся и уходят позже.
///   У каждого события свой GUID, поэтому повторная отправка после обрыва не создаёт дублей.
/// </summary>
public class MurasCloud : MonoBehaviour
{
    public enum State { LoggedOut, Online, Syncing, Offline, Error }

    public const string DefaultServer = "http://127.0.0.1:8000";
    const string KServer = "MURAS_ServerUrl", KToken = "MURAS_Token", KUser = "MURAS_UserJson", KVersion = "MURAS_CloudVersion";
    const float SaveInterval = 20f, FlushInterval = 15f, Timeout = 12f;

    public static MurasCloud Instance { get; private set; }
    public static event Action Changed;

    public static State Status { get; private set; } = State.LoggedOut;
    public static string LastError { get; private set; } = "";
    public static string LastSyncTime { get; private set; } = "";
    public static bool LoggedIn => !string.IsNullOrEmpty(PlayerPrefs.GetString(KToken, ""));
    public static string DisplayName { get; private set; } = "";
    public static string Username { get; private set; } = "";
    public static string Role { get; private set; } = "";
    public static string ClassName { get; private set; } = "";
    public static int PendingCount => Instance != null ? Instance.queue.items.Count : 0;

    public static string ServerUrl
    {
        get
        {
            string s = PlayerPrefs.GetString(KServer, "");
            if (!string.IsNullOrEmpty(s)) return s.TrimEnd('/');
            var cfg = Resources.Load<TextAsset>("MURAS/server_url");
            return cfg != null && cfg.text.Trim().Length > 0 ? cfg.text.Trim().TrimEnd('/') : DefaultServer;
        }
        set { PlayerPrefs.SetString(KServer, (value ?? "").Trim().TrimEnd('/')); PlayerPrefs.Save(); }
    }

    // ------------------------------------------------------------------ очередь на диске
    [Serializable] class QueueItem { public string kind; public string body; }
    [Serializable] class QueueFile { public List<QueueItem> items = new List<QueueItem>(); }
    QueueFile queue = new QueueFile();
    string QueuePath => Path.Combine(Application.persistentDataPath, "muras_queue_" + (string.IsNullOrEmpty(Username) ? "anon" : Username) + ".json");

    string lastPushedHash = "";
    float nextSave, nextFlush, backoff = FlushInterval;
    bool busySave, busyFlush, applying;
    float sessionStart;

    // ------------------------------------------------------------------ старт
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        var go = new GameObject("MurasCloud");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<MurasCloud>();
    }

    void Awake()
    {
        Instance = this;
        ReadUser();
        LoadQueue();
        GameManager.QuestCompleted += OnQuest;
        sessionStart = Time.realtimeSinceStartup;
        if (LoggedIn)
        {
            SetStatus(State.Online);
            Track("session_start", CloudJson.Object("platform", Application.platform.ToString(), "version", Application.version));
            StartCoroutine(PullAndMerge(null));
        }
    }

    void OnDestroy() { GameManager.QuestCompleted -= OnQuest; }

    void OnQuest(string id, int xp)
    {
        Track("quest_completed", CloudJson.Object("id", id, "xp", xp));
        nextSave = 0f;                                  // важное событие — сохраняем сразу
    }

    void Update()
    {
        if (!LoggedIn) return;
        float t = Time.unscaledTime;
        if (t >= nextSave && !busySave)
        {
            nextSave = t + SaveInterval;
            if (StateHash() != lastPushedHash) StartCoroutine(Push(null));
        }
        if (t >= nextFlush && !busyFlush && queue.items.Count > 0)
        {
            nextFlush = t + backoff;
            StartCoroutine(Flush());
        }
    }

    void OnApplicationPause(bool paused) { if (paused && LoggedIn) { SaveQueue(); StartCoroutine(Push(null)); } }

    void OnApplicationQuit()
    {
        if (!LoggedIn) return;
        Track("session_end", CloudJson.Object("minutes", Mathf.RoundToInt((Time.realtimeSinceStartup - sessionStart) / 60f)));
        SaveQueue();
        // последняя попытка отправить сохранение синхронно (короткий таймаут, игра не «зависает»)
        var req = MakeRequest("PUT", "/api/v1/progress", BuildSaveJson(PlayerPrefs.GetInt(KVersion, 0)));
        req.timeout = 3;
        var op = req.SendWebRequest();
        float until = Time.realtimeSinceStartup + 3f;
        while (!op.isDone && Time.realtimeSinceStartup < until) System.Threading.Thread.Sleep(20);
        req.Dispose();
    }

    // ================================================================== аккаунт
    public static void Register(string username, string password, string displayName, Action<bool, string> done)
    {
        string body = CloudJson.Object("username", username, "password", password, "display_name", displayName, "role", "student");
        Instance.StartCoroutine(Instance.Auth("/api/v1/auth/register", body, done));
    }

    public static void Login(string username, string password, Action<bool, string> done)
    {
        string body = CloudJson.Object("username", username, "password", password);
        Instance.StartCoroutine(Instance.Auth("/api/v1/auth/login", body, done));
    }

    public static void Logout()
    {
        if (Instance != null) Instance.SaveQueue();
        PlayerPrefs.DeleteKey(KToken); PlayerPrefs.DeleteKey(KUser); PlayerPrefs.DeleteKey(KVersion);
        PlayerPrefs.Save();
        DisplayName = Username = Role = ClassName = "";
        if (Instance != null) { Instance.queue = new QueueFile(); Instance.lastPushedHash = ""; }
        SetStatus(State.LoggedOut);
    }

    public static void JoinClass(string code, Action<bool, string> done)
    {
        Instance.StartCoroutine(Instance.Send("POST", "/api/v1/classes/join", CloudJson.Object("code", code.Trim().ToUpperInvariant()), (ok, code2, json) =>
        {
            if (ok) { ClassName = CloudJson.S(CloudJson.O(CloudJson.Obj(CloudJson.Parse(json)), "class"), "name") ?? ""; Changed?.Invoke(); }
            done?.Invoke(ok, ok ? ClassName : ErrorText(code2, json));
        }));
    }

    public static void SyncNow(Action<bool, string> done)
    {
        if (!LoggedIn) { done?.Invoke(false, Loc.T("Сначала войдите в аккаунт", "Log in first")); return; }
        Instance.StartCoroutine(Instance.PullAndMerge(done));
    }

    public static void GetLeaderboard(string game, bool classOnly, Action<bool, Dictionary<string, object>> done)
    {
        Instance.StartCoroutine(Instance.Send("GET", "/api/v1/leaderboard/" + game + "?limit=10&scope=" + (classOnly ? "class" : "global"), null,
            (ok, code, json) => done?.Invoke(ok, ok ? CloudJson.Obj(CloudJson.Parse(json)) : null)));
    }

    IEnumerator Auth(string path, string body, Action<bool, string> done)
    {
        SetStatus(State.Syncing);
        string json = null; long code = 0; bool ok = false;
        yield return Raw("POST", path, body, false, (o, c, j) => { ok = o; code = c; json = j; });
        if (!ok)
        {
            SetStatus(LoggedIn ? State.Online : State.LoggedOut);
            done?.Invoke(false, ErrorText(code, json));
            yield break;
        }
        var d = CloudJson.Obj(CloudJson.Parse(json));
        PlayerPrefs.SetString(KToken, CloudJson.S(d, "token"));
        PlayerPrefs.SetString(KUser, CloudJson.Object("user", CloudJson.Raw(UserJson(CloudJson.O(d, "user")))));
        PlayerPrefs.SetInt(KVersion, 0);
        PlayerPrefs.Save();
        ReadUser();
        LoadQueue();
        Track("session_start", CloudJson.Object("platform", Application.platform.ToString(), "version", Application.version));
        yield return PullAndMerge(null);
        yield return Send("GET", "/api/v1/me", null, (o, c, j) =>
        {
            var cls = CloudJson.A(CloudJson.Obj(CloudJson.Parse(j)), "classes");
            if (o && cls != null && cls.Count > 0) ClassName = CloudJson.S(CloudJson.Obj(cls[0]), "name") ?? "";
        });
        done?.Invoke(true, DisplayName);
    }

    static string UserJson(Dictionary<string, object> u) =>
        CloudJson.Object("id", CloudJson.I(u, "id"), "username", CloudJson.S(u, "username"), "display_name", CloudJson.S(u, "display_name"), "role", CloudJson.S(u, "role"));

    void ReadUser()
    {
        var u = CloudJson.O(CloudJson.Obj(CloudJson.Parse(PlayerPrefs.GetString(KUser, ""))), "user");
        DisplayName = CloudJson.S(u, "display_name") ?? "";
        Username = CloudJson.S(u, "username") ?? "";
        Role = CloudJson.S(u, "role") ?? "";
    }

    // ================================================================== облачное сохранение
    static readonly string[] NonMonotonic = { "inv_", "daily_", "lantern_" };
    static bool Monotonic(string key) { foreach (string p in NonMonotonic) if (key.StartsWith(p)) return false; return true; }

    string StateHash()
    {
        var sb = new StringBuilder();
        sb.Append(GameManager.SavedXP).Append('|').Append(string.Join(",", GameManager.CompletedQuestIds())).Append('|').Append(SaveData.Revision);
        return sb.ToString();
    }

    string BuildSaveJson(int baseVersion) =>
        CloudJson.Object("base_version", baseVersion, "xp", GameManager.SavedXP, "quests", GameManager.CompletedQuestIds(),
                         "extra", CloudJson.Raw(CloudJson.Value(SaveData.Snapshot())), "client_info", Application.platform + " " + Application.version);

    /// <summary>Скачать сохранение, слить с локальным (ничего заработанного не теряется) и отправить результат.</summary>
    IEnumerator PullAndMerge(Action<bool, string> done)
    {
        if (busySave) { done?.Invoke(false, Loc.T("Синхронизация уже идёт", "Sync already running")); yield break; }
        busySave = true; SetStatus(State.Syncing);
        bool ok = false; long code = 0; string json = null;
        yield return Send("GET", "/api/v1/progress", null, (o, c, j) => { ok = o; code = c; json = j; });
        if (!ok) { busySave = false; done?.Invoke(false, ErrorText(code, json)); yield break; }

        var d = CloudJson.Obj(CloudJson.Parse(json));
        int serverVersion = CloudJson.I(d, "version");
        var serverExtra = CloudJson.O(d, "extra");
        var local = SaveData.Snapshot();
        var merged = new Dictionary<string, int>(local);
        if (serverExtra != null)
            foreach (var kv in serverExtra)
            {
                int v = kv.Value is double x ? (int)x : 0;
                if (!merged.TryGetValue(kv.Key, out int mine)) merged[kv.Key] = v;
                else if (Monotonic(kv.Key)) merged[kv.Key] = Mathf.Max(mine, v);
                // немонотонные (инвентарь, задания дня): оставляем локальное значение этого устройства
            }
        var quests = new HashSet<string>(GameManager.CompletedQuestIds());
        var sq = CloudJson.A(d, "quests");
        if (sq != null) foreach (var q in sq) if (q is string s) quests.Add(s);
        int xp = Mathf.Max(GameManager.SavedXP, CloudJson.I(d, "xp"));
        Apply(xp, quests, merged);
        PlayerPrefs.SetInt(KVersion, serverVersion);
        busySave = false;
        yield return Push(done);
    }

    IEnumerator Push(Action<bool, string> done)
    {
        if (busySave) { done?.Invoke(false, ""); yield break; }
        busySave = true; SetStatus(State.Syncing);
        string hash = StateHash();
        bool ok = false; long code = 0; string json = null;
        yield return Send("PUT", "/api/v1/progress", BuildSaveJson(PlayerPrefs.GetInt(KVersion, 0)), (o, c, j) => { ok = o; code = c; json = j; });
        busySave = false;
        if (!ok)
        {
            if (code == 409) { PlayerPrefs.SetInt(KVersion, 0); StartCoroutine(PullAndMerge(done)); yield break; }
            done?.Invoke(false, ErrorText(code, json));
            yield break;
        }
        var d = CloudJson.Obj(CloudJson.Parse(json));
        PlayerPrefs.SetInt(KVersion, CloudJson.I(d, "version"));
        if (CloudJson.B(d, "merged"))
        {
            // другое устройство успело сохраниться — применяем слитый вариант с сервера
            var ex = CloudJson.O(d, "extra");
            var dict = new Dictionary<string, int>();
            if (ex != null) foreach (var kv in ex) dict[kv.Key] = kv.Value is double x ? (int)x : 0;
            var qs = new HashSet<string>();
            var ql = CloudJson.A(d, "quests");
            if (ql != null) foreach (var q in ql) if (q is string s) qs.Add(s);
            Apply(CloudJson.I(d, "xp"), qs, dict);
            hash = StateHash();
        }
        PlayerPrefs.Save();
        lastPushedHash = hash;
        LastSyncTime = DateTime.Now.ToString("HH:mm");
        SetStatus(State.Online);
        done?.Invoke(true, LastSyncTime);
    }

    void Apply(int xp, IEnumerable<string> quests, Dictionary<string, int> extra)
    {
        applying = true;
        SaveData.ReplaceAll(extra);
        GameManager.ApplyCloudProgress(xp, quests);
        applying = false;
        Vocab_ChangedSafe();
    }

    static void Vocab_ChangedSafe() { try { Vocab.ClearSaved(); } catch (Exception) { } }   // ClearSaved только рассылает Changed — окна словаря перерисуются

    // ================================================================== события и рекорды
    /// <summary>Записать игровое событие (для панели учителя). Без аккаунта ничего не делает.</summary>
    public static void Track(string type, string payloadJson = "{}")
    {
        if (Instance == null || !LoggedIn || Instance.applying) return;
        string body = CloudJson.Object("id", Guid.NewGuid().ToString("N"), "type", type, "payload", CloudJson.Raw(payloadJson ?? "{}"),
                                       "time", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        Instance.Enqueue("event", body);
    }

    /// <summary>Результат мини-игры: в таблицу рекордов и в журнал событий.</summary>
    public static void ReportScore(string game, int score, int durationMs = 0)
    {
        if (Instance == null || !LoggedIn) return;
        Instance.Enqueue("score", CloudJson.Object("game", game, "score", score, "duration_ms", Mathf.Max(0, durationMs)));
        Track("minigame_result", CloudJson.Object("game", game, "score", score));
    }

    void Enqueue(string kind, string body)
    {
        queue.items.Add(new QueueItem { kind = kind, body = body });
        if (queue.items.Count > 1000) queue.items.RemoveRange(0, queue.items.Count - 1000);   // не растим файл бесконечно
        SaveQueue();
        if (nextFlush > Time.unscaledTime + 3f) nextFlush = Time.unscaledTime + 3f;
    }

    IEnumerator Flush()
    {
        busyFlush = true;
        // 1) события пачкой до 50 штук
        var batch = new List<QueueItem>();
        foreach (var it in queue.items) { if (it.kind == "event") batch.Add(it); if (batch.Count >= 50) break; }
        if (batch.Count > 0)
        {
            var sb = new StringBuilder("{\"events\":[");
            for (int i = 0; i < batch.Count; i++) { if (i > 0) sb.Append(','); sb.Append(batch[i].body); }
            sb.Append("]}");
            bool ok = false; long code = 0;
            yield return Send("POST", "/api/v1/events", sb.ToString(), (o, c, j) => { ok = o; code = c; });
            if (ok || code == 422) { foreach (var b in batch) queue.items.Remove(b); backoff = FlushInterval; }   // 422 — битые данные, повтор не поможет
            else { backoff = Mathf.Min(backoff * 2f, 120f); busyFlush = false; SaveQueue(); yield break; }
        }
        // 2) рекорды по одному
        var scores = queue.items.FindAll(x => x.kind == "score");
        foreach (var s in scores)
        {
            bool ok = false; long code = 0;
            yield return Send("POST", "/api/v1/scores", s.body, (o, c, j) => { ok = o; code = c; });
            if (ok || code == 422) queue.items.Remove(s);
            else { backoff = Mathf.Min(backoff * 2f, 120f); break; }
        }
        SaveQueue();
        busyFlush = false;
    }

    void LoadQueue()
    {
        queue = new QueueFile();
        try { if (File.Exists(QueuePath)) queue = JsonUtility.FromJson<QueueFile>(File.ReadAllText(QueuePath)) ?? new QueueFile(); }
        catch (Exception e) { Debug.LogWarning("MURAS cloud: очередь повреждена, начинаем новую: " + e.Message); }
    }

    void SaveQueue()
    {
        try { File.WriteAllText(QueuePath, JsonUtility.ToJson(queue)); }
        catch (Exception e) { Debug.LogWarning("MURAS cloud: не удалось сохранить очередь: " + e.Message); }
    }

    // ================================================================== HTTP
    UnityWebRequest MakeRequest(string method, string path, string body, bool auth = true)
    {
        var req = new UnityWebRequest(ServerUrl + path, method);
        if (body != null)
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.SetRequestHeader("Content-Type", "application/json");
        }
        req.downloadHandler = new DownloadHandlerBuffer();
        req.timeout = (int)Timeout;
        if (auth) req.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString(KToken, ""));
        return req;
    }

    IEnumerator Send(string method, string path, string body, Action<bool, long, string> done) => Raw(method, path, body, true, done);

    IEnumerator Raw(string method, string path, string body, bool auth, Action<bool, long, string> done)
    {
        using (var req = MakeRequest(method, path, body, auth))
        {
            yield return req.SendWebRequest();
            long code = req.responseCode;
            string text = req.downloadHandler != null ? req.downloadHandler.text : "";
            if (req.result == UnityWebRequest.Result.ConnectionError || req.result == UnityWebRequest.Result.DataProcessingError || code == 0)
            {
                LastError = Loc.T("Нет связи с сервером", "No connection to the server");
                if (LoggedIn) SetStatus(State.Offline);
                done?.Invoke(false, 0, null);
                yield break;
            }
            if (code == 401 && auth)
            {
                LastError = Loc.T("Сессия истекла — войдите снова", "Session expired — please log in again");
                Logout();
                done?.Invoke(false, code, text);
                yield break;
            }
            bool ok = code >= 200 && code < 300;
            if (!ok) { LastError = ErrorText(code, text); if (LoggedIn && Status == State.Syncing) SetStatus(State.Error); }
            else if (Status == State.Offline || Status == State.Error) SetStatus(State.Online);
            done?.Invoke(ok, code, text);
        }
    }

    public static string ErrorText(long code, string json)
    {
        if (code == 0) return Loc.T("Нет связи с сервером", "No connection to the server");
        var err = CloudJson.O(CloudJson.Obj(CloudJson.Parse(json)), "error");
        string c = CloudJson.S(err, "code") ?? "";
        switch (c)
        {
            case "invalid_credentials": return Loc.T("Неверный логин или пароль", "Wrong username or password");
            case "username_taken": return Loc.T("Этот логин уже занят", "This username is taken");
            case "class_not_found": return Loc.T("Класс с таким кодом не найден", "No class with this code");
            case "rate_limited": return Loc.T("Слишком много попыток, подождите минуту", "Too many attempts, wait a minute");
            case "forbidden": return Loc.T("Недостаточно прав", "Not allowed");
            case "validation_error":
                var det = CloudJson.A(err, "details");
                string field = det != null && det.Count > 0 ? CloudJson.S(CloudJson.Obj(det[0]), "field") : "";
                if (field == "username") return Loc.T("Логин: 3–32 символа, латиница, цифры, _ . -", "Username: 3–32 chars, latin letters, digits, _ . -");
                if (field == "password") return Loc.T("Пароль: не короче 8 символов", "Password: at least 8 characters");
                if (field == "display_name") return Loc.T("Имя: от 1 до 48 символов, без < >", "Name: 1–48 characters, no < >");
                return Loc.T("Неверные данные", "Invalid data");
        }
        return CloudJson.S(err, "message") ?? (Loc.T("Ошибка сервера ", "Server error ") + code);
    }

    static void SetStatus(State s)
    {
        Status = s;
        Changed?.Invoke();
    }

    public static string StatusText()
    {
        switch (Status)
        {
            case State.LoggedOut: return Loc.T("Без аккаунта — прогресс только на этом компьютере", "No account — progress stays on this computer");
            case State.Syncing: return Loc.T("Синхронизация…", "Syncing…");
            case State.Offline: return Loc.T("Нет связи — сохраним, когда сервер станет доступен", "Offline — will sync when the server is reachable");
            case State.Error: return LastError;
            default: return Loc.T("Сохранено в облаке", "Saved to the cloud") + (LastSyncTime != "" ? "  ·  " + LastSyncTime : "");
        }
    }
}
