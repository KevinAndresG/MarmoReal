using System;
using System.Collections.Generic;
using UnityEngine;

namespace Marmoreal
{
    public enum EffectMode { Linear, Exponential }
    public enum Currency { Dust, Essence, Icor }

    /// <summary>Poder activable (teclas 1-6). Se desbloquea con el nodo del Altar de mismo id.</summary>
    [Serializable]
    public class PowerDef
    {
        public string id;
        public string displayName;
        public string glyph = "✦";
        [TextArea] public string description;
        [Tooltip("Recarga base en segundos")]
        public float cooldown = 90f;
        [Tooltip("Daño en múltiplos del daño de impacto actual (escala con tu progreso, no rompe el muro)")]
        public float potency = 40f;
        public float radius = 10f;
        public float duration = 0f;
        public int count = 0;
        [Tooltip("Remate final (Gran Meteoro, implosión del Vórtice) en múltiplos del daño de impacto. 0 = sin remate")]
        public float finale = 0f;
    }

    /// <summary>Mecenazgo (logro): al superar el umbral de una estadística da Ícor y Fama permanente.</summary>
    [Serializable]
    public class AchievementDef
    {
        public string id;
        public string displayName;
        public string description;
        [Tooltip("statues, golden, gallery, prestige, combo, powers, musas, dust")]
        public string stat;
        public double threshold;
        public int rewardIcor = 1;
    }

