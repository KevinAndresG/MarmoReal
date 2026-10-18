using System.Collections.Generic;
using UnityEngine;

namespace Marmoreal
{
    /// <summary>
    /// Hace brotar bloques de mármol en los pedestales (hijos de spawnPointsRoot, editables en escena).
    /// El color del mármol cambia con cada galería; algunos bloques salen dorados.
    /// </summary>
    public class MonolithSpawner : MonoBehaviour
    {
        static MonolithSpawner _i;
        /// <summary>Singleton resistente a recargas de dominio (se re-busca si se pierde).</summary>
        public static MonolithSpawner I { get { if (!_i) _i = FindFirstObjectByType<MonolithSpawner>(); return _i; } private set => _i = value; }

        public Monolith smallPrefab;
        public Monolith largePrefab;
        public GameObject[] statuePrefabs;
        public GameObject[] goldenStatuePrefabs;

        [Tooltip("Material de los trozos por tier (cicla con la galería)")]
        public Material[] tierMaterials;
        public Material goldenMaterial;
        [Tooltip("Material del mármol volcánico (bloques que estallan al romperse). Vacío = sin volcánicos")]
        public Material volatileMaterial;

        public Transform spawnPointsRoot;
        public float respawnDelay = 0.9f;
        public float minDistanceToPlayer = 5f;
        [Tooltip("Probabilidad de bloque grande = base + paso*galería (máx)")]
        public float largeChanceBase = 0f, largeChanceStep = 0.04f, largeChanceMax = 0.45f;
        public Transform player;

        readonly List<Monolith> active = new List<Monolith>();
        readonly Stack<Monolith> poolSmall = new Stack<Monolith>();
        readonly Stack<Monolith> poolLarge = new Stack<Monolith>();
        readonly Dictionary<Monolith, int> occupied = new Dictionary<Monolith, int>();
        readonly List<Transform> points = new List<Transform>();
        readonly HashSet<int> busy = new HashSet<int>();
        float cooldown;
        [System.NonSerialized] bool volatileHintShown;

        public IReadOnlyList<Monolith> Active => active;
        public string DebugState => $"pts={points.Count} active={active.Count} busy={busy.Count} cd={cooldown:0.0}";

        void Awake()
        {
            I = this;
            CollectPoints();
        }

        void CollectPoints()
        {
            points.Clear();
            if (spawnPointsRoot) foreach (Transform t in spawnPointsRoot) points.Add(t);
        }

        [System.NonSerialized] GameManager subscribed;

        void Start() { cooldown = 0.5f; }

        void OnDestroy()
        {
            if (subscribed) subscribed.Prestiged -= ResetAll;
        }

        // Índices de material: [0..tiers-1] = tiers, [tiers] = dorado
        public int TierFor(int gallery) => tierMaterials.Length == 0 ? 0 : gallery % tierMaterials.Length;
        public int ShardIndexFor(int tier, bool golden) => golden ? tierMaterials.Length : tier;
        public int DebrisIndexFor(int tier, bool golden) => golden ? tierMaterials.Length : tier;

        void Update()
        {
            var gm = GameManager.I;
            if (!gm) return;
            if (subscribed != gm) { gm.Prestiged += ResetAll; subscribed = gm; }
            if (points.Count == 0) CollectPoints(); // tras recarga de dominio
            cooldown -= Time.deltaTime;
            int target = Mathf.Min(gm.MaxMonoliths, points.Count);
            if (active.Count < target && cooldown <= 0)
            {
                if (TrySpawn()) cooldown = respawnDelay / Mathf.Max(1f, (target - active.Count) * 0.75f);
                else cooldown = 0.25f;
            }
        }

