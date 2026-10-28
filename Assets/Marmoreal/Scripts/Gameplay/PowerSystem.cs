using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Marmoreal
{
    /// <summary>
    /// Poderes activables (teclas 1-7, o clic en la barra de poderes). Se desbloquean y mejoran en el
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
        [Tooltip("Prefab con LineRenderer para la Cadena de Rayos")]
        public GameObject lightningPrefab;
        public Color lightningColor = new Color(0.65f, 0.85f, 1f);
        [Tooltip("Luz puntual que destella en explosiones")]
        public Light flash;
        public float flashLumens = 350000f;
        [Tooltip("Multiplicador global del destello (0-1). Bajo = se ve la jugada en el frenesí")]
        [Range(0f, 1f)] public float flashScale = 0.3f;
        public float meteorHeight = 32f;
        public float meteorFlightTime = 0.55f;
        public float levitateHeight = 6f;

        [System.NonSerialized] float[] cooldownLeft;
        readonly List<GameObject> meteorPool = new List<GameObject>();
        readonly List<LineRenderer> boltPool = new List<LineRenderer>();
        Color flashBaseColor = new Color(-1, 0, 0);
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

        void Awake() { I = this; if (flash) flashBaseColor = flash.color; }

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

            if (flash && flash.intensity > 0f) flash.intensity = Mathf.MoveTowards(flash.intensity, 0f, flashLumens * flashScale * dt * 5f);

            if (Hud.I && Hud.I.BlocksGameplay) return;
            var k = Keyboard.current;
            if (k != null && !(k.leftCtrlKey.isPressed || k.rightCtrlKey.isPressed))
            {
                var keys = new[] { k.digit1Key, k.digit2Key, k.digit3Key, k.digit4Key, k.digit5Key, k.digit6Key, k.digit7Key, k.digit8Key, k.digit9Key };
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
            var mu = GM.Mutator;
            if (mu != null && mu.disabledPower == d.id)
            {
                Hud.I?.ShowToast($"{d.displayName} está sellado por el mutador {mu.displayName}", ToastKind.Info);
                AudioFX.I?.Play(Sfx.Deny, 0.5f);
                cooldownLeft[i] = 1.5f;
                return false;
            }

            bool ok = Cast(d, false);
            if (!ok) { cooldownLeft[i] = 1.5f; return false; } // evita repetir el intento cada frame
            cooldownLeft[i] = CooldownTotal(i);
            if (GM.RelicOn("reloj_arena")) // Reliquia: acorta la recarga de los demás
                for (int j = 0; j < cooldownLeft.Length; j++) if (j != i) cooldownLeft[j] *= 0.85f;
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
                case "meteoros": StartCoroutine(Meteors(d.count + Mathf.RoundToInt(GM.Value("enjambre")), d, true)); return true;
                case "coloso": if (colossusRunning) return false; StartCoroutine(Colossus(d)); return true;
                case "vortice": StartCoroutine(Vortex(d)); return true;
                case "midas":
                    if (!Midas(Mathf.RoundToInt(d.count + GM.Value("reymidas")))) return false;
                    if (GM.RelicOn("midas_avaro")) GM.StartFrenzy(2f, 10f); // Reliquia
                    return true;
                case "rayo":
                    if (!MonolithSpawner.I || MonolithSpawner.I.Nearest(player.position, 999f) == null) { Hud.I?.ShowToast("No hay bloques que fulminar", ToastKind.Info); return false; }
                    StartCoroutine(Lightning(d)); return true;
            }
            return false;
        }

        public void ResetCooldowns()
        {
            EnsureArrays();
            for (int i = 0; i < cooldownLeft.Length; i++) cooldownLeft[i] = 0f;
        }

        /// <summary>Multiplica la recarga restante de todos los poderes (Lira de Calíope).</summary>
        public void ShortenCooldowns(float k)
        {
            EnsureArrays();
            for (int i = 0; i < cooldownLeft.Length; i++) cooldownLeft[i] *= k;
        }

        public void FreeMeteorShower(int count)
        {
            var d = GM.config.GetPower("meteoros");
            if (d != null) StartCoroutine(Meteors(count, d, true));
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
            if (!GM.RelicOn("nova_racimo")) yield break;
            // Reliquia: racimo de 4 bombas menores alrededor
            float a0 = Random.value * 360f;
            for (int k = 0; k < 4; k++)
            {
                yield return new WaitForSeconds(0.18f);
                Vector3 q = Ground(pos + Quaternion.Euler(0, a0 + k * 90f, 0) * Vector3.forward * radius * 0.75f) + Vector3.up * 0.6f;
                Shockwave.I?.Blast(q, radius * 0.5f, GM.PowerDamage(d.id, d.potency * 0.45f), HitSource.Power);
                FlashAt(q, new Color(1f, 0.6f, 0.3f), 0.6f);
                AudioFX.I?.Play(Sfx.Boom, 0.7f, 1.1f + k * 0.08f);
                CameraRig.I?.Shake(0.3f);
            }
        }

        bool Levitation(PowerDef d)
        {
            var sp = MonolithSpawner.I;
            if (!sp) return false;
            var list = new List<Monolith>(sp.Active);
            float dmg = GM.PowerDamage(d.id, d.potency) * Mathf.Max(1f, GM.Value("caida")) * (GM.RelicOn("ingravidez_iman") ? 1.5f : 1f);
            int n = 0;
            foreach (var m in list)
            {
                if (!m || !m.IsTargetable) continue;
                if ((m.transform.position - player.position).sqrMagnitude > d.radius * d.radius) continue;
                m.Levitate(levitateHeight + Random.Range(0f, 2f), 0.9f, 0.4f + n * 0.12f, dmg);
                n++;
            }
            if (n == 0) { Hud.I?.ShowToast("No hay bloques que levantar", ToastKind.Info); return false; }
            if (GM.RelicOn("ingravidez_iman")) ShardSystem.I?.ForceCollect(3f, 999f); // Reliquia: todo el polvo hacia ti
            AudioFX.I?.Play(Sfx.Dash, 0.9f, 0.45f);
            AudioFX.I?.Play(Sfx.Golden, 0.5f, 0.7f);
            CameraRig.I?.Kick(6f);
            return true;
        }

        IEnumerator Meteors(int count, PowerDef d, bool finale)
        {
            float interval = Mathf.Max(0.05f, d.duration / Mathf.Max(1, count));
            float dmg = GM.PowerDamage(d.id, d.potency);
            float radius = d.radius > 0f ? d.radius : 3.4f;
            // Reparte la lluvia: cada bloque recibe su meteorito antes de repetir
            var targets = new List<Monolith>();
            for (int i = 0; i < count; i++)
            {
                if (targets.Count == 0) FillTargets(targets);
                Vector3 at;
                if (targets.Count > 0) { var m = targets[targets.Count - 1]; targets.RemoveAt(targets.Count - 1); at = m ? m.transform.position + Vector3.up * Random.Range(1f, 3f) : RandomGround(); }
                else at = RandomGround();
                StartCoroutine(OneMeteor(at, dmg, radius, 1f, false));
                yield return new WaitForSeconds(interval);
            }
            if (!finale || d.finale <= 0f) yield break;
            // GRAN METEORO: cae donde más bloques hay juntos
            yield return new WaitForSeconds(0.35f);
            StartCoroutine(OneMeteor(DensestPoint(radius * 2f) + Vector3.up * 1.5f, GM.PowerDamage(d.id, d.finale), radius * 2.2f, 2.8f, true));
            if (GM.RelicOn("meteoros_dorados")) { yield return new WaitForSeconds(meteorFlightTime * 1.8f); Midas(3); } // Reliquia
        }

        void FillTargets(List<Monolith> list)
        {
            list.Clear();
            var sp = MonolithSpawner.I;
            if (!sp) return;
            foreach (var m in sp.Active) if (m && m.IsTargetable) list.Add(m);
            for (int i = list.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (list[i], list[j]) = (list[j], list[i]); }
        }

        Vector3 RandomGround()
        {
            Vector2 c = Random.insideUnitCircle * ArenaShape.SpawnRadius;
            return Ground(new Vector3(c.x, 0, c.y));
        }

        /// <summary>Punto (sobre un bloque) con más bloques alrededor en ese radio.</summary>
        Vector3 DensestPoint(float r)
        {
            var sp = MonolithSpawner.I;
            Vector3 best = player ? Ground(player.position) : Vector3.zero;
            int bestN = -1;
            if (!sp) return best;
            foreach (var a in sp.Active)
            {
                if (!a || !a.IsTargetable) continue;
                int n = 0;
                foreach (var b in sp.Active) if (b && b.IsTargetable && (b.transform.position - a.transform.position).sqrMagnitude < r * r) n++;
                if (n > bestN) { bestN = n; best = a.transform.position; }
            }
            return best;
        }

        GameObject GetMeteor()
        {
            foreach (var m in meteorPool) if (m && !m.activeSelf) return m;
            if (!meteorPrefab) return null;
            var go = Instantiate(meteorPrefab, transform);
            meteorPool.Add(go);
            return go;
        }

        IEnumerator OneMeteor(Vector3 target, float dmg, float radius, float scale, bool big)
        {
            var go = GetMeteor();
            Vector2 off = Random.insideUnitCircle.normalized * (big ? 14f : 9f);
            Vector3 start = target + new Vector3(off.x, meteorHeight * (big ? 1.4f : 1f), off.y);
            float flight = meteorFlightTime * (big ? 1.8f : 1f);
            TrailRenderer tr = null;
            if (go)
            {
                tr = go.GetComponentInChildren<TrailRenderer>();
                go.transform.position = start;
                go.transform.localScale = Vector3.one * scale;
                if (tr) { tr.Clear(); tr.widthMultiplier = scale; }
                go.SetActive(true);
            }
            if (big) { AudioFX.I?.Play(Sfx.Dash, 0.8f, 0.4f); Hud.I?.Banner("☄  GRAN METEORO"); }
            float t = 0f;
            while (t < flight)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / flight);
                if (go) go.transform.position = Vector3.Lerp(start, target, k * k);
                yield return null;
            }
            if (go) { go.SetActive(false); go.transform.localScale = Vector3.one; if (tr) tr.widthMultiplier = 1f; }
            Shockwave.I?.Blast(target, radius, dmg, HitSource.Power, !big);
            int idx = MonolithSpawner.I ? MonolithSpawner.I.ShardIndexFor(0, true) : 0;
            ShardSystem.I?.SpawnSparkle(target, big ? 60 : 10, idx, big ? 14f : 8f);
            if (big)
            {
                Shockwave.I?.Blast(target, radius * 1.4f, 0f, HitSource.Power);
                ShardSystem.I?.ForceCollect(3f, radius * 1.6f);
                FlashAt(target, new Color(1f, 0.55f, 0.2f), 1f);
                AudioFX.I?.Play(Sfx.Boom, 1f, 0.6f);
                AudioFX.I?.Play(Sfx.Shatter, 0.9f, 0.7f);
                CameraRig.I?.Shake(0.9f);
                CameraRig.I?.Kick(9f);
                Hud.I?.HitStop(0.07f);
            }
            else
            {
                AudioFX.I?.Play(Sfx.Boom, 0.55f, Random.Range(1.3f, 1.6f));
                CameraRig.I?.Shake(0.22f);
            }
        }

        IEnumerator Colossus(PowerDef d)
        {
            colossusRunning = true;
            var pm = player.GetComponent<PlayerMarble>();
            var rb = player.GetComponent<Rigidbody>();
            if (basePlayerRadius < 0) { basePlayerRadius = pm.radius; basePlayerMass = rb.mass; }
            float target = Mathf.Max(1.2f, d.radius);
            float dur = d.duration + GM.Value("titan");
            GM.colossusMult = Mathf.Max(1f, d.potency);
            AudioFX.I?.Play(Sfx.Boom, 0.7f, 0.5f);
            for (float t = 0; t < 0.4f; t += Time.deltaTime) { SetSize(pm, rb, Mathf.Lerp(1f, target, t / 0.4f)); yield return null; }
            SetSize(pm, rb, target);
            if (CameraRig.I) CameraRig.I.extraDistance = 7f;
            for (float t = 0, step = 0; t < Mathf.Max(0.5f, dur - 0.8f); t += Time.deltaTime)
            {
                step -= Time.deltaTime;
                if (step <= 0 && GM.RelicOn("coloso_sismico")) // Reliquia: ondas al rodar
                {
                    step = 1.1f;
                    Vector3 g = Ground(player.position);
                    Shockwave.I?.Blast(g, 7f * target, GM.PowerDamage(d.id, 8f), HitSource.Power);
                    CameraRig.I?.Shake(0.25f);
                    AudioFX.I?.Play(Sfx.Boom, 0.5f, 0.7f);
                }
                yield return null;
            }
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

        /// <summary>child = vórtice menor de la reliquia "Vórtice Escindido" (nace en start, escala 0,6, mitad de duración).</summary>
        IEnumerator Vortex(PowerDef d, bool child = false, Vector3 start = default)
        {
            Vector3 pos = child ? start : Ground(player.position) + Vector3.up * 2.2f;
            GameObject vortex = child ? GetChildVortex() : this.vortex;
            if (!child && !vortex && vortexPrefab) vortex = this.vortex = Instantiate(vortexPrefab, transform);
            float h = Mathf.Max(1f, GM.Value("horizonte"));
            float k = child ? 0.6f : 1f;
            float radius = d.radius * Mathf.Sqrt(h) * k;
            float dps = GM.PowerDamage(d.id, d.potency) * h * k;
            float duration = d.duration * (child ? 0.5f : 1f);
            if (vortex) { vortex.SetActive(true); vortex.transform.position = pos; vortex.transform.localScale = Vector3.zero; }
            AudioFX.I?.Play(Sfx.Boom, 0.8f, 0.4f);
            CameraRig.I?.Kick(-6f);
            var tmp = new List<Monolith>();
            int sparkIdx = MonolithSpawner.I ? MonolithSpawner.I.ShardIndexFor(0, true) : 0;
            float t = 0f, tick = 0f, hum = 0f;
            Monolith prey = null;
            while (t < duration)
            {
                float dt = Time.deltaTime;
                t += dt; tick += dt; hum -= dt;
                // Errante: viaja hacia el bloque más cercano y lo devora; al acabar busca otro
                if (!prey || !prey.IsTargetable) prey = MonolithSpawner.I ? MonolithSpawner.I.Nearest(pos, 60f) : null;
                if (prey)
                {
                    Vector3 goal = prey.transform.position; goal.y = Ground(goal).y + 2.2f;
                    pos = Vector3.MoveTowards(pos, goal, 4.5f * dt);
                }
                ShardSystem.I?.AttractTo(pos, 0.4f, radius * 1.6f);
                float grow = Mathf.Clamp01(t / 0.4f) * Mathf.Clamp01((duration - t) / 0.25f + 0.35f);
                if (vortex)
                {
                    vortex.transform.position = pos;
                    vortex.transform.localScale = Vector3.one * (radius / 9f) * grow * (1f + 0.08f * Mathf.Sin(t * 24f));
                }
                FlashAt(pos, new Color(0.55f, 0.25f, 1f), 0.12f + 0.06f * Mathf.Sin(t * 30f));
                if (tick >= 0.25f)
                {
                    tick -= 0.25f;
                    float ramp = 0.6f + 0.8f * (t / duration); // tritura cada vez más fuerte
                    MonolithSpawner.I?.InRadius(pos, radius, tmp);
                    foreach (var m in tmp) m.TakeDamage(dps * 0.25f * ramp, m.ClosestPoint(pos), HitSource.Power, false);
                    // Materia que orbita y cae dentro
                    for (int s = 0; s < 4; s++)
                    {
                        Vector2 c = Random.insideUnitCircle.normalized * radius * Random.Range(0.8f, 1.3f);
                        ShardSystem.I?.SpawnSparkle(pos + new Vector3(c.x, Random.Range(-1f, 1.5f), c.y), 2, sparkIdx, 1.5f);
                    }
                    CameraRig.I?.Shake(0.06f);
                }
                if (hum <= 0f) { hum = 0.45f; AudioFX.I?.Play(Sfx.Boom, 0.25f, Random.Range(0.3f, 0.4f)); }
                yield return null;
            }
            // IMPLOSIÓN: se colapsa y estalla
            for (float c = 0f; c < 0.22f; c += Time.deltaTime)
            {
                if (vortex) vortex.transform.localScale = Vector3.one * (radius / 9f) * 0.35f * (1f - c / 0.22f);
                yield return null;
            }
            if (vortex) vortex.SetActive(false);
            if (d.finale > 0f)
            {
                Shockwave.I?.Blast(pos, radius * 1.5f, GM.PowerDamage(d.id, d.finale) * h * k, HitSource.Power);
                Shockwave.I?.Blast(pos, radius * 2f, 0f, HitSource.Power);
            }
            ShardSystem.I?.SpawnSparkle(pos, 50, sparkIdx, 13f);
            ShardSystem.I?.ForceCollect(3f, radius * 2f);
            FlashAt(pos, new Color(0.7f, 0.45f, 1f), 1f);
            AudioFX.I?.Play(Sfx.Boom, 1f, 0.55f);
            AudioFX.I?.Play(Sfx.Shatter, 0.8f, 0.6f);
            CameraRig.I?.Shake(0.85f);
            CameraRig.I?.Kick(10f);
            Hud.I?.HitStop(0.06f);
            if (!child && GM.RelicOn("vortice_gemelo")) // Reliquia: se divide en dos
            {
                Vector3 side = Quaternion.Euler(0, Random.value * 360f, 0) * Vector3.forward * radius * 0.8f;
                StartCoroutine(Vortex(d, true, pos + side));
                StartCoroutine(Vortex(d, true, pos - side));
            }
        }

        readonly List<GameObject> vortexPool = new List<GameObject>();
        GameObject GetChildVortex()
        {
            foreach (var v in vortexPool) if (v && !v.activeSelf) return v;
            if (!vortexPrefab) return null;
            var go = Instantiate(vortexPrefab, transform);
            go.SetActive(false);
            vortexPool.Add(go);
            return go;
        }

        // ---------------------------------------------------------------- Cadena de Rayos
        IEnumerator Lightning(PowerDef d)
        {
            int bolts = Mathf.Max(1, d.count);
            int hops = 3 + Mathf.RoundToInt(GM.Value("cadena"));
            float dmg = GM.PowerDamage(d.id, d.potency);
            float interval = Mathf.Max(0.08f, d.duration / bolts);
            var sp = MonolithSpawner.I;
            var pts = new List<Vector3>();
            var hit = new HashSet<Monolith>();
            var targets = new List<Monolith>();
            for (int b = 0; b < bolts && sp; b++)
            {
                if (targets.Count == 0) FillTargets(targets);
                if (targets.Count == 0) break;
                var cur = targets[targets.Count - 1]; targets.RemoveAt(targets.Count - 1);
                if (!cur || !cur.IsTargetable) { b--; continue; }
                pts.Clear(); hit.Clear();
                Vector3 top = cur.transform.position + new Vector3(Random.Range(-4f, 4f), 30f, Random.Range(-4f, 4f));
                pts.Add(top);
                float k = 1f;
                for (int h = 0; h < hops && cur; h++)
                {
                    Vector3 p = cur.ClosestPoint(cur.transform.position + Vector3.up * 3f);
                    pts.Add(p);
                    hit.Add(cur);
                    cur.TakeDamage(dmg * k, p, HitSource.Power, h == 0);
                    ShardSystem.I?.SpawnSparkle(p, 8, 0, 7f);
                    k *= 0.85f;
                    // salta al bloque más cercano que no haya tocado este rayo
                    Monolith next = null; float bd = d.radius * d.radius;
                    foreach (var m in sp.Active)
                    {
                        if (!m || !m.IsTargetable || hit.Contains(m)) continue;
                        float dd = (m.transform.position - cur.transform.position).sqrMagnitude;
                        if (dd < bd) { bd = dd; next = m; }
                    }
                    cur = next;
                }
                if (GM.RelicOn("rayo_inverso")) // Reliquia: rebota de vuelta por la misma cadena
                {
                    var back = new List<Monolith>(hit);
                    for (int r = back.Count - 2; r >= 0; r--)
                    {
                        var m = back[r];
                        if (!m || !m.IsTargetable) continue;
                        Vector3 p = m.ClosestPoint(m.transform.position + Vector3.up * 3f);
                        pts.Add(p);
                        m.TakeDamage(dmg * 0.6f, p, HitSource.Power, false);
                        ShardSystem.I?.SpawnSparkle(p, 6, 0, 6f);
                    }
                }
                StartCoroutine(DrawBolt(pts));
                FlashAt(pts[1], lightningColor, 0.8f);
                AudioFX.I?.Play(Sfx.Boom, 0.7f, Random.Range(1.7f, 2.0f));
                AudioFX.I?.Play(Sfx.Chip, 0.8f, Random.Range(1.6f, 1.9f));
                CameraRig.I?.Shake(0.3f);
                Hud.I?.HitStop(0.025f);
                yield return new WaitForSeconds(interval * Random.Range(0.7f, 1.3f));
            }
        }

        LineRenderer GetBolt()
        {
            foreach (var l in boltPool) if (l && !l.gameObject.activeSelf) return l;
            if (!lightningPrefab) return null;
            var go = Instantiate(lightningPrefab, transform);
            var lr = go.GetComponent<LineRenderer>();
            if (lr) boltPool.Add(lr);
            return lr;
        }

        /// <summary>Dibuja el rayo en zigzag entre los puntos y lo apaga parpadeando.</summary>
        /// <summary>Gruta de Cristal: el bloque golpeado refleja un rayo al bloque más cercano.</summary>
        public void ReflectBolt(Monolith from, Vector3 point, float dmg)
        {
            var sp = MonolithSpawner.I;
            if (!sp || dmg <= 0) return;
            Monolith next = null; float bd = 14f * 14f;
            foreach (var m in sp.Active)
            {
                if (!m || m == from || !m.IsTargetable) continue;
                float dd = (m.transform.position - point).sqrMagnitude;
                if (dd < bd) { bd = dd; next = m; }
            }
            if (!next) return;
            Vector3 p = next.ClosestPoint(point + Vector3.up * 2f);
            next.TakeDamage(dmg, p, HitSource.Power, false);
            ShardSystem.I?.SpawnSparkle(p, 6, 0, 6f);
            StartCoroutine(DrawBolt(new List<Vector3> { point + Vector3.up * 0.5f, p }));
            FlashAt(p, lightningColor, 0.4f);
            AudioFX.I?.Play(Sfx.Tink, 0.5f, Random.Range(1.6f, 2.0f));
        }

        IEnumerator DrawBolt(List<Vector3> pts)
        {
            var lr = GetBolt();
            if (!lr) yield break;
            var jag = new List<Vector3>();
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 a = pts[i], b = pts[i + 1];
                int seg = i == 0 ? 10 : 6;
                for (int s = 0; s < seg; s++)
                {
                    float f = s / (float)seg;
                    Vector3 p = Vector3.Lerp(a, b, f);
                    if (s > 0) p += Random.insideUnitSphere * (i == 0 ? 1.4f : 0.7f);
                    jag.Add(p);
                }
            }
            jag.Add(pts[pts.Count - 1]);
            lr.positionCount = jag.Count;
            lr.SetPositions(jag.ToArray());
            lr.gameObject.SetActive(true);
            float w = lr.widthMultiplier;
            for (float t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                lr.widthMultiplier = w * (Random.value < 0.25f ? 0.3f : 1f) * (1f - t / 0.4f);
                yield return null;
            }
            lr.widthMultiplier = w;
            lr.gameObject.SetActive(false);
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

        void Flash(Vector3 at) => FlashAt(at, flashBaseColor.r >= 0 ? flashBaseColor : (flash ? flash.color : Color.white), 1f);

        /// <summary>Destello de luz en un punto (explosiones, rayos, volcánicos).</summary>
        public void FlashAt(Vector3 at, Color color, float strength)
        {
            if (!flash) return;
            if (flashBaseColor.r < 0) flashBaseColor = flash.color;
            flash.color = color;
            flash.transform.position = at + Vector3.up * 2f;
            flash.intensity = Mathf.Max(flash.intensity, flashLumens * flashScale * Mathf.Min(strength, 1f) * 0.25f); // ponytail: destellos al 25% para no deslumbrar
        }
    }
}
