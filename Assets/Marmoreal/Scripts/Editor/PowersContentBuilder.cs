using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Marmoreal.EditorTools
{
    /// <summary>
    /// Paso ADITIVO e IDEMPOTENTE para los poderes, el Ícor y las Musas: crea los materiales y prefabs que
    /// falten (Bomb, Meteor, Vortex, Musa), añade a la escena existente los objetos PowerSystem y MusaSpawner
    /// bajo "--- SISTEMAS ---" (solo si no existen; solo rellena referencias vacías) y añade a Economy.asset
    /// los nodos/poderes/mecenazgos nuevos sin tocar los existentes. Nunca borra nada.
    /// </summary>
    public static class PowersContentBuilder
    {
        const string FabDir = "Assets/Marmoreal/Prefabs";

        [MenuItem("Marmoreal/Añadir poderes, Altar y Musas (aditivo)")]
        public static void AddPowersContent()
        {
            var sphere = AssetDatabase.LoadAssetAtPath<Mesh>(MarmorealBuilder.MeshDir + "/Sphere.asset");
            var ring = AssetDatabase.LoadAssetAtPath<Mesh>(MarmorealBuilder.MeshDir + "/Ring.asset");
            if (!sphere || !ring) { Debug.LogError("[Marmoreal] Faltan las meshes Sphere/Ring: ejecuta antes la construcción inicial."); return; }

            // ---------- Materiales ----------
            var mBomb = MarmorealBuilder.Emissive("M_PowerBomb", new Color(0.08f, 0.06f, 0.05f), new Color(1f, 0.35f, 0.08f), 9.5f, 0.85f);
            var mSpark = MarmorealBuilder.Emissive("M_PowerSpark", new Color(1f, 0.8f, 0.5f), new Color(1f, 0.7f, 0.3f), 14f, 0.2f);
            var mMeteor = MarmorealBuilder.Emissive("M_PowerMeteor", new Color(1f, 0.75f, 0.45f), new Color(1f, 0.5f, 0.15f), 13f, 0.4f);
            var mFireTrail = MarmorealBuilder.Trail("M_FireTrail", new Color(1f, 0.45f, 0.12f, 1f));
            var mVortexCore = MarmorealBuilder.Emissive("M_VortexCore", new Color(0.01f, 0.005f, 0.02f), new Color(0.35f, 0.1f, 0.8f), 8.5f, 0.95f);
            var mVortexRing = MarmorealBuilder.Emissive("M_VortexRing", new Color(0.6f, 0.35f, 1f), new Color(0.65f, 0.3f, 1f), 13f, 0.9f);
            var mMusa = MarmorealBuilder.Emissive("M_Musa", new Color(1f, 0.95f, 0.8f), new Color(1f, 0.88f, 0.6f), 14f, 0.9f);
            var mMusaRing = MarmorealBuilder.Emissive("M_MusaRing", new Color(1f, 0.85f, 0.5f), new Color(1f, 0.78f, 0.4f), 13f, 0.9f);
            var mBeam = MarmorealBuilder.Trail("M_MusaBeam", new Color(1f, 0.85f, 0.55f, 0.18f));

            // ---------- Prefabs (solo los que falten) ----------
            var bomb = MarmorealBuilder.SavePrefab("Bomb", () =>
            {
                var go = new GameObject("Bomb");
                var body = MarmorealBuilder.MeshGO("Cuerpo", sphere, mBomb, go.transform);
                body.transform.localScale = Vector3.one * 1.3f;
                var r = MarmorealBuilder.MeshGO("Anillo", ring, mSpark, go.transform, false);
                r.transform.localScale = Vector3.one * 0.95f;
                r.transform.localRotation = Quaternion.Euler(90, 0, 0);
                r.AddComponent<Spinner>().degreesPerSecond = new Vector3(0, 0, 220);
                var spark = MarmorealBuilder.MeshGO("Mecha", sphere, mSpark, go.transform, false);
                spark.transform.localPosition = new Vector3(0, 0.78f, 0);
                spark.transform.localScale = Vector3.one * 0.25f;
                var sp = spark.AddComponent<Spinner>();
                sp.degreesPerSecond = Vector3.zero; sp.pulseAmount = 0.45f; sp.pulseSpeed = 22f;
                Point(go.transform, "Luz", new Vector3(0, 1f, 0), new Color(1f, 0.5f, 0.2f), 9000f, 10f);
                return go;
            });

            var meteor = MarmorealBuilder.SavePrefab("Meteor", () =>
            {
                var go = new GameObject("Meteor");
                var body = MarmorealBuilder.MeshGO("Roca", sphere, mMeteor, go.transform, false);
                body.transform.localScale = Vector3.one * 1.1f;
                body.AddComponent<Spinner>().degreesPerSecond = new Vector3(200, 140, 0);
                var tr = go.AddComponent<TrailRenderer>();
                tr.sharedMaterial = mFireTrail;
                tr.time = 0.32f; tr.startWidth = 1.1f; tr.endWidth = 0f; tr.minVertexDistance = 0.3f;
                tr.shadowCastingMode = ShadowCastingMode.Off;
                tr.startColor = new Color(1f, 0.6f, 0.25f, 1f); tr.endColor = new Color(1f, 0.25f, 0.05f, 0f);
                go.SetActive(false);
                return go;
            });

            var vortex = MarmorealBuilder.SavePrefab("Vortex", () =>
            {
                var go = new GameObject("Vortex");
                var core = MarmorealBuilder.MeshGO("Nucleo", sphere, mVortexCore, go.transform, false);
                core.transform.localScale = Vector3.one * 3f;
                var cs = core.AddComponent<Spinner>(); cs.degreesPerSecond = new Vector3(0, 60, 0); cs.pulseAmount = 0.05f; cs.pulseSpeed = 9f;
                float[] scales = { 3.4f, 5.8f, 8.6f };
                float[] tilts = { 8f, -14f, 4f };
                float[] speeds = { 320f, -210f, 140f };
                for (int i = 0; i < scales.Length; i++)
                {
                    var pivot = new GameObject($"Toro_{i}");
                    pivot.transform.SetParent(go.transform, false);
                    pivot.transform.localRotation = Quaternion.Euler(tilts[i], i * 40f, 0);
                    var t = MarmorealBuilder.MeshGO("Anillo", ring, mVortexRing, pivot.transform, false);
                    t.transform.localScale = new Vector3(scales[i], scales[i] * 0.5f, scales[i]);
                    t.AddComponent<Spinner>().degreesPerSecond = new Vector3(0, speeds[i], 0);
                }
                Point(go.transform, "Luz", Vector3.up * 1.5f, new Color(0.6f, 0.3f, 1f), 40000f, 22f);
                go.SetActive(false);
                return go;
            });

            var musa = MarmorealBuilder.SavePrefab("Musa", () =>
            {
                var go = new GameObject("Musa");
                var orb = MarmorealBuilder.MeshGO("Orbe", sphere, mMusa, go.transform, false);
                orb.transform.localScale = Vector3.one * 0.9f;
                var os = orb.AddComponent<Spinner>(); os.degreesPerSecond = Vector3.zero; os.bobAmplitude = 0.25f; os.bobSpeed = 2.2f; os.pulseAmount = 0.08f; os.pulseSpeed = 5f;
                var r1 = MarmorealBuilder.MeshGO("Toro_A", ring, mMusaRing, go.transform, false);
                r1.transform.localScale = Vector3.one * 1.2f;
                r1.AddComponent<Spinner>().degreesPerSecond = new Vector3(90, 40, 0);
                var r2 = MarmorealBuilder.MeshGO("Toro_B", ring, mMusaRing, go.transform, false);
                r2.transform.localScale = Vector3.one * 1.55f;
                r2.AddComponent<Spinner>().degreesPerSecond = new Vector3(-30, -70, 60);
                var beam = MarmorealBuilder.MeshGO("Haz", sphere, mBeam, go.transform, false);
                beam.transform.localPosition = new Vector3(0, 18f, 0);
                beam.transform.localScale = new Vector3(0.45f, 40f, 0.45f);
                Point(go.transform, "Luz", Vector3.up * 0.5f, new Color(1f, 0.85f, 0.55f), 30000f, 14f);
                return go;
            });

            // ---------- Economía: añade lo nuevo sin tocar lo existente ----------
            var config = AssetDatabase.LoadAssetAtPath<EconomyConfig>("Assets/Marmoreal/Config/Economy.asset");
            if (config && config.EnsureDefaults()) { EditorUtility.SetDirty(config); Debug.Log("[Marmoreal] Economy.asset: contenido nuevo añadido."); }

            // ---------- Escena existente ----------
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != MarmorealBuilder.ScenePath)
                scene = EditorSceneManager.OpenScene(MarmorealBuilder.ScenePath, OpenSceneMode.Single);

            Transform systems = null;
            foreach (var r in scene.GetRootGameObjects()) if (r.name == "--- SISTEMAS ---") systems = r.transform;
            if (!systems) systems = new GameObject("--- SISTEMAS ---").transform;
            var player = Object.FindFirstObjectByType<PlayerMarble>();

            var ps = Object.FindFirstObjectByType<PowerSystem>();
            if (!ps)
            {
                var go = new GameObject("PowerSystem");
                go.transform.SetParent(systems);
                ps = go.AddComponent<PowerSystem>();
                Undo.RegisterCreatedObjectUndo(go, "PowerSystem");
            }
            if (!ps.player && player) ps.player = player.transform;
            if (!ps.bombPrefab) ps.bombPrefab = bomb;
            if (!ps.meteorPrefab) ps.meteorPrefab = meteor;
            if (!ps.vortexPrefab) ps.vortexPrefab = vortex;
            if (!ps.flash)
            {
                var existing = ps.transform.Find("Destello");
                ps.flash = existing ? existing.GetComponent<Light>() : Point(ps.transform, "Destello", Vector3.up * 3f, new Color(1f, 0.75f, 0.5f), 0f, 45f);
            }
            EditorUtility.SetDirty(ps);

            var ms = Object.FindFirstObjectByType<MusaSpawner>();
            if (!ms)
            {
                var go = new GameObject("MusaSpawner");
                go.transform.SetParent(systems);
                ms = go.AddComponent<MusaSpawner>();
                Undo.RegisterCreatedObjectUndo(go, "MusaSpawner");
            }
            if (!ms.player && player) ms.player = player.transform;
            if (!ms.musaPrefab) ms.musaPrefab = musa;
            EditorUtility.SetDirty(ms);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Marmoreal] Poderes, Altar y Musas añadidos (aditivo).");
        }

        static Light Point(Transform parent, string name, Vector3 localPos, Color c, float lumens, float range)
        {
            var lgo = new GameObject(name);
            lgo.transform.SetParent(parent, false);
            lgo.transform.localPosition = localPos;
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Point;
            lgo.AddComponent<HDAdditionalLightData>();
            l.lightUnit = LightUnit.Lumen;
            l.intensity = lumens;
            l.range = range;
            l.color = c;
            l.shadows = LightShadows.None;
            return l;
        }
    }
}
