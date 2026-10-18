using UnityEngine;
using UnityEngine.Rendering;

namespace Marmoreal
{
    /// <summary>
    /// FX de alto volumen sin GameObjects: polvo de mármol (recolectable) y escombros (trozos que saltan).
    /// Todo se dibuja con Graphics.RenderMeshInstanced (GPU instancing) y se simula a mano sobre
    /// el perfil analítico del cuenco → cero físicas, cero GC por frame.
    /// </summary>
    public class ShardSystem : MonoBehaviour
    {
        static ShardSystem _i;
        /// <summary>Singleton resistente a recargas de dominio (se re-busca si se pierde).</summary>
        public static ShardSystem I { get { if (!_i) _i = FindFirstObjectByType<ShardSystem>(); return _i; } private set => _i = value; }

        [Header("Polvo")]
        public Mesh shardMesh;
        [Tooltip("Materiales emisivos con GPU Instancing, uno por tier de mármol")]
        public Material[] shardMaterials;
        public int maxShards = 1600;
        public float shardScale = 0.22f;

        [Header("Escombros")]
        public Mesh debrisMesh;
        [Tooltip("Materiales de los trozos (instancing), mismos índices que los bloques")]
        public Material[] debrisMaterials;
        public int maxDebris = 500;
        public float debrisLife = 2.6f;

        public Transform collector;

        struct Shard
        {
            public Vector3 pos, vel, spin;
            public float age, scale;
            public double value;
            public byte mat;
        }

        struct Debris
        {
            public Vector3 pos, vel, spin, scale;
            public Quaternion rot;
            public float age;
            public byte mat;
            public bool hot;
            public int owner;
        }

        Shard[] shards;
        int shardCount;
        Debris[] debris;
        int debrisCount;
        Matrix4x4[][] buckets;
        int[] bucketCounts;
        float forceCollectTimer, forceCollectRadius;

        public int ActiveShards => shardCount;

        void Awake()
        {
            I = this;
            Init();
        }

        void Init()
        {
            shards = new Shard[maxShards];
            shardCount = 0; debrisCount = 0;
            debris = new Debris[maxDebris];
            int mats = Mathf.Max(shardMaterials?.Length ?? 0, debrisMaterials?.Length ?? 0);
            buckets = new Matrix4x4[mats][];
            for (int i = 0; i < mats; i++) buckets[i] = new Matrix4x4[1023];
            bucketCounts = new int[mats];
        }

        // ================================================================= API
        public void SpawnDust(Vector3 pos, double totalValue, int count, int matIndex, float burst = 5f)
        {
            if (shards == null) Init();
            if (totalValue <= 0 || count <= 0) return;
            double each = totalValue / count;
            for (int i = 0; i < count; i++)
            {
                if (shardCount >= maxShards)
                {
                    // Nunca se pierde polvo: se suma a un fragmento existente.
                    shards[Random.Range(0, shardCount)].value += each;
                    continue;
                }
                Vector3 dir = Random.insideUnitSphere; dir.y = Mathf.Abs(dir.y) + 0.6f;
                shards[shardCount++] = new Shard
                {
                    pos = pos + Random.insideUnitSphere * 0.3f,
                    vel = dir.normalized * burst * Random.Range(0.5f, 1.2f),
                    spin = new Vector3(Random.Range(-540f, 540f), Random.Range(-540f, 540f), Random.Range(-540f, 540f)),
                    age = 0,
                    scale = shardScale * Random.Range(0.7f, 1.25f),
                    value = each,
                    mat = (byte)Mathf.Clamp(matIndex, 0, shardMaterials.Length - 1)
                };
            }
        }

        public void SpawnDebris(Vector3 pos, Quaternion rot, Vector3 scale, Vector3 vel, int matIndex, int owner)
        {
            if (debris == null) Init();
            if (debrisCount >= maxDebris) { debris[Random.Range(0, debrisCount)].age = debrisLife; return; }
            debris[debrisCount++] = new Debris
            {
                pos = pos, rot = rot, scale = scale, vel = vel,
                spin = new Vector3(Random.Range(-300f, 300f), Random.Range(-300f, 300f), Random.Range(-300f, 300f)),
                age = 0, mat = (byte)Mathf.Clamp(matIndex, 0, debrisMaterials.Length - 1),
                hot = true, owner = owner
            };
        }

