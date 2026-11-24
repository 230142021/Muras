using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Көк бөрү (улак тартыш) — упрощённая версия: игрок и два соперника на конях. Нужно подхватить улак
/// (тюк в центре поля) и довезти до своего казана (красный флаг). Соперники везут его к синему казану
/// и могут вырвать улак, если догонят. E — вырвать улак у соперника. До 3 очков или 3 минуты.
/// Кони и всадники берутся у скачек (RaceManager), посадка такая же, как на ат чабыше.
/// </summary>
public class KokBoruGame : MonoBehaviour
{
    public static KokBoruGame Instance { get; private set; }
    public static bool Active { get; private set; }

    public const int WinScore = 3, FirstWinXp = 150, RepeatWinXp = 30;
    public const float GameTime = 180f;

    // поле
    private Vector3 center, axis, right, myKazan, theirKazan;
    private Transform ulak, ulakHome;

    // участники: 0 — игрок, 1..2 — соперники
    private readonly List<Transform> riders = new List<Transform>();
    private int carrier = -1;            // кто везёт улак
    private float carrierSince, resetAt = -1f, startTime, endTime;
    private int myScore, theirScore;
    private bool playing, finished;

    // игрок и посадка
    private Transform player, playerParent;
    private PlayerMovement movement;
    private CharacterController playerCC;
    private PlayerAnimationController animCtrl;
    private PlayerFootsteps steps;
    private Animator animator;
    private RiderPose pose;
    private HorseRider horse;
    private readonly List<GameObject> spawned = new List<GameObject>();
    private Transform marker;

    // интерфейс
    private GameObject hud;
    private TMP_Text topText, bigText, hintText;

    public static KokBoruGame Ensure()
    {
        if (Instance == null) Instance = new GameObject("KokBoruGame").AddComponent<KokBoruGame>();
        return Instance;
    }

    void Awake() { Instance = this; }
    void OnDestroy() { if (Active) End(false); }

    public void Begin(Vector3 c, Vector3 fwd, Transform ulakObject)
    {
        RaceManager race = RaceManager.Instance != null ? RaceManager.Instance : FindAnyObjectByType<RaceManager>();
        if (Active || RaceManager.Active || race == null || race.horseModel == null) return;
        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm == null) return;

        center = c; axis = fwd; axis.y = 0f; axis.Normalize(); right = Vector3.Cross(Vector3.up, axis);
        myKazan = Ground(center + axis * 14f); theirKazan = Ground(center - axis * 14f);
        ulakHome = ulakObject; ulak = ulakObject;

        Active = true; finished = false; playing = false;
        myScore = theirScore = 0; carrier = -1; resetAt = -1f;
        player = pm.transform; movement = pm;
        playerCC = pm.GetComponent<CharacterController>();
        animCtrl = pm.GetComponent<PlayerAnimationController>();
        steps = pm.GetComponent<PlayerFootsteps>();
        animator = pm.GetComponentInChildren<Animator>();
        playerParent = player.parent;
        riders.Clear();

        // конь игрока
        var root = new GameObject("KokBoru_PlayerHorse");
        root.transform.position = Ground(center - right * 7f);
        root.transform.rotation = Quaternion.LookRotation(right, Vector3.up);
        var model = SpawnModel(race.horseModel, root.transform, race.horseScale);
        var cc = root.AddComponent<CharacterController>();
        cc.height = 1.7f * race.horseScale; cc.radius = 0.45f; cc.center = new Vector3(0f, 0.95f * race.horseScale, 0.15f); cc.stepOffset = 0.5f; cc.slopeLimit = 50f;
        horse = root.AddComponent<HorseRider>();
        horse.model = model; horse.gallop = MurasUI.Clip("gallop_loop");
        spawned.Add(root); riders.Add(root.transform);

        // соперники
        for (int r = 1; r <= 2; r++)
        {
            var ai = new GameObject("KokBoru_Rival" + r);
            ai.transform.position = Ground(center + right * 7f + axis * (r == 1 ? 2.5f : -2.5f));
            ai.transform.rotation = Quaternion.LookRotation(-right, Vector3.up);
            var m = SpawnModel(race.horseModel, ai.transform, race.horseScale);
            if (race.riderModels != null && race.riderModels.Length >= r && race.riderModels[r - 1] != null)
            {
                var rider = Instantiate(race.riderModels[r - 1], m);
                rider.transform.localPosition = race.seat; rider.transform.localRotation = Quaternion.identity;
            }
            var brain = ai.AddComponent<KokBoruAI>();
            brain.game = this; brain.index = r; brain.model = m; brain.baseSpeed = r == 1 ? 8.6f : 8.1f;
            spawned.Add(ai); riders.Add(ai.transform);
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
        player.position = model.TransformPoint(race.seat) - player.rotation * hipLocal + Vector3.up * 0.05f;
        pose = player.gameObject.AddComponent<RiderPose>();
        pose.animator = animator; pose.horse = root.transform;
        CameraFollow.FollowBehind = root.transform;

        if (race.markerModel != null) { marker = Instantiate(race.markerModel).transform; spawned.Add(marker.gameObject); }
        PlaceUlakAtHome();
        BuildHud();
        Vocab.Unlock("kok_boru", "ulak");
        StartCoroutine(Countdown());
    }

