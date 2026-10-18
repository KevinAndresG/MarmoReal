using UnityEngine;

namespace Marmoreal
{
    public enum Sfx { Impact, Chip, Tink, Statue, Shatter, Boom, Dash, Buy, GalleryUp, Prestige, Golden, Deny, Count }

    /// <summary>
    /// Audio del juego. Cada clip puede asignarse en el inspector; si está vacío se sintetiza
    /// proceduralmente al arrancar (sin assets externos). Sonidos 2D con reverb de sala.
    /// </summary>
    public class AudioFX : MonoBehaviour
    {
        static AudioFX _i;
        /// <summary>Singleton resistente a recargas de dominio (se re-busca si se pierde).</summary>
        public static AudioFX I { get { if (!_i) _i = FindFirstObjectByType<AudioFX>(); return _i; } private set => _i = value; }

        [Tooltip("Clips opcionales, en el orden del enum Sfx. Vacío = sintetizado.")]
        public AudioClip[] overrides = new AudioClip[(int)Sfx.Count];
        public AudioClip rollLoopOverride;
        public AudioClip ambientOverride;
        [Range(0, 1)] public float masterVolume = 0.8f;
        [Range(0, 1)] public float ambientVolume = 0.16f;
        public int voices = 14;

        AudioClip[] clips;
        AudioSource[] pool;
        int next;
        AudioSource roll, ambient;
        float lastTink;
        int tinkStreak;
        float tinkStreakTimer;
        bool muted;

        // Escala pentatónica para la "melodía" de recolección de polvo
        static readonly float[] Penta = { 1f, 1.125f, 1.25f, 1.5f, 1.6667f, 2f, 2.25f, 2.5f, 3f, 3.3333f, 4f };

        const int SR = 44100;

        void Awake()
        {
            I = this;
            clips = new AudioClip[(int)Sfx.Count];
            for (int i = 0; i < clips.Length; i++)
                clips[i] = overrides != null && i < overrides.Length && overrides[i] ? overrides[i] : Synth((Sfx)i);

            pool = new AudioSource[voices];
            for (int i = 0; i < voices; i++)
            {
                pool[i] = gameObject.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
                pool[i].spatialBlend = 0;
            }
            roll = gameObject.AddComponent<AudioSource>();
            roll.clip = rollLoopOverride ? rollLoopOverride : MakeRoll();
            roll.loop = true; roll.volume = 0; roll.playOnAwake = false;
            roll.Play();

            ambient = gameObject.AddComponent<AudioSource>();
            ambient.clip = ambientOverride ? ambientOverride : MakePad();
            ambient.loop = true; ambient.volume = ambientVolume * masterVolume; ambient.playOnAwake = false;
            ambient.Play();
        }

        void Update()
        {
            tinkStreakTimer -= Time.unscaledDeltaTime;
            if (tinkStreakTimer <= 0) tinkStreak = 0;
        }

        public void SetMuted(bool m)
        {
            muted = m;
            AudioListener.volume = m ? 0 : 1;
        }

        public void Play(Sfx s, float volume = 1f, float pitch = 1f)
        {
            if (muted || clips == null) return;
            var src = pool[next];
            next = (next + 1) % pool.Length;
            src.pitch = pitch;
            src.PlayOneShot(clips[(int)s], volume * masterVolume);
        }

        /// <summary>Nota de recolección: sube por la escala pentatónica mientras recoges seguido.</summary>
        public void Tink()
        {
            if (Time.unscaledTime - lastTink < 0.035f) return;
            lastTink = Time.unscaledTime;
            float p = Penta[tinkStreak % Penta.Length] * (tinkStreak >= Penta.Length ? 1.0f : 1f);
            tinkStreak = Mathf.Min(tinkStreak + 1, Penta.Length - 1);
            tinkStreakTimer = 0.6f;
            Play(Sfx.Tink, 0.32f, p * 0.75f);
        }

        public void SetRoll(float speed01, bool grounded)
        {
            if (!roll) return;
            float target = grounded ? Mathf.Clamp01(speed01) * 0.35f * masterVolume : 0f;
            roll.volume = Mathf.MoveTowards(roll.volume, target, Time.deltaTime * 2f);
            roll.pitch = 0.6f + speed01 * 0.9f;
        }

