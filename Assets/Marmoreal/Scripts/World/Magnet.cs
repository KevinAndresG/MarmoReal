using UnityEngine;

namespace Marmoreal
{
    /// <summary>Imán de la Forja de Hierro: atrae la canica cuando está dentro de su radio (más fuerte cerca del centro).
    /// Vive en los hijos de la sala, así que solo actúa con la sala activa. Editable en la escena.</summary>
    public class Magnet : MonoBehaviour
    {
        public float radius = 9f;
        [Tooltip("Aceleración máxima hacia el imán (m/s²)")]
        public float pull = 14f;

        void FixedUpdate()
        {
            var p = PlayerMarble.I;
            if (!p || (Hud.I && Hud.I.BlocksGameplay)) return;
            var rb = p.GetComponent<Rigidbody>();
            Vector3 d = transform.position - rb.position; d.y = 0;
            float dist = d.magnitude;
            if (dist > radius || dist < 0.3f) return;
            rb.AddForce(d / dist * pull * (1f - dist / radius), ForceMode.Acceleration);
        }

        void OnDrawGizmosSelected() { Gizmos.color = new Color(1f, 0.5f, 0.2f); Gizmos.DrawWireSphere(transform.position, radius); }
    }
}
