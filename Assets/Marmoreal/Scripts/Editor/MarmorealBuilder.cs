using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UIElements;

namespace Marmoreal.EditorTools
{
    /// <summary>
    /// CONSTRUCCIÓN INICIAL (una sola vez). Crea texturas, materiales, meshes, prefabs y la escena
    /// Assets/Marmoreal/Scenes/Marmoreal.unity. Es ADITIVO e IDEMPOTENTE: cada asset que ya existe
    /// se reutiliza y NUNCA se sobrescribe; si la escena ya existe, no se toca.
    /// Después, todo se edita directamente en la escena / prefabs (ver CLAUDE.md).
    /// </summary>
    public static class MarmorealBuilder
    {
        const string Root = "Assets/Marmoreal";
        const string TexDir = Root + "/Textures";
        internal const string MatDir = Root + "/Materials";
        internal const string MeshDir = Root + "/Meshes";
        const string FabDir = Root + "/Prefabs";
        const string CfgDir = Root + "/Config";
        const string UIDir = Root + "/UI";
        const string SceneDir = Root + "/Scenes";
        internal const string ScenePath = SceneDir + "/Marmoreal.unity";

        // Iluminación física (EV100 fijo)
        const float ExposureEV = 9.5f;

        [MenuItem("Marmoreal/Construcción inicial (solo crea lo que falte)")]
        public static void BuildAll()
        {
            foreach (var d in new[] { TexDir, MatDir, MeshDir, FabDir, CfgDir, UIDir, SceneDir })
                Directory.CreateDirectory(d);

            var config = LoadOrCreate(CfgDir + "/Economy.asset", () => { var c = ScriptableObject.CreateInstance<EconomyConfig>(); c.FillDefaults(); return c; });
            var mats = BuildMaterials();
            var meshes = BuildMeshes();
            var fabs = BuildPrefabs(mats, meshes);

            if (File.Exists(ScenePath))
            {
                Debug.Log("[Marmoreal] La escena ya existe: no se modifica. Edita directamente " + ScenePath);
                AssetDatabase.SaveAssets();
                return;
            }
            BuildScene(config, mats, meshes, fabs);
            AssetDatabase.SaveAssets();
            Debug.Log("[Marmoreal] Construcción inicial completada.");
        }

        static T LoadOrCreate<T>(string path, System.Func<T> create) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a) return a;
            a = create();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        // =================================================================== MATERIALES
        public class Mats
        {
            public Material floor, column, gold, dark, outerGround;
            public Material[] chunks;     // por tier
            public Material goldenChunk;
            public Material statue, statueGold;
            public Material[] player;     // por pulido
            public Material[] shards;     // por tier + dorado
            public Material[] debris;     // por tier + dorado
            public Material acolyte, ring, trail, fire, inlay;
        }

        static MarbleTextureGenerator.Spec Spec(string name, Color a, Color b, Color vein, Color vein2, int seed, float warp = 1.1f, float sharp = 9f, float vs = 0.85f, float fs = 0.35f)
            => new MarbleTextureGenerator.Spec
            {
                name = name, baseA = a, baseB = b, vein = vein, vein2 = vein2, seed = seed,
                veinU = 1, veinV = 2, fineU = 3, fineV = -2, warp = warp, fineWarp = 1.6f, sharp = sharp,
                veinStrength = vs, fineStrength = fs, speckle = 0.025f, normalStrength = 6f
            };

        static readonly MarbleTextureGenerator.Spec[] Variants =
        {
            Spec("Carrara",   new Color(0.93f,0.92f,0.90f), new Color(0.86f,0.86f,0.86f), new Color(0.42f,0.44f,0.48f), new Color(0.62f,0.63f,0.66f), 11),
            Spec("Rosa",      new Color(0.90f,0.72f,0.70f), new Color(0.80f,0.58f,0.57f), new Color(0.97f,0.93f,0.90f), new Color(0.55f,0.30f,0.30f), 23, 1.3f, 7f),
            Spec("Verde",     new Color(0.10f,0.24f,0.18f), new Color(0.05f,0.15f,0.11f), new Color(0.80f,0.90f,0.82f), new Color(0.35f,0.55f,0.42f), 37, 1.5f, 10f),
            Spec("Nero",      new Color(0.05f,0.05f,0.055f), new Color(0.025f,0.025f,0.03f), new Color(0.90f,0.89f,0.86f), new Color(0.55f,0.55f,0.55f), 41, 1.2f, 12f, 0.9f, 0.5f),
            Spec("Calacatta", new Color(0.96f,0.94f,0.89f), new Color(0.90f,0.87f,0.80f), new Color(0.72f,0.55f,0.25f), new Color(0.55f,0.50f,0.45f), 53, 0.9f, 6f, 0.9f, 0.3f),
            Spec("Sienna",    new Color(0.82f,0.66f,0.38f), new Color(0.70f,0.50f,0.26f), new Color(0.35f,0.20f,0.12f), new Color(0.95f,0.85f,0.65f), 67, 1.4f, 8f),
        };

        struct TexSet { public Texture2D a, m, n; }

