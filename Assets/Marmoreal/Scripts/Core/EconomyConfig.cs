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
        public double hpGrowthPerGallery = 1.8;
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
        public float frenzyMult = 3f, frenzyDuration = 15f;
        public float furyDuration = 10f;
        [Tooltip("Frenesí que deja la Supernova (polvo ×Codicia durante unos segundos)")]
        public float bombFrenzyDuration = 6f;

        [Header("Mecenazgos")]
        [Tooltip("Fama: +X polvo por cada mecenazgo conseguido (aditivo)")]
        public float famePerAchievement = 0.02f;

        public List<UpgradeDef> upgrades = new List<UpgradeDef>();
        public List<PowerDef> powers = new List<PowerDef>();
        public List<AchievementDef> achievements = new List<AchievementDef>();

        public PowerDef GetPower(string id)
        {
            foreach (var p in powers) if (p.id == id) return p;
            return null;
        }

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
                if (Get(u.id) == null) { upgrades.Add(u); changed = true; map = null; }
            foreach (var p in fresh.powers)
                if (GetPower(p.id) == null) { powers.Add(p); changed = true; }
            foreach (var a in fresh.achievements)
                if (achievements.Find(x => x.id == a.id) == null) { achievements.Add(a); changed = true; }
            if (Application.isPlaying) Destroy(fresh); else DestroyImmediate(fresh);
            return changed;
        }

        /// <summary>Valores por defecto. Solo se usan al crear el asset por primera vez.</summary>
        public void FillDefaults()
        {
            upgrades = new List<UpgradeDef>
            {
                U("masa", "Masa", "Cada impacto golpea más fuerte.", "●", 12, 1.5, 500, EffectMode.Exponential, 3f, 1.3f, "{0} daño"),
                U("inercia", "Inercia", "Velocidad máxima y aceleración.", "➤", 30, 1.75, 25, EffectMode.Linear, 11f, 0.75f, "{0} m/s"),
                U("iman", "Imán", "Radio de atracción del polvo.", "◎", 20, 1.6, 25, EffectMode.Linear, 5f, 0.9f, "{0} m"),
                U("pureza", "Pureza", "Más polvo por cada fragmento.", "✦", 45, 1.75, 500, EffectMode.Exponential, 1f, 1.2f, "×{0}"),
                U("fortuna", "Fortuna", "Probabilidad de golpe crítico (×3).", "★", 150, 1.9, 18, EffectMode.Linear, 0.05f, 0.025f, "{0}", 1),
                U("cantera", "Cantera", "Más bloques de mármol a la vez.", "▣", 80, 2.15, 12, EffectMode.Linear, 5f, 1f, "{0} bloques", 1),
                U("acolitos", "Acólitos", "Esferas que tallan por ti, incluso sin mirar.", "○", 150, 3.0, 12, EffectMode.Linear, 0f, 1f, "{0} acólitos", 2),
                U("cincel", "Cincel", "Daño de los acólitos.", "⚒", 220, 1.6, 500, EffectMode.Exponential, 1.6f, 1.28f, "{0} daño", 2),
                U("resonancia", "Resonancia", "Poder y radio de la onda [F].", "≋", 300, 1.7, 60, EffectMode.Linear, 1f, 0.35f, "×{0}", 3),

                // ---------- MEJORAS DE PODERES (Polvo; aparecen al desbloquear el poder en el Altar y se pierden al Pulir) ----------
                P(U("pw_supernova", "Pólvora Divina", "Daño de la Supernova [1]. Empieza flojo: súbelo en cada vida.", "✹", 400, 2.2, 20, EffectMode.Exponential, 0.25f, 1.1f, "×{0} daño"), "supernova"),
                P(U("pw_ingravidez", "Gravedad Rota", "Daño del impacto de la Ingravidez [2].", "⇡", 900, 2.2, 20, EffectMode.Exponential, 0.25f, 1.1f, "×{0} daño"), "ingravidez"),
                P(U("pw_meteoros", "Núcleo Ígneo", "Daño de cada meteorito [3].", "☄", 900, 2.2, 20, EffectMode.Exponential, 0.25f, 1.1f, "×{0} daño"), "meteoros"),
                P(U("pw_coloso", "Gigantismo", "Duración del Coloso [4].", "⬤", 2500, 2.1, 15, EffectMode.Linear, 4f, 0.6f, "{0} s"), "coloso"),
                P(U("pw_vortice", "Singularidad", "Daño del Vórtice [5].", "◎", 4000, 2.2, 20, EffectMode.Exponential, 0.25f, 1.1f, "×{0} daño"), "vortice"),
                P(U("pw_midas", "Alquimia", "Bloques que dora el Toque de Midas [6].", "♛", 20000, 8f, 4, EffectMode.Linear, 1f, 1f, "{0} bloques"), "midas"),

                // ---------- ÁRBOL DE HABILIDADES (Esencia) ----------
                // Raíz
                E("brillo", "Brillo Eterno", "Multiplica TODO el polvo y el daño. Sin límite.", "☀", 1, 1.55, 300, EffectMode.Exponential, 1f, 1.25f, "×{0}", 0, 0, 0),
                // Rama MASA (izquierda)
                E("fuerza", "Fuerza Ancestral", "Daño de impacto de la canica.", "●", 2, 1.7, 60, EffectMode.Exponential, 1f, 1.4f, "×{0}", -1.3f, 1, 1, "brillo"),
                E("ojo", "Ojo Clínico", "Los críticos multiplican más.", "✧", 3, 2.2, 5, EffectMode.Linear, 0f, 1f, "+{0} crítico", -2.5f, 1.5f, 1, "fuerza"),
                E("rebote", "Rebote Vivo", "MECÁNICA: al chocar un bloque sales despedida con +40% de velocidad. Encadena golpes como un pinball.", "↯", 4, 1, 1, EffectMode.Linear, 0f, 1f, "{0}", -1.3f, 2, 1, "fuerza"),
                E("sismo", "Embestida Sísmica", "MECÁNICA: los impactos durante la Embestida liberan una mini-onda que talla los bloques cercanos.", "✺", 8, 1, 1, EffectMode.Linear, 0f, 1f, "{0}", -2.5f, 2.6f, 1, "rebote"),
                E("metralla", "Metralla", "MECÁNICA: los trozos que saltan dañan a otros bloques al chocar. Reacciones en cadena.", "⁂", 12, 1, 1, EffectMode.Linear, 0f, 1f, "{0}", -1.3f, 3, 1, "rebote"),
                // Rama ORO (centro)
                E("veta", "Veta de Oro", "Estatuas y bloques dorados valen más.", "♛", 2, 1.8, 100, EffectMode.Exponential, 1f, 1.5f, "×{0}", 0, 1, 0, "brillo"),
                E("fiebre", "Fiebre Dorada", "Más bloques dorados.", "✪", 3, 1.9, 8, EffectMode.Linear, 0f, 0.015f, "+{0}", 0, 2, 0, "veta"),
                E("memoria", "Memoria del Escultor", "Tras pulir empiezas en una galería más avanzada.", "⌂", 5, 2.0, 10, EffectMode.Linear, 0f, 1f, "+{0} galerías", 0, 3, 0, "fiebre"),
                // Rama ESPÍRITU (derecha)
                E("eterno", "Acólito Eterno", "Acólitos gratis que nunca se pierden.", "◌", 2, 2.4, 6, EffectMode.Linear, 0f, 1f, "+{0}", 1.3f, 1, 2, "brillo"),
                E("coro", "Coro de Acólitos", "Daño de los acólitos.", "♫", 3, 1.75, 60, EffectMode.Exponential, 1f, 1.4f, "×{0}", 2.5f, 1.5f, 2, "eterno"),
                E("paciencia", "Paciencia", "Progreso offline más eficiente.", "⌛", 2, 2.0, 5, EffectMode.Linear, 0.25f, 0.15f, "{0}", 1.3f, 2, 2, "eterno"),
                E("impulso", "Impulso", "Recarga de la Embestida más rápida.", "»", 3, 2.0, 5, EffectMode.Linear, 2.4f, -0.3f, "{0}s", 2.5f, 2.6f, 2, "paciencia"),
                E("eco", "Eco Resonante", "MECÁNICA: la onda [F] se repite dos veces más, cada eco más amplio.", "≋", 10, 1, 1, EffectMode.Linear, 0f, 1f, "{0}", 1.3f, 3, 2, "paciencia"),
                E("perpetua", "Resonancia Perpetua", "MECÁNICA: la onda se libera sola al llenarse.", "∞", 14, 1, 1, EffectMode.Linear, 0f, 1f, "{0}", 2.5f, 3.6f, 2, "eco"),
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
                G("meteoros", "Lluvia de Meteoros", "PODER [3]: meteoritos de mármol caen sobre los bloques.", "☄", 7, 1, 1, EffectMode.Linear, 0, 1, "{0}", -1f, 0, 3),
                G("enjambre", "Enjambre", "Más meteoritos por lluvia.", "⁘", 4, 1.8, 8, EffectMode.Linear, 0f, 3f, "+{0}", -1f, 1.25f, 2, "meteoros"),
                G("meteoros_cd", "Cielo Abierto", "Recarga de la Lluvia de Meteoros.", "⧗", 4, 1.9, 6, EffectMode.Linear, 1f, -0.07f, "×{0}", -1f, 2.5f, 2, "enjambre"),
                // Coloso (x 0)
                G("coloso", "Coloso", "PODER [4]: tu canica se vuelve gigante y aplasta todo (×3 daño).", "⬤", 15, 1, 1, EffectMode.Linear, 0, 1, "{0}", 0f, 0, 3),
                G("titan", "Titán", "El Coloso dura más.", "♚", 5, 1.8, 8, EffectMode.Linear, 0f, 2f, "+{0}s", 0f, 1.25f, 1, "coloso"),
                G("coloso_cd", "Crecimiento Veloz", "Recarga del Coloso.", "⧗", 6, 1.9, 6, EffectMode.Linear, 1f, -0.07f, "×{0}", 0f, 2.5f, 1, "titan"),
                // Vórtice (x 1)
                G("vortice", "Vórtice", "PODER [5]: un agujero negro tritura los bloques cercanos y absorbe todo el polvo.", "◎", 20, 1, 1, EffectMode.Linear, 0, 1, "{0}", 1f, 0, 3),
                G("horizonte", "Horizonte de Sucesos", "Fuerza y radio del Vórtice.", "◍", 5, 1.8, 10, EffectMode.Exponential, 1f, 1.3f, "×{0}", 1f, 1.25f, 2, "vortice"),
                G("vortice_cd", "Colapso", "Recarga del Vórtice.", "⧗", 7, 1.9, 6, EffectMode.Linear, 1f, -0.07f, "×{0}", 1f, 2.5f, 2, "horizonte"),
                // Midas (x 2)
                G("midas", "Toque de Midas", "PODER [6]: convierte bloques en ORO macizo.", "♛", 40, 1, 1, EffectMode.Linear, 0, 1, "{0}", 2f, 0, 3),
                G("reymidas", "Rey Midas", "El Toque de Midas dora más bloques.", "♕", 8, 2.0, 6, EffectMode.Linear, 0f, 1f, "+{0}", 2f, 1.25f, 0, "midas"),
                G("midas_cd", "Avaricia", "Recarga del Toque de Midas.", "⧗", 10, 1.9, 6, EffectMode.Linear, 1f, -0.07f, "×{0}", 2f, 2.5f, 0, "reymidas"),
                // Musas (x 3)
                G("favor", "Favor de las Musas", "Las Musas aparecen más a menudo.", "♪", 2, 1.9, 5, EffectMode.Linear, 1f, -0.1f, "×{0} espera", 3f, 0, 0),
                G("inspiracion", "Inspiración", "Las bendiciones de las Musas duran más.", "❦", 3, 1.8, 6, EffectMode.Linear, 1f, 0.25f, "×{0}", 3f, 1.25f, 0, "favor"),
                // Generales (afectan a todos los poderes)
                G("ira", "Ira Divina", "Daño de TODOS los poderes. Sin límite.", "ϟ", 4, 1.6, 200, EffectMode.Exponential, 1f, 1.25f, "×{0}", -0.75f, 3.9f, 0),
                G("eternidad", "Eternidad", "Recarga de TODOS los poderes.", "∞", 20, 2.2, 8, EffectMode.Linear, 1f, -0.05f, "×{0}", 0.75f, 3.9f, 0),
            };

            powers = new List<PowerDef>
            {
                new PowerDef { id = "supernova", displayName = "Supernova", glyph = "✹", description = "Bomba de mármol: arrasa en área y desata un Frenesí de polvo.", cooldown = 120, potency = 45, radius = 12, duration = 1.6f },
                new PowerDef { id = "ingravidez", displayName = "Ingravidez", glyph = "⇡", description = "Todos los bloques levitan y se estrellan.", cooldown = 150, potency = 60, radius = 60, duration = 2.2f },
                new PowerDef { id = "meteoros", displayName = "Meteoros", glyph = "☄", description = "Meteoritos sobre los bloques.", cooldown = 90, potency = 20, count = 10, duration = 3f },
                new PowerDef { id = "coloso", displayName = "Coloso", glyph = "⬤", description = "Canica gigante, ×3 daño.", cooldown = 80, potency = 3, radius = 2.2f, duration = 10 },
                new PowerDef { id = "vortice", displayName = "Vórtice", glyph = "◎", description = "Agujero negro que tritura y absorbe.", cooldown = 120, potency = 15, radius = 9, duration = 6 },
                new PowerDef { id = "midas", displayName = "Midas", glyph = "♛", description = "Convierte bloques en oro.", cooldown = 140, count = 2 },
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
            };
            map = null;
        }

        static UpgradeDef U(string id, string n, string d, string g, double c, double gr, int max, EffectMode m, float b, float p, string fmt, int unlock = 0)
            => new UpgradeDef { id = id, displayName = n, description = d, glyph = g, baseCost = c, costGrowth = gr, maxLevel = max, mode = m, effectBase = b, effectPerLevel = p, effectFormat = fmt, unlockGallery = unlock, currency = Currency.Dust };

        /// <summary>Mejora del Taller que requiere tener desbloqueado un nodo (p. ej. el poder en el Altar).</summary>
        static UpgradeDef P(UpgradeDef u, params string[] req) { u.requires = req; return u; }

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
            u.treePos = new Vector2(x, y);
            u.branch = branch;
            u.requires = req ?? new string[0];
            return u;
        }
    }
}
