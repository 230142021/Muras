using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Обустраивает долину Village:
/// • горная речка (вода + каменистое русло) вдоль восточного края долины, с галькой и камнями на берегах;
/// • дополнительные горы-скалы у подножий;
/// • рощи деревьев, камни, цветы, трава и грибы по всей долине (из паков Pure Poly).
/// Не трогает село, зоны квестов и дорожки к ним. Всё кладётся в Environment/Decor_Valley и
/// Environment/River — их можно удалить или двигать вручную. Повторный запуск пересоздаёт декор.
/// Запуск: Tools > Diploma > Decorate Valley (river, mountains, trees).
/// </summary>
public static class ValleyDecorator
{
    private const string ScenePath = "Assets/Game/Scenes/Village.unity";
    private const string PP = "Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/";
    private const string OutDir = "Assets/Game/Models/Environment/";

    // Эллипс ровной поляны долины (как в MountainValley.obj)
    private static readonly Vector2 ValleyCenter = new Vector2(-1.74f, -1.9f);
    private const float FX = 42f, FZ = 78f;

    private static Transform valley;
    private static System.Random rng;
    private static List<Bounds> obstacles;
    private static List<(Vector3 a, Vector3 b, float r)> corridors;
    private static List<Vector3> riverLine;
    private static List<Vector3> placed;

    [MenuItem("Tools/Diploma/Decorate Valley (river, mountains, trees)")]
    public static void Decorate()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform env = Find(scene, "Environment");
        valley = Find(scene, "MountainValley");
        if (env == null || valley == null)
        {
            Debug.LogError("[Decor] Нет Environment или MountainValley");
            return;
        }

        foreach (string old in new[] { "Decor_Valley", "River" })
        {
            Transform t = env.Find(old);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
        Physics.SyncTransforms();

        rng = new System.Random(2026);
        placed = new List<Vector3>();
        CollectObstacles(scene, env);

        // 1. Речка
        riverLine = BuildRiverLine();
        GameObject river = BuildRiver(env, riverLine);

        // 2. Декор
        var root = new GameObject("Decor_Valley").transform;
        root.SetParent(env, false);
        Transform gMount = Group(root, "Mountains");
        Transform gTrees = Group(root, "Trees");
        Transform gRocks = Group(root, "Rocks");
        Transform gFlowers = Group(root, "Flowers");
        Transform gGrass = Group(root, "Grass");
        Transform gRiver = Group(root, "RiverBanks");

        int mountains = PlaceMountains(gMount);
        int trees = PlaceGroves(gTrees);
        int rocks = Scatter(gRocks, new[] { "PP_Rock_Moss_Grown_09", "PP_Rock_Moss_Grown_11", "PP_Rock_Pile_Forest_Moss_05", "PP_Rock_Pile_Forest_Moss_10" },
            45, 0.95f, 1.0f, 2.6f, 4.5f, collide: true, size: Size.Width);
        int flowers = Scatter(gFlowers, new[] { "PP_Daffodil_03", "PP_Hyacinth_04", "PP_Sunflower_04" },
            110, 0.98f, 0.35f, 0.8f, 1.2f, clusters: true, size: Size.Height);
        int grass = Scatter(gGrass, new[] { "PP_Grass_11", "PP_Grass_15" },
            260, 1.05f, 0.3f, 0.6f, 0.8f, clusters: true, size: Size.Height);
        int mush = ScatterNearTrees(gTrees, new[] { "PP_Mushroom_Fantasy_Orange_09", "PP_Mushroom_Fantasy_Orange_10", "PP_Mushroom_Fantasy_Purple_05", "PP_Mushroom_Fantasy_Purple_08" }, 24);
        int banks = DecorateBanks(gRiver);

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = river;
        Debug.Log($"[Decor] Готово: речка ({riverLine.Count} точек), горы {mountains}, деревья {trees}, камни {rocks}, " +
                  $"цветы {flowers}, трава {grass}, грибы {mush}, у реки {banks}. Village сохранена.");
    }

    // ================================================================= obstacles

