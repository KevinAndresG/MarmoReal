using UnityEngine;

namespace Marmoreal
{
    /// <summary>
    /// MUSAS: orbes de luz que aparecen al azar en el santuario. Al tocarlos dan una bendición aleatoria
    /// (Frenesí, Furia, Recarga Divina, Lluvia Dorada, Tormenta, Resonancia Plena) y algo de Ícor.
    /// Prefab: Assets/Marmoreal/Prefabs/Musa. Cifras en Economy.asset (sección Musas).
    /// </summary>
    public class MusaSpawner : MonoBehaviour
    {
        static MusaSpawner _i;
        public static MusaSpawner I { get { if (!_i) _i = FindFirstObjectByType<MusaSpawner>(); return _i; } private set => _i = value; }

        public GameObject musaPrefab;
        public Transform player;
        public float pickupRadius = 1.8f;
        public float hoverHeight = 1.6f;
        [Tooltip("Primera Musa tras desbloquearlas (s)")]
        public float firstDelay = 25f;

        static readonly string[] Names = { "FRENESÍ", "FURIA DIVINA", "RECARGA DIVINA", "LLUVIA DORADA", "TORMENTA", "RESONANCIA PLENA" };

        GameObject musa;
        float timer = -1f, life;
        Vector3 spot;

        public bool MusaActive => musa && musa.activeSelf;
        public Vector3 MusaPosition => musa ? musa.transform.position : Vector3.zero;

        void Awake() { I = this; }

        void Update()
        {
            var gm = GameManager.I;
            if (!gm || !player) return;
            if (gm.S.galleryLevel < gm.config.musaUnlockGallery && gm.S.prestigeCount == 0) return;
            if (Hud.I && Hud.I.BlocksGameplay && !MusaActive) return;

            if (timer < 0f) timer = firstDelay;

            if (!MusaActive)
            {
                timer -= Time.deltaTime;
                if (timer <= 0f) Spawn(gm);
                return;
            }

            life -= Time.deltaTime;
            float k = Mathf.Clamp01(life / 1.5f);
            musa.transform.localScale = Vector3.one * Mathf.Lerp(0.01f, 1f, k);
            Vector3 d = player.position - musa.transform.position;
            float reach = pickupRadius + (player.localScale.x - 1f) * 0.75f;
            if (d.sqrMagnitude < reach * reach) Collect(gm);
            else if (life <= 0f) { musa.SetActive(false); ScheduleNext(gm); }
        }

        void ScheduleNext(GameManager gm)
        {
            float favor = Mathf.Clamp(gm.Value("favor") <= 0 ? 1f : gm.Value("favor"), 0.3f, 1f);
            timer = Random.Range(gm.config.musaIntervalMin, gm.config.musaIntervalMax) * favor;
        }

        void Spawn(GameManager gm)
        {
            if (!musaPrefab) return;
            if (!musa) musa = Instantiate(musaPrefab, transform);
            for (int tries = 0; tries < 12; tries++)
            {
                Vector2 c = Random.insideUnitCircle * (ArenaShape.FlatRadius - 2f);
                spot = new Vector3(c.x, 0, c.y);
                if ((spot - new Vector3(player.position.x, 0, player.position.z)).sqrMagnitude > 64f) break;
            }
            musa.transform.position = spot + Vector3.up * hoverHeight;
            musa.transform.localScale = Vector3.one;
            musa.SetActive(true);
            life = gm.config.musaLifetime;
            AudioFX.I?.Play(Sfx.Golden, 0.7f, 1.2f);
            Hud.I?.ShowToast("✧ Una MUSA ha descendido al santuario. ¡Tócala antes de que se desvanezca!", ToastKind.Divine);
        }

        void Collect(GameManager gm)
        {
            Vector3 p = musa.transform.position;
            musa.SetActive(false);
            ScheduleNext(gm);
            gm.RegisterMusa();
            if (gm.config.icorMusa > 0) gm.AddIcor(gm.config.icorMusa, "musa");

            float insp = Mathf.Max(1f, gm.Value("inspiracion"));
            int pick = Random.Range(0, Names.Length);
            var ps = PowerSystem.I;
            // Recarga Divina sin poderes desbloqueados no sirve: se cambia por Frenesí
            if (pick == 2 && (!ps || !gm.Has("supernova"))) pick = 0;

            switch (pick)
            {
                case 0: gm.StartFrenzy(gm.config.frenzyMult, gm.config.frenzyDuration * insp); break;
                case 1: gm.StartFury(gm.config.furyDuration * insp); break;
                case 2: ps.ResetCooldowns(); break;
                case 3: ps?.FreeGolden(2); break;
                case 4: ps?.FreeMeteorShower(8); break;
                case 5: gm.FillResonance(); Shockwave.I?.TryRelease(); break;
            }

            ShardSystem.I?.SpawnSparkle(p, 40, MonolithSpawner.I ? MonolithSpawner.I.ShardIndexFor(0, true) : 0, 8f);
            AudioFX.I?.Play(Sfx.Statue, 1f, 1.2f);
            AudioFX.I?.Play(Sfx.Golden, 0.9f);
            CameraRig.I?.Kick(6f);
            Hud.I?.Banner($"✧  {Names[pick]}");
        }
    }
}
