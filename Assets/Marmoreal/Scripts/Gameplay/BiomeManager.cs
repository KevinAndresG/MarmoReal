using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Marmoreal
{
    /// <summary>
    /// Salas por Pulido: aplica el BiomeDef actual (GameState.biomeId) a la escena en Play — materiales de
    /// --- SANTUARIO ---, tiers de bloque/polvo, Sol, Volumen y el hijo de --- SALA --- — y ejecuta su regla.
    /// biomes[0] es la sala base (todos sus campos rellenos: sirve de "original").
    /// </summary>
    public class BiomeManager : MonoBehaviour
    {
        static BiomeManager _i;
        public static BiomeManager I { get { if (!_i) _i = FindFirstObjectByType<BiomeManager>(); return _i; } private set => _i = value; }

        [Tooltip("Orden de campaña. [0] = sala base")]
        public BiomeDef[] biomes;
        [Tooltip("Hijos de --- SALA --- (decoración/luces), mismo orden que biomes")]
        public GameObject[] rooms;
        public Transform worldRoot;
        public Light sun;
        public Volume volume;
        [Tooltip("Sonda en tiempo real: se vuelve a renderizar al cambiar de sala")]
        public HDAdditionalReflectionData reflectionProbe;

        [NonSerialized] public int pendingChoice = -1;
        [NonSerialized] string appliedId;
        [NonSerialized] GameManager subscribed;
        [NonSerialized] float heat, reflectCd, sparkCd;
        [NonSerialized] bool lavaHintShown;
        [NonSerialized] string pendingBanner;
        [NonSerialized] int lavaBursts, reflects;

        public BiomeDef Current => Find(GameManager.I ? GameManager.I.S.biomeId : null);
        public string DebugState => $"biome={appliedId} heat={heat:0.0} lava={lavaBursts} reflejos={reflects}";

        void Awake() => I = this;

        void OnDestroy() { if (subscribed) subscribed.Prestiged -= OnPrestiged; }

        BiomeDef Find(string id)
        {
            if (biomes == null || biomes.Length == 0) return null;
            foreach (var b in biomes) if (b && b.id == id) return b;
            return biomes[0];
        }

        int IndexOf(BiomeDef d) => Array.IndexOf(biomes, d);

        // =================== Selección al Pulir ===================
        /// <summary>¿Hay más de una sala desbloqueada para elegir al Pulir?</summary>
        public bool CanChoose(int newPrestige)
        {
            int n = 0;
            if (biomes != null) for (int i = 0; i < biomes.Length; i++) if (Unlocked(i)) n++;
            return n > 1;
        }

        /// <summary>Campaña: la primera sala se abre sola; cada Prueba superada abre la siguiente.
        /// Migración: las partidas de antes de la S4 conservan las salas que ya visitaron (legacyStage).</summary>
        public bool Unlocked(int i)
        {
            var gm = GameManager.I;
            if (!gm || biomes == null || i < 0 || i >= biomes.Length || !biomes[i]) return false;
            int st = biomes[i].campaignStage;
            return st == 0 || st <= gm.S.legacyStage || gm.S.pieces.Contains(st - 1);
        }

        public BiomeDef ByStage(int stage)
        {
            if (biomes != null) foreach (var b in biomes) if (b && b.campaignStage == stage) return b;
            return null;
        }

        /// <summary>Sala del próximo Pulido: la elegida; si no, la de la Prueba pendiente más temprana;
        /// con todo superado (o en el Modo Infinito), rotación mezclada entre las desbloqueadas (determinista por Pulido).</summary>
        public BiomeDef NextFor(int newPrestige)
        {
            if (biomes == null || biomes.Length == 0) return null;
            if (pendingChoice >= 0 && pendingChoice < biomes.Length && Unlocked(pendingChoice)) return biomes[pendingChoice];
            var gm = GameManager.I;
            BiomeDef pending = null;
            var open = new System.Collections.Generic.List<BiomeDef>();
            for (int i = 0; i < biomes.Length; i++)
            {
                if (!Unlocked(i)) continue;
                var b = biomes[i];
                open.Add(b);
                if (b.trialGallery > 0 && gm && !gm.S.pieces.Contains(b.campaignStage) && (!pending || b.campaignStage < pending.campaignStage)) pending = b;
            }
            if (pending && gm && !gm.S.infinite) return pending;
            if (open.Count > 1) open.Remove(Current);
            if (open.Count == 0) return biomes[0];
            return open[(newPrestige * 7 + 3) % open.Count];
        }

        /// <summary>Botón del modal de Pulido: pasa a la siguiente sala desbloqueada.</summary>
        public BiomeDef CycleChoice(int newPrestige)
        {
            int cur = IndexOf(NextFor(newPrestige));
            for (int k = 1; k <= biomes.Length; k++)
            {
                int i = (cur + k) % biomes.Length;
                if (Unlocked(i)) { pendingChoice = i; break; }
            }
            return NextFor(newPrestige);
        }

        // =================== Aplicar a la escena ===================
        Material Original(Material m)
        {
            foreach (var b in biomes)
                if (b) foreach (var s in b.swaps) if (s.to == m && s.from) return s.from;
            return m;
        }

        static Material Swap(BiomeDef d, Material orig)
        {
            foreach (var s in d.swaps) if (s.from == orig && s.to) return s.to;
            return orig;
        }

        void Apply(BiomeDef d)
        {
            var b0 = biomes[0];
            appliedId = d.id;
            if (worldRoot)
                foreach (var r in worldRoot.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var m = Swap(d, Original(mats[i]));
                        if (m != mats[i]) { mats[i] = m; changed = true; }
                    }
                    if (changed) r.sharedMaterials = mats;
                }

            var chunks = d.chunkMaterials != null && d.chunkMaterials.Length > 0 ? d.chunkMaterials : b0.chunkMaterials;
            var sp = MonolithSpawner.I;
            if (sp && chunks != null && chunks.Length > 0) sp.tierMaterials = chunks;
            var ss = ShardSystem.I;
            if (ss && sp)
            {
                var shards = d.shardMaterials != null && d.shardMaterials.Length > 0 ? d.shardMaterials : b0.shardMaterials;
                if (shards != null && shards.Length > 0) ss.shardMaterials = shards;
                var debris = new Material[sp.tierMaterials.Length + 1];
                sp.tierMaterials.CopyTo(debris, 0);
                debris[debris.Length - 1] = sp.goldenMaterial;
                ss.debrisMaterials = debris;
            }

            if (sun) { sun.color = d.sunColor; sun.intensity = d.sunIntensity; }
            if (volume) volume.sharedProfile = d.volumeProfile ? d.volumeProfile : b0.volumeProfile;
            int idx = IndexOf(d);
            if (rooms != null) for (int i = 0; i < rooms.Length; i++) if (rooms[i]) rooms[i].SetActive(i == idx);
            heat = 0;
            if (reflectionProbe)
            {
                // la sonda (tiempo real "OnEnable") guardaría la sala anterior: se pasa a bajo demanda
                reflectionProbe.realtimeMode = ProbeSettings.RealtimeMode.OnDemand;
                reflectionProbe.RequestRenderNextUpdate();
            }
        }

        void OnPrestiged()
        {
            pendingChoice = -1;
            var d = Current;
            if (!d) return;
            Apply(d);
            pendingBanner = $"{d.displayName.ToUpperInvariant()}\n<size=50%>{d.tagline}</size>"; // tras cerrar el modal de Pulido
        }

        void Update()
        {
            var gm = GameManager.I;
            if (!gm || biomes == null || biomes.Length == 0) return;
            if (subscribed != gm) { gm.Prestiged += OnPrestiged; subscribed = gm; }
            var d = Current;
            if (d.id != appliedId) Apply(d);
            if (pendingBanner != null && Hud.I && !Hud.I.BlocksGameplay) { Hud.I.Banner(pendingBanner); pendingBanner = null; }
            reflectCd -= Time.deltaTime;
            if (d.rule == BiomeRule.LavaFloor) TickLava(d, gm);
        }

        // =================== Reglas ===================
        void TickLava(BiomeDef d, GameManager gm)
        {
            var p = PlayerMarble.I;
            if (!p || (Hud.I && Hud.I.BlocksGameplay)) return;
            float dt = Time.deltaTime;
            if (p.Grounded && p.Speed < d.ruleA) heat += dt;
            else heat = Mathf.Max(0, heat - dt * 2f);

            Vector3 pos = p.transform.position;
            if (heat > d.ruleB * 0.4f && (sparkCd -= dt) <= 0)
            {
                sparkCd = Mathf.Lerp(0.25f, 0.06f, heat / d.ruleB);
                ShardSystem.I?.SpawnSparkle(pos + Vector3.down * 0.5f, 2, 0, 3f);
            }
            if (heat < d.ruleB) return;

            heat = 0;
            lavaBursts++;
            Vector2 r = UnityEngine.Random.insideUnitCircle.normalized * 5f;
            p.Launch(new Vector3(r.x, 13f, r.y));
            gm.BreakCombo();
            ShardSystem.I?.SpawnSparkle(pos, 24, 0, 9f);
            PowerSystem.I?.FlashAt(pos, new Color(1f, 0.45f, 0.1f), 1f);
            AudioFX.I?.Play(Sfx.Boom, 0.6f, 0.7f);
            CameraRig.I?.Shake(0.35f);
            if (!lavaHintShown)
            {
                lavaHintShown = true;
                Hud.I?.ShowToast("¡LAVA! Si te quedas quieta, el suelo te lanza y pierdes el combo", ToastKind.Info);
            }
        }

        /// <summary>Monolith.TakeDamage → golpe directo de la canica.</summary>
        public void OnPlayerHit(Monolith m, float dmg, Vector3 point)
        {
            var d = Current;
            if (!d || d.rule != BiomeRule.CrystalReflect || reflectCd > 0 || UnityEngine.Random.value >= d.ruleA) return;
            reflectCd = 0.2f;
            reflects++;
            PowerSystem.I?.ReflectBolt(m, point, dmg * d.ruleB);
        }

        /// <summary>Daño de metralla gratis de la sala (× daño de impacto); 0 = no hay.</summary>
        public float FreeShrapnel { get { var d = Current; return d && d.rule == BiomeRule.CrystalReflect ? d.ruleC : 0f; } }
    }
}