        /// <summary>
        /// Atrae hacia la canica el polvo que esté dentro de un radio durante unos segundos (onda, Supernova).
        /// Fuera de eso el polvo NO vuela solo: hay que pasar cerca (radio del Imán).
        /// </summary>
        public void ForceCollect(float seconds, float radius)
        {
            if (forceCollectTimer <= 0) forceCollectRadius = 0;
            forceCollectTimer = Mathf.Max(forceCollectTimer, seconds);
            forceCollectRadius = Mathf.Max(forceCollectRadius, radius);
        }

        Vector3 attractorPos;
        float attractorTimer;

        /// <summary>Todo el polvo vuela hacia un punto (Vórtice) y se cobra al llegar.</summary>
        public void AttractTo(Vector3 pos, float seconds) { attractorPos = pos; attractorTimer = seconds; }

        /// <summary>Destellos sin valor (solo visuales) para bendiciones, Midas, etc.</summary>
        public void SpawnSparkle(Vector3 pos, int count, int matIndex, float burst = 6f)
        {
            if (shards == null) Init();
            for (int i = 0; i < count && shardCount < maxShards; i++)
            {
                Vector3 dir = Random.insideUnitSphere; dir.y = Mathf.Abs(dir.y) + 0.4f;
                shards[shardCount++] = new Shard
                {
                    pos = pos + Random.insideUnitSphere * 0.5f,
                    vel = dir.normalized * burst * Random.Range(0.5f, 1.2f),
                    spin = new Vector3(Random.Range(-540f, 540f), Random.Range(-540f, 540f), Random.Range(-540f, 540f)),
                    scale = shardScale * Random.Range(0.6f, 1.1f),
                    value = 0,
                    mat = (byte)Mathf.Clamp(matIndex, 0, shardMaterials.Length - 1)
                };
            }
        }

        public void ClearAll(bool collectValue)
        {
            if (collectValue && GameManager.I)
            {
                double sum = 0;
                for (int i = 0; i < shardCount; i++) sum += shards[i].value;
                GameManager.I.AddDust(sum);
            }
            shardCount = 0; debrisCount = 0;
        }

        // ================================================================= Simulación
        void Update()
        {
            if (shards == null || debris == null || buckets == null) Init(); // tras una recarga de dominio
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            forceCollectTimer -= dt;
            SimShards(dt);
            SimDebris(dt);
            Render();
        }

        void SimShards(float dt)
        {
            var gm = GameManager.I;
            attractorTimer -= dt;
            bool toAttractor = attractorTimer > 0;
            Vector3 target = toAttractor ? attractorPos : collector ? collector.position : Vector3.zero;
            float magnet = gm ? gm.MagnetRadius : 5f;
            float magnetSq = magnet * magnet;
            bool force = forceCollectTimer > 0;
            float forceSq = forceCollectRadius * forceCollectRadius;
            double collected = 0;
            int collectedCount = 0;

            for (int i = 0; i < shardCount; i++)
            {
                ref Shard s = ref shards[i];
                s.age += dt;
                Vector3 to = target - s.pos;
                float dSq = to.sqrMagnitude;
                bool attract = (collector || toAttractor) && s.age > 0.35f && (dSq < magnetSq || (force && dSq < forceSq) || toAttractor);

                if (attract)
                {
                    float d = Mathf.Sqrt(dSq);
                    float speed = Mathf.Lerp(30f, 12f, Mathf.Clamp01(d / 20f)) + s.age * 3f;
                    s.vel = Vector3.Lerp(s.vel, to / Mathf.Max(d, 0.001f) * speed, 1f - Mathf.Exp(-dt * 9f));
                    s.pos += s.vel * dt;
                    if (d < 0.9f)
                    {
                        collected += s.value;
                        collectedCount++;
                        shards[i] = shards[--shardCount];
                        i--;
                        continue;
                    }
                }
                else
                {
                    s.vel.y -= 18f * dt;
                    s.pos += s.vel * dt;
                    float floor = ArenaShape.Height(s.pos) + s.scale * 0.4f;
                    if (s.pos.y < floor)
                    {
                        s.pos.y = floor;
                        if (s.vel.y < 0) s.vel.y = -s.vel.y * 0.35f;
                        s.vel.x *= 0.82f; s.vel.z *= 0.82f;
                    }
                }
            }

            if (collectedCount > 0 && gm)
            {
                gm.CollectDust(collected);
                AudioFX.I?.Tink();
                Hud.I?.PulseDust();
            }
        }

