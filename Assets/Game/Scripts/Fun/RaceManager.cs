using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ат чабыш — скачки. Трасса из флажков (gates) по кругу, 2 круга, двое соперников.
/// Старт — у чабандеса Эрмека (RaceStarter). Герой садится на коня (HorseRider + RiderPose),
/// обратный отсчёт 3-2-1, гонка, затем награда и герой спешивается.
/// </summary>
public class RaceManager : MonoBehaviour
{
    public static RaceManager Instance { get; private set; }
    public static bool Active { get; private set; }

    [Header("Трасса")]
    public Transform[] gates;
    public int laps = 2;
    public float gateRadius = 7f;
    public float gateHalfWidth = 3.2f;

    [Header("Модели")]
    public GameObject horseModel;
    public GameObject[] riderModels;
    public GameObject markerModel;
    public float horseScale = 0.9f;
    public Vector3 seat = new Vector3(0f, 1.58f, 0.03f);

    [Header("Звуки")]
    public AudioClip neigh;

    // racer 0 — игрок, 1..2 — соперники
    private readonly List<int> lap = new List<int>(), next = new List<int>();
    private readonly List<Vector3> pos = new List<Vector3>();
    private readonly List<float> finishTime = new List<float>();
    private readonly List<GameObject> spawned = new List<GameObject>();
    private HorseRider horse;
    private Transform player, playerParent, marker;
    private PlayerMovement movement;
    private CharacterController playerCC;
    private PlayerAnimationController animCtrl;
    private PlayerFootsteps steps;
    private Animator animator;
    private RiderPose pose;
    private float startTime, countdownEnd;
    private bool finished;
    private int place;

