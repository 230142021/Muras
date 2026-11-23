using UnityEngine;

/// <summary>
/// Яблоко в саду: после знакомства с Айбеком его можно сорвать (E) — оно попадёт в капчык.
/// Через несколько минут на этом месте вырастает новое.
/// </summary>
public class ApplePickup : MonoBehaviour
{
    public Transform player;
    public AppleQuestManager questManager;

    [Tooltip("С какого расстояния (м) можно подобрать яблоко")]
    public float pickupRadius = 1.3f;
    [Tooltip("Через сколько секунд вырастает новое яблоко")]
    public float regrowSeconds = 180f;

    private float hiddenUntil = -1f;
    private Renderer[] renderers;
    private Collider[] colliders;

    void Start()
    {
        // только те рендеры, что включены в сцене: у корня есть служебная серая сфера (выключена) — её не трогаем,
        // иначе ночью над землёй висели тёмные шары
        renderers = System.Array.FindAll(GetComponentsInChildren<Renderer>(true), r => r.enabled && r.gameObject.activeInHierarchy && r.GetComponent<MeshFilter>()?.sharedMesh?.name != "Sphere");
        colliders = GetComponentsInChildren<Collider>(true);
        if (questManager == null) questManager = FindAnyObjectByType<AppleQuestManager>();
        if (player == null) { var pm = FindAnyObjectByType<PlayerMovement>(); if (pm != null) player = pm.transform; }
    }

    void Update()
    {
        bool can = questManager == null || questManager.CanCollectApples;
        bool hidden = hiddenUntil > Time.time;
        Show(can && !hidden);
        if (!can || hidden || player == null || !Modal.Free) return;

        Vector3 a = transform.position, p = player.position;
        a.y = 0f; p.y = 0f;
        if (Vector3.Distance(a, p) > pickupRadius) return;

        InteractionHint.Request("<b><color=#E8B04A>E</color></b>  —  " + Loc.T("сорвать яблоко", "pick the apple"));
        if (Input.GetKeyDown(KeyCode.E)) Collect();
    }

    void Show(bool v)
    {
        if (renderers == null) return;
        foreach (Renderer r in renderers) if (r != null && r.enabled != v) r.enabled = v;
        foreach (Collider c in colliders) if (c != null && c.enabled != v) c.enabled = v;
    }

    void Collect()
    {
        GameAudio.Pickup();
        Inventory.Add("alma");
        Vocab.Unlock("alma");
        hiddenUntil = Time.time + regrowSeconds;
        Show(false);
    }
}
