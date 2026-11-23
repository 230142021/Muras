using UnityEngine;

/// <summary>
/// Скорость героя: умения с базара («Жеңил өтүк» — быстрее, «Тоо секирик» — выше прыжок)
/// и временная бодрость от еды из капчыка (PlayerBuff).
/// </summary>
[RequireComponent(typeof(PlayerMovement))]
public class PlayerPerks : MonoBehaviour
{
    public float walkBonus = 1.15f;
    public float runBonus = 1.25f;
    public float jumpBonus = 1.6f;

    private PlayerMovement movement;
    private float baseWalk, baseRun, baseJump, lastBuff = 1f;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        baseWalk = movement.walkSpeed;
        baseRun = movement.runSpeed;
        baseJump = movement.jumpApexHeight;
    }

    void Start()
    {
        ShopCatalog.Changed += Apply;
        Apply();
    }

    void OnDestroy() => ShopCatalog.Changed -= Apply;

    void Update()
    {
        float b = PlayerBuff.Multiplier;
        if (!Mathf.Approximately(b, lastBuff)) Apply();
    }

    void Apply()
    {
        bool fast = ShopCatalog.IsActive("boots");
        lastBuff = PlayerBuff.Multiplier;
        movement.walkSpeed = baseWalk * (fast ? walkBonus : 1f) * Mathf.Lerp(1f, lastBuff, 0.5f);
        movement.runSpeed = baseRun * (fast ? runBonus : 1f) * lastBuff;
        movement.jumpApexHeight = baseJump * (ShopCatalog.IsActive("jump") ? jumpBonus : 1f);
    }
}
