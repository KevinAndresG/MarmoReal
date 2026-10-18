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
        public int version = 1;
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
