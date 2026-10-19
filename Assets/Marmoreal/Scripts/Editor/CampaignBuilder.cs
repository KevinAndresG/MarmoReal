using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Marmoreal.EditorTools
{
    /// <summary>
    /// Paso ADITIVO e IDEMPOTENTE de la campaña (S4): migra Economy.asset (piezas, colores), crea los materiales de
    /// canica que falten, rellena solo los huecos (material de color vacío, trialGallery = 0) y crea --- MONUMENTO ---
    /// en la escena si no existe. Nunca borra ni pisa lo editado a mano.
    /// </summary>
    public static class CampaignBuilder
    {
        const string MatDir = MarmorealBuilder.MatDir;

        [MenuItem("Marmoreal/Añadir campaña: Monumento y Colección (aditivo)")]
        public static void AddCampaign()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != MarmorealBuilder.ScenePath) { Debug.LogError("[Marmoreal] Abre la escena Marmoreal antes."); return; }

            // ---------- Economy.asset: piezas + colores ----------
            var cfg = AssetDatabase.LoadAssetAtPath<EconomyConfig>("Assets/Marmoreal/Config/Economy.asset");
            if (cfg)
            {
                cfg.EnsureDefaults();
                foreach (var c in cfg.marbleColors)
                    if (!c.material) c.material = ColorMaterial(c);
                EditorUtility.SetDirty(cfg);
            }

            // ---------- Pruebas por sala (solo si están a 0) ----------
            SetTrial("Biome_Carrara", 45);
            SetTrial("Biome_Volcanica", 65);
            SetTrial("Biome_Cristal", 90);

            // ---------- Monumento físico ----------
            if (!Object.FindFirstObjectByType<Monument>(FindObjectsInactive.Include)) BuildMonument();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Marmoreal] Campaña añadida (aditivo).");
        }

        static void SetTrial(string asset, int gallery)
        {
            var b = AssetDatabase.LoadAssetAtPath<BiomeDef>($"Assets/Marmoreal/Config/Biomes/{asset}.asset");
            if (!b || b.trialGallery != 0) return;
            b.trialGallery = gallery;
            EditorUtility.SetDirty(b);
        }

        /// <summary>Material existente M_Player_{Nombre} o copia teñida de M_Player_Carrara.</summary>
        static Material ColorMaterial(MarbleColorDef c)
        {
            string name = char.ToUpperInvariant(c.id[0]) + c.id.Substring(1);
            string path = $"{MatDir}/M_Player_{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m) return m;
            if (!AssetDatabase.CopyAsset($"{MatDir}/M_Player_Carrara.mat", path)) return null;
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Color tint = Color.Lerp(c.trailColor, Color.white, 0.25f);
            if (c.id == "dorado") { tint = new Color(1f, 0.78f, 0.38f); m.SetFloat("_Metallic", 0.85f); m.SetFloat("_Smoothness", 0.85f); }
            if (c.id == "cosmica") { tint = new Color(0.12f, 0.1f, 0.3f); m.SetColor("_EmissiveColor", new Color(0.45f, 0.35f, 1f) * 60f); }
            m.SetColor("_BaseColor", tint);
            HDMaterial.ValidateMaterial(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void BuildMonument()
        {
            var marble = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_Statue.mat");
            var gold = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_StatueGold.mat");
            var soul = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_Musa.mat") ?? gold;

            // Fuera del cuenco, en la terraza del fondo, mirando al centro
            var root = new GameObject("--- MONUMENTO ---");
            root.transform.SetPositionAndRotation(new Vector3(0, ArenaShape.RimHeight, 62f), Quaternion.Euler(0, 180, 0));
            root.transform.localScale = Vector3.one * 1.8f; // asoma entre las columnas del santuario
            var mon = root.AddComponent<Monument>();

            var p0 = Piece(root, "Base");
            Prim(p0, PrimitiveType.Cube, new Vector3(0, 0.75f, 0), new Vector3(8, 1.5f, 8), Vector3.zero, marble);
            Prim(p0, PrimitiveType.Cube, new Vector3(0, 1.9f, 0), new Vector3(6, 0.8f, 6), Vector3.zero, marble);

            var p1 = Piece(root, "Columnas");
            foreach (var x in new[] { -2.4f, 2.4f })
                foreach (var z in new[] { -2.4f, 2.4f })
                    Prim(p1, PrimitiveType.Cylinder, new Vector3(x, 5.8f, z), new Vector3(0.8f, 3.5f, 0.8f), Vector3.zero, marble);
            Prim(p1, PrimitiveType.Cube, new Vector3(0, 9.55f, 0), new Vector3(6, 0.5f, 6), Vector3.zero, marble);

            var p2 = Piece(root, "Torso");
            Prim(p2, PrimitiveType.Capsule, new Vector3(0, 4.7f, 0), new Vector3(2.2f, 2.35f, 1.5f), Vector3.zero, marble);

            var p3 = Piece(root, "Brazos");
            Prim(p3, PrimitiveType.Capsule, new Vector3(-1.6f, 5.2f, 0), new Vector3(0.65f, 1.6f, 0.65f), new Vector3(0, 0, -22), marble);
            Prim(p3, PrimitiveType.Capsule, new Vector3(1.6f, 5.2f, 0), new Vector3(0.65f, 1.6f, 0.65f), new Vector3(0, 0, 22), marble);

            var p4 = Piece(root, "Rostro");
            Prim(p4, PrimitiveType.Sphere, new Vector3(0, 7.7f, 0), new Vector3(1.4f, 1.6f, 1.4f), Vector3.zero, marble);

            var p5 = Piece(root, "Corona");
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                Prim(p5, PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 0.6f, 8.75f, Mathf.Sin(a) * 0.6f), new Vector3(0.22f, 0.5f, 0.22f), new Vector3(0, -i * 60f, 0), gold);
            }
            Prim(p5, PrimitiveType.Cylinder, new Vector3(0, 8.5f, 0), new Vector3(1.4f, 0.08f, 1.4f), Vector3.zero, gold);

            var p6 = Piece(root, "Alma");
            Prim(p6, PrimitiveType.Sphere, new Vector3(0, 11f, 0), Vector3.one * 0.9f, Vector3.zero, soul);
            var lightGo = new GameObject("Luz del Alma");
            lightGo.transform.SetParent(p6.transform, false);
            lightGo.transform.localPosition = new Vector3(0, 11f, 0);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.85f, 0.6f); l.range = 30f;
            if (!lightGo.GetComponent<HDAdditionalLightData>()) lightGo.AddComponent<HDAdditionalLightData>();
            l.intensity = 20000f; // lúmenes (exposición fija EV 9.5)

            mon.pieces = new[] { p0, p1, p2, p3, p4, p5, p6 };
            Undo.RegisterCreatedObjectUndo(root, "Monumento");
        }

        static GameObject Piece(GameObject root, string name)
        {
            var g = new GameObject(name);
            g.transform.SetParent(root.transform, false);
            return g;
        }

        static void Prim(GameObject parent, PrimitiveType t, Vector3 pos, Vector3 scale, Vector3 euler, Material m)
        {
            var g = GameObject.CreatePrimitive(t);
            g.name = t.ToString();
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent.transform, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.transform.localEulerAngles = euler;
            if (m) g.GetComponent<Renderer>().sharedMaterial = m;
        }
    }
}
