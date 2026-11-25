using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// «Fun Pack»: ставит в село комузчу Нурлана (ритм-игра), место работы Апы (пазл «Шырдак»),
/// трассу ат чабыш с чабандесом Эрмеком, мостик для рыбалки, бүркүтчү Болота с беркутом,
/// погоду и зоны словаря. Места ищутся автоматически (ровная свободная земля).
/// Запуск: Tools > Diploma > Install Fun Pack. Можно запускать повторно — старое удаляется.
/// </summary>
public static class FunInstaller
{
    private const string ScenePath = "Assets/Game/Scenes/Village.unity";
    private const string M = "Assets/Game/Models/Fun/";
    private const string Farm = "Assets/Game/Models/Farm/";
    private const string Sfx = "Assets/Game/Audio/SFX/";
    private const string Sprites = "Assets/Game/UI/Sprites/";
    private const string Gen = "Assets/Game/Models/Generated/";
    private const string MatDir = "Assets/Game/Materials/";

    private static Transform valley;
    private static readonly List<string> log = new List<string>();

    [MenuItem("Tools/Diploma/Install Fun Pack (komuz, shyrdak, race, fishing, eagle, weather, dictionary)")]
    public static void Install()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previous = SceneManager.GetActiveScene().path;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        log.Clear();

        PrepareImports();
        foreach (Transform t in All(scene).Where(t => t.name == "FunPack").ToList()) if (t != null) Object.DestroyImmediate(t.gameObject);
        foreach (Transform t in All(scene).Where(t => t.name == "Weather" && t.GetComponent<WeatherSystem>() != null).ToList()) if (t != null) Object.DestroyImmediate(t.gameObject);
        valley = All(scene).FirstOrDefault(t => t.name == "MountainValley");
        Physics.SyncTransforms();

        var root = new GameObject("FunPack").transform;
        Transform env = All(scene).FirstOrDefault(t => t.name == "Environment");
        if (env != null) root.SetParent(env, true);
        root.position = Vector3.zero;

        GameObject player = All(scene).Select(t => t.GetComponent<PlayerMovement>()).Where(p => p != null).Select(p => p.gameObject).FirstOrDefault();
        Vector3 spawn = player != null ? player.transform.position : Vector3.zero;
        TMP_Text story = All(scene).Select(t => t.GetComponent<VillagerTalk>()).Where(v => v != null && v.storyText != null).Select(v => v.storyText).FirstOrDefault()
                         ?? All(scene).Select(t => t.GetComponent<TMP_Text>()).FirstOrDefault(t => t != null && t.name == "StoryText");

