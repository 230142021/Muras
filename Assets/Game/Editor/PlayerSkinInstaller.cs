using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Возвращает герою цвета одежды и добавляет скины.
/// Модель из Mixamo пришла без материалов (поэтому серая). Скрипт сопоставляет её вершины
/// с исходным main_hero_traveler.obj, где у каждой части есть свой цвет, и записывает UV
/// в палитру. Затем любая текстура-палитра = отдельный скин.
/// Запуск: Tools > Diploma > Install Player Skins.
/// </summary>
public static class PlayerSkinInstaller
{
    private const string ScenePath = "Assets/Game/Scenes/Village.unity";
    private const string ObjPath = "Assets/Game/Prefabs/main_hero_traveler.obj";
    private const string SkinDir = "Assets/Game/Models/Characters/Skins/";
    private const string MeshPath = "Assets/Game/Models/Characters/Tourist_Colored.asset";
    private const string MaterialPath = "Assets/Game/Models/Characters/Skins/TouristSkin.mat";

    // Порядок колонок в палитрах (skin_*.png).
    private static readonly string[] Parts =
        { "beige", "boot", "brown_pants", "camera", "cap", "hair", "hoodie", "maroon", "metal", "mustard", "skin" };

    private static readonly string[] SkinFiles = { "skin_tourist.png", "skin_nomad.png", "skin_mountain.png" };

    [MenuItem("Tools/Diploma/Install Player Skins")]
    public static void Install()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string previous = SceneManager.GetActiveScene().path;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject player = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .Where(t => t.name == "Player")
            .Select(t => t.gameObject)
            .FirstOrDefault();
        if (player == null) { Debug.LogError("[PlayerSkinInstaller] Нет объекта Player в Village"); return; }

        SkinnedMeshRenderer smr = player.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .FirstOrDefault(r => r.gameObject.activeInHierarchy) ??
            player.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault();
        if (smr == null || smr.sharedMesh == null) { Debug.LogError("[PlayerSkinInstaller] У Player нет SkinnedMeshRenderer"); return; }

        // 1. Исходный OBJ: треугольники и часть одежды каждого треугольника
        if (!ReadObj(out List<Vector3> objVerts, out List<int> objPart, out List<int> objTris, out List<int> objTriPart))
            return;

        // Берём оригинальный меш из модели (а не уже перекрашенный), чтобы повторный запуск был чистым
        Mesh source = smr.sharedMesh;
        var original = PrefabUtility.GetCorrespondingObjectFromOriginalSource(smr) as SkinnedMeshRenderer;
        if (original != null && original.sharedMesh != null && original.sharedMesh.name != "Tourist_Colored")
            source = original.sharedMesh;

        // 2. Новый меш: каждый треугольник получает ОДИН цвет (вершины на стыках деталей дублируются),
        //    поэтому больше нет радужных полос между цветами палитры
        Mesh mesh = BuildColoredMesh(source, objVerts, objPart, objTris, objTriPart);
        mesh.name = "Tourist_Colored";

