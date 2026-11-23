using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Оживляет село: шаги и звук взаимодействия, птицы на деревьях, беркут в небе, кузнечики;
/// реалистичная вода в речке с течением и камнями; солнечный полдень; трава, цветы, грибы и камушки вдоль тропинок.
/// Tools > Diploma > Polish World — всё сразу (можно запускать повторно), или по отдельности пунктами ниже.
/// </summary>
public static class WorldPolish
{
    private const string ScenePath = "Assets/Game/Scenes/Village.unity";
    private const string Sfx = "Assets/Game/Audio/SFX/";
    private const string Trail = "Assets/Game/Models/Trail/";
    private const string PP = "Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/";
    private const string MatDir = "Assets/Game/Materials/";
    private const string TexDir = "Assets/Game/Textures/";

    private static System.Random rng;

    [MenuItem("Tools/Diploma/Polish World (sounds, river, noon, trail)")]
    public static void All() => Run(true, true, true, true);
    [MenuItem("Tools/Diploma/Polish World: Sounds only")]
    public static void SoundsOnly() => Run(true, false, false, false);
    [MenuItem("Tools/Diploma/Polish World: River only")]
    public static void RiverOnly() => Run(false, true, false, false);
    [MenuItem("Tools/Diploma/Polish World: Noon lighting only")]
    public static void LightOnly() => Run(false, false, true, false);
    [MenuItem("Tools/Diploma/Polish World: Trail decor only")]
    public static void TrailOnly() => Run(false, false, false, true);

    private static void Run(bool sounds, bool river, bool light, bool trail)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        rng = new System.Random(2026);
        PrepareAssets();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Physics.SyncTransforms();

