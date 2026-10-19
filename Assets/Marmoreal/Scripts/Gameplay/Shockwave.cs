using System.Collections.Generic;
using UnityEngine;

namespace Marmoreal
{
    /// <summary>
    /// Onda de Resonancia: se carga golpeando bloques y al liberarla talla todo lo que alcanza el anillo.
    /// Nodos del árbol: Eco Resonante (2 ecos), Resonancia Perpetua (auto), Embestida Sísmica (mini-ondas).
    /// </summary>
    public class Shockwave : MonoBehaviour
    {
        static Shockwave _i;
        /// <summary>Singleton resistente a recargas de dominio (se re-busca si se pierde).</summary>
        public static Shockwave I { get { if (!_i) _i = FindFirstObjectByType<Shockwave>(); return _i; } private set => _i = value; }

        public Transform center;
        [Tooltip("Anillos (hijos desactivados) reutilizados por las ondas")]
        public Transform[] rings;
        public float expandTime = 0.55f;
        public float ringHeight = 0.6f;
        [Tooltip("Brillo de los anillos respecto al emisivo del material (1 = tal cual). Se apaga al expandirse y se reparte entre ondas simultáneas")]
        [Range(0f, 1f)] public float glow = 0.3f;
        [System.NonSerialized] MaterialPropertyBlock block;
        [System.NonSerialized] bool hasTint, hasBase;
        [System.NonSerialized] Color tintColor, baseEmissive;

        /// <summary>Color de la onda según la canica elegida (Colección).</summary>
        public void SetTint(Color c)
        {
            hasTint = true;
            tintColor = c;
            baseEmissive = c.linear * 1024f;
            hasBase = true;
        }

        void ApplyGlow(Transform ring, float k, float crowd)
        {
            if (!ring.TryGetComponent<Renderer>(out var mr)) return;
            if (!hasBase)
            {
                baseEmissive = mr.sharedMaterial && mr.sharedMaterial.HasProperty("_EmissiveColor") ? mr.sharedMaterial.GetColor("_EmissiveColor") : Color.white * 1024f;
                hasBase = true;
            }
            block ??= new MaterialPropertyBlock();
            if (hasTint) block.SetColor("_BaseColor", tintColor);
            float fade = (1f - k) * (1f - k);
            block.SetColor("_EmissiveColor", baseEmissive * (glow * fade * crowd));
            mr.SetPropertyBlock(block);
        }

        class Wave
        {
            public Transform ring;
            public Vector3 origin;
            public float radius, damage, t, delay;
            public bool mini;
            public HitSource source;
            public float ringScale = 1f;
            public readonly HashSet<Monolith> hit = new HashSet<Monolith>();
        }

        readonly List<Wave> waves = new List<Wave>();
        readonly List<Monolith> tmp = new List<Monolith>();
        int ringIdx;

        void Awake()
        {
            I = this;
            foreach (var r in rings) if (r) r.gameObject.SetActive(false);
        }

        public void TryRelease()
        {
            var gm = GameManager.I;
            if (!gm) return;
            if (!gm.ConsumeResonance())
            {
                if (gm.Resonance < 1f) Hud.I?.FlashResonance();
                return;
            }
            Release(gm);
        }

        void Release(GameManager gm)
        {
            Vector3 o = center ? center.position : transform.position;
            float r = gm.ShockwaveRadius;
            float dmg = gm.ShockwaveDamage;
            Add(o, r, dmg, 0f, false);
            // Eco Resonante: nivel L → L+1 ecos, cada uno más amplio y algo más débil
            int echoes = gm.Has("eco") ? Mathf.RoundToInt(gm.Value("eco")) : 0;
            for (int k = 1; k <= echoes; k++)
                Add(o, r * (1f + 0.3f * k), dmg * Mathf.Max(0.25f, 0.6f - 0.1f * k), 0.35f * k, false);
            AudioFX.I?.Play(Sfx.Boom, 1f);
            CameraRig.I?.Shake(0.6f);
            CameraRig.I?.Kick(6f);
            ShardSystem.I?.ForceCollect(3f + 0.35f * echoes, r * (1f + 0.3f * echoes)); // la onda arrastra el polvo de su radio
        }

