using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Живое село: жилые горные домики с дворами (дрова, тандыр, бельё, шырдак, сено), дым из труб,
/// свет в окнах ночью; жители у домов (Апа, Байке, Чабан) с репликами; смена дня и ночи
/// с солнцем, луной, звёздами, часами на экране; совы и сверчки ночью.
/// Tools > Diploma > Build Village Life (houses, NPC, day-night). Можно запускать повторно — дома пересоздаются.
/// </summary>
public static class VillageLifeInstaller
{
    private const string ScenePath = "Assets/Game/Scenes/Village.unity";
    private const string HouseDir = "Assets/Game/Models/Houses/";
    private const string NpcDir = "Assets/Game/Models/NPC/";
    private const string Portraits = "Assets/Game/UI/Sprites/";
    private const string Sfx = "Assets/Game/Audio/SFX/";
    private const string GenDir = "Assets/Game/Models/Generated/";
    private const string MatDir = "Assets/Game/Materials/";

    private static System.Random rng;
    private static Transform valley;

    private struct Plan { public string model; public Vector2 pos; public float yaw; }

    // Примерные места: по обе стороны тропы, между зонами квестов. Точное место ищется рядом автоматически.
    private static readonly Plan[] Plans =
    {
        new Plan { model = "KG_House_Blue",  pos = new Vector2(-19f, -30f), yaw = 90f },
        new Plan { model = "KG_House_White", pos = new Vector2(-21f, -9f),  yaw = 90f },
        new Plan { model = "KG_House_Mint",  pos = new Vector2(-19f, 13f),  yaw = 90f },
        new Plan { model = "KG_House_Sky",   pos = new Vector2(-21f, 33f),  yaw = 90f },
        new Plan { model = "KG_House_White", pos = new Vector2(11f, -34f),  yaw = -90f },
        new Plan { model = "KG_House_Blue",  pos = new Vector2(13f, -14f),  yaw = -90f },
        new Plan { model = "KG_House_Mint",  pos = new Vector2(10f, 13f),   yaw = -90f },
    };

    private static readonly Dictionary<string, Vector3> Chimneys = new Dictionary<string, Vector3>
    {
        { "House_White_Green", new Vector3(2.1f, 5.45f, -0.9f) },
        { "House_White_Red", new Vector3(2.1f, 5.45f, -0.9f) },
        { "House_Stone", new Vector3(-1.8f, 4.55f, -0.6f) },
        { "House_Log", new Vector3(-1.9f, 5.1f, -0.8f) },
        { "KG_House_Blue", new Vector3(1.725f, 5.6f, -0.625f) },
        { "KG_House_White", new Vector3(1.725f, 5.6f, -0.625f) },
        { "KG_House_Mint", new Vector3(1.725f, 5.6f, -0.625f) },
        { "KG_House_Sky", new Vector3(1.725f, 5.6f, -0.625f) },
    };