    IEnumerator Countdown()
    {
        for (int i = 3; i >= 1; i--) { bigText.text = i.ToString(); MurasUI.Play("beep", 0.8f); yield return new WaitForSeconds(1f); }
        bigText.text = "Бар!"; MurasUI.Play("beep_go", 0.9f);
        playing = true; startTime = Time.time; endTime = startTime + GameTime;
        horse.controls = true;
        yield return new WaitForSeconds(0.8f);
        if (!finished) bigText.text = "";
    }

    // ------------------------------------------------------------------ игра

    public Vector3 UlakPos => ulak != null ? ulak.position : center;
    public int Carrier => carrier;
    public Vector3 Rider(int i) => riders[i].position;
    public Vector3 TheirKazan => theirKazan;
    public bool Playing => playing && !finished && resetAt < 0f;

    void Update()
    {
        if (!Active) return;

        if (playing && !finished)
        {
            // улак едет с тем, кто его везёт
            if (carrier >= 0 && ulak != null)
            {
                Transform t = riders[carrier];
                ulak.position = Vector3.Lerp(ulak.position, t.position + t.right * 0.75f + Vector3.up * 1.05f, Time.deltaTime * 14f);
                ulak.rotation = t.rotation;
            }

            if (resetAt > 0f && Time.time > resetAt) { resetAt = -1f; PlaceUlakAtHome(); bigText.text = ""; }

            if (resetAt < 0f)
            {
                // подхватить свободный улак
                if (carrier < 0)
                    for (int i = 0; i < riders.Count; i++)
                        if (Flat(riders[i].position - ulak.position) < 2.1f) { Take(i); break; }

                // игрок вырывает улак у соперника — E
                if (carrier > 0 && Input.GetKeyDown(KeyCode.E) && Flat(riders[0].position - riders[carrier].position) < 2.6f)
                {
                    if (Random.value < 0.7f) { Take(0); MurasToast.Push("Тартып алдың!", Loc.T("Ты вырвал улак!", "You snatched the ulak!"), MurasUI.Green, null); }
                    else MurasUI.Play("komuz_miss", 0.8f);
                }

                // соперник догнал игрока — может вырвать
                if (carrier == 0 && Time.time - carrierSince > 2.2f)
                    for (int i = 1; i < riders.Count; i++)
                        if (Flat(riders[i].position - riders[0].position) < 1.9f && Random.value < 0.9f * Time.deltaTime)
                        {
                            Take(i);
                            MurasToast.Push(Loc.T("Улакты тартып алышты!", "They snatched the ulak!"), Loc.T("Догони и вырви обратно — E", "Catch up and snatch it back — E"), MurasUI.Red, null);
                            break;
                        }

                // гол
                if (carrier == 0 && Flat(riders[0].position - myKazan) < 2.8f) Score(true);
                else if (carrier > 0 && Flat(riders[carrier].position - theirKazan) < 2.8f) Score(false);
            }

            if (Time.time > endTime) Finish();
            if (Input.GetKeyDown(KeyCode.Q)) End(false);
            if (Flat(riders[0].position - center) > 75f) End(false);
        }

        if (marker != null && !finished)
        {
            Vector3 target = carrier == 0 ? myKazan + Vector3.up * 4f : carrier > 0 ? riders[carrier].position + Vector3.up * 3.6f : UlakPos + Vector3.up * 2.4f;
            marker.position = target + Vector3.up * Mathf.Sin(Time.time * 3f) * 0.2f;
            marker.rotation = Quaternion.Euler(90f, Time.time * 90f, 0f);
        }

        if (topText != null && !finished)
        {
            float left = playing ? Mathf.Max(0f, endTime - Time.time) : GameTime;
            string who = carrier == 0 ? Loc.T("<color=#5FC873>улак у тебя — к красному казану!</color>", "<color=#5FC873>you have the ulak — to the red kazan!</color>")
                       : carrier > 0 ? Loc.T("<color=#E06050>улак у соперника — догони, E</color>", "<color=#E06050>a rival has it — catch up, E</color>")
                       : Loc.T("хватай улак!", "grab the ulak!");
            topText.text = "<b>КӨК БӨРҮ</b>   " + Loc.T("Сен ", "You ") + "<color=#E8B04A><b>" + myScore + " : " + theirScore + "</b></color>" + Loc.T(" Атаандар", " Rivals") +
                           "   ·   " + ((int)left / 60) + ":" + ((int)left % 60).ToString("00") + "   ·   " + who;
        }
    }

