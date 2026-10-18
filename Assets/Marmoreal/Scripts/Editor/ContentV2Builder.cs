using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Marmoreal.EditorTools
{
    /// <summary>
    /// Paso ADITIVO e IDEMPOTENTE del contenido v2: Discípulas (prefab Ally + AllySquad), Cadena de Rayos
    /// (prefab Lightning), mármol volcánico (material) y disco de
    /// acreción del Vórtice. Solo crea lo que falte y solo rellena referencias vacías. Nunca borra nada.
    /// </summary>
    public static class ContentV2Builder
    {
        const string MatDir = "Assets/Marmoreal/Materials";

        [MenuItem("Marmoreal/Añadir Discípulas, Rayo y Volcánicos (aditivo)")]
        public static void AddContent()
        {
            var sphere = AssetDatabase.LoadAssetAtPath<Mesh>(MarmorealBuilder.MeshDir + "/Sphere.asset");
            var ring = AssetDatabase.LoadAssetAtPath<Mesh>(MarmorealBuilder.MeshDir + "/Ring.asset");
            var playerSphere = AssetDatabase.LoadAssetAtPath<Mesh>(MarmorealBuilder.MeshDir + "/PlayerSphere.asset");
            if (!sphere || !ring || !playerSphere) { Debug.LogError("[Marmoreal] Faltan meshes base: ejecuta antes la construcción inicial."); return; }

            // ---------- Materiales ----------
            var mAlly = CopyMat("M_Ally", "M_Player_Calacatta", m => m.SetColor("_BaseColor", new Color(0.78f, 0.88f, 1f)));
            var mGhost = MarmorealBuilder.Emissive("M_AllyGhost", new Color(0.7f, 0.9f, 1f), new Color(0.45f, 0.8f, 1f), 11.5f, 0.9f);
            var mBolt = MarmorealBuilder.Emissive("M_Lightning", new Color(0.85f, 0.93f, 1f), new Color(0.7f, 0.85f, 1f), 15.5f, 0.2f);
            var mDisk = MarmorealBuilder.Emissive("M_VortexDisk", new Color(1f, 0.6f, 0.9f), new Color(0.9f, 0.4f, 1f), 13.5f, 0.9f);
            var mVolcanic = CopyMat("M_Chunk_Volcanic", "M_Chunk_Nero", m =>
            {
                // Las vetas claras del Nero brillan como lava
                var tex = m.GetTexture("_BaseColorMap");
                if (tex) m.SetTexture("_EmissiveColorMap", tex);
                m.SetColor("_BaseColor", new Color(0.55f, 0.35f, 0.3f));
                HDMaterial.SetUseEmissiveIntensity(m, true);
                HDMaterial.SetEmissiveColor(m, new Color(1f, 0.32f, 0.06f));
                HDMaterial.SetEmissiveIntensity(m, 10.5f, EmissiveIntensityUnit.EV100);
            });
            var trailMat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/M_Trail.mat");
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(MatDir + "/PM_Marble.physicMaterial");

            // ---------- Prefabs (solo los que falten) ----------
            var ally = MarmorealBuilder.SavePrefab("Ally", () =>
            {
                var go = new GameObject("Ally");
                var body = MarmorealBuilder.MeshGO("Body", playerSphere, mAlly, go.transform);
                body.transform.localScale = Vector3.one * 0.75f;
                var sc = go.AddComponent<SphereCollider>();
                sc.radius = 0.56f;
                if (pm) sc.sharedMaterial = pm;
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 1.2f; rb.linearDamping = 0.12f; rb.angularDamping = 0.3f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.maxAngularVelocity = 80f;
                var trGo = new GameObject("Trail");
                trGo.transform.SetParent(go.transform, false);
                var tr = trGo.AddComponent<TrailRenderer>();
                tr.sharedMaterial = trailMat;
                tr.time = 0.18f; tr.startWidth = 0.4f; tr.endWidth = 0f; tr.minVertexDistance = 0.2f;
                tr.shadowCastingMode = ShadowCastingMode.Off;
                tr.startColor = new Color(0.8f, 0.9f, 1f, 0.2f); tr.endColor = new Color(0.8f, 0.9f, 1f, 0f);
                tr.emitting = false;
                go.AddComponent<AllyMarble>().trail = tr;
                return go;
            });

            var bolt = MarmorealBuilder.SavePrefab("Lightning", () =>
            {
                var go = new GameObject("Lightning");
                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = mBolt;
                lr.useWorldSpace = true;
                lr.widthMultiplier = 0.32f;
                lr.widthCurve = new AnimationCurve(new Keyframe(0, 1.6f), new Keyframe(0.4f, 1f), new Keyframe(1, 0.5f));
                lr.numCapVertices = 2;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                lr.receiveShadows = false;
                go.SetActive(false);
                return go;
            });

            // Disco de acreción del Vórtice (solo si falta)
            const string vortexPath = "Assets/Marmoreal/Prefabs/Vortex.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(vortexPath))
            {
                var root = PrefabUtility.LoadPrefabContents(vortexPath);
                if (!root.transform.Find("Disco"))
                {
                    var pivot = new GameObject("Disco");
                    pivot.transform.SetParent(root.transform, false);
                    pivot.transform.localRotation = Quaternion.Euler(12f, 0, 0);
                    for (int i = 0; i < 5; i++)
                    {
                        float s = 3.6f + i * 0.9f;
                        var r = MarmorealBuilder.MeshGO($"Banda_{i}", ring, mDisk, pivot.transform, false);
                        r.transform.localScale = new Vector3(s, 0.25f, s);
                        r.AddComponent<Spinner>().degreesPerSecond = new Vector3(0, 420f - i * 60f, 0);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, vortexPath);
                }
                PrefabUtility.UnloadPrefabContents(root);
            }

            // ---------- Economía: añade lo nuevo sin tocar lo existente ----------
            var config = AssetDatabase.LoadAssetAtPath<EconomyConfig>("Assets/Marmoreal/Config/Economy.asset");
            if (config && config.EnsureDefaults()) { EditorUtility.SetDirty(config); Debug.Log("[Marmoreal] Economy.asset: contenido nuevo añadido."); }

            // ---------- Escena existente ----------
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != MarmorealBuilder.ScenePath)
                scene = EditorSceneManager.OpenScene(MarmorealBuilder.ScenePath, OpenSceneMode.Single);
            Transform systems = null;
            foreach (var r in scene.GetRootGameObjects())
            {
                if (r.name == "--- SISTEMAS ---") systems = r.transform;
            }
            if (!systems) systems = new GameObject("--- SISTEMAS ---").transform;
            var player = Object.FindFirstObjectByType<PlayerMarble>();

            var squad = Object.FindFirstObjectByType<AllySquad>();
            if (!squad)
            {
                var go = new GameObject("Discípulas");
                go.transform.SetParent(systems);
                squad = go.AddComponent<AllySquad>();
                Undo.RegisterCreatedObjectUndo(go, "AllySquad");
            }
            if (!squad.allyPrefab && ally) squad.allyPrefab = ally.GetComponent<AllyMarble>();
            if (!squad.ghostMaterial) squad.ghostMaterial = mGhost;
            if (!squad.player && player) squad.player = player.transform;
            EditorUtility.SetDirty(squad);

            var ps = Object.FindFirstObjectByType<PowerSystem>();
            if (ps && !ps.lightningPrefab) { ps.lightningPrefab = bolt; EditorUtility.SetDirty(ps); }

            var spawner = Object.FindFirstObjectByType<MonolithSpawner>();
            if (spawner && !spawner.volatileMaterial) { spawner.volatileMaterial = mVolcanic; EditorUtility.SetDirty(spawner); }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Marmoreal] Contenido v2 añadido (aditivo).");
        }

        /// <summary>Copia un material existente (si el destino no existe) y le aplica un ajuste.</summary>
        static Material CopyMat(string name, string from, System.Action<Material> tweak)
        {
            string path = $"{MatDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;
            if (!AssetDatabase.CopyAsset($"{MatDir}/{from}.mat", path)) return null;
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            tweak(m);
            HDMaterial.ValidateMaterial(m);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
