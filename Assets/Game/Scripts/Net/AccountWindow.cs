using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Окно «Аккаунт»: вход / регистрация, статус облачного сохранения, вход в класс по коду учителя, выход.
/// Открывается из паузы. Без аккаунта игра работает как раньше.
/// </summary>
public class AccountWindow : MonoBehaviour
{
    static AccountWindow instance;
    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;

    GameObject canvas, root, loginPage, accountPage;
    TMP_InputField userField, passField, nameField, serverField, codeField;
    TMP_Text title, message, statusText, nameText, classText, modeHint;
    Button modeButton, submitButton;
    bool registerMode;

    public static void Open()
    {
        if (instance == null) instance = new GameObject("AccountWindow").AddComponent<AccountWindow>();
        instance.Show();
    }

    /// <summary>Esc: закрыть окно, если открыто (вызывается из паузы раньше её собственной обработки).</summary>
    public static bool CloseIfOpen()
    {
        if (!IsOpen) return false;
        instance.root.SetActive(false);
        return true;
    }

    void OnEnable() { MurasCloud.Changed += Refresh; }
    void OnDisable() { MurasCloud.Changed -= Refresh; }

    void Show()
    {
        if (root == null) Build();
        root.SetActive(true);
        message.text = "";
        Refresh();
    }

    void Build()
    {
        canvas = MurasUI.Canvas("AccountCanvas", transform, 520);
        DontDestroyOnLoad(gameObject);
        root = MurasUI.Overlay(canvas.transform, 0.55f);
        var win = MurasUI.WindowBox(root.transform, new Vector2(760, 720));
        MurasUI.CloseButton(win.transform, () => root.SetActive(false));
        title = MurasUI.Text(win.transform, "", 40, MurasUI.Gold, new Vector2(0, 310), new Vector2(680, 56), FontStyles.Bold);
        message = MurasUI.Text(win.transform, "", 21, MurasUI.Red, new Vector2(0, -300), new Vector2(680, 60));

        // ---------------- страница входа / регистрации
        loginPage = MurasUI.Rect("Login", win.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 720));
        modeHint = MurasUI.Text(loginPage.transform, "", 20, MurasUI.Muted, new Vector2(0, 255), new Vector2(660, 56));
        userField = Field(loginPage.transform, Loc.T("Логин (латиница)", "Username (latin)"), new Vector2(0, 175), false);
        passField = Field(loginPage.transform, Loc.T("Пароль (от 8 символов)", "Password (8+ characters)"), new Vector2(0, 100), true);
        nameField = Field(loginPage.transform, Loc.T("Имя — его увидит учитель", "Name — your teacher will see it"), new Vector2(0, 25), false);
        nameField.characterLimit = 48;
        submitButton = MurasUI.Button(loginPage.transform, "", new Vector2(0, -60), new Vector2(420, 62), Submit, 24);
        modeButton = MurasUI.Button(loginPage.transform, "", new Vector2(0, -135), new Vector2(420, 50), ToggleMode, 19);
        modeButton.GetComponent<Image>().color = MurasUI.Card;
        MurasUI.Text(loginPage.transform, Loc.T("Сервер", "Server"), 17, MurasUI.Muted, new Vector2(-250, -205), new Vector2(120, 30));
        serverField = Field(loginPage.transform, MurasCloud.DefaultServer, new Vector2(60, -205), false, new Vector2(480, 44), 18);
        serverField.text = MurasCloud.ServerUrl;
        MurasUI.Text(loginPage.transform, Loc.T("Можно играть и без аккаунта — просто закройте окно.", "You can play without an account — just close this window."),
                     17, MurasUI.Muted, new Vector2(0, -250), new Vector2(660, 30), FontStyles.Italic);

