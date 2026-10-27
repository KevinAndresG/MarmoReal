using System;
using System.Collections.Generic;
using UnityEngine;

namespace Marmoreal
{
    public enum ToastKind { Info, Gold, Gallery, Prestige, Divine }

    /// <summary>
    /// Corazón del incremental: estado, economía, combo, resonancia, guardado y prestigio.
    /// Vive en la escena (objeto "GameManager"). Todas las cifras se editan en EconomyConfig.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        static GameManager _i;
        /// <summary>Singleton resistente a recargas de dominio (se re-busca si se pierde).</summary>
        public static GameManager I { get { if (!_i) _i = FindFirstObjectByType<GameManager>(); return _i; } private set => _i = value; }

        const string SaveKey = "marmoreal.save.v1";

        public EconomyConfig config;
        [Tooltip("Segundos entre autoguardados")]
        public float autosaveInterval = 10f;
        [Tooltip("Intervalo de ataque de cada acólito (s)")]
        public float acolyteInterval = 1.1f;

        public GameState S { get; private set; }
        public string DebugState => $"I==this:{I == this} Iset:{(object)I != null} awakes:{awakeCount}";
        static int awakeCount;

        // ---- Estadísticas derivadas (cache) ----
        public float ImpactDamage { get; private set; }
        public float MaxSpeed { get; private set; }
        public float MagnetRadius { get; private set; }
        public float DustMult { get; private set; }
        public float CritChance { get; private set; }
        public int MaxMonoliths { get; private set; }
        public int AcolyteCount { get; private set; }
        public float AcolyteDamage { get; private set; }
        public float ResonanceMult { get; private set; }
        public float ShockwaveRadius { get; private set; }
        public float GlobalMult { get; private set; }
        public float StatueMult { get; private set; }
        public float OfflineEfficiency { get; private set; }
        public float DashCooldown { get; private set; }
        public float CritMult { get; private set; }
        public float GoldenChance { get; private set; }
        public float FameMult { get; private set; } = 1f;
        public float PowerMult { get; private set; } = 1f;
        public float PowerCooldownMult { get; private set; } = 1f;
        public int DashCharges { get; private set; } = 1;
        public int AllyCount { get; private set; }
        public float AllyFraction { get; private set; }
        public float ResonanceGain { get; private set; } = 1f;

        // ---- Buffs temporales (poderes y Musas) ----
        public float FrenzyMult => frenzyTimer > 0 ? frenzyMult : 1f;
        public float FrenzyTimeLeft => Mathf.Max(0, frenzyTimer);
        public bool FuryActive => furyTimer > 0;
        public float FuryTimeLeft => Mathf.Max(0, furyTimer);
        /// <summary>Onda Infinita (bendición): la Resonancia se rellena sola.</summary>
        public float OverflowTimeLeft => Mathf.Max(0, overflowTimer);
        [Tooltip("Multiplicador de daño de impacto temporal (lo pone el poder Coloso)")]
        [System.NonSerialized] public float colossusMult = 1f;
        float frenzyTimer, frenzyMult = 1f, furyTimer, overflowTimer, achievementTimer;

        // ---- Estado volátil ----
        public int Combo { get; private set; }
        public float ComboTimer { get; private set; }
        public void BreakCombo() => Combo = 0;
        public float ComboMult => 1f + Mathf.Min(Combo, config.comboMaxHits) * config.comboStep;
        public float Resonance { get; private set; }
        public double DustPerSecond { get; private set; }
        /// <summary>Ganancia offline pendiente de mostrar (la HUD la consume).</summary>
        public (double dust, double secs) PendingOffline { get; set; }
        /// <summary>Aviso de una migración al cargar (la HUD lo muestra en un modal).</summary>
        public string PendingNotice { get; set; }

        public event Action StatsChanged;
        public event Action<int> GalleryUp;
        public event Action Prestiged;
        public event Action<string, ToastKind> Toast;
        public event Action<double, double> OfflineEarned; // dust, seconds
        public event Action<AchievementDef> AchievementUnlocked;
        /// <summary>Cae un Coloso (true = Prueba de sala; gancho para la campaña).</summary>
        public event Action<bool> BossDefeated;
        public event Action<int> TrialPassed;   // campaignStage de la pieza conseguida
        public event Action CampaignWon;

        float saveTimer;
        readonly double[] dustWindow = new double[10];
        int windowIdx;
        float windowTimer;

        void Awake()
        {
            awakeCount++;
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            Application.runInBackground = true; // idle: los acólitos siguen tallando con la ventana en segundo plano
            config.EnsureDefaults(); // añade (en memoria) contenido nuevo si el asset es antiguo
            Load();
            if (S.contracts.Count == 0) RollContracts();
            RecalcStats();
        }

        void Start()
        {
            ApplyOffline();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            S.playSeconds += dt;

            if (Combo > 0)
            {
                ComboTimer -= dt;
                if (ComboTimer <= 0) Combo = 0;
            }

            windowTimer += dt;
            if (windowTimer >= 0.5f)
            {
                windowTimer -= 0.5f;
                double sum = 0;
                for (int i = 0; i < dustWindow.Length; i++) sum += dustWindow[i];
                DustPerSecond = sum / (dustWindow.Length * 0.5);
                windowIdx = (windowIdx + 1) % dustWindow.Length;
                dustWindow[windowIdx] = 0;
            }

            frenzyTimer -= dt;
            furyTimer -= dt;
            overflowTimer -= dt;
            if (overflowTimer > 0) Resonance = 1f;

            achievementTimer -= dt;
            if (achievementTimer <= 0) { achievementTimer = 1f; CheckAchievements(); CheckHitos(); CheckColors(); UpdateRecords(); }
            if (S.infinite) S.infSeconds += dt;

            saveTimer += dt;
            if (saveTimer >= autosaveInterval) { saveTimer = 0; Save(); }
        }

