using UnityEngine;

/// <summary>
/// Оживляет статичную модель NPC: плавно поворачивает её к игроку, когда он рядом,
/// и добавляет лёгкое «дыхание». Вращается только модель, коллайдер и скрипты остаются на месте.
/// </summary>
public class NPCLookAtPlayer : MonoBehaviour
{
    [Tooltip("Дочерняя модель NPC, которую нужно поворачивать.")]
    public Transform model;
    public Transform player;

    [Header("Поворот к игроку")]
    public float lookDistance = 6f;
    public float turnSpeed = 4f;

    [Header("Дыхание")]
    public float breathAmount = 0.012f;
    public float breathSpeed = 1.6f;

    private Quaternion startRotation;
    private Vector3 startScale;

    private void Start()
    {
        if (model == null && transform.childCount > 0)
            model = transform.GetChild(0);

        if (player == null)
        {
            PlayerMovement movement = FindAnyObjectByType<PlayerMovement>();
            if (movement != null)
                player = movement.transform;
        }

        if (model != null)
        {
            startRotation = model.rotation;
            startScale = model.localScale;
        }
    }

    private void Update()
    {
        if (model == null)
            return;

        Quaternion target = startRotation;

        if (player != null)
        {
            Vector3 toPlayer = player.position - model.position;
            toPlayer.y = 0f;

            if (toPlayer.sqrMagnitude < lookDistance * lookDistance && toPlayer.sqrMagnitude > 0.01f)
                target = Quaternion.LookRotation(toPlayer);
        }

        model.rotation = Quaternion.Slerp(model.rotation, target, turnSpeed * Time.deltaTime);

        float breath = 1f + Mathf.Sin(Time.time * breathSpeed) * breathAmount;
        model.localScale = new Vector3(startScale.x, startScale.y * breath, startScale.z);
    }
}