        bool TrySpawn()
        {
            var gm = GameManager.I;
            int idx = PickPoint();
            if (idx < 0) return false;

            bool golden = Random.value < gm.GoldenChance;
            float largeChance = Mathf.Min(largeChanceMax, largeChanceBase + largeChanceStep * gm.S.galleryLevel);
            bool large = largePrefab && Random.value < largeChance;

            var m = Get(large);
            var p = points[idx];
            m.transform.SetPositionAndRotation(p.position, Quaternion.Euler(0, Random.Range(0, 4) * 90f + Random.Range(-8f, 8f), 0));

            int tier = TierFor(gm.S.galleryLevel);
            double hp = gm.MonolithHP(large, golden);
            double reward = gm.BlockReward(large, golden);
            double statue = gm.StatueReward(reward, golden);
            var pool = golden && goldenStatuePrefabs != null && goldenStatuePrefabs.Length > 0 ? goldenStatuePrefabs : statuePrefabs;
            var fab = pool != null && pool.Length > 0 ? pool[Random.Range(0, pool.Length)] : null;
            m.Init(this, hp, reward, statue, golden ? goldenMaterial : tierMaterials[tier], tier, fab, golden);
            if (!golden && volatileMaterial && gm.S.galleryLevel >= gm.config.volatileUnlockGallery && Random.value < gm.config.volatileChance)
            {
                m.SetVolatile(volatileMaterial);
                if (!volatileHintShown) { volatileHintShown = true; Hud.I?.ShowToast("Bloque VOLCÁNICO: al romperlo estalla y daña a sus vecinos", ToastKind.Info); }
            }

            active.Add(m);
            occupied[m] = idx;
            busy.Add(idx);
            if (golden)
            {
                AudioFX.I?.Play(Sfx.Golden, 0.6f);
                Hud.I?.ShowToast("¡Un bloque DORADO ha brotado!", ToastKind.Gold);
            }
            return true;
        }

        int PickPoint()
        {
            Vector3 pp = player ? player.position : Vector3.one * 999f;
            float minSq = minDistanceToPlayer * minDistanceToPlayer;
            int start = Random.Range(0, points.Count);
            for (int k = 0; k < points.Count; k++)
            {
                int i = (start + k) % points.Count;
                if (busy.Contains(i)) continue;
                Vector3 d = points[i].position - pp; d.y = 0;
                if (d.sqrMagnitude < minSq) continue;
                return i;
            }
            return -1;
        }

        Monolith Get(bool large)
        {
            var pool = large ? poolLarge : poolSmall;
            if (pool.Count > 0) return pool.Pop();
            var m = Instantiate(large ? largePrefab : smallPrefab, transform);
            m.gameObject.SetActive(false);
            return m;
        }

        public void Release(Monolith m)
        {
            active.Remove(m);
            if (occupied.TryGetValue(m, out int idx)) { busy.Remove(idx); occupied.Remove(m); }
            (m.large ? poolLarge : poolSmall).Push(m);
        }

        void ResetAll()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var m = active[i];
                m.Despawn();
                Release(m);
            }
            ShardSystem.I?.ClearAll(false);
            cooldown = 1f;
        }

        // ================================================================= Consultas
        public Monolith FindMonolithAt(Vector3 p, float radius, int excludeId)
        {
            for (int i = 0; i < active.Count; i++)
            {
                var m = active[i];
                if (!m.IsTargetable || m.GetInstanceID() == excludeId) continue;
                var b = m.WorldBounds; b.Expand(radius * 2f);
                if (b.Contains(p)) return m;
            }
            return null;
        }

        public Monolith Nearest(Vector3 p, float maxDist)
        {
            Monolith best = null; float bd = maxDist * maxDist;
            for (int i = 0; i < active.Count; i++)
            {
                var m = active[i];
                if (!m.IsTargetable) continue;
                float d = (m.transform.position - p).sqrMagnitude;
                if (d < bd) { bd = d; best = m; }
            }
            return best;
        }

        public void InRadius(Vector3 p, float r, List<Monolith> results)
        {
            results.Clear();
            for (int i = 0; i < active.Count; i++)
            {
                var m = active[i];
                if (!m.IsTargetable) continue;
                Vector3 c = m.ClosestPoint(p);
                if ((c - p).sqrMagnitude <= r * r) results.Add(m);
            }
        }
    }
}