    void Take(int i)
    {
        carrier = i; carrierSince = Time.time;
        MurasUI.Play("tile_turn", 0.7f, i == 0 ? 1.1f : 0.8f);
    }

    void Score(bool mine)
    {
        if (mine) myScore++; else theirScore++;
        carrier = -1;
        MurasUI.Play(mine ? "crowd_cheer" : "komuz_miss", 0.8f);
        bigText.text = mine ? Loc.T("<size=70%>Гол! Улак в казане!</size>", "<size=70%>Goal! The ulak is in the kazan!</size>")
                            : Loc.T("<size=70%>Соперники забили…</size>", "<size=70%>The rivals scored…</size>");
        if (myScore >= WinScore || theirScore >= WinScore) { Finish(); return; }
        resetAt = Time.time + 2f;
    }

    void PlaceUlakAtHome()
    {
        carrier = -1;
        if (ulak != null) { ulak.position = Ground(center) + Vector3.up * 0.05f; ulak.rotation = Quaternion.LookRotation(axis); }
    }

    void Finish()
    {
        if (finished) return;
        finished = true; horse.controls = false;
        bool win = myScore > theirScore, draw = myScore == theirScore;
        bool first = win && SaveData.Get("kokboru_win") == 0;
        int xp = win ? (first ? FirstWinXp : RepeatWinXp) : draw ? 15 : 10;
        if (win) { SaveData.Add("kokboru_win", 1); Achievements.Give("kokboru"); Vocab.Unlock("kazan"); }
        if (GameManager.Instance != null) GameManager.Instance.AddXP(xp);
        MurasUI.Play("crowd_cheer", 0.8f);
        bigText.text = "<size=70%>" + (win ? Loc.T("Жеңиш!", "Victory!") : draw ? Loc.T("Тең!", "Draw!") : Loc.T("Утулдуң…", "You lost…")) + "</size>\n<size=40%>" + myScore + " : " + theirScore +
                       "   ·   <color=#5FC873>+" + xp + " XP</color>\n" + (win ? Loc.T("Эрмек: Вот это чабандес! Көк бөрү — игра настоящих джигитов.", "Ermek: Now that's a rider! Kök böru is a game for true horsemen.")
                                                                             : Loc.T("Эрмек: Не сдавайся — в көк бөрү главное сила и смелость!", "Ermek: Don't give up — kök böru is about strength and courage!")) + "</size>";
        StartCoroutine(EndAfter(5f));
    }

    IEnumerator EndAfter(float s) { yield return new WaitForSeconds(s); End(true); }

    public void End(bool completed)
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
        spawned.Clear(); riders.Clear();
        if (hud != null) Destroy(hud);
        CameraFollow.FollowBehind = null;
        PlaceUlakAtHome();
        Active = false;
        if (!completed) MurasToast.Push("Көк бөрү", Loc.T("Игра прервана.", "The game was stopped."), MurasUI.Muted, null);
    }

    // ------------------------------------------------------------------ помощники

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    Transform SpawnModel(GameObject asset, Transform parent, float scale)
    {
        var holder = new GameObject("Model").transform;
        holder.SetParent(parent, false);
        holder.localScale = Vector3.one * scale;
        var m = Instantiate(asset, holder);
        m.transform.localPosition = Vector3.zero; m.transform.localRotation = Quaternion.identity;
        foreach (Collider c in m.GetComponentsInChildren<Collider>()) Destroy(c);
        return holder;
    }

    public static Vector3 Ground(Vector3 p)
    {
        float best = float.MinValue;
        foreach (RaycastHit h in Physics.RaycastAll(p + Vector3.up * 30f, Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.GetComponentInParent<PlayerMovement>() != null || h.collider.GetComponentInParent<HorseRider>() != null || h.collider.GetComponentInParent<KokBoruAI>() != null) continue;
            if (h.collider.name.Contains("Ulak")) continue;
            if (h.normal.y < 0.5f) continue;
            if (h.point.y > best) best = h.point.y;
        }
        return best > float.MinValue ? new Vector3(p.x, best, p.z) : p;
    }

    void BuildHud()
    {
        hud = MurasUI.Canvas("KokBoruHUD", transform, 300);
        var top = MurasUI.Rect("Top", hud.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -100), new Vector2(1100, 52));
        top.AddComponent<Image>().color = new Color(0.10f, 0.08f, 0.06f, 0.85f);
        topText = MurasUI.Text(top.transform, "", 24, MurasUI.Cream, Vector2.zero, new Vector2(1080, 50));
        var big = MurasUI.Rect("Big", hud.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 160), new Vector2(1200, 320));
        bigText = big.AddComponent<TextMeshProUGUI>();
        bigText.fontSize = 110; bigText.color = MurasUI.Gold; bigText.fontStyle = FontStyles.Bold; bigText.alignment = TextAlignmentOptions.Center; bigText.raycastTarget = false;
        bigText.outlineWidth = 0.15f; bigText.outlineColor = new Color32(30, 20, 10, 255);
        hintText = MurasUI.Text(hud.transform, Loc.T("<b>W A D</b> — конь   <b>Shift</b> — камчы   <b>E</b> — вырвать улак   <b>Q</b> — выйти   ·   довези улак до <color=#E06050>красного</color> казана",
                                                     "<b>W A D</b> — horse   <b>Shift</b> — whip   <b>E</b> — snatch the ulak   <b>Q</b> — quit   ·   carry the ulak to the <color=#E06050>red</color> kazan"),
                                18, MurasUI.Cream, Vector2.zero, new Vector2(1400, 30));
        var hrt = (RectTransform)hintText.transform; hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0f); hrt.anchoredPosition = new Vector2(0, 100);
    }
}

