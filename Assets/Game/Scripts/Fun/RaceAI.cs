using UnityEngine;

/// <summary>Соперник на скачках: скачет от флажка к флажку, немного подстраивает скорость под игрока.</summary>
public class RaceAI : MonoBehaviour
{
    public RaceManager race;
    public int racer;           // номер в RaceManager
    public float baseSpeed = 10.4f;
    public float laneOffset;
    public Transform model;
    public bool go;

    private float speed, phase, wobble;

    void Start() => wobble = Random.Range(0f, 10f);

    void Update()
    {
        if (race == null) return;
        Vector3 target = race.GatePoint(race.NextGate(racer), laneOffset);
        Vector3 to = target - transform.position; to.y = 0f;
        float rubber = race.RubberBand(racer);
        float wanted = go ? baseSpeed * rubber * (1f + 0.04f * Mathf.Sin(Time.time * 0.7f + wobble)) : 0f;
        speed = Mathf.MoveTowards(speed, wanted, (wanted > speed ? 4.5f : 8f) * Time.deltaTime);
        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to.normalized, Vector3.up), 110f * Time.deltaTime);
        transform.position += transform.forward * speed * Time.deltaTime;
        Snap();

        phase += Time.deltaTime * Mathf.Lerp(4f, 13f, speed / 12f) * (speed > 0.2f ? 1f : 0f);
        if (model != null)
        {
            model.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(phase)) * 0.12f * Mathf.Clamp01(speed / 3f), 0f);
            model.localRotation = Quaternion.Euler(Mathf.Sin(phase * 2f) * 4f * Mathf.Clamp01(speed / 3f), 0f, 0f);
        }
        race.Report(racer, transform.position);
    }

    void Snap()
    {
        Vector3 p = transform.position;
        float best = float.MinValue;
        foreach (RaycastHit h in Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.root == transform.root || h.collider.GetComponentInParent<RaceManager>() != null) continue;
            if (h.collider.GetComponentInParent<PlayerMovement>() != null) continue;
            if (h.normal.y < 0.5f) continue;
            if (h.point.y > best && h.point.y < p.y + 1.5f) best = h.point.y;
        }
        if (best > float.MinValue) transform.position = new Vector3(p.x, Mathf.Lerp(p.y, best, 0.5f), p.z);
    }
}
