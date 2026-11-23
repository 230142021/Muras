using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    public PlayerMovement movement;

    [Header("Animation Settings")]
    [Tooltip("Сглаживание параметра Speed в аниматоре (сек).")]
    public float speedDampTime = 0.1f;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");

    private void Awake()
    {
        if (movement == null)
            movement = GetComponent<PlayerMovement>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    // LateUpdate: читаем скорость уже после того, как PlayerMovement подвинул персонажа.
    private void LateUpdate()
    {
        if (animator == null || movement == null)
            return;

        // Blend Tree: 0 = Idle, 0.5 = Walking, 1 = Running.
        // Значение берётся из реальной скорости, поэтому шаги совпадают с движением.
        float speed = movement.CurrentSpeed;
        float walk = Mathf.Max(0.01f, movement.walkSpeed);
        float run = Mathf.Max(walk + 0.01f, movement.runSpeed);

        float blend = speed <= walk
            ? 0.5f * speed / walk
            : 0.5f + 0.5f * Mathf.InverseLerp(walk, run, speed);

        if (blend < 0.02f)
            blend = 0f;

        animator.SetFloat(SpeedHash, blend, speedDampTime, Time.deltaTime);
        animator.SetBool(IsGroundedHash, movement.IsGrounded);
    }
}