        Komuzchu(scene, root, spawn);
        Shyrdak(scene, root);
        Race(scene, root, spawn);
        Fishing(scene, root, spawn);
        Eagle(scene, root, spawn, story);
        Weather(scene);
        Words(scene, root, spawn);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[FunPack] Готово:\n• " + string.Join("\n• ", log) + "\nКлавиши: J — словарь, E — у NPC. Для проверки: Tools > Diploma > Play Mode: Fun …");
        if (!string.IsNullOrEmpty(previous) && previous != ScenePath) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
    }

    [MenuItem("Tools/Diploma/Rebuild Race Track (at chabysh)")]
    public static void RebuildRace()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        log.Clear();
        valley = All(scene).FirstOrDefault(t => t.name == "MountainValley");
        Transform root = All(scene).FirstOrDefault(t => t.name == "FunPack");
        if (root == null) { Debug.LogError("[FunPack] Сначала запусти Install Fun Pack."); return; }
        foreach (Transform t in All(scene).Where(t => t.name == "RaceTrack").ToList()) if (t != null) Object.DestroyImmediate(t.gameObject);
        Physics.SyncTransforms();
        GameObject player = All(scene).Select(t => t.GetComponent<PlayerMovement>()).Where(p => p != null).Select(p => p.gameObject).FirstOrDefault();
        Race(scene, root, player != null ? player.transform.position : Vector3.zero);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[FunPack] " + string.Join("; ", log));
    }

    // ================================================================== Комузчу

    static void Komuzchu(Scene scene, Transform root, Vector3 spawn)
    {
        Transform yurt = All(scene).FirstOrDefault(t => t.name == "Zone_Yurt") ?? All(scene).FirstOrDefault(t => t.name.Contains("Yurt") && t.GetComponentInChildren<Renderer>() != null);
        Vector3 c = yurt != null ? yurt.position : spawn;
        if (!FindSpot(c, 6f, 14f, 1.0f, out Vector3 p)) { p = spawn + Vector3.right * 4f; log.Add("комузчу: место у юрты не найдено, поставлен у старта"); }
        var npc = Npc(root, "NPC_Komuzchu", M + "NPC_Komuzchu.obj", p, Face(p, Vector3.Lerp(c, spawn, 0.5f)));
        npc.AddComponent<KomuzMusician>();
        log.Add($"комузчу Нурлан ({p.x:0}, {p.z:0})");
    }

    // ================================================================== Шырдак у Апы

    static void Shyrdak(Scene scene, Transform root)
    {
        Transform apa = All(scene).FirstOrDefault(t => t.name == "NPC_Apa");
        Vector3 p; Quaternion r;
        if (apa != null)
        {
            p = apa.position + apa.right * 2.3f + apa.forward * 0.6f; r = apa.rotation;
            if (!Free(p, 1.2f, 0.4f, out _) && FindSpot(apa.position, 1.8f, 4.5f, 1.1f, out Vector3 q)) p = q;
        }
        else { FindSpot(Vector3.zero, 5f, 20f, 1.2f, out p); r = Quaternion.identity; log.Add("шырдак: Апа не найдена"); }
        GroundY(p, out float gy); p.y = gy;
        var go = Instance(M + "Shyrdak_Workplace.obj", root, p, r);
        if (go != null) { go.name = "Shyrdak_Workplace"; go.AddComponent<ShyrdakStation>(); }
        log.Add($"шырдак у Апы ({p.x:0}, {p.z:0})");
    }

    // ================================================================== Ат чабыш

    static void Race(Scene scene, Transform root, Vector3 spawn)
    {
        const int N = 12;
        BuildRiver(scene);
        var radii = new[] { new Vector2(32f, 17f), new Vector2(28f, 15f), new Vector2(24f, 14f), new Vector2(20f, 12f) };
        bool found = false; Vector3 center = Vector3.zero; Vector2 rad = radii[0]; float angle = 0f; float bestScore = float.MaxValue;
        foreach (Vector2 rr in radii)
        {
            for (float x = -150f; x <= 150f; x += 8f)
                for (float z = -150f; z <= 150f; z += 8f)
                {
                    float dist = Vector2.Distance(new Vector2(x, z), new Vector2(spawn.x, spawn.z));
                    if (dist < rr.x + 12f || dist > 120f) continue;
                    float score = dist + (radii[0].x - rr.x) * 3f;
                    if (score >= bestScore) continue;
                    for (float ang = 0f; ang < 180f; ang += 30f)
                    {
                        if (!TrackOk(new Vector3(x, spawn.y, z), rr, ang, N, spawn.y)) continue;
                        bestScore = score; center = new Vector3(x, spawn.y, z); rad = rr; angle = ang; found = true;
                        break;
                    }
                }
            if (found) break;
        }
        if (!found) { log.Add("ат чабыш: ровное место для трассы не найдено — трасса не поставлена"); return; }

        var track = new GameObject("RaceTrack").transform;
        track.SetParent(root, true);
        GroundY(center, out float cy); track.position = new Vector3(center.x, cy, center.z);
        var rm = track.gameObject.AddComponent<RaceManager>();
        rm.gateRadius = 8.5f;
        var gates = new List<Transform>();
        Quaternion rot = Quaternion.Euler(0f, angle, 0f);
        for (int i = 0; i < N; i++)
        {
            float a = 2f * Mathf.PI * i / N;
            Vector3 p = center + rot * new Vector3(rad.x * Mathf.Cos(a), 0f, rad.y * Mathf.Sin(a));
            GroundY(p, out float y); p.y = y;
            Vector3 tan = (rot * new Vector3(-rad.x * Mathf.Sin(a), 0f, rad.y * Mathf.Cos(a))).normalized;
            var g = new GameObject("Gate_" + i).transform;
            g.SetParent(track, true);
            g.position = p; g.rotation = Quaternion.LookRotation(tan, Vector3.up);
            gates.Add(g);
            if (i == 0) Instance(M + "Race_Arch.obj", g, p, g.rotation);
            else
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 fp = p + g.right * (4.0f * s);
                    GroundY(fp, out float fy); fp.y = fy;
                    Instance(M + (i % 2 == 0 ? "Race_Flag_Red.obj" : "Race_Flag_Blue.obj"), g, fp, g.rotation * Quaternion.Euler(0f, s < 0 ? 180f : 0f, 0f));
                }
        }
        rm.gateHalfWidth = 4f;
        rm.gates = gates.ToArray();
        rm.laps = 2;
        rm.horseModel = Load(Farm + "Animal_Horse.obj");
        rm.riderModels = new[] { Load(M + "Rider_Red.obj"), Load(M + "Rider_Blue.obj") };
        rm.markerModel = Load(M + "Race_Marker.obj");
        rm.neigh = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "horse_1.wav");

        Transform g0 = gates[0];
        Vector3 outside = g0.position + g0.right * 8f;
        if (Vector3.Distance(outside, center) < Vector3.Distance(g0.position - g0.right * 8f, center)) outside = g0.position - g0.right * 8f;
        GroundY(outside, out float oy); outside.y = oy;
        var npc = Npc(track, "NPC_Chabandes", M + "NPC_Chabandes.obj", outside, Face(outside, g0.position));
        var starter = npc.AddComponent<RaceStarter>(); starter.race = rm;
        Vector3 hp = outside - g0.forward * 2.2f; GroundY(hp, out float hy); hp.y = hy;
        Instance(Farm + "Animal_Horse.obj", track, hp, Quaternion.LookRotation(g0.forward));
        log.Add($"ат чабыш: трасса {rad.x * 2:0}×{rad.y * 2:0} м, поворот {angle:0}°, центр ({center.x:0}, {center.z:0}), {Vector3.Distance(Flat(center), Flat(spawn)):0} м от старта");
    }

    static readonly List<(Vector3 c, float half)> river = new List<(Vector3, float)>();
    static void BuildRiver(Scene scene)
    {
        river.Clear();
        Transform waterT = All(scene).FirstOrDefault(t => t.name == "RiverWater");
        MeshFilter mf = waterT != null ? waterT.GetComponent<MeshFilter>() : null;
        if (mf == null || mf.sharedMesh == null) return;
        Vector3[] v = mf.sharedMesh.vertices;
        for (int i = 0; i + 1 < v.Length; i += 2)
        {
            Vector3 l = waterT.TransformPoint(v[i]), r = waterT.TransformPoint(v[i + 1]);
            river.Add(((l + r) * 0.5f, Vector3.Distance(l, r) * 0.5f));
        }
    }

    static bool NearRiver(Vector3 p, float margin)
    {
        foreach (var r in river)
        {
            float dx = p.x - r.c.x, dz = p.z - r.c.z;
            float lim = r.half + margin;
            if (dx * dx + dz * dz < lim * lim) return true;
        }
        return false;
    }

    static bool TrackOk(Vector3 c, Vector2 rad, float angle, int n, float baseY)
    {
        Quaternion rot = Quaternion.Euler(0f, angle, 0f);
        float min = float.MaxValue, max = float.MinValue, prev = float.NaN, first = 0f;
        for (int i = 0; i < n * 2; i++)
        {
            float a = Mathf.PI * i / n;   // флажки и середины между ними
            Vector3 p = c + rot * new Vector3(rad.x * Mathf.Cos(a), 0f, rad.y * Mathf.Sin(a));
            if (NearRiver(p, 3f)) return false;
            if (!GroundY(p, out float y)) return false;
            if (y > baseY + 8f || y < baseY - 8f) return false;   // не в снежных горах
            if (!float.IsNaN(prev) && Mathf.Abs(y - prev) > 1.6f) return false;
            if (i == 0) first = y;
            prev = y; min = Mathf.Min(min, y); max = Mathf.Max(max, y);
            if (max - min > 4f) return false;
            foreach (Collider col in Physics.OverlapSphere(new Vector3(p.x, y + 1.6f, p.z), 3.0f, ~0, QueryTriggerInteraction.Ignore))
                if (!IsGround(col) && !IsPath(col) && col.bounds.size.magnitude > 2.5f) return false;   // кусты и камешки не мешают
        }
        return Mathf.Abs(prev - first) <= 1.6f;
    }

    // ================================================================== Рыбалка

    static void Fishing(Scene scene, Transform root, Vector3 spawn)
    {
        Transform waterT = All(scene).FirstOrDefault(t => t.name == "RiverWater");
        MeshFilter mf = waterT != null ? waterT.GetComponent<MeshFilter>() : null;
        if (mf == null || mf.sharedMesh == null) { log.Add("рыбалка: река не найдена"); return; }
        Vector3[] v = mf.sharedMesh.vertices;
        var pairs = new List<(Vector3 l, Vector3 r, float d)>();
        for (int i = 0; i + 1 < v.Length; i += 2)
        {
            Vector3 l = waterT.TransformPoint(v[i]), r = waterT.TransformPoint(v[i + 1]);
            Vector3 c = (l + r) * 0.5f;
            pairs.Add((l, r, Vector2.Distance(new Vector2(c.x, c.z), new Vector2(spawn.x, spawn.z))));
        }
        foreach (var pr in pairs.Where(p => p.d > 12f).OrderBy(p => p.d))
        {
            Vector3 c = (pr.l + pr.r) * 0.5f;
            Vector3 near = Vector3.Distance(pr.l, spawn) < Vector3.Distance(pr.r, spawn) ? pr.l : pr.r;
            Vector3 outDir = near - c; outDir.y = 0f;
            if (outDir.sqrMagnitude < 0.01f) continue;
            outDir.Normalize();
            Vector3 bank = near + outDir * 1.2f;
            if (!GroundY(bank, out float by) || by < c.y - 0.6f || by > c.y + 2.5f) continue;
            bank.y = by;
            if (!Free(bank - outDir * 0.8f, 1.0f, 0.5f, out _)) continue;

            Quaternion rot = Quaternion.LookRotation(-outDir, Vector3.up);
            var pier = Instance(M + "Fishing_Pier.obj", root, bank, rot);
            if (pier == null) return;
            pier.name = "Fishing_Pier";
            foreach (MeshFilter m in pier.GetComponentsInChildren<MeshFilter>())
                if (m.sharedMesh != null) m.gameObject.AddComponent<MeshCollider>().sharedMesh = m.sharedMesh;
            var spot = pier.AddComponent<FishingSpot>();
            var stand = new GameObject("StandPoint").transform; stand.SetParent(pier.transform, false); stand.localPosition = new Vector3(0f, 0.45f, 1.5f);
            float half = Vector3.Distance(pr.l, pr.r) * 0.5f;
            var water = new GameObject("WaterPoint").transform; water.SetParent(pier.transform, false);
            water.position = c - outDir * Mathf.Min(half * 0.4f, 1.5f) + Vector3.up * 0.03f;
            if (Vector3.Distance(Flat(water.position), Flat(bank)) < 2.8f) water.position = bank - outDir * 3.2f + Vector3.up * (c.y + 0.03f - bank.y);
            spot.standPoint = stand; spot.waterPoint = water;
            spot.bobberModel = Load(M + "Fishing_Bobber.obj");
            log.Add($"рыбалка: мостик у реки ({bank.x:0}, {bank.z:0})");
            return;
        }
        log.Add("рыбалка: подходящий берег не найден");
    }

    // ================================================================== Бүркүтчү

    static void Eagle(Scene scene, Transform root, Vector3 spawn, TMP_Text story)
    {
        Transform farm = All(scene).FirstOrDefault(t => t.name == "Farm" && t.parent != null && t.parent.name == "Environment") ?? All(scene).FirstOrDefault(t => t.name == "Farm");
        Vector3 c = farm != null ? farm.position : spawn + new Vector3(-30f, 0f, 0f);
        if (!FindSpot(c, 14f, 26f, 1.2f, out Vector3 p)) { FindSpot(spawn, 10f, 30f, 1.2f, out p); }
        var npc = Npc(root, "NPC_Burkutchu", M + "NPC_Burkutchu.obj", p, Face(p, spawn));
        var hunter = npc.AddComponent<EagleHunter>();
        Transform model = npc.transform.Find("NPC_Burkutchu");
        var perched = Instance(M + "Eagle_Perched.obj", model != null ? model : npc.transform, Vector3.zero, Quaternion.identity);
        if (perched != null)
        {
            perched.transform.localPosition = new Vector3(0.41f, 1.50f, 0.29f);
            perched.transform.localRotation = Quaternion.Euler(0f, -20f, 0f);
            hunter.perchedEagle = perched.transform;
        }
        hunter.flyingModel = Load(M + "Eagle_Flying.obj");
        hunter.cries = new[] { "eagle_1", "eagle_2" }.Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + n + ".wav")).Where(a => a != null).ToArray();
        hunter.storyText = story;

        SpeechPanel sp = All(scene).Select(t => t.GetComponent<SpeechPanel>()).FirstOrDefault(s => s != null);
        if (sp != null)
        {
            var s = sp.speakers.FirstOrDefault(x => x.key == "Болот");
            if (s == null) { s = new SpeechPanel.Speaker(); sp.speakers.Add(s); }
            s.key = "Болот"; s.displayName = "Болот"; s.role = "бүркүтчү"; s.roleEn = "eagle hunter"; s.voicePitch = 0.85f;
            s.portrait = AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + "ui_portrait_burkutchu.png");
            EditorUtility.SetDirty(sp);
        }
        log.Add($"бүркүтчү Болот ({p.x:0}, {p.z:0})");
    }

    // ================================================================== Погода

    static void Weather(Scene scene)
    {
        var go = new GameObject("Weather");
        var w = go.AddComponent<WeatherSystem>();
        w.rainMaterial = ParticleMat("Weather_Rain.mat", Gen + "rain_streak.png");
        w.snowMaterial = ParticleMat("Weather_Snow.mat", Gen + "snowflake.png");
        w.rainLoop = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "rain_loop.ogg");
        w.windLoop = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "wind_loop.ogg");
        log.Add("погода: дождь, снег, облачность");
    }

    static Material ParticleMat(string file, string texPath)
    {
        string path = MatDir + file;
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        else if (sh != null) m.shader = sh;
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    // ================================================================== Зоны словаря

    static void Words(Scene scene, Transform root, Vector3 spawn)
    {
        int n = 0;
        Transform farm = All(scene).FirstOrDefault(t => t.name == "Farm");
        if (farm != null) { Zone(root, "Words_Farm", farm.position, 16f, "koy", "echki", "uy", "at", "it"); n++; }
        Transform pier = All(scene).FirstOrDefault(t => t.name == "Fishing_Pier");
        Transform river = All(scene).FirstOrDefault(t => t.name == "RiverSounds");
        if (pier != null) { Zone(root, "Words_River", pier.position, 10f, "suu", "dariya"); n++; }
        if (river != null) foreach (Transform s in river) { Zone(root, "Words_River", s.position, 7f, "suu", "dariya"); n++; }
        Transform bazaar = All(scene).FirstOrDefault(t => t.name == "Bazaar");
        if (bazaar != null) { Zone(root, "Words_Bazaar", bazaar.position, 6f, "bazar", "soodager", "kalpak"); n++; }
        Transform houses = All(scene).FirstOrDefault(t => t.name == "Village_Houses");
        if (houses != null) foreach (Transform h in houses) { Zone(root, "Words_House", h.position + Vector3.up * 0.6f, 3.2f, "juk", "tushkiyiz"); n++; }

        var talk = new Dictionary<string, string[]>
        {
            { "NPC_Apa", new[] { "apa", "elechek" } }, { "NPC_Baike", new[] { "baike", "kalpak" } }, { "NPC_Chaban", new[] { "chaban", "jailoo", "koy" } },
        };
        foreach (var kv in talk)
        {
            Transform t = All(scene).FirstOrDefault(x => x.name == kv.Key);
            var vt = t != null ? t.GetComponent<VillagerTalk>() : null;
            if (vt != null) { vt.vocabWords = kv.Value; EditorUtility.SetDirty(vt); }
        }
        log.Add($"словарь: {Vocab.Total} слов, зон {n}");
    }

    static void Zone(Transform root, string name, Vector3 p, float r, params string[] words)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, true);
        go.transform.position = p;
        var z = go.AddComponent<VocabZone>(); z.radius = r; z.words = words;
    }

    // ================================================================== проверка в Play Mode

    static bool Playing() { if (!Application.isPlaying) Debug.LogWarning("Сначала нажми Play."); return Application.isPlaying; }
    [MenuItem("Tools/Diploma/Play Mode: Fun/Open Dictionary (J)")] static void TDict() { if (Playing() && JournalWindow.Instance != null) JournalWindow.Instance.Open(); }
    [MenuItem("Tools/Diploma/Play Mode: Fun/Unlock 10 Words")] static void TWords() { if (Playing()) Vocab.Unlock(Vocab.Words.Where(w => !Vocab.Known(w.id)).Take(10).Select(w => w.id).ToArray()); }
    [MenuItem("Tools/Diploma/Play Mode: Fun/Komuz Game")] static void TKomuz() { if (Playing()) KomuzGame.Ensure().Open(); }
    [MenuItem("Tools/Diploma/Play Mode: Fun/Komuz: Start Jailoo")] static void TKomuz1() { if (Playing()) { KomuzGame.Ensure().Open(); KomuzGame.Instance.StartSong(0); } }
    [MenuItem("Tools/Diploma/Play Mode: Fun/Shyrdak: Level 1 Board")] static void TSh1() { if (Playing()) { ShyrdakPuzzle.Ensure().Open(); ShyrdakPuzzle.Instance.StartLevel(0); } }
    [MenuItem("Tools/Diploma/Play Mode: Fun/Shyrdak Puzzle")] static void TShyrdak() { if (Playing()) ShyrdakPuzzle.Ensure().Open(); }
    [MenuItem("Tools/Diploma/Play Mode: Fun/Start Horse Race")] static void TRace() { if (Playing() && RaceManager.Instance != null) RaceManager.Instance.StartRace(); }
    [MenuItem("Tools/Diploma/Play Mode: Fun/Weather: Rain")] static void TRain() { if (Playing() && WeatherSystem.Instance != null) WeatherSystem.Instance.Set(WeatherSystem.Kind.Rain); }
    [MenuItem("Tools/Diploma/Play Mode: Fun/Weather: Snow")] static void TSnow() { if (Playing() && WeatherSystem.Instance != null) WeatherSystem.Instance.Set(WeatherSystem.Kind.Snow); }
    [MenuItem("Tools/Diploma/Play Mode: Fun/Weather: Clear")] static void TClear() { if (Playing() && WeatherSystem.Instance != null) WeatherSystem.Instance.Set(WeatherSystem.Kind.Clear); }
    [MenuItem("Tools/Diploma/Play Mode: Fun/Inventory: Open (I)")] static void TInv() { if (Playing() && InventoryWindow.Instance != null) InventoryWindow.Instance.Open(false); }
    [MenuItem("Tools/Diploma/Play Mode: Fun/Inventory: Add Test Items")] static void TInvAdd() { if (Playing()) { Inventory.Add("lantern"); Inventory.Add("alma", 3); Inventory.Add("monko", 2); Inventory.Add("altyn", 1); Inventory.Add("kymyz", 1); Inventory.Add("feather", 1); } }
    [MenuItem("Tools/Diploma/Play Mode: Fun/Time: 12:00")] static void TNoon() { SetHour(12f); }
    [MenuItem("Tools/Diploma/Play Mode: Fun/Time: 18:20 (golden hour)")] static void TGold() { SetHour(18.33f); }
    [MenuItem("Tools/Diploma/Play Mode: Fun/Time: 19:30 (dusk)")] static void TDusk() { SetHour(19.5f); }
    [MenuItem("Tools/Diploma/Play Mode: Fun/Time: 23:00 (night)")] static void TNight() { SetHour(23f); }
    static void SetHour(float h) { if (Playing() && DayNightCycle.Instance != null) DayNightCycle.Instance.hour = h; }
    [MenuItem("Tools/Diploma/Play Mode: Fun/Race: Easy")] static void TRaceEasy() { if (Playing() && RaceManager.Instance != null) RaceManager.Instance.StartRace(0); }
    [MenuItem("Tools/Diploma/Play Mode: Fun/Close Window (Esc)")] static void TClose() { if (Playing()) Modal.TryClose(); }
    [MenuItem("Tools/Diploma/Play Mode: Fun/Teleport To Race Start")] static void TTpRace() { if (Playing() && RaceManager.Instance != null) Teleport(RaceManager.Instance.gates[0].position - RaceManager.Instance.gates[0].right * 5f); }
    [MenuItem("Tools/Diploma/Play Mode: Fun/Teleport To Fishing")] static void TTpFish() { var f = Object.FindAnyObjectByType<FishingSpot>(); if (Playing() && f != null) Teleport(f.transform.position - f.transform.forward * 1.5f); }
    [MenuItem("Tools/Diploma/Play Mode: Fun/Teleport To Eagle Hunter")] static void TTpEagle() { var f = Object.FindAnyObjectByType<EagleHunter>(); if (Playing() && f != null) Teleport(f.transform.position + f.transform.forward * 2.5f); }
    [MenuItem("Tools/Diploma/Play Mode: Fun/Teleport To Komuzchu")] static void TTpKomuz() { var f = Object.FindAnyObjectByType<KomuzMusician>(); if (Playing() && f != null) Teleport(f.transform.position + f.transform.forward * 2.2f); }

    static void Teleport(Vector3 p)
    {
        var pm = Object.FindAnyObjectByType<PlayerMovement>();
        if (pm == null) return;
        var cc = pm.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        pm.transform.position = p + Vector3.up * 1.5f;
        if (cc != null) cc.enabled = true;
    }

    // ================================================================== Участковый Актан Турдакунович

    [MenuItem("Tools/Diploma/Install NPC: Uchastkovyi Aktan Turdakunovich")]
    public static void InstallAktan()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (Transform t in All(scene).Where(t => t.name == "NPC_Aktan").ToList()) if (t != null) Object.DestroyImmediate(t.gameObject);
        valley = All(scene).FirstOrDefault(t => t.name == "MountainValley");
        Physics.SyncTransforms();

        GameObject player = All(scene).Select(t => t.GetComponent<PlayerMovement>()).Where(p => p != null).Select(p => p.gameObject).FirstOrDefault();
        Vector3 spawn = player != null ? player.transform.position : Vector3.zero;
        Transform parent = All(scene).FirstOrDefault(t => t.name == "Villagers") ?? All(scene).FirstOrDefault(t => t.name == "Environment");
        TMP_Text story = All(scene).Select(t => t.GetComponent<VillagerTalk>()).Where(v => v != null && v.storyText != null).Select(v => v.storyText).FirstOrDefault()
                         ?? All(scene).Select(t => t.GetComponent<TMP_Text>()).FirstOrDefault(t => t != null && t.name == "StoryText");

        // стоит на виду в центре села: у базара или рядом с Айбеком, но не на ферме и не вплотную к другим NPC
        Transform farm = All(scene).FirstOrDefault(t => t.name == "Farm" && t.parent != null && t.parent.name == "Environment") ?? All(scene).FirstOrDefault(t => t.name == "Farm");
        var others = All(scene).Where(t => t.name.StartsWith("NPC_") || t.GetComponent<VillagerTalk>() != null).Select(t => t.position).ToList();
        var centers = new[] { "Bazaar", "NPC_Aibek" }.Select(n => All(scene).FirstOrDefault(t => t.name == n)).Where(t => t != null).Select(t => t.position).ToList();
        centers.Add(spawn);
        Vector3 p = spawn + new Vector3(5f, 0f, 3f); bool found = false;
        foreach (Vector3 c in centers)
        {
            for (float r = 4f; r <= 14f && !found; r += 1.5f)
                for (int k = 0; k < 16 && !found; k++)
                {
                    float a = k * Mathf.PI / 8f + r;
                    Vector3 q = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    if (farm != null && Vector2.Distance(new Vector2(q.x, q.z), new Vector2(farm.position.x, farm.position.z)) < 22f) continue;
                    if (others.Any(o => Vector2.Distance(new Vector2(q.x, q.z), new Vector2(o.x, o.z)) < 3.5f)) continue;
                    if (!Free(q, 0.9f, 0.4f, out float gy)) continue;
                    p = new Vector3(q.x, gy, q.z); found = true;
                }
            if (found) break;
        }
        var npc = Npc(parent, "NPC_Aktan", M + "NPC_Aktan.obj", p, Face(p, spawn));

        // настоящее лицо с фотографии — на голову модели (PNG с прозрачными краями)
        Transform model = npc.transform.Find("NPC_Aktan");
        var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
        face.name = "PhotoFace";
        Object.DestroyImmediate(face.GetComponent<Collider>());
        face.transform.SetParent(model != null ? model : npc.transform, false);
        face.transform.localPosition = new Vector3(0f, 1.575f, 0.136f);
        face.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        face.transform.localScale = new Vector3(0.23f, 0.23f * 243f / 288f, 1f);
        var fr = face.GetComponent<MeshRenderer>();
        fr.sharedMaterial = FaceMaterial();
        fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var talk = npc.AddComponent<VillagerTalk>();
        talk.speakerKey = "Актан";
        talk.nameRu = "Актаном Турдакуновичем";
        talk.nameEn = "Aktan Turdakunovich";
        talk.storyText = story;
        talk.talkDistance = 2.8f;
        talk.linesRu = new[]
        {
            "Салам, жигит! Мен Актан Турдакунович — айылдын участковыйы. Я участковый этого села, будем знакомы.",
            "Айылда тынчтык — в селе у нас спокойно. Тынчтык — значит «мир, покой». Хорошее слово, запомни.",
            "Тартип баарынан жогору — порядок превыше всего! Если что-то случится, сразу иди ко мне.",
            "На ат чабыше скачи осторожно: ат — не машина, тормозов у него нет.",
            "Вечером без чырака далеко не ходи. Фонари фонарями, а у реки темно.",
            "Асан на базаре — честный торговец, лично проверял. А вот цену всегда спрашивай заранее!",
            "Мыйзам — это закон, урмат — уважение. Кто уважает старших, тот и закон уважает.",
        };
        talk.linesEn = new[]
        {
            "Salam, young man! Мен Актан Турдакунович — I'm the district officer of this village. Nice to meet you.",
            "Айылда тынчтык — our village is peaceful. Тынчтык means \"peace, calm\". A good word to remember.",
            "Тартип баарынан жогору — order above all! If anything happens, come straight to me.",
            "Ride carefully at the ат чабыш: a horse is not a car, it has no brakes.",
            "Don't wander far at night without a lantern. Street lamps are fine, but it's dark by the river.",
            "Asan at the bazaar is an honest trader — I checked myself. Still, always ask the price first!",
            "Мыйзам is law, урмат is respect. Whoever respects the elders respects the law too.",
        };
        talk.nightGreetingRu = "Түн жакшы болсун! Поздно уже — я делаю обход, в селе всё спокойно.";
        talk.nightGreetingEn = "Түн жакшы болсун! It's late — I'm on my night round, all is calm in the village.";
        var known = new HashSet<string>(Vocab.Words.Select(w => w.id));
        talk.vocabWords = new[] { "tynchtyk", "tartip", "myizam", "urmat" }.Where(known.Contains).ToArray();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Aktan] Участковый Актан Турдакунович стоит в ({p.x:0}, {p.z:0}). Подойди и нажми E.");
        Selection.activeGameObject = npc;
    }

    static Material FaceMaterial() => FaceMaterial(M + "aktan_face.png", MatDir + "Aktan_Face.mat");

    static Material FaceMaterial(string texPath, string matPath)
    {
        if (AssetImporter.GetAtPath(texPath) is TextureImporter ti && (!ti.alphaIsTransparency || ti.wrapMode != TextureWrapMode.Clamp))
        { ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = true; ti.SaveAndReimport(); }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (m == null)
        {
            if (!AssetDatabase.IsValidFolder(MatDir.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/Game", "Materials");
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, matPath);
        }
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", 0.5f);
        m.EnableKeyword("_ALPHATEST_ON");
        m.SetFloat("_Smoothness", 0.1f);
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        EditorUtility.SetDirty(m);
        return m;
    }

    // ================================================================== Местный бунтарь

    [MenuItem("Tools/Diploma/Install NPC: Buntar (local rebel)")]
    public static void InstallBuntar()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (Transform t in All(scene).Where(t => t.name == "NPC_Buntar").ToList()) if (t != null) Object.DestroyImmediate(t.gameObject);
        valley = All(scene).FirstOrDefault(t => t.name == "MountainValley");
        Physics.SyncTransforms();

        GameObject player = All(scene).Select(t => t.GetComponent<PlayerMovement>()).Where(p => p != null).Select(p => p.gameObject).FirstOrDefault();
        Vector3 spawn = player != null ? player.transform.position : Vector3.zero;
        Transform parent = All(scene).FirstOrDefault(t => t.name == "Villagers") ?? All(scene).FirstOrDefault(t => t.name == "Environment");
        TMP_Text story = All(scene).Select(t => t.GetComponent<VillagerTalk>()).Where(v => v != null && v.storyText != null).Select(v => v.storyText).FirstOrDefault()
                         ?? All(scene).Select(t => t.GetComponent<TMP_Text>()).FirstOrDefault(t => t != null && t.name == "StoryText");

        // тусуется у игрового луга, подальше от участкового
        Transform farm = All(scene).FirstOrDefault(t => t.name == "Farm" && t.parent != null && t.parent.name == "Environment") ?? All(scene).FirstOrDefault(t => t.name == "Farm");
        Transform aktan = All(scene).FirstOrDefault(t => t.name == "NPC_Aktan");
        var others = All(scene).Where(t => t.name.StartsWith("NPC_") || t.GetComponent<VillagerTalk>() != null).Select(t => t.position).ToList();
        var centers = new[] { "Zone_Games", "Zone_Yurt", "NPC_Aibek" }.Select(n => All(scene).FirstOrDefault(t => t.name == n)).Where(t => t != null).Select(t => t.position).ToList();
        centers.Add(spawn);
        Vector3 p = spawn + new Vector3(-6f, 0f, 4f); bool found = false;
        foreach (Vector3 c in centers)
        {
            for (float r = 6f; r <= 16f && !found; r += 1.5f)
                for (int k = 0; k < 16 && !found; k++)
                {
                    float a = k * Mathf.PI / 8f + r * 0.7f;
                    Vector3 q = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    Vector2 q2 = new Vector2(q.x, q.z);
                    if (farm != null && Vector2.Distance(q2, new Vector2(farm.position.x, farm.position.z)) < 22f) continue;
                    if (aktan != null && Vector2.Distance(q2, new Vector2(aktan.position.x, aktan.position.z)) < 14f) continue;
                    if (others.Any(o => Vector2.Distance(q2, new Vector2(o.x, o.z)) < 4f)) continue;
                    if (!Free(q, 0.9f, 0.4f, out float gy)) continue;
                    p = new Vector3(q.x, gy, q.z); found = true;
                }
            if (found) break;
        }
        var npc = Npc(parent, "NPC_Buntar", M + "NPC_Buntar.obj", p, Face(p, spawn));

        Transform model = npc.transform.Find("NPC_Buntar");
        var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
        face.name = "PhotoFace";
        Object.DestroyImmediate(face.GetComponent<Collider>());
        face.transform.SetParent(model != null ? model : npc.transform, false);
        face.transform.localPosition = new Vector3(0f, 1.587f, 0.146f);
        face.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        face.transform.localScale = new Vector3(0.23f, 0.23f * 308f / 264f, 1f);
        var fr = face.GetComponent<MeshRenderer>();
        fr.sharedMaterial = FaceMaterial(M + "buntar_face.png", MatDir + "Buntar_Face.mat");
        fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var talk = npc.AddComponent<VillagerTalk>();
        talk.speakerKey = "Бунтарь";
        talk.nameRu = "Анасом";
        talk.nameEn = "Anas";
        talk.storyText = story;
        talk.talkDistance = 2.8f;
        talk.linesRu = new[]
        {
            "Эй, кимсиң? А, новенький... Мен Анас — я тут местный бунтарь. Правила? Не, не слышал.",
            "Эркиндик! Свобода — вот что главное. Эркиндик — запомни это слово.",
            "Актан Турдакунович опять говорит, что музыка у меня в наушниках слишком громкая. Как он вообще её слышит?!",
            "Весь айыл встаёт в пять утра... а я — к обеду. Кто-то же должен отдыхать за всех.",
            "Когда-нибудь уеду в шаар — в город. Бишкек, огни... Хотя апа говорит, что без её боорсоков я там пропаду. Наверное, права.",
            "На ат чабыше я бы всех обогнал. Просто... лошади меня почему-то не любят.",
            "Только никому не говори: я тоже помогаю аксакалу с юртой. Бунтарь — не значит лентяй! Достор — друзья — так и делают.",
        };
        talk.linesEn = new[]
        {
            "Hey, кимсиң? Ah, the new guy... Мен Анас — I'm the local rebel. Rules? Never heard of them.",
            "Эркиндик! Freedom — that's what matters. Эркиндик — remember that word.",
            "Aktan Turdakunovich says my headphones are too loud again. How can he even hear them?!",
            "The whole айыл gets up at five... I get up for lunch. Someone has to rest for everyone.",
            "One day I'll move to the шаар — the city. Bishkek, the lights... But apa says without her boorsok I'd be lost there. She's probably right.",
            "I'd win the ат чабыш easily. It's just... horses don't seem to like me.",
            "Don't tell anyone: I help the aksakal with the yurt too. A rebel isn't a slacker! That's what достор — friends — do.",
        };
        talk.nightGreetingRu = "Түн? Для меня день только начинается. Тсс, участковому не говори!";
        talk.nightGreetingEn = "Night? For me the day is just starting. Shh, don't tell the officer!";
        var known = new HashSet<string>(Vocab.Words.Select(w => w.id));
        talk.vocabWords = new[] { "erkindik", "shaar", "dostor" }.Where(known.Contains).ToArray();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Buntar] Анас, местный бунтарь, стоит в ({p.x:0}, {p.z:0}). Подойди и нажми E.");
        Selection.activeGameObject = npc;
    }

    [MenuItem("Tools/Diploma/Play Mode: Fun/Teleport To Buntar")]
    static void TTpBuntar()
    {
        if (!Playing()) return;
        GameObject a = GameObject.Find("NPC_Buntar");
        if (a != null) Teleport(a.transform.position + a.transform.forward * 2f);
    }

    [MenuItem("Tools/Diploma/Play Mode: Fun/Dialog: Buntar")]
    static void TBuntarSay()
    {
        if (Playing() && SpeechPanel.Instance != null && SpeechPanel.Instance.text != null)
            SpeechPanel.Instance.text.text = "Бунтарь: Эй, кимсиң? А, новенький... Я тут местный бунтарь. Правила? Не, не слышал.";
    }

    [MenuItem("Tools/Diploma/Play Mode: Fun/Teleport To Uchastkovyi")]
    static void TTpAktan()
    {
        if (!Playing()) return;
        GameObject a = GameObject.Find("NPC_Aktan");
        if (a != null) Teleport(a.transform.position + a.transform.forward * 2f);
    }

    [MenuItem("Tools/Diploma/Play Mode: Fun/Dialog: Uchastkovyi")]
    static void TAktanSay()
    {
        if (Playing() && SpeechPanel.Instance != null && SpeechPanel.Instance.text != null)
            SpeechPanel.Instance.text.text = "Актан: Салам, жигит! Мен Актан Турдакунович — айылдын участковыйы. Я участковый этого села, будем знакомы.";
    }

    // ================================================================== Обновление v3: детектив, манасчы, утренние задания

    [MenuItem("Tools/Diploma/Install Update v3 (detective, Manaschy, daily tasks)")]
    public static void InstallV3()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        valley = All(scene).FirstOrDefault(t => t.name == "MountainValley");
        foreach (string p in new[] { "manas_chant" })
            if (AssetImporter.GetAtPath("Assets/Game/Resources/MURAS/Audio/" + p + ".ogg") is AudioImporter ai)
            { var st = ai.defaultSampleSettings; if (st.loadType != AudioClipLoadType.Streaming) { st.loadType = AudioClipLoadType.Streaming; ai.defaultSampleSettings = st; ai.SaveAndReimport(); } }
        for (int i = 1; i <= 6; i++)
            if (AssetImporter.GetAtPath("Assets/Game/Resources/MURAS/Manas/manas_" + i + ".png") is TextureImporter ti && (ti.mipmapEnabled || ti.npotScale != TextureImporterNPOTScale.None))
            { ti.mipmapEnabled = false; ti.wrapMode = TextureWrapMode.Clamp; ti.npotScale = TextureImporterNPOTScale.None; ti.SaveAndReimport(); }
        foreach (Transform t in All(scene).Where(t => t.name == "Update_v3").ToList()) if (t != null) Object.DestroyImmediate(t.gameObject);
        Physics.SyncTransforms();
        BuildRiver(scene);
        var log3 = new List<string>();

        GameObject player = All(scene).Select(t => t.GetComponent<PlayerMovement>()).Where(p => p != null).Select(p => p.gameObject).FirstOrDefault();
        Vector3 spawn = player != null ? player.transform.position : Vector3.zero;
        Transform env = All(scene).FirstOrDefault(t => t.name == "Environment");
        var root = new GameObject("Update_v3").transform; root.SetParent(env, false);
        Transform farm = All(scene).FirstOrDefault(t => t.name == "Farm" && t.parent != null && t.parent.name == "Environment") ?? All(scene).FirstOrDefault(t => t.name == "Farm");
        var others = All(scene).Where(t => t.name.StartsWith("NPC_") || t.GetComponent<VillagerTalk>() != null || t.name == "Aksakal" || t.name == "Ezhe").Select(t => t.position).ToList();
        Vector2 F(Vector3 v) => new Vector2(v.x, v.z);
        bool FarFromAll(Vector3 q, float npc) => (farm == null || Vector2.Distance(F(q), F(farm.position)) > 22f) && others.All(o => Vector2.Distance(F(q), F(o)) > npc);

        // ---- 1. Манасчы у костра (рядом с юртой, на тихом месте)
        Transform yurt = All(scene).FirstOrDefault(t => t.name == "Zone_Yurt");
        Vector3 yc = yurt != null ? yurt.position : spawn;
        Vector3 firePos = Vector3.zero; bool fireOk = false;
        for (float r = 9f; r <= 26f && !fireOk; r += 1.5f)
            for (int k = 0; k < 20 && !fireOk; k++)
            {
                float a = k * Mathf.PI / 10f + r * 0.37f;
                Vector3 q = yc + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (!FarFromAll(q, 7f) || NearRiver(q, 4f)) continue;
                Vector3 toSpawn = Flat(spawn - q).normalized;
                Vector3 seat = q - toSpawn * 1.9f;
                if (!Free(q, 1.3f, 0.35f, out float gy) || !Free(seat, 0.8f, 0.35f, out float sy)) continue;
                firePos = new Vector3(q.x, gy, q.z); fireOk = true;
            }
        if (fireOk)
        {
            Vector3 toSpawn = Flat(spawn - firePos).normalized;
            var fire = Instance(M + "Campfire.obj", root, firePos, Quaternion.identity);
            if (fire != null)
            {
                fire.name = "Campfire";
                var flame = Emissive("Fire_Flame", new Color(1f, 0.45f, 0.08f), 2.6f);
                var core = Emissive("Fire_Core", new Color(1f, 0.85f, 0.35f), 3.2f);
                var ember = Emissive("Fire_Ember", new Color(0.9f, 0.2f, 0.05f), 1.6f);
                foreach (var r in fire.GetComponentsInChildren<MeshRenderer>())
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null) continue;
                        if (mats[i].name.Contains("FlameCore")) mats[i] = core;
                        else if (mats[i].name.Contains("Flame")) mats[i] = flame;
                        else if (mats[i].name.Contains("Ember")) mats[i] = ember;
                    }
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                var lgo = new GameObject("FireLight"); lgo.transform.SetParent(fire.transform, false); lgo.transform.localPosition = new Vector3(0f, 0.8f, 0f);
                var l = lgo.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.6f, 0.3f); l.range = 9f; l.intensity = 3f; l.shadows = LightShadows.None;
                var fl = fire.AddComponent<FireFlicker>(); fl.fireLight = l;
                var src = fire.AddComponent<AudioSource>();
                src.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Resources/MURAS/Audio/fire_loop.ogg");
                src.loop = true; src.playOnAwake = true; src.spatialBlend = 1f; src.volume = 0.55f; src.minDistance = 2f; src.maxDistance = 16f; src.rolloffMode = AudioRolloffMode.Linear;
                var col = fire.AddComponent<SphereCollider>(); col.radius = 0.6f; col.center = new Vector3(0f, 0.3f, 0f);
            }
            Vector3 seat = firePos - toSpawn * 1.9f; GroundY(seat, out float sy2); seat.y = sy2;
            var man = new GameObject("NPC_Manaschy");
            man.transform.SetParent(root, true);
            man.transform.position = seat; man.transform.rotation = Face(seat, firePos);
            var cap = man.AddComponent<CapsuleCollider>(); cap.center = new Vector3(0f, 0.6f, 0f); cap.height = 1.2f; cap.radius = 0.55f; cap.direction = 0;
            var mm = Instance(M + "NPC_Manaschy.obj", man.transform, seat, man.transform.rotation);
            if (mm != null) mm.name = "NPC_Manaschy";
            man.AddComponent<ManasStory>();
            log3.Add($"манасчы и костёр: ({firePos.x:0}, {firePos.z:0})");
        }
        else log3.Add("манасчы: не нашлось места рядом с юртой");

        // ---- 2. Гнездо сороки с наушниками — у реки, за пирсом
        var fishing = All(scene).Select(t => t.GetComponent<FishingSpot>()).FirstOrDefault(f => f != null);
        Vector3 pc = fishing != null ? fishing.transform.position : spawn;
        if (FindSpotWhere(pc, 5f, 14f, 0.6f, q => FarFromAll(q, 4f), out Vector3 nestPos))
        {
            var nest = Instance(M + "Clue_Nest.obj", root, nestPos, Quaternion.Euler(0f, 37f, 0f));
            if (nest != null)
            {
                nest.name = "Clue_MagpieNest";
                var ph = Instance(M + "Clue_Headphones.obj", nest.transform, nestPos + Vector3.up * 0.08f, Quaternion.identity);
                if (ph != null) { ph.name = "Headphones"; ph.transform.localScale = Vector3.one * 0.9f; }
                var clue = nest.AddComponent<HeadphonesClue>(); clue.headphones = ph;
                log3.Add($"гнездо сороки: ({nestPos.x:0}, {nestPos.z:0})");
            }
        }
        else log3.Add("гнездо: не нашлось места у пирса");

        // ---- 3. Утренние задания: ведро у реки, корзина с нан у кухни, три вязанки по селу
        if (FindSpotWhere(pc, 2f, 7f, 0.4f, q => true, out Vector3 bucketPos))
            DailyProp(root, "Daily_Bucket", bucketPos, DailyTasks.T.Water, "chaka", 0, false);
        Transform kitchen = All(scene).FirstOrDefault(t => t.name == "Kitchen") ?? All(scene).FirstOrDefault(t => t.name == "Zone_Food");
        if (kitchen != null && FindSpotWhere(kitchen.position, 2.5f, 8f, 0.4f, q => true, out Vector3 basketPos))
            DailyProp(root, "Daily_NanBasket", basketPos, DailyTasks.T.Bread, "nan", 0, true);
        var woodSpots = new List<Vector3>();
        for (int i = 0; i < 3; i++)
        {
            float baseA = i * Mathf.PI * 2f / 3f + 0.6f;
            for (float r = 14f; r <= 38f; r += 3f)
            {
                Vector3 q = spawn + new Vector3(Mathf.Cos(baseA) * r, 0f, Mathf.Sin(baseA) * r);
                if (!FarFromAll(q, 3f) || NearRiver(q, 3f) || woodSpots.Any(w => Vector3.Distance(w, q) < 10f)) continue;
                if (!Free(q, 0.5f, 0.4f, out float wy)) continue;
                q.y = wy; woodSpots.Add(q);
                DailyProp(root, "Daily_Firewood", q, DailyTasks.T.Wood, "otun", 1 + i, false);
                break;
            }
        }
        log3.Add($"утренние задания: ведро, корзина, вязанок {woodSpots.Count}");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Update v3] " + string.Join(" · ", log3));
    }

    // ======================================================================== Update v4
    [MenuItem("Tools/Diploma/Install Update v4 (kok-boru, ordo, beshbarmak, yurt decor)")]
    public static void InstallV4()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        valley = All(scene).FirstOrDefault(t => t.name == "MountainValley");
        foreach (Transform t in All(scene).Where(t => t.name == "Update_v4").ToList()) if (t != null) Object.DestroyImmediate(t.gameObject);
        Physics.SyncTransforms();
        BuildRiver(scene);
        var log4 = new List<string>();

        GameObject player = All(scene).Select(t => t.GetComponent<PlayerMovement>()).Where(p => p != null).Select(p => p.gameObject).FirstOrDefault();
        Vector3 spawn = player != null ? player.transform.position : Vector3.zero;
        Transform env = All(scene).FirstOrDefault(t => t.name == "Environment");
        var root = new GameObject("Update_v4").transform; root.SetParent(env, false);
        var others = All(scene).Where(t => t.name.StartsWith("NPC_") || t.GetComponent<VillagerTalk>() != null || t.name == "Aksakal" || t.name == "Ezhe").Select(t => t.position).ToList();
        Vector2 F(Vector3 v) => new Vector2(v.x, v.z);
        bool FarFromNpc(Vector3 q, float d) => others.All(o => Vector2.Distance(F(q), F(o)) > d) && !NearRiver(q, 3f);

        // ---- 4. Көк бөрү — в центре трассы ат чабыш
        var race = All(scene).Select(t => t.GetComponent<RaceManager>()).FirstOrDefault(r => r != null);
        if (race != null && race.gates != null && race.gates.Length > 0)
        {
            Vector3 c = race.transform.position;
            Vector3 axis = Flat(race.gates[0].position - c).normalized;
            if (axis.sqrMagnitude < 0.5f) axis = Vector3.forward;
            GroundY(c, out float cy); c.y = cy;
            var kb = new GameObject("KokBoru").transform;
            kb.SetParent(root, true); kb.position = c; kb.rotation = Quaternion.LookRotation(axis);
            var ulak = Instance(M + "KokBoru_Ulak.obj", kb, c + Vector3.up * 0.02f, kb.rotation * Quaternion.Euler(0f, 90f, 0f));
            if (ulak != null) ulak.name = "Ulak";
            var st = kb.gameObject.AddComponent<KokBoruStarter>();
            st.ulak = ulak != null ? ulak.transform : null; st.axis = Vector3.forward;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 kp = c + axis * (14f * s); GroundY(kp, out float ky); kp.y = ky;
                var kz = Instance(M + (s > 0 ? "KokBoru_Kazan.obj" : "KokBoru_KazanBlue.obj"), kb, kp, Face(kp, c));
                if (kz != null) { kz.name = s > 0 ? "Kazan_Player" : "Kazan_Rivals"; var bc = kz.AddComponent<BoxCollider>(); bc.size = new Vector3(2.2f, 1f, 2.2f); bc.center = new Vector3(0f, 0.5f, 0f); }
            }
            log4.Add($"көк бөрү: центр ({c.x:0}, {c.z:0})");
        }
        else log4.Add("көк бөрү: нет трассы ат чабыш (сначала Install Fun Pack)");

        // ---- 5. Ордо — круг у игровой площадки
        Transform games = All(scene).FirstOrDefault(t => t.name == "Zone_Games");
        Vector3 gc = games != null ? games.position : spawn;
        if (FindSpotWhere(gc, 4f, 20f, 1.6f, q => FarFromNpc(q, 3.5f), out Vector3 op))
        {
            var ordo = Instance(M + "Ordo_Circle.obj", root, op + Vector3.up * 0.02f, Quaternion.Euler(0f, 20f, 0f));
            if (ordo != null) { ordo.name = "Ordo_Circle"; ordo.AddComponent<OrdoStation>(); }
            log4.Add($"ордо: ({op.x:0}, {op.z:0})");
        }
        else log4.Add("ордо: не нашлось места");

        // ---- 6. Бешбармак — казан у кухни, продукты у базара
        Transform kitchen = All(scene).FirstOrDefault(t => t.name == "Kitchen") ?? All(scene).FirstOrDefault(t => t.name == "Zone_Food");
        Vector3 kc = kitchen != null ? kitchen.position : spawn;
        if (FindSpotWhere(kc, 3f, 14f, 1.0f, q => FarFromNpc(q, 2.5f), out Vector3 cp))
        {
            var kaz = Instance(M + "Cook_Kazan.obj", root, cp, Face(cp, spawn));
            if (kaz != null)
            {
                kaz.name = "Cook_Kazan"; kaz.AddComponent<CookStation>();
                var bc = kaz.AddComponent<BoxCollider>(); bc.size = new Vector3(1.4f, 1.2f, 1.4f); bc.center = new Vector3(0f, 0.6f, 0f);
                var lgo = new GameObject("FireGlow"); lgo.transform.SetParent(kaz.transform, false); lgo.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                var l = lgo.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.55f, 0.25f); l.range = 4f; l.intensity = 1.5f; l.shadows = LightShadows.None;
            }
            log4.Add($"казан: ({cp.x:0}, {cp.z:0})");
        }
        else log4.Add("казан: не нашлось места");
        Transform bazaar = All(scene).FirstOrDefault(t => t.name == "Bazaar");
        if (bazaar != null && FindSpotWhere(bazaar.position, 2.5f, 12f, 0.7f, q => FarFromNpc(q, 1.8f), out Vector3 gp))
        {
            var gr = Instance(M + "Cook_Groceries.obj", root, gp, Face(gp, bazaar.position));
            if (gr != null) { gr.name = "Cook_Groceries"; gr.AddComponent<GroceriesSpot>(); }
            log4.Add($"продукты: ({gp.x:0}, {gp.z:0})");
        }
        else log4.Add("продукты: нет базара или места");

        // ---- 7. Убранство юрты — точка у входа собранной юрты
        Transform built = All(scene).FirstOrDefault(t => t.name == "BuiltKerege");
        Transform aks = All(scene).FirstOrDefault(t => t.name == "Aksakal");
        Transform zy = All(scene).FirstOrDefault(t => t.name == "Zone_Yurt");
        Transform bp = All(scene).FirstOrDefault(t => t.name == "BuildPoint");
        Vector3 yc = bp != null ? bp.position : zy != null ? zy.position : spawn; float yr = 3.5f;
        if (built != null)
        {
            bool any = false; Bounds b = default;
            foreach (var mf in built.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Bounds lb = mf.sharedMesh.bounds; var m = mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = m.MultiplyPoint3x4(lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (!any) { b = new Bounds(corner, Vector3.zero); any = true; } else b.Encapsulate(corner);
                }
            }
            if (any) { yc = new Vector3(b.center.x, b.min.y, b.center.z); yr = Mathf.Min(b.extents.x, b.extents.z); }
        }
        Vector3 door = aks != null ? Flat(aks.position - yc) : Vector3.forward;
        door = door.sqrMagnitude > 0.01f ? door.normalized : Vector3.forward;
        Vector3 sp = yc + door * (yr + 1.2f); GroundY(sp, out float syy); sp.y = syy;
        var stn = new GameObject("YurtDecorStation");
        stn.transform.SetParent(root, true); stn.transform.position = sp; stn.transform.rotation = Face(sp, yc);
        var yd = stn.AddComponent<YurtDecorStation>(); yd.yurtBuilt = built; yd.doorHint = aks;
        log4.Add($"убранство юрты: точка ({sp.x:0}, {sp.z:0}), радиус юрты {yr:0.0} м" + (built == null ? " (BuiltKerege не найден)" : ""));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Update v4] " + string.Join(" · ", log4));
    }

    static void TpTo(Component c, float back) { if (Playing() && c != null) Teleport(c.transform.position - c.transform.forward * back + Vector3.up * 0.2f); }
    [MenuItem("Tools/Diploma/Play Mode: Story/Kok-boru: teleport")] static void TTpKb() { TpTo(Object.FindAnyObjectByType<KokBoruStarter>(), 2.5f); }
    [MenuItem("Tools/Diploma/Play Mode: Story/Kok-boru: start")] static void TKb() { var s = Object.FindAnyObjectByType<KokBoruStarter>(); if (Playing() && s != null) s.Play(); }
    [MenuItem("Tools/Diploma/Play Mode: Story/Kok-boru: quit")] static void TKbQ() { if (Playing() && KokBoruGame.Instance != null && KokBoruGame.Active) KokBoruGame.Instance.End(false); }
    [MenuItem("Tools/Diploma/Play Mode: Story/Ordo: teleport")] static void TTpOrdo() { TpTo(Object.FindAnyObjectByType<OrdoStation>(), 3.5f); }
    [MenuItem("Tools/Diploma/Play Mode: Story/Ordo: open")] static void TOrdo() { if (Playing()) OrdoGame.Ensure().Open(); }
    [MenuItem("Tools/Diploma/Play Mode: Story/Cook: give groceries + open")] static void TCook() { if (Playing()) { foreach (string n in new[] { "et", "un", "piyaz" }) if (Inventory.Count(n) < 1) Inventory.Add(n, 1, false); CookingGame.Ensure().Open(); } }
    [MenuItem("Tools/Diploma/Play Mode: Story/Cook: teleport to kazan")] static void TTpCook() { TpTo(Object.FindAnyObjectByType<CookStation>(), 2f); }
    [MenuItem("Tools/Diploma/Play Mode: Story/Yurt decor: teleport")] static void TTpYd() { TpTo(Object.FindAnyObjectByType<YurtDecorStation>(), -1.5f); }
    [MenuItem("Tools/Diploma/Play Mode: Story/Yurt decor: start + give all")]
    static void TYd()
    {
        if (!Playing()) return;
        if (SaveData.Get("yd") == 0) SaveData.Set("yd", 1);
        foreach (var it in YurtDecor.Items) if (!YurtDecor.Placed(it) && Inventory.Count(it.inv) < 1) { SaveData.Set("yd_g_" + it.id, 1); Inventory.Add(it.inv, 1, false); }
        YurtDecor.UpdateHud();
    }
    [MenuItem("Tools/Diploma/Play Mode: Story/Yurt decor: place all")]
    static void TYdPlace()
    {
        if (!Playing()) return;
        foreach (var it in YurtDecor.Items) SaveData.Set("yd_" + it.id, 1);
        var s = Object.FindAnyObjectByType<YurtDecorStation>(); if (s != null) s.SpawnPlaced();
    }

    // ======================================================================== More houses (v5)
    class HouseSpec { public float floor; public Vector3 door, porch, light, chimney; public Vector4 yard; public (string m, Vector3 p, float yaw)[] fur; }
    static readonly Dictionary<string, HouseSpec> HouseSpecs = new Dictionary<string, HouseSpec>
    {
        { "KG_House_TwoStory", new HouseSpec { floor = 0.5f, door = new Vector3(1.400f, 0.500f, 3.350f), porch = new Vector3(0.900f, 3.050f, 4.000f), light = new Vector3(0.000f, 2.400f, 0.000f), chimney = new Vector3(-2.400f, 8.520f, -1.400f), yard = new Vector4(-4.0f, 4.0f, -3.5f, 3.5f),
            fur = new[] { ("Interior_Shyrdak", new Vector3(-0.000f, 0.512f, -0.200f), 0f), ("Interior_Dastorkon", new Vector3(-0.000f, 0.527f, -0.200f), 0f), ("Interior_Tushkiyiz", new Vector3(-0.000f, 1.950f, -3.180f), 0f), ("Interior_Juk", new Vector3(-3.020f, 0.500f, -2.870f), 0f), ("Interior_Stove", new Vector3(3.120f, 0.500f, -2.770f), 0f), ("Interior_Bed", new Vector3(-3.220f, 0.500f, 1.800f), 0f), ("Interior_Cupboard", new Vector3(3.470f, 0.500f, 1.300f), -90f), ("Interior_Lamp", new Vector3(-0.000f, 3.240f, 0.000f), 0f) } } },
        { "KG_House_Adobe", new HouseSpec { floor = 0.3f, door = new Vector3(0.700f, 0.300f, 2.450f), porch = new Vector3(0.200f, 2.600f, 3.100f), light = new Vector3(0.000f, 2.200f, 0.000f), chimney = new Vector3(2.600f, 4.150f, -1.000f), yard = new Vector4(-3.7f, 3.7f, -2.6f, 2.6f),
            fur = new[] { ("Interior_Shyrdak", new Vector3(-0.000f, 0.312f, -0.200f), 0f), ("Interior_Dastorkon", new Vector3(-0.000f, 0.327f, -0.200f), 0f), ("Interior_Tushkiyiz", new Vector3(-0.000f, 1.750f, -2.280f), 0f), ("Interior_Juk", new Vector3(-2.720f, 0.300f, -1.970f), 0f), ("Interior_Stove", new Vector3(2.820f, 0.300f, -1.870f), 0f), ("Interior_Bed", new Vector3(-2.920f, 0.300f, 0.900f), 0f), ("Interior_Cupboard", new Vector3(3.170f, 0.300f, 0.400f), -90f), ("Interior_Lamp", new Vector3(-0.000f, 2.640f, 0.000f), 0f) } } },
        { "KG_House_Veranda", new HouseSpec { floor = 0.6f, door = new Vector3(-2.500f, 0.600f, 2.850f), porch = new Vector3(-3.000f, 3.150f, 3.500f), light = new Vector3(0.000f, 2.500f, 0.000f), chimney = new Vector3(2.500f, 5.700f, -1.000f), yard = new Vector4(-5.0f, 5.0f, -3.0f, 3.0f),
            fur = new[] { ("Interior_Shyrdak", new Vector3(-0.000f, 0.612f, -0.200f), 0f), ("Interior_Dastorkon", new Vector3(-0.000f, 0.627f, -0.200f), 0f), ("Interior_Tushkiyiz", new Vector3(-0.000f, 2.050f, -2.680f), 0f), ("Interior_Juk", new Vector3(4.020f, 0.600f, -2.370f), 0f), ("Interior_Stove", new Vector3(-4.120f, 0.600f, -2.270f), 0f), ("Interior_Bed", new Vector3(4.220f, 0.600f, 1.300f), 0f), ("Interior_Cupboard", new Vector3(-4.470f, 0.600f, 0.800f), 90f), ("Interior_Lamp", new Vector3(-0.000f, 3.240f, 0.000f), 0f) } } },
        { "KG_House_Brick", new HouseSpec { floor = 0.45f, door = new Vector3(-1.100f, 0.450f, 2.650f), porch = new Vector3(-1.600f, 3.000f, 3.300f), light = new Vector3(0.000f, 2.350f, 0.000f), chimney = new Vector3(1.500f, 5.150f, -0.800f), yard = new Vector4(-3.2f, 3.2f, -2.8f, 2.8f),
            fur = new[] { ("Interior_Shyrdak", new Vector3(-0.000f, 0.462f, -0.200f), 0f), ("Interior_Dastorkon", new Vector3(-0.000f, 0.477f, -0.200f), 0f), ("Interior_Tushkiyiz", new Vector3(-0.000f, 1.900f, -2.480f), 0f), ("Interior_Juk", new Vector3(2.220f, 0.450f, -2.170f), 0f), ("Interior_Stove", new Vector3(-2.320f, 0.450f, -2.070f), 0f), ("Interior_Bed", new Vector3(2.420f, 0.450f, 1.100f), 0f), ("Interior_Cupboard", new Vector3(-2.670f, 0.450f, 0.600f), 90f), ("Interior_Lamp", new Vector3(-0.000f, 2.990f, 0.000f), 0f) } } },
    };

    static readonly (string model, float hw, float hd)[] NewHouses =
    {
        ("KG_House_TwoStory", 4.6f, 5.0f), ("KG_House_Adobe", 4.2f, 4.8f), ("KG_House_Veranda", 5.4f, 4.6f), ("KG_House_Brick", 3.8f, 4.0f),
        ("KG_House_TwoStory", 4.6f, 5.0f), ("KG_House_Brick", 3.8f, 4.0f), ("KG_House_Veranda", 5.4f, 4.6f), ("KG_House_Adobe", 4.2f, 4.8f),
    };
    static readonly string[] YardSet = { "Yard_Firewood", "Yard_Tandyr", "Yard_Clothesline", "Yard_ShyrdakFence", "Yard_Barrel", "Yard_Haystack" };

    [MenuItem("Tools/Diploma/Add More Houses (two-storey, adobe, veranda, brick + darbaza)")]
    public static void InstallHouses()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        valley = All(scene).FirstOrDefault(t => t.name == "MountainValley");
        foreach (Transform t in All(scene).Where(t => t.name == "Village_Houses_More").ToList()) if (t != null) Object.DestroyImmediate(t.gameObject);
        Physics.SyncTransforms();
        BuildRiver(scene);
        const string HD = "Assets/Game/Models/Houses/";
        var logH = new List<string>();
        var rnd = new System.Random(5);

        Transform env = All(scene).FirstOrDefault(t => t.name == "Environment");
        var root = new GameObject("Village_Houses_More").transform; root.SetParent(env, false);
        Transform vh = All(scene).FirstOrDefault(t => t.name == "Village_Houses");
        var old = vh != null ? vh.Cast<Transform>().Where(t => t.name.StartsWith("House_")).Select(t => t.position).ToList() : new List<Vector3>();
        if (old.Count < 2) { Debug.LogWarning("[Houses] Нет Village_Houses — сначала Build Village Life"); return; }
        float roadX = old.Average(p => p.x);
        float leftX = old.Where(p => p.x < roadX).DefaultIfEmpty(new Vector3(roadX - 16f, 0, 0)).Average(p => p.x);
        float rightX = old.Where(p => p.x >= roadX).DefaultIfEmpty(new Vector3(roadX + 16f, 0, 0)).Average(p => p.x);
        float zMin = old.Min(p => p.z), zMax = old.Max(p => p.z), y0 = old.Average(p => p.y);
        Material smoke = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "ChimneySmoke.mat");

        var avoid = new List<(Vector3 p, float r)>();
        foreach (Transform t in All(scene))
        {
            string n = t.name;
            if (n.StartsWith("House_") && t.parent == vh) avoid.Add((t.position, 12f));
            else if (n.StartsWith("Zone_")) avoid.Add((t.position, 16f));
            else if (n == "RaceTrack") avoid.Add((t.position, 40f));
            else if (n == "Farm" || n == "Bazaar") avoid.Add((t.position, 18f));
            else if (n == "NPC_Aibek" || n == "Aksakal" || n == "Ezhe" || n == "Kitchen") avoid.Add((t.position, 9f));
            else if ((n.StartsWith("NPC_") || t.GetComponent<VillagerTalk>() != null) && t.parent != null && !t.parent.name.StartsWith("NPC_")) avoid.Add((t.position, 5f));
            else if (t.parent != null && (t.parent.name == "Update_v3" || t.parent.name == "Update_v4")) avoid.Add((t.position, 6f));
            else if (t.parent != null && t.parent.parent == env && (t.parent.name.StartsWith("Trees") || t.parent.name == "Mountains" || t.parent.name == "Decorations"))
            {
                var rr = t.GetComponentInChildren<Renderer>();
                if (rr != null && rr.bounds.size.magnitude > 3f) avoid.Add((t.position, Mathf.Min(6f, Mathf.Max(rr.bounds.extents.x, rr.bounds.extents.z) * 0.6f + 0.8f)));
            }
            else if (t.name == "StreetLamp" || t.name.StartsWith("Race_") || t.name == "Campfire") avoid.Add((t.position, 3f));
        }
        Vector2 F(Vector3 v) => new Vector2(v.x, v.z);
        bool Clear(Vector3 q, float r) => avoid.All(a => Vector2.Distance(F(q), F(a.p)) > a.r + r) && !NearRiver(q, r + 2f);

        var slots = new List<(Vector3 p, float side)>();
        for (int k = 1; k <= 3; k++)
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side < 0 ? leftX : rightX;
                slots.Add((new Vector3(x, y0, zMax + 20f * k), side));
                slots.Add((new Vector3(x, y0, zMin - 20f * k), side));
            }
        for (float z = zMin; z <= zMax; z += 22f)
        {
            slots.Add((new Vector3(leftX - 18f, y0, z + 8f), -1f));
            slots.Add((new Vector3(rightX + 18f, y0, z - 4f), 1f));
        }

        int hi = 0, placed = 0, gates = 0, animals = 0;
        foreach (var slot in slots)
        {
            if (hi >= NewHouses.Length) break;
            var spec = NewHouses[hi];
            HouseSpecs.TryGetValue(spec.model, out HouseSpec hs);
            float r = Mathf.Max(spec.hw, spec.hd);
            if (!FindSpotWhere(slot.p, 0f, 12f, r * 0.85f, q => Clear(q, r + 2.5f), out Vector3 at)) continue;
            Vector3 fwd = new Vector3(roadX - at.x, 0f, 0f).normalized; if (fwd.sqrMagnitude < 0.5f) fwd = Vector3.right * -slot.side;
            Quaternion rot = Quaternion.LookRotation(fwd) * Quaternion.Euler(0f, (float)(rnd.NextDouble() * 10.0 - 5.0), 0f);
            // высота: пол дома выше самой высокой земли внутри, низ — не выше самой низкой точки по углам
            float low = float.MaxValue, highIn = float.MinValue;
            for (float u = -1f; u <= 1.01f; u += 0.25f)
                for (float w = -1f; w <= 1.01f; w += 0.25f)
                    if (GroundY(at + rot * new Vector3(u * (spec.hw - 0.5f), 0f, w * (spec.hd - 1.2f)), out float gy)) { low = Mathf.Min(low, gy); highIn = Mathf.Max(highIn, gy); }
            if (low == float.MaxValue) { low = at.y; highIn = at.y; }
            float floorH = hs != null ? hs.floor : 0.5f;
            float baseY = Mathf.Max(low, highIn - floorH + 0.12f);
            var house = Instance(HD + spec.model + ".obj", root, new Vector3(at.x, baseY, at.z), rot);
            if (house == null) { hi++; continue; }
            house.name = "House_More_" + (placed + 1) + "_" + spec.model.Replace("KG_House_", "");
            foreach (var mf in house.GetComponentsInChildren<MeshFilter>()) if (mf.sharedMesh != null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            GameObjectUtility.SetStaticEditorFlags(house, StaticEditorFlags.BatchingStatic);
            // фундамент под домом, если земля ниже (не висит в воздухе)
            if (baseY - low > 0.05f)
            {
                var fnd = GameObject.CreatePrimitive(PrimitiveType.Cube); fnd.name = "Foundation";
                Object.DestroyImmediate(fnd.GetComponent<Collider>());
                fnd.transform.SetParent(house.transform, false);
                float hgt = baseY - low + 0.6f;
                fnd.transform.localPosition = new Vector3(0f, 0.02f - hgt / 2f, 0f);
                fnd.transform.localScale = new Vector3(hs != null ? hs.yard.y - hs.yard.x + 0.1f : spec.hw * 2f, hgt, hs != null ? hs.yard.w - hs.yard.z + 0.1f : spec.hd * 2f);
                var pr = house.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(x => x != fnd.GetComponent<MeshRenderer>());
                var pm = pr != null ? pr.sharedMaterials.FirstOrDefault(m => m != null && (m.name.Contains("PlinthDark") || m.name.Contains("ClayDark"))) : null;
                if (pm != null) fnd.GetComponent<MeshRenderer>().sharedMaterial = pm;
            }
            ClearSmallDecor(scene, house);

            if (hs != null)
            {
                // свет у крыльца и в окнах ночью
                var lightGo = new GameObject("PorchLight"); lightGo.transform.SetParent(house.transform, false); lightGo.transform.localPosition = hs.porch;
                var pl = lightGo.AddComponent<Light>(); pl.type = LightType.Point; pl.range = 9f; pl.color = new Color(1f, 0.72f, 0.42f); pl.intensity = 0f; pl.shadows = LightShadows.None; pl.enabled = false;
                var hl = house.AddComponent<HouseLights>(); hl.porchLight = pl;
                // обстановка внутри
                var room = new GameObject("Interior").transform; room.SetParent(house.transform, false);
                foreach (var f in hs.fur)
                {
                    var go = Instance(HD + f.m + ".obj", room, Vector3.zero, Quaternion.identity);
                    if (go == null) continue;
                    go.transform.localPosition = f.p; go.transform.localRotation = Quaternion.Euler(0f, f.yaw, 0f);
                    if (f.m == "Interior_Juk" || f.m == "Interior_Stove" || f.m == "Interior_Bed" || f.m == "Interior_Cupboard")
                    {
                        Bounds b = LocalBoundsOf(go); var bc = go.AddComponent<BoxCollider>(); bc.center = b.center; bc.size = b.size;
                    }
                }
                var lg = new GameObject("InteriorLight"); lg.transform.SetParent(room, false); lg.transform.localPosition = hs.light;
                var il = lg.AddComponent<Light>(); il.type = LightType.Point; il.range = 7.5f; il.intensity = 0.55f; il.color = new Color(1f, 0.82f, 0.6f); il.shadows = LightShadows.None;
                hl.interiorLight = il;
                // дверь на петле
                var door = Instance(HD + "KG_Door.obj", house.transform, Vector3.zero, Quaternion.identity);
                if (door != null) { door.name = "Door"; door.transform.localPosition = hs.door; door.transform.localRotation = Quaternion.identity; door.AddComponent<HouseDoor>(); }
                // дым из трубы
                if (smoke != null) AddChimneySmoke(house.transform, hs.chimney, smoke);
            }

            // двор: сарай сзади, 3 вещи по бокам, лавочка у двери
            var yard = new GameObject("Yard").transform; yard.SetParent(house.transform, false);
            float hw = spec.hw, hd = hs != null ? hs.yard.w : spec.hd;
            YardProp(yard, HD + "KG_Saray.obj", new Vector3(1.8f, 0f, -hd - 4.6f), 180f, true);
            var picks = YardSet.OrderBy(_ => rnd.Next()).Take(3).ToList();
            var spots = new[] { (new Vector3(-hw - 1.6f, 0f, 0.4f), 90f), (new Vector3(hw + 1.6f, 0f, -0.6f), -90f), (new Vector3(-1.8f, 0f, -hd - 2.2f), 0f) };
            for (int k = 0; k < picks.Count; k++) YardProp(yard, HD + picks[k] + ".obj", spots[k].Item1, spots[k].Item2 + (float)(rnd.NextDouble() * 20 - 10), picks[k] != "Yard_Clothesline" && picks[k] != "Yard_ShyrdakFence");
            if (hs != null) YardProp(yard, HD + "Yard_Bench.obj", new Vector3(hs.door.x + 1.7f, 0f, hs.yard.w + 0.5f), 180f, false);
            // животное во дворе: собака или козы
            string beast = placed % 3 == 0 ? "Animal_Dog" : placed % 3 == 1 ? "Animal_Goat" : null;
            if (beast != null)
            {
                var area = new GameObject("YardArea").transform; area.SetParent(yard, false); area.localPosition = new Vector3(-2.5f, 0f, -hd - 2.2f);
                Vector3 w0 = area.position;
                if (GroundY(w0, out float ay))
                {
                    var a = new GameObject(beast.Replace("Animal_", "")); a.transform.SetParent(yard, true);
                    a.transform.position = new Vector3(w0.x, ay, w0.z); a.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                    var m = Instance("Assets/Game/Models/Farm/" + beast + ".obj", a.transform, a.transform.position, a.transform.rotation);
                    var aw = a.AddComponent<AnimalWander>(); aw.area = area; aw.halfSize = new Vector2(hw + 1f, 1.8f);
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/SFX/" + (beast == "Animal_Dog" ? "dog_1" : "goat_1") + ".wav");
                    aw.calls = clip != null ? new[] { clip } : new AudioClip[0];
                    if (beast == "Animal_Dog") { aw.walkSpeed = 1.2f; aw.idleTime = new Vector2(2f, 5f); aw.stepRate = 9f; aw.grazeTilt = 0f; aw.callPause = new Vector2(30f, 70f); }
                    else { aw.walkSpeed = 0.6f; aw.callPause = new Vector2(25f, 55f); }
                    animals++;
                }
            }
            avoid.Add((at, r + 4f));
            placed++; hi++;

            if (placed % 2 == 1)
            {
                Vector3 gp = at + fwd * (spec.hd + 4.6f);
                if (GroundY(gp, out float gy2) && !NearRiver(gp, 2f) && avoid.Where(a => a.p != at).All(a => Vector2.Distance(F(gp), F(a.p)) > a.r * 0.6f))
                {
                    var g = Instance(HD + "KG_Darbaza.obj", root, new Vector3(gp.x, gy2, gp.z), rot);
                    if (g != null)
                    {
                        g.name = "Darbaza_" + placed;
                        // коллайдеры только у столбов и забора; створки ворот проходимы — это вход во двор
                        foreach (var mf in g.GetComponentsInChildren<MeshFilter>()) if (mf.sharedMesh != null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                        gates++;
                    }
                }
            }
            logH.Add($"{spec.model.Replace("KG_House_", "")} ({at.x:0}, {at.z:0})");
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Houses] Новых домов: {placed}, дарбаза: {gates}, животных: {animals} · " + string.Join(" · ", logH));
    }

    static Bounds LocalBoundsOf(GameObject go)
    {
        var b = new Bounds(); bool first = true;
        foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            Matrix4x4 m = go.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            Bounds mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                if (first) { b = new Bounds(c, Vector3.zero); first = false; } else b.Encapsulate(c);
            }
        }
        return b;
    }

    static void YardProp(Transform parent, string path, Vector3 local, float yaw, bool collider)
    {
        Vector3 w = parent.TransformPoint(local);
        if (!GroundY(w, out float y)) return;
        foreach (Collider c in Physics.OverlapSphere(new Vector3(w.x, y + 0.6f, w.z), 0.6f, ~0, QueryTriggerInteraction.Collide))
            if (!IsGround(c) && !IsPath(c) && !c.transform.IsChildOf(parent.parent)) return;
        var go = Instance(path, parent, new Vector3(w.x, y, w.z), parent.rotation * Quaternion.Euler(0f, yaw, 0f));
        if (go == null) return;
        if (collider) { Bounds b = LocalBoundsOf(go); var bc = go.AddComponent<BoxCollider>(); bc.center = b.center; bc.size = b.size; }
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }

    static void ClearSmallDecor(Scene scene, GameObject house)
    {
        Physics.SyncTransforms();
        var b = new Bounds(house.transform.position, Vector3.zero);
        foreach (Renderer r in house.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        b.Expand(new Vector3(1f, 10f, 1f));
        var kill = new List<GameObject>();
        foreach (string rn in new[] { "Decor_Trail", "Decor_Valley", "Decorations" })
        {
            Transform root = All(scene).FirstOrDefault(t => t.name == rn && t.parent != null);
            if (root == null) continue;
            foreach (Transform t in root.GetComponentsInChildren<Transform>())
            {
                if (t == root || t.parent != root && t.parent.parent != root) continue;
                Renderer r = t.GetComponentInChildren<Renderer>();
                if (r == null || r.bounds.size.magnitude > 3.5f) continue;
                if (b.Contains(r.bounds.center) && !t.name.Contains("Fence") && !t.name.Contains("Floor_Tile") && !t.name.Contains("Meadow") && !t.name.Contains("Road"))
                    kill.Add(PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject) ?? t.gameObject);
            }
        }
        foreach (GameObject g in kill.Distinct()) if (g != null) Object.DestroyImmediate(g);
    }

    static void AddChimneySmoke(Transform house, Vector3 local, Material mat)
    {
        var go = new GameObject("ChimneySmoke");
        go.transform.SetParent(house, false); go.transform.localPosition = local; go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f); main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f); main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(0.85f, 0.85f, 0.85f, 0.5f); main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60; main.gravityModifier = -0.01f; main.prewarm = true;
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
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.2f; noise.frequency = 0.3f;
        var pr = go.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = mat;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; pr.receiveShadows = false;
    }

    [MenuItem("Tools/Diploma/Fix House Floors (old houses)")]
    public static void FixHouseFloors()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform vh = All(scene).FirstOrDefault(t => t.name == "Village_Houses");
        int n = 0;
        if (vh != null)
            foreach (Transform t in vh.GetComponentsInChildren<Transform>(true))
            {
                // ковёр и дастаркон чуть выше пола — без мерцания
                if (t.name.StartsWith("Interior_Shyrdak") && t.parent != null && t.parent.name == "Interior") { var p = t.localPosition; p.y = 0.55f + 0.012f; t.localPosition = p; n++; }
                if (t.name.StartsWith("Interior_Dastorkon") && t.parent != null && t.parent.name == "Interior") { var p = t.localPosition; p.y = 0.55f + 0.027f; t.localPosition = p; n++; }
            }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Floors] Пол в старых домах исправлен (модели), ковров поднято: {n}");
    }

    static void TpHouse(int i)
    {
        if (!Playing()) return;
        var h = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name.StartsWith("House_More_")).OrderBy(t => t.name).ElementAtOrDefault(i);
        if (h != null) Teleport(h.TransformPoint(new Vector3(0f, 0.6f, 0f)));
    }
    [MenuItem("Tools/Diploma/Play Mode: Story/Houses: inside new house 1")] static void TH1() { TpHouse(0); }
    [MenuItem("Tools/Diploma/Play Mode: Story/Houses: inside new house 2")] static void TH2() { TpHouse(1); }
    [MenuItem("Tools/Diploma/Play Mode: Story/Houses: inside new house 3")] static void TH3() { TpHouse(2); }
    [MenuItem("Tools/Diploma/Play Mode: Story/Houses: inside new house 4")] static void TH4() { TpHouse(3); }

    static void DailyProp(Transform root, string model, Vector3 p, DailyTasks.T task, string item, int bit, bool reusable)
    {
        var go = Instance(M + model + ".obj", root, p, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        if (go == null) return;
        var spot = go.AddComponent<DailySpot>();
        spot.task = task; spot.item = item; spot.bit = bit; spot.reusable = reusable;
    }

    static bool FindSpotWhere(Vector3 c, float rMin, float rMax, float half, System.Func<Vector3, bool> ok, out Vector3 spot)
    {
        for (float r = rMin; r <= rMax; r += 1f)
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI * 2f / 16f + r;
                Vector3 p = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (ok(p) && Free(p, half, 0.4f, out float gy)) { spot = new Vector3(p.x, gy, p.z); return true; }
            }
        spot = c; return false;
    }

    static Material Emissive(string name, Color c, float intensity)
    {
        string path = MatDir + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", c);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", c * intensity);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(m);
        return m;
    }

    [MenuItem("Tools/Diploma/Play Mode: Story/Manas: open story")] static void TManas() { if (Playing()) ManasStory.OpenNearest(); }
    [MenuItem("Tools/Diploma/Play Mode: Story/Teleport To Manaschy")] static void TTpManas() { var m = Object.FindAnyObjectByType<ManasStory>(); if (Playing() && m != null) Teleport(m.transform.position + m.transform.forward * 3.4f); }
    [MenuItem("Tools/Diploma/Play Mode: Story/Teleport To Magpie Nest")] static void TTpNest() { var m = Object.FindAnyObjectByType<HeadphonesClue>(); if (Playing() && m != null) Teleport(m.transform.position + new Vector3(1.6f, 0f, 0f)); }
    [MenuItem("Tools/Diploma/Play Mode: Story/Detective: next step")]
    static void TDetective()
    {
        if (!Playing()) return;
        string who = HeadphonesQuest.Stage switch { 0 => "Бунтарь", 1 => "Актан", 2 => SaveData.Get("hq_apa") == 0 ? "Апа" : SaveData.Get("hq_baike") == 0 ? "Байке" : SaveData.Get("hq_chaban") == 0 ? "Чабан" : null, 3 => "Бунтарь", 4 => "Актан", _ => null };
        if (who == null && HeadphonesQuest.Stage == 2) { HeadphonesQuest.FoundHeadphones(); return; }
        if (who != null && QuestTalk.TryHandle(who, out string line)) QuestTalk.Say(who, line);
    }
    [MenuItem("Tools/Diploma/Play Mode: Story/Daily: new day")] static void TDailyDay() { if (Playing()) DailyTasks.TestNewDay(); }
    [MenuItem("Tools/Diploma/Play Mode: Story/Daily: finish one task")]
    static void TDailyOne()
    {
        if (!Playing() || DailyTasks.Instance == null) return;
        for (int i = 0; i < 3; i++) { var t = DailyTasks.TaskAt(i); if (!DailyTasks.Done(t)) { DailyTasks.Instance.Advance(t, 3); return; } }
    }

    // ================================================================== Разбросать деревья и камни у дороги по всему селу

    [MenuItem("Tools/Diploma/Scatter Trees + Rocks Around Village")]
    public static void ScatterTrees()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        valley = All(scene).FirstOrDefault(t => t.name == "MountainValley");
        BuildRiver(scene);
        Physics.SyncTransforms();

        Transform env = All(scene).FirstOrDefault(t => t.name == "Environment");
        if (env == null) { Debug.LogWarning("[Scatter] Нет Environment"); return; }
        var items = new List<Transform>();
        bool TreeOrRock(string n) => n.StartsWith("PP_Rock") || n.StartsWith("PP_Tree") || n.StartsWith("PP_Birch") || n.StartsWith("PP_Pine") || System.Text.RegularExpressions.Regex.IsMatch(n, @"^Tree\d");
        foreach (string group in new[] { "Trees", "Trees (1)", "Mountains", "Decorations" })
            foreach (Transform g in env.Cast<Transform>().Where(c => c.name == group))
                foreach (Transform c in g)
                    if (group == "Trees" || group == "Trees (1)" || TreeOrRock(c.name)) items.Add(c);
        if (items.Count == 0) { Debug.LogWarning("[Scatter] Деревья и камни не найдены"); return; }

        GameObject player = All(scene).Select(t => t.GetComponent<PlayerMovement>()).Where(p => p != null).Select(p => p.gameObject).FirstOrDefault();
        Vector3 spawn = player != null ? player.transform.position : Vector3.zero;
        GroundY(spawn, out float baseY);

        // что нельзя загораживать: жители, костёр, гнёзда, фонари, трасса скачек, вход в юрту
        var keep = All(scene).Where(t => t.name.StartsWith("NPC_") || t.name == "Aksakal" || t.name == "Ezhe" || t.name.StartsWith("Child_NPC")
                                       || t.name == "Campfire" || t.name.StartsWith("Daily_") || t.name.StartsWith("Clue_") || t.name == "StreetLamp"
                                       || t.name.StartsWith("Race_") || t.name.StartsWith("Zone_") || t.name == "Bazaar" || t.name.StartsWith("Fishing"))
                             .Select(t => t.position).ToList();
        var race = All(scene).Select(t => t.GetComponent<RaceManager>()).FirstOrDefault(r => r != null);
        if (race != null && race.gates != null) keep.AddRange(race.gates.Where(g => g != null).Select(g => g.position));

        // временно выключаем коллайдеры переносимых объектов, чтобы они не мешали сами себе
        var cols = items.SelectMany(i => i.GetComponentsInChildren<Collider>()).Where(c => c.enabled).ToList();
        foreach (var c in cols) c.enabled = false;
        Physics.SyncTransforms();

        var rnd = new System.Random(2026);
        var placed = new List<Vector3>();
        int moved = 0;
        foreach (Transform it in items.OrderBy(_ => rnd.Next()))
        {
            bool rock = it.name.StartsWith("PP_Rock");
            for (int attempt = 0; attempt < 400; attempt++)
            {
                float ang = (float)(rnd.NextDouble() * Mathf.PI * 2);
                float r = 14f + (float)System.Math.Sqrt(rnd.NextDouble()) * 78f;      // равномерно по площади
                Vector3 q = spawn + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                if (!GroundY(q, out float gy) || Mathf.Abs(gy - baseY) > 5f) continue;          // не на горы
                if (NearRiver(q, rock ? 2.5f : 4f)) continue;
                if (placed.Any(p => (Flat(p) - Flat(q)).sqrMagnitude < (rock ? 7f * 7f : 6f * 6f))) continue;
                if (keep.Any(k => (Flat(k) - Flat(q)).sqrMagnitude < 7f * 7f)) continue;
                if (!Free(q, rock ? 1.6f : 1.1f, 0.6f, out gy)) continue;
                if (Physics.OverlapSphere(new Vector3(q.x, gy + 0.5f, q.z), 3f, ~0, QueryTriggerInteraction.Collide).Any(c => IsPath(c))) continue;  // не на дорогу
                if (OnRoad(q)) continue;
                it.position = new Vector3(q.x, gy - (rock ? 0.15f : 0.05f), q.z);
                it.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                it.localScale *= 0.9f + (float)rnd.NextDouble() * 0.25f;
                placed.Add(q); moved++;
                break;
            }
        }
        foreach (var c in cols) c.enabled = true;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Scatter] Перенесено {moved} из {items.Count} деревьев и камней — теперь они по всему селу.");
        MoveBirdsToTrees();
    }

    [MenuItem("Tools/Diploma/Move Bird Sounds To Trees")]
    public static void MoveBirdsToTrees()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) { if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return; scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); }
        Transform env = All(scene).FirstOrDefault(t => t.name == "Environment");
        Transform birds = All(scene).FirstOrDefault(t => t.name == "Birds");
        if (env == null || birds == null) { Debug.LogWarning("[Birds] Нет Environment или Birds"); return; }

        bool IsTree(string n) => n.StartsWith("PP_Tree") || n.StartsWith("PP_Birch") || n.StartsWith("PP_Pine") || System.Text.RegularExpressions.Regex.IsMatch(n, @"^Tree\d");
        var trees = env.Cast<Transform>().Where(g => g.name == "Trees" || g.name == "Trees (1)" || g.name == "Decorations")
                       .SelectMany(g => g.Cast<Transform>()).Where(t => IsTree(t.name)).ToList();
        if (trees.Count == 0) { Debug.LogWarning("[Birds] Деревья не найдены"); return; }

        var rnd = new System.Random(77);
        var order = trees.OrderBy(_ => rnd.Next()).ToList();
        int i = 0, moved = 0;
        foreach (Transform b in birds.Cast<Transform>().Where(b => b.name.StartsWith("Bird") || b.name.StartsWith("Owl")).ToList())
        {
            Transform tree = order[i % order.Count]; i++;
            // высота кроны: верх рендереров дерева
            float top = tree.position.y + 4f;
            var rs = tree.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0) top = rs.Max(r => r.bounds.max.y);
            float h = Mathf.Lerp(tree.position.y + 1.5f, top, 0.75f);
            Vector2 off = new Vector2((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f) * 1.2f;
            b.position = new Vector3(tree.position.x + off.x, h, tree.position.z + off.y);
            moved++;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Birds] Звуки птиц и сов ({moved}) пересажены на деревья ({Mathf.Min(moved, trees.Count)} деревьев).");
    }

    static readonly List<Bounds> roadBounds = new List<Bounds>();
    static bool OnRoad(Vector3 q)
    {
        if (roadBounds.Count == 0)
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                string n = r.name.ToLower();
                if (n.Contains("road") || n.Contains("meadow_path") || n.Contains("path_") || n.Contains("dirt")) roadBounds.Add(r.bounds);
            }
        foreach (Bounds b in roadBounds)
        {
            // широкие дороги-меши: проверяем по треугольникам грубо — по рамке, но только для узких объектов
            if (Mathf.Min(b.size.x, b.size.z) > 30f) continue;
            Bounds e = b; e.Expand(new Vector3(4f, 100f, 4f));
            if (e.Contains(new Vector3(q.x, b.center.y, q.z))) return true;
        }
        return RoadTris(q) < 3f;
    }

    static List<Vector2[]> roadTris2;
    static float RoadTris(Vector3 p)
    {
        if (roadTris2 == null)
        {
            roadTris2 = new List<Vector2[]>();
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                string n = mf.name.ToLower();
                if (mf.sharedMesh == null || !(n.Contains("road") || n.Contains("meadow_path"))) continue;
                Vector3[] v = mf.sharedMesh.vertices; int[] tr = mf.sharedMesh.triangles; Matrix4x4 m = mf.transform.localToWorldMatrix;
                for (int i = 0; i + 2 < tr.Length; i += 3)
                {
                    Vector3 a = m.MultiplyPoint3x4(v[tr[i]]), b = m.MultiplyPoint3x4(v[tr[i + 1]]), c = m.MultiplyPoint3x4(v[tr[i + 2]]);
                    roadTris2.Add(new[] { new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z) });
                }
            }
        }
        var q = new Vector2(p.x, p.z); float best = 999f;
        foreach (var t in roadTris2)
        {
            float d1 = (t[1].x - t[0].x) * (q.y - t[0].y) - (t[1].y - t[0].y) * (q.x - t[0].x);
            float d2 = (t[2].x - t[1].x) * (q.y - t[1].y) - (t[2].y - t[1].y) * (q.x - t[1].x);
            float d3 = (t[0].x - t[2].x) * (q.y - t[2].y) - (t[0].y - t[2].y) * (q.x - t[2].x);
            if (!((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0))) return 0f;
            for (int k = 0; k < 3; k++)
            {
                Vector2 a = t[k], b = t[(k + 1) % 3], ab = b - a;
                float u = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(q, a + ab * u));
            }
        }
        return best;
    }

    // ================================================================== общие помощники

    static void PrepareImports()
    {
        foreach (string p in new[] { "ui_portrait_komuzchu.png", "ui_portrait_burkutchu.png", "ui_portrait_chabandes.png" })
            if (AssetImporter.GetAtPath(Sprites + p) is TextureImporter ti && ti.textureType != TextureImporterType.Sprite)
            { ti.textureType = TextureImporterType.Sprite; ti.alphaIsTransparency = true; ti.SaveAndReimport(); }
        foreach (string p in new[] { Gen + "rain_streak.png", Gen + "snowflake.png" })
            if (AssetImporter.GetAtPath(p) is TextureImporter ti && !ti.alphaIsTransparency)
            { ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport(); }
        for (int i = 1; i <= 3; i++)
            if (AssetImporter.GetAtPath("Assets/Game/Resources/MURAS/shyrdak_" + i + ".png") is TextureImporter ti && (ti.mipmapEnabled || ti.wrapMode != TextureWrapMode.Clamp))
            { ti.mipmapEnabled = false; ti.wrapMode = TextureWrapMode.Clamp; ti.npotScale = TextureImporterNPOTScale.None; ti.SaveAndReimport(); }
        // длинные треки — потоком, короткие звуки — в память
        foreach (string p in new[] { "komuz_jailoo", "komuz_tulpar" })
            if (AssetImporter.GetAtPath("Assets/Game/Resources/MURAS/Audio/" + p + ".ogg") is AudioImporter ai)
            {
                var s = ai.defaultSampleSettings;
                if (s.loadType != AudioClipLoadType.Streaming) { s.loadType = AudioClipLoadType.Streaming; ai.defaultSampleSettings = s; ai.SaveAndReimport(); }
            }
    }

    static IEnumerable<Transform> All(Scene s) => s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true));
    static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);
    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    static Quaternion Face(Vector3 from, Vector3 to) { Vector3 d = to - from; d.y = 0f; return d.sqrMagnitude > 0.01f ? Quaternion.LookRotation(d) : Quaternion.identity; }

    static GameObject Instance(string path, Transform parent, Vector3 pos, Quaternion rot)
    {
        GameObject a = Load(path);
        if (a == null) { Debug.LogWarning("[FunPack] Нет модели " + path); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(a, parent);
        go.transform.position = pos; go.transform.rotation = rot;
        return go;
    }

    static void Prop(Transform parent, string path, Vector3 local, float yaw, float scale)
    {
        var go = Instance(path, parent, parent.TransformPoint(local), parent.rotation * Quaternion.Euler(0f, yaw, 0f));
        if (go != null) go.transform.localScale = Vector3.one * scale;
    }

    static GameObject Npc(Transform parent, string name, string model, Vector3 p, Quaternion rot)
    {
        GroundY(p, out float y); p.y = y;
        var npc = new GameObject(name);
        npc.transform.SetParent(parent, true);
        npc.transform.position = p; npc.transform.rotation = rot;
        var cap = npc.AddComponent<CapsuleCollider>(); cap.center = new Vector3(0f, 0.9f, 0f); cap.height = 1.8f; cap.radius = 0.3f;
        var m = Instance(model, npc.transform, p, rot);
        if (m != null)
        {
            m.name = name;
            var look = npc.AddComponent<NPCLookAtPlayer>(); look.model = m.transform;
        }
        return npc;
    }

    static bool FindSpot(Vector3 c, float rMin, float rMax, float half, out Vector3 spot)
    {
        for (float r = rMin; r <= rMax; r += 1.5f)
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI * 2f / 16f + r;
                Vector3 p = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (Free(p, half, 0.5f, out float gy)) { spot = new Vector3(p.x, gy, p.z); return true; }
            }
        spot = c; return false;
    }

    static bool Free(Vector3 p, float half, float maxSlope, out float gy)
    {
        if (!GroundY(p, out gy)) return false;
        foreach (Vector3 o in new[] { new Vector3(half, 0, 0), new Vector3(-half, 0, 0), new Vector3(0, 0, half), new Vector3(0, 0, -half) })
            if (!GroundY(p + o, out float y) || Mathf.Abs(y - gy) > maxSlope) return false;
        foreach (Collider col in Physics.OverlapBox(new Vector3(p.x, gy + 1.1f, p.z), new Vector3(half, 0.9f, half), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            if (!IsGround(col) && !IsPath(col)) return false;
        return true;
    }

    static bool GroundY(Vector3 p, out float y)
    {
        y = p.y; bool ok = false;
        foreach (RaycastHit h in Physics.RaycastAll(new Vector3(p.x, p.y + 80f, p.z), Vector3.down, 250f, ~0, QueryTriggerInteraction.Ignore))
            if (IsGround(h.collider) && (!ok || h.point.y > y)) { y = h.point.y; ok = true; }
        return ok;
    }

    static bool IsGround(Collider c) =>
        (valley != null ? c.transform.IsChildOf(valley) : c.name.Contains("Ground")) || c.name.Contains("PP_Meadow_0") || c.name.Contains("Lake_Ground");
    static bool IsPath(Collider c) => c.name.Contains("Road") || c.name.Contains("Meadow_Path") || c.name.Contains("Floor_Tile");
}
