using UnityEngine;

namespace Marmoreal
{
    /// <summary>
    /// Discípula: canica aliada que rueda sola, toma carrerilla y embiste bloques con una parte de TU daño
    /// de impacto (mejora Vínculo). No usa poderes ni habilidades. Prefab: Assets/Marmoreal/Prefabs/Ally.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class AllyMarble : MonoBehaviour
    {
        public float acceleration = 22f;
        [Tooltip("Velocidad máxima relativa a la de la canica")]
        public float speedFactor = 0.9f;
        public float radius = 0.56f;
        public float boundaryRadius = 41f;
        [Tooltip("Distancia a la que se aleja para tomar carrerilla")]
        public float runUp = 6f;
        public TrailRenderer trail;

        [System.NonSerialized] public bool ghost;
        [System.NonSerialized] public AllySquad squad;

        Rigidbody rb;
        Monolith target;
        bool retreat;
        float retarget, impactCooldown;
        Vector3 lastVel;

        public Rigidbody Body => rb ? rb : rb = GetComponent<Rigidbody>();

        void Awake() { rb = GetComponent<Rigidbody>(); }

        void FixedUpdate()
        {
            var gm = GameManager.I;
            if (!gm || !rb) return;
            float dt = Time.fixedDeltaTime;
            retarget -= dt;
            impactCooldown -= dt;
            if (!target || !target.IsTargetable || retarget <= 0f) PickTarget();

            Vector3 pos = rb.position, dir = Vector3.zero;
            if (target)
            {
                Vector3 to = target.ClosestPoint(pos) - pos; to.y = 0;
                float d = to.magnitude;
                if (retreat) { dir = -to.normalized; if (d > runUp) retreat = false; }
                else { dir = to.normalized; if (d < radius + 0.5f) retreat = true; }
            }
            else if (squad && squad.player)
            {
                Vector3 to = squad.player.position - pos; to.y = 0;
                if (to.magnitude > 4f) dir = to.normalized;
            }

            float maxSpeed = gm.MaxSpeed * speedFactor;
            float accel = acceleration * (0.6f + maxSpeed / 25f);
            rb.AddForce(dir * accel, ForceMode.Acceleration);
            if (dir.sqrMagnitude > 0.01f) rb.AddTorque(Vector3.Cross(Vector3.up, dir) * accel * 0.5f, ForceMode.Acceleration);

            Vector3 v = rb.linearVelocity;
            Vector3 hv = new Vector3(v.x, 0, v.z);
            if (hv.magnitude > maxSpeed * 1.15f)
            {
                hv = Vector3.Lerp(hv, hv.normalized * maxSpeed * 1.15f, 0.15f);
                rb.linearVelocity = new Vector3(hv.x, v.y, hv.z);
            }

            // Pared invisible del borde (como la canica)
            Vector3 flat = new Vector3(pos.x, 0, pos.z);
            float r = flat.magnitude;
            if (r > boundaryRadius)
            {
                Vector3 outDir = flat / r;
                float outward = Vector3.Dot(rb.linearVelocity, outDir);
                if (outward > 0) rb.linearVelocity -= outDir * outward * 1.6f;
            }
            if (pos.y < -12f && squad) squad.Place(this);
            if (trail) trail.emitting = hv.magnitude > maxSpeed * 0.7f;
            lastVel = rb.linearVelocity;
        }

        /// <summary>Elige al azar uno de los 3 bloques más cercanos (así no van todas al mismo).</summary>
        void PickTarget()
        {
            retarget = Random.Range(3f, 5f);
            target = null;
            var sp = MonolithSpawner.I;
            if (!sp) return;
            Monolith a = null, b = null, c = null;
            float da = float.MaxValue, db = float.MaxValue, dc = float.MaxValue;
            Vector3 p = transform.position;
            foreach (var m in sp.Active)
            {
                if (!m || !m.IsTargetable) continue;
                float d = (m.transform.position - p).sqrMagnitude;
                if (d < da) { c = b; dc = db; b = a; db = da; a = m; da = d; }
                else if (d < db) { c = b; dc = db; b = m; db = d; }
                else if (d < dc) { c = m; dc = d; }
            }
            int pick = Random.Range(0, 3);
            target = pick == 2 && c ? c : pick >= 1 && b ? b : a;
        }

        void OnCollisionEnter(Collision c)
        {
            if (!c.collider.TryGetComponent(out Monolith m) || !m.IsTargetable) return;
            var gm = GameManager.I;
            if (!gm || impactCooldown > 0) return;
            var cp = c.GetContact(0);
            float approach = Mathf.Max(Mathf.Abs(Vector3.Dot(lastVel, cp.normal)), c.relativeVelocity.magnitude * 0.6f);
            float frac = ghost ? Mathf.Max(0.5f, gm.AllyFraction) : gm.AllyFraction;
            float dmg = gm.AllyHitDamage(approach, frac, out bool crit);
            if (dmg <= 0) return;
            impactCooldown = 0.1f;
            m.TakeDamage(dmg, cp.point, HitSource.Ally, crit);
            gm.AddResonance(gm.config.allyResonancePerHit);
            float k = Mathf.Clamp01(approach / Mathf.Max(1f, gm.MaxSpeed));
            AudioFX.I?.Play(Sfx.Impact, 0.12f + 0.25f * k, Random.Range(1.15f, 1.35f));
            retreat = true;
        }
    }
}
