using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Рыбалка на реке: E — забросить удочку, ждать поклёвку, по «!» быстро нажать E,
/// затем трижды поймать бегунок в зелёной зоне (E). Рыба: мөңкө (форель), чабак, редкая «алтын мөңкө».
/// </summary>
public class FishingSpot : MonoBehaviour, IModal
{
    public Transform standPoint;     // где стоит герой (смотрит на воду)
    public Transform waterPoint;     // куда падает поплавок
    public GameObject bobberModel;
    public float useDistance = 2.6f;

    enum State { Idle, Wait, Bite, Reel, Result }
    private State state = State.Idle;
    private Transform player, bobber;
    private PlayerMovement movement;
    private PlayerAnimationController anim;
    private GameObject canvas, panel;
    private TMP_Text status;
    private RectTransform bar, zone, needle;
    private float timer, needlePos, needleDir = 1f, needleSpeed, zoneCenter;
    private int hits, misses;
    private bool active;

    static readonly string[] FishKy = { "Мөңкө", "Чабак", "Алтын мөңкө" };
    static readonly string[] FishRu = { "горная форель", "чабак", "золотая форель — большая редкость!" };
    static readonly string[] FishEn = { "mountain trout", "chebak", "golden trout — a real rarity!" };

    void Start()
    {
        movement = FindAnyObjectByType<PlayerMovement>();
        if (movement != null) { player = movement.transform; anim = movement.GetComponent<PlayerAnimationController>(); }
    }

    void Update()
    {
        if (!active)
        {
            if (player == null || !Modal.Free) return;
            Vector3 c = standPoint != null ? standPoint.position : transform.position;
            Vector3 d = player.position - c; d.y = 0f;
            if (d.sqrMagnitude > useDistance * useDistance) return;
            InteractionHint.Request("<b><color=#E8B04A>E</color></b>  —  " + Loc.T("порыбачить", "go fishing") +
                                    (SaveData.Get("fish") > 0 ? "  <color=#C9BFA6>(" + Loc.T("поймано: ", "caught: ") + SaveData.Get("fish") + ")</color>" : ""));
            if (Input.GetKeyDown(KeyCode.E)) Begin();
            return;
        }

        bool e = Input.GetKeyDown(KeyCode.E);
        switch (state)
        {
            case State.Wait:
                Bob(0.02f, 2f);
                timer -= Time.deltaTime;
                if (timer <= 0f) { state = State.Bite; timer = 1.1f; status.text = "<size=140%><color=#E8B04A>!</color></size>  " + Loc.T("Клюёт! Жми <b>E</b>!", "A bite! Press <b>E</b>!"); MurasUI.Play("bobber_plop", 0.9f); }
                else if (e) { status.text = Loc.T("Рано! Рыба испугалась… Жди поклёвку.", "Too early! The fish got scared… Wait for a bite."); timer += 1.5f; }
                break;
            case State.Bite:
                Bob(0.08f, 14f);
                timer -= Time.deltaTime;
                if (e) StartReel();
                else if (timer <= 0f) Lost(Loc.T("Не успел — рыба сорвалась.", "Too slow — the fish got away."));
                break;
            case State.Reel:
                Bob(0.05f, 9f);
                needlePos += needleDir * needleSpeed * Time.deltaTime;
                if (needlePos > 1f) { needlePos = 1f; needleDir = -1f; }
                if (needlePos < 0f) { needlePos = 0f; needleDir = 1f; }
                needle.anchoredPosition = new Vector2((needlePos - 0.5f) * 560f, 0f);
                if (e)
                {
                    if (Mathf.Abs(needlePos - zoneCenter) <= 0.11f)
                    {
                        hits++; MurasUI.Play("reel_tick", 0.9f, 1f + hits * 0.15f);
                        if (hits >= 3) { Caught(); break; }
                        NewZone(); needleSpeed += 0.25f;
                    }
                    else
                    {
                        misses++; MurasUI.Play("komuz_miss", 0.6f);
                        if (misses >= 2) { Lost(Loc.T("Леска ослабла — рыба ушла.", "The line went slack — the fish escaped.")); break; }
                    }
                    status.text = Loc.T("Тяни! Лови бегунок в зелёной зоне (<b>E</b>)  ", "Reel in! Catch the marker in the green zone (<b>E</b>)  ") + hits + "/3";
                }
                break;
            case State.Result:
                if (e) Cast();
                break;
        }
    }