        var log = new List<string>();
        if (sounds) log.Add(Sounds(scene));
        if (river) log.Add(River(scene));
        if (light) log.Add(Noon(scene));
        if (trail) log.Add(TrailDecor(scene));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Polish] " + string.Join(" · ", log) + ". Village сохранена.");
    }

    // ================================================================== assets

    private static void PrepareAssets()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Sfx.TrimEnd('/') }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(p) is AudioImporter ai)) continue;
            var s = ai.defaultSampleSettings;
            bool loop = p.Contains("meadow");
            s.loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = loop ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
            ai.defaultSampleSettings = s;
            ai.forceToMono = !loop;
            ai.SaveAndReimport();
        }
        if (AssetImporter.GetAtPath(TexDir + "river_normal.png") is TextureImporter n && n.textureType != TextureImporterType.NormalMap)
        {
            n.textureType = TextureImporterType.NormalMap;
            n.wrapMode = TextureWrapMode.Repeat;
            n.SaveAndReimport();
        }
        if (AssetImporter.GetAtPath(TexDir + "river_foam.png") is TextureImporter f && f.sRGBTexture)
        {
            f.sRGBTexture = false;
            f.wrapMode = TextureWrapMode.Repeat;
            f.SaveAndReimport();
        }
    }

    // ================================================================== sounds

    private static string Sounds(Scene scene)
    {
        AudioClip[] Clips(string prefix) => Enumerable.Range(1, 6).Select(i => AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + prefix + i + ".wav")).Where(c => c != null).ToArray();

        // Шаги
        var player = Find<PlayerMovement>(scene).FirstOrDefault();
        if (player != null)
        {
            var fs = GetOrAdd<PlayerFootsteps>(player.gameObject);
            fs.grass = Clips("step_grass_");
            fs.dirt = Clips("step_dirt_");
            fs.stone = Clips("step_stone_");
            fs.wood = Clips("step_wood_");
            fs.land = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "land.wav");
            EditorUtility.SetDirty(fs);
        }

        // Звук взаимодействия
        var ga = Find<GameAudio>(scene).FirstOrDefault();
        if (ga != null) { ga.interact = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "interact.wav"); EditorUtility.SetDirty(ga); }

        // Птицы на деревьях
        DestroyRoot(scene, "Birds");
        var birdsRoot = new GameObject("Birds");
        AudioClip[] songs = Clips("bird_");
        var trees = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .Where(t => t.gameObject.activeInHierarchy && IsTree(t)
                        && t.GetComponentInChildren<Renderer>() != null && Flat(t.position).magnitude < 95f)
            .OrderBy(_ => rng.Next()).Take(24).ToList();
        int birds = 0;
        foreach (Transform t in trees)
        {
            Bounds b = GetBounds(t.gameObject);
            var go = new GameObject("Bird_" + (++birds));
            go.transform.SetParent(birdsRoot.transform, false);
            go.transform.position = new Vector3(b.center.x, b.min.y + b.size.y * 0.8f, b.center.z);
            var src = go.AddComponent<AudioSource>();
            src.spatialBlend = 1f; src.rolloffMode = AudioRolloffMode.Logarithmic; src.minDistance = 3f; src.maxDistance = 38f; src.spread = 60f;
            var bs = go.AddComponent<BirdSinger>();
            bs.songs = songs.OrderBy(_ => rng.Next()).Take(3).ToArray();
            bs.volume = 0.5f;
            bs.pause = new Vector2(4f, 14f);
        }

        // Беркут в небе над долиной
        var eagle = new GameObject("Eagle_Sky");
        eagle.transform.SetParent(birdsRoot.transform, false);
        var es = eagle.AddComponent<AudioSource>();
        es.spatialBlend = 1f; es.rolloffMode = AudioRolloffMode.Linear; es.minDistance = 30f; es.maxDistance = 220f; es.spread = 90f;
        var eb = eagle.AddComponent<BirdSinger>();
        eb.songs = Clips("eagle_");
        eb.flyAroundPlayer = true;
        eb.pause = new Vector2(35f, 80f);
        eb.pitch = new Vector2(0.95f, 1.05f);
        eb.volume = 0.75f;

        // Кузнечики на лугу (тихий фон)
        DestroyRoot(scene, "MeadowInsects");
        var ins = new GameObject("MeadowInsects");
        var isrc = ins.AddComponent<AudioSource>();
        isrc.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "meadow_insects.ogg");
        isrc.spatialBlend = 0f; isrc.loop = true; isrc.playOnAwake = false;
        var il = ins.AddComponent<AmbientLoop>();
        il.volume = 0.16f;

        return $"звуки: шаги {(player != null ? "да" : "нет")}, птиц {birds}, беркут, кузнечики";
    }

    // ================================================================== river

    private static string River(Scene scene)
    {
        Transform river = FindT(scene, "River");
        Transform waterT = FindT(scene, "RiverWater");
        if (river == null || waterT == null) return "река не найдена";

        // Меш речки построен в мировых координатах, а папка Environment сдвинута — из-за этого вода
        // уезжала на ~8 м и пряталась под склон. Ставим речку в ноль мира.
        river.position = Vector3.zero;
        river.rotation = Quaternion.identity;
        river.localScale = Vector3.one;
        Physics.SyncTransforms();

        var shader = Shader.Find("MURAS/RiverWater");
        if (shader == null) return "нет шейдера MURAS/RiverWater";
        Material water = LoadOrCreate(MatDir + "RiverWater_Flow.mat", shader);
        water.SetTexture("_NormalTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "river_normal.png"));
        water.SetTexture("_FoamTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "river_foam.png"));
        water.SetFloat("_FoamAmount", 0.14f);
        water.SetFloat("_FoamEdge", 0.08f);
        water.SetFloat("_NormalStrength", 0.7f);
        water.SetColor("_ShallowColor", new Color(0.33f, 0.55f, 0.52f, 0.6f));
        EditorUtility.SetDirty(water);
        var mr = waterT.GetComponent<MeshRenderer>();
        mr.sharedMaterial = water;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = true;

        Transform bedT = FindT(scene, "RiverBed");
        if (bedT != null && bedT.TryGetComponent(out MeshRenderer bed))
        {
            Material bm = LoadOrCreate(MatDir + "RiverBed_Wet.mat", Shader.Find("Universal Render Pipeline/Lit"));
            bm.SetColor("_BaseColor", new Color(0.34f, 0.37f, 0.27f));   // илистый берег, в тон траве
            bm.SetFloat("_Smoothness", 0.25f);
            bm.SetFloat("_Cull", 0f);   // двусторонний: у ленты дна нормали смотрят вниз
            bm.doubleSidedGI = true;
            EditorUtility.SetDirty(bm);
            bed.sharedMaterial = bm;
            bed.enabled = false;   // полоса дна шире воды и выглядела как бетонный жёлоб — край воды теперь сразу у травы
        }

        // Камни в русле и галька на дне
        Transform old = river.Find("RiverRocks");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var rocks = new GameObject("RiverRocks").transform;
        rocks.SetParent(river, false);
        Mesh m = waterT.GetComponent<MeshFilter>().sharedMesh;
        Vector3[] v = m.vertices;
        int count = 0;
        for (int i = 2; i + 1 < v.Length; i += 2)
        {
            Vector3 l = waterT.TransformPoint(v[i]), r = waterT.TransformPoint(v[i + 1]);
            Vector3 c = (l + r) * 0.5f, side = (r - l);
            float roll = (float)rng.NextDouble();
            if (roll < 0.22f)   // валун, торчащий из воды
            {
                Vector3 p = c + side * Rand(-0.32f, 0.32f);
                p.y = c.y - 0.25f;
                count += Place(rocks, PP + (rng.Next(2) == 0 ? "PP_Rock_Moss_Grown_09" : "PP_Rock_Moss_Grown_11") + ".prefab", p, Rand(0.3f, 0.55f), false) ? 1 : 0;
            }
            else if (roll < 0.62f)   // галька на дне
            {
                Vector3 p = c + side * Rand(-0.4f, 0.4f);
                p.y = c.y - 0.12f;
                count += Place(rocks, PP + (rng.Next(2) == 0 ? "PP_Cemetery_Pebbles_03" : "PP_Cemetery_Pebbles_09") + ".prefab", p, Rand(0.8f, 1.3f), false) ? 1 : 0;
            }
        }
        return $"речка: новая вода с течением, камней {count}";
    }

    // ================================================================== noon

    private static string Noon(Scene scene)
    {
        Light sun = Find<Light>(scene).FirstOrDefault(l => l.type == LightType.Directional);
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(63f, -35f, 0f);
            sun.useColorTemperature = true;
            sun.colorTemperature = 6100f;
            sun.color = Color.white;
            sun.intensity = 1.55f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
            EditorUtility.SetDirty(sun);
        }

        Material sky = LoadOrCreate(MatDir + "Sky_Noon.mat", Shader.Find("Skybox/Procedural"));
        sky.SetFloat("_SunDisk", 2f);
        sky.SetFloat("_SunSize", 0.035f);
        sky.SetFloat("_SunSizeConvergence", 6f);
        sky.SetFloat("_AtmosphereThickness", 0.72f);
        sky.SetColor("_SkyTint", new Color(0.46f, 0.6f, 0.86f));
        sky.SetColor("_GroundColor", new Color(0.46f, 0.48f, 0.43f));
        sky.SetFloat("_Exposure", 1.2f);
        EditorUtility.SetDirty(sky);

        RenderSettings.skybox = sky;
        if (sun != null) RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.73f, 0.88f);
        RenderSettings.ambientEquatorColor = new Color(0.56f, 0.62f, 0.58f);
        RenderSettings.ambientGroundColor = new Color(0.36f, 0.34f, 0.29f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.73f, 0.82f, 0.93f);
        RenderSettings.fogStartDistance = 70f;
        RenderSettings.fogEndDistance = 480f;
        DynamicGI.UpdateEnvironment();

        // Пост-обработка: мягкий тонмаппинг, немного цвета и контраста, лёгкое свечение солнца
        Volume vol = Find<Volume>(scene).FirstOrDefault(x => x.isGlobal);
        if (vol == null) vol = new GameObject("Global Volume").AddComponent<Volume>();
        if (vol.sharedProfile == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Game/Settings")) AssetDatabase.CreateFolder("Assets/Game", "Settings");
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(prof, "Assets/Game/Settings/Village_Volume.asset");
            vol.sharedProfile = prof;
        }
        VolumeProfile p = vol.sharedProfile;
        var tm = Get<Tonemapping>(p); tm.mode.Override(TonemappingMode.Neutral);
        var bloom = Get<Bloom>(p); bloom.threshold.Override(1.1f); bloom.intensity.Override(0.25f); bloom.scatter.Override(0.6f);
        var ca = Get<ColorAdjustments>(p); ca.postExposure.Override(0.1f); ca.contrast.Override(10f); ca.saturation.Override(12f);
        var vg = Get<Vignette>(p); vg.intensity.Override(0.18f); vg.smoothness.Override(0.4f);
        var wb = Get<WhiteBalance>(p); wb.temperature.Override(-4f);
        EditorUtility.SetDirty(p);

        foreach (Camera cam in Find<Camera>(scene))
        {
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) { data.renderPostProcessing = true; EditorUtility.SetDirty(data); }
        }
        return "полдень: солнце высоко, небо, тени, пост-обработка";
    }

    // ================================================================== trail decor

    private static readonly (string path, float w, Vector2 scale, bool shadows, bool edgeOnly)[] TrailItems =
    {
        (Trail + "Trail_GrassTuft_Tall.obj", 22, new Vector2(1.0f, 1.6f), false, false),
        (Trail + "Trail_GrassTuft_Short.obj", 22, new Vector2(1.0f, 1.7f), false, false),
        (PP + "PP_Grass_11.prefab", 6, new Vector2(0.6f, 1.0f), false, false),
        (PP + "PP_Grass_15.prefab", 6, new Vector2(0.6f, 1.0f), false, false),
        (Trail + "Trail_Stones_A.obj", 8, new Vector2(1.0f, 2.0f), true, false),
        (Trail + "Trail_Stones_B.obj", 8, new Vector2(1.0f, 2.0f), true, false),
        (Trail + "Trail_EdgeStone.obj", 7, new Vector2(1.0f, 1.8f), true, true),
        (Trail + "Trail_Edelweiss.obj", 5, new Vector2(1.4f, 2.0f), false, false),
        (Trail + "Trail_Bellflower.obj", 6, new Vector2(1.4f, 2.1f), false, false),
        (PP + "PP_Daffodil_03.prefab", 2, new Vector2(0.6f, 0.9f), false, false),
        (PP + "PP_Hyacinth_04.prefab", 2, new Vector2(0.6f, 0.9f), false, false),
        (Trail + "Trail_Mushroom_Brown.obj", 3, new Vector2(1.4f, 2.2f), true, false),
        (Trail + "Trail_Mushroom_Red.obj", 2, new Vector2(1.4f, 2.0f), true, false),
    };

    private static HashSet<Collider> pathColliders;
    private static Transform valley;

    private static string TrailDecor(Scene scene)
    {
        Transform env = FindT(scene, "Environment");
        valley = FindT(scene, "MountainValley");
        if (env == null) return "нет Environment";
        Transform old = env.Find("Decor_Trail");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject("Decor_Trail").transform;
        root.SetParent(env, false);

        // Тропинки и плитки + временные коллайдеры для проверки «на тропе / рядом»
        var pathRenderers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Renderer>())
            .Where(r => IsPathName(r.name) || (r.transform.parent != null && IsPathName(r.transform.parent.name))).ToList();
        pathColliders = new HashSet<Collider>();
        var temp = new List<Collider>();
        foreach (Renderer r in pathRenderers)
        {
            if (r.TryGetComponent(out Collider c)) { pathColliders.Add(c); continue; }
            if (r.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null)
            {
                var mc = r.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                pathColliders.Add(mc); temp.Add(mc);
            }
        }
        Physics.SyncTransforms();

        float total = TrailItems.Sum(x => x.w);
        var used = new HashSet<long>();
        int placed = 0;
        const float step = 0.55f;
        foreach (Renderer r in pathRenderers)
        {
            Bounds b = r.bounds;
            b.Expand(new Vector3(4f, 0f, 4f));
            for (float x = b.min.x; x <= b.max.x; x += step)
                for (float z = b.min.z; z <= b.max.z; z += step)
                {
                    long key = ((long)Mathf.RoundToInt(x / step) << 32) ^ (uint)Mathf.RoundToInt(z / step);
                    if (!used.Add(key) || placed > 1100) continue;
                    var p = new Vector3(x + Rand(-0.22f, 0.22f), b.center.y, z + Rand(-0.22f, 0.22f));
                    bool on = OnPath(p);
                    if (on)
                    {
                        if (rng.NextDouble() < 0.035 && Spawn(root, Trail + "Trail_Stones_A.obj", p, Rand(0.6f, 0.9f), true, -0.02f)) placed++;
                        continue;
                    }
                    float d = NearPath(p);
                    if (d < 0f) continue;
                    double chance = d < 1f ? 0.5 : 0.24;
                    if (rng.NextDouble() > chance) continue;

                    var item = PickItem(total, d < 1f);
                    if (Spawn(root, item.path, p, Rand(item.scale.x, item.scale.y), item.shadows, 0f)) placed++;
                }
        }

        // Грибы у деревьев
        int mush = 0;
        foreach (Transform t in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>())
                     .Where(t => IsTree(t) && Flat(t.position).magnitude < 80f))
        {
            if (rng.NextDouble() > 0.45) continue;
            int k = rng.Next(1, 3);
            for (int i = 0; i < k; i++)
            {
                float a = Rand(0f, Mathf.PI * 2f), dist = Rand(0.7f, 1.6f);
                Vector3 p = t.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * dist;
                string m = rng.NextDouble() < 0.6 ? "Trail_Mushroom_Brown.obj" : "Trail_Mushroom_Red.obj";
                if (Spawn(root, Trail + m, p, Rand(1.4f, 2.1f), true, 0f)) mush++;
            }
        }

        foreach (Collider c in temp) Object.DestroyImmediate(c);
        return $"тропинки: украшений {placed}, грибов у деревьев {mush}";
    }

    // Отдельное дерево (а не папка «Trees»)
    private static bool IsTree(Transform t) =>
        t.name.Contains("Tree") && (t.parent == null || !t.parent.name.Contains("Tree") || t.parent.name.StartsWith("Trees")) &&
        (PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) || t.GetComponent<Renderer>() != null);

    private static bool IsPathName(string n) => n.Contains("Road") || n.Contains("Meadow_Path") || n.Contains("Floor_Tile");

    private static (string path, float w, Vector2 scale, bool shadows, bool edgeOnly) PickItem(float total, bool atEdge)
    {
        for (int tries = 0; tries < 10; tries++)
        {
            double r = rng.NextDouble() * total;
            foreach (var it in TrailItems)
            {
                r -= it.w;
                if (r <= 0) { if (it.edgeOnly && !atEdge) break; return it; }
            }
        }
        return TrailItems[0];
    }

    private static bool OnPath(Vector3 p)
    {
        foreach (RaycastHit h in Physics.RaycastAll(p + Vector3.up * 20f, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore))
            if (pathColliders.Contains(h.collider)) return true;
        return false;
    }

    private static float NearPath(Vector3 p)
    {
        foreach (float d in new[] { 0.9f, 1.8f })
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f;
                if (OnPath(p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * d)) return d;
            }
        return -1f;
    }

    private static bool Spawn(Transform parent, string path, Vector3 p, float scale, bool shadows, float sink)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) return false;
        if (!GroundY(p, out float y)) return false;
        Vector3 at = new Vector3(p.x, y + sink, p.z);
        if (Blocked(at)) return false;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.name = asset.name.StartsWith("Trail") ? asset.name : "Trail_" + asset.name;
        go.transform.position = at;
        go.transform.rotation = Quaternion.Euler(0f, Rand(0f, 360f), 0f);
        go.transform.localScale = Vector3.one * scale;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        foreach (Collider c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);   // не мешают ходить
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return true;
    }

    private static bool Place(Transform parent, string path, Vector3 at, float scale, bool keepCollider)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) return false;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.transform.position = at;
        go.transform.rotation = Quaternion.Euler(Rand(-8f, 8f), Rand(0f, 360f), Rand(-8f, 8f));
        go.transform.localScale = Vector3.one * scale;
        if (!keepCollider) foreach (Collider c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        return true;
    }

    private static bool GroundY(Vector3 p, out float y)
    {
        y = 0f;
        bool ok = false;
        foreach (RaycastHit h in Physics.RaycastAll(new Vector3(p.x, p.y + 60f, p.z), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore))
        {
            bool ground = valley != null ? h.collider.transform.IsChildOf(valley) : h.collider.name.Contains("Ground");
            if (ground && (!ok || h.point.y > y)) { y = h.point.y; ok = true; }
        }
        return ok;
    }

    private static bool Blocked(Vector3 at)
    {
        foreach (Collider c in Physics.OverlapSphere(at + Vector3.up * 0.45f, 0.35f, ~0, QueryTriggerInteraction.Collide))
        {
            if (pathColliders != null && pathColliders.Contains(c)) continue;
            if (valley != null && c.transform.IsChildOf(valley)) continue;
            if (c.name.Contains("Ground") || c.name.Contains("River")) continue;
            return true;
        }
        return false;
    }

    // ================================================================== helpers

    private static T Get<T>(VolumeProfile p) where T : VolumeComponent
    {
        if (!p.TryGet(out T c)) c = p.Add<T>(true);
        c.active = true;
        return c;
    }

    private static Material LoadOrCreate(string path, Shader shader)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Game/Materials")) AssetDatabase.CreateFolder("Assets/Game", "Materials");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        else if (m.shader != shader) m.shader = shader;
        return m;
    }

    private static IEnumerable<T> Find<T>(Scene s) where T : Component =>
        s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));

    private static Transform FindT(Scene s, string name) =>
        s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == name);

    private static void DestroyRoot(Scene s, string name)
    {
        foreach (GameObject g in s.GetRootGameObjects().Where(g => g.name == name).ToList()) Object.DestroyImmediate(g);
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component => go.TryGetComponent(out T c) ? c : go.AddComponent<T>();

    private static Bounds GetBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    private static float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
}
