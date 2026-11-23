using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Скорость (м/с)")]
    [Tooltip("Обычная ходьба. Подбирай так, чтобы ноги не скользили по земле.")]
    public float walkSpeed = 2f;
    [Tooltip("Бег с зажатым Shift.")]
    public float runSpeed = 5.5f;

    [Header("Плавность")]
    [Tooltip("Как быстро персонаж разгоняется (м/с²).")]
    public float acceleration = 10f;
    [Tooltip("Как быстро персонаж останавливается (м/с²).")]
    public float deceleration = 14f;
    public float turnSpeed = 10f;

    [Header("Прыжок и гравитация")]
    public float gravity = -9.81f;
    [Tooltip("Высота прыжка в метрах. Персонаж ростом ~1.8 м: 0.5–0.7 выглядит естественно.")]
    public float jumpApexHeight = 0.6f;   // новое имя поля, чтобы старое значение 1.2 из сцены не перебивало

    [Header("Земля")]
    [Tooltip("Сколько секунд персонаж считается на земле после потери контакта. " +
             "Убирает мигание анимации прыжка на кочках и ступеньках.")]
    public float groundedGraceTime = 0.15f;

    /// <summary>Фактическая горизонтальная скорость, м/с.</summary>
    public float CurrentSpeed { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsGrounded => Time.time - lastGroundedTime <= groundedGraceTime;

    private CharacterController controller;
    private DialogueManager dialogue;
    private Vector3 horizontalVelocity;
    private float verticalVelocity;
    private float lastGroundedTime;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        dialogue = FindAnyObjectByType<DialogueManager>();
        lastGroundedTime = Time.time;
    }

    private void Update()
    {
        bool inDialogue = dialogue != null && dialogue.IsOpen;

        Vector2 input = inDialogue
            ? Vector2.zero
            : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        input = Vector2.ClampMagnitude(input, 1f);

        // Направление движения зависит от камеры.
        Vector3 direction = new Vector3(input.x, 0f, input.y);
        if (Camera.main != null)
        {
            Vector3 forward = Camera.main.transform.forward;
            Vector3 right = Camera.main.transform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
            direction = forward * input.y + right * input.x;
        }
        direction = Vector3.ClampMagnitude(direction, 1f);

        bool hasInput = direction.sqrMagnitude > 0.01f;
        IsRunning = hasInput &&
                    (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

        // Плавный разгон и торможение вместо мгновенного старта.
        float targetSpeed = hasInput ? (IsRunning ? runSpeed : walkSpeed) * direction.magnitude : 0f;
        Vector3 targetVelocity = hasInput ? direction.normalized * targetSpeed : Vector3.zero;
        float rate = targetVelocity.sqrMagnitude > horizontalVelocity.sqrMagnitude
            ? acceleration
            : deceleration;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, rate * Time.deltaTime);

        if (hasInput)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(direction),
                turnSpeed * Time.deltaTime);
        }

        // Земля и прыжок.
        if (controller.isGrounded)
        {
            lastGroundedTime = Time.time;
            if (verticalVelocity < 0f)
                verticalVelocity = -2f;
        }

        if (!inDialogue && Input.GetKeyDown(KeyCode.Space) && IsGrounded && verticalVelocity <= 0f)
        {
            verticalVelocity = Mathf.Sqrt(jumpApexHeight * -2f * gravity);
            lastGroundedTime = float.NegativeInfinity;
        }

        verticalVelocity += gravity * Time.deltaTime;

        // Один вызов Move за кадр, чтобы isGrounded был стабильным.
        controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);

        Vector3 actual = controller.velocity;
        actual.y = 0f;
        CurrentSpeed = actual.magnitude;
    }
}
