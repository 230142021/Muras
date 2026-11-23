using UnityEngine;

/// <summary>
/// Управление конём на скачках: W — вперёд, S — тормоз, A/D — поворот, Shift — камчы (рывок, тратит силы).
/// Конь двигается через CharacterController, поэтому не проходит сквозь дома и деревья и едет по склонам.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class HorseRider : MonoBehaviour
{
    public float maxSpeed = 9.5f, sprintSpeed = 13f, accel = 5.5f, brake = 10f, turnRate = 125f;
    public float staminaDrain = 0.3f, staminaRegen = 0.15f;
    public bool controls;
    public Transform model;
    public AudioClip gallop;

    public float Speed { get; private set; }
    public float Stamina { get; private set; } = 1f;

    private CharacterController cc;
    private float yaw, vertical, phase;
    private AudioSource hooves;
    private bool tired;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        yaw = transform.eulerAngles.y;
    }

    void Start()
    {
        if (gallop != null)
        {
            hooves = gameObject.AddComponent<AudioSource>();
            hooves.clip = gallop; hooves.loop = true; hooves.spatialBlend = 0.5f; hooves.volume = 0f; hooves.Play();
        }
    }

    void Update()
    {
        float v = controls ? Input.GetAxisRaw("Vertical") : 0f;
        float h = controls ? Input.GetAxisRaw("Horizontal") : 0f;
        bool sprint = controls && v > 0.1f && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && !tired;

        if (sprint) { Stamina = Mathf.Max(0f, Stamina - staminaDrain * Time.deltaTime); if (Stamina <= 0f) tired = true; }
        else { Stamina = Mathf.Min(1f, Stamina + staminaRegen * Time.deltaTime); if (Stamina > 0.35f) tired = false; }

        float target = v > 0.1f ? (sprint ? sprintSpeed : maxSpeed) : (v < -0.1f ? 0f : Speed * 0.985f);
        float rate = target > Speed ? accel : (v < -0.1f ? brake : accel * 0.6f);
        Speed = Mathf.MoveTowards(Speed, target, rate * Time.deltaTime);

        float turnK = Mathf.Lerp(1.3f, 0.95f, Speed / sprintSpeed);
        if (Speed > 0.3f || Mathf.Abs(h) > 0.1f) yaw += h * turnRate * turnK * Time.deltaTime;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (cc.isGrounded && vertical < 0f) vertical = -2f;
        vertical += -9.81f * Time.deltaTime;
        cc.Move((transform.forward * Speed + Vector3.up * vertical) * Time.deltaTime);
        // упёрлись в препятствие — теряем скорость
        Vector3 real = cc.velocity; real.y = 0f;
        if (Speed > 2f && real.magnitude < Speed * 0.4f) Speed = Mathf.Max(real.magnitude, Speed - 20f * Time.deltaTime);

        Animate();
    }

    void Animate()
    {
        float k = Speed / sprintSpeed;
        phase += Time.deltaTime * Mathf.Lerp(4f, 14f, k) * (Speed > 0.2f ? 1f : 0f);
        if (model != null)
        {
            float bob = Mathf.Abs(Mathf.Sin(phase)) * 0.12f * Mathf.Clamp01(Speed / 3f);
            float pitch = Mathf.Sin(phase * 2f) * 4f * Mathf.Clamp01(Speed / 3f);
            model.localPosition = new Vector3(0f, bob, 0f);
            model.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
        if (hooves != null)
        {
            hooves.volume = Mathf.Clamp01(Speed / 4f) * 0.7f;
            hooves.pitch = Mathf.Lerp(0.8f, 1.35f, k);
        }
    }
}
