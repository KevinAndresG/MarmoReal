using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Marmoreal.EditorTools
{
    /// <summary>
    /// Ambientación (S12), ADITIVA e IDEMPOTENTE: cielo/niebla claros por sala (VolumeProfiles), suelo exterior con textura y color por sala,
    /// paisaje (colinas, montañas, árboles, ruinas, islas flotantes…) como GameObjects reales bajo --- SANTUARIO --- / --- SALA ---,
    /// y la fisura de la Grieta (textura + material M_Crack + quad "Grieta" en los prefabs de bloque).
    /// Solo crea lo que falta; los grupos "Paisaje" ya existentes no se tocan (edítalos a mano). Meshes/texturas/materiales quedan como assets.
    /// </summary>
    public static class SceneryBuilder
    {
        const string Root = "Assets/Marmoreal";
        const string MeshDir = Root + "/Meshes";
        const string TexDir = Root + "/Textures";
        const string MatDir = Root + "/Materials";
        const string SceneMatDir = MatDir + "/Scenery";
        const float GroundY = -6f;

        // ============================================================ Menús
        [MenuItem("Marmoreal/Ambientación: cielo, suelo y paisaje (aditivo)")]
        public static void AddAll()
        {
            if (EditorSceneManager.GetActiveScene().path != MarmorealBuilder.ScenePath) { Debug.LogError("[Marmoreal] Abre la escena Marmoreal antes."); return; }
            if (!AssetDatabase.IsValidFolder(SceneMatDir)) AssetDatabase.CreateFolder(MatDir, "Scenery");
            ApplyAtmosphere();
            ApplyOuterGround();
            AddScenery();
            AddCrack();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[Marmoreal] Ambientación aplicada.");
        }

        // ============================================================ Cielo y niebla
        struct Sky { public string path; public Color top, mid, bottom, fog; }

        static void ApplyAtmosphere()
        {
            var skies = new[]
            {
                new Sky { path = Root + "/Config/VP_Santuario.asset",        top = C(0.20f, 0.42f, 0.90f), mid = C(1.00f, 0.84f, 0.66f), bottom = C(0.50f, 0.44f, 0.38f), fog = C(0.80f, 0.86f, 0.95f) },
                new Sky { path = Root + "/Config/Biomes/VP_Volcanica.asset", top = C(0.34f, 0.15f, 0.20f), mid = C(1.00f, 0.50f, 0.24f), bottom = C(0.30f, 0.15f, 0.10f), fog = C(0.80f, 0.55f, 0.45f) },
                new Sky { path = Root + "/Config/Biomes/VP_Cristal.asset",   top = C(0.20f, 0.22f, 0.70f), mid = C(0.70f, 0.62f, 1.00f), bottom = C(0.28f, 0.28f, 0.44f), fog = C(0.70f, 0.70f, 0.95f) },
                new Sky { path = Root + "/Config/Biomes/VP_Hielo.asset",     top = C(0.26f, 0.52f, 0.98f), mid = C(0.86f, 0.95f, 1.00f), bottom = C(0.50f, 0.56f, 0.64f), fog = C(0.86f, 0.93f, 1.00f) },
                new Sky { path = Root + "/Config/Biomes/VP_Forja.asset",     top = C(0.32f, 0.26f, 0.26f), mid = C(0.95f, 0.56f, 0.30f), bottom = C(0.28f, 0.22f, 0.20f), fog = C(0.72f, 0.58f, 0.50f) },
                new Sky { path = Root + "/Config/Biomes/VP_Jardin.asset",    top = C(0.30f, 0.56f, 0.96f), mid = C(1.00f, 0.80f, 0.82f), bottom = C(0.40f, 0.42f, 0.35f), fog = C(0.92f, 0.86f, 0.92f) },
                new Sky { path = Root + "/Config/Biomes/VP_Panteon.asset",   top = C(0.36f, 0.36f, 0.72f), mid = C(1.00f, 0.86f, 0.42f), bottom = C(0.46f, 0.38f, 0.24f), fog = C(0.95f, 0.88f, 0.65f) },
            };
            foreach (var s in skies)
            {
                var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(s.path);
                if (!p) { Debug.LogWarning("[Marmoreal] Falta " + s.path); continue; }
                if (p.TryGet<GradientSky>(out var sky))
                {
                    sky.top.Override(s.top); sky.middle.Override(s.mid); sky.bottom.Override(s.bottom);
                    sky.gradientDiffusion.Override(1.2f);
                }
                if (p.TryGet<Fog>(out var fog))
                {
                    fog.colorMode.Override(FogColorMode.ConstantColor);
                    fog.color.Override(s.fog);
                    fog.meanFreePath.Override(240f); // lejos: bruma suave que tiñe colinas y montañas sin ensuciar la arena
                }
                if (p.TryGet<ColorAdjustments>(out var ca)) ca.saturation.Override(14f);
                if (p.TryGet<Exposure>(out var ex)) ex.fixedExposure.Override(9.0f); // 0,5 EV más luminoso que antes (9,5)
                foreach (var c in p.components) EditorUtility.SetDirty(c);
                EditorUtility.SetDirty(p);
            }
        }

        // ============================================================ Suelo exterior
        static void ApplyOuterGround()
        {
            var tex = GroundTexture();
            var tints = new Dictionary<string, Color>
            {
                [MatDir + "/M_OuterGround.mat"] = C(0.46f, 0.55f, 0.30f),
                [MatDir + "/Biomes/M_OuterGround_Volcanica.mat"] = C(0.36f, 0.24f, 0.20f),
                [MatDir + "/Biomes/M_OuterGround_Cristal.mat"] = C(0.42f, 0.42f, 0.62f),
                [MatDir + "/Biomes/M_OuterGround_Hielo.mat"] = C(0.92f, 0.96f, 1.00f),
                [MatDir + "/Biomes/M_OuterGround_Hierro.mat"] = C(0.44f, 0.38f, 0.35f),
                [MatDir + "/Biomes/M_OuterGround_Rosa.mat"] = C(0.46f, 0.62f, 0.34f),
                [MatDir + "/Biomes/M_OuterGround_Oro.mat"] = C(0.80f, 0.68f, 0.40f),
            };
            foreach (var kv in tints)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(kv.Key);
                if (!m) continue;
                m.SetTexture("_BaseColorMap", tex);
                m.SetTextureScale("_BaseColorMap", new Vector2(0.03f, 0.03f));
                m.SetColor("_BaseColor", kv.Value);
                m.SetFloat("_Smoothness", 0.2f); m.SetFloat("_SmoothnessRemapMin", 0.1f); m.SetFloat("_SmoothnessRemapMax", 0.35f);
                HDMaterial.ValidateMaterial(m);
                EditorUtility.SetDirty(m);
            }
        }

        static Texture2D GroundTexture()
        {
            string path = TexDir + "/T_GroundNoise.png";
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t) return t;
            const int N = 256;
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float v = 0.5f * Tile(x, y, N, 6f, 11f) + 0.3f * Tile(x, y, N, 14f, 71f) + 0.2f * Tile(x, y, N, 40f, 33f);
                    float g = Mathf.Lerp(0.62f, 1f, v);
                    px[y * N + x] = new Color(g, g * 0.98f, g * 0.94f, 1f);
                }
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            File.WriteAllBytes(Path.GetFullPath(path), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Ruido Perlin que se repite sin costuras (mezcla de 4 muestras desplazadas).</summary>
        static float Tile(int x, int y, int n, float freq, float seed)
        {
            float fx = x / (float)n, fy = y / (float)n;
            float a = Mathf.PerlinNoise(seed + fx * freq, seed + fy * freq);
            float b = Mathf.PerlinNoise(seed + (fx - 1f) * freq, seed + fy * freq);
            float c = Mathf.PerlinNoise(seed + fx * freq, seed + (fy - 1f) * freq);
            float d = Mathf.PerlinNoise(seed + (fx - 1f) * freq, seed + (fy - 1f) * freq);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        // ============================================================ Grieta (fisura)
        static void AddCrack()
        {
            string texPath = TexDir + "/T_Crack.png";
            if (!AssetDatabase.LoadAssetAtPath<Texture2D>(texPath)) MakeCrackTexture(texPath);
            string matPath = MatDir + "/M_Crack.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!mat)
            {
                mat = new Material(Shader.Find("HDRP/Unlit")) { name = "M_Crack" };
                AssetDatabase.CreateAsset(mat, matPath);
                mat.SetTexture("_UnlitColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
                mat.SetColor("_UnlitColor", new Color(0.55f, 0.85f, 1f, 1f));
                mat.SetFloat("_AlphaCutoffEnable", 1f); mat.SetFloat("_AlphaCutoff", 0.45f);
                mat.SetFloat("_DoubleSidedEnable", 1f);
                HDMaterial.ValidateMaterial(mat);
                EditorUtility.SetDirty(mat);
            }
            foreach (var name in new[] { "Monolith_Small", "Monolith_Large", "Monolith_Coloso" })
            {
                string pp = $"{Root}/Prefabs/{name}.prefab";
                if (!File.Exists(pp)) continue;
                var root = PrefabUtility.LoadPrefabContents(pp);
                if (!root.transform.Find("Grieta"))
                {
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    q.name = "Grieta";
                    UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
                    q.transform.SetParent(root.transform, false);
                    var r = q.GetComponent<MeshRenderer>();
                    r.sharedMaterial = mat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    r.enabled = false;
                    var mono = root.GetComponent<Monolith>();
                    if (mono) mono.crackFx = r;
                    PrefabUtility.SaveAsPrefabAsset(root, pp);
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Fisura irregular: tronco ramificado de líneas finas con núcleo claro, sobre transparente.</summary>
        static void MakeCrackTexture(string path)
        {
            const int N = 256;
            var a = new float[N * N];
            var rnd = new System.Random(7);
            void Line(Vector2 p0, Vector2 p1, float w)
            {
                int x0 = Mathf.Max(0, (int)Mathf.Min(p0.x, p1.x) - 4), x1 = Mathf.Min(N - 1, (int)Mathf.Max(p0.x, p1.x) + 4);
                int y0 = Mathf.Max(0, (int)Mathf.Min(p0.y, p1.y) - 4), y1 = Mathf.Min(N - 1, (int)Mathf.Max(p0.y, p1.y) + 4);
                Vector2 d = p1 - p0; float l2 = Mathf.Max(d.sqrMagnitude, 1e-4f);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float t = Mathf.Clamp01(Vector2.Dot(new Vector2(x, y) - p0, d) / l2);
                        float dist = Vector2.Distance(new Vector2(x, y), p0 + d * t);
                        a[y * N + x] = Mathf.Max(a[y * N + x], Mathf.Clamp01((w - dist) / 1.2f + 0.5f));
                    }
            }
            void Walk(Vector2 p, float ang, int steps, float w, int depth)
            {
                for (int i = 0; i < steps; i++)
                {
                    ang += ((float)rnd.NextDouble() - 0.5f) * 1.1f;
                    float len = 9f + (float)rnd.NextDouble() * 11f;
                    var n = p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * len;
                    float ww = w * (1f - i / (float)steps * 0.6f);
                    Line(p, n, ww);
                    if (depth < 2 && rnd.NextDouble() < 0.33)
                        Walk(n, ang + ((rnd.NextDouble() < 0.5) ? 0.8f : -0.8f), Mathf.Max(2, steps / 2), ww * 0.65f, depth + 1);
                    p = n;
                    if (p.x < 8 || p.y < 8 || p.x > N - 8 || p.y > N - 8) break;
                }
            }
            Walk(new Vector2(N * 0.5f, N * 0.5f), 0.5f, 9, 2.4f, 0);
            Walk(new Vector2(N * 0.5f, N * 0.5f), 0.5f + Mathf.PI, 9, 2.4f, 0);
            var px = new Color[N * N];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(1f, 1f, 1f, a[i]);
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            File.WriteAllBytes(Path.GetFullPath(path), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.alphaIsTransparency = true; ti.mipmapEnabled = true; ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
        }

        // ============================================================ Paisaje
        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        static void AddScenery()
        {
            var sanct = GameObject.Find("--- SANTUARIO ---");
            var sala = GameObject.Find("--- SALA ---");
            if (!sanct || !sala) { Debug.LogError("[Marmoreal] Falta --- SANTUARIO --- o --- SALA ---."); return; }

            // ---- Común (sus materiales base los cambia BiomeManager con los swaps de cada sala)
            if (!sanct.transform.Find("Paisaje Común")) BuildCommon(sanct.transform);

            // ---- Por sala
            Room(sala.transform, "Santuario de Carrara", C(0.38f, 0.48f, 0.30f), (g, rnd, m) =>
            {
                var cyp = Mat("Cipres", C(0.10f, 0.26f, 0.12f), 0.15f);
                var olive = Mat("OlivoCopa", C(0.34f, 0.44f, 0.20f), 0.15f); var bark = Mat("Corteza", C(0.24f, 0.17f, 0.11f), 0.1f);
                Scatter(g, rnd, "Ciprés", Mesh("S_Cypress"), new[] { cyp }, 110, 52, 230, 3f, 4.5f, 14f, 26f, true);
                Scatter(g, rnd, "Olivo", Mesh("S_Tree"), new[] { bark, olive }, 55, 52, 200, 6f, 9f, 7f, 11f, true);
                Islands(g, rnd, 7, m.rock, m.top, Mesh("S_Cypress"), cyp);
            });
            Room(sala.transform, "Cantera Volcánica", C(0.22f, 0.14f, 0.12f), (g, rnd, m) =>
            {
                var ash = Mat("CenizaRoca", C(0.16f, 0.12f, 0.11f), 0.25f);
                var lava = Mat("LavaBrillo", C(0.9f, 0.25f, 0.05f), 0.4f, 0f, C(1f, 0.32f, 0.06f), 260f);
                Scatter(g, rnd, "Volcán", Mesh("S_Volcano"), new[] { ash, lava }, 6, 150, 280, 40f, 70f, 35f, 65f, false);
                Scatter(g, rnd, "Charco de lava", Mesh("S_Disc"), new[] { lava }, 18, 60, 210, 4f, 14f, 1f, 1f, false, GroundY + 0.08f);
                Scatter(g, rnd, "Aguja de ceniza", Mesh("S_Cone"), new[] { ash }, 80, 52, 220, 4f, 10f, 10f, 32f, true);
                Islands(g, rnd, 8, ash, lava, Mesh("S_Cone"), ash);
            });
            Room(sala.transform, "Gruta de Cristal", C(0.28f, 0.28f, 0.46f), (g, rnd, m) =>
            {
                var cA = Mat("CristalCian", C(0.3f, 0.8f, 1f), 0.9f, 0f, C(0.3f, 0.8f, 1f), 90f);
                var cB = Mat("CristalVioleta", C(0.6f, 0.4f, 1f), 0.9f, 0f, C(0.6f, 0.4f, 1f), 90f);
                var cC = Mat("CristalRosa", C(1f, 0.45f, 0.8f), 0.9f, 0f, C(1f, 0.45f, 0.8f), 80f);
                Scatter(g, rnd, "Cristal cian", Mesh("S_Crystal"), new[] { cA }, 50, 52, 260, 3f, 9f, 10f, 45f, false, GroundY, 14f);
                Scatter(g, rnd, "Cristal violeta", Mesh("S_Crystal"), new[] { cB }, 45, 52, 260, 3f, 9f, 10f, 45f, false, GroundY, 14f);
                Scatter(g, rnd, "Cristal rosa", Mesh("S_Crystal"), new[] { cC }, 30, 52, 260, 3f, 8f, 8f, 36f, false, GroundY, 14f);
                Islands(g, rnd, 9, m.rock, cA, Mesh("S_Crystal"), cA);
            });
            Room(sala.transform, "Catedral de Hielo", C(0.84f, 0.92f, 1f), (g, rnd, m) =>
            {
                var ice = Mat("HieloAguja", C(0.70f, 0.88f, 1f), 0.95f, 0f, C(0.45f, 0.75f, 1f), 12f);
                var snow = Mat("NievePino", C(0.88f, 0.94f, 1f), 0.2f); var bark = Mat("Corteza", C(0.24f, 0.17f, 0.11f), 0.1f);
                Scatter(g, rnd, "Aguja de hielo", Mesh("S_Spire"), new[] { ice }, 90, 52, 260, 4f, 10f, 15f, 60f, false);
                Scatter(g, rnd, "Pino nevado", Mesh("S_Pine"), new[] { bark, snow }, 80, 52, 220, 5f, 8f, 14f, 24f, true);
                Islands(g, rnd, 7, ice, snow, Mesh("S_Spire"), ice);
            });
            Room(sala.transform, "Forja de Hierro", C(0.24f, 0.22f, 0.22f), (g, rnd, m) =>
            {
                var iron = Mat("HierroTorre", C(0.20f, 0.19f, 0.2f), 0.5f, 0.9f);
                var rust = Mat("HierroOxido", C(0.38f, 0.2f, 0.12f), 0.35f, 0.7f);
                var glow = Mat("HornoBrillo", C(1f, 0.45f, 0.1f), 0.4f, 0f, C(1f, 0.45f, 0.1f), 220f);
                Scatter(g, rnd, "Torre de hierro", Mesh("S_Chimney"), new[] { iron, glow }, 30, 60, 250, 6f, 12f, 25f, 60f, true);
                Scatter(g, rnd, "Engranaje", Mesh("S_Gear"), new[] { rust }, 12, 70, 230, 12f, 26f, 12f, 26f, false, GroundY + 2f);
                Scatter(g, rnd, "Horno", Mesh("S_Cube"), new[] { glow }, 36, 52, 200, 3f, 6f, 1f, 2f, false, GroundY + 0.3f);
                Islands(g, rnd, 7, iron, rust, Mesh("S_Chimney"), iron);
            });
            Room(sala.transform, "Jardín de Rosa", C(0.36f, 0.52f, 0.30f), (g, rnd, m) =>
            {
                var bloom = Mat("FlorCopa", C(0.96f, 0.56f, 0.72f), 0.2f, 0f, C(0.96f, 0.5f, 0.68f), 6f);
                var leaf = Mat("HojaVerde", C(0.30f, 0.50f, 0.22f), 0.15f); var bark = Mat("Corteza", C(0.28f, 0.19f, 0.13f), 0.1f);
                Scatter(g, rnd, "Cerezo", Mesh("S_Tree"), new[] { bark, bloom }, 80, 52, 220, 7f, 11f, 8f, 13f, true);
                Scatter(g, rnd, "Arbusto", Mesh("S_Sphere"), new[] { leaf }, 90, 52, 200, 3f, 6f, 2f, 4f, true, GroundY + 0.3f);
                Scatter(g, rnd, "Arbusto en flor", Mesh("S_Sphere"), new[] { bloom }, 60, 52, 200, 2.5f, 5f, 1.5f, 3f, true, GroundY + 0.3f);
                Islands(g, rnd, 8, m.rock, leaf, Mesh("S_Tree"), bloom, bark);
            });
            Room(sala.transform, "Panteón Dorado", C(0.72f, 0.60f, 0.34f), (g, rnd, m) =>
            {
                var gold = Mat("OroTemplo", C(0.95f, 0.72f, 0.28f), 0.7f, 1f, C(1f, 0.7f, 0.25f), 8f);
                var cream = Mat("Crema", C(0.92f, 0.86f, 0.72f), 0.45f);
                var cyp = Mat("CipresDorado", C(0.55f, 0.50f, 0.15f), 0.15f);
                Scatter(g, rnd, "Obelisco dorado", Mesh("S_Spire"), new[] { gold }, 20, 60, 240, 4f, 7f, 24f, 55f, false);
                Scatter(g, rnd, "Templo", Mesh("S_Temple"), new[] { cream, gold }, 10, 90, 220, 9f, 14f, 9f, 14f, true);
                Scatter(g, rnd, "Ciprés", Mesh("S_Cypress"), new[] { cyp }, 50, 52, 220, 3f, 4.5f, 14f, 24f, true);
                Rings(g, rnd, 12, gold);
                Islands(g, rnd, 6, cream, gold, Mesh("S_Spire"), gold);
            });
        }

        // ------------------------------------------------------------ Común
        static void BuildCommon(Transform sanct)
        {
            var g = new GameObject("Paisaje Común").transform;
            g.SetParent(sanct, false);
            var rnd = new System.Random(4242);
            var col = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/M_Column.mat");
            var statue = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/M_Statue.mat");
            var colMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/Column.asset");
            // columnas en ruinas: unas en pie (truncadas) y otras caídas
            for (int i = 0; i < 34; i++)
            {
                float a = (float)rnd.NextDouble() * 360f, r = 52f + (float)rnd.NextDouble() * 70f;
                Vector3 p = Pol(a, r, GroundY);
                bool fallen = rnd.NextDouble() < 0.4;
                float s = 1.6f + (float)rnd.NextDouble() * 1.4f;
                var rot = fallen ? Quaternion.Euler(90f + R(rnd, 8), (float)rnd.NextDouble() * 360f, 0) : Quaternion.Euler(R(rnd, 4), (float)rnd.NextDouble() * 360f, R(rnd, 4));
                Prop(g, fallen ? "Columna caída" : "Columna rota", colMesh, new[] { col }, p + (fallen ? Vector3.up * s * 0.7f : Vector3.zero), rot,
                    new Vector3(s, fallen ? s : s * (0.35f + (float)rnd.NextDouble() * 0.5f), s), true);
            }
            // arcos
            var cube = Mesh("S_Cube");
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f + R(rnd, 14), r = 60f + (float)rnd.NextDouble() * 55f;
                var arc = new GameObject("Arco_" + i).transform;
                arc.SetParent(g, false);
                arc.position = Pol(a, r, GroundY);
                arc.rotation = Quaternion.Euler(0, a + 90f, 0);
                float h = 9f + (float)rnd.NextDouble() * 4f, w = 8f + (float)rnd.NextDouble() * 3f;
                Prop(arc, "Pilar L", cube, new[] { col }, arc.position + arc.right * -w * 0.5f + Vector3.up * h * 0.5f, arc.rotation, new Vector3(1.8f, h, 1.8f), true);
                Prop(arc, "Pilar R", cube, new[] { col }, arc.position + arc.right * w * 0.5f + Vector3.up * h * 0.5f, arc.rotation, new Vector3(1.8f, h, 1.8f), true);
                Prop(arc, "Dintel", cube, new[] { col }, arc.position + Vector3.up * (h + 0.7f), arc.rotation, new Vector3(w + 3f, 1.6f, 2.2f), true);
            }
            // estatuas sobre pedestales
            string[] meshes = { "Statue_Busto", "Statue_Obelisco", "Statue_Anfora", "Statue_Armilar" };
            for (int i = 0; i < 14; i++)
            {
                float a = i * (360f / 14f) + R(rnd, 8), r = 54f + (float)rnd.NextDouble() * 34f;
                var m = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshDir}/{meshes[i % meshes.Length]}.asset");
                if (!m) continue;
                Vector3 p = Pol(a, r, GroundY);
                Prop(g, "Pedestal", cube, new[] { col }, p + Vector3.up * 1.2f, Quaternion.Euler(0, a, 0), new Vector3(3.2f, 2.4f, 3.2f), true);
                Prop(g, "Estatua", m, new[] { statue }, p + Vector3.up * 2.4f, Quaternion.Euler(0, a + 180f, 0), Vector3.one * 3.2f, true);
            }
        }

        // ------------------------------------------------------------ Sala
        struct RoomMats { public Material rock, top; }

        static void Room(Transform sala, string roomName, Color hills, Action<Transform, System.Random, RoomMats> fill)
        {
            var room = sala.Find(roomName);
            if (!room) { Debug.LogWarning("[Marmoreal] Falta la sala " + roomName); return; }
            if (room.Find("Paisaje")) return; // ya construido: se edita a mano
            var g = new GameObject("Paisaje").transform;
            g.SetParent(room, false);
            var rnd = new System.Random(roomName.GetHashCode() & 0xffff);
            var hillMat = Mat("Colinas_" + roomName.Replace(" ", ""), hills, 0.12f);
            var rock = Mat("Roca_" + roomName.Replace(" ", ""), Color.Lerp(hills, new Color(0.35f, 0.33f, 0.32f), 0.55f), 0.2f);
            var top = Mat("Cima_" + roomName.Replace(" ", ""), hills, 0.15f);
            Prop(g, "Colinas y montañas", Hills(), new[] { hillMat }, new Vector3(0, GroundY, 0), Quaternion.identity, Vector3.one, false);
            fill(g, rnd, new RoomMats { rock = rock, top = top });
        }

        /// <summary>Esparce n copias de un mesh en el anillo [rMin,rMax] sobre el suelo exterior.</summary>
        static void Scatter(Transform parent, System.Random rnd, string name, Mesh mesh, Material[] mats, int n, float rMin, float rMax,
            float sxz0, float sxz1, float sy0, float sy1, bool shadows, float y = GroundY, float tilt = 0f)
        {
            var g = new GameObject(name + "s").transform;
            g.SetParent(parent, false);
            for (int i = 0; i < n; i++)
            {
                float a = (float)rnd.NextDouble() * 360f;
                float r = Mathf.Lerp(rMin, rMax, Mathf.Sqrt((float)rnd.NextDouble()));
                float sxz = Mathf.Lerp(sxz0, sxz1, (float)rnd.NextDouble()), sy = Mathf.Lerp(sy0, sy1, (float)rnd.NextDouble());
                var rot = Quaternion.Euler(R(rnd, tilt), (float)rnd.NextDouble() * 360f, R(rnd, tilt));
                Prop(g, name + "_" + i.ToString("00"), mesh, mats, Pol(a, r, y), rot, new Vector3(sxz, sy, sxz), shadows && r < 110f);
            }
        }

        /// <summary>Islotes de roca que flotan y se mecen en el cielo, con un adorno encima.</summary>
        static void Islands(Transform parent, System.Random rnd, int n, Material rock, Material top, Mesh deco, Material decoMat, Material decoMat2 = null)
        {
            var g = new GameObject("Islotes flotantes").transform;
            g.SetParent(parent, false);
            var isl = Mesh("S_Island");
            for (int i = 0; i < n; i++)
            {
                float a = i * (360f / n) + R(rnd, 20), r = 80f + (float)rnd.NextDouble() * 120f;
                float s = 8f + (float)rnd.NextDouble() * 14f, y = 22f + (float)rnd.NextDouble() * 55f;
                var go = Prop(g, "Islote_" + i.ToString("00"), isl, new[] { rock }, Pol(a, r, y), Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0), new Vector3(s, s, s), false);
                GameObjectUtility.SetStaticEditorFlags(go, 0);
                var sp = go.AddComponent<Spinner>();
                sp.degreesPerSecond = new Vector3(0, 1.5f + (float)rnd.NextDouble() * 2f, 0); sp.bobAmplitude = 0.8f + (float)rnd.NextDouble() * 1.2f; sp.bobSpeed = 0.4f + (float)rnd.NextDouble() * 0.4f;
                var mats = decoMat2 ? new[] { decoMat2, decoMat } : new[] { decoMat };
                var d = Prop(go.transform, "Adorno", deco, mats, go.transform.position, go.transform.rotation, Vector3.one * s * 0.35f, false);
                GameObjectUtility.SetStaticEditorFlags(d, 0);
                d.transform.SetParent(go.transform, true);
                d.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
            }
        }

        /// <summary>Anillos dorados que giran lentamente en el cielo (Panteón).</summary>
        static void Rings(Transform parent, System.Random rnd, int n, Material mat)
        {
            var g = new GameObject("Anillos flotantes").transform;
            g.SetParent(parent, false);
            var ring = Mesh("S_Torus");
            for (int i = 0; i < n; i++)
            {
                float a = i * (360f / n) + R(rnd, 12), r = 90f + (float)rnd.NextDouble() * 110f, s = 8f + (float)rnd.NextDouble() * 10f;
                var go = Prop(g, "Anillo_" + i.ToString("00"), ring, new[] { mat }, Pol(a, r, 30f + (float)rnd.NextDouble() * 50f), Quaternion.Euler(70f + R(rnd, 20), (float)rnd.NextDouble() * 360f, 0), Vector3.one * s, false);
                GameObjectUtility.SetStaticEditorFlags(go, 0);
                var sp = go.AddComponent<Spinner>(); sp.degreesPerSecond = new Vector3(0, 0, 8f + (float)rnd.NextDouble() * 10f); sp.bobAmplitude = 1f; sp.bobSpeed = 0.5f;
            }
        }

        // ============================================================ Ayudantes
        static Vector3 Pol(float deg, float r, float y) => new Vector3(Mathf.Sin(deg * Mathf.Deg2Rad) * r, y, Mathf.Cos(deg * Mathf.Deg2Rad) * r);
        static float R(System.Random rnd, float amp) => ((float)rnd.NextDouble() * 2f - 1f) * amp;

        static GameObject Prop(Transform parent, string name, Mesh mesh, Material[] mats, Vector3 pos, Quaternion rot, Vector3 scale, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mesh && mesh.subMeshCount > mats.Length ? Pad(mats, mesh.subMeshCount) : mats;
            mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        static Material[] Pad(Material[] m, int n)
        {
            var r = new Material[n];
            for (int i = 0; i < n; i++) r[i] = m[Mathf.Min(i, m.Length - 1)];
            return r;
        }

        static Material Mat(string name, Color c, float smooth = 0.3f, float metal = 0f, Color? emit = null, float nits = 0f)
        {
            if (!AssetDatabase.IsValidFolder(SceneMatDir)) AssetDatabase.CreateFolder(MatDir, "Scenery");
            string path = $"{SceneMatDir}/M_{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m) return m;
            m = new Material(Shader.Find("HDRP/Lit")) { name = "M_" + name };
            AssetDatabase.CreateAsset(m, path);
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            if (emit.HasValue)
            {
                HDMaterial.SetUseEmissiveIntensity(m, true);
                HDMaterial.SetEmissiveColor(m, emit.Value);
                HDMaterial.SetEmissiveIntensity(m, nits, EmissiveIntensityUnit.Nits);
            }
            HDMaterial.ValidateMaterial(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------ Meshes (se generan una vez y se guardan)
        static Mesh Mesh(string name)
        {
            string path = $"{MeshDir}/{name}.asset";
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (m) return m;
            m = name switch
            {
                "S_Cone" => MeshFactory.Lathe(new[] { V(0, 0), V(1, 0), V(0, 1) }, 12, name, (h, a) => 1f + 0.12f * Mathf.Sin(a * 3f + h * 4f)),
                "S_Cypress" => MeshFactory.Lathe(new[] { V(0, 0), V(0.35f, 0.02f), V(0.5f, 0.25f), V(0.42f, 0.6f), V(0.2f, 0.88f), V(0, 1) }, 12, name, (h, a) => 1f + 0.08f * Mathf.Sin(a * 5f + h * 9f)),
                "S_Crystal" => MeshFactory.Lathe(new[] { V(0, 0), V(0.55f, 0.12f), V(0.45f, 0.72f), V(0, 1) }, 6, name, null, 0f, true),
                "S_Spire" => MeshFactory.Lathe(new[] { V(0, 0), V(0.6f, 0), V(0.32f, 0.5f), V(0, 1) }, 8, name, (h, a) => 1f + 0.18f * Mathf.Sin(a * 2f + h * 6f), 0f, true),
                "S_Disc" => MeshFactory.Lathe(new[] { V(0, 0), V(1, 0), V(0, 0) }, 24, name),
                "S_Island" => MeshFactory.Lathe(new[] { V(0, -1.1f), V(0.25f, -0.8f), V(0.7f, -0.35f), V(1, -0.05f), V(0.95f, 0), V(0, 0) }, 14, name, (h, a) => 1f + 0.18f * Mathf.Sin(a * 3f + h * 5f) + 0.1f * Mathf.Sin(a * 7f), 0f, true),
                "S_Torus" => MeshFactory.Torus(1f, 0.06f, 64, 12, name),
                "S_Cube" => Copy(Resources.GetBuiltinResource<Mesh>("Cube.fbx"), name, true),
                "S_Sphere" => Copy(Resources.GetBuiltinResource<Mesh>("Sphere.fbx"), name, false),
                "S_Tree" => Combine(name, MeshFactory.Lathe(new[] { V(0, 0), V(0.12f, 0), V(0.08f, 0.5f), V(0, 0.5f) }, 8, "t"),
                    MeshFactory.Combine("c", (Copy(Resources.GetBuiltinResource<Mesh>("Sphere.fbx"), "s", false), MeshFactory.TRS(new Vector3(0, 0.75f, 0), Vector3.zero, new Vector3(0.7f, 0.5f, 0.7f))),
                        (Copy(Resources.GetBuiltinResource<Mesh>("Sphere.fbx"), "s", false), MeshFactory.TRS(new Vector3(0.28f, 0.62f, 0.1f), Vector3.zero, new Vector3(0.42f, 0.34f, 0.42f))),
                        (Copy(Resources.GetBuiltinResource<Mesh>("Sphere.fbx"), "s", false), MeshFactory.TRS(new Vector3(-0.25f, 0.66f, -0.15f), Vector3.zero, new Vector3(0.45f, 0.36f, 0.45f))))),
                "S_Pine" => Combine(name, MeshFactory.Lathe(new[] { V(0, 0), V(0.1f, 0), V(0.07f, 0.3f), V(0, 0.3f) }, 8, "t"),
                    MeshFactory.Combine("c", (MeshFactory.Lathe(new[] { V(0, 0), V(0.5f, 0), V(0, 0.45f) }, 10, "k"), MeshFactory.TRS(new Vector3(0, 0.18f, 0), Vector3.zero, 1f)),
                        (MeshFactory.Lathe(new[] { V(0, 0), V(0.4f, 0), V(0, 0.4f) }, 10, "k"), MeshFactory.TRS(new Vector3(0, 0.45f, 0), Vector3.zero, 1f)),
                        (MeshFactory.Lathe(new[] { V(0, 0), V(0.28f, 0), V(0, 0.38f) }, 10, "k"), MeshFactory.TRS(new Vector3(0, 0.72f, 0), Vector3.zero, 1f)))),
                "S_Volcano" => Combine(name, MeshFactory.Lathe(new[] { V(0, 0), V(1, 0), V(0.62f, 0.4f), V(0.34f, 0.8f), V(0.26f, 0.86f), V(0.18f, 0.8f), V(0, 0.8f) }, 28, "v", (h, a) => 1f + 0.16f * Mathf.Sin(a * 4f + h * 3f) + 0.08f * Mathf.Sin(a * 9f)),
                    MeshFactory.Lathe(new[] { V(0, 0.79f), V(0.19f, 0.79f), V(0, 0.79f) }, 20, "l")),
                "S_Chimney" => Combine(name, MeshFactory.Lathe(new[] { V(0, 0), V(0.5f, 0), V(0.34f, 0.9f), V(0.4f, 1f), V(0, 1f) }, 12, "c"),
                    MeshFactory.Lathe(new[] { V(0, 0.99f), V(0.32f, 0.99f), V(0.32f, 1.02f), V(0, 1.02f) }, 12, "g")),
                "S_Gear" => MeshFactory.Lathe(new[] { V(0, -0.12f), V(1, -0.12f), V(1, 0.12f), V(0, 0.12f) }, 16, name, (h, a) => (Mathf.Sin(a * 8f) > 0 ? 1f : 0.86f), 0f, true),
                "S_Temple" => Combine(name, MeshFactory.Combine("c",
                        (Copy(Resources.GetBuiltinResource<Mesh>("Cube.fbx"), "b", true), MeshFactory.TRS(new Vector3(0, 0.05f, 0), Vector3.zero, new Vector3(1.2f, 0.1f, 0.9f))),
                        (Copy(Resources.GetBuiltinResource<Mesh>("Cube.fbx"), "b", true), MeshFactory.TRS(new Vector3(0, 0.82f, 0), Vector3.zero, new Vector3(1.15f, 0.08f, 0.85f))),
                        (Copy(Resources.GetBuiltinResource<Mesh>("Cube.fbx"), "b", true), MeshFactory.TRS(new Vector3(-0.45f, 0.43f, -0.32f), Vector3.zero, new Vector3(0.1f, 0.7f, 0.1f))),
                        (Copy(Resources.GetBuiltinResource<Mesh>("Cube.fbx"), "b", true), MeshFactory.TRS(new Vector3(0.45f, 0.43f, -0.32f), Vector3.zero, new Vector3(0.1f, 0.7f, 0.1f))),
                        (Copy(Resources.GetBuiltinResource<Mesh>("Cube.fbx"), "b", true), MeshFactory.TRS(new Vector3(-0.45f, 0.43f, 0.32f), Vector3.zero, new Vector3(0.1f, 0.7f, 0.1f))),
                        (Copy(Resources.GetBuiltinResource<Mesh>("Cube.fbx"), "b", true), MeshFactory.TRS(new Vector3(0.45f, 0.43f, 0.32f), Vector3.zero, new Vector3(0.1f, 0.7f, 0.1f)))),
                    MeshFactory.Lathe(new[] { V(0, 0.86f), V(0.78f, 0.86f), V(0, 1.15f) }, 4, "r", null, Mathf.PI / 4f, true)),
                _ => throw new ArgumentException(name)
            };
            m.name = name;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Vector2 V(float r, float h) => new Vector2(r, h);

        static Mesh Copy(Mesh src, string name, bool flat)
        {
            var m = UnityEngine.Object.Instantiate(src); m.name = name; return m;
        }

        /// <summary>Dos partes en un mesh con 2 submeshes (material 0 = a, material 1 = b).</summary>
        static Mesh Combine(string name, Mesh a, Mesh b)
        {
            var m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.CombineMeshes(new[]
            {
                new CombineInstance { mesh = a, transform = Matrix4x4.identity },
                new CombineInstance { mesh = b, transform = Matrix4x4.identity },
            }, false, false);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Anillo de relieve: llanura baja junto al santuario → colinas → montañas lejanas (se guarda una vez).</summary>
        static Mesh Hills() => BuildHills(false);

        /// <summary>Regenera el relieve (misma asset, los objetos que lo usan se actualizan).</summary>
        public static void RegenHills() { BuildHills(true); AssetDatabase.SaveAssets(); }

        static Mesh BuildHills(bool force)
        {
            const string path = MeshDir + "/S_Hills.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing && !force) return existing;
            const int ang = 256, rad = 44;
            const float r0 = 105f, r1 = 450f;
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            for (int j = 0; j <= rad; j++)
            {
                float t = j / (float)rad, r = Mathf.Lerp(r0, r1, t * t * 0.4f + t * 0.6f);
                float amp = 0.2f + 40f * Mathf.Pow(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.04f, 0.8f, t)), 1.7f) * (1f - 0.97f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.82f, 1f, t)));
                for (int i = 0; i <= ang; i++)
                {
                    float a = i / (float)ang * Mathf.PI * 2f, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    float n1 = Mathf.PerlinNoise(ca * 2.2f + 10f + t * 3f, sa * 2.2f + 10f);
                    float n2 = Mathf.PerlinNoise(ca * 6f + 40f + t * 5f, sa * 6f + 40f);
                    float ridge = 1f - Mathf.Abs(2f * n2 - 1f);
                    float h = amp * (0.25f + 0.55f * n1 + 0.5f * ridge * ridge);
                    verts.Add(new Vector3(ca * r, h, sa * r));
                    uvs.Add(new Vector2(ca * r, sa * r) / 40f);
                }
            }
            int cols = ang + 1;
            for (int j = 0; j < rad; j++)
                for (int i = 0; i < ang; i++)
                {
                    int a = j * cols + i, b = a + 1, c = a + cols, d = c + 1;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
            var m = new Mesh { name = "S_Hills", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
            if (existing) { existing.Clear(); EditorUtility.CopySerialized(m, existing); EditorUtility.SetDirty(existing); return existing; }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
