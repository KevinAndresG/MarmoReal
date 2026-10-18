using System.Collections.Generic;
using UnityEngine;

namespace Marmoreal
{
    /// <summary>
    /// Gestiona las Discípulas: tantas como la mejora del Taller "gemelas", más la Legión temporal de
    /// la bendición de las Musas (fantasmas con otro material). Vive en "--- SISTEMAS ---".
    /// </summary>
    public class AllySquad : MonoBehaviour
    {
        static AllySquad _i;
        public static AllySquad I { get { if (!_i) _i = FindFirstObjectByType<AllySquad>(); return _i; } private set => _i = value; }

        public AllyMarble allyPrefab;
        [Tooltip("Material de las Discípulas fantasma de la Legión")]
        public Material ghostMaterial;
        public Transform player;

        readonly List<AllyMarble> allies = new List<AllyMarble>();
        readonly List<AllyMarble> ghosts = new List<AllyMarble>();
        float legionTimer;
        int legionCount;

        public float LegionTimeLeft => Mathf.Max(0, legionTimer);

        void Awake() { I = this; }

        void Update()
        {
            var gm = GameManager.I;
            if (!gm || !allyPrefab || !player) return;
            // Tras una recarga de dominio las listas se pierden pero los objetos siguen: se limpian.
            if (allies.Count == 0 && ghosts.Count == 0 && transform.childCount > 0)
                for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            legionTimer -= Time.deltaTime;
            Sync(allies, gm.AllyCount, false);
            Sync(ghosts, legionTimer > 0 ? legionCount : 0, true);
        }

        public void SummonLegion(int count, float seconds)
        {
            legionCount = Mathf.Max(legionCount, count);
            if (legionTimer <= 0) legionCount = count;
            legionTimer = Mathf.Max(legionTimer, seconds);
        }

        void Sync(List<AllyMarble> list, int count, bool ghost)
        {
            while (list.Count < count)
            {
                var a = Instantiate(allyPrefab, transform);
                a.ghost = ghost;
                a.squad = this;
                if (ghost && ghostMaterial) foreach (var r in a.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = ghostMaterial;
                Ignore(a);
                Place(a);
                list.Add(a);
                ShardSystem.I?.SpawnSparkle(a.transform.position, 14, 0, 5f);
            }
            while (list.Count > count)
            {
                var a = list[list.Count - 1];
                list.RemoveAt(list.Count - 1);
                if (a) { ShardSystem.I?.SpawnSparkle(a.transform.position, 14, 0, 5f); Destroy(a.gameObject); }
            }
        }

        /// <summary>No chocan con la canica ni entre ellas (solo con los bloques y el suelo).</summary>
        void Ignore(AllyMarble a)
        {
            var col = a.GetComponent<Collider>();
            var pc = player.GetComponent<Collider>();
            if (pc) Physics.IgnoreCollision(col, pc);
            foreach (var o in allies) if (o) Physics.IgnoreCollision(col, o.GetComponent<Collider>());
            foreach (var o in ghosts) if (o) Physics.IgnoreCollision(col, o.GetComponent<Collider>());
        }

        /// <summary>Coloca la Discípula junto a la canica (al aparecer o si se cae del mundo).</summary>
        public void Place(AllyMarble a)
        {
            Vector2 c = Random.insideUnitCircle.normalized * 2.5f;
            Vector3 p = player.position + new Vector3(c.x, 1.5f, c.y);
            var rb = a.Body;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = p;
            a.transform.position = p;
        }
    }
}
