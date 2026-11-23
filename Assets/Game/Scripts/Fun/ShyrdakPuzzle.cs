using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Пазл «Шырдак»: узор войлочного ковра разрезан на квадраты, которые повернулись.
/// Кликай по квадрату — он поворачивается на 90°. Собери узор, как на образце справа.
/// Три узора: Тумар (3×3), Кочкор мүйүз (4×4), Ит куйрук (5×5) — каждый с объяснением смысла.
/// Открывается у места работы Апы (ShyrdakStation). Картинки — Resources/MURAS/shyrdak_N.
/// </summary>
public class ShyrdakPuzzle : MonoBehaviour, IModal
{
    public static ShyrdakPuzzle Instance { get; private set; }

    static readonly string[] NameKy = { "Тумар", "Кочкор мүйүз", "Ит куйрук" };
    static readonly string[] MeanRu =
    {
        "<b>Тумар</b> — оберег. Треугольники охраняют дом и семью от беды. Такой узор часто вышивали для детей.",
        "<b>Кочкор мүйүз</b> — «рога барана». Самый любимый кыргызский узор: он желает богатства, силы и много скота.",
        "<b>Ит куйрук</b> — «собачий хвост». Волна по краю ковра — знак верности и защиты, как верный пёс у юрты."
    };
    static readonly string[] MeanEn =
    {
        "<b>Tumar</b> — an amulet. The triangles guard the home and family from harm. It was often stitched for children.",
        "<b>Kochkor müyüz</b> — \"ram's horns\". The best-loved Kyrgyz pattern: it wishes wealth, strength and large herds.",
        "<b>It kuyruk</b> — \"dog's tail\". The wave along the edge stands for loyalty and protection, like a faithful dog by the yurt."
    };
    static readonly string[][] LevelWords = { new[] { "shyrdak", "oyu", "tumar" }, new[] { "kochkor" }, new[] { "it_kuyruk" } };
    static readonly int[] LevelXp = { 40, 60, 80 };

    private GameObject canvas, root, board, menu, done;
    private TMP_Text title, info, doneText;
    private RawImage preview;
    private int level, moves;
    private int[] rot;
    private RectTransform[] tiles;
    private float[] shown;
    private bool open, solved;

    public static ShyrdakPuzzle Ensure()
    {
        if (Instance == null) new GameObject("ShyrdakPuzzle").AddComponent<ShyrdakPuzzle>();
        return Instance;
    }

    void Awake() => Instance = this;
    void Start() { if (canvas == null) Build(); Loc.Changed += Rebuild; }
    void OnDestroy() { Loc.Changed -= Rebuild; if (open) { Modal.Close(this); if (!PauseMenu.IsPaused) Time.timeScale = 1f; } }

    public void Open()
    {
        if (canvas == null) Build();
        open = true; Modal.Open(this);
        Time.timeScale = 0f;
        root.SetActive(true);
        ShowMenu();
    }

    public void Close()
    {
        if (!open) return;
        open = false; root.SetActive(false); Modal.Close(this);
        if (!PauseMenu.IsPaused) Time.timeScale = 1f;
    }
    public void CloseModal() => Close();

    void Rebuild() { bool was = open; if (canvas != null) Destroy(canvas); Build(); if (was) { root.SetActive(true); ShowMenu(); } }

