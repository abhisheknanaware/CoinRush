using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CoinRushBuilder
{
    const string ScenesDir = "Assets/Scenes";
    const string PrefabsDir = "Assets/Prefabs";
    const string SpritesDir = "Assets/Sprites";
    const string MaterialsDir = "Assets/Materials";
    const string AudioDir = "Assets/Audio";
    const string UIDir = "Assets/UI";
    const string MenuScene = ScenesDir + "/MainMenu.unity";
    const string GameScene = ScenesDir + "/Game.unity";

    static Sprite sSquare, sCircle, sSoft, sPlayer, sEnemy, sCoin, sDirt, sGrass, sCrate, sFlag, sPanel;
    static Material spriteMat, particleMat, textOutlineMat;
    static PhysicsMaterial2D noFriction;
    static ParticleSystem fxCoin, fxHit, fxJump, fxVictory;
    static GameObject playerPrefab, coinPrefab, enemyPrefab, goalPrefab;

    static readonly Color Gold = new Color(1f, 0.82f, 0.2f);
    static readonly Color Sky = new Color(0.45f, 0.74f, 0.98f);
    static readonly Color Green = new Color(0.22f, 0.72f, 0.36f);
    static readonly Color Blue = new Color(0.22f, 0.5f, 0.95f);
    static readonly Color BoxColor = new Color(0.1f, 0.13f, 0.28f, 0.96f);

    // ---------- Entry points ----------

    [MenuItem("CoinRush/1. Import TMP Essentials")]
    public static void ImportTMP()
    {
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0)
        {
            Debug.Log("[CoinRush] TMP Essentials already present.");
            return;
        }
        string pkg = Path.GetFullPath("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage");
        if (!File.Exists(pkg)) throw new FileNotFoundException("TMP Essential Resources package not found", pkg);
        AssetDatabase.ImportPackage(pkg, false);
        AssetDatabase.Refresh();
        Debug.Log("[CoinRush] Imported TMP Essentials from " + pkg);
    }

    [MenuItem("CoinRush/2. Build Project Content")]
    public static void BuildAll()
    {
        try
        {
            if (TMP_Settings.defaultFontAsset == null)
                throw new Exception("TMP default font asset missing. Run 'Import TMP Essentials' first.");

            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
            PlayerSettings.productName = "Coin Rush";
            PlayerSettings.companyName = "CoinRush";

            foreach (string d in new[] { ScenesDir, PrefabsDir, SpritesDir, MaterialsDir, AudioDir, UIDir })
                EnsureFolder(d);

            CreateSprites();
            CreateMaterials();
            CreateAudio();
            CreateEffects();
            CreatePrefabs();
            BuildMenuScene();
            BuildGameScene();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScene, true),
                new EditorBuildSettingsScene(GameScene, true)
            };

            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(MenuScene);
            Debug.Log("[CoinRush] Project content built successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError("[CoinRush] Build content failed: " + e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else throw;
        }
    }

    [MenuItem("CoinRush/3. Build WebGL")]
    public static void BuildWebGL()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string output = Path.Combine(projectRoot, "Builds", "WebGL");
        Directory.CreateDirectory(output);

        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        PlayerSettings.runInBackground = true;

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { MenuScene, GameScene },
            locationPathName = output,
            target = BuildTarget.WebGL,
            options = Array.IndexOf(Environment.GetCommandLineArgs(), "-coinrushDev") >= 0 ? BuildOptions.Development : BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary s = report.summary;
        Debug.Log($"[CoinRush] WebGL build {s.result}: {s.totalSize / (1024f * 1024f):F1} MB, {s.totalErrors} errors, {s.totalWarnings} warnings, {s.totalTime}");
        if (s.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
    }

    // ---------- Assets ----------

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static Color Over(Color top, Color bottom)
    {
        float a = top.a + bottom.a * (1f - top.a);
        if (a <= 0f) return Color.clear;
        Color c = (top * top.a + bottom * bottom.a * (1f - top.a)) / a;
        c.a = a;
        return c;
    }

    static float Fill(float signedDistance, int size) => Mathf.Clamp01(0.5f - signedDistance * size);

    static float CircleSD(Vector2 p, Vector2 c, float r) => Vector2.Distance(p, c) - r;

    static float BoxSD(Vector2 p, Vector2 c, Vector2 half, float r)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + new Vector2(r, r);
        Vector2 qm = Vector2.Max(q, Vector2.zero);
        return qm.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
    }

    static Color Paint(Color baseColor, Color layer, float coverage)
    {
        layer.a *= coverage;
        return Over(layer, baseColor);
    }

    static Sprite MakeSprite(string folder, string name, int w, int h, Func<Vector2, Color> pixel,
        Vector4 border = default, bool tiled = false)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = pixel(new Vector2((x + 0.5f) / w, (y + 0.5f) / h));
        tex.SetPixels(px);
        tex.Apply();

        string path = $"{folder}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = Mathf.Max(w, h);
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.filterMode = FilterMode.Bilinear;
        imp.wrapMode = tiled ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        imp.spriteBorder = border;
        TextureImporterSettings settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(settings);
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void CreateSprites()
    {
        sSquare = MakeSprite(SpritesDir, "Square", 32, 32, p => Color.white);
        sCircle = MakeSprite(SpritesDir, "Circle", 128, 128, p => new Color(1, 1, 1, Fill(CircleSD(p, new Vector2(.5f, .5f), .48f), 128)));
        sSoft = MakeSprite(SpritesDir, "SoftCircle", 64, 64, p =>
        {
            float d = Mathf.Clamp01(1f - Vector2.Distance(p, new Vector2(.5f, .5f)) / .5f);
            return new Color(1, 1, 1, d * d);
        });

        const int N = 64;
        sPlayer = MakeSprite(SpritesDir, "Player", N, N, p =>
        {
            Color c = Color.clear;
            c = Paint(c, new Color(.08f, .2f, .45f), Fill(BoxSD(p, new Vector2(.5f, .47f), new Vector2(.42f, .45f), .18f), N));
            c = Paint(c, new Color(.25f, .58f, 1f), Fill(BoxSD(p, new Vector2(.5f, .48f), new Vector2(.37f, .4f), .15f), N));
            c = Paint(c, new Color(.45f, .72f, 1f), Fill(BoxSD(p, new Vector2(.5f, .7f), new Vector2(.28f, .12f), .1f), N));
            foreach (float ex in new[] { .38f, .64f })
            {
                c = Paint(c, Color.white, Fill(CircleSD(p, new Vector2(ex, .6f), .1f), N));
                c = Paint(c, new Color(.05f, .05f, .1f), Fill(CircleSD(p, new Vector2(ex + .035f, .59f), .05f), N));
            }
            c = Paint(c, new Color(1f, .55f, .6f, .8f), Fill(CircleSD(p, new Vector2(.75f, .42f), .05f), N));
            return c;
        });

        sEnemy = MakeSprite(SpritesDir, "Enemy", N, N, p =>
        {
            Color c = Color.clear;
            float dome = Mathf.Max(CircleSD(p, new Vector2(.5f, .32f), .44f), .12f - p.y);
            c = Paint(c, new Color(.45f, .05f, .15f), Fill(dome, N));
            float inner = Mathf.Max(CircleSD(p, new Vector2(.5f, .32f), .39f), .16f - p.y);
            c = Paint(c, new Color(.9f, .22f, .32f), Fill(inner, N));
            foreach (float fx in new[] { .3f, .7f })
                c = Paint(c, new Color(.35f, .05f, .1f), Fill(BoxSD(p, new Vector2(fx, .1f), new Vector2(.12f, .07f), .06f), N));
            foreach (float ex in new[] { .37f, .63f })
            {
                c = Paint(c, Color.white, Fill(CircleSD(p, new Vector2(ex, .46f), .09f), N));
                c = Paint(c, Color.black, Fill(CircleSD(p, new Vector2(ex - .02f, .44f), .045f), N));
            }
            float browL = Mathf.Abs((p.y - .58f) + (p.x - .37f) * .5f) - .02f;
            float browR = Mathf.Abs((p.y - .58f) - (p.x - .63f) * .5f) - .02f;
            if (Mathf.Abs(p.x - .37f) < .1f) c = Paint(c, Color.black, Fill(browL, N));
            if (Mathf.Abs(p.x - .63f) < .1f) c = Paint(c, Color.black, Fill(browR, N));
            c = Paint(c, new Color(.2f, 0, .05f), Fill(BoxSD(p, new Vector2(.5f, .28f), new Vector2(.15f, .04f), .03f), N));
            return c;
        });

        sCoin = MakeSprite(SpritesDir, "Coin", N, N, p =>
        {
            Vector2 ctr = new Vector2(.5f, .5f);
            Color c = Color.clear;
            c = Paint(c, new Color(.8f, .5f, .05f), Fill(CircleSD(p, ctr, .47f), N));
            c = Paint(c, Gold, Fill(CircleSD(p, ctr, .38f), N));
            c = Paint(c, new Color(.95f, .65f, .1f), Fill(BoxSD(p, ctr, new Vector2(.07f, .22f), .05f), N));
            c = Paint(c, new Color(1f, .97f, .7f, .9f), Fill(CircleSD(p, new Vector2(.36f, .64f), .07f), N));
            return c;
        });

        sDirt = MakeSprite(SpritesDir, "Dirt", N, N, p =>
        {
            Color c = new Color(.55f, .34f, .2f);
            float n = Mathf.PerlinNoise(p.x * 6f + 3f, p.y * 6f + 7f);
            c = Color.Lerp(c, new Color(.45f, .27f, .15f), (n > .62f ? 1f : 0f));
            float dots = Mathf.Sin(p.x * 37f) * Mathf.Sin(p.y * 29f);
            if (dots > .93f) c = new Color(.66f, .45f, .28f);
            return c;
        }, default, true);

        sGrass = MakeSprite(SpritesDir, "Grass", N, N / 2, p =>
        {
            float edge = .35f + .12f * Mathf.Sin(p.x * Mathf.PI * 8f);
            if (p.y < edge) return Color.clear;
            Color c = new Color(.3f, .78f, .3f);
            if (p.y > .8f) c = new Color(.45f, .9f, .4f);
            return c;
        }, default, true);

        sCrate = MakeSprite(SpritesDir, "Crate", N, N, p =>
        {
            Color dark = new Color(.42f, .25f, .1f);
            Color wood = new Color(.78f, .53f, .26f);
            bool border = p.x < .1f || p.x > .9f || p.y < .1f || p.y > .9f;
            bool plank = Mathf.Abs(p.x - p.y) < .07f || Mathf.Abs(p.x - (1f - p.y)) < .07f;
            return border || plank ? dark : wood;
        });

        sFlag = MakeSprite(SpritesDir, "Flag", N, N, p =>
        {
            float t = (p.y - .3f) / .7f;
            bool inside = t >= 0f && t <= 1f && p.x < .95f * (1f - Mathf.Abs(t - .5f) * 2f);
            return inside ? new Color(.2f, .85f, .4f) : Color.clear;
        });

        sPanel = MakeSprite(UIDir, "RoundedPanel", N, N, p =>
            new Color(1, 1, 1, Fill(BoxSD(p, new Vector2(.5f, .5f), new Vector2(.5f, .5f), .3f), N)),
            new Vector4(22, 22, 22, 22));
    }

    static void CreateMaterials()
    {
        Shader sprite = Shader.Find("Sprites/Default");
        spriteMat = SaveMaterial(new Material(sprite) { name = "SpriteUnlit" }, MaterialsDir + "/SpriteUnlit.mat");

        Material pm = new Material(sprite) { name = "Particle", mainTexture = sSoft.texture };
        particleMat = SaveMaterial(pm, MaterialsDir + "/Particle.mat");

        string pmPath = MaterialsDir + "/NoFriction.physicsMaterial2D";
        AssetDatabase.DeleteAsset(pmPath);
        noFriction = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
        AssetDatabase.CreateAsset(noFriction, pmPath);

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        Material tm = new Material(font.material) { name = "TMP_Outline" };
        tm.EnableKeyword(ShaderUtilities.Keyword_Outline);
        tm.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.18f);
        tm.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.05f, 0.05f, 0.15f, 1f));
        tm.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        tm.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0, 0, 0, 0.5f));
        tm.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
        tm.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
        textOutlineMat = SaveMaterial(tm, UIDir + "/TMP_Outline.mat");
    }

    static Material SaveMaterial(Material m, string path)
    {
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    static void CreateAudio()
    {
        foreach (string name in AudioManager.SoundNames)
        {
            float[] data = AudioManager.Generate(name);
            string path = $"{AudioDir}/{name}.wav";
            WriteWav(path, data, AudioManager.SampleRate);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
    }

    static void WriteWav(string path, float[] samples, int rate)
    {
        using (BinaryWriter w = new BinaryWriter(File.Create(path)))
        {
            int dataSize = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + dataSize);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(rate);
            w.Write(rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(dataSize);
            foreach (float s in samples)
                w.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
        }
    }

    static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{name}.wav");

    // ---------- Effects ----------

    static ParticleSystem MakeParticles(string name, Transform parent, Gradient colors, bool loop, int burst,
        float rate, float lifetime, Vector2 speed, Vector2 size, float gravity,
        ParticleSystemShapeType shape, float radius, int sortingOrder = 20)
    {
        GameObject go = new GameObject(name);
        if (parent) go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = loop ? 2f : 1f;
        main.loop = loop;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = new ParticleSystem.MinMaxGradient(colors) { mode = ParticleSystemGradientMode.RandomColor };
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;

        var emission = ps.emission;
        emission.rateOverTime = rate;
        emission.SetBursts(burst > 0 ? new[] { new ParticleSystem.Burst(0f, (short)burst) } : new ParticleSystem.Burst[0]);

        var sh = ps.shape;
        sh.shapeType = shape;
        sh.radius = radius;
        if (shape == ParticleSystemShapeType.Cone)
        {
            sh.angle = 30f;
            sh.rotation = new Vector3(-90f, 0f, 0f);
        }

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = fade;

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = particleMat;
        r.sortingOrder = sortingOrder;
        return ps;
    }

    static Gradient Colors(params Color[] cs)
    {
        Gradient g = new Gradient();
        GradientColorKey[] keys = new GradientColorKey[cs.Length];
        for (int i = 0; i < cs.Length; i++) keys[i] = new GradientColorKey(cs[i], cs.Length == 1 ? 0f : (float)i / (cs.Length - 1));
        g.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    static ParticleSystem SaveFx(ParticleSystem ps)
    {
        string path = $"{PrefabsDir}/{ps.name}.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(ps.gameObject, path);
        UnityEngine.Object.DestroyImmediate(ps.gameObject);
        return prefab.GetComponent<ParticleSystem>();
    }

    static void CreateEffects()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        fxCoin = SaveFx(MakeParticles("FX_CoinSparkle", null, Colors(Gold, Color.white, new Color(1f, .95f, .5f)),
            false, 22, 0, 0.6f, new Vector2(2f, 5f), new Vector2(.1f, .28f), 0.4f, ParticleSystemShapeType.Circle, .2f));
        fxHit = SaveFx(MakeParticles("FX_Hit", null, Colors(new Color(1f, .2f, .2f), new Color(1f, .6f, .1f), Color.white),
            false, 26, 0, 0.5f, new Vector2(3f, 7f), new Vector2(.12f, .3f), 1f, ParticleSystemShapeType.Circle, .3f));
        fxJump = SaveFx(MakeParticles("FX_JumpDust", null, Colors(new Color(.9f, .9f, .85f), new Color(.75f, .7f, .6f)),
            false, 8, 0, 0.4f, new Vector2(.8f, 2f), new Vector2(.15f, .35f), 0f, ParticleSystemShapeType.Circle, .3f));

        ParticleSystem victory = MakeParticles("FX_Victory", null,
            Colors(new Color(1f, .25f, .3f), Gold, new Color(.3f, .9f, .4f), new Color(.3f, .6f, 1f), new Color(1f, .4f, .9f)),
            false, 0, 0, 2.5f, new Vector2(7f, 13f), new Vector2(.15f, .35f), 1.2f, ParticleSystemShapeType.Cone, .5f);
        var vMain = victory.main;
        vMain.duration = 1.6f;
        victory.emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)70),
            new ParticleSystem.Burst(0.5f, (short)60),
            new ParticleSystem.Burst(1.0f, (short)60)
        });
        var rot = victory.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
        fxVictory = SaveFx(victory);
    }

    // ---------- Prefabs ----------

    static SpriteRenderer AddSprite(GameObject go, Sprite sprite, Color color, int order)
    {
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        sr.sharedMaterial = spriteMat;
        return sr;
    }

    static GameObject SavePrefab(GameObject go, string name)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabsDir}/{name}.prefab");
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    static void CreatePrefabs()
    {
        GameObject player = new GameObject("Player") { tag = "Player" };
        AddSprite(player, sPlayer, Color.white, 10);
        Rigidbody2D prb = player.AddComponent<Rigidbody2D>();
        prb.gravityScale = 3f;
        prb.freezeRotation = true;
        prb.interpolation = RigidbodyInterpolation2D.Interpolate;
        prb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        CapsuleCollider2D cap = player.AddComponent<CapsuleCollider2D>();
        cap.size = new Vector2(0.78f, 0.92f);
        cap.offset = new Vector2(0f, -0.02f);
        cap.sharedMaterial = noFriction;
        PlayerController pc = player.AddComponent<PlayerController>();
        pc.groundCheckOffset = 0.48f;
        pc.groundCheckSize = new Vector2(0.65f, 0.08f);
        pc.hitEffectPrefab = fxHit;
        pc.jumpEffectPrefab = fxJump;
        playerPrefab = SavePrefab(player, "Player");

        GameObject coin = new GameObject("Coin");
        coin.transform.localScale = Vector3.one * 0.65f;
        AddSprite(coin, sCoin, Color.white, 6);
        CircleCollider2D cc = coin.AddComponent<CircleCollider2D>();
        cc.isTrigger = true;
        cc.radius = 0.45f;
        coin.AddComponent<Coin>().collectEffectPrefab = fxCoin;
        GameObject coinGlow = new GameObject("Glow");
        coinGlow.transform.SetParent(coin.transform, false);
        coinGlow.transform.localScale = Vector3.one * 1.8f;
        AddSprite(coinGlow, sSoft, new Color(1f, .9f, .4f, .35f), 5);
        coinPrefab = SavePrefab(coin, "Coin");

        GameObject enemy = new GameObject("Enemy");
        enemy.transform.localScale = Vector3.one * 0.95f;
        AddSprite(enemy, sEnemy, Color.white, 8);
        Rigidbody2D erb = enemy.AddComponent<Rigidbody2D>();
        erb.bodyType = RigidbodyType2D.Kinematic;
        erb.interpolation = RigidbodyInterpolation2D.Interpolate;
        BoxCollider2D ebox = enemy.AddComponent<BoxCollider2D>();
        ebox.isTrigger = true;
        ebox.size = new Vector2(0.8f, 0.6f);
        ebox.offset = new Vector2(0f, -0.15f);
        enemy.AddComponent<EnemyController>();
        enemyPrefab = SavePrefab(enemy, "Enemy");

        GameObject goal = new GameObject("Goal");
        BoxCollider2D gbox = goal.AddComponent<BoxCollider2D>();
        gbox.isTrigger = true;
        gbox.size = new Vector2(1.4f, 3.4f);
        gbox.offset = new Vector2(0.2f, 1.7f);
        GameObject glow = Child(goal, "Glow", new Vector3(0.3f, 1.8f, 0f), new Vector3(4.5f, 5f, 1f));
        SpriteRenderer glowSr = AddSprite(glow, sSoft, new Color(1f, .95f, .45f, .7f), 2);
        GameObject pole = Child(goal, "Pole", new Vector3(0f, 1.7f, 0f), new Vector3(0.14f, 3.4f, 1f));
        AddSprite(pole, sSquare, new Color(.92f, .92f, .95f), 4);
        GameObject top = Child(goal, "Top", new Vector3(0f, 3.45f, 0f), Vector3.one * 0.3f);
        AddSprite(top, sCircle, Gold, 5);
        GameObject flag = Child(goal, "Flag", new Vector3(0.6f, 2.75f, 0f), new Vector3(1.2f, 1.1f, 1f));
        AddSprite(flag, sFlag, Color.white, 4);
        GameObject gbase = Child(goal, "Base", new Vector3(0f, 0.1f, 0f), new Vector3(1f, 0.25f, 1f));
        AddSprite(gbase, sSquare, new Color(.35f, .35f, .45f), 4);
        ParticleSystem gfx = MakeParticles("Sparkles", goal.transform, Colors(Gold, Color.white, new Color(.6f, 1f, .6f)),
            true, 0, 10, 1.6f, new Vector2(0.4f, 1.2f), new Vector2(.08f, .2f), -0.15f, ParticleSystemShapeType.Circle, 1f, 3);
        gfx.transform.localPosition = new Vector3(0.3f, 1.6f, 0f);
        goal.AddComponent<Goal>().glow = glowSr;
        goalPrefab = SavePrefab(goal, "Goal");
    }

    static GameObject Child(GameObject parent, string name, Vector3 pos, Vector3 scale)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        return go;
    }

    static GameObject Place(GameObject prefab, Transform parent, Vector3 pos)
    {
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.position = pos;
        return go;
    }

    // ---------- World ----------

    static Camera CreateCamera(Vector3 pos)
    {
        GameObject go = new GameObject("Main Camera") { tag = "MainCamera" };
        go.transform.position = pos;
        Camera cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Sky;
        go.AddComponent<AudioListener>();

        GameObject sun = Child(go, "Sun", new Vector3(7.5f, 3.6f, 10f), Vector3.one * 2.2f);
        AddSprite(sun, sCircle, new Color(1f, .93f, .55f), -40);
        GameObject sunGlow = Child(sun, "Glow", Vector3.zero, Vector3.one * 2.5f);
        AddSprite(sunGlow, sSoft, new Color(1f, .95f, .6f, .5f), -41);
        return cam;
    }

    static void CreatePlatform(Transform parent, string name, float xLeft, float xRight, float yTop, float thickness, bool oneWay)
    {
        float w = xRight - xLeft;
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3((xLeft + xRight) * 0.5f, yTop - thickness * 0.5f, 0f);

        SpriteRenderer dirt = AddSprite(go, sDirt, Color.white, 0);
        dirt.drawMode = SpriteDrawMode.Tiled;
        dirt.size = new Vector2(w, thickness);

        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(w, thickness);
        if (oneWay)
        {
            box.usedByEffector = true;
            PlatformEffector2D eff = go.AddComponent<PlatformEffector2D>();
            eff.useOneWay = true;
            eff.surfaceArc = 170f;
        }

        GameObject grass = Child(go, "Grass", new Vector3(0f, thickness * 0.5f - 0.18f, 0f), Vector3.one);
        SpriteRenderer gs = AddSprite(grass, sGrass, Color.white, 1);
        gs.drawMode = SpriteDrawMode.Tiled;
        gs.size = new Vector2(w + 0.1f, 0.5f);
    }

    static void CreateCrate(Transform parent, float x, float y)
    {
        GameObject go = new GameObject("Crate");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(x, y, 0f);
        AddSprite(go, sCrate, Color.white, 2);
        go.AddComponent<BoxCollider2D>().size = Vector2.one;
    }

    static void CreateWall(Transform parent, string name, float x)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(x, 10f, 0f);
        go.AddComponent<BoxCollider2D>().size = new Vector2(1f, 40f);
    }

    static void CreateBackground(float fromX, float toX)
    {
        GameObject bg = new GameObject("Background");
        int i = 0;
        for (float x = fromX; x < toX; x += 13f, i++)
        {
            GameObject far = Child(bg, "HillFar", new Vector3(x + 6f, -7f + (i % 2) * 1.5f, 0f), Vector3.one * 22f);
            AddSprite(far, sCircle, new Color(.5f, .78f, .62f), -30);
            GameObject near = Child(bg, "Hill", new Vector3(x, -9.5f + (i % 3), 0f), Vector3.one * 18f);
            AddSprite(near, sCircle, new Color(.4f, .72f, .45f), -25);
        }
        for (float x = fromX; x < toX; x += 11f, i++)
        {
            float y = 6.5f + (i % 3) * 1.2f;
            GameObject cloud = Child(bg, "Cloud", new Vector3(x + (i % 4), y, 0f), Vector3.one);
            float[,] puffs = { { 0f, 0f, 1.6f }, { 1f, .3f, 2f }, { 2.1f, 0f, 1.5f }, { 1f, -.3f, 1.4f } };
            for (int p = 0; p < 4; p++)
            {
                GameObject puff = Child(cloud, "Puff", new Vector3(puffs[p, 0], puffs[p, 1], 0f), Vector3.one * puffs[p, 2]);
                AddSprite(puff, sCircle, new Color(1f, 1f, 1f, .92f), -20);
            }
        }
    }

    // ---------- UI ----------

    static Canvas CreateCanvas()
    {
        GameObject go = new GameObject("Canvas");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    static void CreateEventSystem()
    {
        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        Type inputSystemModule = null;
#if ENABLE_INPUT_SYSTEM
        inputSystemModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
#endif
        if (inputSystemModule != null) go.AddComponent(inputSystemModule);
        else go.AddComponent<StandaloneInputModule>();
    }

    static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static RectTransform Stretch(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    static Image Panel(RectTransform rt, Color color)
    {
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sPanel;
        img.type = Image.Type.Sliced;
        img.color = color;
        return img;
    }

    static TMP_Text Label(Transform parent, string name, string text, float size, Color color,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 box, TextAlignmentOptions align = TextAlignmentOptions.Center, bool outline = true)
    {
        RectTransform rt = Rect(name, parent, anchor, pivot, pos, box);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = FontStyles.Bold;
        t.raycastTarget = false;
        if (outline) t.fontSharedMaterial = textOutlineMat;
        return t;
    }

    static Button MakeButton(Transform parent, string label, Vector2 pos, Vector2 size, Color color, UnityAction action)
    {
        RectTransform rt = Rect(label + "Button", parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f), pos, size);
        Image img = Panel(rt, color);
        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        cb.pressedColor = new Color(.75f, .75f, .75f);
        cb.selectedColor = Color.white;
        cb.colorMultiplier = 1.2f;
        b.colors = cb;
        UnityEventTools.AddPersistentListener(b.onClick, action);
        Label(rt, "Label", label, size.y * 0.45f, Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, size);
        return b;
    }

    static GameObject Popup(Transform canvas, string name, Vector2 boxSize, out RectTransform box)
    {
        RectTransform dim = Stretch(name, canvas);
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        box = Rect("Box", dim, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, boxSize);
        Panel(box, BoxColor);
        RectTransform border = Stretch("Border", box);
        border.offsetMin = new Vector2(-6f, -6f);
        border.offsetMax = new Vector2(6f, 6f);
        Image bi = Panel(border, new Color(1f, .82f, .2f, .9f));
        bi.raycastTarget = false;
        border.SetAsFirstSibling();
        Image inner = Panel(Stretch("Inner", box), BoxColor);
        inner.transform.SetSiblingIndex(1);
        box.GetComponent<Image>().color = Color.clear;
        return dim.gameObject;
    }

    static AudioManager CreateAudioManager()
    {
        AudioManager am = new GameObject("AudioManager").AddComponent<AudioManager>();
        am.coin = Clip("coin");
        am.jump = Clip("jump");
        am.hit = Clip("hit");
        am.click = Clip("click");
        am.win = Clip("win");
        am.lose = Clip("lose");
        return am;
    }

    // ---------- Scenes ----------

    static void BuildMenuScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCamera(new Vector3(0f, 3f, -10f));
        CreateBackground(-20f, 20f);

        GameObject level = new GameObject("Decor");
        CreatePlatform(level.transform, "Ground", -14f, 14f, -1.5f, 3f, false);
        GameObject hero = Child(level, "Hero", new Vector3(-6f, -1f, 0f), Vector3.one * 1.2f);
        AddSprite(hero, sPlayer, Color.white, 10);
        Place(enemyPrefab, level.transform, new Vector3(5f, -1.05f, 0f)).GetComponent<EnemyController>().patrolDistance = 3f;
        for (int i = 0; i < 5; i++)
            Place(coinPrefab, level.transform, new Vector3(-3f + i * 1.5f, 0.2f + Mathf.Sin(i) * 0.4f, 0f));
        GameObject flag = Place(goalPrefab, level.transform, new Vector3(10f, -1.5f, 0f));
        UnityEngine.Object.DestroyImmediate(flag.GetComponent<Goal>(), true);

        CreateAudioManager();
        Canvas canvas = CreateCanvas();
        UIManager ui = canvas.gameObject.AddComponent<UIManager>();

        TMP_Text title = Label(canvas.transform, "Title", "COIN RUSH", 170, Gold,
            new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 290f), new Vector2(1400f, 220f));
        ui.menuTitle = title.rectTransform;
        Label(canvas.transform, "Tagline", "Collect coins. Avoid enemies. Reach the goal!", 46, Color.white,
            new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 150f), new Vector2(1400f, 80f));
        MakeButton(canvas.transform, "PLAY", new Vector2(0f, -10f), new Vector2(440f, 115f), Green, ui.PlayGame);
        MakeButton(canvas.transform, "HOW TO PLAY", new Vector2(0f, -150f), new Vector2(440f, 115f), Blue, ui.ShowHowTo);

        ui.howToPanel = Popup(canvas.transform, "HowToPanel", new Vector2(980f, 760f), out RectTransform box);
        Label(box, "Title", "HOW TO PLAY", 80, Gold, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 290f), new Vector2(900f, 110f));
        Label(box, "Body",
            "<color=#FFD54A>A / Left Arrow</color>  →  Move Left\n" +
            "<color=#FFD54A>D / Right Arrow</color>  →  Move Right\n" +
            "<color=#FFD54A>Space</color>  →  Jump\n\n" +
            "Collect coins.\nAvoid enemies.\nReach the goal before time runs out.",
            40, Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 10f), new Vector2(820f, 420f),
            TextAlignmentOptions.Center, false);
        MakeButton(box, "BACK", new Vector2(0f, -290f), new Vector2(300f, 95f), new Color(.85f, .3f, .35f), ui.HideHowTo);

        CreateEventSystem();
        EditorSceneManager.SaveScene(scene, MenuScene);
    }

    static void BuildGameScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Camera cam = CreateCamera(new Vector3(2.8f, 3f, -10f));
        CreateBackground(-15f, 105f);

        Transform level = new GameObject("Level").transform;
        Transform ground = new GameObject("Ground").transform;
        ground.SetParent(level);
        CreatePlatform(ground, "Ground_1", -8f, 14f, 0f, 3f, false);
        CreatePlatform(ground, "Ground_2", 17f, 34f, 0f, 3f, false);
        CreatePlatform(ground, "Ground_3", 37f, 60f, 0f, 3f, false);
        CreatePlatform(ground, "Ground_4", 63f, 93f, 0f, 3f, false);

        Transform platforms = new GameObject("Platforms").transform;
        platforms.SetParent(level);
        float[,] plats =
        {
            { 4.5f, 7.5f, 2.5f }, { 8.5f, 11.5f, 4.5f }, { 20f, 24f, 2.6f }, { 26.5f, 29.5f, 4.2f },
            { 40.5f, 43.5f, 2.6f }, { 45f, 49f, 4.6f }, { 51.5f, 54.5f, 2.6f }, { 66f, 70f, 3f },
            { 73.5f, 76.5f, 5f }, { 79.5f, 82.5f, 3f }
        };
        for (int i = 0; i < plats.GetLength(0); i++)
            CreatePlatform(platforms, $"Platform_{i + 1}", plats[i, 0], plats[i, 1], plats[i, 2], 0.6f, true);

        Transform obstacles = new GameObject("Obstacles").transform;
        obstacles.SetParent(level);
        CreateCrate(obstacles, 18.5f, 0.5f);
        CreateCrate(obstacles, 31f, 0.5f);
        CreateCrate(obstacles, 31f, 1.5f);
        CreateCrate(obstacles, 57f, 0.5f);
        CreateCrate(obstacles, 58f, 0.5f);
        CreateCrate(obstacles, 57.5f, 1.5f);
        CreateCrate(obstacles, 72f, 0.5f);
        CreateWall(obstacles, "LeftBoundary", -8.5f);
        CreateWall(obstacles, "RightBoundary", 93.5f);

        Transform coins = new GameObject("Coins").transform;
        coins.SetParent(level);
        Vector2[] coinPos =
        {
            new Vector2(1f, 1f), new Vector2(6f, 3.6f), new Vector2(10f, 5.6f), new Vector2(15.5f, 2.2f),
            new Vector2(22f, 3.7f), new Vector2(28f, 5.3f), new Vector2(35.5f, 2.2f), new Vector2(42f, 3.7f),
            new Vector2(47f, 5.7f), new Vector2(53f, 3.7f), new Vector2(61.5f, 2.2f), new Vector2(75f, 6.1f),
            new Vector2(81f, 4.1f), new Vector2(86f, 1f)
        };
        foreach (Vector2 p in coinPos) Place(coinPrefab, coins, p);

        Transform enemies = new GameObject("Enemies").transform;
        enemies.SetParent(level);
        (Vector2 pos, float dist, float speed)[] enemyData =
        {
            (new Vector2(25f, 0.45f), 3f, 2.5f),
            (new Vector2(47f, 5.05f), 1.4f, 1.8f),
            (new Vector2(46f, 0.45f), 3.5f, 3f),
            (new Vector2(67.5f, 0.45f), 3f, 2.8f),
            (new Vector2(84.5f, 0.45f), 2.5f, 3.2f)
        };
        foreach (var e in enemyData)
        {
            EnemyController ec = Place(enemyPrefab, enemies, e.pos).GetComponent<EnemyController>();
            ec.patrolDistance = e.dist;
            ec.speed = e.speed;
        }

        Place(goalPrefab, level, new Vector3(89f, 0f, 0f));
        GameObject start = new GameObject("StartPoint");
        start.transform.SetParent(level);
        start.transform.position = new Vector3(-5f, 1f, 0f);

        PlayerController player = Place(playerPrefab, null, start.transform.position).GetComponent<PlayerController>();

        CameraFollow follow = cam.gameObject.AddComponent<CameraFollow>();
        follow.target = player.transform;
        follow.minX = 2.8f;
        follow.maxX = 82f;
        follow.minY = 3f;
        follow.maxY = 8f;

        CreateAudioManager();

        Canvas canvas = CreateCanvas();
        UIManager ui = canvas.gameObject.AddComponent<UIManager>();

        RectTransform overlay = Stretch("DamageOverlay", canvas.transform);
        Image ov = overlay.gameObject.AddComponent<Image>();
        ov.color = Color.clear;
        ov.raycastTarget = false;
        ui.damageOverlay = ov;

        RectTransform hudL = Rect("HUD_Left", canvas.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(380f, 150f));
        Panel(hudL, new Color(0f, 0f, 0.1f, 0.45f)).raycastTarget = false;
        ui.scoreText = Label(hudL, "ScoreText", "Score: 0", 52, Gold, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -12f), new Vector2(340f, 64f), TextAlignmentOptions.Left);
        ui.healthText = Label(hudL, "HealthText", "Health: 3", 52, new Color(1f, .55f, .55f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -76f), new Vector2(340f, 64f), TextAlignmentOptions.Left);

        RectTransform hudR = Rect("HUD_Right", canvas.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(320f, 86f));
        Panel(hudR, new Color(0f, 0f, 0.1f, 0.45f)).raycastTarget = false;
        ui.timeText = Label(hudR, "TimeText", "Time: 60", 52, Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(300f, 70f));

        ui.winPanel = Popup(canvas.transform, "WinPanel", new Vector2(820f, 620f), out RectTransform win);
        Label(win, "Title", "YOU WIN!", 110, Gold, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 205f), new Vector2(760f, 140f));
        ui.winScoreText = Label(win, "FinalScore", "Final Score: 0", 48, Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 80f), new Vector2(740f, 64f));
        ui.winCoinsText = Label(win, "Coins", "Coins Collected: 0", 48, Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 10f), new Vector2(740f, 64f));
        ui.winTimeText = Label(win, "TimeRemaining", "Time Remaining: 0s", 48, Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, -60f), new Vector2(740f, 64f));
        MakeButton(win, "RESTART", new Vector2(-170f, -200f), new Vector2(300f, 100f), Green, ui.Restart);
        MakeButton(win, "MENU", new Vector2(170f, -200f), new Vector2(300f, 100f), Blue, ui.MainMenu);

        ui.gameOverPanel = Popup(canvas.transform, "GameOverPanel", new Vector2(820f, 560f), out RectTransform lose);
        ui.gameOverTitle = Label(lose, "Title", "GAME OVER", 110, new Color(1f, .35f, .35f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 175f), new Vector2(760f, 140f));
        ui.gameOverScoreText = Label(lose, "Score", "Score: 0", 48, Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 20f), new Vector2(740f, 140f));
        MakeButton(lose, "RESTART", new Vector2(-170f, -170f), new Vector2(300f, 100f), Green, ui.Restart);
        MakeButton(lose, "MENU", new Vector2(170f, -170f), new Vector2(300f, 100f), Blue, ui.MainMenu);

        CreateEventSystem();

        GameManager gm = new GameObject("GameManager").AddComponent<GameManager>();
        gm.player = player;
        gm.ui = ui;
        gm.victoryEffectPrefab = fxVictory;

        EditorSceneManager.SaveScene(scene, GameScene);
    }
}