        void SimDebris(float dt)
        {
            var gm = GameManager.I;
            bool metralla = gm && gm.Has("metralla");
            var sp = MonolithSpawner.I;

            for (int i = 0; i < debrisCount; i++)
            {
                ref Debris d = ref debris[i];
                d.age += dt;
                if (d.age >= debrisLife) { debris[i] = debris[--debrisCount]; i--; continue; }
                d.vel.y -= 20f * dt;
                d.pos += d.vel * dt;
                d.rot = Quaternion.Euler(d.spin * dt) * d.rot;
                float floor = ArenaShape.Height(d.pos) + d.scale.y * 0.35f;
                if (d.pos.y < floor)
                {
                    d.pos.y = floor;
                    if (d.vel.y < 0) d.vel.y = -d.vel.y * 0.3f;
                    d.vel.x *= 0.6f; d.vel.z *= 0.6f;
                    d.spin *= 0.6f;
                }

                if (metralla && d.hot && sp && d.age < 1.2f && d.vel.sqrMagnitude > 9f)
                {
                    var hit = sp.FindMonolithAt(d.pos, 0.5f, d.owner);
                    if (hit)
                    {
                        d.hot = false;
                        d.vel *= -0.3f;
                        hit.TakeDamage(gm.ImpactDamage * 0.4f, d.pos, HitSource.Debris, false);
                    }
                }
            }
        }

        // ================================================================= Render
        void Render()
        {
            if (shardMesh && shardMaterials != null && shardMaterials.Length > 0)
            {
                ClearBuckets();
                float t = Time.time;
                for (int i = 0; i < shardCount; i++)
                {
                    ref Shard s = ref shards[i];
                    float pop = Mathf.Min(1f, s.age * 8f);
                    var m = Matrix4x4.TRS(s.pos, Quaternion.Euler(s.spin * (t * 0.25f + i)), Vector3.one * (s.scale * pop));
                    Push(s.mat, m, shardMesh, shardMaterials);
                }
                Flush(shardMesh, shardMaterials, ShadowCastingMode.Off);
            }

            if (debrisMesh && debrisMaterials != null && debrisMaterials.Length > 0)
            {
                ClearBuckets();
                for (int i = 0; i < debrisCount; i++)
                {
                    ref Debris d = ref debris[i];
                    float shrink = Mathf.Clamp01((debrisLife - d.age) / 0.6f);
                    Push(d.mat, Matrix4x4.TRS(d.pos, d.rot, d.scale * shrink), debrisMesh, debrisMaterials);
                }
                Flush(debrisMesh, debrisMaterials, ShadowCastingMode.On);
            }
        }

        void ClearBuckets() { for (int i = 0; i < bucketCounts.Length; i++) bucketCounts[i] = 0; }

        void Push(int mat, Matrix4x4 m, Mesh mesh, Material[] mats)
        {
            if (mat >= buckets.Length) return;
            buckets[mat][bucketCounts[mat]++] = m;
            if (bucketCounts[mat] == 1023)
            {
                Draw(mesh, mats[mat], buckets[mat], 1023, ShadowCastingMode.Off);
                bucketCounts[mat] = 0;
            }
        }

        void Flush(Mesh mesh, Material[] mats, ShadowCastingMode shadows)
        {
            for (int i = 0; i < bucketCounts.Length && i < mats.Length; i++)
                if (bucketCounts[i] > 0) Draw(mesh, mats[i], buckets[i], bucketCounts[i], shadows);
        }

        static void Draw(Mesh mesh, Material mat, Matrix4x4[] data, int count, ShadowCastingMode shadows)
        {
            if (!mat) return;
            var rp = new RenderParams(mat)
            {
                shadowCastingMode = shadows,
                receiveShadows = true,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 200f),
                motionVectorMode = MotionVectorGenerationMode.Camera
            };
            Graphics.RenderMeshInstanced(rp, mesh, 0, data, count);
        }
    }
}