        void OnApplicationPause(bool pause) { if (pause) Save(); }
        void OnApplicationQuit() { Save(); }

        // =================== Economía ===================

        public int Level(string id) => S.Level(id);

        public float Value(string id)
        {
            var d = config.Get(id);
            return d == null ? 0f : d.Value(S.Level(id));
        }

        /// <summary>Tope actual: el base + lo que suma cada Pulido (hasta maxLevelLimit).</summary>
        public int MaxLevelOf(UpgradeDef d) => MaxLevelAt(d, S.prestigeCount);

        public static int MaxLevelAt(UpgradeDef d, int prestiges)
        {
            if (d.currency != Currency.Dust || d.maxLevelPerPrestige <= 0 || d.maxLevelLimit <= 0) return d.maxLevel;
            return Mathf.Max(d.maxLevel, Mathf.Min(d.maxLevelLimit, d.maxLevel + Mathf.FloorToInt(d.maxLevelPerPrestige * prestiges + 1e-4f)));
        }

        public bool IsMaxed(UpgradeDef d) => S.Level(d.id) >= MaxLevelOf(d);

        /// <summary>Factor de coste del Taller: (Brillo×Obra×Fama)^k × crecimiento^pulidos. Mantiene el ritmo de compra pese al poder permanente.</summary>
        public double WorkshopCostFactor(UpgradeDef d)
        {
            if (d.currency != Currency.Dust || !d.scaleCost || !config.workshopCostScale) return 1.0;
            double power = Math.Max(1.0, (double)GlobalMult * FameMult);
            return Math.Pow(power, config.workshopCostK) * Math.Pow(config.workshopCostPerPrestige, config.consecrateResetsCostGrowth ? PrestigesSinceConsecrate : S.prestigeCount);
        }

        public double CostOf(UpgradeDef d) => Math.Ceiling(d.Cost(S.Level(d.id)) * WorkshopCostFactor(d));

        public double RunStat(string stat)
        {
            switch (stat)
            {
                case "runGalleries": return Math.Max(0, S.galleryLevel - S.lifeStartGallery);
                case "runVolatile": return S.runVolatile;
                default: return 0;
            }
        }

        public bool MilestoneMet(UpgradeDef d) => string.IsNullOrEmpty(d.unlockStat) || RunStat(d.unlockStat) >= d.unlockThreshold;
        public double Balance(Currency c) => c == Currency.Dust ? S.dust : c == Currency.Essence ? S.essence : S.icor;
        public bool CanAfford(UpgradeDef d) => !IsMaxed(d) && IsUnlocked(d) && Balance(d.currency) >= CostOf(d);
        public bool IsUnlocked(UpgradeDef d)
        {
            // ponytail: el Taller ya no se esconde por galería/hito; siempre visible (petición de diseño S10)
            if (d.requires == null) return true;
            foreach (var r in d.requires) if (S.Level(r) <= 0) return false;
            return true;
        }

        /// <summary>¿Tiene desbloqueada la mecánica/nodo?</summary>
        public bool Has(string id) => S.Level(id) > 0;

        /// <summary>¿El mutador de esta vida anula esta mejora? (sin Acólitos, poder sellado…). La HUD la oculta.</summary>
        public bool MutatorBlocks(UpgradeDef d)
        {
            var mu = Mutator;
            if (mu == null || d == null) return false;
            if (mu.acolyteMult <= 0f && d.id == "acolitos") return true;
            if (mu.allyMult <= 0f && d.id == "gemelas") return true;
            if (string.IsNullOrEmpty(mu.disabledPower)) return false;
            return d.id == mu.disabledPower || d.id == "pw_" + mu.disabledPower;
        }

        public bool TryBuy(UpgradeDef d)
        {
            if (!CanAfford(d)) return false;
            double cost = CostOf(d);
            if (d.currency == Currency.Dust) S.dust -= cost;
            else if (d.currency == Currency.Essence) S.essence -= cost;
            else S.icor -= cost;
            S.SetLevel(d.id, S.Level(d.id) + 1);
            RecalcStats();
            AudioFX.I?.Play(Sfx.Buy);
            return true;
        }

        public void RecalcStats()
        {
            GlobalMult = Value("brillo") * Value("obra");
            ImpactDamage = Value("masa") * Value("fuerza") * GlobalMult;
            MaxSpeed = Value("inercia");
            MagnetRadius = Value("iman");
            FameMult = 1f + config.famePerAchievement * S.achievements.Count;
            DustMult = Value("pureza") * GlobalMult * FameMult;
            PowerMult = Value("ira") * (1f + config.relicPowerBonus * S.relics.Count);
            PowerCooldownMult = Mathf.Max(0.3f, Value("eternidad") <= 0 ? 1f : Value("eternidad"));
            CritChance = Mathf.Clamp01(Value("fortuna"));
            MaxMonoliths = Mathf.RoundToInt(Value("cantera"));
            AcolyteCount = Mathf.RoundToInt(Value("acolitos") + Value("eterno"));
            AcolyteDamage = Value("cincel") * Value("coro") * GlobalMult;
            ResonanceMult = Value("resonancia");
            ShockwaveRadius = config.shockwaveBaseRadius + S.Level("resonancia") * 0.45f;
            StatueMult = Value("veta");
            OfflineEfficiency = Mathf.Clamp01(Value("paciencia"));
            DashCooldown = Mathf.Max(0.6f, Value("impulso"));
            CritMult = config.critMult + Value("ojo");
            GoldenChance = config.goldenChance + Value("fiebre");
            DashCharges = Mathf.Max(1, Mathf.RoundToInt(Value("cargas")));
            AllyCount = Mathf.RoundToInt(Value("gemelas"));
            AllyFraction = Value("vinculo");
            ResonanceGain = Has("perpetua") ? Mathf.Max(1f, Value("perpetua")) : 1f;
            var mu = Mutator;
            if (mu != null)
            {
                DustMult *= mu.dustMult;
                MagnetRadius *= mu.magnetMult;
                AcolyteCount = Mathf.RoundToInt(AcolyteCount * mu.acolyteMult);
                AllyCount = Mathf.RoundToInt(AllyCount * mu.allyMult);
                PowerCooldownMult *= mu.powerCooldownMult;
                if (mu.alwaysCrit) CritChance = 1f;
            }
            StatsChanged?.Invoke();
        }

