using UnityEditor;
using UnityEngine;

/// <summary>Меню для облака MURAS: настройка HTTP для локального сервера и проверки в Play Mode.</summary>
public static class CloudTools
{
    [MenuItem("Tools/Diploma/Cloud: Setup (allow HTTP, run in background)")]
    static void AllowHttp()
    {
        // Локальный сервер работает по http://127.0.0.1:8000; развёрнутый — по https. Unity по умолчанию запрещает http.
        PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
        // Игра не замирает, когда окно свёрнуто, — облачное сохранение и очередь событий продолжают работать.
        PlayerSettings.runInBackground = true;
        Application.runInBackground = true;
        Debug.Log("[Cloud] Разрешены http-запросы (для локального сервера). Для релиза с https можно вернуть Not Allowed.");
    }

    // ------------------------------------------------------------------ локальный сервер
    static string ServerDir => System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Server"));

    [MenuItem("Tools/Diploma/Cloud: Start local server")]
    static void StartServer()
    {
        // Создаёт виртуальное окружение в Server/.venv (один раз), ставит зависимости и запускает сервер в фоне.
        // Журналы — Server/instance/setup.log и server.log, номер процесса — Server/instance/server.pid.
        string inst = System.IO.Path.Combine(ServerDir, "instance");
        System.IO.Directory.CreateDirectory(inst);
        string sh = System.IO.Path.Combine(inst, "start.sh");
        System.IO.File.WriteAllText(sh,
            "#!/bin/bash\n" +
            "cd \"$(dirname \"$0\")/..\"\n" +
            "if [ ! -x .venv/bin/python ]; then\n" +
            "  python3 -m venv .venv && .venv/bin/pip install -q --disable-pip-version-check Flask PyJWT pydantic\n" +
            "fi > instance/setup.log 2>&1\n" +
            "[ -f instance/server.pid ] && kill \"$(cat instance/server.pid)\" 2>/dev/null\n" +
            "export TEACHER_INVITE_CODE=\"${TEACHER_INVITE_CODE:-teach2027}\"\n" +
            "nohup .venv/bin/python wsgi.py > instance/server.log 2>&1 &\n" +
            "echo $! > instance/server.pid\n");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("/bin/bash", "\"" + sh + "\"") { UseShellExecute = false, CreateNoWindow = true });
        Debug.Log("[Cloud] Запускаю локальный сервер… через ~10–60 с: http://127.0.0.1:8000/health  (журнал: Server/instance/server.log)");
    }

    [MenuItem("Tools/Diploma/Cloud: Stop local server")]
    static void StopServer()
    {
        string pid = System.IO.Path.Combine(ServerDir, "instance", "server.pid");
        if (!System.IO.File.Exists(pid)) { Debug.Log("[Cloud] Сервер не запущен из Unity."); return; }
        if (int.TryParse(System.IO.File.ReadAllText(pid).Trim(), out int id))
            try { System.Diagnostics.Process.GetProcessById(id).Kill(); } catch (System.Exception) { }
        System.IO.File.Delete(pid);
        Debug.Log("[Cloud] Локальный сервер остановлен.");
    }

    [MenuItem("Tools/Diploma/Cloud: Open teacher dashboard")]
    static void OpenDashboard() => Application.OpenURL(MurasCloud.ServerUrl + "/teacher/");

    static bool Playing() { if (!Application.isPlaying) Debug.LogWarning("Сначала нажми Play."); return Application.isPlaying; }

    [MenuItem("Tools/Diploma/Play Mode: Cloud/Open Account window")] static void Acc() { if (Playing()) AccountWindow.Open(); }
    [MenuItem("Tools/Diploma/Play Mode: Cloud/Open Leaderboards")] static void Lb() { if (Playing()) LeaderboardWindow.Open(); }

    [MenuItem("Tools/Diploma/Play Mode: Cloud/Register test student")]
    static void TestRegister()
    {
        if (!Playing()) return;
        string u = "test_" + Random.Range(1000, 9999);
        MurasCloud.Register(u, "password123", "Тест " + u.Substring(5), (ok, msg) => Debug.Log("[Cloud] register " + u + ": " + ok + " " + msg));
    }

    [MenuItem("Tools/Diploma/Play Mode: Cloud/Sync now")]
    static void Sync() { if (Playing()) MurasCloud.SyncNow((ok, msg) => Debug.Log("[Cloud] sync: " + ok + " " + msg)); }

    [MenuItem("Tools/Diploma/Play Mode: Cloud/Unlock 5 words + report scores")]
    static void Demo()
    {
        if (!Playing()) return;
        Vocab.Unlock("salam", "rahmat", "kosh", "boz_uy", "kerege");
        MurasCloud.ReportScore("ordo", Random.Range(6, 20));
        MurasCloud.ReportScore("race", Random.Range(42000, 70000));
        Debug.Log("[Cloud] queued: " + MurasCloud.PendingCount);
    }

    [MenuItem("Tools/Diploma/Play Mode: Cloud/Log status")]
    static void Status() { if (Playing()) Debug.Log("[Cloud] " + MurasCloud.Status + " · " + MurasCloud.StatusText() + " · user=" + MurasCloud.Username + " · queue=" + MurasCloud.PendingCount + " · server=" + MurasCloud.ServerUrl); }

    [MenuItem("Tools/Diploma/Play Mode: Cloud/Log out")]
    static void Out() { if (Playing()) MurasCloud.Logout(); }
}