/// <summary>Соперник в көк бөрү: бежит к свободному улаку, с улаком — к синему казану, иначе гонится за тем, у кого улак.</summary>
public class KokBoruAI : MonoBehaviour
{
    public KokBoruGame game;
    public int index;
    public Transform model;
    public float baseSpeed = 8.4f;
    private float speed, phase, wobble;

    void Start() => wobble = Random.Range(0f, 10f);

    void Update()
    {
        if (game == null) return;
        Vector3 target;
        int c = game.Carrier;
        if (!game.Playing) target = transform.position;
        else if (c < 0) target = game.UlakPos;
        else if (c == index) target = game.TheirKazan;
        else if (c == 0) target = game.Rider(0) + (transform.position - game.Rider(0)).normalized * 0.8f;
        else target = game.Rider(c) + (game.Rider(c) - game.TheirKazan).normalized * 3f + transform.right * 2f;   // прикрывает товарища

        Vector3 to = target - transform.position; to.y = 0f;
        float wanted = game.Playing && to.magnitude > 0.6f ? baseSpeed * (1f + 0.05f * Mathf.Sin(Time.time * 0.8f + wobble)) * (c == index ? 0.92f : 1f) : 0f;
        speed = Mathf.MoveTowards(speed, wanted, (wanted > speed ? 4.5f : 8f) * Time.deltaTime);
        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to.normalized, Vector3.up), 120f * Time.deltaTime);
        transform.position += transform.forward * speed * Time.deltaTime;
        transform.position = Vector3.Lerp(transform.position, KokBoruGame.Ground(transform.position), 0.5f);

        phase += Time.deltaTime * Mathf.Lerp(4f, 13f, speed / 12f) * (speed > 0.2f ? 1f : 0f);
        if (model != null)
        {
            model.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(phase)) * 0.12f * Mathf.Clamp01(speed / 3f), 0f);
            model.localRotation = Quaternion.Euler(Mathf.Sin(phase * 2f) * 4f * Mathf.Clamp01(speed / 3f), 0f, 0f);
        }
    }
}

/// <summary>Улак в центре поля: подойди пешком и нажми E — начнётся көк бөрү.</summary>
public class KokBoruStarter : MonoBehaviour
{
    public Transform ulak;
    public Vector3 axis = Vector3.forward;
    public float radius = 3f;
    private Transform player;

    void Start() { var pm = FindAnyObjectByType<PlayerMovement>(); if (pm != null) player = pm.transform; }

    void Update()
    {
        if (player == null || KokBoruGame.Active || !Modal.Free || PauseMenu.IsPaused) return;
        Vector3 d = player.position - transform.position; d.y = 0f;
        if (d.sqrMagnitude > radius * radius) return;
        InteractionHint.Request("<b><color=#E8B04A>E</color></b>  —  " + Loc.T("сыграть в көк бөрү (улак тартыш)", "play kök böru (ulak tartysh)"));
        if (Input.GetKeyDown(KeyCode.E)) Play();
    }

    public void Play() => KokBoruGame.Ensure().Begin(transform.position, transform.TransformDirection(axis), ulak != null ? ulak : transform);
}
