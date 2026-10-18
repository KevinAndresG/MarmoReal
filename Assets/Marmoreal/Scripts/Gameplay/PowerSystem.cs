using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Marmoreal
{
    /// <summary>
    /// Poderes activables (teclas 1-6, o clic en la barra de poderes). Se desbloquean y mejoran en el
    /// ALTAR DE LOS DIOSES con Ícor. Sus cifras base están en Economy.asset → powers.
    /// El daño escala con el daño de impacto actual (GameManager.PowerDamage), así nunca rompen el muro suave.
    /// </summary>
    public class PowerSystem : MonoBehaviour
    {
        static PowerSystem _i;
        /// <summary>Singleton resistente a recargas de dominio (se re-busca si se pierde).</summary>
        public static PowerSystem I { get { if (!_i) _i = FindFirstObjectByType<PowerSystem>(); return _i; } private set => _i = value; }

        public Transform player;
        public GameObject bombPrefab;
        public GameObject meteorPrefab;
        public GameObject vortexPrefab;
        [Tooltip("Luz puntual que destella en explosiones")]
        public Light flash;
        public float flashLumens = 350000f;
        public float meteorHeight = 32f;
        public float meteorFlightTime = 0.55f;
        public float levitateHeight = 6f;

        [System.NonSerialized] float[] cooldownLeft;
        readonly List<GameObject> meteorPool = new List<GameObject>();
        GameObject bomb, vortex;
        float basePlayerRadius = -1f, basePlayerMass = -1f;
        bool colossusRunning;

        GameManager GM => GameManager.I;
        public int Count => GM ? GM.config.powers.Count : 0;
        public PowerDef Def(int i) => GM.config.powers[i];
        public bool Unlocked(int i) => GM && i < Count && GM.Has(Def(i).id);

        public float CooldownTotal(int i)
        {
            var d = Def(i);
            float m = GM.PowerCooldownMult;
            // Recarga propia de cada poder (nodo de su rama en el Altar): "nova_cd" para la Supernova, "<id>_cd" para el resto.
            float own = GM.Value(d.id == "supernova" ? "nova_cd" : d.id + "_cd");
            if (own > 0f) m *= Mathf.Max(0.4f, own);
            return Mathf.Max(5f, d.cooldown * m);
        }

        public float CooldownLeft(int i) { EnsureArrays(); return i < cooldownLeft.Length ? Mathf.Max(0, cooldownLeft[i]) : 0; }
        public float Ready01(int i) => 1f - Mathf.Clamp01(CooldownLeft(i) / CooldownTotal(i));
        public bool IsReady(int i) => Unlocked(i) && CooldownLeft(i) <= 0f;

        void Awake() { I = this; }

        void EnsureArrays()
        {
            int n = Count;
            if (cooldownLeft == null || cooldownLeft.Length != n) cooldownLeft = new float[n];
        }

        void Update()
        {
            if (!GM) return;
            EnsureArrays();
            float dt = Time.deltaTime;
            for (int i = 0; i < cooldownLeft.Length; i++) cooldownLeft[i] -= dt;

            if (flash && flash.intensity > 0f) flash.intensity = Mathf.MoveTowards(flash.intensity, 0f, flashLumens * dt * 4f);

            if (Hud.I && Hud.I.BlocksGameplay) return;
            var k = Keyboard.current;
            if (k != null && !(k.leftCtrlKey.isPressed || k.rightCtrlKey.isPressed))
            {
                var keys = new[] { k.digit1Key, k.digit2Key, k.digit3Key, k.digit4Key, k.digit5Key, k.digit6Key };
                for (int i = 0; i < keys.Length && i < Count; i++)
                    if (keys[i].wasPressedThisFrame) TryCast(i);
            }
            var g = Gamepad.current;
            if (g != null)
            {
                if (g.dpad.up.wasPressedThisFrame) TryCast(0);
                if (g.dpad.right.wasPressedThisFrame) TryCast(1);
                if (g.dpad.down.wasPressedThisFrame) TryCast(2);
                if (g.dpad.left.wasPressedThisFrame) TryCast(3);
            }
        }

        public bool TryCast(int i)
        {
            if (!GM || i < 0 || i >= Count) return false;
            EnsureArrays();
            var d = Def(i);
            if (!Unlocked(i))
            {
                Hud.I?.ShowToast($"{d.displayName}: desbloquéalo en el ALTAR DE LOS DIOSES [G] con Ícor", ToastKind.Divine);
                AudioFX.I?.Play(Sfx.Deny, 0.5f);
                return false;
            }
            if (cooldownLeft[i] > 0f) { AudioFX.I?.Play(Sfx.Deny, 0.4f); return false; }

            bool ok = Cast(d, false);
            if (!ok) { cooldownLeft[i] = 1.5f; return false; } // evita repetir el intento cada frame
            cooldownLeft[i] = CooldownTotal(i);
            GM.RegisterPowerUse();
            Hud.I?.Banner($"{d.glyph}  {d.displayName.ToUpperInvariant()}");
            return true;
        }

        /// <summary>Lanza un poder. free = viene de una bendición (no consume recarga).</summary>
        public bool Cast(PowerDef d, bool free)
        {
            switch (d.id)
            {
                case "supernova": StartCoroutine(Supernova(d)); return true;
                case "ingravidez": return Levitation(d);
                case "meteoros": StartCoroutine(Meteors(d.count + Mathf.RoundToInt(GM.Value("enjambre")), d)); return true;
                case "coloso": if (colossusRunning) return false; StartCoroutine(Colossus(d)); return true;
                case "vortice": StartCoroutine(Vortex(d)); return true;
                case "midas": return Midas(Mathf.RoundToInt(GM.PowerUpgrade(d.id, d.count) + GM.Value("reymidas")));
            }
            return false;
        }

        public void ResetCooldowns()
        {
            EnsureArrays();
            for (int i = 0; i < cooldownLeft.Length; i++) cooldownLeft[i] = 0f;
        }

        public void FreeMeteorShower(int count)
        {
            var d = GM.config.GetPower("meteoros");
            if (d != null) StartCoroutine(Meteors(count, d));
        }

        public void FreeGolden(int count) => Midas(count);

        // ================================================================= Poderes
        Vector3 Ground(Vector3 p) => new Vector3(p.x, ArenaShape.Height(p), p.z);

        IEnumerator Supernova(PowerDef d)
        {
            Vector3 pos = Ground(player.position) + Vector3.up * 0.6f;
            if (!bomb && bombPrefab) bomb = Instantiate(bombPrefab, transform);
            if (bomb) { bomb.SetActive(true); bomb.transform.position = pos; }
            float fuse = Mathf.Max(0.3f, d.duration), t = 0f, nextBeep = 0f;
            while (t < fuse)
            {
                t += Time.deltaTime;
                float k = t / fuse;
                if (bomb) bomb.transform.localScale = Vector3.one * (1f + 0.25f * Mathf.Abs(Mathf.Sin(t * Mathf.Lerp(6f, 30f, k))));
                if (t >= nextBeep) { AudioFX.I?.Play(Sfx.Tink, 0.5f, 0.8f + k * 1.2f); nextBeep = t + Mathf.Lerp(0.3f, 0.07f, k); }
                yield return null;
            }
            if (bomb) bomb.SetActive(false);

            float radius = d.radius * Mathf.Max(1f, GM.Value("nova_radio"));
            Shockwave.I?.Blast(pos, radius, GM.PowerDamage(d.id, d.potency), HitSource.Power);
            Shockwave.I?.Blast(pos, radius * 1.35f, 0f, HitSource.Power);
            GM.StartFrenzy(Mathf.Max(2f, GM.Value("codicia")), GM.config.bombFrenzyDuration);
            ShardSystem.I?.ForceCollect(4f, radius * 1.3f);
            Flash(pos);
            AudioFX.I?.Play(Sfx.Boom, 1f, 0.8f);
            AudioFX.I?.Play(Sfx.Shatter, 0.9f, 0.8f);
            CameraRig.I?.Shake(1f);
            CameraRig.I?.Kick(10f);
            Hud.I?.HitStop(0.08f);
            var rb = player ? player.GetComponent<Rigidbody>() : null;
            if (rb && (rb.position - pos).sqrMagnitude < radius * radius)
                rb.AddExplosionForce(14f, pos, radius, 1.2f, ForceMode.VelocityChange);
        }

        bool Levitation(PowerDef d)
        {
            var sp = MonolithSpawner.I;
            if (!sp) return false;
            var list = new List<Monolith>(sp.Active);
            float dmg = GM.PowerDamage(d.id, d.potency) * Mathf.Max(1f, GM.Value("caida"));
            int n = 0;
            foreach (var m in list)
            {
                if (!m || !m.IsTargetable) continue;
                if ((m.transform.position - player.position).sqrMagnitude > d.radius * d.radius) continue;
                m.Levitate(levitateHeight + Random.Range(0f, 2f), 0.9f, 0.4f + n * 0.12f, dmg);
                n++;
            }
            if (n == 0) { Hud.I?.ShowToast("No hay bloques que levantar", ToastKind.Info); return false; }
            AudioFX.I?.Play(Sfx.Dash, 0.9f, 0.45f);
            AudioFX.I?.Play(Sfx.Golden, 0.5f, 0.7f);
            CameraRig.I?.Kick(6f);
            return true;
        }

        IEnumerator Meteors(int count, PowerDef d)
        {
            float interval = Mathf.Max(0.05f, d.duration / Mathf.Max(1, count));
            float dmg = GM.PowerDamage(d.id, d.potency);
            for (int i = 0; i < count; i++)
            {
                StartCoroutine(OneMeteor(PickMeteorTarget(), dmg));
                yield return new WaitForSeconds(interval);
            }
        }

        Vector3 PickMeteorTarget()
        {
            var sp = MonolithSpawner.I;
            if (sp && sp.Active.Count > 0)
            {
                for (int tries = 0; tries < 6; tries++)
                {
                    var m = sp.Active[Random.Range(0, sp.Active.Count)];
                    if (m && m.IsTargetable) return m.transform.position + Vector3.up * Random.Range(1f, 3f);
                }
            }
            Vector2 c = Random.insideUnitCircle * ArenaShape.SpawnRadius;
            return Ground(new Vector3(c.x, 0, c.y));
        }

        GameObject GetMeteor()
        {
            foreach (var m in meteorPool) if (m && !m.activeSelf) return m;
            if (!meteorPrefab) return null;
            var go = Instantiate(meteorPrefab, transform);
            meteorPool.Add(go);
            return go;
        }

        IEnumerator OneMeteor(Vector3 target, float dmg)
        {
            var go = GetMeteor();
            Vector2 off = Random.insideUnitCircle.normalized * 9f;
            Vector3 start = target + new Vector3(off.x, meteorHeight, off.y);
            TrailRenderer tr = null;
            if (go)
            {
                tr = go.GetComponentInChildren<TrailRenderer>();
                go.transform.position = start;
                if (tr) tr.Clear();
                go.SetActive(true);
            }
            float t = 0f;
            while (t < meteorFlightTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / meteorFlightTime);
                if (go) go.transform.position = Vector3.Lerp(start, target, k * k);
                yield return null;
            }
            if (go) go.SetActive(false);
            Shockwave.I?.Blast(target, 3.4f, dmg, HitSource.Power, true);
            ShardSystem.I?.SpawnSparkle(target, 10, MonolithSpawner.I ? MonolithSpawner.I.ShardIndexFor(0, true) : 0, 8f);
            AudioFX.I?.Play(Sfx.Boom, 0.55f, Random.Range(1.3f, 1.6f));
            CameraRig.I?.Shake(0.22f);
        }

        IEnumerator Colossus(PowerDef d)
        {
            colossusRunning = true;
            var pm = player.GetComponent<PlayerMarble>();
            var rb = player.GetComponent<Rigidbody>();
            if (basePlayerRadius < 0) { basePlayerRadius = pm.radius; basePlayerMass = rb.mass; }
            float target = Mathf.Max(1.2f, d.radius);
            float dur = GM.PowerUpgrade(d.id, d.duration) + GM.Value("titan");
            GM.colossusMult = Mathf.Max(1f, d.potency);
            AudioFX.I?.Play(Sfx.Boom, 0.7f, 0.5f);
            for (float t = 0; t < 0.4f; t += Time.deltaTime) { SetSize(pm, rb, Mathf.Lerp(1f, target, t / 0.4f)); yield return null; }
            SetSize(pm, rb, target);
            if (CameraRig.I) CameraRig.I.extraDistance = 7f;
            yield return new WaitForSeconds(Mathf.Max(0.5f, dur - 0.8f));
            for (float t = 0; t < 0.4f; t += Time.deltaTime) { SetSize(pm, rb, Mathf.Lerp(target, 1f, t / 0.4f)); yield return null; }
            SetSize(pm, rb, 1f);
            GM.colossusMult = 1f;
            if (CameraRig.I) CameraRig.I.extraDistance = 0f;
            colossusRunning = false;
        }

        void SetSize(PlayerMarble pm, Rigidbody rb, float s)
        {
            player.localScale = Vector3.one * s;
            pm.radius = basePlayerRadius * s;
            rb.mass = basePlayerMass * s * s;
        }

        IEnumerator Vortex(PowerDef d)
        {
            Vector3 pos = Ground(player.position) + Vector3.up * 2.2f;
            if (!vortex && vortexPrefab) vortex = Instantiate(vortexPrefab, transform);
            float h = Mathf.Max(1f, GM.Value("horizonte"));
            float radius = d.radius * Mathf.Sqrt(h);
            float dps = GM.PowerDamage(d.id, d.potency) * h;
            if (vortex) { vortex.SetActive(true); vortex.transform.position = pos; vortex.transform.localScale = Vector3.zero; }
            ShardSystem.I?.AttractTo(pos, d.duration);
            AudioFX.I?.Play(Sfx.Boom, 0.8f, 0.4f);
            var tmp = new List<Monolith>();
            float t = 0f, tick = 0f;
            while (t < d.duration)
            {
                t += Time.deltaTime; tick += Time.deltaTime;
                float grow = Mathf.Clamp01(t / 0.4f) * Mathf.Clamp01((d.duration - t) / 0.4f);
                if (vortex) vortex.transform.localScale = Vector3.one * (radius / 9f) * grow * (1f + 0.06f * Mathf.Sin(t * 20f));
                if (tick >= 0.25f)
                {
                    tick -= 0.25f;
                    MonolithSpawner.I?.InRadius(pos, radius, tmp);
                    foreach (var m in tmp) m.TakeDamage(dps * 0.25f, m.ClosestPoint(pos), HitSource.Power, false);
                    CameraRig.I?.Shake(0.05f);
                }
                yield return null;
            }
            if (vortex) vortex.SetActive(false);
        }

        bool Midas(int count)
        {
            var sp = MonolithSpawner.I;
            if (!sp) return false;
            var list = new List<Monolith>(sp.Active);
            for (int i = list.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (list[i], list[j]) = (list[j], list[i]); }
            int n = 0;
            foreach (var m in list) { if (n >= count) break; if (m && m.MakeGolden()) n++; }
            if (n == 0) { Hud.I?.ShowToast("No hay bloques que dorar", ToastKind.Info); return false; }
            AudioFX.I?.Play(Sfx.Golden, 1f);
            AudioFX.I?.Play(Sfx.Statue, 0.6f, 1.3f);
            Hud.I?.ShowToast($"¡{n} bloque{(n > 1 ? "s" : "")} convertido{(n > 1 ? "s" : "")} en ORO!", ToastKind.Gold);
            return true;
        }

        void Flash(Vector3 at)
        {
            if (!flash) return;
            flash.transform.position = at + Vector3.up * 2f;
            flash.intensity = flashLumens;
        }
    }
}