        static TexSet Tex(MarbleTextureGenerator.Spec s)
        {
            string pa = $"{TexDir}/T_{s.name}_Albedo.png";
            if (File.Exists(pa))
                return new TexSet
                {
                    a = AssetDatabase.LoadAssetAtPath<Texture2D>(pa),
                    m = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/T_{s.name}_Mask.png"),
                    n = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/T_{s.name}_Normal.png")
                };
            MarbleTextureGenerator.Generate(TexDir, s, 1024, out var a, out var m, out var n);
            return new TexSet { a = a, m = m, n = n };
        }

        static Material Lit(string name, TexSet t, float smoothMin, float smoothMax, float normalScale, float coat, Color tint, Vector2 tiling, float metallic = 0f, float triplanarWorldScale = 0f)
        {
            string path = $"{MatDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;
            var mat = new Material(Shader.Find("HDRP/Lit")) { name = name, enableInstancing = true };
            mat.SetTexture("_BaseColorMap", t.a);
            mat.SetColor("_BaseColor", tint);
            mat.SetTexture("_MaskMap", t.m);
            mat.SetTexture("_NormalMap", t.n);
            mat.SetFloat("_NormalScale", normalScale);
            mat.SetFloat("_SmoothnessRemapMin", smoothMin);
            mat.SetFloat("_SmoothnessRemapMax", smoothMax);
            mat.SetFloat("_MetallicRemapMin", metallic);
            mat.SetFloat("_MetallicRemapMax", metallic);
            mat.SetFloat("_CoatMask", coat);
            mat.SetTextureScale("_BaseColorMap", tiling);
            if (triplanarWorldScale > 0f)
            {
                // Triplanar en espacio mundo: las vetas fluyen continuas entre los trozos del bloque
                mat.SetFloat("_UVBase", 5f);
                mat.SetFloat("_ObjectSpaceUVMapping", 0f);
                mat.SetFloat("_TexWorldScale", triplanarWorldScale);
            }
            HDMaterial.ValidateMaterial(mat);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static Material Solid(string name, Color c, float smooth, float metallic, float coat = 0f)
        {
            string path = $"{MatDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;
            var mat = new Material(Shader.Find("HDRP/Lit")) { name = name, enableInstancing = true };
            mat.SetColor("_BaseColor", c);
            mat.SetFloat("_Smoothness", smooth);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_CoatMask", coat);
            HDMaterial.ValidateMaterial(mat);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        internal static Material Emissive(string name, Color baseColor, Color emissive, float ev, float smooth = 0.6f)
        {
            string path = $"{MatDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;
            var mat = new Material(Shader.Find("HDRP/Lit")) { name = name, enableInstancing = true };
            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Smoothness", smooth);
            HDMaterial.SetUseEmissiveIntensity(mat, true);
            HDMaterial.SetEmissiveColor(mat, emissive);
            HDMaterial.SetEmissiveIntensity(mat, ev, EmissiveIntensityUnit.EV100);
            HDMaterial.ValidateMaterial(mat);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        internal static Material Trail(string name, Color c)
        {
            string path = $"{MatDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;
            var mat = new Material(Shader.Find("HDRP/Unlit")) { name = name };
            mat.SetColor("_UnlitColor", c);
            HDMaterial.SetSurfaceType(mat, true);
            mat.SetFloat("_BlendMode", 1f); // Aditivo
            mat.SetFloat("_EnableFogOnTransparent", 0f);
            HDMaterial.ValidateMaterial(mat);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static Mats BuildMaterials()
        {
            var m = new Mats();
            var tex = new TexSet[Variants.Length];
            for (int i = 0; i < Variants.Length; i++) tex[i] = Tex(Variants[i]);

            // Suelo: damero Carrara/Nero con juntas de latón
            TexSet floorTex;
            if (File.Exists($"{TexDir}/T_Floor_Albedo.png"))
                floorTex = new TexSet
                {
                    a = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/T_Floor_Albedo.png"),
                    m = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/T_Floor_Mask.png"),
                    n = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/T_Floor_Normal.png")
                };
            else
            {
                MarbleTextureGenerator.GenerateFloor(TexDir, "Floor", Variants[0], Variants[3], 2048, new Color(0.42f, 0.32f, 0.16f), out var fa, out var fm, out var fn);
                floorTex = new TexSet { a = fa, m = fm, n = fn };
            }

            m.floor = Lit("M_Floor", floorTex, 0.55f, 0.97f, 0.6f, 0.6f, Color.white, Vector2.one);
            m.column = Lit("M_Column", tex[0], 0.35f, 0.85f, 0.8f, 0.2f, Color.white, Vector2.one);
            m.outerGround = Lit("M_OuterGround", tex[3], 0.3f, 0.8f, 0.5f, 0f, new Color(0.7f, 0.7f, 0.7f), new Vector2(0.08f, 0.08f));
            m.gold = Solid("M_Gold", new Color(1f, 0.78f, 0.38f), 0.82f, 1f);
            m.dark = Solid("M_DarkStone", new Color(0.06f, 0.055f, 0.05f), 0.55f, 0f, 0.3f);
            m.inlay = Solid("M_BrassInlay", new Color(0.85f, 0.65f, 0.32f), 0.7f, 1f);

            // Bloques en bruto (rugosos) por tier: Carrara → Rosa → Verde → Nero → Calacatta → Sienna
            m.chunks = new Material[Variants.Length];
            for (int i = 0; i < Variants.Length; i++)
                m.chunks[i] = Lit($"M_Chunk_{Variants[i].name}", tex[i], 0.12f, 0.42f, 1.6f, 0f, Color.white, new Vector2(0.5f, 0.5f), 0f, 0.4f);
            m.goldenChunk = Lit("M_Chunk_Golden", tex[4], 0.55f, 0.9f, 1.2f, 0f, new Color(1f, 0.82f, 0.45f), new Vector2(0.5f, 0.5f), 0.85f, 0.4f);

            // Estatuas pulidas
            m.statue = Lit("M_Statue", tex[4], 0.75f, 0.97f, 0.4f, 1f, Color.white, Vector2.one);
            m.statueGold = Solid("M_StatueGold", new Color(1f, 0.8f, 0.42f), 0.9f, 1f, 0.5f);

            // Canica por pulido: Carrara, Rosa, Verde, Nero, Calacatta
            m.player = new Material[5];
            for (int i = 0; i < 5; i++)
                m.player[i] = Lit($"M_Player_{Variants[i].name}", tex[i], 0.85f, 0.98f, 0.3f, 1f, Color.white, Vector2.one);

            // FX instanciados
            Color[] shardCols =
            {
                new Color(1f, 0.95f, 0.85f), new Color(1f, 0.6f, 0.6f), new Color(0.55f, 1f, 0.75f),
                new Color(0.8f, 0.8f, 1f), new Color(1f, 0.85f, 0.5f), new Color(1f, 0.7f, 0.35f), new Color(1f, 0.82f, 0.3f)
            };
            m.shards = new Material[Variants.Length + 1];
            for (int i = 0; i < m.shards.Length; i++)
                m.shards[i] = Emissive($"M_Shard_{i}", shardCols[i], shardCols[i], i == Variants.Length ? 12.5f : 11.5f, 0.9f);
            m.debris = new Material[Variants.Length + 1];
            for (int i = 0; i < Variants.Length; i++) m.debris[i] = m.chunks[i];
            m.debris[Variants.Length] = m.goldenChunk;

            m.acolyte = Emissive("M_Acolyte", new Color(0.8f, 0.9f, 1f), new Color(0.6f, 0.85f, 1f), 12.5f, 0.95f);
            m.ring = Emissive("M_ShockRing", new Color(0.6f, 0.9f, 1f), new Color(0.55f, 0.9f, 1f), 13f, 0.9f);
            m.fire = Emissive("M_BrazierFire", new Color(1f, 0.6f, 0.2f), new Color(1f, 0.55f, 0.18f), 13.5f, 0.2f);
            m.trail = Trail("M_Trail", new Color(0.55f, 0.6f, 0.65f, 1f));
            return m;
        }

        // =================================================================== MESHES
        public class Meshes
        {
            public Mesh chunk, sphere, playerSphere, shard, torus, floor, column, ringBeam, brazier, pad, outerDisc, obelisk;
            public Mesh[] statues;
        }

        static Mesh SaveMesh(string name, System.Func<Mesh> make)
        {
            string path = $"{MeshDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) return existing;
            var m = make();
            m.name = name;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Vector2 P(float r, float y) => new Vector2(r, y);

        static Meshes BuildMeshes()
        {
            var ms = new Meshes();
            ms.chunk = SaveMesh("Chunk", () => MeshFactory.RoundedBox(Vector3.one, 0.12f));
            ms.sphere = SaveMesh("Sphere", () => MeshFactory.Sphere(0.5f, 40));
            ms.playerSphere = SaveMesh("PlayerSphere", () => MeshFactory.Sphere(0.75f, 64));
            ms.shard = SaveMesh("Shard", () => MeshFactory.Shard());
            ms.torus = SaveMesh("Ring", () => MeshFactory.Torus(1f, 0.035f, 96, 12));
            ms.pad = SaveMesh("SpawnPad", () => MeshFactory.Torus(2.1f, 0.06f, 96, 10));
            ms.floor = SaveMesh("ArenaFloor", () => MeshFactory.ArenaFloor(160, 4f));
            ms.outerDisc = SaveMesh("OuterGround", () => MeshFactory.Lathe(new[] { P(400, 0), P(0, 0) }, 64, "OuterGround"));

            // Columna con estrías (20 estrías), basa y capitel
            ms.column = SaveMesh("Column", () =>
            {
                var prof = new[]
                {
                    P(0,0), P(1.15f,0), P(1.15f,0.35f), P(1.0f,0.45f), P(1.05f,0.6f), P(0.85f,0.75f), P(0.82f,0.85f),
                    P(0.8f,1.0f), P(0.66f,11.2f), P(0.72f,11.35f), P(0.66f,11.45f), P(0.9f,11.8f), P(1.15f,12.1f), P(1.2f,12.25f), P(1.2f,12.6f), P(0,12.6f)
                };
                return MeshFactory.Lathe(prof, 60, "Column", (y, a) => y > 1.0f && y < 11.2f ? 1f - 0.05f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 10f)), 0.6f) : 1f);
            });
            // Arquitrabe circular sobre las columnas
            ms.ringBeam = SaveMesh("Architrave", () =>
            {
                float r0 = ArenaShape.TerraceRadius - 4.6f, r1 = ArenaShape.TerraceRadius - 1.4f;
                var prof = new[] { P(r1, 0), P(r0, 0), P(r0, 0.9f), P(r0 - 0.25f, 1.1f), P(r0 - 0.25f, 1.6f), P(r1 + 0.25f, 1.6f), P(r1 + 0.25f, 1.1f), P(r1, 0.9f), P(r1, 0) };
                return MeshFactory.Flatten(MeshFactory.Lathe(prof, 160, "Architrave"));
            });
            ms.brazier = SaveMesh("Brazier", () =>
            {
                var prof = new[] { P(0,0), P(0.55f,0), P(0.55f,0.12f), P(0.2f,0.3f), P(0.14f,1.3f), P(0.22f,1.4f), P(0.7f,1.55f), P(0.85f,1.85f), P(0.78f,1.88f), P(0.5f,1.7f), P(0,1.68f) };
                return MeshFactory.Lathe(prof, 40, "Brazier");
            });
            ms.obelisk = SaveMesh("Obelisk", () =>
                MeshFactory.Lathe(new[] { P(0, 0), P(3.4f, 0), P(3.4f, 2.4f), P(2.2f, 2.4f), P(1.6f, 26f), P(0, 29f) }, 4, "Obelisk", null, Mathf.PI / 4f, true, 0.25f));

            ms.statues = new Mesh[5];
            ms.statues[0] = SaveMesh("Statue_Busto", () =>
            {
                var plinth = MeshFactory.RoundedBox(new Vector3(0.95f, 0.5f, 0.95f), 0.06f);
                var sph = MeshFactory.Sphere(0.5f, 40);
                var neck = MeshFactory.Lathe(new[] { P(0, 0), P(0.17f, 0), P(0.15f, 0.4f), P(0, 0.4f) }, 24);
                return MeshFactory.Combine("Statue_Busto",
                    (plinth, MeshFactory.TRS(new Vector3(0, 0.25f, 0), Vector3.zero)),
                    (sph, MeshFactory.TRS(new Vector3(0, 0.95f, 0), Vector3.zero, new Vector3(1.35f, 0.95f, 0.72f))),
                    (neck, MeshFactory.TRS(new Vector3(0, 1.3f, 0.02f), Vector3.zero)),
                    (sph, MeshFactory.TRS(new Vector3(0, 1.9f, 0.04f), Vector3.zero, new Vector3(0.48f, 0.6f, 0.54f))),
                    (sph, MeshFactory.TRS(new Vector3(0, 2.05f, -0.12f), Vector3.zero, new Vector3(0.5f, 0.45f, 0.5f))),
                    (MeshFactory.RoundedBox(new Vector3(0.07f, 0.16f, 0.12f), 0.03f), MeshFactory.TRS(new Vector3(0, 1.88f, 0.3f), new Vector3(-12, 0, 0))));
            });
            ms.statues[1] = SaveMesh("Statue_Anfora", () =>
            {
                var body = MeshFactory.Lathe(new[] { P(0,0), P(0.32f,0), P(0.34f,0.06f), P(0.22f,0.18f), P(0.5f,0.55f), P(0.66f,1.0f), P(0.6f,1.45f), P(0.34f,1.8f), P(0.2f,2.0f), P(0.21f,2.2f), P(0.32f,2.32f), P(0.3f,2.38f), P(0.16f,2.3f), P(0,2.25f) }, 48);
                var handle = MeshFactory.Torus(0.3f, 0.05f, 32, 10);
                return MeshFactory.Combine("Statue_Anfora",
                    (body, Matrix4x4.identity),
                    (handle, MeshFactory.TRS(new Vector3(0.42f, 1.85f, 0), new Vector3(90, 0, 0), new Vector3(0.8f, 1f, 1.2f))),
                    (handle, MeshFactory.TRS(new Vector3(-0.42f, 1.85f, 0), new Vector3(90, 0, 0), new Vector3(0.8f, 1f, 1.2f))));
            });
            ms.statues[2] = SaveMesh("Statue_Obelisco", () =>
            {
                var shaft = MeshFactory.Lathe(new[] { P(0, 0.3f), P(0.5f, 0.3f), P(0.36f, 2.2f), P(0, 2.6f) }, 4, "s", null, Mathf.PI / 4f, true);
                var basePlinth = MeshFactory.RoundedBox(new Vector3(1.1f, 0.3f, 1.1f), 0.04f);
                return MeshFactory.Combine("Statue_Obelisco",
                    (basePlinth, MeshFactory.TRS(new Vector3(0, 0.15f, 0), Vector3.zero)),
                    (shaft, Matrix4x4.identity));
            });
            ms.statues[3] = SaveMesh("Statue_Armilar", () =>
            {
                var ped = MeshFactory.Lathe(new[] { P(0,0), P(0.48f,0), P(0.48f,0.12f), P(0.22f,0.3f), P(0.15f,0.95f), P(0.3f,1.08f), P(0,1.08f) }, 40);
                var ball = MeshFactory.Sphere(0.38f, 40);
                var ring = MeshFactory.Torus(0.68f, 0.035f, 64, 10);
                Vector3 c = new Vector3(0, 1.75f, 0);
                return MeshFactory.Combine("Statue_Armilar",
                    (ped, Matrix4x4.identity), (ball, MeshFactory.TRS(c, Vector3.zero)),
                    (ring, MeshFactory.TRS(c, new Vector3(90, 0, 0))), (ring, MeshFactory.TRS(c, new Vector3(90, 60, 0))),
                    (ring, MeshFactory.TRS(c, new Vector3(23, 0, 0))));
            });
            ms.statues[4] = SaveMesh("Statue_Salomonica", () =>
            {
                var prof = new[] { P(0,0), P(0.55f,0), P(0.55f,0.15f), P(0.4f,0.25f), P(0.3f,0.32f), P(0.3f,0.6f), P(0.3f,1.0f), P(0.3f,1.4f), P(0.3f,1.8f), P(0.3f,2.05f), P(0.4f,2.15f), P(0.55f,2.3f), P(0.55f,2.45f), P(0,2.45f) };
                // radio con lóbulos que giran con la altura → columna salomónica
                var col = MeshFactory.Lathe(SubdivideProfile(prof, 0.05f), 48, "s", (y, a) => y > 0.32f && y < 2.05f ? 1f + 0.18f * Mathf.Sin(a * 3f + y * 7f) : 1f);
                return MeshFactory.Combine("Statue_Salomonica", (col, Matrix4x4.identity));
            });
            return ms;
        }

        static Vector2[] SubdivideProfile(Vector2[] prof, float step)
        {
            var l = new List<Vector2>();
            for (int i = 0; i < prof.Length - 1; i++)
            {
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(prof[i], prof[i + 1]) / step));
                for (int k = 0; k < n; k++) l.Add(Vector2.Lerp(prof[i], prof[i + 1], k / (float)n));
            }
            l.Add(prof[prof.Length - 1]);
            return l.ToArray();
        }

