using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Фонари по всей деревне (вдоль дорог и у всех важных мест) + фонарь-чырак для героя.
/// Фонари зажигаются вечером и гаснут утром (StreetLamp). Запуск: Tools > Diploma > Install Street Lamps + Lantern.
/// </summary>
public static class LampInstaller
{
    private const string ScenePath = "Assets/Game/Scenes/Village.unity";
    private const string Model = "Assets/Game/Models/Fun/Street_Lamp.obj";
    private const string MatPath = "Assets/Game/Materials/Lamp_Glow.mat";
    private static readonly Vector3 GlassLocal = new Vector3(-0.78f, 2.64f, 0f);   // OBJ импортируется с зеркальным X

    private static Transform valley;

    [MenuItem("Tools/Diploma/Install Street Lamps + Lantern")]
    public static void Install()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        IEnumerable<Transform> All() => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true));

        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
        if (asset == null) { Debug.LogError("[Lamps] Нет модели " + Model); return; }
        Material glowMat = GlowMaterial();

        foreach (Transform t in All().Where(t => t.name == "StreetLamps").ToList()) if (t != null) Object.DestroyImmediate(t.gameObject);
        valley = All().FirstOrDefault(t => t.name == "MountainValley");
        BuildRoads(All());
        Physics.SyncTransforms();

        GameObject player = All().Select(t => t.GetComponent<PlayerMovement>()).Where(p => p != null).Select(p => p.gameObject).FirstOrDefault();
        Vector3 spawn = player != null ? player.transform.position : Vector3.zero;

        var root = new GameObject("StreetLamps").transform;
        Transform env = All().FirstOrDefault(t => t.name == "Environment");
        if (env != null) root.SetParent(env, true);
        root.position = Vector3.zero;

        var placed = new List<Vector3>();

        // 1) у важных мест
        string[] spots = { "NPC_Aibek", "Zone_Yurt", "Zone_Games", "Zone_Food", "Bazaar", "Farm", "Fishing_Pier", "NPC_Komuzchu", "NPC_Burkutchu", "Shyrdak_Workplace", "NPC_Chabandes", "GatePoint" };
        foreach (string n in spots)
        {
            Transform t = All().FirstOrDefault(x => x.name == n);
            if (t == null) continue;
            if (FindSpot(t.position, 3f, 7f, placed, 9f, out Vector3 p)) { Lamp(root, asset, glowMat, p, Face(p, t.position)); placed.Add(p); }
        }

        // 1б) у каждого дома — перед входом, со стороны дороги
        Transform houses = All().FirstOrDefault(t => t.name == "Village_Houses");
        if (houses != null)
            foreach (Transform h in houses)
            {
                bool ok = false;
                foreach (Vector3 local in new[] { new Vector3(3.2f, 0f, 6.0f), new Vector3(-3.2f, 0f, 6.0f), new Vector3(4.6f, 0f, 4.8f), new Vector3(-4.6f, 0f, 4.8f) })
                {
                    if (ok) break;
                    Vector3 p = h.TransformPoint(local);
                    if (!Free(p, out float gy) || placed.Any(q => (q - p).sqrMagnitude < 7f * 7f)) continue;
                    p.y = gy;
                    Lamp(root, asset, glowMat, p, Face(p, h.TransformPoint(new Vector3(0f, 0f, 12f))));
                    placed.Add(p); ok = true;
                }
            }

        // 2) вдоль дорог и тропинок: точки через ~14 м по самой дороге, фонарь — на обочине (не на дороге!)
        var samples = new List<Vector3>();
        foreach (Vector3 c in roadCenters.OrderBy(c => Vector3.Distance(c, spawn)))
        {
            if (Vector2.Distance(new Vector2(c.x, c.z), new Vector2(spawn.x, spawn.z)) > 95f) continue;
            if (samples.Any(q => (q - c).sqrMagnitude < 14f * 14f)) continue;
            samples.Add(c);
        }
        foreach (Vector3 c in samples)
        {
            if (placed.Count >= 44) break;
            if (placed.Any(q => (q - c).sqrMagnitude < 10f * 10f)) continue;
            bool done = false;
            for (float d = 1.5f; d <= 6f && !done; d += 0.75f)
                for (int k = 0; k < 12 && !done; k++)
                {
                    float ang = k * Mathf.PI / 6f;
                    Vector3 p = c + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * d;
                    float rd = RoadDist(p);
                    if (rd < 0.9f || rd > 2.4f) continue;
                    if (!Free(p, out float gy) || placed.Any(q => (q - p).sqrMagnitude < 10f * 10f)) continue;
                    p.y = gy;
                    Lamp(root, asset, glowMat, p, Face(p, c));
                    placed.Add(p); done = true;
                }
        }

        // 3) фонарь-чырак для героя
        if (player != null)
        {
            var pl = player.GetComponent<PlayerLantern>() ?? player.AddComponent<PlayerLantern>();
            pl.glowMaterial = glowMat;
            EditorUtility.SetDirty(pl);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Lamps] Фонарей поставлено: {placed.Count} (треугольников дорог: {roadTris.Count}). У героя есть чырак (F) — Айбек дарит его, когда становится гидом.");
    }

    static void Lamp(Transform root, GameObject asset, Material glowMat, Vector3 p, Quaternion rot)
    {
        var go = new GameObject("StreetLamp");
        go.transform.SetParent(root, true);
        go.transform.position = p; go.transform.rotation = rot;
        var m = (GameObject)PrefabUtility.InstantiatePrefab(asset, go.transform);
        m.transform.localPosition = Vector3.zero; m.transform.localRotation = Quaternion.identity;
        var cap = go.AddComponent<CapsuleCollider>(); cap.center = new Vector3(0f, 1.75f, 0f); cap.height = 3.5f; cap.radius = 0.16f;

        var glass = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(glass.GetComponent<Collider>());
        glass.name = "Glow";
        glass.transform.SetParent(go.transform, false);
        glass.transform.localPosition = GlassLocal;
        glass.transform.localScale = new Vector3(0.245f, 0.44f, 0.245f);
        var gr = glass.GetComponent<MeshRenderer>();
        gr.sharedMaterial = glowMat;
        gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var lgo = new GameObject("Light");
        lgo.transform.SetParent(go.transform, false);
        lgo.transform.localPosition = GlassLocal + Vector3.down * 0.05f;
        var l = lgo.AddComponent<Light>();
        l.type = LightType.Point; l.range = 14f; l.intensity = 0f; l.color = new Color(1f, 0.78f, 0.48f);
        l.shadows = LightShadows.None; l.enabled = false;

        var sl = go.AddComponent<StreetLamp>();
        sl.lamp = l; sl.glow = gr;
    }

    static Material GlowMaterial()
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, MatPath); }
        m.SetColor("_BaseColor", new Color(1f, 0.85f, 0.6f));
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(1f, 0.78f, 0.48f) * 2.5f);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(m);
        return m;
    }

    static bool IsRoad(string n, string parent) =>
        (n.Contains("Road") || n.Contains("Meadow_Path") || parent.Contains("Road") || parent.Contains("Meadow_Path")) && !n.StartsWith("Trail");

    static Quaternion Face(Vector3 from, Vector3 to) { Vector3 d = to - from; d.y = 0f; return d.sqrMagnitude > 0.01f ? Quaternion.LookRotation(d) * Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity; }

    static bool FindSpot(Vector3 c, float rMin, float rMax, List<Vector3> placed, float spacing, out Vector3 spot)
    {
        for (float r = rMin; r <= rMax; r += 1f)
            for (int k = 0; k < 12; k++)
            {
                float a = k * Mathf.PI / 6f + r;
                Vector3 p = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (!Free(p, out float gy) || placed.Any(q => (q - p).sqrMagnitude < spacing * spacing)) continue;
                spot = new Vector3(p.x, gy, p.z); return true;
            }
        spot = c; return false;
    }

    // ---- дороги как набор треугольников (вид сверху) ----
    private static List<Vector2[]> roadTris = new List<Vector2[]>();
    private static List<Vector3> roadCenters = new List<Vector3>();

    static void BuildRoads(IEnumerable<Transform> all)
    {
        roadTris.Clear(); roadCenters.Clear();
        foreach (Transform t in all)
        {
            var mf = t.GetComponent<MeshFilter>(); var r = t.GetComponent<Renderer>();
            if (mf == null || mf.sharedMesh == null || r == null || !r.enabled) continue;
            if (!IsRoad(t.name, t.parent != null ? t.parent.name : "")) continue;
            Vector3[] v = mf.sharedMesh.vertices; int[] tr = mf.sharedMesh.triangles; Matrix4x4 m = t.localToWorldMatrix;
            for (int i = 0; i + 2 < tr.Length; i += 3)
            {
                Vector3 a = m.MultiplyPoint3x4(v[tr[i]]), b = m.MultiplyPoint3x4(v[tr[i + 1]]), c = m.MultiplyPoint3x4(v[tr[i + 2]]);
                roadTris.Add(new[] { new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z) });
                roadCenters.Add((a + b + c) / 3f);
            }
        }
    }

    static float RoadDist(Vector3 p)
    {
        var q = new Vector2(p.x, p.z); float best = 999f;
        foreach (Vector2[] t in roadTris)
        {
            if (Mathf.Abs(t[0].x - q.x) > 40f || Mathf.Abs(t[0].y - q.y) > 40f) continue;
            if (InTri(q, t[0], t[1], t[2])) return 0f;
            best = Mathf.Min(best, SegDist(q, t[0], t[1]), Mathf.Min(SegDist(q, t[1], t[2]), SegDist(q, t[2], t[0])));
        }
        return best;
    }

    static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }
    static float Cross(Vector2 p, Vector2 a, Vector2 b) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
    static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a; float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    static bool Free(Vector3 p, out float gy)
    {
        if (!GroundY(p, out gy)) return false;
        if (RoadDist(p) < 0.9f) return false;                       // никогда не ставим фонарь на дорогу
        foreach (Collider col in Physics.OverlapBox(new Vector3(p.x, gy + 1.6f, p.z), new Vector3(0.5f, 1.4f, 0.5f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            if (!IsGround(col) && !IsPath(col)) return false;
        return true;
    }

    static bool GroundY(Vector3 p, out float y)
    {
        y = p.y; bool ok = false;
        foreach (RaycastHit h in Physics.RaycastAll(new Vector3(p.x, p.y + 60f, p.z), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore))
            if (IsGround(h.collider) && (!ok || h.point.y > y)) { y = h.point.y; ok = true; }
        return ok;
    }

    static bool IsGround(Collider c) =>
        (valley != null ? c.transform.IsChildOf(valley) : c.name.Contains("Ground")) || c.name.Contains("PP_Meadow_0") || c.name.Contains("Lake_Ground");
    static bool IsPath(Collider c) => c.name.Contains("Road") || c.name.Contains("Meadow_Path") || c.name.Contains("Floor_Tile");
}
