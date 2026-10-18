using System.Collections.Generic;
using UnityEngine;

namespace Marmoreal
{
    /// <summary>
    /// Esferas luminosas que orbitan la canica y se lanzan a tallar el bloque más cercano.
    /// Son el motor "idle" del juego (y del progreso offline). Prefab: Assets/Marmoreal/Prefabs/Acolyte.
    /// </summary>
    public class AcolyteSwarm : MonoBehaviour
    {
        public Transform owner;
        public GameObject acolytePrefab;
        public float orbitRadius = 2.3f;
        public float orbitHeight = 1.4f;
        public float orbitSpeed = 70f;
        public float seekRange = 16f;
        public float flyOutTime = 0.22f;
        public float flyBackTime = 0.35f;

        class Unit
        {
            public Transform t;
            public float timer;
            public int state; // 0 orbita, 1 ida, 2 vuelta
            public float stateTime;
            public Vector3 from, to;
            public Monolith target;
        }

        readonly List<Unit> units = new List<Unit>();

        void Update()
        {
            var gm = GameManager.I;
            if (!gm || !owner) return;
            Sync(gm.AcolyteCount);

            float dt = Time.deltaTime;
            float interval = gm.acolyteInterval;
            Vector3 center = owner.position;
            float baseAngle = Time.time * orbitSpeed;

            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                float ang = (baseAngle + i * 360f / units.Count) * Mathf.Deg2Rad;
                Vector3 orbit = center + new Vector3(Mathf.Cos(ang) * orbitRadius, orbitHeight + Mathf.Sin(Time.time * 2.3f + i) * 0.25f, Mathf.Sin(ang) * orbitRadius);
                u.stateTime += dt;

                switch (u.state)
                {
                    case 0:
                        u.t.position = Vector3.Lerp(u.t.position, orbit, 1f - Mathf.Exp(-dt * 10f));
                        u.timer -= dt;
                        if (u.timer <= 0)
                        {
                            u.timer = interval * Random.Range(0.85f, 1.15f);
                            var m = MonolithSpawner.I ? MonolithSpawner.I.Nearest(center, seekRange) : null;
                            if (m)
                            {
                                u.target = m; u.state = 1; u.stateTime = 0;
                                u.from = u.t.position;
                                u.to = m.ClosestPoint(u.t.position + Vector3.up * 0.5f);
                            }
                        }
                        break;
                    case 1:
                    {
                        float k = Mathf.Clamp01(u.stateTime / flyOutTime);
                        Vector3 arc = Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.2f;
                        u.t.position = Vector3.Lerp(u.from, u.to, k * k) + arc;
                        if (k >= 1f)
                        {
                            if (u.target && u.target.IsTargetable)
                            {
                                float dmg = gm.AcolyteHitDamage(out bool crit);
                                u.target.TakeDamage(dmg, u.to, HitSource.Acolyte, crit);
                                gm.AddResonance(gm.config.resonancePerAcolyteHit);
                            }
                            u.state = 2; u.stateTime = 0; u.from = u.t.position;
                        }
                        break;
                    }
                    case 2:
                    {
                        float k = Mathf.Clamp01(u.stateTime / flyBackTime);
                        u.t.position = Vector3.Lerp(u.from, orbit, 1f - (1f - k) * (1f - k));
                        if (k >= 1f) u.state = 0;
                        break;
                    }
                }
            }
        }

        void Sync(int count)
        {
            // Tras una recarga de dominio la lista se pierde pero los objetos siguen: se limpian.
            if (units.Count == 0 && transform.childCount > 0)
                for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            while (units.Count < count)
            {
                var go = Instantiate(acolytePrefab, owner.position + Vector3.up * 2f, Quaternion.identity, transform);
                units.Add(new Unit { t = go.transform, timer = Random.Range(0f, 1f) });
            }
            while (units.Count > count)
            {
                var u = units[units.Count - 1];
                Destroy(u.t.gameObject);
                units.RemoveAt(units.Count - 1);
            }
        }
    }
}
