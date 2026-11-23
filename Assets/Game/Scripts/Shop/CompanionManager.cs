using UnityEngine;

/// <summary>Появляет и убирает купленных спутников (пёс Көкжал, конь Тулпар) рядом с героем.</summary>
public class CompanionManager : MonoBehaviour
{
    public GameObject dogModel;
    public GameObject horseModel;
    public AudioClip[] dogCalls;
    public AudioClip[] horseCalls;

    private GameObject dog, horse;

    void Start()
    {
        ShopCatalog.Changed += Refresh;
        Refresh();
    }

    void OnDestroy() => ShopCatalog.Changed -= Refresh;

    void Refresh()
    {
        dog = Ensure(dog, ShopCatalog.IsActive("dog"), dogModel, "Companion_Dog", new Vector3(1.0f, 0f, -1.2f), 2.4f, 8.5f, dogCalls, 9f, 0.035f);
        horse = Ensure(horse, ShopCatalog.IsActive("horse"), horseModel, "Companion_Horse", new Vector3(-2.0f, 0f, 0.2f), 2.0f, 8f, horseCalls, 5.5f, 0.05f);
    }

    GameObject Ensure(GameObject current, bool active, GameObject model, string name, Vector3 offset,
                      float walk, float run, AudioClip[] calls, float stepRate, float bob)
    {
        if (!active)
        {
            if (current != null) Destroy(current);
            return null;
        }
        if (current != null || model == null) return current;

        var root = new GameObject(name);
        var m = Instantiate(model, root.transform);
        m.name = model.name;
        m.transform.localPosition = Vector3.zero;
        m.transform.localRotation = Quaternion.identity;
        foreach (Collider c in m.GetComponentsInChildren<Collider>()) Destroy(c);

        var f = root.AddComponent<CompanionFollower>();
        f.target = transform;
        f.offset = offset;
        f.walkSpeed = walk; f.runSpeed = run;
        f.calls = calls; f.stepRate = stepRate; f.stepBob = bob;
        return root;
    }
}
