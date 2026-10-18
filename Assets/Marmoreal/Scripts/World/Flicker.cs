using UnityEngine;

namespace Marmoreal
{
    /// <summary>Parpadeo cálido para las luces de los braseros (barato: solo intensidad).</summary>
    [RequireComponent(typeof(Light))]
    public class Flicker : MonoBehaviour
    {
        public float amount = 0.18f;
        public float speed = 7f;
        Light l;
        float baseIntensity, seed;

        void Awake()
        {
            l = GetComponent<Light>();
            baseIntensity = l.intensity;
            seed = Random.value * 100f;
        }

        void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * speed) - 0.5f;
            l.intensity = baseIntensity * (1f + n * 2f * amount);
        }
    }
}
