using UnityEngine;

/// <summary>
/// Камера от третьего лица.
/// • ПКМ + мышь — вращение, колесо мыши — приближение/отдаление.
/// • Если между героем и камерой стена, крыша или дерево — камера плавно подъезжает ближе,
///   чтобы не проходить сквозь объекты.
/// • Под крышей (в юрте, доме) камера сама держится ближе к герою, а на улице возвращается обратно.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public Transform target;

    /// <summary>Если задано (скачки), камера сама поворачивается за спину этого объекта, пока не зажата ПКМ.</summary>
    public static Transform FollowBehind;
    public float distance = 6f;
    [Tooltip("Запасной вариант, если у героя нет CharacterController")]
    public float height = 2f;
    public float sensitivity = 3f;

    [Header("Куда смотрит камера")]
    [Tooltip("Точка взгляда на высоте роста героя: 0 — ноги, 1 — макушка (когда камера далеко)")]
    [Range(0f, 1f)] public float focusHeightRatio = 0.7f;
    [Tooltip("То же, когда камера совсем близко (смотрим примерно на плечи/голову)")]
    [Range(0f, 1f)] public float closeFocusHeightRatio = 0.85f;

    [Header("Приближение колесом мыши")]
    public float minZoom = 2.5f;
    public float maxZoom = 10f;
    public float zoomSpeed = 4f;

    [Header("Столкновения камеры")]
    [Tooltip("Слои, которые загораживают камеру (по умолчанию все)")]
    public LayerMask collisionMask = ~0;
    [Tooltip("Толщина «луча» камеры: чем больше, тем раньше она отъезжает от стен")]
    public float collisionRadius = 0.3f;
    [Tooltip("Ближе этого камера к герою не подъедет")]
    public float minDistance = 0.9f;
    [Tooltip("Как быстро камера подъезжает, когда что-то загородило обзор")]
    public float pullInSpeed = 25f;
    [Tooltip("Как плавно камера возвращается, когда препятствие исчезло")]
    public float returnSpeed = 3f;

    [Header("Тесные помещения")]
    [Tooltip("Если над героем крыша ниже этой высоты — считаем, что он внутри помещения")]
    public float roofCheckHeight = 7f;
    [Tooltip("Во сколько раз уменьшается дистанция камеры внутри помещения")]
    [Range(0.2f, 1f)] public float indoorDistanceFactor = 0.5f;
    public float indoorBlendSpeed = 2f;

    private float yaw = 0f;
    private float pitch = 15f;
    private float currentDistance;
    private float indoorBlend;
    private readonly RaycastHit[] hits = new RaycastHit[16];
    private CharacterController body;

    private void Start()
    {
        currentDistance = distance;
        if (PlayerPrefs.HasKey("MURAS_MouseSens")) sensitivity = GameSettings.MouseSensitivity;   // из настроек
        if (target != null)
            body = target.GetComponentInParent<CharacterController>() ?? target.GetComponentInChildren<CharacterController>();
        if (TryGetComponent(out Camera cam) && cam.nearClipPlane > 0.1f)
            cam.nearClipPlane = 0.1f;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * sensitivity;
            pitch -= Input.GetAxis("Mouse Y") * sensitivity;
            pitch = Mathf.Clamp(pitch, -20f, 65f);
        }

        else if (FollowBehind != null)
            yaw = Mathf.LerpAngle(yaw, FollowBehind.eulerAngles.y, 1f - Mathf.Exp(-2.5f * Time.deltaTime));

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
            distance = Mathf.Clamp(distance - scroll * zoomSpeed, minZoom, maxZoom);

        // Под крышей?
        Vector3 basePoint = PointOnBody(0.35f);
        bool indoor = CastClosest(basePoint, 0.2f, Vector3.up, roofCheckHeight, out _);
        indoorBlend = Mathf.MoveTowards(indoorBlend, indoor ? 1f : 0f, indoorBlendSpeed * Time.deltaTime);

        // Чем ближе камера, тем выше точка взгляда (с плеч на голову); вдали — центр фигуры
        float closeness = 1f - Mathf.InverseLerp(minDistance, Mathf.Max(minDistance + 0.1f, distance), currentDistance);
        Vector3 focusPoint = PointOnBody(Mathf.Lerp(focusHeightRatio, closeFocusHeightRatio, closeness));

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 back = rotation * Vector3.back;
        float wanted = distance * Mathf.Lerp(1f, indoorDistanceFactor, indoorBlend);

        // Что-то загораживает камеру?
        float allowed = wanted;
        if (CastClosest(focusPoint, collisionRadius, back, wanted, out float hitDistance))
            allowed = Mathf.Max(minDistance, hitDistance - 0.05f);

        float speed = allowed < currentDistance ? pullInSpeed : returnSpeed;
        currentDistance = Mathf.Lerp(currentDistance, allowed, 1f - Mathf.Exp(-speed * Time.deltaTime));
        if (allowed < currentDistance && currentDistance - allowed > 1.5f)
            currentDistance = allowed + 1.5f;   // резкая стена — не даём камере остаться внутри неё

        transform.position = focusPoint + back * currentDistance;
        transform.LookAt(focusPoint);
    }

    // Точка на высоте ratio от ног до макушки героя
    private Vector3 PointOnBody(float ratio)
    {
        if (body != null && body.enabled)
        {
            Bounds b = body.bounds;
            return new Vector3(b.center.x, b.min.y + b.size.y * ratio, b.center.z);
        }
        return target.position + Vector3.up * height * ratio;
    }

    // Ближайшее препятствие по направлению, без учёта самого героя и триггеров.
    private bool CastClosest(Vector3 origin, float radius, Vector3 direction, float maxDistance, out float closest)
    {
        closest = maxDistance;
        bool found = false;
        int count = Physics.SphereCastNonAlloc(origin, radius, direction, hits, maxDistance,
            collisionMask, QueryTriggerInteraction.Ignore);

        Transform self = target.root;
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hits[i];
            if (h.collider == null || h.distance <= 0f) continue;          // уже внутри коллайдера
            if (h.collider.transform.IsChildOf(self)) continue;           // сам герой
            if (h.collider.GetComponentInParent<ApplePickup>() != null) continue; // мелкие предметы не мешают
            if (h.distance < closest)
            {
                closest = h.distance;
                found = true;
            }
        }
        return found;
    }
}