        public void Mini(Vector3 at, float damage, float radius = 4f)
        {
            Add(at, radius, damage, 0f, true, HitSource.Seismic);
            AudioFX.I?.Play(Sfx.Boom, 0.4f, 1.6f);
        }

        /// <summary>Explosión genérica (poderes): anillo + daño a todo lo que alcanza. damage 0 = solo visual.</summary>
        public void Blast(Vector3 at, float radius, float damage, HitSource source, bool small = false)
        {
            Add(at, radius, damage, 0f, small, source);
        }

        readonly List<Transform> extraRings = new List<Transform>();

        Transform FreeRing()
        {
            if (rings == null || rings.Length == 0) return null;
            for (int k = 0; k < rings.Length; k++)
            {
                var r = rings[(ringIdx + k) % rings.Length];
                if (r && !r.gameObject.activeSelf) { ringIdx += k + 1; return r; }
            }
            foreach (var r in extraRings) if (r && !r.gameObject.activeSelf) return r;
            if (extraRings.Count < 12 && rings[0])
            {
                var clone = Instantiate(rings[0], rings[0].parent);
                clone.gameObject.SetActive(false);
                extraRings.Add(clone);
                return clone;
            }
            return rings[ringIdx++ % rings.Length];
        }

        void Add(Vector3 o, float radius, float damage, float delay, bool mini, HitSource source = HitSource.Shockwave)
        {
            Transform ring = FreeRing();
            if (ring) ring.gameObject.SetActive(false);
            waves.Add(new Wave { ring = ring, origin = o, radius = radius, damage = damage, delay = delay, mini = mini, source = source });
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm && gm.Has("perpetua") && gm.Resonance >= 1f && !(Hud.I && Hud.I.BlocksGameplay))
            {
                gm.ConsumeResonance();
                Release(gm);
            }

            float dt = Time.deltaTime;
            int visible = 0;
            foreach (var w in waves) if (w.delay <= 0 && w.ring) visible++;
            float crowd = 1f / Mathf.Sqrt(Mathf.Max(1, visible));
            for (int i = waves.Count - 1; i >= 0; i--)
            {
                var w = waves[i];
                if (w.delay > 0) { w.delay -= dt; continue; }
                w.t += dt;
                float k = Mathf.Clamp01(w.t / (w.mini ? expandTime * 0.6f : expandTime));
                float e = 1f - (1f - k) * (1f - k) * (1f - k);
                float r = w.radius * e;

                if (w.ring)
                {
                    if (!w.ring.gameObject.activeSelf) w.ring.gameObject.SetActive(true);
                    // ponytail: brillo repartido entre anillos visibles (1/√n) para que el frenesí no deslumbre
                    ApplyGlow(w.ring, k, w.mini ? 0.5f * crowd : crowd);
                    w.ring.position = new Vector3(w.origin.x, ArenaShape.Height(w.origin) + ringHeight, w.origin.z);
                    float thick = Mathf.Lerp(1f, 0.05f, k);
                    w.ring.localScale = new Vector3(Mathf.Max(0.01f, r), thick * (w.mini ? 0.6f : 1.5f), Mathf.Max(0.01f, r));
                }

                if (MonolithSpawner.I)
                {
                    MonolithSpawner.I.InRadius(w.origin, r, tmp);
                    foreach (var m in tmp)
                    {
                        if (!w.hit.Add(m)) continue;
                        Vector3 p = m.ClosestPoint(w.origin);
                        if (w.damage > 0) m.TakeDamage(w.damage, p, w.source, false);
                    }
                }

                if (k >= 1f)
                {
                    if (w.ring) w.ring.gameObject.SetActive(false);
                    waves.RemoveAt(i);
                }
            }
        }
    }
}
