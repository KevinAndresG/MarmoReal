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
            var vChunks = Tint(baseChunks, "Volcanica", m => m.SetColor("_BaseColor", new Color(0.8f, 0.7f, 0.66f)));
            var vShards = TintShards(baseShards, "Volcanica", new Color(1f, 0.45f, 0.12f));
            var vSwaps = new Dictionary<string, Material>
            {
                ["M_Floor"] = Copy("M_Floor", "Volcanica", m =>
                {
                    m.SetColor("_BaseColor", new Color(0.88f, 0.7f, 0.62f));
                    if (neroTex) m.SetTexture("_EmissiveColorMap", neroTex); // vetas del Nero = juntas de lava
                    Emit(m, lavaCol, 10f);
                }),
                ["M_OuterGround"] = Copy("M_OuterGround", "Volcanica", m => m.SetColor("_BaseColor", new Color(0.45f, 0.33f, 0.3f))),
                ["M_Column"] = Copy("M_Column", "Volcanica", m => m.SetColor("_BaseColor", new Color(0.72f, 0.56f, 0.5f))),
                ["M_Gold"] = Copy("M_Gold", "Volcanica", m => m.SetColor("_BaseColor", new Color(0.85f, 0.32f, 0.12f))),
                ["M_BrassInlay"] = MarmorealBuilder.Emissive("Biomes/M_Lava", new Color(1f, 0.4f, 0.1f), lavaCol, 12f, 0.3f),
            };
            var vVP = Profile("VP_Volcanica", new Color(0.101f, 0.071f, 0.104f), new Color(0.836f, 0.426f, 0.281f), new Color(0.136f, 0.071f, 0.052f),
                new Color(1f, 0.978f, 0.966f), new Color(0.84f, 0.72f, 0.68f));
            var volcanica = Def("Biome_Volcanica", d =>
            {
                d.id = "volcanica"; d.displayName = "Cantera Volcánica"; d.campaignStage = 1;
                d.tagline = "+25 % bloques volcánicos · el suelo arde: ¡no te quedes quieta!";
                d.chunkMaterials = vChunks; d.shardMaterials = vShards; d.swaps = Swaps(vSwaps);
                d.sunColor = new Color(1f, 0.867f, 0.807f); d.sunIntensity = 2600f; d.volumeProfile = vVP;
                d.rule = BiomeRule.LavaFloor; d.ruleA = 2.5f; d.ruleB = 1.6f; d.volatileBonus = 0.25f;
            });

            // ---------- Gruta de Cristal ----------
            var iceCol = new Color(0.35f, 0.7f, 1f);
            var cChunks = Tint(baseChunks, "Cristal", m =>
            {
                m.SetColor("_BaseColor", new Color(0.85f, 0.93f, 1f));
                m.SetFloat("_SmoothnessRemapMax", 1f);
                m.SetFloat("_CoatMask", 1f);
                var t = m.GetTexture("_BaseColorMap");
                if (t) m.SetTexture("_EmissiveColorMap", t);
                Emit(m, new Color(0.15f, 0.45f, 1f), 7.5f);
            });
            var cShards = TintShards(baseShards, "Cristal", new Color(0.45f, 0.85f, 1f));
            var cSwaps = new Dictionary<string, Material>
            {
                ["M_Floor"] = Copy("M_Floor", "Cristal", m => { m.SetColor("_BaseColor", new Color(0.82f, 0.9f, 1f)); m.SetFloat("_CoatMask", 1f); }),
                ["M_OuterGround"] = Copy("M_OuterGround", "Cristal", m => m.SetColor("_BaseColor", new Color(0.55f, 0.62f, 0.75f))),
                ["M_Column"] = Copy("M_Column", "Cristal", m =>
                {
                    m.SetColor("_BaseColor", new Color(0.75f, 0.85f, 1f));
                    var t = m.GetTexture("_BaseColorMap");
                    if (t) m.SetTexture("_EmissiveColorMap", t);
                    Emit(m, iceCol, 6.5f);
                }),
                ["M_Gold"] = Copy("M_Gold", "Cristal", m => m.SetColor("_BaseColor", new Color(0.8f, 0.9f, 1f))),
                ["M_BrassInlay"] = MarmorealBuilder.Emissive("Biomes/M_CrystalGlow", new Color(0.6f, 0.9f, 1f), iceCol, 12f, 0.95f),
            };
            var cVP = Profile("VP_Cristal", new Color(0.051f, 0.08f, 0.167f), new Color(0.461f, 0.505f, 0.6f), new Color(0.078f, 0.082f, 0.115f),
                new Color(0.976f, 0.99f, 1f), new Color(0.74f, 0.82f, 0.94f));
            var cristal = Def("Biome_Cristal", d =>
            {
                d.id = "cristal"; d.displayName = "Gruta de Cristal"; d.campaignStage = 2;
                d.tagline = "Los bloques reflejan rayos al golpearlos · los trozos son metralla gratis.";
                d.chunkMaterials = cChunks; d.shardMaterials = cShards; d.swaps = Swaps(cSwaps);
                d.sunColor = new Color(0.902f, 0.948f, 1f); d.sunIntensity = 2600f; d.volumeProfile = cVP;
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

        // ================================================================= Salas 4-7 (S6)
        [MenuItem("Marmoreal/Añadir salas 4-7: Hielo, Forja, Jardín y Panteón (aditivo)")]
        public static void AddBiomes47()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != MarmorealBuilder.ScenePath) { Debug.LogError("[Marmoreal] Abre la escena Marmoreal antes."); return; }
            var bm = Object.FindFirstObjectByType<BiomeManager>(FindObjectsInactive.Include);
            var carrara = AssetDatabase.LoadAssetAtPath<BiomeDef>($"{BioDir}/Biome_Carrara.asset");
            var volcanica = AssetDatabase.LoadAssetAtPath<BiomeDef>($"{BioDir}/Biome_Volcanica.asset");
            Transform sala = null;
            foreach (var r in scene.GetRootGameObjects()) if (r.name == "--- SALA ---") sala = r.transform;
            if (!bm || !carrara || !sala) { Debug.LogError("[Marmoreal] Antes: menú 'Añadir salas: Volcánica y Cristal'."); return; }
            var baseChunks = carrara.chunkMaterials;
            var baseShards = carrara.shardMaterials;
            var sphere = AssetDatabase.LoadAssetAtPath<Mesh>(MarmorealBuilder.MeshDir + "/Sphere.asset");
            var obelisk = AssetDatabase.LoadAssetAtPath<Mesh>(MarmorealBuilder.MeshDir + "/Obelisk.asset");
            var column = AssetDatabase.LoadAssetAtPath<Mesh>(MarmorealBuilder.MeshDir + "/Column.asset");

            // ---------- 4. Catedral de Hielo: suelo resbaladizo ----------
            var frost = new Color(0.6f, 0.85f, 1f);
            var hChunks = Tint(baseChunks, "Hielo", m => { m.SetColor("_BaseColor", new Color(0.82f, 0.92f, 1f)); m.SetFloat("_SmoothnessRemapMax", 1f); m.SetFloat("_CoatMask", 1f); });
            var hSwaps = new Dictionary<string, Material>
            {
                ["M_Floor"] = Copy("M_Floor", "Hielo", m => { m.SetColor("_BaseColor", new Color(0.8f, 0.9f, 1f)); m.SetFloat("_SmoothnessRemapMax", 1f); m.SetFloat("_CoatMask", 1f); }),
                ["M_OuterGround"] = Copy("M_OuterGround", "Hielo", m => m.SetColor("_BaseColor", new Color(0.85f, 0.92f, 1f))),
                ["M_Column"] = Copy("M_Column", "Hielo", m => { m.SetColor("_BaseColor", new Color(0.75f, 0.88f, 1f)); m.SetFloat("_CoatMask", 1f); }),
                ["M_Gold"] = Copy("M_Gold", "Hielo", m => m.SetColor("_BaseColor", new Color(0.85f, 0.95f, 1f))),
                ["M_BrassInlay"] = MarmorealBuilder.Emissive("Biomes/M_Escarcha", new Color(0.8f, 0.95f, 1f), frost, 9f, 0.95f),
            };
            var hielo = Def("Biome_Hielo", d =>
            {
                d.id = "hielo"; d.displayName = "Catedral de Hielo"; d.campaignStage = 3; d.trialGallery = 115;
                d.tagline = "Suelo de hielo: más velocidad, poco agarre · usa la inercia y la Embestida.";
                d.chunkMaterials = hChunks; d.swaps = Swaps(hSwaps);
                d.sunColor = new Color(0.937f, 0.965f, 1f); d.sunIntensity = 2600f;
                d.volumeProfile = Profile("VP_Hielo", new Color(0.059f, 0.081f, 0.129f), new Color(0.544f, 0.494f, 0.466f), new Color(0.087f, 0.082f, 0.086f), new Color(0.988f, 0.995f, 1f), new Color(0.94f, 0.968f, 1f));
                d.rule = BiomeRule.IceFloor; d.ruleA = 0.55f; d.ruleB = 1.3f; d.ruleC = 0.01f;
            });

            // ---------- 5. Forja de Hierro: metal (más HP, polvo ×3) e imanes ----------
            var ember = new Color(1f, 0.5f, 0.15f);
            var fChunks = Tint(baseChunks, "Hierro", m => { m.SetColor("_BaseColor", new Color(0.8f, 0.82f, 0.86f)); m.SetFloat("_Metallic", 0.9f); m.SetFloat("_SmoothnessRemapMax", 0.75f); });
            var magnetMat = MarmorealBuilder.Emissive("Biomes/M_Iman", new Color(0.3f, 0.3f, 0.33f), ember, 11f, 0.7f);
            var fSwaps = new Dictionary<string, Material>
            {
                ["M_Floor"] = Copy("M_Floor", "Hierro", m => { m.SetColor("_BaseColor", new Color(0.74f, 0.74f, 0.78f)); m.SetFloat("_Metallic", 0.3f); }),
                ["M_OuterGround"] = Copy("M_OuterGround", "Hierro", m => m.SetColor("_BaseColor", new Color(0.45f, 0.44f, 0.46f))),
                ["M_Column"] = Copy("M_Column", "Hierro", m => { m.SetColor("_BaseColor", new Color(0.7f, 0.7f, 0.74f)); m.SetFloat("_Metallic", 0.85f); }),
                ["M_Gold"] = Copy("M_Gold", "Hierro", m => m.SetColor("_BaseColor", new Color(0.95f, 0.5f, 0.2f))),
                ["M_BrassInlay"] = MarmorealBuilder.Emissive("Biomes/M_Ascua", new Color(1f, 0.55f, 0.2f), ember, 10f, 0.4f),
            };
            var forja = Def("Biome_Forja", d =>
            {
                d.id = "forja"; d.displayName = "Forja de Hierro"; d.campaignStage = 4; d.trialGallery = 135;
                d.tagline = "Bloques de metal: ×2 de vida, ×3 de polvo · los imanes del suelo atraen tu canica.";
                d.chunkMaterials = fChunks; d.shardMaterials = volcanica ? volcanica.shardMaterials : null; d.swaps = Swaps(fSwaps);
                d.hpMult = 2f; d.dustMult = 3f;
                d.sunColor = new Color(1f, 0.912f, 0.843f); d.sunIntensity = 2600f;
                d.volumeProfile = Profile("VP_Forja", new Color(0.078f, 0.078f, 0.111f), new Color(0.7f, 0.464f, 0.305f), new Color(0.111f, 0.078f, 0.061f), new Color(1f, 0.986f, 0.974f), new Color(0.8f, 0.74f, 0.7f));
                d.rule = BiomeRule.IronForge;
            });

            // ---------- 6. Jardín de Rosa: los bloques crecen si no los rompes ----------
            var petal = new Color(1f, 0.45f, 0.7f);
            var leaf = new Color(0.3f, 0.75f, 0.35f);
            var jChunks = Tint(baseChunks, "Rosa", m => m.SetColor("_BaseColor", new Color(1f, 0.9f, 0.93f)));
            var leafMat = Copy("M_Column", "Hoja", m => m.SetColor("_BaseColor", leaf));
            var jSwaps = new Dictionary<string, Material>
            {
                ["M_Floor"] = Copy("M_Floor", "Rosa", m => m.SetColor("_BaseColor", new Color(1f, 0.85f, 0.88f))),
                ["M_OuterGround"] = Copy("M_OuterGround", "Rosa", m => m.SetColor("_BaseColor", new Color(0.35f, 0.6f, 0.3f))),
                ["M_Column"] = Copy("M_Column", "Rosa", m => m.SetColor("_BaseColor", new Color(0.95f, 0.75f, 0.8f))),
                ["M_Gold"] = Copy("M_Gold", "Rosa", m => m.SetColor("_BaseColor", new Color(0.55f, 0.85f, 0.45f))),
                ["M_BrassInlay"] = MarmorealBuilder.Emissive("Biomes/M_Petalo", petal, petal, 8f, 0.5f),
            };
            var jardin = Def("Biome_Jardin", d =>
            {
                d.id = "jardin"; d.displayName = "Jardín de Rosa"; d.campaignStage = 5; d.trialGallery = 155;
                d.tagline = "Los bloques crecen si no los rompes: más vida, pero también más polvo.";
                d.chunkMaterials = jChunks; d.swaps = Swaps(jSwaps);
                d.sunColor = new Color(1f, 0.958f, 0.948f); d.sunIntensity = 2600f;
                d.volumeProfile = Profile("VP_Jardin", new Color(0.06f, 0.081f, 0.134f), new Color(0.621f, 0.474f, 0.433f), new Color(0.087f, 0.084f, 0.065f), new Color(1f, 0.994f, 0.995f), new Color(1f, 0.94f, 0.96f));
                d.rule = BiomeRule.GardenGrowth; d.ruleA = 8f; d.ruleB = 1.35f; d.ruleC = 4f;
            });

            // ---------- 7. Panteón Dorado: todo oro, bloques gigantes, El Coloso de Mármol ----------
            var goldC = new Color(1f, 0.78f, 0.35f);
            var pChunks = Tint(baseChunks, "Oro", m => { m.SetColor("_BaseColor", new Color(1f, 0.86f, 0.55f)); m.SetFloat("_Metallic", 0.9f); m.SetFloat("_SmoothnessRemapMax", 0.85f); });
            var pShards = new Material[baseShards.Length];
            for (int i = 0; i < pShards.Length; i++) pShards[i] = baseShards[baseShards.Length - 1]; // polvo dorado
            var pSwaps = new Dictionary<string, Material>
            {
                ["M_Floor"] = Copy("M_Floor", "Oro", m => { m.SetColor("_BaseColor", new Color(1f, 0.88f, 0.62f)); m.SetFloat("_SmoothnessRemapMax", 0.95f); }),
                ["M_OuterGround"] = Copy("M_OuterGround", "Oro", m => m.SetColor("_BaseColor", new Color(0.6f, 0.5f, 0.35f))),
                ["M_Column"] = Copy("M_Column", "Oro", m => { m.SetColor("_BaseColor", goldC); m.SetFloat("_Metallic", 0.9f); }),
                ["M_Gold"] = Copy("M_Gold", "Oro", m => m.SetColor("_BaseColor", new Color(1f, 0.85f, 0.45f))),
                ["M_BrassInlay"] = MarmorealBuilder.Emissive("Biomes/M_OroVivo", goldC, goldC, 10f, 0.9f),
            };
            var panteon = Def("Biome_Panteon", d =>
            {
                d.id = "panteon"; d.displayName = "Panteón Dorado"; d.campaignStage = 6; d.trialGallery = 175;
                d.tagline = "La sala final: todo es oro, los bloques son gigantes y te espera El Coloso de Mármol.";
                d.chunkMaterials = pChunks; d.shardMaterials = pShards; d.swaps = Swaps(pSwaps);
                d.sunColor = new Color(1f, 0.948f, 0.86f); d.sunIntensity = 2600f;
                d.volumeProfile = Profile("VP_Panteon", new Color(0.079f, 0.078f, 0.1f), new Color(0.632f, 0.485f, 0.296f), new Color(0.099f, 0.081f, 0.061f), new Color(1f, 0.99f, 0.974f), new Color(0.84f, 0.8f, 0.72f));
                d.rule = BiomeRule.GoldenPantheon; d.ruleA = 0.25f; d.ruleB = 1.5f;
            });

            // ---------- Escena: hijos de --- SALA --- ----------
            var roomH = Room(sala, "Catedral de Hielo", t =>
            {
                for (int i = 0; i < 16; i++)
                {
                    float a = i / 16f * Mathf.PI * 2f + 0.1f, r = 45f + (i % 3) * 3f;
                    var go = MarmorealBuilder.MeshGO($"Carámbano_{i:00}", obelisk, hSwaps["M_Column"], t);
                    go.transform.position = new Vector3(Mathf.Cos(a) * r, -6f, Mathf.Sin(a) * r);
                    go.transform.rotation = Quaternion.Euler((i % 2 == 0 ? 1 : -1) * 6f, i * 23f, 0);
                    go.transform.localScale = new Vector3(1.6f, 2f + (i % 4) * 0.6f, 1.6f);
                }
                for (int i = 0; i < 3; i++)
                {
                    float a = (i + 0.5f) / 3f * Mathf.PI * 2f;
                    PointLight(t, $"Luz Fría_{i}", new Vector3(Mathf.Cos(a) * 24f, 6f, Mathf.Sin(a) * 24f), frost, 12000f, 26f, false);
                }
            });
            var roomF = Room(sala, "Forja de Hierro", t =>
            {
                for (int i = 0; i < 4; i++)
                {
                    float a = (i + 0.25f) / 4f * Mathf.PI * 2f, r = 15f;
                    var pos = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    pos.y = ArenaShape.Height(pos) + 0.02f;
                    var go = MarmorealBuilder.MeshGO($"Imán_{i}", sphere, magnetMat, t, false);
                    go.transform.position = pos;
                    go.transform.localScale = new Vector3(2.4f, 0.12f, 2.4f);
                    go.AddComponent<Magnet>();
                    PointLight(t, $"Brillo del Imán_{i}", pos + Vector3.up * 1.2f, ember, 5000f, 9f, true);
                }
                for (int i = 0; i < 12; i++)
                {
                    float a = i / 12f * Mathf.PI * 2f + 0.15f, r = 47f + (i % 2) * 4f;
                    var go = MarmorealBuilder.MeshGO($"Pilar de Hierro_{i:00}", column, fSwaps["M_Column"], t);
                    go.transform.position = new Vector3(Mathf.Cos(a) * r, -4f, Mathf.Sin(a) * r);
                    go.transform.localScale = new Vector3(1.4f, 1.2f + (i % 3) * 0.3f, 1.4f);
                }
            });
            var roomJ = Room(sala, "Jardín de Rosa", t =>
            {
                var flower = jSwaps["M_BrassInlay"];
                for (int i = 0; i < 22; i++)
                {
                    float a = i / 22f * Mathf.PI * 2f, r = 44f + (i % 4) * 2.5f;
                    var bush = MarmorealBuilder.MeshGO($"Rosal_{i:00}", sphere, leafMat, t);
                    bush.transform.position = new Vector3(Mathf.Cos(a) * r, -1.5f, Mathf.Sin(a) * r);
                    bush.transform.localScale = Vector3.one * (3.5f + i % 3);
                    var rose = MarmorealBuilder.MeshGO($"Rosa_{i:00}", sphere, flower, bush.transform, false);
                    rose.transform.localPosition = new Vector3(0.2f, 0.45f, 0.1f);
                    rose.transform.localScale = Vector3.one * 0.35f;
                }
                PointLight(t, "Luz de Jardín", new Vector3(0, 14f, 0), new Color(1f, 0.8f, 0.85f), 16000f, 40f, false);
            });
            var roomP = Room(sala, "Panteón Dorado", t =>
            {
                for (int i = 0; i < 14; i++)
                {
                    float a = i / 14f * Mathf.PI * 2f, r = 46f;
                    var go = MarmorealBuilder.MeshGO($"Obelisco de Oro_{i:00}", obelisk, pSwaps["M_Column"], t);
                    go.transform.position = new Vector3(Mathf.Cos(a) * r, -5f, Mathf.Sin(a) * r);
                    go.transform.localScale = new Vector3(1.8f, 2.6f + (i % 2) * 0.8f, 1.8f);
                }
                for (int i = 0; i < 4; i++)
                {
                    float a = (i + 0.5f) / 4f * Mathf.PI * 2f;
                    PointLight(t, $"Luz Dorada_{i}", new Vector3(Mathf.Cos(a) * 25f, 6f, Mathf.Sin(a) * 25f), goldC, 12000f, 26f, false);
                }
            });

            // ---------- BiomeManager: añade al final solo lo que falte (mismo índice sala/def) ----------
            var defs = new List<BiomeDef>(bm.biomes ?? new BiomeDef[0]);
            var rooms = new List<GameObject>(bm.rooms ?? new GameObject[0]);
            while (rooms.Count < defs.Count) rooms.Add(null);
            void Add(BiomeDef d, GameObject room) { if (defs.Contains(d)) return; defs.Add(d); rooms.Add(room); }
            Add(hielo, roomH); Add(forja, roomF); Add(jardin, roomJ); Add(panteon, roomP);
            bm.biomes = defs.ToArray();
            bm.rooms = rooms.ToArray();
            EditorUtility.SetDirty(bm);

            var cfg = AssetDatabase.LoadAssetAtPath<EconomyConfig>("Assets/Marmoreal/Config/Economy.asset");
            if (cfg) { cfg.EnsureDefaults(); EditorUtility.SetDirty(cfg); }
            CampaignBuilder.AddCampaign(); // colores nuevos (material) + guarda escena y assets (aditivo)
            Debug.Log("[Marmoreal] Salas 4-7 añadidas (aditivo).");
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
            if (p.TryGet<Fog>(out var fog)) { fog.albedo.Override(fogAlbedo); fog.meanFreePath.Override(150f); }
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