    [MenuItem("Tools/Diploma/Build Village Life (houses, NPC, day-night)")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        rng = new System.Random(77);
        PrepareImports();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        valley = FindT(scene, "MountainValley");
        Physics.SyncTransforms();

        int houses = BuildHouses(scene, out List<Transform> placed);
        BuildFarm(scene, placed);
        int npcs = BuildVillagers(scene, placed);
        AddSpeechCloseButton(scene);
        BuildDayNight(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[VillageLife] Домов: {houses}, жителей: {npcs}, смена дня и ночи включена (T — ускорить время). Village сохранена.");
    }

    // ================================================================== imports

    private static void PrepareImports()
    {
        foreach (string p in new[] { "ui_portrait_apa.png", "ui_portrait_baike.png", "ui_portrait_chaban.png" })
            if (AssetImporter.GetAtPath(Portraits + p) is TextureImporter ti && ti.textureType != TextureImporterType.Sprite)
            {
                ti.textureType = TextureImporterType.Sprite; ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.SaveAndReimport();
            }
        if (AssetImporter.GetAtPath(GenDir + "smoke_puff.png") is TextureImporter st && !st.alphaIsTransparency)
        {
            st.alphaIsTransparency = true; st.wrapMode = TextureWrapMode.Clamp; st.SaveAndReimport();
        }
        foreach (string a in new[] { "owl_1.wav", "owl_2.wav", "night_crickets.ogg" })
            if (AssetImporter.GetAtPath(Sfx + a) is AudioImporter ai)
            {
                var s = ai.defaultSampleSettings;
                s.loadType = a.EndsWith(".ogg") ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                ai.defaultSampleSettings = s; ai.SaveAndReimport();
            }
    }

    // ================================================================== houses

    private static int BuildHouses(Scene scene, out List<Transform> placed)
    {
        placed = new List<Transform>();
        Transform env = FindT(scene, "Environment");
        DestroyAll(scene, "Village_Houses");
        var root = new GameObject("Village_Houses").transform;
        if (env != null) root.SetParent(env, true);
        root.position = Vector3.zero;

        Material smoke = SmokeMaterial();
        var avoid = new List<(Vector3 p, float r)>();
        foreach (string n in new[] { "NPC_Aibek", "Zone_Yurt", "Zone_Games", "Zone_Food" })
        {
            Transform t = FindT(scene, n);
            if (t != null) avoid.Add((t.position, n == "NPC_Aibek" ? 10f : 17f));
        }
        foreach (Transform t in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>()).Where(t => t.name.StartsWith("Apple")))
            avoid.Add((t.position, 5f));

        int i = 0;
        foreach (Plan plan in Plans)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(HouseDir + plan.model + ".obj");
            if (asset == null) { Debug.LogWarning("[VillageLife] Нет модели " + plan.model); continue; }
            var house = (GameObject)PrefabUtility.InstantiatePrefab(asset, root);
            house.name = "House_" + (++i) + "_" + plan.model.Replace("House_", "");
            float yaw = plan.yaw + Rand(-8f, 8f);
            house.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Коллайдер по размеру дома (в локальных координатах)
            Bounds lb = LocalBounds(house);
            var box = house.AddComponent<BoxCollider>();
            box.center = lb.center; box.size = lb.size;

            flatFails = blockFails = avoidFails = 0; blockers.Clear();
            if (!FindSpot(house, plan.pos, lb, avoid, placed, out Vector3 at))
            {
                Debug.LogWarning($"[VillageLife] Не нашлось места для {house.name}: неровно {flatFails}, занято {blockFails} ({string.Join(", ", blockers.OrderByDescending(b => b.Value).Take(4).Select(b => b.Key + "×" + b.Value))}), рядом с зонами {avoidFails}");
                Object.DestroyImmediate(house);
                continue;
            }
            house.transform.position = at;
            Physics.SyncTransforms();
            ClearDecorUnder(scene, house, lb);
            GameObjectUtility.SetStaticEditorFlags(house, StaticEditorFlags.BatchingStatic);

            // Свет у крыльца + свет в окнах ночью
            var lightGo = new GameObject("PorchLight");
            lightGo.transform.SetParent(house.transform, false);
            lightGo.transform.localPosition = new Vector3(-2.4f, 3.1f, 3.6f);   // над дверью
            var pl = lightGo.AddComponent<Light>();
            pl.type = LightType.Point; pl.range = 9f; pl.color = new Color(1f, 0.72f, 0.42f); pl.intensity = 0f; pl.shadows = LightShadows.None; pl.enabled = false;
            var hl = house.AddComponent<HouseLights>();
            hl.porchLight = pl;

            // Можно зайти внутрь: точные коллайдеры стен вместо коробки, открывающаяся дверь, обстановка
            Object.DestroyImmediate(box);
            foreach (MeshFilter mf in house.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null && !mf.TryGetComponent(out Collider _)) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            hl.interiorLight = FurnishInterior(house.transform);

            // Дым из трубы
            if (Chimneys.TryGetValue(plan.model, out Vector3 ch)) AddSmoke(house.transform, ch, smoke);

            // Двор
            DecorateYard(house.transform, lb, i);
            placed.Add(house.transform);
        }
        return placed.Count;
    }

    private static int flatFails, blockFails, avoidFails;
    private static readonly Dictionary<string, int> blockers = new Dictionary<string, int>();

    private static bool FindSpot(GameObject house, Vector2 want, Bounds lb, List<(Vector3 p, float r)> avoid, List<Transform> placed, out Vector3 at)
    {
        at = Vector3.zero;
        float best = float.MaxValue;
        bool found = false;
        for (float r = 0f; r <= 20f; r += 1.5f)
        {
            int steps = r == 0f ? 1 : Mathf.CeilToInt(r * 2f);
            for (int k = 0; k < steps; k++)
            {
                float a = k * Mathf.PI * 2f / steps;
                var c = new Vector3(want.x + Mathf.Cos(a) * r, 0f, want.y + Mathf.Sin(a) * r);
                if (avoid.Any(v => Flat(v.p - c).magnitude < v.r + Mathf.Max(lb.extents.x, lb.extents.z))) { avoidFails++; continue; }
                if (placed.Any(p => Flat(p.position - c).magnitude < 12f)) continue;

                // Ровность: высоты по углам
                Quaternion rot = house.transform.rotation;
                float lo = float.MaxValue, hi = float.MinValue; bool ok = true;
                foreach (Vector2 s in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1), Vector2.zero })
                {
                    Vector3 p = c + rot * new Vector3(lb.center.x + s.x * lb.extents.x, 0f, lb.center.z + s.y * lb.extents.z);
                    if (!GroundY(p, out float y)) { ok = false; break; }
                    lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y);
                }
                if (!ok || hi - lo > 1.4f) { flatFails++; continue; }

                // Свободно ли место (деревья, заборы, другие объекты)
                Vector3 center = c + rot * new Vector3(lb.center.x, lb.extents.y + lo + 0.3f, lb.center.z);
                Collider hit = Physics.OverlapBox(center, new Vector3(lb.extents.x, lb.extents.y, lb.extents.z) + Vector3.one * 0.3f, rot, ~0, QueryTriggerInteraction.Ignore)
                    .FirstOrDefault(col => !IsGround(col) && !col.transform.IsChildOf(house.transform) && !IsPath(col) && col.bounds.size.magnitude > 2.5f);
                if (hit != null) { blockFails++; blockers[hit.name] = blockers.TryGetValue(hit.name, out int bc) ? bc + 1 : 1; continue; }

                float score = r + (hi - lo) * 3f;
                if (score < best) { best = score; at = new Vector3(c.x, lo, c.z); found = true; }
            }
            if (found && r >= 3f) break;
        }
        return found;
    }

    private static void ClearDecorUnder(Scene scene, GameObject house, Bounds lb)
    {
        var b = new Bounds(house.transform.TransformPoint(lb.center), Vector3.zero);
        foreach (Renderer r in house.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        b.Expand(new Vector3(1f, 10f, 1f));
        Transform trail = FindT(scene, "Decor_Trail");
        var kill = new List<GameObject>();
        foreach (Transform root in new[] { trail, FindT(scene, "Decor_Valley"), FindT(scene, "Decorations") })
        {
            if (root == null) continue;
            foreach (Transform t in root.GetComponentsInChildren<Transform>())
            {
                if (t == root || t.parent != root && t.parent.parent != root) continue;
                Renderer r = t.GetComponentInChildren<Renderer>();
                if (r == null || r.bounds.size.magnitude > 6f) continue;
                if (b.Contains(r.bounds.center) && !t.name.Contains("Fence") && !t.name.Contains("Floor_Tile") && !t.name.Contains("Meadow")) kill.Add(PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject) ?? t.gameObject);
            }
        }
        foreach (GameObject g in kill.Distinct()) if (g != null) Object.DestroyImmediate(g);
    }

    private static readonly string[] YardProps = { "Yard_Firewood", "Yard_Tandyr", "Yard_Clothesline", "Yard_ShyrdakFence", "Yard_Barrel", "Yard_Haystack" };

    private static void DecorateYard(Transform house, Bounds lb, int seed)
    {
        var yard = new GameObject("Yard").transform;
        yard.SetParent(house, false);
        // места вокруг дома (локально): бока и задний двор; лавочка у двери
        var spots = new List<(Vector3 p, float yaw)>
        {
            (new Vector3(lb.min.x - 1.6f, 0f, lb.center.z + 0.5f), 90f),
            (new Vector3(lb.max.x + 1.6f, 0f, lb.center.z - 0.5f), -90f),
            (new Vector3(lb.center.x - 1.5f, 0f, lb.min.z - 2.0f), 0f),
            (new Vector3(lb.center.x + 2.2f, 0f, lb.min.z - 2.4f), 0f),
            (new Vector3(lb.max.x + 1.4f, 0f, lb.max.z - 0.5f), -90f),
        };
        // сарай за домом, дверью к дому
        PlaceProp(yard, "Assets/Game/Models/Houses/KG_Saray.obj", new Vector3(lb.center.x + 2.2f, 0f, lb.min.z - 3.6f), 180f, true);
        var props = YardProps.OrderBy(_ => rng.Next()).Take(3 + rng.Next(2)).ToList();
        for (int k = 0; k < props.Count && k < spots.Count; k++)
            PlaceProp(yard, "Assets/Game/Models/Houses/" + props[k] + ".obj", spots[k].p, spots[k].yaw + Rand(-12f, 12f), props[k] != "Yard_Clothesline" && props[k] != "Yard_ShyrdakFence");
        // лавочка у крыльца
        PlaceProp(yard, "Assets/Game/Models/Houses/Yard_Bench.obj", new Vector3(lb.center.x + 2.3f, 0f, lb.max.z - 0.3f), 180f, false);
    }

    private static void PlaceProp(Transform parent, string path, Vector3 local, float yaw, bool collider)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) return;
        Vector3 w = parent.TransformPoint(local);
        if (!GroundY(w, out float y)) return;
        foreach (Collider c in Physics.OverlapSphere(new Vector3(w.x, y + 0.6f, w.z), 0.6f, ~0, QueryTriggerInteraction.Collide))
            if (!IsGround(c) && !IsPath(c) && !c.transform.IsChildOf(parent.parent)) return;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.transform.position = new Vector3(w.x, y, w.z);
        go.transform.rotation = parent.rotation * Quaternion.Euler(0f, yaw, 0f);
        if (collider)
        {
            Bounds b = LocalBounds(go);
            var bc = go.AddComponent<BoxCollider>(); bc.center = b.center; bc.size = b.size;
        }
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }

    private static Material SmokeMaterial()
    {
        string path = MatDir + "ChimneySmoke.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(GenDir + "smoke_puff.png"));
        m.SetColor("_BaseColor", new Color(0.92f, 0.92f, 0.92f, 1f));
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void AddSmoke(Transform house, Vector3 local, Material mat)
    {
        var go = new GameObject("ChimneySmoke");
        go.transform.SetParent(house, false);
        go.transform.localPosition = local;
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(0.85f, 0.85f, 0.85f, 0.5f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;
        main.gravityModifier = -0.01f;
        main.prewarm = true;
        var em = ps.emission; em.rateOverTime = 3.5f;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 8f; shape.radius = 0.12f;
        var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 3.2f)));
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.8f, 0.8f, 0.82f), 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.15f), new GradientAlphaKey(0.25f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0.25f, 0.45f); vel.y = new ParticleSystem.MinMaxCurve(0.1f, 0.2f); vel.z = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.2f; noise.frequency = 0.3f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
    }


    // ================================================================== interior

    private const float FloorY = 0.55f;

    private static readonly (string model, Vector3 pos, float yaw)[] Furniture =
    {
        ("Interior_Shyrdak",   new Vector3(0.6f, FloorY + 0.002f, -0.3f), 0f),
        ("Interior_Dastorkon", new Vector3(0.6f, FloorY + 0.017f, -0.3f), 0f),
        ("Interior_Tushkiyiz", new Vector3(0.6f, FloorY + 1.45f, -2.77f), 0f),
        ("Interior_Juk",       new Vector3(3.2f, FloorY, -2.45f), 0f),
        ("Interior_Stove",     new Vector3(-3.3f, FloorY, -2.35f), 0f),
        ("Interior_Bed",       new Vector3(3.4f, FloorY, 1.4f), 0f),
        ("Interior_Cupboard",  new Vector3(-3.65f, FloorY, 0.9f), 90f),
        ("Interior_Lamp",      new Vector3(0f, FloorY + 2.55f, 0f), 0f),
    };

    private static Light FurnishInterior(Transform house)
    {
        var room = new GameObject("Interior").transform;
        room.SetParent(house, false);
        foreach (var f in Furniture)
        {
            GameObject a = AssetDatabase.LoadAssetAtPath<GameObject>(HouseDir + f.model + ".obj");
            if (a == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(a, room);
            go.transform.localPosition = f.pos;
            go.transform.localRotation = Quaternion.Euler(0f, f.yaw, 0f);
            if (f.model == "Interior_Juk" || f.model == "Interior_Stove" || f.model == "Interior_Bed" || f.model == "Interior_Cupboard")
            {
                Bounds b = LocalBounds(go);
                var bc = go.AddComponent<BoxCollider>(); bc.center = b.center; bc.size = b.size;
            }
        }
        // дверь на петле (створка уходит внутрь дома)
        GameObject door = AssetDatabase.LoadAssetAtPath<GameObject>(HouseDir + "KG_Door.obj");
        if (door != null)
        {
            var d = (GameObject)PrefabUtility.InstantiatePrefab(door, house);
            d.name = "Door";
            d.transform.localPosition = new Vector3(-1.9f, FloorY, 2.94f);
            d.transform.localRotation = Quaternion.identity;
            d.AddComponent<HouseDoor>();
        }
        var lg = new GameObject("InteriorLight");
        lg.transform.SetParent(room, false);
        lg.transform.localPosition = new Vector3(0f, FloorY + 1.9f, 0f);
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point; l.range = 7.5f; l.intensity = 0.55f; l.color = new Color(1f, 0.82f, 0.6f); l.shadows = LightShadows.None;
        return l;
    }

    // ================================================================== farm

    private static Transform farmGate;

    private static void BuildFarm(Scene scene, List<Transform> houses)
    {
        DestroyAll(scene, "Farm");
        farmGate = null;
        Transform env = FindT(scene, "Environment");
        var avoid = new List<(Vector3 p, float r)>();
        foreach (string n in new[] { "NPC_Aibek", "Zone_Yurt", "Zone_Games", "Zone_Food" })
        {
            Transform t = FindT(scene, n);
            if (t != null) avoid.Add((t.position, n == "NPC_Aibek" ? 14f : 22f));
        }
        foreach (Transform h in houses) avoid.Add((h.position, 16f));

        // ищем ровное свободное место ~24 x 16 м
        Vector3 best = Vector3.zero; float bestScore = float.MaxValue; bool found = false;
        foreach (Vector2 c0 in new[] { new Vector2(-44f, -18f), new Vector2(-46f, 8f), new Vector2(-42f, 34f), new Vector2(-34f, -52f) })
            for (float r = 0f; r <= 24f; r += 3f)
                for (int k = 0; k < Mathf.Max(1, (int)(r * 1.2f)); k++)
                {
                    float a = k * Mathf.PI * 2f / Mathf.Max(1, (int)(r * 1.2f));
                    var c = new Vector3(c0.x + Mathf.Cos(a) * r, 0f, c0.y + Mathf.Sin(a) * r);
                    if (avoid.Any(v => Flat(v.p - c).magnitude < v.r + 12f)) continue;
                    float lo = float.MaxValue, hi = float.MinValue; bool ok = true;
                    for (int ix = -2; ix <= 2 && ok; ix++)
                        for (int iz = -2; iz <= 2 && ok; iz++)
                        {
                            if (!GroundY(c + new Vector3(ix * 6f, 0f, iz * 4f), out float y)) { ok = false; break; }
                            lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y);
                        }
                    if (!ok || hi - lo > 1.6f) continue;
                    bool blocked = Physics.OverlapBox(new Vector3(c.x, lo + 1.5f, c.z), new Vector3(12f, 1.2f, 8f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                        .Any(col => !IsGround(col) && !IsPath(col) && col.bounds.size.magnitude > 2.5f);
                    if (blocked) continue;
                    float score = r + (hi - lo) * 4f;
                    if (score < bestScore) { bestScore = score; best = new Vector3(c.x, lo, c.z); found = true; }
                }
        if (!found) { Debug.LogWarning("[VillageLife] Не нашлось места для фермы"); return; }

        var farm = new GameObject("Farm").transform;
        if (env != null) farm.SetParent(env, true);
        farm.position = best;
        Vector3 toVillage = Flat(-best).normalized;   // ворота смотрят к центру села
        farm.rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, toVillage) * -1f, Vector3.up);   // +X фермы — к селу

        const float HX = 9f, HZ = 6f;
        string F = "Assets/Game/Models/Farm/";
        // забор по периметру, проход на стороне к селу
        for (int i = 0; i < 6; i++)
        {
            float x = -HX + 1.5f + i * 3f;
            FarmPiece(farm, F + "Farm_Fence.obj", new Vector3(x, 0f, HZ), 0f, true);
            FarmPiece(farm, F + "Farm_Fence.obj", new Vector3(x, 0f, -HZ), 0f, true);
        }
        for (int i = 0; i < 4; i++)
        {
            float z = -HZ + 1.5f + i * 3f;
            FarmPiece(farm, F + "Farm_Fence.obj", new Vector3(-HX, 0f, z), 90f, true);
            if (i != 2) FarmPiece(farm, F + "Farm_Fence.obj", new Vector3(HX, 0f, z), 90f, true);   // ворота
        }
        FarmPiece(farm, F + "Farm_Shelter.obj", new Vector3(-5.5f, 0f, -3.6f), 0f, true);
        FarmPiece(farm, F + "Farm_Trough.obj", new Vector3(2.5f, 0f, -4.8f), 0f, true);
        FarmPiece(farm, F + "Farm_Feeder.obj", new Vector3(-1.5f, 0f, 4.3f), 0f, true);
        FarmPiece(farm, HouseDir + "Yard_Haystack.obj", new Vector3(6.5f, 0f, -4.4f), 0f, true);
        FarmPiece(farm, HouseDir + "Yard_Haystack.obj", new Vector3(-7.2f, 0f, 4.2f), 40f, true);
        FarmPiece(farm, F + "Farm_HitchingPost.obj", new Vector3(HX + 3.5f, 0f, -3.5f), 90f, true);

        var area = new GameObject("PenArea").transform; area.SetParent(farm, false);
        var horseArea = new GameObject("HorseArea").transform; horseArea.SetParent(farm, false); horseArea.localPosition = new Vector3(HX + 3.5f, 0f, -3.5f);
        var dogArea = new GameObject("DogArea").transform; dogArea.SetParent(farm, false); dogArea.localPosition = new Vector3(HX + 3f, 0f, 1.5f);

        AudioClip[] C(params string[] n) => n.Select(x => AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + x + ".wav")).Where(x => x != null).ToArray();
        var herd = new List<(string model, Transform area, Vector2 half, float speed, AudioClip[] calls, Vector3 start)>();
        for (int i = 0; i < 6; i++) herd.Add((i < 4 ? "Animal_Sheep" : "Animal_Sheep_Brown", area, new Vector2(HX - 1.6f, HZ - 1.6f), 0.55f, C("sheep_1", "sheep_2"), new Vector3(Rand(-6f, 6f), 0f, Rand(-4f, 4f))));
        for (int i = 0; i < 3; i++) herd.Add(("Animal_Goat", area, new Vector2(HX - 1.6f, HZ - 1.6f), 0.7f, C("goat_1"), new Vector3(Rand(-6f, 6f), 0f, Rand(-4f, 4f))));
        for (int i = 0; i < 2; i++) herd.Add(("Animal_Cow", area, new Vector2(HX - 2.2f, HZ - 2.2f), 0.45f, C("cow_1", "cow_2"), new Vector3(Rand(-5f, 5f), 0f, Rand(-3f, 3f))));
        for (int i = 0; i < 2; i++) herd.Add(("Animal_Horse", horseArea, new Vector2(1.6f, 1.2f), 0.5f, C("horse_1", "horse_snort"), new Vector3(HX + 3.5f + (i == 0 ? -1f : 1f), 0f, -2.2f)));
        herd.Add(("Animal_Dog", dogArea, new Vector2(3f, 3f), 1.4f, C("dog_1"), new Vector3(HX + 3f, 0f, 1.5f)));

        var animals = new GameObject("Animals").transform; animals.SetParent(farm, false);
        foreach (var h in herd)
        {
            GameObject a = AssetDatabase.LoadAssetAtPath<GameObject>(F + h.model + ".obj");
            if (a == null) continue;
            var root = new GameObject(h.model.Replace("Animal_", ""));
            root.transform.SetParent(animals, false);
            Vector3 w = farm.TransformPoint(h.start);
            root.transform.position = GroundY(w, out float gy) ? new Vector3(w.x, gy, w.z) : w;
            root.transform.rotation = Quaternion.Euler(0f, Rand(0f, 360f), 0f);
            var m = (GameObject)PrefabUtility.InstantiatePrefab(a, root.transform);
            m.transform.localPosition = Vector3.zero; m.transform.localRotation = Quaternion.identity;
            var aw = root.AddComponent<AnimalWander>();
            aw.area = h.area; aw.halfSize = h.half; aw.walkSpeed = h.speed; aw.calls = h.calls;
            if (h.model == "Animal_Dog") { aw.idleTime = new Vector2(2f, 5f); aw.stepRate = 9f; aw.grazeTilt = 0f; aw.callPause = new Vector2(25f, 60f); }
            if (h.model == "Animal_Horse") { aw.grazeTilt = 12f; aw.callPause = new Vector2(25f, 60f); }
            if (h.model == "Animal_Cow") aw.callPause = new Vector2(20f, 45f);
        }

        farmGate = new GameObject("GatePoint").transform;
        farmGate.SetParent(farm, false);
        farmGate.localPosition = new Vector3(HX + 1.6f, 0f, 2.6f);
        farmGate.localRotation = Quaternion.Euler(0f, 90f, 0f);   // лицом к селу
    }

    private static void FarmPiece(Transform farm, string path, Vector3 local, float yaw, bool collider)
    {
        GameObject a = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (a == null) return;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(a, farm);
        Vector3 w = farm.TransformPoint(local);
        go.transform.position = GroundY(w, out float y) ? new Vector3(w.x, y, w.z) : w;
        go.transform.rotation = farm.rotation * Quaternion.Euler(0f, yaw, 0f);
        if (collider)
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }

    // ================================================================== close button on the speech panel

    private static void AddSpeechCloseButton(Scene scene)
    {
        SpeechPanel sp = Find<SpeechPanel>(scene).FirstOrDefault();
        if (sp == null) return;
        Transform parent = sp.transform;
        Transform old = parent.Find("CloseButton");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var go = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-10f, -10f); rt.sizeDelta = new Vector2(64f, 34f);
        var img = go.GetComponent<Image>();
        img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/UI/Sprites/ui_button.png");
        img.type = img.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        img.color = new Color(0.25f, 0.17f, 0.11f, 0.95f);
        var btn = go.GetComponent<Button>();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, sp.Close);
        var t = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        t.transform.SetParent(go.transform, false);
        var trt = (RectTransform)t.transform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
        var tmp = t.GetComponent<TextMeshProUGUI>();
        tmp.text = "<b>X</b> <size=70%>Esc</size>"; tmp.fontSize = 20; tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.96f, 0.93f, 0.85f); tmp.raycastTarget = false;
        EditorUtility.SetDirty(sp);
    }

    // ================================================================== villagers

    private struct Villager
    {
        public string key, model, nameRu, nameEn, role, roleEn, portrait; public float pitch;
        public string[] ru, en;
    }

    private static readonly Villager[] Villagers =
    {
        new Villager
        {
            key = "Апа", model = "Villager_Apa", nameRu = "апой", nameEn = "Apa", role = "бабушка", roleEn = "grandmother", portrait = "ui_portrait_apa.png", pitch = 0.85f,
            ru = new[]
            {
                "Кел, балам, кош келдиң! Заходи — чай уже готов. У нас гостя всегда встречают чаем и боорсоком.",
                "Видишь шырдак на заборе? Я сшила его сама. Узоры — это рога архара, они берегут дом от беды.",
                "Тандыр во дворе ещё тёплый — утром пекла нан. Лепёшку нельзя класть вверх дном, это неуважение к хлебу.",
                "Вечером в окнах загорается свет — значит, все дома и все сыты. Так и должно быть в айыле."
            },
            en = new[]
            {
                "Кел, балам, кош келдиң! Come in — the tea is ready. A guest is always welcomed with tea and боорсок.",
                "See the шырдак on the fence? I made it myself. The patterns are argali horns — they protect the home.",
                "The тандыр in the yard is still warm — I baked нан this morning. Never put bread upside down, it's disrespectful.",
                "In the evening the windows light up — it means everyone is home and fed. That's how it should be in an айыл."
            }
        },
        new Villager
        {
            key = "Байке", model = "Villager_Baike", nameRu = "байке", nameEn = "Baike", role = "сосед", roleEn = "neighbour", portrait = "ui_portrait_baike.png", pitch = 0.72f,
            ru = new[]
            {
                "Салам, иним! Колю дрова на зиму. В горах зима длинная — без дров никуда.",
                "Видишь беркута в небе? Мой дед был беркутчи — охотился с беркутом на лис зимой.",
                "Ак калпак — гордость кыргыза. Его носят и в жару, и в холод, а снимать и бросать нельзя.",
                "По вечерам мужчины собираются у дома и играют в тогуз коргоол — это наша игра на смекалку."
            },
            en = new[]
            {
                "Салам, иним! I'm chopping wood for the winter. Winters are long in the mountains — you can't live without firewood.",
                "See the eagle in the sky? My grandfather was a беркутчи — he hunted foxes with a golden eagle in winter.",
                "The ак калпак is the pride of a Kyrgyz man. It's worn in heat and cold, and must never be thrown down.",
                "In the evenings the men gather by the house and play тогуз коргоол — our game of wit."
            }
        },
        new Villager
        {
            key = "Чабан", model = "Villager_Chaban", nameRu = "чабаном", nameEn = "the shepherd", role = "пастух", roleEn = "shepherd", portrait = "ui_portrait_chaban.png", pitch = 0.8f,
            ru = new[]
            {
                "Салам! Летом мы поднимаем отары на жайлоо — высокогорные пастбища, там трава сладкая.",
                "На жайлоо живём в юртах всё лето. Утром — кымыз, днём — стадо, ночью — звёзды над головой.",
                "Кымыз делают из кобыльего молока. Летом в горах нет напитка лучше.",
                "Ночью в горах холодно, а звёзды так близко, что кажется — достанешь рукой. Посмотри сам, когда стемнеет."
            },
            en = new[]
            {
                "Салам! In summer we drive the flocks up to the жайлоо — the high mountain pastures, where the grass is sweet.",
                "On the жайлоо we live in yurts all summer. Кымыз in the morning, the herd by day, stars above at night.",
                "Кымыз is made from mare's milk. There's no better drink in the mountains in summer.",
                "Nights in the mountains are cold, and the stars are so close you could touch them. See for yourself after dark."
            }
        },
    };

    private static int BuildVillagers(Scene scene, List<Transform> houses)
    {
        DestroyAll(scene, "Villagers");
        var root = new GameObject("Villagers").transform;

        TMP_Text story = Find<YurtQuestManager>(scene).Select(q => q.storyText).FirstOrDefault(t => t != null)
                         ?? Find<TMP_Text>(scene).FirstOrDefault(t => t.name == "StoryText");
        SpeechPanel speech = Find<SpeechPanel>(scene).FirstOrDefault();

        int[] at = { 0, 1, 5 };   // у каких домов стоят жители (Чабан — у фермы)
        int n = 0;
        for (int i = 0; i < Villagers.Length; i++)
        {
            Villager v = Villagers[i];
            if (houses.Count == 0) break;
            Transform house = houses[Mathf.Min(at[i], houses.Count - 1)];
            Bounds lb = LocalBounds(house.gameObject);
            Vector3 p = house.TransformPoint(new Vector3(1.6f, 0f, lb.max.z + 1.2f));
            if (v.key == "Чабан" && farmGate != null) p = farmGate.position;
            if (!GroundY(p, out float y)) y = house.position.y;

            var npc = new GameObject("NPC_" + v.model.Replace("Villager_", ""));
            npc.transform.SetParent(root, false);
            npc.transform.position = new Vector3(p.x, y, p.z);
            npc.transform.rotation = v.key == "Чабан" && farmGate != null ? farmGate.rotation : house.rotation;
            var cap = npc.AddComponent<CapsuleCollider>(); cap.center = new Vector3(0f, 0.85f, 0f); cap.height = 1.7f; cap.radius = 0.3f;

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(NpcDir + v.model + ".obj");
            if (asset != null)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, npc.transform);
                model.name = v.model;
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                var look = npc.AddComponent<NPCLookAtPlayer>();
                look.model = model.transform;
            }
            var talk = npc.AddComponent<VillagerTalk>();
            talk.speakerKey = v.key; talk.nameRu = v.nameRu; talk.nameEn = v.nameEn;
            talk.linesRu = v.ru; talk.linesEn = v.en; talk.storyText = story;

            if (speech != null)
            {
                var s = speech.speakers.FirstOrDefault(x => x.key == v.key);
                if (s == null) { s = new SpeechPanel.Speaker(); speech.speakers.Add(s); }
                s.key = v.key; s.displayName = v.key; s.role = v.role; s.roleEn = v.roleEn; s.voicePitch = v.pitch;
                s.portrait = AssetDatabase.LoadAssetAtPath<Sprite>(Portraits + v.portrait);
                EditorUtility.SetDirty(speech);
            }
            n++;
        }
        return n;
    }

    // ================================================================== day & night

    private static void BuildDayNight(Scene scene)
    {
        DestroyAll(scene, "DayNight");
        var go = new GameObject("DayNight");
        var dn = go.AddComponent<DayNightCycle>();
        dn.SetDefaults();
        dn.hour = 9.5f;
        dn.sun = Find<Light>(scene).FirstOrDefault(l => l.type == LightType.Directional && l.name != "Moon Light");
        dn.skybox = RenderSettings.skybox;

        var moonGo = new GameObject("Moon Light");
        moonGo.transform.SetParent(go.transform, false);
        var moon = moonGo.AddComponent<Light>();
        moon.type = LightType.Directional; moon.color = new Color(0.62f, 0.72f, 1f); moon.intensity = 0f;
        moon.shadows = LightShadows.Soft; moon.shadowStrength = 0.6f; moon.enabled = false;
        dn.moon = moon;

        // Звёзды — купол из маленьких светящихся точек
        var starsGo = new GameObject("Stars");
        starsGo.transform.SetParent(go.transform, false);
        starsGo.AddComponent<MeshFilter>().sharedMesh = StarsMesh();
        var sr = starsGo.AddComponent<MeshRenderer>();
        Material sm = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "Stars.mat");
        if (sm == null) { sm = new Material(Shader.Find("MURAS/Stars")); AssetDatabase.CreateAsset(sm, MatDir + "Stars.mat"); }
        sr.sharedMaterial = sm; sr.shadowCastingMode = ShadowCastingMode.Off; sr.receiveShadows = false;
        sr.enabled = false;   // днём выключены, ночью их проявляет DayNightCycle
        dn.stars = sr;

        // Часы на экране
        Transform canvas = FindT(scene, "GameCanvas");
        if (canvas != null)
        {
            Transform old = canvas.Find("Clock");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var panel = new GameObject("Clock", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas, false);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -16f); rt.sizeDelta = new Vector2(300f, 46f);
            var img = panel.GetComponent<Image>();
            img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/UI/Sprites/ui_card.png");
            img.type = img.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = new Color(0.10f, 0.08f, 0.06f, 0.78f); img.raycastTarget = false;
            var tgo = new GameObject("Time", typeof(RectTransform), typeof(TextMeshProUGUI));
            tgo.transform.SetParent(panel.transform, false);
            var trt = (RectTransform)tgo.transform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            var tmp = tgo.GetComponent<TextMeshProUGUI>();
            tmp.text = "09:30"; tmp.fontSize = 22; tmp.color = new Color(0.96f, 0.93f, 0.85f);
            tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
            dn.clockText = tmp;
        }

        // Звуки ночи: сверчки и совы; кузнечики — только днём
        foreach (AmbientLoop al in Find<AmbientLoop>(scene).Where(a => a.name == "MeadowInsects")) { al.playAt = AmbientLoop.TimeOfDay.DayOnly; EditorUtility.SetDirty(al); }
        DestroyAll(scene, "NightCrickets");
        var cr = new GameObject("NightCrickets");
        var cs = cr.AddComponent<AudioSource>();
        cs.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "night_crickets.ogg"); cs.loop = true; cs.playOnAwake = false; cs.spatialBlend = 0f;
        var cl = cr.AddComponent<AmbientLoop>(); cl.volume = 0.22f; cl.playAt = AmbientLoop.TimeOfDay.NightOnly;

        Transform birds = FindT(scene, "Birds");
        if (birds != null)
        {
            foreach (Transform t in birds.Cast<Transform>().Where(t => t.name.StartsWith("Owl")).ToList()) Object.DestroyImmediate(t.gameObject);
            var owlClips = new[] { "owl_1.wav", "owl_2.wav" }.Select(c => AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + c)).Where(c => c != null).ToArray();
            var perches = birds.Cast<Transform>().Where(t => t.name.StartsWith("Bird_")).Take(3).ToList();
            int k = 0;
            foreach (Transform p in perches)
            {
                var owl = new GameObject("Owl_" + (++k));
                owl.transform.SetParent(birds, false);
                owl.transform.position = p.position + Vector3.up * 0.5f;
                var src = owl.AddComponent<AudioSource>();
                src.spatialBlend = 1f; src.rolloffMode = AudioRolloffMode.Logarithmic; src.minDistance = 5f; src.maxDistance = 60f;
                var bs = owl.AddComponent<BirdSinger>();
                bs.songs = owlClips; bs.nightOnly = true; bs.pause = new Vector2(18f, 45f); bs.volume = 0.6f; bs.hearDistance = 70f;
            }
        }
    }

    private static Mesh StarsMesh()
    {
        string path = GenDir + "StarsDome.asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null) return old;
        var r = new System.Random(5);
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var c = new List<Color>(); var tri = new List<int>();
        const float R = 420f;
        for (int i = 0; i < 1400; i++)
        {
            // равномерно по верхней полусфере
            float u = (float)r.NextDouble(), w = (float)r.NextDouble();
            float y = Mathf.Lerp(0.06f, 1f, u);
            float ang = w * Mathf.PI * 2f, rr = Mathf.Sqrt(1f - y * y);
            Vector3 dir = new Vector3(Mathf.Cos(ang) * rr, y, Mathf.Sin(ang) * rr);
            Vector3 p = dir * R;
            Vector3 right = Vector3.Cross(dir, Vector3.up).normalized; if (right == Vector3.zero) right = Vector3.right;
            Vector3 up = Vector3.Cross(right, dir).normalized;
            float s = (float)(0.7 + r.NextDouble() * r.NextDouble() * 2.2);
            int b = v.Count;
            v.Add(p - right * s - up * s); v.Add(p + right * s - up * s); v.Add(p + right * s + up * s); v.Add(p - right * s + up * s);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
            float br = (float)(0.55 + r.NextDouble() * 0.45);
            float tint = (float)r.NextDouble();
            var col = new Color(br * (0.85f + 0.15f * tint), br * 0.92f, br * (1.05f - 0.15f * tint), (float)r.NextDouble());
            for (int k = 0; k < 4; k++) c.Add(col);
            tri.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
        }
        var m = new Mesh { name = "StarsDome" };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetColors(c); m.SetTriangles(tri, 0);
        m.bounds = new Bounds(Vector3.zero, Vector3.one * R * 2.2f);
        if (!AssetDatabase.IsValidFolder("Assets/Game/Models/Generated")) AssetDatabase.CreateFolder("Assets/Game/Models", "Generated");
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // ================================================================== helpers

    private static Bounds LocalBounds(GameObject go)
    {
        var b = new Bounds(); bool first = true;
        foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            Matrix4x4 m = go.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            Bounds mb = mf.sharedMesh.bounds;
            foreach (Vector3 corner in new[] { new Vector3(-1, -1, -1), new Vector3(1, -1, -1), new Vector3(1, 1, -1), new Vector3(-1, 1, -1), new Vector3(-1, -1, 1), new Vector3(1, -1, 1), new Vector3(1, 1, 1), new Vector3(-1, 1, 1) })
            {
                Vector3 p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, corner));
                if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
            }
        }
        return b;
    }

    private static bool GroundY(Vector3 p, out float y)
    {
        y = 0f; bool ok = false;
        foreach (RaycastHit h in Physics.RaycastAll(new Vector3(p.x, p.y + 80f, p.z), Vector3.down, 250f, ~0, QueryTriggerInteraction.Ignore))
            if (IsGround(h.collider) && (!ok || h.point.y > y)) { y = h.point.y; ok = true; }
        return ok;
    }

    private static bool IsGround(Collider c) =>
        (valley != null ? c.transform.IsChildOf(valley) : c.name.Contains("Ground")) || c.name.Contains("PP_Meadow_0") || c.name.Contains("Lake_Ground");
    private static bool IsPath(Collider c) => c.name.Contains("Road") || c.name.Contains("Meadow_Path") || c.name.Contains("Floor_Tile") || c.name.StartsWith("River");

    private static IEnumerable<T> Find<T>(Scene s) where T : Component => s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));
    private static Transform FindT(Scene s, string name) => s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == name);
    private static void DestroyAll(Scene s, string name)
    {
        foreach (Transform t in s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Where(t => t.name == name).ToList())
            if (t != null) Object.DestroyImmediate(t.gameObject);
    }
    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    private static float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
}
