using UnityEngine;

namespace Marmoreal
{
    /// <summary>
    /// MUSAS: orbes de luz que aparecen al azar en el santuario. Al tocarlos dan una bendición aleatoria
    /// (Frenesí, Furia, Recarga Divina, Lluvia Dorada, Tormenta, Onda Infinita, Legión, Cornucopia) y algo de Ícor.
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

        static readonly string[] Names = { "FRENESÍ", "FURIA DIVINA", "RECARGA DIVINA", "LLUVIA DORADA", "TORMENTA", "ONDA INFINITA", "LEGIÓN", "CORNUCOPIA" };
        static readonly string[] Descs =
        {
            "Todo el polvo que recojas vale ×{0} durante {1} s",
            "{1} s: TODOS tus golpes son críticos y la Embestida no tiene recarga",
            "Todos tus poderes están listos otra vez",
            "Medio santuario se convierte en ORO macizo",
            "Una tormenta de meteoritos con Gran Meteoro final",
            "{1} s de Resonancia infinita: ¡pulsa F sin parar!",
            "{1} s: un ejército de Discípulas fantasma embiste por ti",
            "Lluvia instantánea de polvo: +{2}",
        };

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
            var ps = PowerSystem.I;
            bool anyPower = false;
            if (ps) for (int i = 0; i < ps.Count; i++) anyPower |= ps.Unlocked(i);
            int pick = Random.Range(0, Names.Length);
            // Recarga Divina sin poderes desbloqueados no sirve: se cambia por otra
            while (pick == 2 && !anyPower) pick = Random.Range(0, Names.Length);

            float secs = 0f, mult = 0f;
            double bonus = 0;
            switch (pick)
            {
                case 0: mult = gm.config.frenzyMult; secs = gm.config.frenzyDuration * insp; gm.StartFrenzy(mult, secs); break;
                case 1: secs = gm.config.furyDuration * insp; gm.StartFury(secs); break;
                case 2: ps.ResetCooldowns(); break;
                case 3:
                {
                    int n = MonolithSpawner.I ? MonolithSpawner.I.Active.Count : 0;
                    ps?.FreeGolden(Mathf.Max(3, Mathf.CeilToInt(n * 0.5f)));
                    break;
                }
                case 4: ps?.FreeMeteorShower(Mathf.RoundToInt(18 * insp)); break;
                case 5: secs = gm.config.overflowDuration * insp; gm.StartOverflow(secs); Shockwave.I?.TryRelease(); gm.StartOverflow(secs); break;
                case 6: secs = gm.config.legionDuration * insp; AllySquad.I?.SummonLegion(gm.config.legionCount, secs); break;
                case 7:
                    bonus = System.Math.Max(gm.DustPerSecond * gm.config.cornucopiaSeconds, gm.BaseBlockReward * 30) * insp;
                    gm.AddDust(bonus);
                    ShardSystem.I?.SpawnSparkle(p, 80, MonolithSpawner.I ? MonolithSpawner.I.ShardIndexFor(0, true) : 0, 14f);
                    Hud.I?.PulseDust();
                    break;
            }
            Hud.I?.ShowToast($"✧ {Names[pick]}: " + string.Format(Descs[pick], mult.ToString("0.#"), secs.ToString("0"), Num.F(bonus)), ToastKind.Divine);

            ShardSystem.I?.SpawnSparkle(p, 40, MonolithSpawner.I ? MonolithSpawner.I.ShardIndexFor(0, true) : 0, 8f);
            AudioFX.I?.Play(Sfx.Statue, 1f, 1.2f);
            AudioFX.I?.Play(Sfx.Golden, 0.9f);
            CameraRig.I?.Kick(6f);
            Hud.I?.Banner($"✧  {Names[pick]}");
        }
    }
}
