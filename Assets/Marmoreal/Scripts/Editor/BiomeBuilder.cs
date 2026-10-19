using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Marmoreal.EditorTools
{
    /// <summary>
    /// Paso ADITIVO e IDEMPOTENTE de las salas (S2): materiales por bioma, VolumeProfiles, BiomeDefs
    /// (Config/Biomes), objeto --- SALA --- con un hijo por bioma y BiomeManager bajo --- SISTEMAS ---.
    /// Solo crea lo que falte y solo rellena referencias vacías. Nunca borra ni pisa nada.
    /// </summary>
    public static class BiomeBuilder
    {
        const string MatDir = MarmorealBuilder.MatDir;
        const string BioMatDir = MatDir + "/Biomes";
        const string BioDir = "Assets/Marmoreal/Config/Biomes";

        [MenuItem("Marmoreal/Añadir salas: Volcánica y Cristal (aditivo)")]
        public static void AddBiomes()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != MarmorealBuilder.ScenePath) { Debug.LogError("[Marmoreal] Abre la escena Marmoreal antes."); return; }
            if (!AssetDatabase.IsValidFolder(BioDir)) AssetDatabase.CreateFolder("Assets/Marmoreal/Config", "Biomes");
            if (!AssetDatabase.IsValidFolder(BioMatDir)) AssetDatabase.CreateFolder(MatDir, "Biomes");

            var spawner = Object.FindFirstObjectByType<MonolithSpawner>();
            var shards = Object.FindFirstObjectByType<ShardSystem>();
            var sun = GameObject.Find("Sol")?.GetComponent<Light>();
            var volume = Object.FindFirstObjectByType<Volume>();
            if (!spawner || !shards || !sun || !volume) { Debug.LogError("[Marmoreal] Falta spawner/shards/Sol/Volumen en la escena."); return; }

            // ---------- Sala base: Carrara (copia lo que hay en escena) ----------
            var carrara = Def("Biome_Carrara", d =>
            {
                d.id = "carrara"; d.displayName = "Santuario de Carrara"; d.campaignStage = 0;
                d.tagline = "El taller original: mármol blanco y luz de atardecer.";
                d.chunkMaterials = (Material[])spawner.tierMaterials.Clone();
                d.shardMaterials = (Material[])shards.shardMaterials.Clone();
                d.sunColor = sun.color; d.sunIntensity = sun.intensity;
                d.volumeProfile = volume.sharedProfile;
            });
            var baseChunks = carrara.chunkMaterials;
            var baseShards = carrara.shardMaterials;
            var nero = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/M_Chunk_Nero.mat");
            var neroTex = nero ? nero.GetTexture("_BaseColorMap") : null;

            // ---------- Cantera Volcánica ----------
            var lavaCol = new Color(1f, 0.32f, 0.06f);
            var vChunks = Tint(baseChunks, "Volcanica", m => m.SetColor("_BaseColor", new Color(0.34f, 0.27f, 0.25f)));
            var vShards = TintShards(baseShards, "Volcanica", new Color(1f, 0.45f, 0.12f));
            var vSwaps = new Dictionary<string, Material>
            {
                ["M_Floor"] = Copy("M_Floor", "Volcanica", m =>
                {
                    m.SetColor("_BaseColor", new Color(0.24f, 0.2f, 0.19f));
                    if (neroTex) m.SetTexture("_EmissiveColorMap", neroTex); // vetas del Nero = juntas de lava
                    Emit(m, lavaCol, 10f);
                }),
                ["M_OuterGround"] = Copy("M_OuterGround", "Volcanica", m => m.SetColor("_BaseColor", new Color(0.12f, 0.09f, 0.08f))),
                ["M_Column"] = Copy("M_Column", "Volcanica", m => m.SetColor("_BaseColor", new Color(0.22f, 0.19f, 0.18f))),
                ["M_Gold"] = Copy("M_Gold", "Volcanica", m => m.SetColor("_BaseColor", new Color(0.85f, 0.32f, 0.12f))),
                ["M_BrassInlay"] = MarmorealBuilder.Emissive("Biomes/M_Lava", new Color(1f, 0.4f, 0.1f), lavaCol, 12f, 0.3f),
            };
            var vVP = Profile("VP_Volcanica", new Color(0.05f, 0.02f, 0.02f), new Color(0.55f, 0.18f, 0.08f), new Color(0.12f, 0.04f, 0.02f),
                new Color(1f, 0.82f, 0.72f), new Color(0.6f, 0.3f, 0.2f));
            var volcanica = Def("Biome_Volcanica", d =>
            {
                d.id = "volcanica"; d.displayName = "Cantera Volcánica"; d.campaignStage = 1;
                d.tagline = "+25 % bloques volcánicos · el suelo arde: ¡no te quedes quieta!";
                d.chunkMaterials = vChunks; d.shardMaterials = vShards; d.swaps = Swaps(vSwaps);
                d.sunColor = new Color(1f, 0.62f, 0.45f); d.sunIntensity = 1800f; d.volumeProfile = vVP;
                d.rule = BiomeRule.LavaFloor; d.ruleA = 2.5f; d.ruleB = 1.6f; d.volatileBonus = 0.25f;
            });

            // ---------- Gruta de Cristal ----------
            var iceCol = new Color(0.35f, 0.7f, 1f);
            var cChunks = Tint(baseChunks, "Cristal", m =>
            {
                m.SetColor("_BaseColor", new Color(0.55f, 0.75f, 1f));
                m.SetFloat("_SmoothnessRemapMax", 1f);
                m.SetFloat("_CoatMask", 1f);
                var t = m.GetTexture("_BaseColorMap");
                if (t) m.SetTexture("_EmissiveColorMap", t);
                Emit(m, new Color(0.15f, 0.45f, 1f), 7.5f);
            });
            var cShards = TintShards(baseShards, "Cristal", new Color(0.45f, 0.85f, 1f));
            var cSwaps = new Dictionary<string, Material>
            {
                ["M_Floor"] = Copy("M_Floor", "Cristal", m => { m.SetColor("_BaseColor", new Color(0.6f, 0.72f, 0.9f)); m.SetFloat("_CoatMask", 1f); }),
                ["M_OuterGround"] = Copy("M_OuterGround", "Cristal", m => m.SetColor("_BaseColor", new Color(0.3f, 0.36f, 0.5f))),
                ["M_Column"] = Copy("M_Column", "Cristal", m =>
                {
                    m.SetColor("_BaseColor", new Color(0.5f, 0.68f, 1f));
                    var t = m.GetTexture("_BaseColorMap");
                    if (t) m.SetTexture("_EmissiveColorMap", t);
                    Emit(m, iceCol, 6.5f);
                }),
                ["M_Gold"] = Copy("M_Gold", "Cristal", m => m.SetColor("_BaseColor", new Color(0.8f, 0.9f, 1f))),
                ["M_BrassInlay"] = MarmorealBuilder.Emissive("Biomes/M_CrystalGlow", new Color(0.6f, 0.9f, 1f), iceCol, 12f, 0.95f),
            };
            var cVP = Profile("VP_Cristal", new Color(0.02f, 0.04f, 0.1f), new Color(0.2f, 0.4f, 0.65f), new Color(0.03f, 0.05f, 0.1f),
                new Color(0.8f, 0.92f, 1f), new Color(0.35f, 0.55f, 0.85f));
            var cristal = Def("Biome_Cristal", d =>
            {
                d.id = "cristal"; d.displayName = "Gruta de Cristal"; d.campaignStage = 2;
                d.tagline = "Los bloques reflejan rayos al golpearlos · los trozos son metralla gratis.";
                d.chunkMaterials = cChunks; d.shardMaterials = cShards; d.swaps = Swaps(cSwaps);
                d.sunColor = new Color(0.72f, 0.85f, 1f); d.sunIntensity = 2000f; d.volumeProfile = cVP;
                d.rule = BiomeRule.CrystalReflect; d.ruleA = 0.35f; d.ruleB = 0.5f; d.ruleC = 0.4f;
            });

            // ---------- Escena: --- SALA --- ----------
            Transform sala = null, systems = null, world = null;
            foreach (var r in scene.GetRootGameObjects())
            {
                if (r.name == "--- SALA ---") sala = r.transform;
                if (r.name == "--- SISTEMAS ---") systems = r.transform;
                if (r.name == "--- SANTUARIO ---") world = r.transform;
            }
            if (!sala) { sala = new GameObject("--- SALA ---").transform; Undo.RegisterCreatedObjectUndo(sala.gameObject, "Sala"); }
            if (!systems) systems = new GameObject("--- SISTEMAS ---").transform;

            var sphere = AssetDatabase.LoadAssetAtPath<Mesh>(MarmorealBuilder.MeshDir + "/Sphere.asset");
            var obelisk = AssetDatabase.LoadAssetAtPath<Mesh>(MarmorealBuilder.MeshDir + "/Obelisk.asset");
            var roomC = Room(sala, "Santuario de Carrara", null);
            var roomV = Room(sala, "Cantera Volcánica", t =>
            {
                var rock = vSwaps["M_Column"];
                for (int i = 0; i < 8; i++)
                {
                    float a = (i + 0.5f) / 8f * Mathf.PI * 2f, r = 20f;
                    var pool = MarmorealBuilder.MeshGO($"Grieta de Lava_{i}", sphere, vSwaps["M_BrassInlay"], t, false);
                    pool.transform.position = new Vector3(Mathf.Cos(a) * r, 0.01f, Mathf.Sin(a) * r);
                    pool.transform.rotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0);
                    pool.transform.localScale = new Vector3(1.2f, 0.04f, 3.2f);
                    if (i % 2 == 0) PointLight(t, $"Brillo de Lava_{i}", pool.transform.position + Vector3.up, new Color(1f, 0.4f, 0.12f), 9000f, 12f, true);
                }
                for (int i = 0; i < 14; i++)
                {
                    float a = i / 14f * Mathf.PI * 2f + 0.2f, r = 47f + (i % 3) * 4f;
                    var go = MarmorealBuilder.MeshGO($"Roca_{i:00}", sphere, rock, t);
                    go.transform.position = new Vector3(Mathf.Cos(a) * r, -6f, Mathf.Sin(a) * r);
                    go.transform.localScale = new Vector3(6f + i % 4, 4f + (i * 7) % 5, 5f + i % 3);
                }
            });
            var roomG = Room(sala, "Gruta de Cristal", t =>
            {
                var glass = cSwaps["M_Column"];
                for (int i = 0; i < 18; i++)
                {
                    float a = i / 18f * Mathf.PI * 2f, r = 46f + (i % 3) * 3f;
                    var go = MarmorealBuilder.MeshGO($"Cristal_{i:00}", obelisk, glass, t);
                    go.transform.position = new Vector3(Mathf.Cos(a) * r, -6f, Mathf.Sin(a) * r);
                    go.transform.rotation = Quaternion.Euler((i % 2 == 0 ? 1 : -1) * (8f + i % 4 * 4f), i * 37f, (i % 3 - 1) * 10f);
                    go.transform.localScale = new Vector3(1.4f, 1.6f + (i % 4) * 0.5f, 1.4f);
                }
                for (int i = 0; i < 4; i++)
                {
                    float a = (i + 0.5f) / 4f * Mathf.PI * 2f;
                    PointLight(t, $"Luz Interior_{i}", new Vector3(Mathf.Cos(a) * 26f, 5f, Mathf.Sin(a) * 26f), new Color(0.4f, 0.75f, 1f), 14000f, 26f, false);
                }
            });

            var bm = Object.FindFirstObjectByType<BiomeManager>(FindObjectsInactive.Include);
            if (!bm)
            {
                var go = new GameObject("BiomeManager");
                go.transform.SetParent(systems);
                bm = go.AddComponent<BiomeManager>();
                Undo.RegisterCreatedObjectUndo(go, "BiomeManager");
            }
            if (bm.biomes == null || bm.biomes.Length == 0) bm.biomes = new[] { carrara, volcanica, cristal };
            if (bm.rooms == null || bm.rooms.Length == 0) bm.rooms = new[] { roomC, roomV, roomG };
            if (!bm.worldRoot) bm.worldRoot = world;
            if (!bm.sun) bm.sun = sun;
            if (!bm.volume) bm.volume = volume;
            if (!bm.reflectionProbe) bm.reflectionProbe = Object.FindFirstObjectByType<HDAdditionalReflectionData>();
            EditorUtility.SetDirty(bm);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Marmoreal] Salas añadidas (aditivo).");
        }

        // ================================================================= Ayudantes
        static BiomeDef Def(string file, System.Action<BiomeDef> fill)
        {
            string path = $"{BioDir}/{file}.asset";
            var d = AssetDatabase.LoadAssetAtPath<BiomeDef>(path);
            if (d) return d;
            d = ScriptableObject.CreateInstance<BiomeDef>();
            fill(d);
            AssetDatabase.CreateAsset(d, path);
            return d;
        }

        static List<MaterialSwap> Swaps(Dictionary<string, Material> map)
        {
            var l = new List<MaterialSwap>();
            foreach (var kv in map)
            {
                var from = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{kv.Key}.mat");
                if (from && kv.Value) l.Add(new MaterialSwap { from = from, to = kv.Value });
            }
            return l;
        }

        /// <summary>Copia un material (si la copia no existe) a Materials/Biomes/{name}_{suffix} y lo ajusta.</summary>
        static Material Copy(Material src, string suffix, System.Action<Material> tweak)
        {
            if (!src) return null;
            string path = $"{BioMatDir}/{src.name}_{suffix}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(src), path)) return null;
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            tweak(m);
            HDMaterial.ValidateMaterial(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Copy(string srcName, string suffix, System.Action<Material> tweak) =>
            Copy(AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{srcName}.mat"), suffix, tweak);

        static Material[] Tint(Material[] src, string suffix, System.Action<Material> tweak)
        {
            var r = new Material[src.Length];
            for (int i = 0; i < src.Length; i++) r[i] = Copy(src[i], suffix, tweak);
            return r;
        }

        static Material[] TintShards(Material[] src, string suffix, Color c)
        {
            var r = new Material[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                bool golden = i == src.Length - 1; // el último es el del bloque dorado: se queda dorado
                r[i] = golden ? src[i] : Copy(src[i], suffix, m =>
                {
                    var orig = m.GetColor("_BaseColor");
                    var col = Color.Lerp(orig, c, 0.7f);
                    m.SetColor("_BaseColor", col);
                    HDMaterial.SetEmissiveColor(m, col);
                });
            }
            return r;
        }

        static void Emit(Material m, Color c, float ev)
        {
            HDMaterial.SetUseEmissiveIntensity(m, true);
            HDMaterial.SetEmissiveColor(m, c);
            HDMaterial.SetEmissiveIntensity(m, ev, EmissiveIntensityUnit.EV100);
        }

        /// <summary>Copia VP_Santuario (si falta) y cambia cielo, niebla y tinte.</summary>
        static VolumeProfile Profile(string name, Color top, Color mid, Color bottom, Color filter, Color fogAlbedo)
        {
            string path = $"{BioDir}/{name}.asset";
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (p) return p;
            if (!AssetDatabase.CopyAsset("Assets/Marmoreal/Config/VP_Santuario.asset", path)) return null;
            p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (p.TryGet<GradientSky>(out var sky)) { sky.top.Override(top); sky.middle.Override(mid); sky.bottom.Override(bottom); }
            if (p.TryGet<Fog>(out var fog)) { fog.albedo.Override(fogAlbedo); fog.meanFreePath.Override(110f); }
            if (p.TryGet<ColorAdjustments>(out var ca)) ca.colorFilter.Override(filter);
            foreach (var c in p.components) EditorUtility.SetDirty(c);
            EditorUtility.SetDirty(p);
            return p;
        }

        static GameObject Room(Transform sala, string name, System.Action<Transform> decorate)
        {
            var t = sala.Find(name);
            if (t) return t.gameObject;
            t = new GameObject(name).transform;
            t.SetParent(sala, false);
            decorate?.Invoke(t);
            if (decorate != null) t.gameObject.SetActive(false); // solo al crear: la sala base queda visible en el editor
            return t.gameObject;
        }

        static void PointLight(Transform parent, string name, Vector3 pos, Color c, float lumen, float range, bool flicker)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            go.AddComponent<HDAdditionalLightData>();
            l.lightUnit = LightUnit.Lumen;
            l.intensity = lumen;
            l.range = range;
            l.color = c;
            l.shadows = LightShadows.None;
            if (flicker) go.AddComponent<Flicker>();
        }
    }
}
