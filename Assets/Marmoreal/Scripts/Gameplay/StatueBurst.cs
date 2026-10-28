using UnityEngine;

namespace Marmoreal
{
    /// <summary>
    /// Onda de vapor/distorsión al liberar una estatua: quads que miran a la cámara con material de distorsión (M_Vapor),
    /// se expanden y se apagan. Quads y material son hijos/assets editables; solo se anima la escala y la intensidad.
    /// </summary>
    public class StatueBurst : MonoBehaviour
    {
        static StatueBurst _i;
        public static StatueBurst I { get { if (!_i) _i = FindFirstObjectByType<StatueBurst>(); return _i; } }

        [Tooltip("Quads hijos (desactivados) reutilizados; todos con M_Vapor")]
        public Renderer[] quads;
        public float duration = 0.8f;
        [Tooltip("Fuerza de la distorsión (HDRP _DistortionScale)")]
        public float distortion = 200f;
        [System.NonSerialized] float[] age, size;
        [System.NonSerialized] Vector3[] pos;
        [System.NonSerialized] MaterialPropertyBlock mpb;
        [System.NonSerialized] int next;

        public void Play(Vector3 at, float radius)
        {
            if (quads == null || quads.Length == 0) return;
            if (age == null || age.Length != quads.Length) { age = new float[quads.Length]; size = new float[quads.Length]; pos = new Vector3[quads.Length]; for (int i = 0; i < age.Length; i++) age[i] = -1; }
            int k = next; next = (next + 1) % quads.Length;
            age[k] = 0; size[k] = radius * 2f; pos[k] = at;
            quads[k].gameObject.SetActive(true);
        }

        void Update()
        {
            if (age == null) return;
            var cam = Camera.main;
            mpb ??= new MaterialPropertyBlock();
            for (int i = 0; i < age.Length; i++)
            {
                if (age[i] < 0) continue;
                age[i] += Time.deltaTime;
                float k = age[i] / duration;
                if (k >= 1f) { age[i] = -1; quads[i].gameObject.SetActive(false); continue; }
                float e = 1f - (1f - k) * (1f - k);
                var t = quads[i].transform;
                t.position = pos[i];
                if (cam) t.rotation = cam.transform.rotation;
                t.localScale = Vector3.one * Mathf.Max(0.05f, size[i] * (0.25f + 0.75f * e));
                float fade = (1f - k) * (1f - k);
                quads[i].GetPropertyBlock(mpb);
                mpb.SetFloat("_DistortionScale", distortion * fade);
                mpb.SetColor("_UnlitColor", new Color(1f, 0.95f, 0.85f, 0.06f * fade));
                quads[i].SetPropertyBlock(mpb);
            }
        }
    }
}