    /// <summary>Mutador: carta riesgo/recompensa que eliges al Pulir; dura toda la vida siguiente.</summary>
    [Serializable]
    public class MutatorDef
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        [Tooltip("Carta segura: sin riesgo ni bono. Siempre se ofrece una")]
        public bool safe;
        public float hpMult = 1f, dustMult = 1f, magnetMult = 1f, acolyteMult = 1f, allyMult = 1f, powerCooldownMult = 1f;
        [Tooltip("Probabilidad extra de bloque volcánico (aditiva)")]
        public float volatileBonus;
        public bool alwaysCrit, noDash;
        [Tooltip("Id del poder bloqueado esta vida (vacío = ninguno)")]
        public string disabledPower = "";
        [Tooltip("Esencia extra (fracción) al Pulir si esta vida subes goalGalleries galerías")]
        public float essenceBonus;
        public int goalGalleries;
    }

    /// <summary>Evento temporal (Luna de Ónice, Lluvia de Cristal, Hora Dorada…). Lo lanza LifeDirector.</summary>
    [Serializable]
    public class LifeEventDef
    {
        public string id;
        public string displayName;
        public string description;
        public int minGallery;
        public float duration = 30f;
        [Tooltip("Probabilidad extra de bloque dorado (1 = todo dorado; también convierte los bloques en pie)")]
        public float goldenChance;
        [Tooltip("Frenesí de polvo durante el evento (0 = no)")]
        public float frenzyMult;
        [Tooltip("Cada cuántos segundos cae una lluvia de meteoros (0 = no)")]
        public float meteorEvery;
        public int meteorCount;
        public float weight = 1f;
    }

    public enum ContractKind { VolatileChain, Combo, PowerBurst, Golden, Boss, Statues }

    /// <summary>Contrato: objetivo opcional de la vida actual. description admite {0} = objetivo.</summary>
    [Serializable]
    public class ContractDef
    {
        public string id;
        public string description;
        public ContractKind kind;
        public int target = 1;
        [Tooltip("Ventana en segundos (cadena de volcánicos, ráfaga de poderes)")]
        public float window;
        public int rewardIcor;
        [Tooltip("Esencia = esta fracción de la Esencia que daría Pulir ahora (mínimo 1)")]
        public float rewardEssencePct;
    }

    /// <summary>Pieza del Gran Monumento (campaña S4). Índice = campaignStage de la sala que la da.</summary>
    [Serializable]
    public class CampaignPieceDef
    {
        public string id;
        public string displayName;
        [Tooltip("Sala que la da (texto; la sala jugable es el BiomeDef con ese campaignStage)")]
        public string room;
    }

    public enum ColorUnlock { Start, Prestiges, Trial, Campaign, InfiniteGallery, Fragments }

    /// <summary>Reliquia (S5): se gana al Consagrar y, equipada, cambia un poder de forma cualitativa. El efecto lo lee
    /// PowerSystem por id (GameManager.RelicOn).</summary>
    [Serializable]
    public class RelicDef
    {
        public string id;
        public string displayName;
        public string glyph = "✧";
        [Tooltip("PowerDef.id al que afecta (vacío = todos)")]
        public string power;
        [TextArea] public string description;
    }

    /// <summary>Modificador aleatorio del Coloso en el Modo Infinito.</summary>
    [Serializable]
    public class BossModDef
    {
        public string id;
        public string displayName;
        public string description;
        public float hpMult = 1f, rewardMult = 1f, scale = 1f;
        [Tooltip("Fracción de la vida que recupera por segundo")]
        public float regenPerSec;
        [Tooltip("Multiplicador del daño recibido de poderes / de la canica")]
        public float powerTaken = 1f, playerTaken = 1f;
        [Tooltip("Fragmentos de Eternidad ×esto al derribarlo")]
        public int fragmentMult = 1;
    }

    /// <summary>Canica coleccionable: material + colores cosméticos (estela, onda). Nada de poder.</summary>
    [Serializable]
    public class MarbleColorDef
    {
        public string id;
        public string displayName;
        public ColorUnlock unlock;
        [Tooltip("Pulidos / nº de Prueba (1-7) / galería del infinito, según unlock")]
        public int threshold;
        public string hint;
        public Material material;
        public Color trailColor = Color.white;
        public Color waveColor = Color.white;
    }

    [Serializable]
    public class UpgradeDef
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public string glyph = "◆";
        public Currency currency = Currency.Dust;
        public double baseCost = 10;
        public double costGrowth = 1.5;
        public int maxLevel = 999;
        public EffectMode mode = EffectMode.Linear;
        public float effectBase;
        public float effectPerLevel = 1;
        [Tooltip("Formato del efecto en la UI: {0} = valor. Ej: \"×{0}\"")]
        public string effectFormat = "{0}";
        [Tooltip("Nivel de galería necesario para que aparezca")]
        public int unlockGallery;

        [Header("Progresión por Pulido (solo Taller)")]
        [Tooltip("Si es true, el coste se multiplica por (Brillo×Obra×Fama)^k × crecimiento^pulidos (ver EconomyConfig)")]
        public bool scaleCost = true;
        [Tooltip("Niveles de tope extra por cada Pulido (0 = tope fijo). Admite fracciones: 0.5 = +1 cada 2 pulidos")]
        public float maxLevelPerPrestige;
        [Tooltip("Tope absoluto al que puede llegar el tope creciente (0 = sin límite extra)")]
        public int maxLevelLimit;
        [Tooltip("Hito de ESTA vida para que aparezca: runGalleries (galerías subidas) o runVolatile (volcánicos rotos). Vacío = sin hito")]
        public string unlockStat;
        public int unlockThreshold;
        [Tooltip("Texto del hito para el aviso, p. ej. \"subir 3 galerías\"")]
        public string unlockHint;

        [Header("Árboles (Esencia/Ícor) — 'requires' también vale para el Taller")]
        [Tooltip("Posición del nodo en el árbol (x: columnas, y: filas hacia abajo)")]
        public Vector2 treePos;
        [Tooltip("Ids de nodos que deben tener al menos nivel 1")]
        public string[] requires = new string[0];
        [Tooltip("Rama visual: 0 centro/oro, 1 masa, 2 espíritu, 3 cumbre")]
        public int branch;

        public double Cost(int level) => Math.Ceiling(baseCost * Math.Pow(costGrowth, level));

        public float Value(int level) => mode == EffectMode.Linear
            ? effectBase + effectPerLevel * level
            : effectBase * Mathf.Pow(effectPerLevel, level);
    }

    /// <summary>
    /// Toda la economía del juego es editable en este asset (Assets/Marmoreal/Config/Economy.asset).
    /// </summary>
    [CreateAssetMenu(menuName = "Marmoreal/Economy Config")]
    public class EconomyConfig : ScriptableObject
    {
        [Header("Monolitos")]
        public double monolithBaseHP = 22;
        [Tooltip("La vida crece más rápido que la recompensa: eso crea el 'muro suave' que invita a Pulir")]
        public double hpGrowthPerGallery = 1.72;
        [Tooltip("Polvo que suelta un bloque pequeño en la galería I (sin estatua)")]
        public double blockBaseReward = 4;
        public double rewardGrowthPerGallery = 1.5;
        [Tooltip("Multiplicador del premio de la estatua revelada sobre el bloque entero")]
        public double statueBonus = 1.5;
        public float largeBlockHPMult = 2.6f;
        public float goldenChance = 0.03f;
        public float goldenHPMult = 3f;
        public float goldenRewardMult = 12f;

        [Header("Galería")]
        public int statuesPerGalleryBase = 8;
        public int statuesPerGalleryStep = 2;
        public int statuesPerGalleryMax = 30;

        [Header("Impacto")]
        [Tooltip("Velocidad (m/s) a la que el impacto hace el daño base")]
        public float referenceSpeed = 9f;
        public float minImpactSpeed = 1.6f;
        public float dashDamageMult = 2f;
        public float critMult = 3f;
        public float comboStep = 0.03f;
        public int comboMaxHits = 50;
        public float comboWindow = 2.5f;

        [Header("Resonancia")]
        public float resonancePerImpact = 0.1f;
        public float resonancePerAcolyteHit = 0.012f;
        public float shockwaveDamageMult = 6f;
        public float shockwaveBaseRadius = 9f;

        [Header("Prestigio (Pulido)")]
        [Tooltip("Galería (índice 0) mínima para Pulir")]
        public int prestigeMinGallery = 8;
        [Tooltip("Esencia = floor(base * (galería - min + 3) ^ pow)")]
        public double prestigeBase = 0.3;
        public double prestigePow = 1.6;

        [Header("Offline")]
        public float offlineMaxHours = 8f;

        [Header("Ícor (moneda divina, permanente)")]
        [Tooltip("Probabilidad de que una estatua suelte 1 Ícor")]
        public float icorStatueChance = 0.02f;
        [Tooltip("Probabilidad de que un bloque dorado natural suelte Ícor")]
        public float icorGoldenChance = 0.3f;
        public int icorGolden = 1;
        public int icorMusa = 1;
        [Tooltip("Ícor al pulir = base + floor(esencia ganada / divisor)")]
        public int icorPrestigeBase = 2;
        public double icorPrestigeDivisor = 10;

        [Header("Musas (bendiciones aleatorias)")]
        public int musaUnlockGallery = 1;
        public float musaIntervalMin = 60f, musaIntervalMax = 120f;
        public float musaLifetime = 18f;
        public float frenzyMult = 5f, frenzyDuration = 20f;
        public float furyDuration = 12f;
        [Tooltip("Frenesí que deja la Supernova (polvo ×Codicia durante unos segundos)")]
        public float bombFrenzyDuration = 6f;
        [Tooltip("Onda Infinita: la Resonancia se rellena sola durante estos segundos")]
        public float overflowDuration = 8f;
        [Tooltip("Legión: Discípulas fantasma temporales")]
        public int legionCount = 4;
        public float legionDuration = 20f;
        [Tooltip("Cornucopia: polvo instantáneo = segundos de tu polvo/s actual (mínimo 30 bloques)")]
        public float cornucopiaSeconds = 90f;

        [Header("Bloques volcánicos (estallan al romperse)")]
        public float volatileChance = 0.06f;
        public int volatileUnlockGallery = 2;
        [Tooltip("Daño de la explosión en múltiplos del daño de impacto")]
        public float volatileBlastMult = 12f;
        public float volatileBlastRadius = 7f;

        [Header("Discípulas (canicas aliadas)")]
        public float allyResonancePerHit = 0.03f;

        [Header("Coste del Taller escalado por poder permanente")]
        public bool workshopCostScale = true;
        [Tooltip("k: coste × (Brillo×Obra×Fama)^k. 0 = sin escalado")]
        public float workshopCostK = 1.8f;
        [Tooltip("Coste × este valor ^ nº de pulidos (más contenido por vida cuanto más pules)")]
        public float workshopCostPerPrestige = 1.6f;

        [Header("Mecenazgos")]
        [Tooltip("Fama: +X polvo por cada mecenazgo conseguido (aditivo)")]
        public float famePerAchievement = 0.02f;

        [Header("Jefes: Coloso cada N galerías")]
        public int bossEveryGalleries = 10;
        [Tooltip("Vida del Coloso en bloques pequeños normales de su galería")]
        public float bossHPMult = 50f;
        [Tooltip("Polvo del Coloso en bloques pequeños (la estatua da statueBonus encima)")]
        public float bossDustMult = 40f;
        public int bossIcor = 3;
        [Tooltip("Ícor extra por cada Coloso anterior (galería XX, XXX…)")]
        public int bossIcorPerTier = 1;

        [Header("Eventos temporales")]
        public int eventUnlockGallery = 2;
        public float eventIntervalMin = 150f, eventIntervalMax = 260f;

        [Header("Contratos")]
        public int contractsPerLife = 3;
        public int contractsUnlockPrestige = 1;

        [Header("Campaña: El Gran Monumento (Pruebas en BiomeDef.trialGallery)")]
        [Tooltip("Piezas para ganar la campaña (7 con todas las salas; hoy 3 jugables)")]
        public int campaignPiecesRequired = 7;
        [Tooltip("Vida del jefe de la Prueba = Coloso × esto")]
        public float trialHPMult = 2f;
        public int trialIcor = 15;
        public List<CampaignPieceDef> campaign = new List<CampaignPieceDef>();
        public List<MarbleColorDef> marbleColors = new List<MarbleColorDef>();

        [Header("Modo Infinito")]
        [Tooltip("Vida de los bloques +X por cada Pulido en el infinito")]
        public float infiniteHpPerLife = 0.25f;
        public string[] rankNames = { "Aprendiz", "Maestro", "Leyenda", "Deidad" };
        [Tooltip("Galería mínima de cada rango (mismo orden)")]
        public int[] rankGalleries = { 0, 150, 300, 500 };

        [Header("Hito de Reliquia (antes Consagrar) y Reliquias")]
        [Tooltip("S8: cada N Pulidos el Pulido entrega 1 Reliquia y cuenta 1 Consagración (no reinicia nada)")]
        public int consecrateMinPrestiges = 5;
        [Tooltip("Esencia al Pulir ×(1 + esto × Consagraciones/hitos de Reliquia)")]
        public float consecrateEssenceBonus = 0.25f;
        [Tooltip("Partidas con Consagraciones antiguas (S5-S7, reiniciaban el Árbol): el ×coste del Taller por Pulido cuenta solo los Pulidos desde la última de ellas")]
        public bool consecrateResetsCostGrowth = true;
        [Tooltip("Daño de poderes ×(1 + esto × reliquias poseídas)")]
        public float relicPowerBonus = 0.1f;
        public int relicSlotsBase = 2;
        [Tooltip("Coste en Fragmentos de cada ranura extra (su número = ranuras comprables)")]
        public int[] relicSlotCosts = { 40, 120 };
        [Header("Fragmentos de Eternidad (Modo Infinito)")]
        public int eternityPerGallery = 1;
        public int eternityPerBoss = 5;
        public List<RelicDef> relics = new List<RelicDef>();
        public List<BossModDef> bossMods = new List<BossModDef>();

        public List<UpgradeDef> upgrades = new List<UpgradeDef>();
        public List<MutatorDef> mutators = new List<MutatorDef>();
        public List<LifeEventDef> lifeEvents = new List<LifeEventDef>();
        public List<ContractDef> contracts = new List<ContractDef>();
        public List<PowerDef> powers = new List<PowerDef>();
        public List<AchievementDef> achievements = new List<AchievementDef>();

        public PowerDef GetPower(string id)
        {
            foreach (var p in powers) if (p.id == id) return p;
            return null;
        }

        public MutatorDef GetMutator(string id) => string.IsNullOrEmpty(id) ? null : mutators.Find(m => m.id == id);
        public ContractDef GetContract(string id) => contracts.Find(c => c.id == id);
        public MarbleColorDef GetColor(string id) => marbleColors.Find(c => c.id == id);
        public RelicDef GetRelic(string id) => relics.Find(r => r.id == id);

        Dictionary<string, UpgradeDef> map;

        public UpgradeDef Get(string id)
        {
            if (map == null || map.Count != upgrades.Count)
            {
                map = new Dictionary<string, UpgradeDef>();
                foreach (var u in upgrades) if (!string.IsNullOrEmpty(u.id)) map[u.id] = u;
            }
            return map.TryGetValue(id, out var d) ? d : null;
        }

        void OnValidate() { map = null; }

        /// <summary>
        /// Migración ADITIVA: añade mejoras/poderes/logros nuevos que falten (por id) sin tocar los existentes.
        /// Devuelve true si cambió algo.
        /// </summary>
        public bool EnsureDefaults()
        {
            var fresh = CreateInstance<EconomyConfig>();
            fresh.FillDefaults();
            bool changed = false;
            foreach (var u in fresh.upgrades)
            {
                var old = Get(u.id);
                if (old == null) { upgrades.Add(u); changed = true; map = null; continue; }
                // Migración aditiva de la progresión por Pulido: solo si el asset aún no la trae (no pisa lo que edites)
                if (old.maxLevelLimit == 0 && old.maxLevelPerPrestige == 0 && u.maxLevelPerPrestige > 0)
                { old.maxLevelPerPrestige = u.maxLevelPerPrestige; old.maxLevelLimit = u.maxLevelLimit; changed = true; }
                if (string.IsNullOrEmpty(old.unlockStat) && !string.IsNullOrEmpty(u.unlockStat))
                { old.unlockStat = u.unlockStat; old.unlockThreshold = u.unlockThreshold; old.unlockHint = u.unlockHint; changed = true; }
                // Mejoras "vivas": los antiguos topes 60 / 14 pasan a ilimitado
                if (u.maxLevel >= 999 && (u.id == "resonancia" && old.maxLevel == 60 || u.id == "vinculo" && old.maxLevel == 14))
                { old.maxLevel = u.maxLevel; changed = true; }
                if (u.id == "cargas" && old.maxLevelLimit == 0 && old.maxLevel == 3) { old.maxLevelLimit = u.maxLevelLimit; old.maxLevelPerPrestige = u.maxLevelPerPrestige; changed = true; }
            }
            foreach (var p in fresh.powers)
                if (GetPower(p.id) == null) { powers.Add(p); changed = true; }
            foreach (var a in fresh.achievements)
                if (achievements.Find(x => x.id == a.id) == null) { achievements.Add(a); changed = true; }
            foreach (var m in fresh.mutators)
                if (GetMutator(m.id) == null) { mutators.Add(m); changed = true; }
            foreach (var e in fresh.lifeEvents)
                if (lifeEvents.Find(x => x.id == e.id) == null) { lifeEvents.Add(e); changed = true; }
            foreach (var c in fresh.contracts)
                if (GetContract(c.id) == null) { contracts.Add(c); changed = true; }
            foreach (var c in fresh.campaign)
                if (campaign.Find(x => x.id == c.id) == null) { campaign.Add(c); changed = true; }
            foreach (var c in fresh.marbleColors)
                if (GetColor(c.id) == null) { marbleColors.Add(c); changed = true; }
            foreach (var r in fresh.relics)
                if (GetRelic(r.id) == null) { relics.Add(r); changed = true; }
            foreach (var b in fresh.bossMods)
                if (bossMods.Find(x => x.id == b.id) == null) { bossMods.Add(b); changed = true; }
            if (Application.isPlaying) Destroy(fresh); else DestroyImmediate(fresh);
            return changed;
        }

        /// <summary>Valores por defecto. Solo se usan al crear el asset por primera vez.</summary>
        public void FillDefaults()
        {
            upgrades = new List<UpgradeDef>
            {
                U("masa", "Masa", "Cada impacto golpea más fuerte.", "●", 12, 1.5, 500, EffectMode.Exponential, 3f, 1.3f, "{0} daño"),
                Cap(U("inercia", "Inercia", "Velocidad máxima y aceleración.", "➤", 30, 1.75, 25, EffectMode.Linear, 11f, 0.75f, "{0} m/s"), 1, 60),
                Cap(U("iman", "Imán", "Radio de atracción del polvo.", "◎", 20, 1.6, 25, EffectMode.Linear, 5f, 0.9f, "{0} m"), 1, 60),
                U("pureza", "Pureza", "Más polvo por cada fragmento.", "✦", 45, 1.75, 500, EffectMode.Exponential, 1f, 1.2f, "×{0}"),
                Cap(U("fortuna", "Fortuna", "Probabilidad de golpe crítico (×3).", "★", 150, 1.9, 18, EffectMode.Linear, 0.05f, 0.025f, "{0}", 1), 1, 30),
                Cap(U("cantera", "Cantera", "Más bloques de mármol a la vez.", "▣", 80, 2.15, 12, EffectMode.Linear, 5f, 1f, "{0} bloques", 1), 1, 30),
                Hito(Cap(U("acolitos", "Acólitos", "Esferas que tallan por ti, incluso sin mirar.", "○", 150, 3.0, 12, EffectMode.Linear, 0f, 1f, "{0} acólitos", 2), 3.25f, 60), "runGalleries", 3, "subir 3 galerías en esta vida"),
                U("cincel", "Cincel", "Daño de los acólitos.", "⚒", 220, 1.6, 500, EffectMode.Exponential, 1.6f, 1.28f, "{0} daño", 2),
                U("resonancia", "Resonancia", "Poder y radio de la onda [F]. Sin límite.", "≋", 300, 1.7, 999, EffectMode.Linear, 1f, 0.35f, "×{0}", 3),
                Cap(U("cargas", "Doble Embestida", "Cargas de Embestida [Shift]: encadena varias seguidas antes de recargar. Cada 2 pulidos, una carga más.", "»", 600, 6, 3, EffectMode.Linear, 1f, 1f, "{0} cargas", 2), 0.5f, 8),
                Hito(Cap(U("gemelas", "Discípulas", "Canicas de mármol que ruedan solas y embisten bloques con una parte de TU daño de impacto.", "◐", 2500, 3.2, 8, EffectMode.Linear, 0f, 1f, "{0} discípulas", 4), 1, 24), "runVolatile", 12, "romper 12 bloques volcánicos en esta vida"),
                P(U("vinculo", "Vínculo", "Parte de tu daño de impacto que copian las Discípulas. Sin límite.", "∾", 4000, 1.85, 999, EffectMode.Linear, 0.3f, 0.05f, "{0} de tu daño", 4), "gemelas"),

                // ---------- MEJORAS DE PODERES (Polvo; aparecen al desbloquear el poder en el Altar y se pierden al Pulir) ----------
                P(U("pw_supernova", "Pólvora Divina", "Daño de la Supernova [1]. Empieza flojo: súbelo en cada vida.", "✹", 400, 2.2, 20, EffectMode.Exponential, 0.25f, 1.1f, "×{0} daño"), "supernova"),
                P(U("pw_ingravidez", "Gravedad Rota", "Daño del impacto de la Ingravidez [2].", "⇡", 900, 2.2, 20, EffectMode.Exponential, 0.25f, 1.1f, "×{0} daño"), "ingravidez"),
                P(U("pw_meteoros", "Núcleo Ígneo", "Daño de cada meteorito [3].", "☄", 900, 2.2, 20, EffectMode.Exponential, 0.25f, 1.1f, "×{0} daño"), "meteoros"),
                P(U("pw_coloso", "Gigantismo", "Duración del Coloso [4].", "⬤", 2500, 2.1, 15, EffectMode.Linear, 4f, 0.6f, "{0} s"), "coloso"),
                P(U("pw_vortice", "Singularidad", "Daño del Vórtice [5].", "◎", 4000, 2.2, 20, EffectMode.Exponential, 0.25f, 1.1f, "×{0} daño"), "vortice"),
                P(U("pw_midas", "Alquimia", "Bloques que dora el Toque de Midas [6].", "♛", 20000, 8f, 4, EffectMode.Linear, 1f, 1f, "{0} bloques"), "midas"),
                P(U("pw_rayo", "Ira de Zeus", "Daño de cada rayo de la Cadena de Rayos [7].", "↯", 6000, 2.2, 20, EffectMode.Exponential, 0.25f, 1.1f, "×{0} daño"), "rayo"),

                // ---------- ÁRBOL DE HABILIDADES (Esencia) ----------
                // Raíz
                E("brillo", "Brillo Eterno", "Multiplica TODO el polvo y el daño. Sin límite.", "☀", 1, 1.55, 300, EffectMode.Exponential, 1f, 1.25f, "×{0}", 0, 0, 0),
                // Rama MASA (izquierda)
                E("fuerza", "Fuerza Ancestral", "Daño de impacto de la canica.", "●", 2, 1.7, 60, EffectMode.Exponential, 1f, 1.4f, "×{0}", -1.3f, 1, 1, "brillo"),
                E("ojo", "Ojo Clínico", "Los críticos multiplican más.", "✧", 3, 2.2, 5, EffectMode.Linear, 0f, 1f, "+{0} crítico", -2.5f, 1.5f, 1, "fuerza"),
                E("rebote", "Rebote Vivo", "MECÁNICA: al chocar un bloque sales despedida. Encadena golpes como un pinball. Cada nivel rebota más fuerte.", "↯", 4, 2.5, 4, EffectMode.Linear, 0.3f, 0.1f, "+{0} rebote", -1.3f, 2, 1, "fuerza"),
                E("sismo", "Embestida Sísmica", "MECÁNICA: los impactos de la Embestida y del Golpe en Picado liberan una mini-onda. Cada nivel: más daño y radio.", "✺", 8, 2.5, 4, EffectMode.Linear, 0f, 3f, "×{0} daño", -2.5f, 2.6f, 1, "rebote"),
                E("metralla", "Metralla", "MECÁNICA: los trozos que saltan dañan a otros bloques al chocar. Reacciones en cadena. Cada nivel: trozos más letales.", "⁂", 12, 2.5, 4, EffectMode.Linear, 0f, 0.4f, "×{0} daño", -1.3f, 3, 1, "rebote"),
                // Rama ORO (centro)
                E("veta", "Veta de Oro", "Estatuas y bloques dorados valen más.", "♛", 2, 1.8, 100, EffectMode.Exponential, 1f, 1.5f, "×{0}", 0, 1, 0, "brillo"),
                E("fiebre", "Fiebre Dorada", "Más bloques dorados.", "✪", 3, 1.9, 8, EffectMode.Linear, 0f, 0.015f, "+{0}", 0, 2, 0, "veta"),
                E("memoria", "Memoria del Escultor", "Tras pulir empiezas en una galería más avanzada.", "⌂", 5, 2.0, 10, EffectMode.Linear, 0f, 1f, "+{0} galerías", 0, 3, 0, "fiebre"),
                // Rama ESPÍRITU (derecha)
                E("eterno", "Acólito Eterno", "Acólitos gratis que nunca se pierden.", "◌", 2, 2.4, 6, EffectMode.Linear, 0f, 1f, "+{0}", 1.3f, 1, 2, "brillo"),
                E("coro", "Coro de Acólitos", "Daño de los acólitos.", "♫", 3, 1.75, 60, EffectMode.Exponential, 1f, 1.4f, "×{0}", 2.5f, 1.5f, 2, "eterno"),
                E("paciencia", "Paciencia", "Progreso offline más eficiente.", "⌛", 2, 2.0, 5, EffectMode.Linear, 0.25f, 0.15f, "{0}", 1.3f, 2, 2, "eterno"),
                E("impulso", "Impulso", "Recarga de la Embestida más rápida.", "»", 3, 2.0, 5, EffectMode.Linear, 2.4f, -0.3f, "{0}s", 2.5f, 2.6f, 2, "paciencia"),
                E("eco", "Eco Resonante", "MECÁNICA: la onda [F] se repite en ecos cada vez más amplios. Nivel 1: 2 ecos · nivel 4: 5 ecos.", "≋", 10, 2.5, 4, EffectMode.Linear, 1f, 1f, "{0} ecos", 1.3f, 3, 2, "paciencia"),
                E("perpetua", "Resonancia Perpetua", "MECÁNICA: la onda se libera sola al llenarse. Cada nivel: la Resonancia se carga más rápido.", "∞", 14, 2.5, 4, EffectMode.Linear, 0.75f, 0.25f, "×{0} carga", 2.5f, 3.6f, 2, "eco"),
                // Cumbre
                E("obra", "Obra Maestra", "La cumbre del escultor: duplica TODO por nivel.", "♜", 30, 3.0, 50, EffectMode.Exponential, 1f, 2f, "×{0}", 0, 4.3f, 3, "metralla", "memoria", "eco"),

                // ---------- ALTAR DE LOS DIOSES (Ícor, permanente: no se pierde al pulir) ----------
                // Cada poder es una columna independiente (fila 0, sin requisitos) con su propia rama de mejoras debajo.
                // Columna de las Musas a la derecha y, abajo, las bendiciones generales de todos los poderes.
                // Supernova (x −3)
                G("supernova", "Supernova", "PODER [1]: planta una bomba de mármol que arrasa todo a su alrededor y desata un Frenesí de polvo.", "✹", 6, 1, 1, EffectMode.Linear, 0, 1, "{0}", -3f, 0, 3),
                G("nova_radio", "Onda Expansiva", "Radio de la Supernova.", "◉", 3, 1.8, 8, EffectMode.Linear, 1f, 0.15f, "×{0}", -3f, 1.25f, 1, "supernova"),
                G("nova_cd", "Mecha Corta", "Recarga de la Supernova.", "⧗", 3, 1.9, 6, EffectMode.Linear, 1f, -0.07f, "×{0}", -3f, 2.5f, 1, "nova_radio"),
                G("codicia", "Codicia Divina", "Multiplicador del Frenesí de la Supernova.", "₪", 6, 1.9, 8, EffectMode.Linear, 2f, 0.25f, "×{0}", -3f, 3.75f, 1, "nova_cd"),
                // Ingravidez (x −2)
                G("ingravidez", "Ingravidez", "PODER [2]: todos los bloques levitan, giran… y se estrellan contra el suelo.", "⇡", 7, 1, 1, EffectMode.Linear, 0, 1, "{0}", -2f, 0, 3),
                G("caida", "Caída Celestial", "Daño de la Ingravidez.", "⇣", 4, 1.8, 10, EffectMode.Exponential, 1f, 1.35f, "×{0}", -2f, 1.25f, 1, "ingravidez"),
                G("ingravidez_cd", "Levedad", "Recarga de la Ingravidez.", "⧗", 4, 1.9, 6, EffectMode.Linear, 1f, -0.07f, "×{0}", -2f, 2.5f, 1, "caida"),
                // Meteoros (x −1)
                G("meteoros", "Lluvia de Meteoros", "PODER [3]: meteoritos caen sobre todos los bloques y la lluvia termina con un GRAN METEORO.", "☄", 7, 1, 1, EffectMode.Linear, 0, 1, "{0}", -1f, 0, 3),
                G("enjambre", "Enjambre", "Más meteoritos por lluvia.", "⁘", 4, 1.8, 8, EffectMode.Linear, 0f, 3f, "+{0}", -1f, 1.25f, 2, "meteoros"),
                G("meteoros_cd", "Cielo Abierto", "Recarga de la Lluvia de Meteoros.", "⧗", 4, 1.9, 6, EffectMode.Linear, 1f, -0.07f, "×{0}", -1f, 2.5f, 2, "enjambre"),
                // Coloso (x 0)
                G("coloso", "Coloso", "PODER [4]: tu canica se vuelve gigante y aplasta todo (×3 daño).", "⬤", 15, 1, 1, EffectMode.Linear, 0, 1, "{0}", 0f, 0, 3),
                G("titan", "Titán", "El Coloso dura más.", "♚", 5, 1.8, 8, EffectMode.Linear, 0f, 2f, "+{0}s", 0f, 1.25f, 1, "coloso"),
                G("coloso_cd", "Crecimiento Veloz", "Recarga del Coloso.", "⧗", 6, 1.9, 6, EffectMode.Linear, 1f, -0.07f, "×{0}", 0f, 2.5f, 1, "titan"),
                // Vórtice (x 1)
                G("vortice", "Vórtice", "PODER [5]: un agujero negro que viaja de bloque en bloque, se traga trozos y polvo, y al final IMPLOSIONA.", "◎", 20, 1, 1, EffectMode.Linear, 0, 1, "{0}", 1f, 0, 3),
                G("horizonte", "Horizonte de Sucesos", "Fuerza y radio del Vórtice.", "◍", 5, 1.8, 10, EffectMode.Exponential, 1f, 1.3f, "×{0}", 1f, 1.25f, 2, "vortice"),
                G("vortice_cd", "Colapso", "Recarga del Vórtice.", "⧗", 7, 1.9, 6, EffectMode.Linear, 1f, -0.07f, "×{0}", 1f, 2.5f, 2, "horizonte"),
                // Midas (x 2)
                G("midas", "Toque de Midas", "PODER [6]: convierte bloques en ORO macizo.", "♛", 40, 1, 1, EffectMode.Linear, 0, 1, "{0}", 2f, 0, 3),
                G("reymidas", "Rey Midas", "El Toque de Midas dora más bloques.", "♕", 8, 2.0, 6, EffectMode.Linear, 0f, 1f, "+{0}", 2f, 1.25f, 0, "midas"),
                G("midas_cd", "Avaricia", "Recarga del Toque de Midas.", "⧗", 10, 1.9, 6, EffectMode.Linear, 1f, -0.07f, "×{0}", 2f, 2.5f, 0, "reymidas"),
                // Rayo (x 3)
                G("rayo", "Cadena de Rayos", "PODER [7]: Zeus descarga rayos que saltan de bloque en bloque, cada salto algo más débil.", "↯", 25, 1, 1, EffectMode.Linear, 0, 1, "{0}", 3f, 0, 3),
                G("cadena", "Conductor", "Cada rayo salta a más bloques.", "⇝", 5, 1.8, 6, EffectMode.Linear, 0f, 1f, "+{0} saltos", 3f, 1.25f, 2, "rayo"),
                G("rayo_cd", "Tormenta Perpetua", "Recarga de la Cadena de Rayos.", "⧗", 7, 1.9, 6, EffectMode.Linear, 1f, -0.07f, "×{0}", 3f, 2.5f, 2, "cadena"),
                // Musas (fila inferior derecha)
                G("favor", "Favor de las Musas", "Las Musas aparecen más a menudo.", "♪", 2, 1.9, 5, EffectMode.Linear, 1f, -0.1f, "×{0} espera", 2f, 3.9f, 0),
                G("inspiracion", "Inspiración", "Las bendiciones de las Musas duran más.", "❦", 3, 1.8, 6, EffectMode.Linear, 1f, 0.25f, "×{0}", 3f, 3.9f, 0, "favor"),
                // Generales (afectan a todos los poderes)
                G("ira", "Ira Divina", "Daño de TODOS los poderes. Sin límite.", "ϟ", 4, 1.6, 200, EffectMode.Exponential, 1f, 1.25f, "×{0}", -0.75f, 3.9f, 0),
                G("eternidad", "Eternidad", "Recarga de TODOS los poderes.", "∞", 20, 2.2, 8, EffectMode.Linear, 1f, -0.05f, "×{0}", 0.75f, 3.9f, 0),
            };

            powers = new List<PowerDef>
            {
                new PowerDef { id = "supernova", displayName = "Supernova", glyph = "✹", description = "Bomba de mármol: arrasa en área y desata un Frenesí de polvo.", cooldown = 120, potency = 45, radius = 12, duration = 1.6f },
                new PowerDef { id = "ingravidez", displayName = "Ingravidez", glyph = "⇡", description = "Todos los bloques levitan y se estrellan.", cooldown = 150, potency = 60, radius = 60, duration = 2.2f },
                new PowerDef { id = "meteoros", displayName = "Meteoros", glyph = "☄", description = "Meteoritos sobre todos los bloques y un Gran Meteoro final.", cooldown = 90, potency = 35, radius = 4.5f, count = 12, duration = 3f, finale = 150 },
                new PowerDef { id = "coloso", displayName = "Coloso", glyph = "⬤", description = "Canica gigante, ×3 daño.", cooldown = 80, potency = 3, radius = 2.2f, duration = 10 },
                new PowerDef { id = "vortice", displayName = "Vórtice", glyph = "◎", description = "Agujero negro errante que tritura, absorbe e implosiona.", cooldown = 120, potency = 25, radius = 9, duration = 6, finale = 60 },
                new PowerDef { id = "midas", displayName = "Midas", glyph = "♛", description = "Convierte bloques en oro.", cooldown = 140, count = 2 },
                new PowerDef { id = "rayo", displayName = "Cadena de Rayos", glyph = "↯", description = "Rayos que saltan de bloque en bloque.", cooldown = 100, potency = 30, radius = 14, count = 6, duration = 2.4f },
            };

            achievements = new List<AchievementDef>
            {
                A("est1", "Primer Cincelazo", "Libera tu primera estatua.", "statues", 1, 1),
                A("est25", "Aprendiz", "Libera 25 estatuas.", "statues", 25, 1),
                A("est100", "Taller Propio", "Libera 100 estatuas.", "statues", 100, 2),
                A("est500", "Maestro de Carrara", "Libera 500 estatuas.", "statues", 500, 3),
                A("est2k", "Leyenda del Mármol", "Libera 2.000 estatuas.", "statues", 2000, 4),
                A("est10k", "Panteón", "Libera 10.000 estatuas.", "statues", 10000, 6),
                A("oro1", "Brillo Inesperado", "Encuentra un bloque dorado.", "golden", 1, 1),
                A("oro10", "Buscador de Oro", "Encuentra 10 bloques dorados.", "golden", 10, 2),
                A("oro50", "El Dorado", "Encuentra 50 bloques dorados.", "golden", 50, 4),
                A("gal5", "Galería V", "Llega a la Galería V.", "gallery", 5, 1),
                A("gal10", "Galería X", "Llega a la Galería X.", "gallery", 10, 2),
                A("gal20", "Galería XX", "Llega a la Galería XX.", "gallery", 20, 3),
                A("gal40", "Galería XL", "Llega a la Galería XL.", "gallery", 40, 5),
                A("gal75", "Galería LXXV", "Llega a la Galería LXXV.", "gallery", 75, 8),
                A("pul1", "Primer Pulido", "Pule la canica por primera vez.", "prestige", 1, 2),
                A("pul5", "Espejo", "Pule la canica 5 veces.", "prestige", 5, 3),
                A("pul15", "Reflejo Eterno", "Pule la canica 15 veces.", "prestige", 15, 6),
                A("com15", "Ritmo", "Encadena un combo de 15.", "combo", 15, 1),
                A("com40", "Frenesí", "Encadena un combo de 40.", "combo", 40, 2),
                A("com100", "Imparable", "Encadena un combo de 100.", "combo", 100, 4),
                A("pod1", "Chispa Divina", "Usa un poder.", "powers", 1, 1),
                A("pod50", "Elegido", "Usa 50 poderes.", "powers", 50, 3),
                A("pod500", "Deidad", "Usa 500 poderes.", "powers", 500, 6),
                A("mus1", "Inspirada", "Atrapa una Musa.", "musas", 1, 1),
                A("mus25", "Coro de Musas", "Atrapa 25 Musas.", "musas", 25, 3),
                A("pol6", "Millonario", "Acumula 1M de polvo en total.", "dust", 1e6, 1),
                A("pol12", "Billonario", "Acumula 1T de polvo en total.", "dust", 1e12, 3),
                A("pol24", "Inconmensurable", "Acumula 1Sp de polvo en total.", "dust", 1e24, 6),
                A("pieza1", "Pieza 1/7", "Consigue la 1.ª pieza del Gran Monumento.", "pieces", 1, 3),
                A("pieza2", "Pieza 2/7", "Consigue 2 piezas del Gran Monumento.", "pieces", 2, 4),
                A("pieza3", "Pieza 3/7", "Consigue 3 piezas del Gran Monumento.", "pieces", 3, 5),
                A("pieza4", "Pieza 4/7", "Consigue 4 piezas del Gran Monumento.", "pieces", 4, 6),
                A("pieza5", "Pieza 5/7", "Consigue 5 piezas del Gran Monumento.", "pieces", 5, 7),
                A("pieza6", "Pieza 6/7", "Consigue 6 piezas del Gran Monumento.", "pieces", 6, 8),
                A("pieza7", "Pieza 7/7", "Completa las 7 piezas del Gran Monumento.", "pieces", 7, 10),
                A("victoria", "Obra Maestra", "Completa la campaña del Gran Monumento.", "campaign", 1, 10),
                A("consagra1", "Consagrado", "Consagra la canica por primera vez.", "consecrations", 1, 5),
                A("consagra5", "Relicario", "Consagra la canica 5 veces.", "consecrations", 5, 10),
            };

            mutators = new List<MutatorDef>
            {
                new MutatorDef { id = "sereno", displayName = "Camino Sereno", description = "Sin cambios. Pule como siempre.", safe = true },
                new MutatorDef { id = "petreo", displayName = "Mármol Pétreo", description = "Bloques ×2 de vida, polvo ×3.", hpMult = 2f, dustMult = 3f, essenceBonus = 0.15f, goalGalleries = 12 },
                new MutatorDef { id = "soledad", displayName = "Voto de Soledad", description = "Sin Acólitos. Discípulas ×3.", acolyteMult = 0f, allyMult = 3f, essenceBonus = 0.25f, goalGalleries = 11 },
                new MutatorDef { id = "furia", displayName = "Furia Ciega", description = "Todo golpe es crítico, pero sin Embestida.", alwaysCrit = true, noDash = true, essenceBonus = 0.2f, goalGalleries = 12 },
                new MutatorDef { id = "plegaria", displayName = "Plegaria Impaciente", description = "Poderes con media recarga, pero sin Supernova.", powerCooldownMult = 0.5f, disabledPower = "supernova", essenceBonus = 0.15f, goalGalleries = 12 },
                new MutatorDef { id = "volcan", displayName = "Lluvia de Volcánicos", description = "+35 % bloques volcánicos, bloques ×1,3 de vida.", volatileBonus = 0.35f, hpMult = 1.3f, essenceBonus = 0.25f, goalGalleries = 11 },
                new MutatorDef { id = "avaro", displayName = "Avaricia", description = "Polvo ×2, pero el Imán atrae a menos de la mitad.", dustMult = 2f, magnetMult = 0.4f, essenceBonus = 0.2f, goalGalleries = 12 },
                new MutatorDef { id = "fragil", displayName = "Mármol Quebradizo", description = "Bloques a mitad de vida, polvo ×0,4. Objetivo largo.", hpMult = 0.5f, dustMult = 0.4f, essenceBonus = 0.3f, goalGalleries = 14 },
            };

            lifeEvents = new List<LifeEventDef>
            {
                new LifeEventDef { id = "luna", displayName = "Luna de Ónice", description = "todo el mármol es oro", minGallery = 3, duration = 30, goldenChance = 1f },
                new LifeEventDef { id = "cristal", displayName = "Lluvia de Cristal", description = "meteoros de cristal caen del cielo", minGallery = 2, duration = 20, meteorEvery = 4, meteorCount = 4 },
                new LifeEventDef { id = "hora", displayName = "Hora Dorada", description = "polvo ×2,5", minGallery = 1, duration = 45, frenzyMult = 2.5f },
            };

            contracts = new List<ContractDef>
            {
                new ContractDef { id = "cadena", description = "Rompe {0} volcánicos de una explosión", kind = ContractKind.VolatileChain, target = 4, window = 1.2f, rewardIcor = 3 },
                new ContractDef { id = "combo", description = "Alcanza un combo de {0}", kind = ContractKind.Combo, target = 80, rewardIcor = 2 },
                new ContractDef { id = "rafaga", description = "Lanza {0} poderes en 10 s", kind = ContractKind.PowerBurst, target = 4, window = 10, rewardIcor = 3 },
                new ContractDef { id = "dorados", description = "Libera {0} estatuas doradas", kind = ContractKind.Golden, target = 3, rewardEssencePct = 0.1f },
                new ContractDef { id = "coloso", description = "Derrota a un Coloso", kind = ContractKind.Boss, target = 1, rewardIcor = 4 },
                new ContractDef { id = "estatuas", description = "Libera {0} estatuas en esta vida", kind = ContractKind.Statues, target = 80, rewardEssencePct = 0.15f },
            };

            campaign = new List<CampaignPieceDef>
            {
                new CampaignPieceDef { id = "base", displayName = "Base", room = "Santuario de Carrara" },
                new CampaignPieceDef { id = "columnas", displayName = "Columnas", room = "Cantera Volcánica" },
                new CampaignPieceDef { id = "torso", displayName = "Torso", room = "Gruta de Cristal" },
                new CampaignPieceDef { id = "brazos", displayName = "Brazos", room = "Catedral de Hielo" },
                new CampaignPieceDef { id = "rostro", displayName = "Rostro", room = "Forja de Hierro" },
                new CampaignPieceDef { id = "corona", displayName = "Corona", room = "Jardín de Rosa" },
                new CampaignPieceDef { id = "alma", displayName = "Alma", room = "Panteón Dorado" },
            };

            // ponytail: los materiales los asigna el menú aditivo de campaña (CampaignBuilder); null = el de Carrara
            marbleColors = new List<MarbleColorDef>
            {
                MC("carrara", "Carrara", ColorUnlock.Start, 0, "Inicio", new Color(1f, 0.95f, 0.85f), new Color(1f, 0.9f, 0.7f)),
                MC("rosa", "Rosa", ColorUnlock.Prestiges, 1, "Pule la canica por 1.ª vez", new Color(1f, 0.6f, 0.7f), new Color(1f, 0.55f, 0.7f)),
                MC("verde", "Verde", ColorUnlock.Trial, 1, "Supera la Prueba 1", new Color(0.45f, 1f, 0.6f), new Color(0.4f, 1f, 0.55f)),
                MC("rojo", "Rojo Volcánico", ColorUnlock.Trial, 2, "Supera la Prueba 2", new Color(1f, 0.35f, 0.15f), new Color(1f, 0.4f, 0.1f)),
                MC("violeta", "Violeta Cristal", ColorUnlock.Trial, 3, "Supera la Prueba 3", new Color(0.75f, 0.45f, 1f), new Color(0.7f, 0.4f, 1f)),
                MC("celeste", "Celeste Hielo", ColorUnlock.Trial, 4, "Supera la Prueba 4", new Color(0.55f, 0.9f, 1f), new Color(0.5f, 0.85f, 1f)),
                MC("nero", "Nero Obsidiana", ColorUnlock.Prestiges, 25, "Pule la canica 25 veces", new Color(0.5f, 0.5f, 0.6f), new Color(0.35f, 0.3f, 0.5f)),
                MC("dorado", "Dorado", ColorUnlock.Campaign, 0, "Completa la campaña", new Color(1f, 0.8f, 0.3f), new Color(1f, 0.75f, 0.2f)),
                MC("cosmica", "Cósmica", ColorUnlock.InfiniteGallery, 500, "Galería 500 en el Modo Infinito", new Color(0.5f, 0.6f, 1f), new Color(0.9f, 0.5f, 1f)),
                MC("hierro", "Hierro Forjado", ColorUnlock.Trial, 5, "Supera la Prueba 5", new Color(0.75f, 0.78f, 0.85f), new Color(1f, 0.55f, 0.2f)),
                MC("flor", "Flor de Jardín", ColorUnlock.Trial, 6, "Supera la Prueba 6", new Color(1f, 0.45f, 0.75f), new Color(0.5f, 1f, 0.45f)),
                MC("eterna", "Eterna", ColorUnlock.Fragments, 60, "Cómprala con 60 Fragmentos de Eternidad", new Color(0.4f, 1f, 0.95f), new Color(0.6f, 1f, 1f)),
            };

            relics = new List<RelicDef>
            {
                new RelicDef { id = "nova_racimo", displayName = "Supernova en Racimo", glyph = "✹", power = "supernova", description = "Tras estallar, la Supernova suelta 4 bombas menores a su alrededor." },
                new RelicDef { id = "vortice_gemelo", displayName = "Vórtice Escindido", glyph = "◎", power = "vortice", description = "Al implosionar, el Vórtice se divide en dos vórtices menores." },
                new RelicDef { id = "rayo_inverso", displayName = "Rayo Inverso", glyph = "↯", power = "rayo", description = "Cada rayo, al acabar su cadena, rebota hacia atrás por los mismos bloques." },
                new RelicDef { id = "meteoros_dorados", displayName = "Meteoros Dorados", glyph = "☄", power = "meteoros", description = "El Gran Meteoro convierte en oro 3 bloques." },
                new RelicDef { id = "coloso_sismico", displayName = "Pisada Sísmica", glyph = "⬤", power = "coloso", description = "Mientras eres Coloso, sueltas ondas que dañan todo alrededor." },
                new RelicDef { id = "ingravidez_iman", displayName = "Ingravidez Imantada", glyph = "⇡", power = "ingravidez", description = "La Ingravidez atrae todo el polvo del santuario y golpea ×1,5." },
                new RelicDef { id = "midas_avaro", displayName = "Corona Avara", glyph = "♛", power = "midas", description = "Midas desata además un Frenesí de polvo ×2 durante 10 s." },
                new RelicDef { id = "reloj_arena", displayName = "Reloj de Arena", glyph = "⌛", power = "", description = "Cada poder lanzado acorta un 15 % la recarga de los demás." },
            };

            bossMods = new List<BossModDef>
            {
                new BossModDef { id = "blindado", displayName = "Blindado", description = "vida ×2", hpMult = 2f, rewardMult = 1.5f, fragmentMult = 2 },
                new BossModDef { id = "regenerante", displayName = "Regenerante", description = "recupera vida sin parar", regenPerSec = 0.015f, rewardMult = 1.5f, fragmentMult = 2 },
                new BossModDef { id = "titanico", displayName = "Titánico", description = "enorme: vida ×1,6, polvo ×2", scale = 1.3f, hpMult = 1.6f, rewardMult = 2f, fragmentMult = 2 },
                new BossModDef { id = "sordo", displayName = "Sordo a los Dioses", description = "los poderes apenas le dañan", powerTaken = 0.3f, fragmentMult = 2 },
                new BossModDef { id = "coraza", displayName = "Acorazado", description = "tus golpes le hacen la mitad", playerTaken = 0.5f, fragmentMult = 2 },
            };
            map = null;
        }

        static UpgradeDef U(string id, string n, string d, string g, double c, double gr, int max, EffectMode m, float b, float p, string fmt, int unlock = 0)
            => new UpgradeDef { id = id, displayName = n, description = d, glyph = g, baseCost = c, costGrowth = gr, maxLevel = max, mode = m, effectBase = b, effectPerLevel = p, effectFormat = fmt, unlockGallery = unlock, currency = Currency.Dust };

        /// <summary>Mejora del Taller que requiere tener desbloqueado un nodo (p. ej. el poder en el Altar).</summary>
        static UpgradeDef P(UpgradeDef u, params string[] req) { u.requires = req; return u; }

        static UpgradeDef Cap(UpgradeDef u, float perPrestige, int limit) { u.maxLevelPerPrestige = perPrestige; u.maxLevelLimit = limit; return u; }
        static UpgradeDef Hito(UpgradeDef u, string stat, int threshold, string hint) { u.unlockStat = stat; u.unlockThreshold = threshold; u.unlockHint = hint; return u; }

        static MarbleColorDef MC(string id, string n, ColorUnlock u, int th, string hint, Color trail, Color wave)
            => new MarbleColorDef { id = id, displayName = n, unlock = u, threshold = th, hint = hint, trailColor = trail, waveColor = wave };

        static AchievementDef A(string id, string n, string d, string stat, double th, int icor)
            => new AchievementDef { id = id, displayName = n, description = d, stat = stat, threshold = th, rewardIcor = icor };

        static UpgradeDef G(string id, string n, string d, string g, double c, double gr, int max, EffectMode m, float b, float p, string fmt,
            float x, float y, int branch, params string[] req)
        {
            var u = E(id, n, d, g, c, gr, max, m, b, p, fmt, x, y, branch, req);
            u.currency = Currency.Icor;
            return u;
        }

        static UpgradeDef E(string id, string n, string d, string g, double c, double gr, int max, EffectMode m, float b, float p, string fmt,
            float x, float y, int branch, params string[] req)
        {
            var u = U(id, n, d, g, c, gr, max, m, b, p, fmt);
            u.currency = Currency.Essence;
            u.scaleCost = false;
            u.treePos = new Vector2(x, y);
            u.branch = branch;
            u.requires = req ?? new string[0];
            return u;
        }
    }
}
