using UnityEngine;

/// <summary>
/// Спутник героя (пёс или конь, купленные на базаре): идёт следом, держится сбоку-сзади,
/// переходит на бег, если отстаёт, стоит рядом, когда герой остановился. Иногда подаёт голос.
/// Если герой ушёл слишком далеко (портал, прыжок с обрыва), спутник появляется рядом.
/// </summary>
public class CompanionFollower : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(1.1f, 0f, -1.3f);   // в координатах героя: вправо и назад
    public float walkSpeed = 2.2f;
    public float runSpeed = 6.5f;
    public float stopDistance = 0.6f;
    public float turnSpeed = 6f;
    public float teleportDistance = 28f;

    [Header("Шаги")]
    public float stepBob = 0.04f;
    public float stepRate = 8f;

    [Header("Голос")]
    public AudioClip[] calls;
    public Vector2 callPause = new Vector2(18f, 40f);
    [Range(0f, 1f)] public float callVolume = 0.6f;

    private Transform model;
    private AudioSource source;
    private float speed;
    private float nextCall;
    private float phase;

    void Start()
    {
        model = transform.childCount > 0 ? transform.GetChild(0) : null;
        source = gameObject.AddComponent<AudioSource>();
        source.spatialBlend = 1f; source.minDistance = 3f; source.maxDistance = 30f;
        source.rolloffMode = AudioRolloffMode.Logarithmic; source.playOnAwake = false;
        nextCall = Time.time + Random.Range(4f, 10f);
        if (target != null) Teleport();
        Call();
    }

    void Update()
    {
        if (target == null) return;

        Vector3 goal = target.TransformPoint(offset);
        Vector3 to = goal - transform.position; to.y = 0f;
        float dist = to.magnitude;

        if (dist > teleportDistance) { Teleport(); return; }

        float wanted = dist < stopDistance ? 0f : (dist > 3.5f ? runSpeed : walkSpeed * Mathf.Clamp01(dist / 1.5f + 0.3f));
        speed = Mathf.MoveTowards(speed, wanted, (wanted > speed ? 8f : 12f) * Time.deltaTime);

        Vector3 look = speed > 0.15f && dist > 0.05f ? to / dist : Flat(target.forward);
        if (look.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look, Vector3.up), turnSpeed * Time.deltaTime);

        if (speed > 0.01f)
        {
            float facing = Mathf.Clamp01(Vector3.Dot(transform.forward, look) * 0.7f + 0.3f);
            transform.position += transform.forward * speed * facing * Time.deltaTime;
            SnapToGround();
        }

        if (model != null)
        {
            phase += Time.deltaTime * stepRate * Mathf.Clamp(speed / walkSpeed, 0f, 2f);
            float bob = speed > 0.1f ? Mathf.Abs(Mathf.Sin(phase)) * stepBob * Mathf.Clamp01(speed) : 0f;
            float pitch = speed > runSpeed * 0.6f ? Mathf.Sin(phase * 2f) * 3f : 0f;
            model.localPosition = new Vector3(0f, bob, 0f);
            model.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        if (calls != null && calls.Length > 0 && Time.time > nextCall) Call();
    }

    void Call()
    {
        nextCall = Time.time + Random.Range(callPause.x, callPause.y);
        if (calls == null || calls.Length == 0 || source == null) return;
        source.pitch = Random.Range(0.95f, 1.08f);
        source.PlayOneShot(calls[Random.Range(0, calls.Length)], callVolume);
    }

    public void Teleport()
    {
        Vector3 p = target.TransformPoint(offset);
        transform.position = new Vector3(p.x, target.position.y, p.z);
        transform.rotation = Quaternion.LookRotation(Flat(target.forward), Vector3.up);
        speed = 0f;
        SnapToGround();
    }

    void SnapToGround()
    {
        Vector3 p = transform.position;
        float best = float.MinValue;
        foreach (RaycastHit h in Physics.RaycastAll(p + Vector3.up * 2.5f, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform) || (target != null && h.collider.transform.IsChildOf(target))) continue;
            if (h.normal.y < 0.5f) continue;
            if (h.point.y > best && h.point.y < p.y + 1.2f) best = h.point.y;
        }
        if (best > float.MinValue) transform.position = new Vector3(p.x, Mathf.Lerp(p.y, best, 0.6f), p.z);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward; }
}