        // ================================================================= Síntesis
        static float Env(float t, float a, float d) => t < a ? t / a : Mathf.Exp(-(t - a) * d);

        static AudioClip Clip(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, SR, false);
            c.SetData(data, 0);
            return c;
        }

        static float Bell(float f, float t) =>
            Mathf.Sin(2 * Mathf.PI * f * t) + 0.45f * Mathf.Sin(2 * Mathf.PI * f * 2.76f * t) * Mathf.Exp(-t * 6) + 0.25f * Mathf.Sin(2 * Mathf.PI * f * 5.4f * t) * Mathf.Exp(-t * 12);

        static AudioClip Synth(Sfx s)
        {
            var rnd = new System.Random(42 + (int)s);
            float N() => (float)(rnd.NextDouble() * 2 - 1);
            float[] d;
            switch (s)
            {
                case Sfx.Impact:
                {
                    d = new float[(int)(SR * 0.45f)];
                    float ph = 0, lp = 0;
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)SR;
                        float f = Mathf.Lerp(95, 42, Mathf.Clamp01(t / 0.25f));
                        ph += 2 * Mathf.PI * f / SR;
                        lp += (N() - lp) * 0.08f;
                        d[i] = (Mathf.Sin(ph) * Env(t, 0.002f, 9f) * 0.9f + lp * Env(t, 0.001f, 30f) * 1.6f) * 0.8f;
                    }
                    break;
                }
                case Sfx.Chip:
                {
                    d = new float[(int)(SR * 0.18f)];
                    float hp = 0, prev = 0;
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)SR;
                        float n = N();
                        hp = 0.85f * (hp + n - prev); prev = n;
                        d[i] = (hp * Env(t, 0.001f, 38f) * 0.7f + Mathf.Sin(2 * Mathf.PI * 2300 * t) * Env(t, 0.001f, 60f) * 0.25f);
                    }
                    break;
                }
                case Sfx.Tink:
                {
                    d = new float[(int)(SR * 0.4f)];
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)SR;
                        d[i] = Bell(1046.5f, t) * Env(t, 0.001f, 14f) * 0.5f;
                    }
                    break;
                }
                case Sfx.Statue:
                {
                    d = new float[(int)(SR * 2.2f)];
                    float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
                    for (int k = 0; k < notes.Length; k++)
                    {
                        int off = (int)(SR * 0.07f * k);
                        for (int i = off; i < d.Length; i++)
                        {
                            float t = (i - off) / (float)SR;
                            d[i] += Bell(notes[k], t) * Env(t, 0.002f, 2.4f) * 0.22f;
                        }
                    }
                    break;
                }
                case Sfx.Shatter:
                {
                    d = new float[(int)(SR * 0.9f)];
                    float hp = 0, prev = 0;
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)SR; float n = N();
                        hp = 0.7f * (hp + n - prev); prev = n;
                        d[i] = hp * Env(t, 0.001f, 7f) * 0.55f;
                    }
                    for (int k = 0; k < 14; k++)
                    {
                        int off = rnd.Next(0, SR / 3);
                        float f = 1500 + (float)rnd.NextDouble() * 3500;
                        for (int i = off; i < d.Length; i++)
                        {
                            float t = (i - off) / (float)SR;
                            d[i] += Mathf.Sin(2 * Mathf.PI * f * t) * Env(t, 0.0005f, 45f) * 0.12f;
                        }
                    }
                    break;
                }
                case Sfx.Boom:
                {
                    d = new float[(int)(SR * 1.6f)];
                    float ph = 0, lp = 0;
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)SR;
                        ph += 2 * Mathf.PI * Mathf.Lerp(70, 28, Mathf.Clamp01(t / 0.9f)) / SR;
                        lp += (N() - lp) * 0.03f;
                        d[i] = Mathf.Sin(ph) * Env(t, 0.005f, 2.8f) * 0.95f + lp * Env(t, 0.002f, 4f) * 2.2f
                             + Bell(392f, t) * Env(t, 0.01f, 3f) * 0.12f;
                    }
                    break;
                }
                case Sfx.Dash:
                {
                    d = new float[(int)(SR * 0.45f)];
                    float lp = 0;
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)SR;
                        float k = Mathf.Lerp(0.02f, 0.35f, t / 0.45f);
                        lp += (N() - lp) * k;
                        d[i] = lp * Mathf.Sin(Mathf.PI * t / 0.45f) * 0.9f;
                    }
                    break;
                }
                case Sfx.Buy:
                {
                    d = new float[(int)(SR * 0.35f)];
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)SR;
                        float f = t < 0.07f ? 1318.5f : 1975.5f;
                        float tt = t < 0.07f ? t : t - 0.07f;
                        d[i] = Bell(f, tt) * Env(tt, 0.001f, 16f) * 0.28f;
                    }
                    break;
                }
                case Sfx.GalleryUp:
                {
                    d = new float[(int)(SR * 2.4f)];
                    float[] notes = { 392f, 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f };
                    for (int k = 0; k < notes.Length; k++)
                    {
                        int off = (int)(SR * 0.09f * k);
                        for (int i = off; i < d.Length; i++)
                        {
                            float t = (i - off) / (float)SR;
                            d[i] += Bell(notes[k], t) * Env(t, 0.002f, 2.2f) * 0.18f;
                        }
                    }
                    break;
                }
                case Sfx.Prestige:
                {
                    d = new float[(int)(SR * 4f)];
                    float[] notes = { 130.81f, 196f, 261.63f, 329.63f, 392f, 523.25f, 783.99f };
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)SR;
                        float v = 0;
                        for (int k = 0; k < notes.Length; k++)
                            v += Mathf.Sin(2 * Mathf.PI * notes[k] * t + k) * (1 + 0.3f * Mathf.Sin(t * 5 + k));
                        d[i] = v / notes.Length * Env(t, 0.6f, 0.9f) * 0.5f;
                    }
                    break;
                }
                case Sfx.Golden:
                {
                    d = new float[(int)(SR * 1.4f)];
                    for (int k = 0; k < 22; k++)
                    {
                        int off = (int)(SR * 0.04f * k);
                        float f = 1568f * Penta[rnd.Next(0, 7)];
                        for (int i = off; i < d.Length; i++)
                        {
                            float t = (i - off) / (float)SR;
                            d[i] += Bell(f, t) * Env(t, 0.001f, 9f) * 0.07f;
                        }
                    }
                    break;
                }
                default: // Deny
                {
                    d = new float[(int)(SR * 0.18f)];
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)SR;
                        d[i] = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 140 * t)) * Env(t, 0.001f, 20f) * 0.12f;
                    }
                    break;
                }
            }
            return Clip(s.ToString(), d);
        }

        static AudioClip MakeRoll()
        {
            var rnd = new System.Random(7);
            var d = new float[SR * 2];
            float b = 0, lp = 0;
            for (int i = 0; i < d.Length; i++)
            {
                b = Mathf.Clamp(b + (float)(rnd.NextDouble() * 2 - 1) * 0.04f, -1, 1);
                lp += (b - lp) * 0.05f;
                d[i] = lp * 1.4f;
            }
            // fundido cruzado del bucle
            int x = SR / 10;
            for (int i = 0; i < x; i++) { float k = i / (float)x; d[i] = d[i] * k + d[d.Length - x + i] * (1 - k); }
            System.Array.Resize(ref d, d.Length - x);
            return Clip("RollLoop", d);
        }

        static AudioClip MakePad()
        {
            const float len = 16f;
            var d = new float[(int)(SR * len)];
            // Frecuencias múltiplo de 1/len Hz → bucle perfecto
            float[] f = { 110f, 164.8125f, 220f, 277.1875f, 329.625f };
            float[] a = { 0.5f, 0.32f, 0.26f, 0.12f, 0.1f };
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float v = 0;
                for (int k = 0; k < f.Length; k++)
                    v += Mathf.Sin(2 * Mathf.PI * f[k] * t) * a[k] * (0.6f + 0.4f * Mathf.Sin(2 * Mathf.PI * t * (k + 1) / len));
                d[i] = v * 0.35f;
            }
            return Clip("AmbientPad", d);
        }
    }
}
