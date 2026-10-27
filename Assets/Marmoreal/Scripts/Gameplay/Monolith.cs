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
        /// <summary>Coloso (jefe): 3 fases visibles, recompensa de Ícor. Trial = Prueba de sala (campaña).</summary>
        public bool IsBoss { get; private set; }
        public bool Trial { get; private set; }
        public int BossPhase { get; private set; }
        /// <summary>Modificador del Coloso del Modo Infinito (null = ninguno).</summary>
        public BossModDef Mod { get; private set; }
        [Tooltip("Brillo de las grietas del Coloso en las fases II y III")]
        public Color phaseGlow2 = new Color(1f, 0.45f, 0.1f), phaseGlow3 = new Color(1f, 0.08f, 0.04f);
        public float phaseGlowIntensity = 20f;
        [Tooltip("Grieta (punto débil): color y brillo (nits) del trozo marcado; late para llamar la atención")]
        public Color weakGlowColor = new Color(0.45f, 0.9f, 1f);
        public float weakGlowIntensity = 180f;
        /// <summary>Mármol volcánico: al romperse del todo estalla y daña a los bloques vecinos.</summary>
        public bool Volatile { get; private set; }
        public double HP { get; private set; }
        public double HPLeft => System.Math.Max(0, HP - dealt);

        /// <summary>Jardín de Rosa: el bloque sin romper crece (HP y recompensa × m).</summary>
        public void Overgrow(float m)
        {
            HP *= m; chunkHP *= m; rewardPerChunk *= m; statueReward *= m;
            punch = 1f;
        }
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
        [System.NonSerialized] int weakIdx = -1;
        [System.NonSerialized] MaterialPropertyBlock weakMpb;
        Color baseGlow = Color.black; // brillo de fase del Coloso (las grietas lo restauran al moverse)
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
            IsBoss = false; Trial = false; BossPhase = 0; Mod = null;
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
            weakIdx = -1;
            SetChunkGlow(Color.black);
            PickWeakPoint();
            UpdateCollider();
            col.enabled = true;
            gameObject.SetActive(true);
            CurrentState = State.Rising;
            stateTime = 0;
            block.localPosition = baseBlockPos + Vector3.down * BlockHeight;
            if (statueAnchor) statueAnchor.localPosition = Vector3.down * BlockHeight;
        }

        /// <summary>Convierte el bloque recién iniciado en Coloso (lo llama MonolithSpawner.SpawnBoss).</summary>
        public void MakeBoss(bool trial, BossModDef mod = null)
        {
            IsBoss = true; Trial = trial; BossPhase = 0; Mod = mod;
            SetChunkGlow(Color.black);
        }

        void SetChunkGlow(Color c)
        {
            baseGlow = c;
            var b = new MaterialPropertyBlock();
            if (c != Color.black) b.SetColor(EmissiveColorId, c);
            for (int i = 0; i < chunks.Length; i++) if (i != weakIdx) chunks[i].SetPropertyBlock(c == Color.black ? null : b);
        }

        // ================================================================= Grietas (S11)

        /// <summary>Marca como grieta un trozo exterior y bajo (alcanzable rodando). Lo devuelve a su brillo normal al moverla.</summary>
        void PickWeakPoint()
        {
            if (weakIdx >= 0 && weakIdx < chunks.Length)
            {
                var b = new MaterialPropertyBlock();
                if (baseGlow != Color.black) b.SetColor(EmissiveColorId, baseGlow);
                chunks[weakIdx].SetPropertyBlock(baseGlow == Color.black ? null : b);
            }
            weakIdx = -1;
            if (alive.Count == 0 || homePos == null) return;
            float minY = float.MaxValue, cx = 0, cz = 0;
            foreach (int i in alive) { minY = Mathf.Min(minY, targetY[i]); cx += homePos[i].x; cz += homePos[i].z; }
            cx /= alive.Count; cz /= alive.Count;
            // puntuación = lo exterior que está − lo alto que queda; se elige al azar entre los mejores
            int best = -1; float bestScore = float.MinValue;
            foreach (int i in alive)
            {
                float s = new Vector2(homePos[i].x - cx, homePos[i].z - cz).magnitude - 0.6f * (targetY[i] - minY) + Random.value * 0.9f;
                if (s > bestScore) { bestScore = s; best = i; }
            }
            weakIdx = best;
        }

        public bool TryWeakPos(out Vector3 p)
        {
            bool ok = weakIdx >= 0 && weakIdx < chunks.Length && chunks[weakIdx].gameObject.activeSelf;
            p = ok ? chunks[weakIdx].transform.position : Vector3.zero;
            return ok;
        }

        /// <summary>¿El contacto de la canica cae sobre la grieta?</summary>
        public bool HitsWeakPoint(Vector3 point)
        {
            if (weakIdx < 0 || weakIdx >= chunks.Length || !chunks[weakIdx].gameObject.activeSelf) return false;
            float r = GameManager.I ? GameManager.I.config.weakPointRadius : 1.25f;
            return (chunks[weakIdx].transform.position - point).sqrMagnitude < r * r;
        }

        /// <summary>Grieta acertada (tras aplicar el daño): estallido de luz y polvo, y la grieta salta a otro trozo.</summary>
        public void OnWeakHit(Vector3 point)
        {
            Vector3 p = weakIdx >= 0 && weakIdx < chunks.Length ? chunks[weakIdx].transform.position : point;
            PowerSystem.I?.FlashAt(p, weakGlowColor, 0.35f);
            ShardSystem.I?.SpawnSparkle(p, 18, owner ? owner.ShardIndexFor(tierIndex, true) : 0, 6f);
            AudioFX.I?.Play(Sfx.Golden, 0.55f, Random.Range(1.5f, 1.7f));
            if (CurrentState == State.Active && weakIdx >= 0 && chunks[weakIdx].gameObject.activeSelf) PickWeakPoint();
        }

        void UpdateWeakGlow()
        {
            if (weakIdx < 0 || weakIdx >= chunks.Length) return;
            weakMpb ??= new MaterialPropertyBlock(); // se pierde en recargas de dominio
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 7f);
            weakMpb.SetColor(EmissiveColorId, weakGlowColor * (weakGlowIntensity * pulse) + baseGlow);
            chunks[weakIdx].SetPropertyBlock(weakMpb);
        }

        void BossPhaseCheck(Vector3 point)
        {
            int phase = dealt >= HP * 2.0 / 3.0 ? 2 : dealt >= HP / 3.0 ? 1 : 0;
            if (phase <= BossPhase) return;
            BossPhase = phase;
            SetChunkGlow((phase == 1 ? phaseGlow2 : phaseGlow3) * phaseGlowIntensity);
            Vector3 c = transform.position + Vector3.up * 2f;
            PowerSystem.I?.FlashAt(c, phase == 1 ? phaseGlow2 : phaseGlow3, 0.8f);
            for (int i = 0; i < 14; i++)
                ShardSystem.I?.SpawnDebris(c + Random.insideUnitSphere * 1.5f, Random.rotation, Vector3.one * Random.Range(0.4f, 0.8f),
                    Random.insideUnitSphere * 12f + Vector3.up * 9f, owner ? owner.DebrisIndexFor(tierIndex, false) : 0, GetInstanceID());
            AudioFX.I?.Play(Sfx.Boom, 1f, 0.7f);
            CameraRig.I?.Shake(0.6f);
            Hud.I?.Banner(phase == 1 ? "COLOSO · FASE II" : "COLOSO · FASE III");
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
            if (Mod != null && Mod.regenPerSec > 0 && CurrentState == State.Active)
            {
                double heal = HP * Mod.regenPerSec * dt;
                dealt = System.Math.Max(0, dealt - heal);
                pending = System.Math.Max(0, pending - heal);
            }

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
                    UpdateWeakGlow();
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
            if (Mod != null) dmg *= src == HitSource.Power ? Mod.powerTaken : src == HitSource.Player ? Mod.playerTaken : 1f;
            if (alive.Count == 0) // tras una recarga de dominio la lista se pierde: se reconstruye
                for (int i = 0; i < chunks.Length; i++) if (chunks[i].gameObject.activeSelf) alive.Add(i);
            pending += dmg;
            dealt += dmg;
            punch = Mathf.Min(1f, punch + 0.35f + (src == HitSource.Player ? 0.4f : 0f));
            Hud.I?.DamageNumber(point, dmg, crit, src);
            if (src == HitSource.Player) BiomeManager.I?.OnPlayerHit(this, dmg, point);
            if (IsBoss) BossPhaseCheck(point);

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
            if (idx == weakIdx) PickWeakPoint(); // la grieta cayó con el trozo: se abre otra
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
            gm.OnVolatileBroken();
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
            if (IsBoss) { GameManager.I?.OnBossDefeated(Trial, Mod); SetChunkGlow(Color.black); }
            GameManager.I?.OnStatueFreed(Golden, transmuted);
            if (statue) statue.SetActive(false);
            CurrentState = State.Idle;
            gameObject.SetActive(false);
            owner?.Release(this);
        }

        void SetStatueGlow(float k)
        {
            if (statueRenderers == null) return;
            mpb ??= new MaterialPropertyBlock(); // se pierde en recargas de dominio
            mpb.SetColor(EmissiveColorId, statueGlowColor * (statueGlow * k));
            foreach (var r in statueRenderers) r.SetPropertyBlock(k <= 0 ? null : mpb);
        }

        // ================================================================= Poderes
        float levHeight, levRise, levHold, levFallVel, levOffset, levDamage, levSpin;

        /// <summary>Ingravidez: el bloque sube girando, flota y se estrella contra el suelo.</summary>
        public void Levitate(float height, float rise, float hold, float damage)
        {
            if (CurrentState != State.Active) return;
            if (IsBoss) { TakeDamage(damage, transform.position + Vector3.up, HitSource.Power, false); return; } // el Coloso no levita
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
            if (Golden || IsBoss || (CurrentState != State.Active && CurrentState != State.Rising) || !owner) return false;
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
