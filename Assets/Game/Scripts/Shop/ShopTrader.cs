using UnityEngine;

/// <summary>Торговец Асан на базаре: подойди и нажми E, чтобы потратить опыт.</summary>
public class ShopTrader : MonoBehaviour
{
    [Tooltip("Точка перед прилавком, от неё считается расстояние")]
    public Transform counterFront;
    public float talkDistance = 3.2f;

    private Transform player;

    /// <summary>Игрок стоит у прилавка — в капчыке можно продавать.</summary>
    public static bool PlayerNear { get; private set; }

    void OnDisable() => PlayerNear = false;

    void Start()
    {
        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null) player = pm.transform;
    }

    void Update()
    {
        if (player == null) return;
        Vector3 c = counterFront != null ? counterFront.position : transform.position;
        Vector3 d = player.position - c; d.y = 0f;
        PlayerNear = d.sqrMagnitude <= (talkDistance + 1.5f) * (talkDistance + 1.5f);
        if (!Modal.Free || VillageShop.Instance == null) return;
        if (d.sqrMagnitude > talkDistance * talkDistance) return;

        InteractionHint.Request("<b><color=#E8B04A>E</color></b>  —  " +
                                Loc.T("базар: потратить опыт (" + ShopCatalog.Balance + " XP)", "bazaar: spend XP (" + ShopCatalog.Balance + " XP)"));
        if (Input.GetKeyDown(KeyCode.E)) VillageShop.Instance.Open(true);
    }
}