        // ---------------- страница аккаунта
        accountPage = MurasUI.Rect("Account", win.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 720));
        nameText = MurasUI.Text(accountPage.transform, "", 30, MurasUI.Cream, new Vector2(0, 230), new Vector2(680, 44), FontStyles.Bold);
        statusText = MurasUI.Text(accountPage.transform, "", 21, MurasUI.Muted, new Vector2(0, 180), new Vector2(680, 50));
        classText = MurasUI.Text(accountPage.transform, "", 22, MurasUI.Cream, new Vector2(0, 115), new Vector2(680, 40));
        codeField = Field(accountPage.transform, Loc.T("Код класса от учителя", "Class code from your teacher"), new Vector2(-95, 55), false, new Vector2(330, 54));
        codeField.characterLimit = 8;
        MurasUI.Button(accountPage.transform, Loc.T("Войти в класс", "Join class"), new Vector2(175, 55), new Vector2(190, 54), JoinClass, 20);
        MurasUI.Button(accountPage.transform, Loc.T("Синхронизировать сейчас", "Sync now"), new Vector2(0, -30), new Vector2(420, 58), SyncNow, 22);
        MurasUI.Button(accountPage.transform, Loc.T("Таблица рекордов", "Leaderboards"), new Vector2(0, -100), new Vector2(420, 58), () => { root.SetActive(false); LeaderboardWindow.Open(); }, 22);
        var logout = MurasUI.Button(accountPage.transform, Loc.T("Выйти из аккаунта", "Log out"), new Vector2(0, -170), new Vector2(420, 50), Logout, 19);
        logout.GetComponent<Image>().color = MurasUI.Card;
        MurasUI.Text(accountPage.transform, Loc.T("Прогресс сохраняется на сервере и доступен с любого компьютера.", "Progress is stored on the server and available on any computer."),
                     17, MurasUI.Muted, new Vector2(0, -235), new Vector2(660, 30), FontStyles.Italic);
        root.SetActive(false);
    }

    TMP_InputField Field(Transform parent, string placeholder, Vector2 pos, bool password, Vector2? size = null, float font = 22f)
    {
        Vector2 sz = size ?? new Vector2(520, 56);
        var go = MurasUI.Box("Input", parent, pos, sz, new Color(0.07f, 0.055f, 0.045f, 1f));
        var area = MurasUI.Rect("TextArea", go.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-28, -10));
        area.AddComponent<RectMask2D>();
        var ph = MurasUI.Text(area.transform, placeholder, font, new Color(MurasUI.Muted.r, MurasUI.Muted.g, MurasUI.Muted.b, 0.6f), Vector2.zero, Vector2.zero, FontStyles.Italic, TextAlignmentOptions.MidlineLeft);
        Stretch(ph.rectTransform);
        ph.textWrappingMode = TextWrappingModes.NoWrap;
        var txt = MurasUI.Text(area.transform, "", font, MurasUI.Cream, Vector2.zero, Vector2.zero, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        Stretch(txt.rectTransform);
        txt.textWrappingMode = TextWrappingModes.NoWrap;
        txt.raycastTarget = true;
        var input = go.AddComponent<TMP_InputField>();
        input.textViewport = (RectTransform)area.transform;
        input.textComponent = txt;
        input.placeholder = ph;
        input.caretColor = MurasUI.Gold;
        input.selectionColor = new Color(MurasUI.Gold.r, MurasUI.Gold.g, MurasUI.Gold.b, 0.35f);
        input.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
        input.characterLimit = 128;
        input.pointSize = font;
        return input;
    }

    static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

    void Refresh()
    {
        if (root == null) return;
        bool inAcc = MurasCloud.LoggedIn;
        loginPage.SetActive(!inAcc);
        accountPage.SetActive(inAcc);
        if (inAcc)
        {
            title.text = Loc.T("АККАУНТ", "ACCOUNT");
            nameText.text = MurasCloud.DisplayName + "  <size=70%><color=#B8AD94>@" + MurasCloud.Username + "</color></size>";
            statusText.text = MurasCloud.StatusText() + (MurasCloud.PendingCount > 0 ? "\n<size=85%>" + Loc.T("В очереди событий: ", "Queued events: ") + MurasCloud.PendingCount + "</size>" : "");
            statusText.color = MurasCloud.Status == MurasCloud.State.Online ? MurasUI.Green : MurasCloud.Status == MurasCloud.State.Error ? MurasUI.Red : MurasUI.Muted;
            classText.text = string.IsNullOrEmpty(MurasCloud.ClassName)
                ? Loc.T("Класс: не выбран — введите код учителя", "Class: none — enter your teacher's code")
                : Loc.T("Класс: ", "Class: ") + "<color=#E8B04A>" + MurasCloud.ClassName + "</color>";
        }
        else
        {
            title.text = registerMode ? Loc.T("РЕГИСТРАЦИЯ", "SIGN UP") : Loc.T("ВХОД", "LOG IN");
            modeHint.text = Loc.T("Аккаунт сохраняет прогресс в облаке, а учитель видит, какие слова вы выучили.",
                                  "An account keeps your progress in the cloud and lets your teacher see which words you learned.");
            nameField.gameObject.SetActive(registerMode);
            submitButton.GetComponentInChildren<TMP_Text>().text = registerMode ? Loc.T("Создать аккаунт", "Create account") : Loc.T("Войти", "Log in");
            modeButton.GetComponentInChildren<TMP_Text>().text = registerMode ? Loc.T("У меня уже есть аккаунт", "I already have an account") : Loc.T("Нет аккаунта? Регистрация", "No account? Sign up");
        }
    }

    void ToggleMode() { registerMode = !registerMode; message.text = ""; Refresh(); }

    void Submit()
    {
        MurasCloud.ServerUrl = serverField.text;
        string u = userField.text.Trim(), p = passField.text;
        if (u.Length == 0 || p.Length == 0) { Say(Loc.T("Введите логин и пароль", "Enter username and password"), false); return; }
        submitButton.interactable = false;
        Say(Loc.T("Подключаемся…", "Connecting…"), true);
        System.Action<bool, string> done = (ok, msg) =>
        {
            submitButton.interactable = true;
            if (ok) { passField.text = ""; Say(Loc.T("Добро пожаловать, ", "Welcome, ") + msg + "!", true); MurasUI.Play("word_chime", 0.6f); }
            else Say(msg, false);
            Refresh();
        };
        if (registerMode)
        {
            string n = nameField.text.Trim();
            if (n.Length == 0) { submitButton.interactable = true; Say(Loc.T("Введите имя", "Enter your name"), false); return; }
            MurasCloud.Register(u, p, n, done);
        }
        else MurasCloud.Login(u, p, done);
    }

    void JoinClass()
    {
        if (codeField.text.Trim().Length < 4) { Say(Loc.T("Введите код класса", "Enter the class code"), false); return; }
        MurasCloud.JoinClass(codeField.text, (ok, msg) =>
        {
            if (ok) { codeField.text = ""; Say(Loc.T("Вы в классе ", "You joined ") + msg, true); }
            else Say(msg, false);
            Refresh();
        });
    }

    void SyncNow() => MurasCloud.SyncNow((ok, msg) => { if (ok) Say(Loc.T("Сохранено в облаке ", "Saved to the cloud ") + msg, true); else if (msg != "") Say(msg, false); Refresh(); });

    void Logout() { MurasCloud.Logout(); registerMode = false; Say(Loc.T("Вы вышли из аккаунта. Прогресс остался на этом компьютере.", "Logged out. Progress stays on this computer."), true); Refresh(); }

    void Say(string text, bool good) { message.text = text; message.color = good ? MurasUI.Green : MurasUI.Red; }
}
