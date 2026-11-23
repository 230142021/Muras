using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Новый облик главного героя. Старая модель из Mixamo была авто-ригом угловатого OBJ:
/// при ходьбе одежда мялась и рвалась, цвета «плыли». Теперь герой собран из аккуратных
/// low-poly деталей в стиле жителей села, и каждая деталь прикреплена к своей кости скелета
/// (бедро — к бедру, предплечье — к предплечью). Анимации те же, но ничего не растягивается.
/// Одежду и шапку меняет HeroOutfit (покупки на базаре).
/// Запуск: Tools > Diploma > Rebuild Hero Model.
/// </summary>
public static class HeroModelBuilder
{
    private const string ScenePath = "Assets/Game/Scenes/Village.unity";
    private const string Dir = "Assets/Game/Models/Characters/Hero/";
    private const string MeshDir = Dir + "Meshes/";
    private const string MatDir = Dir + "Materials/";

    // Цвета деталей, которые не меняются от комплекта одежды
    private static readonly Dictionary<string, Color> Fixed = new Dictionary<string, Color>
    {
        { "Skin", new Color(0.83f, 0.64f, 0.50f) }, { "Hair", new Color(0.10f, 0.07f, 0.05f) },
        { "Eyes", new Color(0.05f, 0.04f, 0.04f) }, { "Mouth", new Color(0.62f, 0.30f, 0.30f) },
        { "Sole", new Color(0.08f, 0.07f, 0.06f) }, { "Strap", new Color(0.22f, 0.15f, 0.09f) },
        { "Bedroll", new Color(0.36f, 0.42f, 0.27f) }, { "Kalpak", new Color(0.96f, 0.95f, 0.92f) },
        { "KalpakBlack", new Color(0.06f, 0.06f, 0.07f) }, { "Fur", new Color(0.33f, 0.24f, 0.16f) },
        { "HatTop", new Color(0.62f, 0.14f, 0.14f) }, { "Gold", new Color(0.85f, 0.66f, 0.22f) },
        { "Cap", new Color(0.85f, 0.62f, 0.20f) }, { "CapDark", new Color(0.30f, 0.22f, 0.10f) },
    };

    private static float s;          // масштаб: 1 единица «как у жителей» → метры героя
    private static Vector3 fwd;      // куда смотрит герой
    private static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    private static int partCount;

