using UnityEngine;

namespace Marmoreal
{
    /// <summary>Gira / flota / pulsa un objeto decorativo (anillos de Musas, vórtice, bomba).</summary>
    public class Spinner : MonoBehaviour
    {
        public Vector3 degreesPerSecond = new Vector3(0, 90, 0);
        public float bobAmplitude;
        public float bobSpeed = 2f;
        public float pulseAmount;
        public float pulseSpeed = 4f;

        Vector3 basePos, baseScale;
        float seed;

        void OnEnable()
        {
            basePos = transform.localPosition;
            baseScale = transform.localScale;
            seed = Random.value * 10f;
        }

        void Update()
        {
            transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
            if (bobAmplitude > 0f)
                transform.localPosition = basePos + Vector3.up * Mathf.Sin((Time.time + seed) * bobSpeed) * bobAmplitude;
            if (pulseAmount > 0f)
                transform.localScale = baseScale * (1f + Mathf.Sin((Time.time + seed) * pulseSpeed) * pulseAmount);
        }
    }
}