        // =================================================================== PREFABS
        public class Fabs
        {
            public Monolith small, large;
            public GameObject[] statues, goldenStatues;
            public GameObject acolyte, player;
        }

        internal static GameObject SavePrefab(string name, System.Func<GameObject> make)
        {
            string path = $"{FabDir}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing) return existing;
            var go = make();
            var fab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return fab;
        }

        internal static GameObject MeshGO(string name, Mesh mesh, Material mat, Transform parent = null, bool shadows = true)
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        static Fabs BuildPrefabs(Mats m, Meshes ms)
        {
            var f = new Fabs();
            string[] statueNames = { "Busto", "Anfora", "Obelisco", "Armilar", "Salomonica" };
            f.statues = new GameObject[5];
            f.goldenStatues = new GameObject[5];
            for (int i = 0; i < 5; i++)
            {
                int k = i;
                f.statues[i] = SavePrefab($"Statue_{statueNames[i]}", () => MeshGO($"Statue_{statueNames[k]}", ms.statues[k], m.statue));
                f.goldenStatues[i] = SavePrefab($"Statue_{statueNames[i]}_Oro", () => MeshGO($"Statue_{statueNames[k]}_Oro", ms.statues[k], m.statueGold));
            }

            f.large = SavePrefab("Monolith_Large", () => MakeMonolith("Monolith_Large", 3, 4, 3, true, ms, m, 1f, 42)).GetComponent<Monolith>();
            f.small = SavePrefab("Monolith_Small", () => MakeMonolith("Monolith_Small", 2, 3, 2, false, ms, m, 0.72f, 7)).GetComponent<Monolith>();

            f.acolyte = SavePrefab("Acolyte", () =>
            {
                var go = MeshGO("Acolyte", ms.sphere, m.acolyte, null, false);
                go.transform.localScale = Vector3.one * 0.38f;
                var tr = go.AddComponent<TrailRenderer>();
                tr.sharedMaterial = m.trail;
                tr.time = 0.35f;
                tr.startWidth = 0.22f; tr.endWidth = 0f;
                tr.minVertexDistance = 0.15f;
                tr.shadowCastingMode = ShadowCastingMode.Off;
                tr.startColor = new Color(0.7f, 0.9f, 1f, 1f); tr.endColor = new Color(0.7f, 0.9f, 1f, 0f);
                return go;
            });

            f.player = SavePrefab("PlayerMarble", () =>
            {
                var go = new GameObject("PlayerMarble");
                var body = MeshGO("Body", ms.playerSphere, m.player[0], go.transform);
                var sc = go.AddComponent<SphereCollider>();
                sc.radius = 0.75f;
                var pm = new PhysicsMaterial("MarbleBounce") { bounciness = 0.45f, dynamicFriction = 0.35f, staticFriction = 0.45f, bounceCombine = PhysicsMaterialCombine.Maximum, frictionCombine = PhysicsMaterialCombine.Average };
                string pmPath = $"{MatDir}/PM_Marble.physicMaterial";
                var existingPm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(pmPath);
                if (!existingPm) { AssetDatabase.CreateAsset(pm, pmPath); existingPm = pm; }
                sc.sharedMaterial = existingPm;
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 2f; rb.linearDamping = 0.12f; rb.angularDamping = 0.3f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                var trailGo = new GameObject("Trail");
                trailGo.transform.SetParent(go.transform, false);
                var tr = trailGo.AddComponent<TrailRenderer>();
                tr.sharedMaterial = m.trail;
                tr.time = 0.22f; tr.startWidth = 0.55f; tr.endWidth = 0f; tr.minVertexDistance = 0.2f;
                tr.shadowCastingMode = ShadowCastingMode.Off;
                tr.startColor = new Color(1f, 0.95f, 0.85f, 0.22f); tr.endColor = new Color(1f, 0.95f, 0.85f, 0f);
                tr.emitting = false;

                var p = go.AddComponent<PlayerMarble>();
                p.body = body.GetComponent<MeshRenderer>();
                p.prestigeMaterials = m.player;
                p.trail = tr;
                return go;
            });
            return f;
        }

