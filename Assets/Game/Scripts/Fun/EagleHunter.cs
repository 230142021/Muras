using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Бүркүтчү Болот с беркутом на руке. По E рассказывает о салбуурун и запускает беркута:
/// птица набирает высоту кругами, пикирует на добычу и возвращается на перчатку.
/// </summary>
public class EagleHunter : MonoBehaviour
{
    public Transform perchedEagle;
    public GameObject flyingModel;
    public AudioClip[] cries;
    public TMP_Text storyText;
    public float talkDistance = 3f;

    private Transform player;
    private bool showing;
    private AudioSource voice;

    static readonly string[] LinesRu =
    {
        "Болот: Салам, жигит! Это мой бүркүт — беркут. Кыргызы веками охотились с ним в горах.",
        "Болот: Смотри — сейчас он поднимется к облакам. У беркута глаза зорче, чем у человека, в восемь раз!",
        "Болот: Такая охота называется <b>салбуурун</b>: беркут, быстрые собаки тайган и лук.",
        "Болот: Вот он, возвращается на перчатку. Беркута растят годами — он становится членом семьи."
    };
    static readonly string[] LinesEn =
    {
        "Bolot: Салам, jigit! This is my bürküt — a golden eagle. The Kyrgyz have hunted with eagles in the mountains for centuries.",
        "Bolot: Watch — now he'll rise to the clouds. An eagle's eyes are eight times sharper than a man's!",
        "Bolot: This hunt is called <b>salbuurun</b>: the eagle, swift taigan hounds and a bow.",
        "Bolot: Here he comes back to the glove. An eagle is raised for years — he becomes part of the family."
    };

    void Start()
    {
        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null) player = pm.transform;
        voice = gameObject.AddComponent<AudioSource>();
        voice.spatialBlend = 1f; voice.minDistance = 6f; voice.maxDistance = 80f; voice.playOnAwake = false;
    }

    void Update()
    {
        if (showing || player == null || !Modal.Free) return;
        Vector3 d = player.position - transform.position; d.y = 0f;
        if (d.sqrMagnitude > talkDistance * talkDistance) return;
        InteractionHint.Request("<b><color=#E8B04A>E</color></b>  —  " + Loc.T("попросить Болота запустить беркута", "ask Bolot to fly the eagle"));
        if (Input.GetKeyDown(KeyCode.E)) StartCoroutine(Show());
    }

    void Say(int i)
    {
        if (storyText != null) storyText.text = Loc.IsEnglish ? LinesEn[i] : LinesRu[i];
    }

    void Cry(float vol = 1f)
    {
        if (cries == null || cries.Length == 0 || voice == null) return;
        voice.transform.position = transform.position;
        voice.pitch = Random.Range(0.95f, 1.05f);
        voice.PlayOneShot(cries[Random.Range(0, cries.Length)], vol);
    }

    IEnumerator Show()
    {
        showing = true;
        Say(0);
        yield return new WaitForSeconds(3.5f);
        if (perchedEagle == null || flyingModel == null) { showing = false; yield break; }

        Vector3 perch = perchedEagle.position;
        perchedEagle.gameObject.SetActive(false);
        var bird = Instantiate(flyingModel).transform;
        bird.position = perch;
        var voiceSrc = voice; voice = bird.gameObject.AddComponent<AudioSource>();
        voice.spatialBlend = 1f; voice.minDistance = 6f; voice.maxDistance = 120f; voice.playOnAwake = false;
        Cry();
        Say(1);

        Vector3 center = transform.position;
        Vector3 last = bird.position;
        float t = 0f, climb = 9f;
        // взлёт и круги вверх
        while (t < climb)
        {
            t += Time.deltaTime;
            float k = t / climb;
            float ang = k * Mathf.PI * 2.4f;
            float r = Mathf.Lerp(2f, 16f, Mathf.Min(1f, k * 1.6f));
            Vector3 target = center + new Vector3(Mathf.Cos(ang) * r, Mathf.Lerp(2f, 26f, Mathf.SmoothStep(0, 1, k)), Mathf.Sin(ang) * r);
            Move(bird, ref last, k < 0.12f ? Vector3.Lerp(perch, target, k / 0.12f) : target);
            if (Mathf.Abs(t - climb * 0.6f) < Time.deltaTime) { Cry(0.8f); Say(2); }
            yield return null;
        }
        // пике на «добычу»
        Vector3 prey = center + (Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward) * 22f;
        prey.y = center.y + 0.3f;
        Vector3 from = bird.position;
        for (t = 0f; t < 2.2f; t += Time.deltaTime)
        {
            float k = t / 2.2f;
            Move(bird, ref last, Vector3.Lerp(from, prey, k * k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 3f);
            yield return null;
        }
        yield return new WaitForSeconds(1.2f);
        Cry(0.7f);
        Say(3);
        // возвращение на перчатку
        from = bird.position;
        for (t = 0f; t < 3.5f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 3.5f);
            Move(bird, ref last, Vector3.Lerp(from, perchedEagle.position, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 5f);
            yield return null;
        }
        Destroy(bird.gameObject);
        voice = voiceSrc;
        perchedEagle.gameObject.SetActive(true);

        bool first = SaveData.Get("eagle") == 0;
        SaveData.Set("eagle", 1);
        if (first && GameManager.Instance != null) GameManager.Instance.AddXP(20);
        if (first) { Inventory.Add("feather"); if (storyText != null) storyText.text = Loc.T("Болот: Вот, держи перо моего беркута — пусть приносит удачу!", "Bolot: Here, take a feather from my eagle — may it bring you luck!"); }
        Vocab.Unlock("burkut", "burkutchu", "salbuurun");
        Achievements.Give("eagle");
        yield return new WaitForSeconds(2f);
        showing = false;
    }

    static void Move(Transform bird, ref Vector3 last, Vector3 p)
    {
        Vector3 v = p - last;
        bird.position = p;
        if (v.sqrMagnitude > 1e-6f)
        {
            Quaternion look = Quaternion.LookRotation(v.normalized, Vector3.up);
            float bank = Mathf.Clamp(Vector3.SignedAngle(bird.forward, v.normalized, Vector3.up) * 6f, -40f, 40f);
            bird.rotation = Quaternion.Slerp(bird.rotation, look * Quaternion.Euler(0f, 0f, -bank), 0.2f);
        }
        last = p;
    }
}
