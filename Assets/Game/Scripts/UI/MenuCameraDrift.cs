using UnityEngine;

/// <summary>
/// Камера главного меню медленно плывёт по дуге вокруг юрты, не опускаясь в землю.
/// Цель смещена вправо в кадре, чтобы слева было место для меню.
/// </summary>
public class MenuCameraDrift : MonoBehaviour
{
    public Transform target;
    public Vector3 targetOffset = new Vector3(0f, 2.5f, 0f);
    public float radius = 17f;
    public float height = 5.5f;
    [Tooltip("Средний угол (градусы) — откуда смотрим на цель")]
    public float baseAngle = 0f;
    [Tooltip("Насколько градусов камера уходит влево-вправо")]
    public float swingAngle = 35f;
    public float swingSpeed = 0.06f;
    [Tooltip("Сдвиг цели в кадре вправо (градусы поворота камеры)")]
    public float screenShift = 13f;
    public float minAboveGround = 2.5f;

    private float smoothY = float.NaN;
    private float yVel;

    void LateUpdate()
    {
        if (target == null) return;

        float a = (baseAngle + Mathf.Sin(Time.unscaledTime * swingSpeed) * swingAngle) * Mathf.Deg2Rad;
        Vector3 focus = target.position + targetOffset;
        Vector3 pos = target.position + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
        pos.y = target.position.y + height + Mathf.Sin(Time.unscaledTime * swingSpeed * 1.7f) * 0.6f;

        if (Physics.Raycast(pos + Vector3.up * 100f, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
            pos.y = Mathf.Max(pos.y, hit.point.y + minAboveGround);
        smoothY = float.IsNaN(smoothY) ? pos.y : Mathf.SmoothDamp(smoothY, pos.y, ref yVel, 1.2f, Mathf.Infinity, Time.unscaledDeltaTime);
        pos.y = smoothY;

        transform.position = pos;
        transform.rotation = Quaternion.LookRotation(focus - pos, Vector3.up) * Quaternion.Euler(0f, -screenShift, 0f);
    }
}
