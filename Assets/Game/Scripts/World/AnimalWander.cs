using UnityEngine;

/// <summary>
/// Домашнее животное: бродит внутри своей площадки (загон, место у коновязи), останавливается
/// пощипать траву, иногда подаёт голос. Ночью больше стоит на месте (спит).
/// Площадка — прямоугольник в координатах объекта area (центр и половинки размеров).
/// </summary>
public class AnimalWander : MonoBehaviour
{
    public Transform area;
    public Vector2 halfSize = new Vector2(7f, 4.5f);
    public float walkSpeed = 0.6f;
    public float turnSpeed = 2.5f;
    public Vector2 idleTime = new Vector2(3f, 9f);
    public float stepBob = 0.03f;
    public float stepRate = 5f;
    public float grazeTilt = 8f;

    [Header("Голос")]
    public AudioClip[] calls;
    public Vector2 callPause = new Vector2(10f, 28f);
    [Range(0f, 1f)] public float callVolume = 0.7f;

    private enum State { Idle, Walk }
    private State state;
    private Vector3 target;
    private float timer;
    private float nextCall;
    private float tilt;
    private float bob;
    private AudioSource source;
    private Transform model;

    void Start()
    {
        model = transform.childCount > 0 ? transform.GetChild(0) : transform;
        source = gameObject.AddComponent<AudioSource>();
        source.spatialBlend = 1f; source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = 3f; source.maxDistance = 35f; source.playOnAwake = false;
        timer = Random.Range(0f, idleTime.y);
        nextCall = Time.time + Random.Range(2f, callPause.y);
        SnapToGround();
    }

    void Update()
    {
        bool night = DayNightCycle.IsNight;
        timer -= Time.deltaTime;

        if (state == State.Idle)
        {
            tilt = Mathf.MoveTowards(tilt, night ? 0f : grazeTilt, Time.deltaTime * 10f);
            bob = Mathf.MoveTowards(bob, 0f, Time.deltaTime);
            if (timer <= 0f && (!night || Random.value < 0.15f)) PickTarget();
            else if (timer <= 0f) timer = Random.Range(idleTime.x, idleTime.y);
        }
        else
        {
            tilt = Mathf.MoveTowards(tilt, 0f, Time.deltaTime * 15f);
            Vector3 to = target - transform.position; to.y = 0f;
            if (to.magnitude < 0.25f || timer <= 0f)
            {
                state = State.Idle;
                timer = Random.Range(idleTime.x, idleTime.y);
            }
            else
            {
                Quaternion look = Quaternion.LookRotation(to.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * turnSpeed);
                float facing = Mathf.Clamp01(Vector3.Dot(transform.forward, to.normalized));
                transform.position += transform.forward * walkSpeed * facing * Time.deltaTime;
                bob = Mathf.Abs(Mathf.Sin(Time.time * stepRate)) * stepBob;
                SnapToGround();
            }
        }

        if (model != transform)
        {
            model.localRotation = Quaternion.Euler(tilt, 0f, 0f);
            model.localPosition = new Vector3(0f, bob, 0f);
        }

        if (calls != null && calls.Length > 0 && Time.time > nextCall)
        {
            nextCall = Time.time + Random.Range(callPause.x, callPause.y) * (night ? 2.5f : 1f);
            source.pitch = Random.Range(0.92f, 1.1f);
            source.PlayOneShot(calls[Random.Range(0, calls.Length)], callVolume);
        }
    }

    void PickTarget()
    {
        if (area == null) { timer = 5f; return; }
        Vector3 local = new Vector3(Random.Range(-halfSize.x, halfSize.x), 0f, Random.Range(-halfSize.y, halfSize.y));
        target = area.TransformPoint(local);
        state = State.Walk;
        timer = 20f;
    }

    void SnapToGround()
    {
        Vector3 p = transform.position;
        float best = float.MinValue;
        foreach (RaycastHit h in Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.normal.y < 0.6f) continue;
            if (h.point.y > best && h.point.y < p.y + 0.8f) best = h.point.y;
        }
        if (best > float.MinValue) transform.position = new Vector3(p.x, best, p.z);
    }
}