        static GameObject MakeMonolith(string name, int nx, int ny, int nz, bool large, Meshes ms, Mats m, float statueScale, int seed)
        {
            var rnd = new System.Random(seed);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var go = new GameObject(name);
            var block = new GameObject("Block").transform;
            block.SetParent(go.transform, false);
            for (int y = 0; y < ny; y++)
            for (int x = 0; x < nx; x++)
            for (int z = 0; z < nz; z++)
            {
                bool interior = x > 0 && x < nx - 1 && z > 0 && z < nz - 1 && y > 0 && y < ny - 1;
                if (interior) continue;
                var c = MeshGO($"Chunk_{x}{y}{z}", ms.chunk, m.chunks[0], block);
                c.transform.localPosition = new Vector3(x - (nx - 1) * 0.5f + R(-0.025f, 0.025f), y + 0.5f, z - (nz - 1) * 0.5f + R(-0.025f, 0.025f));
                c.transform.localRotation = Quaternion.Euler(R(-3f, 3f), R(-4f, 4f), R(-3f, 3f));
                float s = R(0.95f, 1.0f);
                c.transform.localScale = new Vector3(s, R(0.95f, 1f), s);
            }
            var anchor = new GameObject("StatueAnchor").transform;
            anchor.SetParent(go.transform, false);
            var statueRoot = new GameObject("StatueScale").transform;
            statueRoot.SetParent(anchor, false);
            statueRoot.localScale = Vector3.one * statueScale;

            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, ny * 0.5f, 0);
            col.size = new Vector3(nx, ny, nz);