        /// <summary>Polvo recogido en partida (aplica Frenesí).</summary>
        public void CollectDust(double v) => AddDust(v * FrenzyMult);

        public void AddDust(double v)
        {
            if (v <= 0) return;
            S.dust += v;
            S.runDust += v;
            S.lifetimeDust += v;
            dustWindow[windowIdx] += v;
        }

        // =================== Monolitos ===================

        public double MonolithHP(bool large, bool golden)
        {
            double hp = config.monolithBaseHP * Math.Pow(config.hpGrowthPerGallery, S.galleryLevel);
            if (large) hp *= config.largeBlockHPMult;
            if (golden) hp *= config.goldenHPMult;
            var b = BiomeManager.I ? BiomeManager.I.Current : null;
            if (b) hp *= b.hpMult;
            var mu = Mutator;
            if (mu != null) hp *= mu.hpMult;
            if (S.infinite) hp *= 1 + config.infiniteHpPerLife * S.infiniteLives;
            return hp;
        }

        /// <summary>Polvo base de un bloque pequeño en la galería actual (ya con multiplicadores).</summary>
        public double BaseBlockReward => config.blockBaseReward * DustMult * Math.Pow(config.rewardGrowthPerGallery, S.galleryLevel)
            * (BiomeManager.I && BiomeManager.I.Current ? BiomeManager.I.Current.dustMult : 1f);

        /// <summary>Polvo total que suelta el bloque al tallarse entero (sin la estatua).</summary>
        public double BlockReward(bool large, bool golden)
        {
            double r = BaseBlockReward;
            if (large) r *= config.largeBlockHPMult;
            if (golden) r *= config.goldenRewardMult * StatueMult;
            return r;
        }

        public double StatueReward(double blockReward, bool golden)
            => blockReward * config.statueBonus * StatueMult * (golden ? 2 : 1);

        public int StatuesNeeded => Mathf.Min(config.statuesPerGalleryMax,
            config.statuesPerGalleryBase + config.statuesPerGalleryStep * S.galleryLevel);

        public void OnVolatileBroken() { S.runVolatile++; }

        public void OnStatueFreed(bool golden, bool transmuted = false)
        {
            S.totalStatues++;
            S.lifeStatues++;
            if (golden) S.lifeGolden++;
            if (golden && !transmuted)
            {
                S.goldenFound++;
                if (UnityEngine.Random.value < config.icorGoldenChance) AddIcor(config.icorGolden, "bloque dorado");
            }
            else if (UnityEngine.Random.value < config.icorStatueChance) AddIcor(1, "estatua");
            S.statuesThisGallery++;
            if (BossDue || TrialDue) S.statuesThisGallery = Mathf.Min(S.statuesThisGallery, StatuesNeeded - 1); // el Coloso cierra la galería
            if (S.statuesThisGallery >= StatuesNeeded)
            {
                S.statuesThisGallery = 0;
                S.galleryLevel++;
                S.maxGallery = Mathf.Max(S.maxGallery, S.galleryLevel + 1);
                if (S.galleryLevel == config.musaUnlockGallery && S.musasCaught == 0)
                    Toast?.Invoke("Las MUSAS empiezan a visitar el santuario: toca sus orbes de luz", ToastKind.Divine);
                AudioFX.I?.Play(Sfx.GalleryUp);
                Toast?.Invoke($"GALERÍA {Num.Roman(S.galleryLevel + 1)} — el mármol se endurece, el polvo vale más", ToastKind.Gallery);
                if (S.infinite) AddEternity(config.eternityPerGallery);
                GalleryUp?.Invoke(S.galleryLevel);
                if (S.galleryLevel == config.prestigeMinGallery && S.prestigeCount == 0)
                    Toast?.Invoke("Puedes PULIR la canica: reinicia a cambio de Esencia eterna", ToastKind.Prestige);
                foreach (var d in config.upgrades)
                    if (d.currency == Currency.Dust && d.unlockGallery == S.galleryLevel && S.prestigeCount == 0)
                        Toast?.Invoke($"Nueva mejora: {d.displayName}", ToastKind.Info);
                StatsChanged?.Invoke();
            }
        }

        // =================== Impactos / combo / resonancia ===================

        public float ImpactDamageFor(float speed, bool dashing, out bool crit)
        {
            crit = false;
            if (speed < config.minImpactSpeed) return 0f;
            float k = speed / config.referenceSpeed;
            float dmg = ImpactDamage * k * k * ComboMult;
            if (dashing) dmg *= config.dashDamageMult;
            dmg *= colossusMult;
            if (FuryActive || UnityEngine.Random.value < CritChance) { crit = true; dmg *= CritMult; }
            return dmg;
        }

