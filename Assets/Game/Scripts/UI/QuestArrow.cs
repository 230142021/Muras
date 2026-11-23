using UnityEngine;

/// <summary>Парящая стрелка-подсказка над целью квеста: покачивается и вращается.</summary>
public class QuestArrow : MonoBehaviour
{
    public Transform target;
    public float height = 2.5f;
    public float bobHeight = 0.25f;
    public float bobSpeed = 2.5f;
    public float spinSpeed = 90f;

    private Renderer[] renderers;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    public void Show(Transform newTarget, float newHeight)
    {
        target = newTarget;
        height = newHeight;
    }

    public void Hide()
    {
        target = null;
    }

    private void LateUpdate()
    {
        bool visible = target != null;
        foreach (Renderer r in renderers)
            if (r != null && r.enabled != visible) r.enabled = visible;

        if (!visible) return;

        transform.position = target.position + Vector3.up * (height + Mathf.Sin(Time.time * bobSpeed) * bobHeight);
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
    }
}
