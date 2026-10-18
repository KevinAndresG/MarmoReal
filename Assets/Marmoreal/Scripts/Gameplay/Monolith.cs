using System.Collections.Generic;
using UnityEngine;

namespace Marmoreal
{
    public enum HitSource { Player, Acolyte, Shockwave, Debris, Seismic, Power, Ally }

    /// <summary>
    /// Bloque de mármol en bruto formado por trozos (hijos de "Block"). Cada golpe arranca los trozos
    /// más cercanos al impacto; al quitar el último se revela la estatua que dormía dentro, brilla
    /// y estalla en polvo. Prefabs en Assets/Marmoreal/Prefabs (editables).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class Monolith : MonoBehaviour
    {
        public enum State { Idle, Rising, Active, Revealing, Levitating }

        public Transform block;
        public Transform statueAnchor;
        public bool large;
        public float riseTime = 0.8f;
        public float revealTime = 1.3f;
        [Tooltip("Intensidad emisiva del brillo de la estatua revelada (HDRP, unidades de luminancia)")]
        public float statueGlow = 2500f;
        public Color statueGlowColor = new Color(1f, 0.82f, 0.5f);
        [Tooltip("Gravedad de los trozos que se quedan sin apoyo al romper los de debajo")]
        public float chunkGravity = 28f;

        public State CurrentState { get; private set; }
        public bool IsTargetable => CurrentState == State.Active;
        public bool Golden { get; private set; }
        /// <summary>Mármol volcánico: al romperse del todo estalla y daña a los bloques vecinos.</summary>
        public bool Volatile { get; private set; }
        public double HP { get; private set; }
        public double HPLeft => System.Math.Max(0, HP - dealt);
        public Bounds WorldBounds => col.bounds;

        MeshRenderer[] chunks;
        readonly List<int> alive = new List<int>();
        BoxCollider col;
        GameObject statue;
        GameObject statuePrefab;
        Renderer[] statueRenderers;
        MaterialPropertyBlock mpb;
        MonolithSpawner owner;

        double chunkHP, pending, dealt;
        double rewardPerChunk, statueReward;
        int tierIndex;
        bool transmuted; // dorado por Midas: no da Ícor
        float stateTime, punch;
        Vector3 baseBlockPos;
        float chipSoundCooldown;

        // Asentamiento: los trozos sin apoyo caen sobre los de debajo (nada flota)
        Vector3[] homePos;
        Vector2Int[] cell;
        float[] targetY, fallVel;
        bool anyFalling;
        static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");

        void Awake()
        {
            col = GetComponent<BoxCollider>();
            chunks = block.GetComponentsInChildren<MeshRenderer>(true);
            mpb = new MaterialPropertyBlock();
            baseBlockPos = block.localPosition;
            CacheGrid();
        }

        void CacheGrid()
        {
            homePos = new Vector3[chunks.Length];
            cell = new Vector2Int[chunks.Length];
            targetY = new float[chunks.Length];
            fallVel = new float[chunks.Length];
            for (int i = 0; i < chunks.Length; i++)
            {
                var lp = chunks[i].transform.localPosition;
                homePos[i] = lp;
                cell[i] = new Vector2Int(Mathf.RoundToInt(lp.x * 2f), Mathf.RoundToInt(lp.z * 2f));
                targetY[i] = lp.y;
            }
        }

        public void Init(MonolithSpawner spawner, double hp, double blockReward, double statueRewardValue, Material chunkMat, int tier, GameObject statueFab, bool golden)
        {
            owner = spawner;
            HP = hp; dealt = 0; pending = 0;
            Golden = golden;
            Volatile = false;
            transmuted = false;
            tierIndex = tier;
            chunkHP = hp / chunks.Length;
            rewardPerChunk = blockReward / chunks.Length;
            statueReward = statueRewardValue;

            alive.Clear();
            if (homePos == null || homePos.Length != chunks.Length) CacheGrid();
            anyFalling = false;
            for (int i = 0; i < chunks.Length; i++)
            {
                chunks[i].transform.localPosition = homePos[i];
                targetY[i] = homePos[i].y;
                fallVel[i] = 0;
                chunks[i].gameObject.SetActive(true);
                chunks[i].sharedMaterial = chunkMat;
                alive.Add(i);
            }

            if (statue == null || statuePrefab != statueFab)
            {
                if (statue) Destroy(statue);
                statuePrefab = statueFab;
                if (statueFab)
                {
                    statue = Instantiate(statueFab, statueAnchor, false);
                    statueRenderers = statue.GetComponentsInChildren<Renderer>();
                }
            }
            if (statue)
            {
                statue.transform.localPosition = Vector3.zero;
                statue.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
                SetStatueGlow(0);
                statue.SetActive(true);
            }

            block.localPosition = baseBlockPos;
            block.localScale = Vector3.one;
            block.localRotation = Quaternion.identity;
            if (statueAnchor) statueAnchor.localRotation = Quaternion.identity;
            UpdateCollider();
            col.enabled = true;
            gameObject.SetActive(true);
            CurrentState = State.Rising;
            stateTime = 0;
            block.localPosition = baseBlockPos + Vector3.down * BlockHeight;
            if (statueAnchor) statueAnchor.localPosition = Vector3.down * BlockHeight;
        }

        public void SetVolatile(Material mat)
        {
            Volatile = true;
            foreach (int i in alive) chunks[i].sharedMaterial = mat;
        }

        float BlockHeight => col ? col.size.y + 0.2f : 4f;

        void Update()
        {
            float dt = Time.deltaTime;
            stateTime += dt;
            chipSoundCooldown -= dt;

            switch (CurrentState)
            {
                case State.Rising:
                {
                    float t = Mathf.Clamp01(stateTime / riseTime);
                    float e = 1f - Mathf.Pow(1f - t, 3f);
                    Vector3 off = Vector3.down * BlockHeight * (1f - e);
                    block.localPosition = baseBlockPos + off;
                    if (statueAnchor) statueAnchor.localPosition = off;
                    if (t >= 1f) { CurrentState = State.Active; block.localPosition = baseBlockPos; if (statueAnchor) statueAnchor.localPosition = Vector3.zero; }
                    break;
                }
                case State.Active:
                {
                    if (punch > 0)
                    {
                        punch = Mathf.Max(0, punch - dt * 5f);
                        float s = 1f + Mathf.Sin(punch * 40f) * punch * 0.05f;
                        block.localScale = new Vector3(s, 2f - s, s);
                    }
                    else block.localScale = Vector3.one;
                    if (anyFalling) SimulateFalling(dt);
                    break;
                }
                case State.Levitating:
                    UpdateLevitation(dt);
                    break;
                case State.Revealing:
                {
                    float t = Mathf.Clamp01(stateTime / revealTime);
                    if (statue)
                    {
                        statue.transform.localPosition = Vector3.up * (Mathf.Sin(t * Mathf.PI * 0.5f) * 0.6f);
                        statue.transform.Rotate(0, dt * 90f * (0.3f + t * 2f), 0, Space.Self);
                        SetStatueGlow(t * t);
                    }
                    if (t >= 1f) Shatter();
                    break;
                }
            }
        }

        public Vector3 ClosestPoint(Vector3 p) => col.enabled ? col.ClosestPoint(p) : transform.position + Vector3.up;

        public void TakeDamage(float dmg, Vector3 point, HitSource src, bool crit)
        {
            if (CurrentState != State.Active || dmg <= 0) return;
            if (alive.Count == 0) // tras una recarga de dominio la lista se pierde: se reconstruye
                for (int i = 0; i < chunks.Length; i++) if (chunks[i].gameObject.activeSelf) alive.Add(i);
            pending += dmg;
            dealt += dmg;
            punch = Mathf.Min(1f, punch + 0.35f + (src == HitSource.Player ? 0.4f : 0f));
            Hud.I?.DamageNumber(point, dmg, crit, src);

            int broken = 0;
            while (pending >= chunkHP && alive.Count > 0)
            {
                pending -= chunkHP;
                BreakChunk(NearestAlive(point), point);
                broken++;
            }
            if (broken > 0)
            {
                if (chipSoundCooldown <= 0) { AudioFX.I?.Play(Sfx.Chip, Mathf.Min(1f, 0.45f + broken * 0.1f), Random.Range(0.85f, 1.2f)); chipSoundCooldown = 0.05f; }
                if (alive.Count == 0) BeginReveal();
                else UpdateCollider();
            }
        }

        int NearestAlive(Vector3 point)
        {
            int best = 0; float bd = float.MaxValue;
            for (int k = 0; k < alive.Count; k++)
            {
                float d = (chunks[alive[k]].transform.position - point).sqrMagnitude;
                if (d < bd) { bd = d; best = k; }
            }
            int idx = alive[best];
            alive.RemoveAt(best);
            return idx;
        }

        void BreakChunk(int idx, Vector3 hitPoint)
        {
            var tr = chunks[idx].transform;
            Vector3 p = tr.position;
            Vector3 away = p - hitPoint; away.y = 0;
            if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
            Vector3 vel = away.normalized * Random.Range(3f, 7f) + Vector3.up * Random.Range(3f, 7f);
            ShardSystem.I?.SpawnDebris(p, tr.rotation, tr.lossyScale, vel, owner ? owner.DebrisIndexFor(tierIndex, Golden) : 0, GetInstanceID());
            ShardSystem.I?.SpawnDust(p, rewardPerChunk, Golden ? 4 : 2, owner ? owner.ShardIndexFor(tierIndex, Golden) : 0, 4f);
            chunks[idx].gameObject.SetActive(false);
            DropAbove(idx);
        }

        /// <summary>Todo lo que estaba encima del trozo roto (misma columna) baja un nivel.</summary>
        void DropAbove(int idx)
        {
            if (homePos == null) CacheGrid();
            float brokenY = targetY[idx];
            foreach (int j in alive)
            {
                if (cell[j] != cell[idx] || targetY[j] <= brokenY + 0.01f) continue;
                targetY[j] -= 1f;
                anyFalling = true;
            }
        }

        void SimulateFalling(float dt)
        {
            bool still = false, landedHard = false;
            foreach (int j in alive)
            {
                var t = chunks[j].transform;
                var lp = t.localPosition;
                if (lp.y <= targetY[j] + 0.0005f) continue;
                fallVel[j] += chunkGravity * dt;
                lp.y -= fallVel[j] * dt;
                if (lp.y <= targetY[j])
                {
                    lp.y = targetY[j];
                    if (fallVel[j] > 3f) landedHard = true;
                    fallVel[j] = 0;
                }
                else still = true;
                t.localPosition = lp;
            }
            anyFalling = still;
            UpdateCollider();
            if (landedHard)
            {
                punch = Mathf.Min(1f, punch + 0.3f);
                if (chipSoundCooldown <= 0) { AudioFX.I?.Play(Sfx.Impact, 0.35f, Random.Range(1.4f, 1.7f)); chipSoundCooldown = 0.06f; }
            }
        }

        void UpdateCollider()
        {
            if (alive.Count == 0) return;
            var b = new Bounds();
            bool first = true;
            foreach (int i in alive)
            {
                var t = chunks[i].transform;
                Vector3 c = transform.InverseTransformPoint(t.position);
                Vector3 e = Vector3.Scale(chunks[i].localBounds.size, t.lossyScale) * 0.5f;
                var cb = new Bounds(c, e * 2f);
                if (first) { b = cb; first = false; } else b.Encapsulate(cb);
            }
            col.center = b.center;
            col.size = b.size;
        }

        void BeginReveal()
        {
            CurrentState = State.Revealing;
            stateTime = 0;
            col.enabled = false;
            block.localScale = Vector3.one;
            AudioFX.I?.Play(Sfx.Statue, 0.8f, Golden ? 1.25f : Random.Range(0.95f, 1.05f));
            if (Golden) AudioFX.I?.Play(Sfx.Golden, 0.8f);
            CameraRig.I?.Shake(0.15f);
            if (Volatile) Erupt();
        }

        /// <summary>Explosión del mármol volcánico: puede encadenar otros volcánicos cercanos.</summary>
        void Erupt()
        {
            var gm = GameManager.I;
            if (!gm) return;
            Vector3 c = transform.position + Vector3.up * 1.2f;
            Shockwave.I?.Blast(c, gm.config.volatileBlastRadius, gm.ImpactDamage * gm.config.volatileBlastMult, HitSource.Debris);
            for (int i = 0; i < 10; i++)
                ShardSystem.I?.SpawnDebris(c + Random.insideUnitSphere * 0.8f, Random.rotation, Vector3.one * Random.Range(0.3f, 0.6f),
                    Random.insideUnitSphere * 11f + Vector3.up * 8f, owner ? owner.DebrisIndexFor(tierIndex, false) : 0, GetInstanceID());
            PowerSystem.I?.FlashAt(c, new Color(1f, 0.45f, 0.15f), 0.6f);
            AudioFX.I?.Play(Sfx.Boom, 0.9f, Random.Range(1.05f, 1.2f));
            CameraRig.I?.Shake(0.45f);
            Hud.I?.HitStop(0.04f);
        }

        void Shatter()
        {
            Vector3 c = statue ? statue.transform.position + Vector3.up * 1.2f : transform.position + Vector3.up * 1.5f;
            ShardSystem.I?.SpawnDust(c, statueReward, Golden ? 40 : 16, owner ? owner.ShardIndexFor(tierIndex, true) : 0, 9f);
            for (int i = 0; i < 6; i++)
                ShardSystem.I?.SpawnDebris(c + Random.insideUnitSphere * 0.6f, Random.rotation, Vector3.one * Random.Range(0.25f, 0.5f),
                    Random.insideUnitSphere * 6f + Vector3.up * 6f, owner ? owner.DebrisIndexFor(0, true) : 0, GetInstanceID());
            AudioFX.I?.Play(Sfx.Shatter, 0.9f, Random.Range(0.9f, 1.1f));
            Hud.I?.StatueFreed(c, statueReward, Golden);
            GameManager.I?.OnStatueFreed(Golden, transmuted);
            if (statue) statue.SetActive(false);
            CurrentState = State.Idle;
            gameObject.SetActive(false);
            owner?.Release(this);
        }

        void SetStatueGlow(float k)
        {
            if (statueRenderers == null) return;
            mpb.SetColor(EmissiveColorId, statueGlowColor * (statueGlow * k));
            foreach (var r in statueRenderers) r.SetPropertyBlock(k <= 0 ? null : mpb);
        }

        // ================================================================= Poderes
        float levHeight, levRise, levHold, levFallVel, levOffset, levDamage, levSpin;

        /// <summary>Ingravidez: el bloque sube girando, flota y se estrella contra el suelo.</summary>
        public void Levitate(float height, float rise, float hold, float damage)
        {
            if (CurrentState != State.Active) return;
            CurrentState = State.Levitating;
            stateTime = 0;
            levHeight = height; levRise = rise; levHold = hold; levDamage = damage;
            levFallVel = 0; levOffset = 0;
            levSpin = Random.Range(-160f, 160f);
            col.enabled = false;
        }

        void UpdateLevitation(float dt)
        {
            if (stateTime < levRise + levHold)
            {
                float t = Mathf.Clamp01(stateTime / levRise);
                levOffset = levHeight * (1f - (1f - t) * (1f - t)) + Mathf.Sin(stateTime * 6f) * 0.15f * t;
                block.localRotation = Quaternion.Euler(Mathf.Sin(stateTime * 3f) * 8f * t, levSpin * stateTime, Mathf.Cos(stateTime * 2.6f) * 8f * t);
            }
            else
            {
                levFallVel += 45f * dt;
                levOffset -= levFallVel * dt;
                block.localRotation = Quaternion.Slerp(block.localRotation, Quaternion.identity, dt * 10f);
                if (levOffset <= 0f)
                {
                    levOffset = 0;
                    block.localPosition = baseBlockPos;
                    block.localRotation = Quaternion.identity;
                    if (statueAnchor) { statueAnchor.localPosition = Vector3.zero; statueAnchor.localRotation = Quaternion.identity; }
                    CurrentState = State.Active;
                    col.enabled = true;
                    CameraRig.I?.Shake(0.22f);
                    AudioFX.I?.Play(Sfx.Impact, 0.8f, Random.Range(0.6f, 0.75f));
                    TakeDamage(levDamage, transform.position + Vector3.up * 0.5f, HitSource.Power, false);
                    return;
                }
            }
            block.localPosition = baseBlockPos + Vector3.up * levOffset;
            if (statueAnchor) { statueAnchor.localPosition = Vector3.up * levOffset; statueAnchor.localRotation = block.localRotation; }
        }

        /// <summary>Toque de Midas: convierte el bloque en oro (vale mucho más).</summary>
        public bool MakeGolden()
        {
            if (Golden || (CurrentState != State.Active && CurrentState != State.Rising) || !owner) return false;
            var gm = GameManager.I;
            Golden = true;
            transmuted = true;
            float k = gm ? gm.config.goldenRewardMult * gm.StatueMult : 10f;
            rewardPerChunk *= k;
            statueReward *= k * 2f;
            foreach (int i in alive) chunks[i].sharedMaterial = owner.goldenMaterial;
            ShardSystem.I?.SpawnSparkle(transform.position + Vector3.up * 2f, 30, owner.ShardIndexFor(tierIndex, true), 7f);
            punch = 1f;
            return true;
        }

        /// <summary>Retira el bloque sin recompensa (al pulir).</summary>
        public void Despawn()
        {
            CurrentState = State.Idle;
            gameObject.SetActive(false);
        }
    }
}
