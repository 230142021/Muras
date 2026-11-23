using UnityEngine;

/// <summary>
/// Одна косточка-чүкө в круге. Физическая: сака её выбивает, менеджер считает,
/// когда она вылетела за верёвку. Помнит начальную позицию для нового раунда.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ChukoTarget : MonoBehaviour
{
    public ChukoGameManager gameManager;

    private Rigidbody rb;
    private Vector3 startPos;
    private Quaternion startRot;
    private bool initialized;

    public bool Counted { get; private set; }

    void Awake() => Init();

    void Init()
    {
        if (initialized) return;
        rb = GetComponent<Rigidbody>();
        startPos = transform.position;
        startRot = transform.rotation;
        initialized = true;
    }

    public void MarkCounted() => Counted = true;

    /// <summary>Вернуть косточку на место. kinematic = true — стоит неподвижно (до начала игры).</summary>
    public void ResetPiece(bool kinematic)
    {
        Init();
        Counted = false;
        gameObject.SetActive(true);
        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        rb.isKinematic = true;
        transform.SetPositionAndRotation(startPos, startRot);
        rb.isKinematic = kinematic;
        if (!kinematic) rb.Sleep();
    }

    // Совместимость со старой механикой «клик по мишени»
    public void Hit()
    {
        if (Counted || gameManager == null || !gameManager.IsPlaying) return;
        Counted = true;
        gameManager.RegisterHit();
        gameObject.SetActive(false);
    }
}