    void Build()
    {
        canvas = MurasUI.Canvas("ShyrdakCanvas", transform, 460);
        root = MurasUI.Overlay(canvas.transform, 0.7f);
        var win = MurasUI.WindowBox(root.transform, new Vector2(1240, 860));
        MurasUI.CloseButton(win.transform, Close);
        title = MurasUI.Text(win.transform, "ШЫРДАК", 48, MurasUI.Gold, new Vector2(0, 380), new Vector2(1000, 60), FontStyles.Bold);

        menu = MurasUI.Rect("Menu", win.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 760));
        MurasUI.Text(menu.transform, Loc.T("Апа: Балам, помоги мне сшить шырдак! Кусочки войлока перепутались — поверни их, чтобы узор сложился.",
                                           "Apa: Балам, help me sew a shyrdak! The felt pieces got mixed up — turn them so the pattern comes together."),
                     23, MurasUI.Cream, new Vector2(0, 290), new Vector2(1000, 70), FontStyles.Italic);
        for (int i = 0; i < 3; i++)
        {
            int captured = i;
            bool doneBefore = SaveData.Get("shyrdak_" + (i + 1)) == 1;
            bool unlocked = i == 0 || SaveData.Get("shyrdak_" + i) == 1;
            var card = MurasUI.Box("Level" + i, menu.transform, new Vector2(-360 + i * 360, 20), new Vector2(320, 380), MurasUI.Card);
            var img = MurasUI.Rect("Pic", card.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 50), new Vector2(250, 250)).AddComponent<RawImage>();
            img.texture = Resources.Load<Texture2D>("MURAS/shyrdak_" + (i + 1));
            img.color = unlocked ? Color.white : new Color(0.25f, 0.25f, 0.25f);
            MurasUI.Text(card.transform, NameKy[i] + "  <size=70%>" + ShyrdakData.Size[i] + "×" + ShyrdakData.Size[i] + "</size>", 24, MurasUI.Cream, new Vector2(0, -105), new Vector2(300, 32), FontStyles.Bold);
            var b = MurasUI.Button(card.transform, doneBefore ? Loc.T("Собрано — ещё раз", "Done — again") : unlocked ? Loc.T("Собрать", "Start") : Loc.T("Сначала прошлый", "Finish previous"),
                                   new Vector2(0, -152), new Vector2(260, 48), () => StartLevel(captured), 19);
            b.interactable = unlocked;
        }
        MurasUI.Text(menu.transform, Loc.T("<b>Шырдак</b> — войлочный ковёр. Два куска войлока разного цвета режут по одному узору и меняют местами — получаются два ковра-«близнеца». Каждый узор (<b>оюу</b>) что-то означает.",
                                           "A <b>shyrdak</b> is a felt carpet. Two felt pieces of different colours are cut along one pattern and swapped, making two \"twin\" carpets. Every pattern (<b>oyuu</b>) has a meaning."),
                     18, MurasUI.Gold, new Vector2(0, -280), new Vector2(1060, 80), FontStyles.Italic);

        board = MurasUI.Rect("Board", win.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-220, -30), new Vector2(600, 600));
        var right = MurasUI.Rect("Right", win.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(380, -30), new Vector2(400, 640));
        MurasUI.Text(right.transform, Loc.T("Образец", "Pattern"), 22, MurasUI.Muted, new Vector2(0, 290), new Vector2(380, 30));
        preview = MurasUI.Rect("Preview", right.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 130), new Vector2(280, 280)).AddComponent<RawImage>();
        info = MurasUI.Text(right.transform, "", 19, MurasUI.Cream, new Vector2(0, -130), new Vector2(380, 230), FontStyles.Normal, TextAlignmentOptions.TopLeft);
        board.SetActive(false); right.name = "RightPanel"; right.SetActive(false);

        done = MurasUI.Rect("Done", win.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(380, -260), new Vector2(400, 200));
        doneText = MurasUI.Text(done.transform, "", 24, MurasUI.Green, new Vector2(0, 40), new Vector2(380, 90), FontStyles.Bold);
        MurasUI.Button(done.transform, Loc.T("Дальше", "Next"), new Vector2(0, -40), new Vector2(220, 56), ShowMenu);
        done.SetActive(false);
        root.SetActive(false);
    }

    void ShowMenu()
    {
        // пересобираем меню, чтобы обновились отметки «собрано»
        bool keep = open;
        if (canvas != null) Destroy(canvas);
        Build();
        if (keep) root.SetActive(true);
        title.text = "ШЫРДАК";
    }

    public void StartLevel(int lv)
    {
        level = lv; moves = 0; solved = false;
        int n = ShyrdakData.Size[lv];
        menu.SetActive(false); board.SetActive(true); done.SetActive(false);
        board.transform.parent.Find("RightPanel").gameObject.SetActive(true);
        title.text = NameKy[lv];
        Texture2D tex = Resources.Load<Texture2D>("MURAS/shyrdak_" + (lv + 1));
        preview.texture = tex;
        info.text = Loc.T(MeanRu[lv], MeanEn[lv]) + "\n\n<size=85%><color=#C9BFA6>" + Loc.T("Кликай по квадрату, чтобы повернуть его.", "Click a square to turn it.") + "</color></size>";

        foreach (Transform c in board.transform) Destroy(c.gameObject);
        rot = new int[n * n]; shown = new float[n * n]; tiles = new RectTransform[n * n];
        float size = 600f / n;
        int[] sym = ShyrdakData.Symmetry[lv];
        for (int i = 0; i < n * n; i++)
        {
            int r = i / n, c = i % n;
            // случайный поворот, который действительно портит плитку
            int k = Random.Range(1, 4);
            if (sym[i] == 2 && k == 2) k = 1;
            rot[i] = sym[i] == 1 ? 0 : k;
            if (Random.value < 0.15f && sym[i] != 1) rot[i] = 0;   // пара плиток уже на месте
            shown[i] = rot[i] * 90f;

            var go = MurasUI.Rect("Tile" + i, board.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                  new Vector2(-300 + size * (c + 0.5f), 300 - size * (r + 0.5f)), new Vector2(size - 3, size - 3));
            var img = go.AddComponent<RawImage>();
            img.texture = tex;
            img.uvRect = new Rect((float)c / n, 1f - (float)(r + 1) / n, 1f / n, 1f / n);
            var b = go.AddComponent<Button>();
            int captured = i;
            b.onClick.AddListener(() => Turn(captured));
            tiles[i] = (RectTransform)go.transform;
            tiles[i].localEulerAngles = new Vector3(0, 0, -shown[i]);
        }
        if (IsSolved()) StartLevel(lv);   // вдруг всё случайно сложилось
    }

    public void Turn(int i)
    {
        if (solved) return;
        rot[i] = (rot[i] + 1) % 4; moves++;
        MurasUI.Play("tile_turn", 0.7f, Random.Range(0.95f, 1.05f));
        if (IsSolved()) Solve();
    }

    bool IsSolved()
    {
        int[] sym = ShyrdakData.Symmetry[level];
        for (int i = 0; i < rot.Length; i++)
        {
            if (sym[i] == 1) continue;
            if (sym[i] == 2 ? rot[i] % 2 != 0 : rot[i] != 0) return false;
        }
        return true;
    }

    void Solve()
    {
        solved = true;
        bool first = SaveData.Get("shyrdak_" + (level + 1)) != 1;
        SaveData.Set("shyrdak_" + (level + 1), 1);
        int xp = first ? LevelXp[level] : 0;
        if (xp > 0 && GameManager.Instance != null) GameManager.Instance.AddXP(xp);
        Vocab.Unlock(LevelWords[level]);
        if (first) Inventory.Add("felt");
        if (SaveData.Get("shyrdak_1") == 1 && SaveData.Get("shyrdak_2") == 1 && SaveData.Get("shyrdak_3") == 1) Achievements.Give("shyrdak");
        doneText.text = Loc.T("Апа: Азаматсың! Узор готов.", "Apa: Азаматсың! The pattern is done.") + (xp > 0 ? "\n+" + xp + " XP" : "") +
                        "\n<size=70%><color=#C9BFA6>" + Loc.T("Ходов: ", "Moves: ") + moves + "</color></size>";
        done.SetActive(true);
        MurasUI.Play("achievement", 0.6f);
    }

    void Update()
    {
        if (!open || tiles == null || !board.activeSelf) return;
        for (int i = 0; i < tiles.Length; i++)
        {
            if (tiles[i] == null) continue;
            float target = rot[i] * 90f;
            if (shown[i] > target + 1f) target += 360f;    // 270 → 0: крутим дальше по часовой
            shown[i] = Mathf.MoveTowards(shown[i], target, Time.unscaledDeltaTime * 900f);
            if (shown[i] >= 360f) shown[i] -= 360f;
            tiles[i].localEulerAngles = new Vector3(0, 0, -shown[i]);
            if (solved) tiles[i].localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(Time.unscaledTime * 6f + i));
        }
    }
}
