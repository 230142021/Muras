using UnityEngine;

/// <summary>Предмет для подбора медленно вращается и покачивается, чтобы его было видно издалека.</summary>
public class PickupBob : MonoBehaviour
{
    public Transform model;
    public float rotateSpeed = 60f;
    public float bobHeight = 0.12f;
    public float bobSpeed = 2f;

    private Vector3 startLocalPos;
    private float phase;

    private void Start()
    {
        if (model == null && transform.childCount > 0)
            model = transform.GetChild(0);
        if (model != null)
            startLocalPos = model.localPosition;
        phase = Random.value * Mathf.PI * 2f;
    }

    private void Update()
    {
        if (model == null) return;
        model.Rotate(0f, rotateSpeed * Time.deltaTime, 0f, Space.World);
        float scale = transform.lossyScale.y > 0.0001f ? 1f / transform.lossyScale.y : 1f;
        model.localPosition = startLocalPos + Vector3.up * Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight * scale;
    }
}