    private GameObject hud;
    private TMP_Text topText, bigText, hintText;
    private Image staminaFill;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) { Instance = null; Active = false; CameraFollow.FollowBehind = null; } }

    public Vector3 GatePoint(int i, float offset)
    {
        Transform g = gates[Mathf.Clamp(i, 0, gates.Length - 1)];
        return g.position + g.right * offset;
    }

    public int NextGate(int r) => r < next.Count ? next[r] : 0;

    float Progress(int r)
    {
        if (finishTime[r] > 0f) return 100000f - finishTime[r];
        int passed = lap[r] * gates.Length + (next[r] == 0 ? gates.Length : next[r]) - 1;
        Vector3 to = GatePoint(next[r], 0f) - pos[r]; to.y = 0f;
        return passed * 1000f - to.magnitude;
    }

    /// <summary>Соперники чуть притормаживают, если сильно впереди, и ускоряются, если отстали.</summary>
    public float RubberBand(int r)
    {
        if (!Active || finished) return 1f;
        float diff = (Progress(r) - Progress(0)) / 1000f;   // в «флажках»
        float up = Difficulty == 0 ? 1f : Difficulty == 1 ? 1.05f : 1.1f;      // на лёгком соперники не догоняют
        return Mathf.Clamp(1f - diff * 0.06f, Difficulty == 0 ? 0.8f : 0.88f, up);
    }

    public void Report(int r, Vector3 p)
    {
        if (!Active || r >= pos.Count) return;
        pos[r] = p;
        if (finishTime[r] > 0f || Time.time < countdownEnd) return;
        Vector3 d = GatePoint(next[r], 0f) - p; d.y = 0f;
        if (d.magnitude > gateRadius) return;
        if (next[r] == 0)
        {
            lap[r]++;
            if (lap[r] >= laps) { finishTime[r] = Time.time - startTime; if (r == 0) Finish(); return; }
        }
        next[r] = (next[r] + 1) % gates.Length;
        if (r == 0) MurasUI.Play("tile_turn", 0.5f, 1.4f);
    }

    // ------------------------------------------------------------------ старт

    /// <summary>Сложность: 0 — оңой (легко), 1 — орто (средне), 2 — кыйын (сложно).</summary>
    public int Difficulty { get; private set; } = 1;
    static readonly float[][] AiSpeed = { new[] { 7.6f, 8.2f }, new[] { 8.9f, 9.4f }, new[] { 10.1f, 10.6f } };
    static readonly int[] FirstWinXp = { 80, 150, 220 }, RepeatWinXp = { 20, 40, 60 };
    public static readonly string[] DiffKy = { "Оңой", "Орто", "Кыйын" };

    public void StartRace() => StartRace(1);

    public void StartRace(int difficulty)
    {
        Difficulty = Mathf.Clamp(difficulty, 0, 2);
        if (Active || gates == null || gates.Length < 3 || horseModel == null) return;
        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm == null) return;
        Active = true; finished = false;
        player = pm.transform; movement = pm;
        playerCC = pm.GetComponent<CharacterController>();
        animCtrl = pm.GetComponent<PlayerAnimationController>();
        steps = pm.GetComponent<PlayerFootsteps>();
        animator = pm.GetComponentInChildren<Animator>();
        playerParent = player.parent;

        lap.Clear(); next.Clear(); pos.Clear(); finishTime.Clear();
        Transform g0 = gates[0];
        Vector3 startDir = g0.forward;
        Vector3 basePos = g0.position - startDir * 7f;
        float[] lanes = { 0f, -2.6f, 2.6f };
        for (int r = 0; r < 3; r++)
        {
            lap.Add(-1); next.Add(0); finishTime.Add(-1f);
            pos.Add(basePos + g0.right * lanes[r]);
        }

        // конь игрока
        var root = new GameObject("RaceHorse_Player");
        root.transform.position = Ground(pos[0]);
        root.transform.rotation = Quaternion.LookRotation(startDir, Vector3.up);
        var model = SpawnModel(horseModel, root.transform);
        var cc = root.AddComponent<CharacterController>();
        cc.height = 1.7f * horseScale; cc.radius = 0.45f; cc.center = new Vector3(0f, 0.95f * horseScale, 0.15f); cc.stepOffset = 0.5f; cc.slopeLimit = 50f;
        horse = root.AddComponent<HorseRider>();
        horse.model = model; horse.gallop = MurasUI.Clip("gallop_loop");
        spawned.Add(root);

        // соперники
        for (int r = 1; r < 3; r++)
        {
            var ai = new GameObject("RaceHorse_AI" + r);
            ai.transform.position = Ground(pos[r]);
            ai.transform.rotation = Quaternion.LookRotation(startDir, Vector3.up);
            var m = SpawnModel(horseModel, ai.transform);
            if (riderModels != null && riderModels.Length >= r && riderModels[r - 1] != null)
            {
                var rider = Instantiate(riderModels[r - 1], m);
                rider.transform.localPosition = seat; rider.transform.localRotation = Quaternion.identity;
            }
            var brain = ai.AddComponent<RaceAI>();
            brain.race = this; brain.racer = r; brain.model = m;
            brain.baseSpeed = AiSpeed[Difficulty][r - 1];
            brain.laneOffset = lanes[r] * 0.5f;
            spawned.Add(ai);
        }

        // садимся на коня
        movement.enabled = false;
        if (playerCC != null) playerCC.enabled = false;
        if (animCtrl != null) animCtrl.enabled = false;
        if (steps != null) steps.enabled = false;
        if (animator != null) { animator.SetFloat("Speed", 0f); animator.SetBool("IsGrounded", true); }
        Transform hips = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
        Vector3 hipLocal = Quaternion.Inverse(player.rotation) * (hips != null ? hips.position - player.position : Vector3.up * 0.9f);
        player.SetParent(model, true);
        player.rotation = root.transform.rotation;
        player.position = model.TransformPoint(seat) - player.rotation * hipLocal + Vector3.up * 0.05f;
        pose = player.gameObject.AddComponent<RiderPose>();
        pose.animator = animator; pose.horse = root.transform;
        CameraFollow.FollowBehind = root.transform;

        if (markerModel != null) { marker = Instantiate(markerModel).transform; spawned.Add(marker.gameObject); }

        BuildHud();
        StartCoroutine(Countdown());
        if (neigh != null) AudioSource.PlayClipAtPoint(neigh, root.transform.position, 0.8f);
    }

    IEnumerator Countdown()
    {
        countdownEnd = Time.time + 3.2f;
        for (int i = 3; i >= 1; i--)
        {
            bigText.text = i.ToString(); MurasUI.Play("beep", 0.8f);
            yield return new WaitForSeconds(1f);
        }
        bigText.text = "Бар!"; MurasUI.Play("beep_go", 0.9f);
        startTime = Time.time;
        for (int r = 0; r < 3; r++) { lap[r] = 0; next[r] = 1; }
        horse.controls = true;
        foreach (GameObject s in spawned) { var ai = s.GetComponent<RaceAI>(); if (ai != null) ai.go = true; }
        yield return new WaitForSeconds(0.8f);
        if (!finished) bigText.text = "";
    }

    void Update()
    {
        if (!Active) return;
        if (horse != null) Report(0, horse.transform.position);

        if (marker != null && !finished)
        {
            marker.position = GatePoint(next[0], 0f) + Vector3.up * 3.6f + Vector3.up * Mathf.Sin(Time.time * 3f) * 0.2f;
            marker.rotation = Quaternion.LookRotation(gates[next[0]].forward, Vector3.up) * Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, Time.time * 90f, 0f);
        }

        if (hud != null && !finished)
        {
            place = 1;
            for (int r = 1; r < 3; r++) if (Progress(r) > Progress(0)) place++;
            float t = Time.time < countdownEnd ? 0f : Time.time - startTime;
            int shownLap = Mathf.Clamp(lap[0] + 1, 1, laps);
            topText.text = "<b>АТ ЧАБЫШ</b> <size=75%><color=#C9BFA6>" + DiffKy[Difficulty] + "</color></size>   " + Loc.T("Айлам ", "Lap ") + shownLap + "/" + laps + "   ·   " +
                           Loc.T("Орун ", "Place ") + "<color=#E8B04A><b>" + place + "</b></color>/3   ·   " + t.ToString("0.0") + " s";
            if (staminaFill != null) { staminaFill.fillAmount = horse != null ? horse.Stamina : 0f; }
        }

        if (!finished && Input.GetKeyDown(KeyCode.Q)) EndRace(false);
        if (!finished && horse != null && Vector3.Distance(horse.transform.position, gates[0].position) > 160f) EndRace(false);
    }

    void Finish()
    {
        finished = true;
        horse.controls = false;
        place = 1;
        for (int r = 1; r < 3; r++) if (finishTime[r] > 0f && finishTime[r] < finishTime[0]) place++;
        string winKey = "race_win_d" + Difficulty;
        bool firstWin = place == 1 && SaveData.Get(winKey) == 0;
        int xp = place == 1 ? (firstWin ? FirstWinXp[Difficulty] : RepeatWinXp[Difficulty]) : place == 2 ? 20 + Difficulty * 10 : 5 + Difficulty * 5;
        if (place == 1) { SaveData.Add("race_wins", 1); SaveData.Set(winKey, SaveData.Get(winKey) + 1); Inventory.Add("kymyz"); }
        SaveData.SetMax("race_best_ms", -Mathf.RoundToInt(finishTime[0] * 1000f));
        if (GameManager.Instance != null) GameManager.Instance.AddXP(xp);
        Vocab.Unlock("at_chabysh", "chabandes");
        if (place == 1) { Vocab.Unlock("kymyz"); Achievements.Give("race"); }
        MurasUI.Play("crowd_cheer", 0.8f);
        string[] places = { "", Loc.T("1-орун! Жеңиш!", "1st place! Victory!"), Loc.T("2-орун", "2nd place"), Loc.T("3-орун", "3rd place") };
        bigText.text = "<size=80%>" + places[place] + "</size>\n<size=45%>" + finishTime[0].ToString("0.0") + " s   ·   <color=#5FC873>+" + xp + " XP</color>" +
                       (place == 1 ? "\n" + Loc.T("Эрмек: Азамат! Вот тебе кымыз — заслужил!", "Ermek: Азамат! Here's some kymyz — you earned it!") : "\n" + Loc.T("Эрмек: Неплохо! Попробуй ещё раз.", "Ermek: Not bad! Try again.")) + "</size>";
        StartCoroutine(EndAfter(4.5f));
    }

    IEnumerator EndAfter(float s) { yield return new WaitForSeconds(s); EndRace(true); }

    void EndRace(bool completed)
    {
        if (!Active) return;
        StopAllCoroutines();
        if (pose != null) Destroy(pose);
        if (player != null)
        {
            Vector3 p = horse != null ? horse.transform.position - horse.transform.right * 1.4f : player.position;
            player.SetParent(playerParent, true);
            player.rotation = Quaternion.Euler(0f, player.eulerAngles.y, 0f);
            p = Ground(p);
            float halfH = playerCC != null ? (playerCC.height * 0.5f - playerCC.center.y) * player.lossyScale.y : 1f;
            player.position = p + Vector3.up * (halfH + 0.1f);
            if (playerCC != null) playerCC.enabled = true;
            movement.enabled = true;
            if (animCtrl != null) animCtrl.enabled = true;
            if (steps != null) steps.enabled = true;
        }
        foreach (GameObject s in spawned) if (s != null) Destroy(s);
        spawned.Clear();
        if (hud != null) Destroy(hud);
        CameraFollow.FollowBehind = null;
        Active = false;
        if (!completed) MurasToast.Push("Ат чабыш", Loc.T("Скачки прерваны.", "The race was stopped."), MurasUI.Muted, null);
    }

    // ------------------------------------------------------------------ helpers

    Transform SpawnModel(GameObject asset, Transform parent)
    {
        var holder = new GameObject("Model").transform;
        holder.SetParent(parent, false);
        holder.localScale = Vector3.one * horseScale;
        var m = Instantiate(asset, holder);
        m.transform.localPosition = Vector3.zero; m.transform.localRotation = Quaternion.identity;
        foreach (Collider c in m.GetComponentsInChildren<Collider>()) Destroy(c);
        return holder;
    }

    Vector3 Ground(Vector3 p)
    {
        float best = float.MinValue;
        foreach (RaycastHit h in Physics.RaycastAll(p + Vector3.up * 30f, Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.GetComponentInParent<PlayerMovement>() != null || h.collider.GetComponentInParent<HorseRider>() != null) continue;
            if (h.normal.y < 0.5f) continue;
            if (h.point.y > best) best = h.point.y;
        }
        return best > float.MinValue ? new Vector3(p.x, best, p.z) : p;
    }

    void BuildHud()
    {
        hud = MurasUI.Canvas("RaceHUD", transform, 300);
        var top = MurasUI.Rect("Top", hud.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -100), new Vector2(820, 52));
        top.AddComponent<Image>().color = new Color(0.10f, 0.08f, 0.06f, 0.85f);
        topText = MurasUI.Text(top.transform, "", 24, MurasUI.Cream, Vector2.zero, new Vector2(800, 50));
        var big = MurasUI.Rect("Big", hud.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 160), new Vector2(1000, 300));
        bigText = big.AddComponent<TextMeshProUGUI>();
        bigText.fontSize = 110; bigText.color = MurasUI.Gold; bigText.fontStyle = FontStyles.Bold; bigText.alignment = TextAlignmentOptions.Center; bigText.raycastTarget = false;
        bigText.outlineWidth = 0.15f; bigText.outlineColor = new Color32(30, 20, 10, 255);

        var bar = MurasUI.Rect("Stamina", hud.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(360, 22));
        bar.AddComponent<Image>().color = new Color(0.1f, 0.08f, 0.06f, 0.85f);
        var fill = MurasUI.Rect("Fill", bar.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-6, -6));
        staminaFill = fill.AddComponent<Image>();
        staminaFill.color = MurasUI.Gold; staminaFill.type = Image.Type.Filled; staminaFill.fillMethod = Image.FillMethod.Horizontal;
        staminaFill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
        MurasUI.Text(bar.transform, Loc.T("силы коня", "horse stamina"), 15, MurasUI.Cream, new Vector2(0, 24), new Vector2(360, 22));
        hintText = MurasUI.Text(hud.transform, Loc.T("<b>W</b> — вперёд   <b>A / D</b> — поворот   <b>Shift</b> — камчы (рывок)   <b>S</b> — тормоз   <b>Q</b> — сдаться   · скачи через флажки, золотое кольцо — следующий",
                                                     "<b>W</b> — go   <b>A / D</b> — turn   <b>Shift</b> — whip (sprint)   <b>S</b> — brake   <b>Q</b> — give up   · ride through the flags, the gold ring marks the next one"),
                                18, MurasUI.Cream, Vector2.zero, new Vector2(1400, 30));
        var hrt = (RectTransform)hintText.transform; hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0f); hrt.anchoredPosition = new Vector2(0, 100);
    }
}
