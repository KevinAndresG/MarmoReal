using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Marmoreal
{
    public enum BiomeRule
    {
        None,
        /// <summary>Lava: quieta (vel &lt; ruleA) durante ruleB s → la lava te lanza y pierdes el combo.</summary>
        LavaFloor,
        /// <summary>Cristal: cada golpe tuyo refleja un rayo (prob. ruleA, ruleB × daño) y los trozos son metralla gratis (ruleC × daño de impacto).</summary>
        CrystalReflect,
        /// <summary>Hielo: suelo resbaladizo. ruleA × aceleración (cuesta frenar/girar), ruleB × velocidad máxima, ruleC = amortiguación lineal de la canica.</summary>
        IceFloor,
        /// <summary>Forja: bloques de metal (hpMult/dustMult de la sala) e imanes en el suelo (componente Magnet en los hijos de la sala).</summary>
        IronForge,
        /// <summary>Jardín: cada ruleA s sin romperlo, un bloque crece ×ruleB (HP y polvo), hasta ruleC veces.</summary>
        GardenGrowth,
        /// <summary>Panteón: +ruleA a la probabilidad de bloque grande; la Prueba es "El Coloso de Mármol" con HP ×ruleB.</summary>
        GoldenPantheon,
    }

    [Serializable]
    public class MaterialSwap
    {
        public Material from;
        public Material to;
    }

    /// <summary>Sala/bioma: look (materiales, luz, volumen) + UNA regla. Assets en Config/Biomes/.</summary>
    [CreateAssetMenu(menuName = "Marmoreal/Bioma", fileName = "Biome_")]
    public class BiomeDef : ScriptableObject
    {
        public string id = "carrara";
        public string displayName = "Santuario de Carrara";
        [TextArea] public string tagline = "";
        [Tooltip("Orden en la campaña (S4). 0 = primera sala")]
        public int campaignStage;
        [Tooltip("Multiplicadores de economía de la sala (campaña S4)")]
        public float hpMult = 1f, dustMult = 1f;
        [Tooltip("Prueba de la sala: al llegar a esta Galería (nº visible, 1-based) aparece el jefe. 0 = sin Prueba")]
        public int trialGallery;

        [Header("Bloques (mismo nº que los tiers base; vacío = los de la escena)")]
        public Material[] chunkMaterials;
        [Tooltip("Polvo emisivo por tier + dorado al final (vacío = el base)")]
        public Material[] shardMaterials;

        [Header("Sala: materiales de --- SANTUARIO --- reemplazados (suelo, pedestales, columnas…)")]
        public List<MaterialSwap> swaps = new List<MaterialSwap>();

        [Header("Luz y ambiente")]
        public Color sunColor = Color.white;
        [Tooltip("Lux del Sol (Carrara 2600)")]
        public float sunIntensity = 2600f;
        [Tooltip("Cielo, niebla, tinte (vacío = el perfil de la escena)")]
        public VolumeProfile volumeProfile;

        [Header("Regla")]
        public BiomeRule rule;
        public float ruleA, ruleB, ruleC;
        [Tooltip("Suma a la probabilidad de bloque volcánico (desde la galería 0)")]
        public float volatileBonus;
    }
}
