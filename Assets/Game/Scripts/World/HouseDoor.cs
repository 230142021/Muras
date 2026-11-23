using UnityEngine;

/// <summary>
/// Дверь дома: сама открывается внутрь, когда игрок подходит, и закрывается, когда он уходит.
/// Объект двери вращается вокруг своей точки (петли).
/// </summary>
public class HouseDoor : MonoBehaviour
{
    public float openAngle = -100f;
    public float openDistance = 2.4f;
    public float speed = 4f;
    public AudioClip openSound;
    public AudioClip closeSound;

    private Transform player;
    private Quaternion closedRot;
    private float t;
    private bool wasOpen;
    private AudioSource source;

    void Start()
    {
        closedRot = transform.localRotation;
        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null) player = pm.transform;
        source = gameObject.AddComponent<AudioSource>();
        source.spatialBlend = 1f; source.minDistance = 2f; source.maxDistance = 15f; source.playOnAwake = false;
    }

    void Update()
    {
        if (player == null) return;
        // центр дверного проёма — середина створки
        Vector3 doorCenter = transform.parent != null
            ? transform.parent.TransformPoint(transform.localPosition + closedRot * new Vector3(-0.5f, 1f, 0f))
            : transform.position;
        bool open = (player.position - doorCenter).sqrMagnitude < openDistance * openDistance;
        if (open != wasOpen)
        {
            wasOpen = open;
            AudioClip c = open ? openSound : closeSound;
            if (c != null) source.PlayOneShot(c, 0.6f);
        }
        t = Mathf.MoveTowards(t, open ? 1f : 0f, Time.deltaTime * speed);
        float e = t * t * (3f - 2f * t);
        transform.localRotation = closedRot * Quaternion.Euler(0f, openAngle * e, 0f);
    }
}