            var mono = go.AddComponent<Monolith>();
            mono.block = block;
            mono.statueAnchor = statueRoot;
            mono.large = large;
            return go;
        }

        // =================================================================== ESCENA
        static void BuildScene(EconomyConfig config, Mats m, Meshes ms, Fabs f)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------- Luz y volumen ----------
            var lighting = new GameObject("--- ILUMINACIÓN ---").transform;
            var sunGo = new GameObject("Sol");
            sunGo.transform.SetParent(lighting);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sunGo.AddComponent<HDAdditionalLightData>();
            sun.lightUnit = LightUnit.Lux;
            sun.intensity = 2600f;
            sun.useColorTemperature = true;
            sun.colorTemperature = 4700f;
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(36f, -38f, 0f);

            var volGo = new GameObject("Volumen Global");
            volGo.transform.SetParent(lighting);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = BuildVolumeProfile();

            var probeGo = new GameObject("Sonda de Reflexión");
            probeGo.transform.SetParent(lighting);
            probeGo.transform.position = new Vector3(0, 3f, 0);
            probeGo.AddComponent<ReflectionProbe>();
            var probe = probeGo.AddComponent<HDAdditionalReflectionData>();
            probe.mode = ProbeSettings.Mode.Realtime;
            probe.realtimeMode = ProbeSettings.RealtimeMode.OnEnable;
            var inf = probe.influenceVolume;
            inf.shape = InfluenceShape.Sphere;
            inf.sphereRadius = 70f;

            // ---------- Mundo ----------
            var world = new GameObject("--- SANTUARIO ---").transform;
            var floor = MeshGO("Suelo (Cuenco)", ms.floor, m.floor, world);
            floor.AddComponent<MeshCollider>().sharedMesh = ms.floor;
            floor.isStatic = true;

            var outer = MeshGO("Suelo Exterior", ms.outerDisc, m.outerGround, world, false);
            outer.transform.position = new Vector3(0, -6f, 0);
            outer.isStatic = true;

            var colonnade = new GameObject("Columnata").transform;
            colonnade.SetParent(world);
            int columns = 24;
            float colR = ArenaShape.TerraceRadius - 3f;
            for (int i = 0; i < columns; i++)
            {
                float a = i / (float)columns * Mathf.PI * 2f;
                var c = MeshGO($"Columna_{i:00}", ms.column, m.column, colonnade);
                c.transform.position = new Vector3(Mathf.Cos(a) * colR, ArenaShape.RimHeight, Mathf.Sin(a) * colR);
                c.AddComponent<CapsuleCollider>().radius = 1.0f;
                var cc = c.GetComponent<CapsuleCollider>(); cc.height = 12.6f; cc.center = new Vector3(0, 6.3f, 0);
                c.isStatic = true;
            }
            var beam = MeshGO("Arquitrabe", ms.ringBeam, m.column, world);
            beam.transform.position = new Vector3(0, ArenaShape.RimHeight + 12.6f, 0);
            beam.isStatic = true;
            var beamTrim = MeshGO("Moldura Dorada", ms.torus, m.gold, world, false);
            beamTrim.transform.position = new Vector3(0, ArenaShape.RimHeight + 12.6f + 0.95f, 0);
            beamTrim.transform.localScale = new Vector3(colR - 0.1f, 2f, colR - 0.1f);
            beamTrim.isStatic = true;

            // Anillo de latón donde empieza la ladera
            var rimInlay = MeshGO("Junta de Latón", ms.torus, m.inlay, world, false);
            rimInlay.transform.position = new Vector3(0, 0.0f, 0);
            rimInlay.transform.localScale = new Vector3(ArenaShape.FlatRadius, 0.4f, ArenaShape.FlatRadius);
            rimInlay.isStatic = true;

            // Braseros con luz cálida (8)
            var braziers = new GameObject("Braseros").transform;
            braziers.SetParent(world);
            for (int i = 0; i < 8; i++)
            {
                float a = (i + 0.5f) / 8f * Mathf.PI * 2f;
                Vector3 p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (ArenaShape.BowlRadius + 2.5f) + Vector3.up * ArenaShape.RimHeight;
                var b = MeshGO($"Brasero_{i}", ms.brazier, m.dark, braziers);
                b.transform.position = p;
                b.transform.localScale = Vector3.one * 1.3f;
                b.isStatic = true;
                var fire = MeshGO("Fuego", ms.sphere, m.fire, b.transform, false);
                fire.transform.localPosition = new Vector3(0, 1.78f, 0);
                fire.transform.localScale = new Vector3(1.1f, 0.35f, 1.1f);
                var lgo = new GameObject("Luz");
                lgo.transform.SetParent(b.transform, false);
                lgo.transform.localPosition = new Vector3(0, 2.6f, 0);
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Point;
                lgo.AddComponent<HDAdditionalLightData>();
                l.lightUnit = LightUnit.Lumen;
                l.intensity = 16000f;
                l.range = 22f;
                l.color = new Color(1f, 0.62f, 0.32f);
                l.shadows = LightShadows.None;
                lgo.AddComponent<Flicker>();
            }

            // Obeliscos lejanos (telón de fondo)
            var backdrop = new GameObject("Obeliscos Lejanos").transform;
            backdrop.SetParent(world);
            for (int i = 0; i < 6; i++)
            {
                float a = (i / 6f + 0.08f) * Mathf.PI * 2f;
                var o = MeshGO($"Obelisco_{i}", ms.obelisk, m.column, backdrop);
                o.transform.position = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (75f + i % 2 * 18f) + Vector3.down * 6f;
                o.transform.rotation = Quaternion.Euler(0, i * 23f, 0);
                o.transform.localScale = Vector3.one * (0.9f + (i % 3) * 0.2f);
                o.isStatic = true;
            }

            // ---------- Jugador ----------
            var player = (GameObject)PrefabUtility.InstantiatePrefab(f.player);
            player.transform.position = new Vector3(0, 1.2f, -2f);
            var pm = player.GetComponent<PlayerMarble>();

            // ---------- Cámara ----------
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 600f;
            var hdCam = camGo.AddComponent<HDAdditionalCameraData>();
            hdCam.antialiasing = HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camGo.AddComponent<AudioListener>();
            var rig = camGo.AddComponent<CameraRig>();
            rig.target = player.transform;
            camGo.transform.position = new Vector3(0, 12, -30);
            pm.cameraRig = rig;

            // ---------- Sistemas ----------
            var systems = new GameObject("--- SISTEMAS ---").transform;
            var gmGo = new GameObject("GameManager");
            gmGo.transform.SetParent(systems);
            gmGo.AddComponent<GameManager>().config = config;

            var audioGo = new GameObject("AudioFX");
            audioGo.transform.SetParent(systems);
            audioGo.AddComponent<AudioFX>();
            var rz = audioGo.AddComponent<AudioReverbZone>();
            rz.minDistance = 300f; rz.maxDistance = 400f;
            rz.reverbPreset = AudioReverbPreset.Auditorium;

            var shardGo = new GameObject("ShardSystem");
            shardGo.transform.SetParent(systems);
            var ss = shardGo.AddComponent<ShardSystem>();
            ss.shardMesh = ms.shard; ss.shardMaterials = m.shards;
            ss.debrisMesh = ms.chunk; ss.debrisMaterials = m.debris;
            ss.collector = player.transform;

            var spawnGo = new GameObject("MonolithSpawner");
            spawnGo.transform.SetParent(systems);
            var sp = spawnGo.AddComponent<MonolithSpawner>();
            sp.smallPrefab = f.small; sp.largePrefab = f.large;
            sp.statuePrefabs = f.statues; sp.goldenStatuePrefabs = f.goldenStatues;
            sp.tierMaterials = m.chunks; sp.goldenMaterial = m.goldenChunk;
            sp.player = player.transform;
            var pts = new GameObject("Pedestales (puntos de aparición)").transform;
            pts.SetParent(world);
            sp.spawnPointsRoot = pts;
            int[] counts = { 5, 7, 8 };
            float[] radii = { 6.5f, 11.5f, 16.5f };
            int n = 0;
            for (int ring = 0; ring < 3; ring++)
                for (int i = 0; i < counts[ring]; i++)
                {
                    float a = (i + ring * 0.37f) / counts[ring] * Mathf.PI * 2f;
                    var t = new GameObject($"Pedestal_{n++:00}").transform;
                    t.SetParent(pts);
                    t.position = new Vector3(Mathf.Cos(a) * radii[ring], 0, Mathf.Sin(a) * radii[ring]);
                    var pad = MeshGO("Anillo", ms.pad, m.inlay, t, false);
                    pad.transform.localScale = new Vector3(1f, 0.25f, 1f);
                    pad.transform.localPosition = new Vector3(0, 0.005f, 0);
                    pad.isStatic = true;
                }

            var swGo = new GameObject("Shockwave");
            swGo.transform.SetParent(systems);
            var sw = swGo.AddComponent<Shockwave>();
            sw.center = player.transform;
            var rings = new Transform[4];
            for (int i = 0; i < rings.Length; i++)
            {
                var r = MeshGO($"Anillo_{i}", ms.torus, m.ring, swGo.transform, false);
                rings[i] = r.transform;
                r.SetActive(false);
            }
            sw.rings = rings;

            var acoGo = new GameObject("Acólitos");
            acoGo.transform.SetParent(systems);
            var swarm = acoGo.AddComponent<AcolyteSwarm>();
            swarm.owner = player.transform;
            swarm.acolytePrefab = f.acolyte;

            // ---------- UI ----------
            var hudGo = new GameObject("HUD");
            hudGo.transform.SetParent(systems);
            var uidoc = hudGo.AddComponent<UIDocument>();
            uidoc.panelSettings = BuildPanelSettings();
            uidoc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UIDir + "/Hud.uxml");
            var hud = hudGo.AddComponent<Hud>();
            hud.worldCamera = cam;
            hud.cameraRig = rig;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        /// <summary>Crea el perfil del santuario. Si existe pero está vacío, lo rellena (nunca pisa overrides existentes).</summary>
        public static VolumeProfile BuildVolumeProfile()
        {
            string path = CfgDir + "/VP_Santuario.asset";
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (p && p.components.Count > 0) return p;
            if (!p)
            {
                p = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(p, path);
            }

            var ve = p.Add<VisualEnvironment>(true);
            ve.skyType.Override((int)SkyType.Gradient);
            ve.skyAmbientMode.Override(SkyAmbientMode.Dynamic);

            var sky = p.Add<GradientSky>(true);
            sky.top.Override(new Color(0.06f, 0.08f, 0.14f));
            sky.middle.Override(new Color(0.62f, 0.48f, 0.38f));
            sky.bottom.Override(new Color(0.10f, 0.08f, 0.07f));
            sky.gradientDiffusion.Override(1.6f);
            sky.skyIntensityMode.Override(SkyIntensityMode.Exposure);
            sky.exposure.Override(ExposureEV);

            var exp = p.Add<Exposure>(true);
            exp.mode.Override(ExposureMode.Fixed);
            exp.fixedExposure.Override(ExposureEV);

            var fog = p.Add<Fog>(true);
            fog.enabled.Override(true);
            fog.meanFreePath.Override(150f);
            fog.maximumHeight.Override(40f);
            fog.baseHeight.Override(-6f);
            fog.maxFogDistance.Override(2000f);

            var bloom = p.Add<Bloom>(true);
            bloom.intensity.Override(0.22f);
            bloom.scatter.Override(0.65f);

            var tm = p.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);

            var vig = p.Add<Vignette>(true);
            vig.intensity.Override(0.3f);
            vig.smoothness.Override(0.45f);

            var ca = p.Add<ColorAdjustments>(true);
            ca.contrast.Override(8f);
            ca.saturation.Override(6f);

            var ao = p.Add<ScreenSpaceAmbientOcclusion>(true);
            ao.intensity.Override(1.1f);

            var ssr = p.Add<ScreenSpaceReflection>(true);
            ssr.enabled.Override(true);

            var cs = p.Add<ContactShadows>(true);
            cs.enable.Override(true);

            var sh = p.Add<HDShadowSettings>(true);
            sh.maxShadowDistance.Override(110f);

            AddMotionBlurOff(p);

            foreach (var c in p.components)
            {
                c.name = c.GetType().Name;
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, p);
            }
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            return p;
        }

        /// <summary>El perfil por defecto de HDRP trae motion blur fuerte: lo neutraliza (aditivo).</summary>
        public static void AddMotionBlurOff(VolumeProfile p)
        {
            if (p.Has<MotionBlur>()) return;
            var mb = p.Add<MotionBlur>(true);
            mb.intensity.Override(0f);
            if (AssetDatabase.Contains(p) && !AssetDatabase.Contains(mb))
            {
                mb.name = nameof(MotionBlur);
                mb.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(mb, p);
                EditorUtility.SetDirty(p);
            }
        }

        static PanelSettings BuildPanelSettings()
        {
            string tss = UIDir + "/MarmorealTheme.tss";
            if (!File.Exists(tss))
            {
                File.WriteAllText(tss, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(tss);
            }
            return LoadOrCreate(UIDir + "/PanelSettings.asset", () =>
            {
                var ps = ScriptableObject.CreateInstance<PanelSettings>();
                ps.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(tss);
                ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                ps.referenceResolution = new Vector2Int(1920, 1080);
                ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                ps.match = 0.5f;
                return ps;
            });
        }
    }
}