        public float AcolyteHitDamage(out bool crit)
        {
            crit = UnityEngine.Random.value < CritChance * 0.5f;
            return AcolyteDamage * (crit ? CritMult : 1f);
        }

        /// <summary>Embestida de una Discípula: tu daño de impacto (sin combo) × Vínculo.</summary>
        public float AllyHitDamage(float speed, float fraction, out bool crit)
        {
            crit = false;
            if (speed < config.minImpactSpeed) return 0f;
            float k = speed / config.referenceSpeed;
            float dmg = ImpactDamage * k * k * fraction;
            if (FuryActive || UnityEngine.Random.value < CritChance) { crit = true; dmg *= CritMult; }
            return dmg;
        }

        public float ShockwaveDamage => ImpactDamage * config.shockwaveDamageMult * ResonanceMult * ComboMult;

        public void RegisterImpact(double damage)
        {
            Combo++;
            if (Combo > S.bestCombo) S.bestCombo = Combo;
            ComboTimer = config.comboWindow;
            AddResonance(config.resonancePerImpact);
            if (damage > S.bestHit) S.bestHit = damage;
            if (S.infinite && damage > S.infBestHit) S.infBestHit = damage;
        }

        public void AddResonance(float v) => Resonance = Mathf.Clamp01(Resonance + v * ResonanceGain);
        public bool ConsumeResonance()
        {
            if (Resonance < 1f) return false;
            Resonance = 0f;
            return true;
        }

        // =================== Prestigio ===================

        public bool CanPrestige => S.galleryLevel >= config.prestigeMinGallery && PrestigeGain >= 1;

        public double PrestigeGain => S.galleryLevel < config.prestigeMinGallery ? 0 :
            Math.Floor(config.prestigeBase * Math.Pow(S.galleryLevel - config.prestigeMinGallery + 3, config.prestigePow) * ConsecrateMult);

        /// <summary>Esencia ×(1 + bonus × Consagraciones): lo que hace rentable volver a empezar el Árbol.</summary>
        public double ConsecrateMult => 1.0 + config.consecrateEssenceBonus * S.consecrations;

        /// <summary>Esencia que daría pulir en la siguiente galería (para motivar a seguir un poco más).</summary>
        public double PrestigeGainNext =>
            Math.Floor(config.prestigeBase * Math.Pow(Math.Max(0, S.galleryLevel + 1 - config.prestigeMinGallery) + 3, config.prestigePow) * ConsecrateMult);

        /// <summary>Texto "qué se abre ahora" del último Pulido (lo muestra la HUD).</summary>
        public string PrestigeReport { get; private set; } = "";

