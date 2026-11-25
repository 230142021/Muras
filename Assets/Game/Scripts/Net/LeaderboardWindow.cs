using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Таблица рекордов мини-игр: лучшие 10 игроков (все или только мой класс) и моё место.</summary>
public class LeaderboardWindow : MonoBehaviour
{
    static LeaderboardWindow instance;
    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;

    static readonly (string id, string ru, string en)[] Games =
    {
        ("ordo", "Ордо", "Ordo"), ("kokboru", "Көк бөрү", "Kök börü"), ("race", "Ат чабыш", "Horse race"),
        ("komuz", "Комуз", "Komuz"), ("cooking", "Бешбармак", "Beshbarmak"), ("fishing", "Балык", "Fishing"),
    };

    GameObject canvas, root;
    TMP_Text header, body, meText;
    readonly List<Image> tabs = new List<Image>();
    Image scopeAll, scopeClass;
    int game;
    bool classOnly;

    public static void Open()
    {
        if (instance == null) instance = new GameObject("LeaderboardWindow").AddComponent<LeaderboardWindow>();
        if (instance.root == null) instance.Build();
        instance.root.SetActive(true);
        instance.Load();
    }

    public static bool CloseIfOpen()
    {
        if (!IsOpen) return false;
        instance.root.SetActive(false);
        return true;
    }

    void Build()
    {
        DontDestroyOnLoad(gameObject);
        canvas = MurasUI.Canvas("LeaderboardCanvas", transform, 521);
        root = MurasUI.Overlay(canvas.transform, 0.55f);
        var win = MurasUI.WindowBox(root.transform, new Vector2(980, 760));
        MurasUI.CloseButton(win.transform, () => root.SetActive(false));
        MurasUI.Text(win.transform, Loc.T("РЕКОРДЫ", "LEADERBOARDS"), 40, MurasUI.Gold, new Vector2(0, 330), new Vector2(800, 56), FontStyles.Bold);
        for (int i = 0; i < Games.Length; i++)
        {
            int k = i;
            var b = MurasUI.Button(win.transform, Loc.T(Games[i].ru, Games[i].en), new Vector2(-400 + i * 160, 260), new Vector2(150, 50), () => { game = k; Load(); }, 19);
            tabs.Add(b.GetComponent<Image>());
        }
        scopeAll = MurasUI.Button(win.transform, Loc.T("Все игроки", "Everyone"), new Vector2(-110, 195), new Vector2(200, 44), () => { classOnly = false; Load(); }, 18).GetComponent<Image>();
        scopeClass = MurasUI.Button(win.transform, Loc.T("Мой класс", "My class"), new Vector2(110, 195), new Vector2(200, 44), () => { classOnly = true; Load(); }, 18).GetComponent<Image>();
        header = MurasUI.Text(win.transform, "", 22, MurasUI.Muted, new Vector2(0, 140), new Vector2(820, 36));
        body = MurasUI.Text(win.transform, "", 25, MurasUI.Cream, new Vector2(0, -95), new Vector2(760, 430), FontStyles.Normal, TextAlignmentOptions.TopLeft);
        meText = MurasUI.Text(win.transform, "", 23, MurasUI.Gold, new Vector2(0, -335), new Vector2(820, 40), FontStyles.Bold);
        root.SetActive(false);
    }

    void Load()
    {
        for (int i = 0; i < tabs.Count; i++) tabs[i].color = i == game ? MurasUI.Gold * 0.85f : MurasUI.ButtonCol;
        scopeAll.color = classOnly ? MurasUI.Card : MurasUI.ButtonCol;
        scopeClass.color = classOnly ? MurasUI.ButtonCol : MurasUI.Card;
        meText.text = "";
        if (!MurasCloud.LoggedIn)
        {
            header.text = "";
            body.text = "\n\n<align=center>" + Loc.T("Войдите в аккаунт (Пауза → Аккаунт), чтобы видеть рекорды.", "Log in (Pause → Account) to see leaderboards.") + "</align>";
            return;
        }
        header.text = Loc.T("Загрузка…", "Loading…");
        body.text = "";
        string id = Games[game].id;
        MurasCloud.GetLeaderboard(id, classOnly, (ok, d) =>
        {
            if (root == null || Games[game].id != id) return;
            if (!ok) { header.text = MurasCloud.LastError; return; }
            bool asc = CloudJson.S(d, "order") == "asc";
            string unit = CloudJson.S(d, "unit") ?? "";
            header.text = Loc.T(Games[game].ru, Games[game].en) + "  ·  " + (classOnly ? Loc.T("мой класс", "my class") : Loc.T("все игроки", "everyone")) +
                          "  ·  " + (asc ? Loc.T("меньше — лучше", "lower is better") : Loc.T("больше — лучше", "higher is better"));
            var top = CloudJson.A(d, "top");
            var sb = new System.Text.StringBuilder();
            if (top == null || top.Count == 0) sb.Append("\n<align=center><color=#B8AD94>" + Loc.T("Пока никто не играл. Будьте первым!", "Nobody has played yet. Be the first!") + "</color></align>");
            else
                foreach (var o in top)
                {
                    var r = CloudJson.Obj(o);
                    int rank = CloudJson.I(r, "rank");
                    string medal = rank == 1 ? "<color=#E8B04A>" : rank == 2 ? "<color=#D8D8D8>" : rank == 3 ? "<color=#C98A4B>" : "<color=#F5EDD9>";
                    sb.Append(medal).Append("<mspace=0.6em>").Append(rank.ToString().PadLeft(2)).Append("</mspace>.  ")
                      .Append(Escape(CloudJson.S(r, "display_name"))).Append("</color><pos=78%>")
                      .Append(Format(CloudJson.I(r, "best"), asc, unit)).Append('\n');
                }
            body.text = sb.ToString();
            var me = CloudJson.O(d, "me");
            meText.text = me != null
                ? Loc.T("Ваше место: ", "Your rank: ") + CloudJson.I(me, "rank") + "  ·  " + Format(CloudJson.I(me, "best"), asc, unit)
                : Loc.T("Сыграйте, чтобы попасть в таблицу", "Play to get on the board");
        });
    }

    static string Format(int v, bool asc, string unit) => asc ? (v / 1000f).ToString("0.0") + " s" : v + " " + unit;
    static string Escape(string s) => (s ?? "").Replace("<", "‹").Replace(">", "›");
}
