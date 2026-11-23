using TMPro;
using UnityEngine;

/// <summary>Окно Эрмека перед скачками: выбор сложности (мышью или клавишами 1 / 2 / 3).</summary>
public class RaceMenu : MonoBehaviour, IModal
{
    private static RaceMenu instance;
    private RaceManager race;
    private GameObject canvas;

    public static void Show(RaceManager race)
    {
        if (instance != null || race == null) return;
        instance = new GameObject("RaceMenu").AddComponent<RaceMenu>();
        instance.race = race;
    }

    void Start()
    {
        Modal.Open(this);
        Time.timeScale = 0f;
        canvas = MurasUI.Canvas("RaceMenuCanvas", transform, 460);
        var root = MurasUI.Overlay(canvas.transform, 0.55f);
        var win = MurasUI.WindowBox(root.transform, new Vector2(820, 600));
        var w = win.transform;
        MurasUI.CloseButton(w, Close);
        MurasUI.Text(w, "АТ ЧАБЫШ", 48, MurasUI.Gold, new Vector2(0, 245), new Vector2(760, 60), FontStyles.Bold);
        MurasUI.Text(w, Loc.T("Эрмек: Салам, джигит! Кругов: " + race.laps + ", скачем через флажки, двое соперников. Насколько быстро поскачем?",
                              "Ermek: Салам, jigit! Laps: " + race.laps + ", ride through the flags, two rivals. How fast shall we ride?"),
                     21, MurasUI.Cream, new Vector2(0, 180), new Vector2(740, 60), FontStyles.Italic);
        string[] ru = { "Легко — соперники не спешат", "Средне — честная гонка", "Сложно — лучшие чабандесы айыла" };
        string[] en = { "Easy — the rivals take it slow", "Normal — a fair race", "Hard — the best riders of the ayil" };
        int[] xp = { 80, 150, 220 };
        for (int i = 0; i < 3; i++)
        {
            int d = i;
            bool won = SaveData.Get("race_win_d" + i) > 0;
            string label = (i + 1) + ".  " + RaceManager.DiffKy[i] + "\n<size=60%>" + Loc.T(ru[i], en[i]) + "   ·   " +
                           (won ? "<color=#5FC873>" + Loc.T("уже выиграно", "already won") + "</color>" : Loc.T("победа: +", "win: +") + xp[i] + " XP") + "</size>";
            MurasUI.Button(w, label, new Vector2(0, 75 - i * 105), new Vector2(600, 92), () => Go(d), 28);
        }
        MurasUI.Text(w, Loc.T("Управление: W — вперёд, A / D — поворот, Shift — рывок (тратит силы коня), Q — сдаться",
                              "Controls: W — go, A / D — turn, Shift — sprint (uses horse stamina), Q — give up"),
                     17, MurasUI.Muted, new Vector2(0, -255), new Vector2(760, 40));
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) Go(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) Go(1);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) Go(2);
    }

    void Go(int d)
    {
        Close();
        race.StartRace(d);
    }

    public void CloseModal() => Close();

    void Close()
    {
        if (instance != this) return;
        instance = null;
        Modal.Close(this);
        if (!PauseMenu.IsPaused) Time.timeScale = 1f;
        Destroy(gameObject);
    }
}