        string BuildReport(int oldP, int newP, double gain)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("VIDA ANTERIOR");
            sb.AppendLine($"{Num.Time(S.lastLifeSeconds)} · {S.lastLifeStatues} estatuas · hasta Galería {Num.Roman(S.lastLifeGalleries)} · {Num.F(S.lastLifeDust)} polvo · mejor golpe {Num.F(S.lastLifeBestHit)}");
            sb.AppendLine($"Esencia +{Num.F(gain)}");
            if (lastMutatorLine.Length > 0) sb.AppendLine(lastMutatorLine);
            if (lastContractsLine.Length > 0) sb.AppendLine(lastContractsLine);
            sb.AppendLine();
            var room = BiomeManager.I ? BiomeManager.I.Current : null;
            if (room) { sb.AppendLine($"SALA: {room.displayName.ToUpperInvariant()}"); sb.AppendLine(room.tagline); sb.AppendLine(); }
            sb.AppendLine("QUÉ SE ABRE AHORA");
            bool any = false;
            foreach (var d in config.upgrades)
            {
                if (d.currency != Currency.Dust) continue;
                int a = MaxLevelAt(d, oldP), b = MaxLevelAt(d, newP);
                if (b > a) { sb.AppendLine($"· {d.displayName}: tope {a} → {b}"); any = true; }
            }
            if (!any) sb.AppendLine("· (nada nuevo esta vez)");
            return sb.ToString().TrimEnd();
        }

        /// <summary>Pulir: el único reinicio (Polvo, Taller, Galerías). Cada consecrateMinPrestiges Pulidos trae además una Reliquia.</summary>
        public void DoPrestige()
        {
            if (!CanPrestige) return;
            double gain = PrestigeGain;
            var mu = Mutator;
            double mBonus = MutatorBonus(gain);
            lastMutatorLine = mu == null || mu.safe || mu.goalGalleries <= 0 ? "" :
                $"Mutador {mu.displayName}: {(mBonus > 0 ? $"objetivo CUMPLIDO, +{Num.F(mBonus)} Esencia" : $"objetivo no cumplido ({LifeGalleries}/{mu.goalGalleries} galerías)")}";
            lastContractsLine = S.contracts.Count == 0 || !ContractsOpen ? "" : $"Contratos cumplidos: {S.contractsDone.Count}/{S.contracts.Count}";
            gain += mBonus;
            // Resumen de la vida que termina
            S.lastLifeSeconds = S.playSeconds - S.lifeStartSeconds;
            S.lastLifeStatues = S.lifeStatues;
            S.lastLifeGalleries = S.galleryLevel + 1;
            S.lastLifeDust = S.runDust;
            S.lastLifeBestHit = S.bestHit;
            int oldP = S.prestigeCount;
            S.essence += gain;
            S.lifetimeEssence += gain;
            S.prestigeCount++;
            if (S.infinite) S.infiniteLives++;
            string newRelic = S.prestigeCount - S.relicPrestige >= Math.Max(1, config.consecrateMinPrestiges) ? GrantRelic() : null;
            var biome = BiomeManager.I ? BiomeManager.I.NextFor(S.prestigeCount) : null;
            if (biome) S.biomeId = biome.id;
            AddIcor(config.icorPrestigeBase + Math.Floor(gain / config.icorPrestigeDivisor), "pulido");
            S.dust = 0;
            S.runDust = 0;
            S.statuesThisGallery = 0;
            foreach (var d in config.upgrades)
                if (d.currency == Currency.Dust) S.SetLevel(d.id, 0);
            S.galleryLevel = Mathf.RoundToInt(Value("memoria"));
            S.lifeStartGallery = S.galleryLevel;
            S.runVolatile = 0;
            S.lifeStatues = 0;
            S.lifeStartSeconds = S.playSeconds;
            S.hitosSeen.Clear();
            S.bossDoneGallery = -1;
            S.lifeBosses = 0;
            S.lifeGolden = 0;
            RollContracts();
            RollMutatorOffer();
            Combo = 0;
            Resonance = 0;
            RecalcStats();
            PrestigeReport = BuildReport(oldP, S.prestigeCount, gain);
            if (newRelic != null)
                PrestigeReport = $"★ RELIQUIA NUEVA (hito {S.consecrations}): {newRelic}\n" +
                    $"Ranuras: {S.relicsEquipped.Count}/{RelicSlots} equipadas · RELIQUIAS [R] · Esencia al Pulir ×{ConsecrateMult:0.##}\n\n" + PrestigeReport;
            Save();
            AudioFX.I?.Play(Sfx.Prestige);
            Prestiged?.Invoke();
            Toast?.Invoke($"Canica pulida · +{Num.F(gain)} Esencia" + (newRelic != null ? " · ¡Punto de Reliquia!" : ""), ToastKind.Prestige);
        }

        // =================== Consagrar y Reliquias (S5) ===================

        public int PrestigesSinceConsecrate => S.prestigeCount - S.prestigesAtConsecrate;
        /// <summary>Pulidos que faltan para el siguiente hito de Reliquia.</summary>
        public int PrestigesToRelic => Math.Max(0, config.consecrateMinPrestiges - (S.prestigeCount - S.relicPrestige));
        public int RelicSlots => config.relicSlotsBase + S.relicSlotsBought;
        public int NextSlotCost => S.relicSlotsBought < config.relicSlotCosts.Length ? config.relicSlotCosts[S.relicSlotsBought] : -1;
        public bool RelicOn(string id) => S.relicsEquipped.Contains(id);

        // Hito de Reliquia (S8): lo llama DoPrestige tras sumar el Pulido. No reinicia nada; prestigesAtConsecrate queda
        // congelado en la última Consagración antigua para no encarecer el Taller de quien ya perdió su Árbol.
        string GrantRelic()
        {
            S.consecrations++;
            S.relicPrestige = S.prestigeCount;
            if (S.relics.Count + S.relicTokens >= config.relics.Count) { AddIcor(config.trialIcor, "reliquia repetida"); return "(todas conseguidas: +Ícor)"; }
            S.relicTokens++;
            return "1 Punto de Reliquia: elige cuál desbloquear";
        }

        /// <summary>Gasta 1 Punto de Reliquia en desbloquear la reliquia (S9: se elige a mano).</summary>
        public bool UnlockRelic(string id)
        {
            if (S.relicTokens <= 0 || S.relics.Contains(id) || config.relics.Find(r => r.id == id) == null) return false;
            S.relicTokens--;
            S.relics.Add(id);
            if (S.relicsEquipped.Count < RelicSlots) S.relicsEquipped.Add(id);
            RecalcStats();
            Save();
            return true;
        }

        /// <summary>Devuelve la reliquia: recuperas el Punto para gastarlo en otra (gratis; el respec es libre).</summary>
        public bool RefundRelic(string id)
        {
            if (!S.relics.Remove(id)) return false;
            S.relicsEquipped.Remove(id);
            S.relicTokens++;
            RecalcStats();
            Save();
            return true;
        }

        public void ToggleRelic(string id)
        {
            if (!S.relics.Contains(id)) return;
            if (S.relicsEquipped.Remove(id)) { Save(); return; }
            if (S.relicsEquipped.Count >= RelicSlots) { Toast?.Invoke($"Ranuras llenas ({RelicSlots}): quita una reliquia primero", ToastKind.Info); return; }
            S.relicsEquipped.Add(id);
            Save();
        }

        public bool BuyRelicSlot()
        {
            int c = NextSlotCost;
            if (c < 0 || S.eternity < c) return false;
            S.eternity -= c;
            S.relicSlotsBought++;
            Save();
            return true;
        }

        public bool BuyColor(MarbleColorDef c)
        {
            if (c == null || c.unlock != ColorUnlock.Fragments || S.colors.Contains(c.id) || S.eternity < c.threshold) return false;
            S.eternity -= c.threshold;
            S.colors.Add(c.id);
            S.marbleColor = c.id;
            Save();
            return true;
        }

        public void AddEternity(double n)
        {
            if (n <= 0) return;
            S.eternity += n;
            S.lifetimeEternity += n;
        }

        public BossModDef RollBossMod() => S.infinite && config.bossMods.Count > 0 ? config.bossMods[UnityEngine.Random.Range(0, config.bossMods.Count)] : null;

        // =================== Mutadores ===================

        string lastMutatorLine = "", lastContractsLine = "";
        public MutatorDef Mutator => config.GetMutator(S.mutatorId);
        public int LifeGalleries => Math.Max(0, S.galleryLevel - S.lifeStartGallery);
        public double MutatorBonus(double gain)
        {
            var mu = Mutator;
            return mu != null && mu.essenceBonus > 0 && mu.goalGalleries > 0 && LifeGalleries >= mu.goalGalleries
                ? Math.Max(1, Math.Floor(gain * mu.essenceBonus)) : 0;
        }

        /// <summary>Línea de estado del mutador activo ("" si no hay o es la carta segura).</summary>
        public string MutatorStatus()
        {
            var mu = Mutator;
            if (mu == null || mu.safe) return "";
            return mu.essenceBonus > 0 && mu.goalGalleries > 0
                ? $"{mu.displayName} · {Math.Min(LifeGalleries, mu.goalGalleries)}/{mu.goalGalleries} galerías → +{mu.essenceBonus * 100:0}% Esencia"
                : mu.displayName;
        }

        /// <summary>3 cartas: la segura + 2 al azar. Por defecto queda la segura hasta que elijas otra.</summary>
        void RollMutatorOffer()
        {
            S.mutatorOffer.Clear();
            var safe = S.infinite ? null : config.mutators.Find(m => m.safe); // infinito: sin carta segura
            if (safe != null) S.mutatorOffer.Add(safe.id);
            var pool = config.mutators.FindAll(m => !m.safe);
            while (S.mutatorOffer.Count < 3 && pool.Count > 0)
            {
                int i = UnityEngine.Random.Range(0, pool.Count);
                S.mutatorOffer.Add(pool[i].id);
                pool.RemoveAt(i);
            }
            S.mutatorId = safe != null ? safe.id : S.mutatorOffer.Count > 0 ? S.mutatorOffer[0] : "";
        }

        public void ChooseMutator(string id)
        {
            if (!S.mutatorOffer.Contains(id)) return;
            S.mutatorId = id;
            RecalcStats();
            Save();
        }

        // =================== Jefes (Coloso) ===================

        /// <summary>¿Hay un Coloso pendiente en esta galería? Cierra la galería hasta que caiga.</summary>
        public bool BossDue => config.bossEveryGalleries > 0 && (S.galleryLevel + 1) % config.bossEveryGalleries == 0
            && S.bossDoneGallery != S.galleryLevel && MonolithSpawner.I && MonolithSpawner.I.bossPrefab;
        public int BossTier => Mathf.Max(1, (S.galleryLevel + 1) / Mathf.Max(1, config.bossEveryGalleries));
        public double BossHP => MonolithHP(false, false) * config.bossHPMult;
        public double BossReward => BaseBlockReward * config.bossDustMult;

        public void OnBossDefeated(bool trial, BossModDef mod = null)
        {
            if (S.infinite && !trial)
            {
                double f = config.eternityPerBoss * (mod != null ? Math.Max(1, mod.fragmentMult) : 1);
                AddEternity(f);
                Toast?.Invoke($"+{f} Fragmentos de Eternidad", ToastKind.Divine);
            }
            var tb = trial ? TrialBiome : null;
            S.bossDoneGallery = S.galleryLevel;
            S.lifeBosses++;
            S.statuesThisGallery = Mathf.Max(S.statuesThisGallery, StatuesNeeded - 1); // su estatua cierra la galería
            AddIcor(config.bossIcor + config.bossIcorPerTier * (BossTier - 1), "Coloso");
            Toast?.Invoke(tb ? "¡PRUEBA SUPERADA! La galería se abre" : "¡COLOSO derribado! La galería se abre", ToastKind.Gallery);
            BossDefeated?.Invoke(trial);
            if (tb) CompleteTrial(tb.campaignStage);
        }

        // =================== Campaña: El Gran Monumento ===================

        /// <summary>Sala actual si su Prueba está pendiente (tiene trialGallery y su pieza no se ha conseguido).</summary>
        public BiomeDef TrialBiome
        {
            get
            {
                var b = BiomeManager.I ? BiomeManager.I.Current : null;
                return b && b.trialGallery > 0 && b.campaignStage < config.campaign.Count && !S.pieces.Contains(b.campaignStage) ? b : null;
            }
        }
        /// <summary>¿Toca el jefe de la Prueba? Cierra la galería hasta que caiga (como el Coloso).</summary>
        public bool TrialDue { get { var b = TrialBiome; return b && S.galleryLevel + 1 >= b.trialGallery && MonolithSpawner.I && MonolithSpawner.I.bossPrefab; } }
        public double TrialHP => BossHP * config.trialHPMult;
        public int PiecesNeeded => Mathf.Clamp(config.campaignPiecesRequired, 1, Mathf.Max(1, config.campaign.Count));

        public void CompleteTrial(int stage)
        {
            if (S.pieces.Contains(stage)) return;
            S.pieces.Add(stage);
            var piece = stage < config.campaign.Count ? config.campaign[stage] : null;
            AddIcor(config.trialIcor, "Prueba");
            Toast?.Invoke($"PIEZA DEL MONUMENTO: {piece?.displayName?.ToUpperInvariant()}  ({S.pieces.Count}/{PiecesNeeded})", ToastKind.Divine);
            var next = BiomeManager.I ? BiomeManager.I.ByStage(stage + 1) : null;
            if (next) Toast?.Invoke($"Nueva sala desbloqueada: {next.displayName}. Llévala al Pulir", ToastKind.Prestige);
            CheckColors();
            TrialPassed?.Invoke(stage);
            if (!S.campaignWon && S.pieces.Count >= PiecesNeeded)
            {
                S.campaignWon = true;
                S.campaignSeconds = S.playSeconds;
                CheckColors();
                CampaignWon?.Invoke();
            }
            Save();
        }

        public void EnterInfinite()
        {
            if (!S.campaignWon) return;
            S.infinite = true;
            S.victoryShown = true;
            Toast?.Invoke("MODO INFINITO: las salas rotan, los mutadores no perdonan. Persigue tus récords", ToastKind.Prestige);
            Save();
        }

        public MarbleColorDef CurrentColor => config.GetColor(S.marbleColor) ?? (config.marbleColors.Count > 0 ? config.marbleColors[0] : null);

        public void SelectColor(string id)
        {
            if (!S.colors.Contains(id)) return;
            S.marbleColor = id;
            Save();
        }

        public bool ColorEarned(MarbleColorDef c) => c.unlock switch
        {
            ColorUnlock.Start => true,
            ColorUnlock.Prestiges => S.prestigeCount >= c.threshold,
            ColorUnlock.Trial => S.pieces.Contains(c.threshold - 1),
            ColorUnlock.Campaign => S.campaignWon,
            ColorUnlock.InfiniteGallery => S.infBestGallery >= c.threshold,
            ColorUnlock.Fragments => false, // se compra (BuyColor)
            _ => false,
        };

        void CheckColors()
        {
            foreach (var c in config.marbleColors)
            {
                if (S.colors.Contains(c.id) || !ColorEarned(c)) continue;
                S.colors.Add(c.id);
                if (c.unlock == ColorUnlock.Start) continue;
                S.marbleColor = c.id; // estrenar la nueva canica
                Toast?.Invoke($"Nueva canica: {c.displayName}. Cámbiala en COLECCIÓN [C]", ToastKind.Divine);
            }
        }

        void UpdateRecords()
        {
            if (!S.infinite) return;
            S.infBestGallery = Mathf.Max(S.infBestGallery, S.galleryLevel + 1);
            if (DustPerSecond > S.infBestDps) S.infBestDps = DustPerSecond;
        }

        public string Rank
        {
            get
            {
                string r = config.rankNames.Length > 0 ? config.rankNames[0] : "";
                for (int i = 0; i < config.rankNames.Length && i < config.rankGalleries.Length; i++)
                    if (S.infBestGallery >= config.rankGalleries[i]) r = config.rankNames[i];
                return r;
            }
        }

        // =================== Contratos ===================

        public bool ContractsOpen => S.prestigeCount >= config.contractsUnlockPrestige;
        public string ContractText(ContractDef c) => string.Format(c.description, c.target);

        void RollContracts()
        {
            S.contracts.Clear();
            S.contractsDone.Clear();
            int powers = 0;
            foreach (var p in config.powers) if (Has(p.id)) powers++;
            var pool = config.contracts.FindAll(c => c.kind != ContractKind.PowerBurst || powers >= c.target);
            while (S.contracts.Count < config.contractsPerLife && pool.Count > 0)
            {
                int i = UnityEngine.Random.Range(0, pool.Count);
                S.contracts.Add(pool[i].id);
                pool.RemoveAt(i);
            }
        }

        public void CompleteContract(ContractDef c)
        {
            if (c == null || S.contractsDone.Contains(c.id)) return;
            S.contractsDone.Add(c.id);
            string reward = "";
            if (c.rewardEssencePct > 0)
            {
                double e = Math.Max(1, Math.Floor(Math.Max(PrestigeGain, PrestigeGainNext) * c.rewardEssencePct));
                S.essence += e;
                S.lifetimeEssence += e;
                reward = $"+{Num.F(e)} Esencia";
            }
            if (c.rewardIcor > 0) { S.icor += c.rewardIcor; S.lifetimeIcor += c.rewardIcor; reward += (reward.Length > 0 ? ", " : "+") + $"{c.rewardIcor} Ícor"; }
            AudioFX.I?.Play(Sfx.GalleryUp, 0.6f, 1.2f);
            Toast?.Invoke($"CONTRATO cumplido: {ContractText(c)} — {reward}", ToastKind.Divine);
            StatsChanged?.Invoke();
        }

        // =================== Poderes / Ícor / buffs ===================

        /// <summary>Daño de un poder: daño de impacto actual × potencia × mejora del Taller (pw_id) × Ira Divina.</summary>
        public float PowerDamage(string id, float potency) => ImpactDamage * potency * PowerUpgrade(id, 1f) * PowerMult;

        /// <summary>Valor de la mejora de Taller del poder (id "pw_" + poder). Se pierde al Pulir, como todo el Taller.</summary>
        public float PowerUpgrade(string powerId, float fallback)
        {
            var d = config.Get("pw_" + powerId);
            return d == null ? fallback : d.Value(S.Level(d.id));
        }

        public void AddIcor(double n, string reason)
        {
            if (n <= 0) return;
            S.icor += n;
            S.lifetimeIcor += n;
            AudioFX.I?.Play(Sfx.Golden, 0.5f, 1.4f);
            Toast?.Invoke($"+{Num.F(n)} ÍCOR ({reason})" + (S.lifetimeIcor <= n ? " — abre el ALTAR [G]" : ""), ToastKind.Divine);
            StatsChanged?.Invoke();
        }

        public void StartFrenzy(float mult, float seconds)
        {
            if (frenzyTimer > 0 && frenzyMult > mult) { frenzyTimer = Mathf.Max(frenzyTimer, seconds); return; }
            frenzyMult = mult;
            frenzyTimer = Mathf.Max(frenzyTimer, seconds);
        }

        public void StartFury(float seconds) => furyTimer = Mathf.Max(furyTimer, seconds);
        public void StartOverflow(float seconds) { overflowTimer = Mathf.Max(overflowTimer, seconds); Resonance = 1f; }
        public void FillResonance() => Resonance = 1f;

        public void RegisterPowerUse() => S.powersUsed++;
        public void RegisterMusa() { S.musasCaught++; }

        // =================== Hitos de ayudantes ===================

        void CheckHitos()
        {
            foreach (var d in config.upgrades)
            {
                if (d.currency != Currency.Dust || string.IsNullOrEmpty(d.unlockStat) || !MilestoneMet(d)) continue;
                string key = S.prestigeCount + ":" + d.id;
                if (S.hitosSeen.Contains(key)) continue;
                S.hitosSeen.Add(key);
                Toast?.Invoke($"Hito cumplido: ya puedes comprar {d.displayName}", ToastKind.Gallery);
                StatsChanged?.Invoke();
            }
        }

        // =================== Mecenazgos ===================

        public double StatValue(string stat)
        {
            switch (stat)
            {
                case "statues": return S.totalStatues;
                case "golden": return S.goldenFound;
                case "gallery": return Math.Max(S.maxGallery, S.galleryLevel + 1);
                case "prestige": return S.prestigeCount;
                case "combo": return S.bestCombo;
                case "powers": return S.powersUsed;
                case "musas": return S.musasCaught;
                case "dust": return S.lifetimeDust;
                case "icor": return S.lifetimeIcor;
                case "pieces": return S.pieces.Count;
                case "campaign": return S.campaignWon ? 1 : 0;
                case "consecrations": return S.consecrations;
                default: return 0;
            }
        }

        public bool HasAchievement(string id) => S.achievements.Contains(id);

        void CheckAchievements()
        {
            bool any = false;
            foreach (var a in config.achievements)
            {
                if (S.achievements.Contains(a.id) || StatValue(a.stat) < a.threshold) continue;
                S.achievements.Add(a.id);
                any = true;
                Toast?.Invoke($"MECENAZGO: {a.displayName} — +{a.rewardIcor} Ícor, +{config.famePerAchievement * 100:0}% Fama", ToastKind.Divine);
                S.icor += a.rewardIcor;
                S.lifetimeIcor += a.rewardIcor;
                AchievementUnlocked?.Invoke(a);
            }
            if (any)
            {
                AudioFX.I?.Play(Sfx.GalleryUp, 0.6f, 1.3f);
                RecalcStats();
            }
        }

        // =================== Guardado / Offline ===================

        public void Save()
        {
            if (S == null) return;
            S.lastSaveUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(S));
            PlayerPrefs.Save();
        }

        void Load()
        {
            S = null;
            string json = PlayerPrefs.GetString(SaveKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { S = JsonUtility.FromJson<GameState>(json); }
                catch (Exception e) { Debug.LogWarning("[Marmoreal] Guardado corrupto, empezando de cero: " + e.Message); }
            }
            S ??= new GameState();
            if (S.version < 2) { S.legacyStage = S.prestigeCount; S.version = 2; } // conserva las salas ya visitadas
            if (S.relicPrestige < S.prestigesAtConsecrate) S.relicPrestige = S.prestigesAtConsecrate; // S8: hitos desde la última Consagración
            MigrateConsecrateRefund();
        }

        /// <summary>S9, una sola vez: la Consagración antigua (S5-S7) borraba Esencia y Árbol. Devuelve como Esencia lo ganado y perdido
        /// (ganado en total − Esencia actual − coste de los niveles del Árbol que aún tienes). Corrección de un fallo de diseño ya retirado.</summary>
        void MigrateConsecrateRefund()
        {
            if (S.consecrateRefundDone) return;
            S.consecrateRefundDone = true;
            if (S.prestigesAtConsecrate <= 0) return; // nunca consagró con la lógica antigua
            double spent = 0;
            foreach (var d in config.upgrades)
                if (d.currency == Currency.Essence)
                    for (int l = 0; l < S.Level(d.id); l++) spent += d.Cost(l);
            double lost = Math.Floor(S.lifetimeEssence - S.essence - spent);
            if (lost < 1) return;
            S.essence += lost;
            PendingNotice = $"La Consagración antigua borraba tu Esencia y tu Árbol del Escultor (un fallo de diseño ya retirado). " +
                $"Te devolvemos +{Num.F(lost)} Esencia para que reconstruyas el Árbol. No se repetirá.";
        }

        public void ResetSave()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            S = new GameState();
            PrestigeReport = ""; // sin informe: la HUD no abre el modal de "Canica pulida"
            RecalcStats();
            Prestiged?.Invoke();
        }

        void ApplyOffline()
        {
            if (S.lastSaveUnix <= 0) return;
            double secs = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - S.lastSaveUnix;
            secs = Math.Min(secs, config.offlineMaxHours * 3600.0);
            if (secs < 60 || AcolyteCount <= 0) return;
            double dps = AcolyteCount * AcolyteDamage / acolyteInterval;
            double blocksPerSec = Math.Min(dps / MonolithHP(false, false), 0.02 * AcolyteCount + 0.1);
            double earned = blocksPerSec * secs * BlockReward(false, false) * (1 + config.statueBonus * StatueMult) * OfflineEfficiency;
            if (earned <= 0) return;
            AddDust(earned);
            PendingOffline = (earned, secs);
            OfflineEarned?.Invoke(earned, secs);
        }
    }
}