    private static void CollectObstacles(Scene scene, Transform env)
    {
        obstacles = new List<Bounds>();
        foreach (Renderer r in env.GetComponentsInChildren<Renderer>())
        {
            if (r.transform.IsChildOf(valley)) continue;
            Bounds b = r.bounds;
            if (Mathf.Max(b.size.x, b.size.z) < 0.8f || b.size.x > 90f) continue;
            b.Expand(new Vector3(3f, 0f, 3f));
            obstacles.Add(b);
        }

        foreach (string n in new[] { "Zone_Yurt", "Zone_Games", "Zone_Food", "NPC_Aibek", "Player", "Apple_01", "Apple_01 (1)", "Apple_01 (2)" })
        {
            Transform t = Find(scene, n);
            if (t == null) continue;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
            {
                Bounds b = r.bounds;
                if (b.size.x > 90f) continue;
                b.Expand(new Vector3(7f, 0f, 7f));   // вокруг зон квестов — свободное место
                obstacles.Add(b);
            }
        }

        // Дорожки от Айбека к зонам — без деревьев и камней
        corridors = new List<(Vector3, Vector3, float)>();
        Transform aibek = Find(scene, "NPC_Aibek");
        Vector3 hub = aibek != null ? aibek.position : Vector3.zero;
        foreach (string target in new[] { "BuildPoint", "Zone_Games", "Kitchen", "Zone_Food" })
        {
            Transform t = Find(scene, target);
            if (t != null) corridors.Add((Flat(hub), Flat(t.position), 5f));
        }
    }

    private static bool Blocked(Vector3 p, float radius)
    {
        foreach (Bounds b in obstacles)
            if (p.x > b.min.x - radius && p.x < b.max.x + radius && p.z > b.min.z - radius && p.z < b.max.z + radius)
                return true;
        foreach (var (a, b, r) in corridors)
            if (DistToSegment(Flat(p), a, b) < r + radius) return true;
        return false;
    }

    // ===================================================================== river

    private static List<Vector3> BuildRiverLine()
    {
        var line = new List<Vector3>();
        float phase = (float)rng.NextDouble() * 10f;
        float prevX = 31f;
        for (float z = -210f; z <= 210f; z += 2.5f)
        {
            float baseX = 31f + 5f * Mathf.Sin(z / 26f + phase) + 2f * Mathf.Sin(z / 9f);
            // Отодвигаем русло от препятствий (в пределах восточной части долины)
            float best = baseX, bestCost = float.MaxValue;
            for (float dx = -10f; dx <= 10f; dx += 1f)
            {
                float x = baseX + dx;
                if (x < 22f || x > 42f) continue;
                float cost = Mathf.Abs(dx) + Mathf.Abs(x - prevX) * 2f + (Blocked(new Vector3(x, 0f, z), 3f) ? 1000f : 0f);
                if (cost < bestCost) { bestCost = cost; best = x; }
            }
            prevX = best;
            float y = Ground(new Vector3(best, 0f, z), out bool ok);
            if (!ok || y > 14f) { if (line.Count > 8) break; line.Clear(); continue; }  // только по низине
            line.Add(new Vector3(best, y, z));
        }

        // Сглаживание
        for (int pass = 0; pass < 3; pass++)
            for (int i = 1; i < line.Count - 1; i++)
                line[i] = new Vector3((line[i - 1].x + line[i].x * 2f + line[i + 1].x) / 4f, line[i].y, line[i].z);
        return line;
    }

    private static GameObject BuildRiver(Transform env, List<Vector3> line)
    {
        var river = new GameObject("River");
        river.transform.SetParent(env, false);
        if (line.Count < 4) { Debug.LogWarning("[Decor] Не удалось проложить речку"); return river; }

        Material water = AssetDatabase.LoadAssetAtPath<Material>("Assets/Pure Poly/Free Low Poly Nature Pack/Materials/PP_Water.mat");
        Material bedMat = GetMat(OutDir + "RiverBed.mat", new Color(0.36f, 0.42f, 0.42f));

        Mesh bed = Ribbon(line, i => 5.6f + 1.2f * Noise(i * 0.13f), 0.04f);
        Mesh surface = Ribbon(line, i => 4.0f + 1.0f * Noise(i * 0.13f), 0.14f);
        SaveMesh(bed, OutDir + "RiverBed.asset");
        SaveMesh(surface, OutDir + "RiverWater.asset");

        AddMeshObject(river.transform, "RiverBed", bed, bedMat);
        GameObject w = AddMeshObject(river.transform, "RiverWater", surface, water != null ? water : GetMat(OutDir + "RiverWaterFallback.mat", new Color(0.4f, 0.75f, 0.95f)));
        w.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return river;
    }

