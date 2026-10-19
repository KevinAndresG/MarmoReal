using System;
using System.Collections.Generic;

namespace Marmoreal
{
    [Serializable]
    public class UpgradeLevel
    {
        public string id;
        public int level;
    }

    /// <summary>Estado persistente (JSON en PlayerPrefs).</summary>
    [Serializable]
    public class GameState
    {
        public int version = 2; // 1 = antes de la campaña (S4)
        public double dust;
        public double runDust;
        public double lifetimeDust;
        public double essence;
        public double lifetimeEssence;
        public int galleryLevel;
        public int statuesThisGallery;
        public int totalStatues;
        public int goldenFound;
        public int prestigeCount;
        public double bestHit;
        public long lastSaveUnix;
        public float playSeconds;

        // Ícor, mecenazgos y estadísticas
        public double icor;
        public double lifetimeIcor;
        public int maxGallery;
        public int bestCombo;
        public int powersUsed;
        public int musasCaught;
        public List<string> achievements = new List<string>();
        public List<UpgradeLevel> levels = new List<UpgradeLevel>();

        // Vida actual (se reinicia al Pulir) y resumen de la anterior
        public int lifeStartGallery;
        public int runVolatile;
        public int lifeStatues;
        public float lifeStartSeconds;
        public List<string> hitosSeen = new List<string>();
        public float lastLifeSeconds;
        public int lastLifeStatues;
        public int lastLifeGalleries;
        public double lastLifeDust;
        public double lastLifeBestHit;
        public string biomeId = "carrara"; // sala actual (BiomeDef.id); partidas antiguas → carrara

        // Mutadores, jefes y contratos (S3)
        public string mutatorId = "";                 // MutatorDef.id de la vida actual ("" = ninguno)
        public List<string> mutatorOffer = new List<string>(); // cartas ofrecidas al último Pulido
        public int bossDoneGallery = -1;              // última galería cuyo Coloso cayó
        public int lifeBosses;
        public int lifeGolden;
        public List<string> contracts = new List<string>();
        public List<string> contractsDone = new List<string>();

        // Campaña: El Gran Monumento (S4)
        public int legacyStage = -1;                  // migración: salas ya visitadas antes de la S4 (campaignStage <= esto)
        public List<int> pieces = new List<int>();    // piezas conseguidas (= campaignStage de cada Prueba superada)
        public List<string> colors = new List<string>(); // canicas desbloqueadas (MarbleColorDef.id)
        public string marbleColor = "";
        public bool campaignWon;
        public float campaignSeconds;
        public bool victoryShown;

        // Modo Infinito
        public bool infinite;
        public int infiniteLives;
        public int infBestGallery;
        public float infSeconds;
        public double infBestHit;
        public double infBestDps;

        // Ajustes
        public int quality = -1;
        public bool muted;
        public float cameraSensitivity = 1f;
        public bool tutorialDone;

        public int Level(string id)
        {
            for (int i = 0; i < levels.Count; i++) if (levels[i].id == id) return levels[i].level;
            return 0;
        }

        public void SetLevel(string id, int lvl)
        {
            for (int i = 0; i < levels.Count; i++)
                if (levels[i].id == id) { levels[i].level = lvl; return; }
            levels.Add(new UpgradeLevel { id = id, level = lvl });
        }
    }
}
