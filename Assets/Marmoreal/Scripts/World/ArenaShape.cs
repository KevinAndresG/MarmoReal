using UnityEngine;

namespace Marmoreal
{
    /// <summary>
    /// Perfil analítico del cuenco de mármol. El mesh del suelo se generó con estos valores
    /// (Assets/Marmoreal/Meshes/ArenaFloor.asset) y los FX lo usan para rebotar sin físicas.
    /// </summary>
    public static class ArenaShape
    {
        public const float FlatRadius = 22f;
        public const float BowlRadius = 34f;
        public const float RimHeight = 3.2f;
        public const float TerraceRadius = 42f;
        public const float SpawnRadius = 18f;

        public static float Height(float r)
        {
            if (r <= FlatRadius) return 0f;
            if (r >= BowlRadius) return RimHeight;
            float t = (r - FlatRadius) / (BowlRadius - FlatRadius);
            return RimHeight * t * t * (3f - 2f * t);
        }

        public static float Height(Vector3 p) => Height(Mathf.Sqrt(p.x * p.x + p.z * p.z));
    }
}
