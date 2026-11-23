using UnityEngine;

/// <summary>Место, которое учит словам: подошёл ближе radius — слова открылись (ферма, река, дом, базар).</summary>
public class VocabZone : MonoBehaviour
{
    public string[] words;
    public float radius = 8f;

    private Transform player;
    private float next;
    private bool done;

    void Start()
    {
        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null) player = pm.transform;
    }

    void Update()
    {
        if (done || player == null || Time.time < next) return;
        next = Time.time + 0.5f;
        Vector3 d = player.position - transform.position; d.y *= 0.5f;
        if (d.sqrMagnitude > radius * radius) return;
        done = true;
        Vocab.Unlock(words);
    }
}