    void Begin()
    {
        active = true;
        Modal.Open(this);
        movement.enabled = false;
        if (anim != null) anim.enabled = false;
        var a = player.GetComponentInChildren<Animator>(); if (a != null) a.SetFloat("Speed", 0f);
        if (standPoint != null)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.position = new Vector3(standPoint.position.x, player.position.y + (standPoint.position.y - FeetY()), standPoint.position.z);
            if (cc != null) cc.enabled = true;
        }
        if (waterPoint != null)
        {
            Vector3 look = waterPoint.position - player.position; look.y = 0f;
            if (look.sqrMagnitude > 0.01f) player.rotation = Quaternion.LookRotation(look);
        }
        BuildUI();
        Cast();
    }

    float FeetY()
    {
        var cc = player.GetComponent<CharacterController>();
        return cc != null ? cc.bounds.min.y : player.position.y;
    }

    void Cast()
    {
        state = State.Wait;
        timer = Random.Range(2.5f, 6f);
        bar.gameObject.SetActive(false);
        status.text = Loc.T("Поплавок в воде… Жди поклёвку.", "The float is in the water… Wait for a bite.");
        if (bobber == null && bobberModel != null) bobber = Instantiate(bobberModel).transform;
        if (bobber != null && waterPoint != null) bobber.position = waterPoint.position;
        MurasUI.Play("water_splash", 0.5f, 1.3f);
    }

    void StartReel()
    {
        state = State.Reel; hits = 0; misses = 0; needleSpeed = 0.9f; needlePos = 0f;
        bar.gameObject.SetActive(true);
        NewZone();
        status.text = Loc.T("Тяни! Лови бегунок в зелёной зоне (<b>E</b>)  0/3", "Reel in! Catch the marker in the green zone (<b>E</b>)  0/3");
    }

    void NewZone()
    {
        zoneCenter = Random.Range(0.18f, 0.82f);
        zone.anchoredPosition = new Vector2((zoneCenter - 0.5f) * 560f, 0f);
    }

    void Caught()
    {
        float roll = Random.value;
        int f = roll < 0.1f ? 2 : roll < 0.4f ? 1 : 0;
        float size = f == 2 ? Random.Range(45f, 60f) : Random.Range(18f, 38f);
        int xp = f == 2 ? 30 : 10;
        SaveData.Add("fish", 1);
        MurasCloud.ReportScore("fishing", SaveData.Get("fish"));
        if (GameManager.Instance != null) GameManager.Instance.AddXP(xp);
        Inventory.Add(f == 0 ? "monko" : f == 1 ? "chabak" : "altyn");
        Vocab.Unlock("balyk");
        if (f != 1) Vocab.Unlock("monko");
        if (SaveData.Get("fish") >= 5) Achievements.Give("fish5");
        MurasUI.Play("water_splash", 0.8f);
        Result("<color=#5FC873>" + Loc.T("Поймал! ", "Caught! ") + "</color><b>" + FishKy[f] + "</b> — " + Loc.T(FishRu[f], FishEn[f]) + ", " + size.ToString("0") + Loc.T(" см", " cm") +
               "   <color=#5FC873>+" + xp + " XP</color>");
    }

    void Lost(string msg) => Result("<color=#DB5C4D>" + msg + "</color>");

    void Result(string msg)
    {
        state = State.Result;
        bar.gameObject.SetActive(false);
        status.text = msg + "\n<size=80%><color=#C9BFA6>" + Loc.T("E — забросить ещё   ·   Esc — закончить", "E — cast again   ·   Esc — stop") + "</color></size>";
    }

    void Bob(float amp, float speed)
    {
        if (bobber == null || waterPoint == null) return;
        bobber.position = waterPoint.position + Vector3.up * Mathf.Sin(Time.time * speed) * amp - (state == State.Bite ? Vector3.up * 0.05f : Vector3.zero);
    }

    public void CloseModal() => End();

    void End()
    {
        if (!active) return;
        active = false; state = State.Idle;
        Modal.Close(this);
        movement.enabled = true;
        if (anim != null) anim.enabled = true;
        if (bobber != null) Destroy(bobber.gameObject);
        if (canvas != null) Destroy(canvas);
    }

    void BuildUI()
    {
        if (canvas != null) Destroy(canvas);
        canvas = MurasUI.Canvas("FishingCanvas", transform, 320);
        panel = MurasUI.Rect("Panel", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 170), new Vector2(760, 170));
        panel.AddComponent<Image>().color = new Color(0.10f, 0.08f, 0.06f, 0.9f);
        MurasUI.Text(panel.transform, "БАЛЫК УУЛОО · " + Loc.T("рыбалка", "fishing"), 18, MurasUI.Gold, new Vector2(0, 66), new Vector2(720, 26), FontStyles.Bold);
        status = MurasUI.Text(panel.transform, "", 22, MurasUI.Cream, new Vector2(0, 18), new Vector2(720, 70));
        var b = MurasUI.Box("Bar", panel.transform, new Vector2(0, -50), new Vector2(580, 26), new Color(0.25f, 0.2f, 0.15f));
        bar = (RectTransform)b.transform;
        zone = (RectTransform)MurasUI.Box("Zone", b.transform, Vector2.zero, new Vector2(0.22f * 560f, 26), MurasUI.Green).transform;
        needle = (RectTransform)MurasUI.Box("Needle", b.transform, Vector2.zero, new Vector2(8, 40), MurasUI.Cream).transform;
        b.SetActive(false);
    }
}