        Directory.CreateDirectory(Path.GetDirectoryName(MeshPath));
        // Каждый раз новый файл меша: у пересобранного меша другое число вершин,
        // а перезапись старого ассета ломает SkinnedMeshRenderer («does not match the expected mesh data size»)
        if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath) != null)
            AssetDatabase.DeleteAsset(MeshPath);
        AssetDatabase.CreateAsset(mesh, MeshPath);

        // 3. Текстуры-палитры и материал
        var skins = new List<Texture2D>();
        foreach (string file in SkinFiles)
        {
            string path = SkinDir + file;
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                ti.textureType = TextureImporterType.Default;
                ti.filterMode = FilterMode.Point;
                ti.mipmapEnabled = false;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.sRGBTexture = true;
                ti.SaveAndReimport();
            }
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null) skins.Add(tex);
        }
        if (skins.Count == 0) { Debug.LogError("[PlayerSkinInstaller] Не найдены палитры в " + SkinDir); return; }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", skins[0]);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", skins[0]);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.15f);
        EditorUtility.SetDirty(mat);

        // 4. Назначаем на героя
        smr.sharedMesh = null;
        smr.sharedMesh = mesh;
        smr.localBounds = mesh.bounds;
        smr.sharedMaterials = Enumerable.Repeat(mat, Mathf.Max(1, mesh.subMeshCount)).ToArray();
        EditorUtility.SetDirty(smr);

        if (!player.TryGetComponent(out PlayerSkinSwitcher switcher))
            switcher = player.AddComponent<PlayerSkinSwitcher>();
        switcher.targetRenderer = smr;
        switcher.skins = skins.ToArray();
        EditorUtility.SetDirty(switcher);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[PlayerSkinInstaller] Готово: цвета восстановлены, скинов: {skins.Count}. K — переключить скин в игре.");

        if (!string.IsNullOrEmpty(previous) && previous != ScenePath)
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
    }

    private static bool ReadObj(out List<Vector3> verts, out List<int> partOfVertex, out List<int> tris, out List<int> triPart)
    {
        verts = new List<Vector3>();
        partOfVertex = new List<int>();
        tris = new List<int>();
        triPart = new List<int>();
        string full = Path.Combine(Directory.GetCurrentDirectory(), ObjPath);
        if (!File.Exists(full)) { Debug.LogError("[PlayerSkinInstaller] Нет файла " + ObjPath); return false; }

        int part = Parts.Length - 1;
        var ci = CultureInfo.InvariantCulture;
        var assigned = new Dictionary<int, int>();
        foreach (string raw in File.ReadLines(full))
        {
            string[] p = raw.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
            if (p.Length == 0) continue;
            if (p[0] == "v" && p.Length >= 4)
                verts.Add(new Vector3(float.Parse(p[1], ci), float.Parse(p[2], ci), float.Parse(p[3], ci)));
            else if (p[0] == "usemtl" && p.Length > 1)
            {
                int idx = System.Array.IndexOf(Parts, p[1]);
                part = idx >= 0 ? idx : Parts.Length - 1;
            }
            else if (p[0] == "f" && p.Length >= 4)
            {
                var face = new List<int>();
                for (int k = 1; k < p.Length; k++)
                {
                    int vi = int.Parse(p[k].Split('/')[0], ci);
                    vi = vi < 0 ? verts.Count + vi : vi - 1;
                    face.Add(vi);
                    assigned[vi] = part;
                }
                for (int k = 1; k + 1 < face.Count; k++)
                {
                    tris.Add(face[0]); tris.Add(face[k]); tris.Add(face[k + 1]);
                    triPart.Add(part);
                }
            }
        }
        for (int i = 0; i < verts.Count; i++)
            partOfVertex.Add(assigned.TryGetValue(i, out int v) ? v : Parts.Length - 1);
        return verts.Count > 0 && tris.Count > 0;
    }

    private static Mesh BuildColoredMesh(Mesh src, List<Vector3> objVerts, List<int> objPart, List<int> objTris, List<int> objTriPart)
    {
        Vector3[] v = src.vertices;
        Vector3[] n = src.normals;
        Vector4[] tg = src.tangents;
        BoneWeight[] bw = src.boneWeights;

        // Ориентация осей Mixamo-меша относительно OBJ
        FindAxes(v, objVerts.ToArray(), out int[] perm, out Vector3 sign, out Vector3 aC, out float aS, out Vector3 bC, out float bS);
        Vector3[] b = objVerts.Select(p => (p - bC) / bS).ToArray();

        // Все треугольники меша (все сабмеши — в один)
        var srcTris = new List<int>();
        for (int s = 0; s < src.subMeshCount; s++) srcTris.AddRange(src.GetTriangles(s));

        var newV = new List<Vector3>(); var newN = new List<Vector3>(); var newT = new List<Vector4>();
        var newW = new List<BoneWeight>(); var newUV = new List<Vector2>(); var newTris = new List<int>();
        var map = new Dictionary<long, int>();

        for (int t = 0; t < srcTris.Count; t += 3)
        {
            Vector3 c = (v[srcTris[t]] + v[srcTris[t + 1]] + v[srcTris[t + 2]]) / 3f;
            Vector3 q = Transform((c - aC) / aS, perm, sign);
            int part = NearestTrianglePart(q, b, objTris, objTriPart);

            for (int k = 0; k < 3; k++)
            {
                int vi = srcTris[t + k];
                long key = (long)vi * 32 + part;
                if (!map.TryGetValue(key, out int ni))
                {
                    ni = newV.Count;
                    map[key] = ni;
                    newV.Add(v[vi]);
                    if (n.Length == v.Length) newN.Add(n[vi]);
                    if (tg.Length == v.Length) newT.Add(tg[vi]);
                    if (bw.Length == v.Length) newW.Add(bw[vi]);
                    newUV.Add(new Vector2((part + 0.5f) / 16f, 0.5f));
                }
                newTris.Add(ni);
            }
        }

        var m = new Mesh();
        m.indexFormat = newV.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        m.SetVertices(newV);
        if (newN.Count == newV.Count) m.SetNormals(newN);
        if (newT.Count == newV.Count) m.SetTangents(newT);
        m.SetUVs(0, newUV);
        m.subMeshCount = 1;
        m.SetTriangles(newTris, 0);
        if (newW.Count == newV.Count) m.boneWeights = newW.ToArray();
        m.bindposes = src.bindposes;
        m.RecalculateBounds();
        if (newN.Count != newV.Count) m.RecalculateNormals();
        Debug.Log($"[PlayerSkinInstaller] Треугольников: {newTris.Count / 3}, вершин: {v.Length} → {newV.Count}");
        return m;
    }

    private static void FindAxes(Vector3[] meshVerts, Vector3[] objVerts, out int[] bestPerm, out Vector3 bestSign,
        out Vector3 aC, out float aS, out Vector3 bC, out float bS)
    {
        GetBounds(meshVerts, out aC, out aS);
        GetBounds(objVerts, out bC, out bS);
        Vector3 ac = aC, bc = bC; float asz = aS, bsz = bS;
        Vector3[] a = meshVerts.Select(p => (p - ac) / asz).ToArray();
        Vector3[] b = objVerts.Select(p => (p - bc) / bsz).ToArray();

        int[][] perms = { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } };
        float bestErr = float.MaxValue;
        bestPerm = perms[0];
        bestSign = Vector3.one;
        int step = Mathf.Max(1, a.Length / 400);
        foreach (int[] perm in perms)
            for (int s = 0; s < 8; s++)
            {
                var sign = new Vector3((s & 1) == 0 ? 1 : -1, (s & 2) == 0 ? 1 : -1, (s & 4) == 0 ? 1 : -1);
                float err = 0f;
                for (int i = 0; i < a.Length; i += step)
                    err += Nearest(Transform(a[i], perm, sign), b, out _);
                if (err < bestErr) { bestErr = err; bestPerm = perm; bestSign = sign; }
            }
    }

    private static void GetBounds(Vector3[] pts, out Vector3 center, out float size)
    {
        Vector3 min = pts[0], max = pts[0];
        foreach (Vector3 p in pts) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
        center = (min + max) * 0.5f;
        size = Mathf.Max(max.x - min.x, max.y - min.y, max.z - min.z);
    }

    private static int NearestTrianglePart(Vector3 p, Vector3[] v, List<int> tris, List<int> triPart)
    {
        float best = float.MaxValue;
        int part = Parts.Length - 1;
        for (int t = 0, f = 0; t < tris.Count; t += 3, f++)
        {
            float d = (ClosestOnTriangle(p, v[tris[t]], v[tris[t + 1]], v[tris[t + 2]]) - p).sqrMagnitude;
            if (d < best) { best = d; part = triPart[f]; }
        }
        return part;
    }

    // Ближайшая точка треугольника (Ericson, Real-Time Collision Detection)
    private static Vector3 ClosestOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a, ac = c - a, ap = p - a;
        float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0f && d2 <= 0f) return a;
        Vector3 bp = p - b;
        float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0f && d4 <= d3) return b;
        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + ab * (d1 / (d1 - d3));
        Vector3 cp = p - c;
        float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0f && d5 <= d6) return c;
        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + ac * (d2 / (d2 - d6));
        float va = d3 * d6 - d5 * d4;
        if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f) return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
        float denom = 1f / (va + vb + vc);
        return a + ab * (vb * denom) + ac * (vc * denom);
    }

    // Меш из Mixamo может быть повёрнут/масштабирован относительно OBJ: нормализуем оба облака
    // точек по габаритам и подбираем ориентацию осей с наименьшей ошибкой.
    private static int[] MatchParts(Vector3[] meshVerts, List<Vector3> objVerts, List<int> objPart)
    {
        Vector3[] a = Normalize(meshVerts);
        Vector3[] b = Normalize(objVerts.ToArray());

        int[][] perms = { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } };
        float bestErr = float.MaxValue;
        int[] bestPerm = perms[0];
        Vector3 bestSign = Vector3.one;
        int step = Mathf.Max(1, a.Length / 300);

        foreach (int[] perm in perms)
            for (int s = 0; s < 8; s++)
            {
                var sign = new Vector3((s & 1) == 0 ? 1 : -1, (s & 2) == 0 ? 1 : -1, (s & 4) == 0 ? 1 : -1);
                float err = 0f;
                for (int i = 0; i < a.Length; i += step)
                    err += Nearest(Transform(a[i], perm, sign), b, out _);
                if (err < bestErr) { bestErr = err; bestPerm = perm; bestSign = sign; }
            }

        var result = new int[a.Length];
        for (int i = 0; i < a.Length; i++)
        {
            Nearest(Transform(a[i], bestPerm, bestSign), b, out int j);
            result[i] = objPart[j];
        }
        return result;
    }

    private static Vector3 Transform(Vector3 v, int[] perm, Vector3 sign) =>
        new Vector3(v[perm[0]] * sign.x, v[perm[1]] * sign.y, v[perm[2]] * sign.z);

    private static Vector3[] Normalize(Vector3[] pts)
    {
        Vector3 min = pts[0], max = pts[0];
        foreach (Vector3 p in pts) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
        Vector3 c = (min + max) * 0.5f;
        float size = Mathf.Max(max.x - min.x, max.y - min.y, max.z - min.z);
        return pts.Select(p => (p - c) / size).ToArray();
    }

    private static float Nearest(Vector3 p, Vector3[] cloud, out int index)
    {
        float best = float.MaxValue;
        index = 0;
        for (int i = 0; i < cloud.Length; i++)
        {
            float d = (cloud[i] - p).sqrMagnitude;
            if (d < best) { best = d; index = i; }
        }
        return best;
    }
}