    [MenuItem("Tools/Diploma/Rebuild Hero Model")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previous = SceneManager.GetActiveScene().path;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject player = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerMovement>(true)).Select(p => p.gameObject).FirstOrDefault();
        if (player == null) { Debug.LogError("[HeroModel] В сцене Village нет игрока (PlayerMovement)."); return; }
        Animator anim = player.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a.isHuman && a.gameObject.activeInHierarchy)
                        ?? player.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a.isHuman);
        if (anim == null) { Debug.LogError("[HeroModel] У игрока нет гуманоидного Animator."); return; }

        Install(scene, player, anim);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[HeroModel] Герой пересобран: {partCount} деталей на костях, масштаб {s:0.000}. Одежда меняется на базаре (E у торговца) и в гардеробе (C).");

        if (!string.IsNullOrEmpty(previous) && previous != ScenePath) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
    }

    public static void Install(Scene scene, GameObject player, Animator anim)
    {
        Transform B(HumanBodyBones b) => anim.GetBoneTransform(b);

        // 1. Убираем прошлую сборку и прячем старую кожу (кости и анимации остаются)
        foreach (Transform t in anim.GetComponentsInChildren<Transform>(true).Where(IsOurs).ToList())
            if (t != null) Object.DestroyImmediate(t.gameObject);
        foreach (SkinnedMeshRenderer smr in anim.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.enabled = false;
            EditorUtility.SetDirty(smr);
        }
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // без видимой кожи аниматор не должен засыпать
        EditorUtility.SetDirty(anim);

        foreach (var sw in player.GetComponentsInChildren<PlayerSkinSwitcher>(true)) { sw.enabled = false; EditorUtility.SetDirty(sw); }

        if (AssetDatabase.IsValidFolder(MeshDir.TrimEnd('/'))) AssetDatabase.DeleteAsset(MeshDir.TrimEnd('/'));
        Directory.CreateDirectory(MeshDir);
        Directory.CreateDirectory(MatDir);
        AssetDatabase.Refresh();
        mats.Clear();
        partCount = 0;

        Transform hips = B(HumanBodyBones.Hips), spine = B(HumanBodyBones.Spine), chest = B(HumanBodyBones.Chest);
        Transform upper = B(HumanBodyBones.UpperChest) ?? chest, neck = B(HumanBodyBones.Neck), head = B(HumanBodyBones.Head);
        Transform lFoot = B(HumanBodyBones.LeftFoot), rFoot = B(HumanBodyBones.RightFoot);
        if (hips == null || spine == null || neck == null || head == null || lFoot == null || rFoot == null)
        {
            Debug.LogError("[HeroModel] Не найдены кости скелета."); return;
        }

        fwd = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
        if (fwd.sqrMagnitude < 0.5f) fwd = Vector3.forward;
        s = Mathf.Max(0.2f, (head.position.y - (lFoot.position.y + rFoot.position.y) * 0.5f) / 1.42f);

        Transform headTop = Child(head, "HeadTop");
        float headLen = headTop != null ? Vector3.Dot(headTop.position - head.position, Vector3.up) / s : 0.22f;

        // ---------------- туловище
        Upright("Pelvis", hips, k => k.Cyl("Pants", 0, -0.12f, 0, new[] { (0f, 0.178f), (0.13f, 0.167f) }, 12, 0.74f));

        Seg("Belly", spine, upper != spine ? upper : neck, (k, L) =>
            k.Loft("Jacket", 0, 0, 0.72f, 12, new[] { (-0.10f, 0.173f), (0f, 0.169f), (L * 0.5f, 0.168f), (L, 0.181f) }));
        Seg("Chest", upper, neck, (k, L) =>
            k.Loft("Jacket", 0, 0, 0.70f, 12, new[] { (-0.04f, 0.181f), (L * 0.4f, 0.196f), (L * 0.75f, 0.19f), (L - 0.01f, 0.16f), (L + 0.015f, 0.105f) }));

        Upright("Belt", spine, k =>
        {
            k.Cyl("Belt", 0, -0.035f, 0, new[] { (0f, 0.179f), (0.055f, 0.179f) }, 12, 0.74f);
            k.Box("Accent", new Vector3(0, -0.008f, 0.133f), new Vector3(0.05f, 0.045f, 0.016f));
        });

        Upright("Neck", neck, k =>
        {
            k.Cyl("Skin", 0, -0.03f, 0.005f, new[] { (0f, 0.052f), (0.13f, 0.05f) }, 10, 1f);
            k.Cyl("Accent", 0, -0.025f, 0, new[] { (0f, 0.088f), (0.04f, 0.074f) }, 10, 0.9f);   // ворот / шарф
        });

        // Рюкзак со скаткой
        Upright("Backpack", upper, k =>
        {
            k.Box("Pack", new Vector3(0, 0.10f, -0.21f), new Vector3(0.27f, 0.36f, 0.14f));
            k.Box("Strap", new Vector3(0, 0.255f, -0.205f), new Vector3(0.28f, 0.05f, 0.155f));
            k.Box("Strap", new Vector3(0, 0.0f, -0.285f), new Vector3(0.18f, 0.13f, 0.03f));
            k.Tube("Bedroll", new Vector3(-0.165f, 0.33f, -0.2f), new Vector3(0.165f, 0.33f, -0.2f), 0.056f, 0.056f, 8);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                k.Box("Strap", new Vector3(0.085f * sx, 0.07f, 0.142f), new Vector3(0.036f, 0.29f, 0.014f));
                k.Box("Strap", new Vector3(0.085f * sx, 0.212f, -0.01f), new Vector3(0.036f, 0.014f, 0.29f));
            }
        });

        // Полы чапана (видны только у длинной одежды)
        Upright("Coat_Skirt", hips, k =>
        {
            const float gap = 1.15f;
            var rings = new[] { (0.06f, 0.183f), (-0.15f, 0.2f), (-0.40f, 0.222f) };
            k.Loft("Jacket", 0, -0.005f, 0.78f, 16, rings, gap / 2f, Mathf.PI * 2f - gap / 2f);
            k.Loft("Accent", 0, -0.005f, 0.78f, 16, new[] { (-0.37f, 0.225f), (-0.41f, 0.227f) }, gap / 2f, Mathf.PI * 2f - gap / 2f);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                float a = sx * gap / 2f;
                Vector3 top = new Vector3(0.186f * Mathf.Sin(a), 0.06f, -0.005f + 0.186f * 0.78f * Mathf.Cos(a));
                Vector3 bot = new Vector3(0.226f * Mathf.Sin(a), -0.41f, -0.005f + 0.226f * 0.78f * Mathf.Cos(a));
                k.Tube("Accent", top, bot, 0.013f, 0.013f, 6);
            }
        });

        // ---------------- голова
        float yc = headLen - 0.125f;   // центр головы
        Upright("Head", head, k =>
        {
            k.Ellipsoid("Skin", new Vector3(0, yc, 0.01f), new Vector3(0.1f, 0.125f, 0.11f), 12, 8);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                k.Ellipsoid("Skin", new Vector3(0.1f * sx, yc, 0f), new Vector3(0.02f, 0.035f, 0.025f), 6, 4);
                k.Box("Eyes", new Vector3(0.037f * sx, yc + 0.015f, 0.105f), new Vector3(0.022f, 0.016f, 0.012f));
                k.Box("Hair", new Vector3(0.038f * sx, yc + 0.045f, 0.104f), new Vector3(0.042f, 0.009f, 0.012f));
            }
            k.Ellipsoid("Hair", new Vector3(0, yc + 0.04f, -0.015f), new Vector3(0.104f, 0.09f, 0.108f), 12, 6);
            k.Box("Skin", new Vector3(0, yc - 0.015f, 0.12f), new Vector3(0.02f, 0.03f, 0.022f));
            k.Box("Mouth", new Vector3(0, yc - 0.052f, 0.107f), new Vector3(0.036f, 0.009f, 0.012f));
        });

        // Шапки: включается одна (HeroOutfit)
        float kb = yc + 0.08f;
        Upright("Hat_Kalpak", head, k =>
        {
            k.Cyl("Kalpak", 0, kb, 0, new[] { (0f, 0.12f), (0.08f, 0.117f), (0.16f, 0.106f), (0.23f, 0.088f), (0.28f, 0.064f), (0.315f, 0.036f), (0.33f, 0.006f) }, 16, 0.98f);
            k.Brim("KalpakBlack", kb);
        });
        float tb = yc + 0.06f;
        Upright("Hat_Tebetey", head, k =>
        {
            k.Cyl("Fur", 0, tb, 0, new[] { (0f, 0.137f), (0.05f, 0.145f), (0.10f, 0.14f) }, 14, 1f);
            k.Cyl("HatTop", 0, tb + 0.08f, 0, new[] { (0f, 0.115f), (0.08f, 0.10f), (0.13f, 0.06f), (0.15f, 0.0f) }, 12, 1f);
            k.Ellipsoid("Gold", new Vector3(0, tb + 0.235f, 0), new Vector3(0.02f, 0.02f, 0.02f), 6, 4);
        });
        float cb = yc + 0.065f;
        Upright("Hat_Cap", head, k =>
        {
            k.Cyl("Cap", 0, cb, -0.005f, new[] { (0f, 0.113f), (0.06f, 0.111f), (0.11f, 0.087f), (0.14f, 0.045f), (0.152f, 0.0f) }, 12, 1.02f);
            k.Box("Cap", new Vector3(0, cb + 0.008f, 0.16f), new Vector3(0.17f, 0.014f, 0.1f));
            k.Box("CapDark", new Vector3(0, cb + 0.004f, 0.012f), new Vector3(0.232f, 0.022f, 0.01f));
        });

        // ---------------- руки
        foreach (bool left in new[] { true, false })
        {
            string side = left ? "L" : "R";
            Transform ua = B(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            Transform la = B(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            Transform hand = B(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Transform index = B(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            Transform sh = B(left ? HumanBodyBones.LeftShoulder : HumanBodyBones.RightShoulder);
            if (ua == null || la == null || hand == null) continue;

            if (sh != null)
                Seg("Shoulder" + side, sh, ua, (k, L) => k.Tube("Jacket", new Vector3(0, L * 0.25f, 0), new Vector3(0, L, 0), 0.07f, 0.062f, 8));
            Seg("UpperArm" + side, ua, la, (k, L) =>
            {
                k.Ellipsoid("Jacket", Vector3.zero, new Vector3(0.064f, 0.064f, 0.064f), 8, 5);
                k.Tube("Jacket", Vector3.zero, new Vector3(0, L, 0), 0.06f, 0.049f, 8);
            });
            Seg("LowerArm" + side, la, hand, (k, L) =>
            {
                k.Tube("Jacket", new Vector3(0, -0.02f, 0), new Vector3(0, L, 0), 0.05f, 0.043f, 8);
                k.Tube("Accent", new Vector3(0, L - 0.04f, 0), new Vector3(0, L - 0.005f, 0), 0.047f, 0.047f, 8);
            });
            Vector3 handTip = index != null ? index.position : hand.position + (hand.position - la.position).normalized * 0.08f * s;
            SegTo("Hand" + side, hand, handTip, k =>
            {
                k.Ellipsoid("Skin", new Vector3(0, 0.05f, 0), new Vector3(0.03f, 0.065f, 0.045f), 8, 5);
                k.Ellipsoid("Skin", new Vector3(0, 0.025f, 0.042f), new Vector3(0.016f, 0.03f, 0.016f), 6, 4);   // большой палец
            });
        }

        // ---------------- ноги
        foreach (bool left in new[] { true, false })
        {
            string side = left ? "L" : "R";
            Transform ul = B(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
            Transform ll = B(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
            Transform foot = left ? lFoot : rFoot;
            Transform toes = B(left ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes);
            if (ul == null || ll == null) continue;

            Seg("Thigh" + side, ul, ll, (k, L) => k.Tube("Pants", new Vector3(0, -0.04f, 0), new Vector3(0, L, 0), 0.096f, 0.073f, 10));
            Seg("Shin" + side, ll, foot, (k, L) => k.Tube("Pants", new Vector3(0, -0.02f, 0), new Vector3(0, L, 0), 0.073f, 0.062f, 10));

            Transform toeEnd = toes != null ? (Child(toes, "Toe_End") ?? toes) : null;
            Vector3 tip = toeEnd != null ? toeEnd.position : foot.position + fwd * 0.17f * s;
            Vector3 flatDir = Vector3.ProjectOnPlane(tip - foot.position, Vector3.up);
            if (flatDir.sqrMagnitude < 1e-6f) flatDir = fwd;
            float toeLen = Mathf.Clamp(flatDir.magnitude / s, 0.12f, 0.26f);
            float g = Mathf.Clamp((Mathf.Min(tip.y, foot.position.y) - foot.position.y) / s - 0.015f, -0.15f, -0.04f);

            var boot = MakeGo("Boot" + side, foot, foot.position, Quaternion.LookRotation(flatDir.normalized, Vector3.up));
            var bk = new Kit(s);
            bk.Cyl("Boots", 0, g, -0.005f, new[] { (0f, 0.07f), (0.06f, 0.074f), (0.33f, 0.068f) }, 10, 1.22f);
            bk.Box("Boots", new Vector3(0, g + 0.036f, toeLen * 0.55f), new Vector3(0.102f, 0.072f, toeLen * 0.85f));
            bk.Box("Sole", new Vector3(0, g + 0.008f, toeLen * 0.40f), new Vector3(0.114f, 0.017f, toeLen + 0.13f));
            Finish(boot, bk);
        }

        // В редакторе показываем комплект по умолчанию: калпак, без пол чапана (в игре это решает HeroOutfit)
        foreach (Transform t in anim.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Coat_Skirt" || t.name == "Hat_Tebetey" || t.name == "Hat_Cap") t.gameObject.SetActive(false);
        }
    }

    // ================================================================== детали на костях

    private static bool IsOurs(Transform t) => t.name.StartsWith("HeroPart_") || t.name.StartsWith("Hat_") || t.name.StartsWith("Coat_");

    private static Transform Child(Transform root, string contains) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t != root && t.name.Contains(contains));

    /// <summary>Деталь в «прямой» системе: Y вверх, Z вперёд героя, начало — в кости.</summary>
    private static void Upright(string name, Transform bone, System.Action<Kit> build)
    {
        if (bone == null) return;
        var go = MakeGo(name, bone, bone.position, Quaternion.LookRotation(fwd, Vector3.up));
        var k = new Kit(s); build(k); Finish(go, k);
    }

    /// <summary>Деталь вдоль кости: Y от кости a к кости b (L — длина в единицах жителей).</summary>
    private static void Seg(string name, Transform a, Transform b, System.Action<Kit, float> build)
    {
        if (a == null || b == null) return;
        Vector3 d = b.position - a.position;
        if (d.sqrMagnitude < 1e-8f) return;
        var go = MakeGo(name, a, a.position, Frame(d));
        var k = new Kit(s); build(k, d.magnitude / s); Finish(go, k);
    }

    private static void SegTo(string name, Transform a, Vector3 tip, System.Action<Kit> build)
    {
        Vector3 d = tip - a.position;
        if (d.sqrMagnitude < 1e-8f) d = Vector3.down;
        var go = MakeGo(name, a, a.position, Frame(d));
        var k = new Kit(s); build(k); Finish(go, k);
    }

    private static Quaternion Frame(Vector3 dir)
    {
        dir.Normalize();
        Vector3 f = Vector3.ProjectOnPlane(fwd, dir);
        if (f.sqrMagnitude < 0.01f) f = Vector3.ProjectOnPlane(Vector3.up, dir);
        return Quaternion.LookRotation(f.normalized, dir);
    }

    private static GameObject MakeGo(string name, Transform bone, Vector3 pos, Quaternion rot)
    {
        bool special = name.StartsWith("Hat_") || name.StartsWith("Coat_");
        var go = new GameObject(special ? name : "HeroPart_" + name);
        go.transform.SetParent(bone, false);
        go.transform.position = pos;
        go.transform.rotation = rot;
        Vector3 ls = bone.lossyScale;
        go.transform.localScale = new Vector3(1f / Mathf.Max(1e-5f, Mathf.Abs(ls.x)), 1f / Mathf.Max(1e-5f, Mathf.Abs(ls.y)), 1f / Mathf.Max(1e-5f, Mathf.Abs(ls.z)));
        return go;
    }

    private static void Finish(GameObject go, Kit k)
    {
        Mesh mesh = k.ToMesh(go.name, out List<string> slots);
        AssetDatabase.CreateAsset(mesh, MeshDir + go.name + ".asset");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = slots.Select(Mat).ToArray();
        partCount++;
    }

    private static Material Mat(string slot)
    {
        if (mats.TryGetValue(slot, out Material m)) return m;
        string path = MatDir + "Hero_" + slot + ".mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        Color c = Fixed.TryGetValue(slot, out Color fc) ? fc : DefaultOutfitColor(slot);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", slot == "Eyes" ? 0.6f : 0.12f);
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);   // двусторонний — без дыр на стыках
        m.doubleSidedGI = true;
        EditorUtility.SetDirty(m);
        mats[slot] = m;
        return m;
    }

    private static Color DefaultOutfitColor(string slot)
    {
        ShopCatalog.Item it = ShopCatalog.Get(ShopCatalog.DefaultOutfit);
        int i = System.Array.IndexOf(ShopCatalog.OutfitSlots, slot);
        return it != null && i >= 0 && i < it.colors.Length ? it.colors[i] : Color.gray;
    }

    // ================================================================== простая лепка мешей

    private class Kit
    {
        private class Part { public readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>(); public readonly List<int> t = new List<int>(); }
        private readonly Dictionary<string, Part> parts = new Dictionary<string, Part>();
        private readonly List<string> order = new List<string>();
        private readonly float k;

        public Kit(float scale) { k = scale; }

        private Part P(string slot)
        {
            if (!parts.TryGetValue(slot, out Part p)) { p = new Part(); parts[slot] = p; order.Add(slot); }
            return p;
        }

        /// <summary>Плоская грань; outward — куда она должна смотреть.</summary>
        public void Face(string slot, Vector3 outward, params Vector3[] pts)
        {
            if (pts.Length < 3) return;
            Vector3 area = Vector3.zero;
            for (int i = 1; i < pts.Length - 1; i++) area += Vector3.Cross(pts[i] - pts[0], pts[i + 1] - pts[0]);
            if (area.sqrMagnitude < 1e-14f) return;
            if (Vector3.Dot(area, outward) < 0f) System.Array.Reverse(pts);
            Vector3 nrm = Vector3.Dot(area, outward) < 0f ? -area.normalized : area.normalized;
            Part p = P(slot);
            int b = p.v.Count;
            foreach (Vector3 q in pts) { p.v.Add(q * k); p.n.Add(nrm); }
            for (int i = 1; i < pts.Length - 1; i++) { p.t.Add(b); p.t.Add(b + i); p.t.Add(b + i + 1); }
        }

        /// <summary>Тело вращения вдоль Y: кольца (y, r), сечение — эллипс r × r*ez.</summary>
        public void Loft(string slot, float cx, float cz, float ez, int n, (float y, float r)[] rings, float a0 = 0f, float a1 = -1f)
        {
            bool closed = a1 < 0f;
            if (closed) { a0 = 0f; a1 = Mathf.PI * 2f; }
            int cnt = closed ? n : n + 1;
            var R = new List<Vector3[]>();
            foreach (var (y, r) in rings)
            {
                var ring = new Vector3[cnt];
                for (int i = 0; i < cnt; i++)
                {
                    float a = a0 + (a1 - a0) * i / n;
                    ring[i] = new Vector3(cx + r * Mathf.Sin(a), y, cz + r * ez * Mathf.Cos(a));
                }
                R.Add(ring);
            }
            for (int j = 0; j < R.Count - 1; j++)
            {
                Vector3[] A = R[j], B2 = R[j + 1];
                int segs = closed ? n : n;
                for (int i = 0; i < segs; i++)
                {
                    int i2 = closed ? (i + 1) % n : i + 1;
                    Vector3 c = (A[i] + A[i2] + B2[i] + B2[i2]) * 0.25f;
                    Vector3 axis = new Vector3(cx, c.y, cz);
                    Face(slot, c - axis, A[i], A[i2], B2[i2], B2[i]);
                }
            }
            if (closed)
            {
                if (rings[0].r > 1e-4f) Face(slot, Vector3.down, R[0]);
                if (rings[rings.Length - 1].r > 1e-4f) Face(slot, Vector3.up, R[R.Count - 1]);
            }
        }

        public void Cyl(string slot, float x, float y0, float z, (float dy, float r)[] profile, int n, float ez) =>
            Loft(slot, x, z, ez, n, profile.Select(p => (y0 + p.dy, p.r)).ToArray());

        public void Ellipsoid(string slot, Vector3 c, Vector3 r, int nu, int nv)
        {
            var G = new Vector3[nv + 1, nu];
            for (int v = 0; v <= nv; v++)
                for (int u = 0; u < nu; u++)
                {
                    float th = Mathf.PI * v / nv, ph = 2f * Mathf.PI * u / nu;
                    G[v, u] = c + new Vector3(r.x * Mathf.Sin(th) * Mathf.Sin(ph), r.y * Mathf.Cos(th), r.z * Mathf.Sin(th) * Mathf.Cos(ph));
                }
            for (int v = 0; v < nv; v++)
                for (int u = 0; u < nu; u++)
                {
                    int u2 = (u + 1) % nu;
                    Vector3 a = G[v, u], b = G[v, u2], cc = G[v + 1, u2], d = G[v + 1, u];
                    Vector3 mid = (a + b + cc + d) * 0.25f;
                    if (v == 0) Face(slot, mid - c, a, cc, d);
                    else if (v == nv - 1) Face(slot, mid - c, a, b, d);
                    else Face(slot, mid - c, a, b, cc, d);
                }
        }

        public void Box(string slot, Vector3 c, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3 V(int x, int y, int z) => c + new Vector3(x * h.x, y * h.y, z * h.z);
            Face(slot, Vector3.right, V(1, -1, -1), V(1, 1, -1), V(1, 1, 1), V(1, -1, 1));
            Face(slot, Vector3.left, V(-1, -1, -1), V(-1, -1, 1), V(-1, 1, 1), V(-1, 1, -1));
            Face(slot, Vector3.up, V(-1, 1, -1), V(-1, 1, 1), V(1, 1, 1), V(1, 1, -1));
            Face(slot, Vector3.down, V(-1, -1, -1), V(1, -1, -1), V(1, -1, 1), V(-1, -1, 1));
            Face(slot, Vector3.forward, V(-1, -1, 1), V(1, -1, 1), V(1, 1, 1), V(-1, 1, 1));
            Face(slot, Vector3.back, V(-1, -1, -1), V(-1, 1, -1), V(1, 1, -1), V(1, -1, -1));
        }

        public void Tube(string slot, Vector3 p0, Vector3 p1, float r0, float r1, int n)
        {
            Vector3 ax = (p1 - p0).normalized;
            Vector3 t = Mathf.Abs(ax.x) < 0.9f ? Vector3.right : Vector3.forward;
            Vector3 u = Vector3.Cross(ax, t).normalized, w = Vector3.Cross(ax, u);
            var A = new Vector3[n]; var Bv = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = 2f * Mathf.PI * i / n;
                Vector3 off = Mathf.Cos(a) * u + Mathf.Sin(a) * w;
                A[i] = p0 + off * r0; Bv[i] = p1 + off * r1;
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector3 mid = (A[i] + A[j] + Bv[i] + Bv[j]) * 0.25f;
                Vector3 onAxis = p0 + ax * Vector3.Dot(mid - p0, ax);
                Face(slot, mid - onAxis, A[i], A[j], Bv[j], Bv[i]);
            }
            Face(slot, -ax, A);
            Face(slot, ax, Bv);
        }

        /// <summary>Чёрные отвороты калпака: две загнутые вверх половинки с разрезом спереди и сзади.</summary>
        public void Brim(string slot, float y)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                const int n = 8;
                float a0 = 10f * Mathf.Deg2Rad, a1 = Mathf.PI - 10f * Mathf.Deg2Rad;
                for (int i = 0; i < n; i++)
                {
                    float aa = side * (a0 + (a1 - a0) * i / n), ab = side * (a0 + (a1 - a0) * (i + 1) / n);
                    Vector3 lo0 = new Vector3(0.124f * Mathf.Sin(aa), y - 0.005f, 0.122f * Mathf.Cos(aa));
                    Vector3 lo1 = new Vector3(0.124f * Mathf.Sin(ab), y - 0.005f, 0.122f * Mathf.Cos(ab));
                    Vector3 hi0 = new Vector3(0.142f * Mathf.Sin(aa), y + 0.065f, 0.140f * Mathf.Cos(aa));
                    Vector3 hi1 = new Vector3(0.142f * Mathf.Sin(ab), y + 0.065f, 0.140f * Mathf.Cos(ab));
                    Vector3 mid = (lo0 + lo1 + hi0 + hi1) * 0.25f;
                    Face(slot, new Vector3(mid.x, 0f, mid.z), lo0, lo1, hi1, hi0);
                }
            }
        }

        public Mesh ToMesh(string name, out List<string> slots)
        {
            var mesh = new Mesh { name = name };
            var V = new List<Vector3>(); var N = new List<Vector3>();
            foreach (string sl in order) { V.AddRange(parts[sl].v); N.AddRange(parts[sl].n); }
            mesh.SetVertices(V); mesh.SetNormals(N);
            mesh.subMeshCount = order.Count;
            int baseV = 0;
            for (int i = 0; i < order.Count; i++)
            {
                Part p = parts[order[i]];
                mesh.SetTriangles(p.t.Select(x => x + baseV).ToList(), i);
                baseV += p.v.Count;
            }
            mesh.RecalculateBounds();
            slots = new List<string>(order);
            return mesh;
        }
    }
}