    private static Mesh Ribbon(List<Vector3> line, System.Func<int, float> width, float lift)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        float dist = 0f;
        for (int i = 0; i < line.Count; i++)
        {
            Vector3 prev = line[Mathf.Max(0, i - 1)], next = line[Mathf.Min(line.Count - 1, i + 1)];
            Vector3 dir = Flat(next - prev).normalized;
            Vector3 side = new Vector3(dir.z, 0f, -dir.x);
            float w = width(i) * 0.5f;
            Vector3 c = line[i], l = c - side * w, r = c + side * w;
            float y = Mathf.Max(Ground(l, out _), Ground(r, out _), Ground(c, out _)) + lift;
            verts.Add(new Vector3(l.x, y, l.z));
            verts.Add(new Vector3(r.x, y, r.z));
            if (i > 0) dist += Vector3.Distance(line[i], line[i - 1]);
            uvs.Add(new Vector2(0f, dist / 4f));
            uvs.Add(new Vector2(1f, dist / 4f));
            if (i > 0)
            {
                int a = (i - 1) * 2;
                tris.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
            }
        }
        var mesh = new Mesh { name = "River" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static int DecorateBanks(Transform parent)
    {
        int n = 0;
        string[] pebbles = { "PP_Cemetery_Pebbles_03", "PP_Cemetery_Pebbles_09" };
        string[] stones = { "PP_Rock_Moss_Grown_09", "PP_Rock_Moss_Grown_11" };
        for (int i = 2; i < riverLine.Count - 2; i += 3)
        {
            Vector3 dir = Flat(riverLine[i + 1] - riverLine[i - 1]).normalized;
            Vector3 side = new Vector3(dir.z, 0f, -dir.x);
            foreach (int s in new[] { -1, 1 })
            {
                if (rng.NextDouble() < 0.35) continue;
                Vector3 p = riverLine[i] + side * s * (3.3f + (float)rng.NextDouble() * 1.4f);
                bool big = rng.NextDouble() < 0.18;
                if (Spawn(parent, big ? Pick(stones) : Pick(pebbles), p, big ? 1.2f : 0.9f, big ? 2f : 1.6f, Size.Width, big))
                    n++;
            }
        }
        return n;
    }

    // ================================================================ mountains

    private static int PlaceMountains(Transform parent)
    {
        int n = 0;
        string[] names = { "PP_Forest_Mountain_Moss_01", "PP_Forest_Mountain_Moss_02" };
        for (int k = 0; k < 26; k++)
        {
            float a = k / 26f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.15f;
            float e = 1.12f + (float)rng.NextDouble() * 0.28f;
            var p = new Vector3(ValleyCenter.x + FX * e * Mathf.Cos(a), 0f, ValleyCenter.y + FZ * e * Mathf.Sin(a));
            if (NearRiver(p, 14f)) continue;
            if (Spawn(parent, Pick(names), p, 22f, 42f, Size.Width, collide: true, sink: 1.5f))
                n++;
        }
        return n;
    }

    // =================================================================== trees

    private static int PlaceGroves(Transform parent)
    {
        int n = 0;
        string[] names = { "PP_Tree_02", "PP_Tree_10", "PP_Birch_Tree_05", "PP_Birch_Tree_06" };
        for (int g = 0; g < 22; g++)
        {
            Vector3 c = RandomInValley(1.22f);
            if (Blocked(c, 2f) || NearRiver(c, 6f)) continue;
            int count = 3 + rng.Next(6);
            string kind = Pick(names);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = c + new Vector3((float)(rng.NextDouble() - 0.5) * 14f, 0f, (float)(rng.NextDouble() - 0.5) * 14f);
                if (Blocked(p, 1.5f) || NearRiver(p, 5f) || TooClose(p, 3f)) continue;
                string name = rng.NextDouble() < 0.7 ? kind : Pick(names);
                if (Spawn(parent, name, p, 4.5f, 8f, Size.Height, collide: true)) n++;
            }
        }
        // одиночные деревья по лугу
        for (int i = 0; i < 25; i++)
        {
            Vector3 p = RandomInValley(1.0f);
            if (Blocked(p, 2f) || NearRiver(p, 5f) || TooClose(p, 6f)) continue;
            if (Spawn(parent, Pick(names), p, 4.5f, 7.5f, Size.Height, collide: true)) n++;
        }
        return n;
    }

    private static int ScatterNearTrees(Transform trees, string[] names, int count)
    {
        int n = 0;
        var list = trees.Cast<Transform>().ToList();
        for (int i = 0; i < count && list.Count > 0; i++)
        {
            Transform t = list[rng.Next(list.Count)];
            Vector3 p = t.position + new Vector3((float)(rng.NextDouble() - 0.5) * 3f, 0f, (float)(rng.NextDouble() - 0.5) * 3f);
            if (Blocked(p, 0.3f)) continue;
            if (Spawn(t.parent, Pick(names), p, 0.3f, 0.6f, Size.Height)) n++;
        }
        return n;
    }

    // ================================================================= generic

    private enum Size { Height, Width }

