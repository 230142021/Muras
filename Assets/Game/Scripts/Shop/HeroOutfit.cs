using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Внешний вид героя: красит части одежды по выбранному комплекту и показывает нужную шапку.
/// Модель героя собрана из кусков, прикреплённых к костям (Tools > Diploma > Rebuild Hero Model),
/// материалы называются Hero_&lt;Слот&gt; — по имени слота мы и меняем цвет.
/// В игре материалы копируются, поэтому ассеты в проекте не меняются.
/// </summary>
public class HeroOutfit : MonoBehaviour
{
    public Transform modelRoot;

    private readonly Dictionary<string, Material> slots = new Dictionary<string, Material>();
    private readonly List<GameObject> coat = new List<GameObject>();
    private readonly Dictionary<string, GameObject> hats = new Dictionary<string, GameObject>();

    void Start()
    {
        if (modelRoot == null)
        {
            Transform t = transform.Find("HeroModel");
            modelRoot = t != null ? t : transform;
        }

        var copies = new Dictionary<Material, Material>();
        foreach (Renderer r in modelRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (r is SkinnedMeshRenderer) continue;
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null || !m.name.StartsWith("Hero_")) continue;
                if (!copies.TryGetValue(m, out Material copy))
                {
                    copy = new Material(m) { name = m.name + " (runtime)" };
                    copies[m] = copy;
                    slots[m.name.Substring(5)] = copy;
                }
                mats[i] = copy;
            }
            r.sharedMaterials = mats;
        }

        foreach (Transform t in modelRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.StartsWith("Coat")) coat.Add(t.gameObject);
            else if (t.name.StartsWith("Hat_")) hats[t.name.Substring(4).ToLowerInvariant()] = t.gameObject;
        }

        ShopCatalog.Changed += Apply;
        Apply();
    }

    void OnDestroy() => ShopCatalog.Changed -= Apply;

    public void Apply()
    {
        ShopCatalog.Item outfit = ShopCatalog.Get(ShopCatalog.EquippedOutfit) ?? ShopCatalog.Get(ShopCatalog.DefaultOutfit);
        if (outfit != null && outfit.colors != null)
        {
            for (int i = 0; i < ShopCatalog.OutfitSlots.Length && i < outfit.colors.Length; i++)
                SetColor(ShopCatalog.OutfitSlots[i], outfit.colors[i]);
            foreach (GameObject c in coat) if (c != null) c.SetActive(outfit.coat);
        }

        string hat = ShopCatalog.EquippedHat;
        foreach (var kv in hats) if (kv.Value != null) kv.Value.SetActive(kv.Key == hat);
    }

    void SetColor(string slot, Color c)
    {
        if (!slots.TryGetValue(slot, out Material m)) return;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }
}