    private static int Scatter(Transform parent, string[] names, int count, float maxE, float minSize, float maxSize,
        float minSpacing, bool collide = false, bool clusters = false, Size size = Size.Height)
    {
        int n = 0;
        Vector3 center = RandomInValley(maxE);
        for (int i = 0; i < count; i++)
        {
            if (!clusters || i % 8 == 0) center = RandomInValley(maxE);
            Vector3 p = clusters
                ? center + new Vector3((float)(rng.NextDouble() - 0.5) * 9f, 0f, (float)(rng.NextDouble() - 0.5) * 9f)
                : center;
            if (!clusters) center = RandomInValley(maxE);
            if (Blocked(p, 0.5f) || NearRiver(p, 3.5f) || TooClose(p, minSpacing)) continue;
            if (Spawn(parent, Pick(names), p, minSize, maxSize, size, collide)) n++;
        }
        return n;
    }

    private static bool Spawn(Transform parent, string prefabName, Vector3 p, float minSize, float maxSize,
        Size size, bool collide = false, float sink = 0f)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PP + prefabName + ".prefab");
        if (asset == null) return false;

        float y = Ground(p, out bool ok);
        if (!ok) return false;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.transform.position = new Vector3(p.x, y, p.z);
        go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

        Bounds b = Measure(go);
        float current = size == Size.Height ? b.size.y : Mathf.Max(b.size.x, b.size.z);
        float wanted = Mathf.Lerp(minSize, maxSize, (float)rng.NextDouble());
        if (current > 0.001f) go.transform.localScale *= wanted / current;

        b = Measure(go);
        go.transform.position += Vector3.up * (y - b.min.y - sink - 0.02f);

        if (collide && !go.GetComponentInChildren<Collider>())
        {
            b = Measure(go);
            if (size == Size.Height && b.size.y > 2.5f)
            {
                // ствол дерева
                var cap = go.AddComponent<CapsuleCollider>();
                Vector3 s = go.transform.lossyScale;
                cap.radius = 0.35f / Mathf.Max(0.01f, s.x);
                cap.height = b.size.y * 0.6f / Mathf.Max(0.01f, s.y);
                cap.center = new Vector3(0f, cap.height * 0.5f, 0f);
            }
            else
            {
                foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
                    if (mf.sharedMesh != null)
                    {
                        var mc = mf.gameObject.AddComponent<MeshCollider>();
                        mc.sharedMesh = mf.sharedMesh;
                    }
            }
        }

        placed.Add(go.transform.position);
        return true;
    }

    private static Vector3 RandomInValley(float maxE)
    {
        for (int tries = 0; tries < 50; tries++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float e = Mathf.Sqrt((float)rng.NextDouble()) * maxE;
            var p = new Vector3(ValleyCenter.x + FX * e * Mathf.Cos(a), 0f, ValleyCenter.y + FZ * e * Mathf.Sin(a));
            if (e > 0.25f) return p;   // самый центр — село
        }
        return new Vector3(ValleyCenter.x, 0f, ValleyCenter.y + FZ * 0.8f);
    }

    private static bool NearRiver(Vector3 p, float d)
    {
        if (riverLine == null) return false;
        for (int i = 1; i < riverLine.Count; i++)
            if (DistToSegment(Flat(p), Flat(riverLine[i - 1]), Flat(riverLine[i])) < d) return true;
        return false;
    }

    private static bool TooClose(Vector3 p, float d)
    {
        float d2 = d * d;
        foreach (Vector3 q in placed)
            if ((Flat(q) - Flat(p)).sqrMagnitude < d2) return true;
        return false;
    }

    // Высота долины в точке (только сам рельеф MountainValley)
    private static float Ground(Vector3 p, out bool ok)
    {
        ok = false;
        var hits = Physics.RaycastAll(new Vector3(p.x, 200f, p.z), Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MinValue;
        foreach (RaycastHit h in hits)
            if (h.collider.transform.IsChildOf(valley) && h.point.y > best) { best = h.point.y; ok = true; }
        return ok ? best : 0f;
    }

    private static float Noise(float x) => Mathf.PerlinNoise(x, 0.37f) * 2f - 1f;

    private static string Pick(string[] arr) => arr[rng.Next(arr.Length)];

    private static Transform Group(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    private static GameObject AddMeshObject(Transform parent, string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    private static void SaveMesh(Mesh mesh, string path)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
    }

    private static Material GetMat(string path, Color color)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Bounds Measure(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    private static float DistToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude < 0.0001f ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector3.Distance(p, a + ab * t);
    }

    private static Transform Find(Scene scene, string name) =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == name);
}
