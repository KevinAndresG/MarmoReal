using System.Collections.Generic;
using UnityEngine;

namespace Marmoreal
{
    /// <summary>
    /// Ritmo de la vida: eventos temporales (Luna de Ónice, Lluvia de Cristal, Hora Dorada) y progreso de los contratos.
    /// Las cifras viven en Economy.asset (lifeEvents, contracts). Lee el estado por sondeo (sin suscripciones):
    /// sobrevive a recargas de dominio.
    /// </summary>
    public class LifeDirector : MonoBehaviour
    {
        static LifeDirector _i;
        public static LifeDirector I { get { if (!_i) _i = FindFirstObjectByType<LifeDirector>(); return _i; } private set => _i = value; }

        public LifeEventDef ActiveEvent { get; private set; }
        public float EventTimeLeft => ActiveEvent != null ? Mathf.Max(0, eventTimer) : 0f;
        public float GoldenBonus => ActiveEvent != null ? ActiveEvent.goldenChance : 0f;

        float nextEvent = -1f, eventTimer, meteorTimer;

        // Seguimiento de contratos (en memoria; los cumplidos se guardan en GameState.contractsDone)
        int lastVolatile = -1, chain, lastPowers = -1, bestCombo, bestChain, bestBurst;
        float chainTimer;
        readonly Queue<float> powerTimes = new Queue<float>();

        public string DebugState => $"ev={(ActiveEvent != null ? ActiveEvent.id : "-")} evT={EventTimeLeft:0} next={nextEvent:0} chain={chain} combo={bestCombo} ctr={string.Join(",", GameManager.I ? GameManager.I.S.contractsDone : new List<string>())} enc={(Encargo != null ? $"{Encargo.id} {EncargoProgress}/{Encargo.target} {EncargoTimeLeft:0}s" : $"- next {nextEncargo:0}")} insp={(GameManager.I ? GameManager.I.InspirationStacks : 0)} weak={(GameManager.I ? GameManager.I.weakHits : 0)} encDone={(GameManager.I ? GameManager.I.S.encargosDone : 0)}";

        void Awake() { I = this; }

        void Update()
        {
            var gm = GameManager.I;
            if (!gm || gm.S == null) return;
            if (Hud.I && Hud.I.BlocksGameplay) return;
            UpdateEvents(gm);
            UpdateContracts(gm);
            UpdateEncargo(gm);
        }

        // ================================================================= Encargos (S11)
        public EncargoDef Encargo { get; private set; }
        public float EncargoTimeLeft => Encargo != null ? Mathf.Max(0, encargoTimer) : 0f;
        float encargoTimer, nextEncargo = 25f;
        int encargoBase, encargoCombo;
        string lastEncargo;

        int EncargoCounter(GameManager gm, EncargoKind k) => k switch
        {
            EncargoKind.WeakHits => gm.weakHits,
            EncargoKind.DashHits => gm.dashHits,
            EncargoKind.SlamHits => gm.slamHits,
            EncargoKind.Statues => gm.S.lifeStatues,
            _ => 0,
        };

        public int EncargoProgress
        {
            get
            {
                var gm = GameManager.I;
                if (Encargo == null || !gm) return 0;
                return Encargo.kind == EncargoKind.Combo ? encargoCombo : Mathf.Max(0, EncargoCounter(gm, Encargo.kind) - encargoBase);
            }
        }

        /// <summary>Espera aleatoria con margen amplio (sesgada a lo corto, con cola larga) para que nunca sea predecible.</summary>
        static float RollEncargoGap(GameManager gm, float scale)
        {
            float lo = gm.config.encargoMinGap, hi = Mathf.Max(lo + 1f, gm.config.encargoMaxGap);
            return Mathf.Lerp(lo, hi, Mathf.Pow(Random.value, 1.3f)) * scale;
        }

        void UpdateEncargo(GameManager gm)
        {
            var list = gm.config.encargos;
            if (list == null || list.Count == 0 || gm.S.totalStatues < gm.config.encargoMinStatues) return;
            if (Encargo == null)
            {
                nextEncargo -= Time.deltaTime;
                if (nextEncargo > 0) return;
                var pool = list.FindAll(e => e.id != lastEncargo && !(e.kind == EncargoKind.DashHits && gm.Mutator != null && gm.Mutator.noDash));
                if (pool.Count == 0) pool = list;
                Encargo = pool[Random.Range(0, pool.Count)];
                lastEncargo = Encargo.id;
                encargoTimer = Encargo.time;
                encargoBase = EncargoCounter(gm, Encargo.kind);
                encargoCombo = 0;
                AudioFX.I?.Play(Sfx.Tink, 0.5f, 1.3f);
                Hud.I?.ShowToast($"ENCARGO: {string.Format(Encargo.description, Encargo.target)} ({Encargo.time:0} s)", ToastKind.Info);
                return;
            }
            if (Encargo.kind == EncargoKind.Combo) encargoCombo = Mathf.Max(encargoCombo, gm.Combo);
            if (EncargoCounter(gm, Encargo.kind) < encargoBase) encargoBase = EncargoCounter(gm, Encargo.kind); // Pulido: contador de la vida a 0
            encargoTimer -= Time.deltaTime;
            if (EncargoProgress >= Encargo.target)
            {
                var done = Encargo;
                Encargo = null;
                nextEncargo = RollEncargoGap(gm, 1f);
                gm.CompleteEncargo(done);
            }
            else if (encargoTimer <= 0)
            {
                Encargo = null;
                nextEncargo = RollEncargoGap(gm, Random.value < gm.config.encargoRetryChance ? 0.5f : 1f); // fallar no castiga: puede volver antes
                AudioFX.I?.Play(Sfx.Deny, 0.35f, 0.9f);
            }
        }

        // ================================================================= Eventos
        void UpdateEvents(GameManager gm)
        {
            var c = gm.config;
            if (nextEvent < 0) nextEvent = Random.Range(c.eventIntervalMin, c.eventIntervalMax);
            if (ActiveEvent != null)
            {
                eventTimer -= Time.deltaTime;
                if (ActiveEvent.meteorEvery > 0)
                {
                    meteorTimer -= Time.deltaTime;
                    if (meteorTimer <= 0) { meteorTimer = ActiveEvent.meteorEvery; PowerSystem.I?.FreeMeteorShower(ActiveEvent.meteorCount); }
                }
                if (eventTimer <= 0) ActiveEvent = null;
                return;
            }
            if (gm.S.galleryLevel < c.eventUnlockGallery) return;
            nextEvent -= Time.deltaTime;
            if (nextEvent > 0) return;
            nextEvent = Random.Range(c.eventIntervalMin, c.eventIntervalMax);
            float total = 0;
            foreach (var e in c.lifeEvents) if (gm.S.galleryLevel >= e.minGallery) total += e.weight;
            float r = Random.value * total;
            foreach (var e in c.lifeEvents)
            {
                if (gm.S.galleryLevel < e.minGallery) continue;
                r -= e.weight;
                if (r <= 0) { StartEvent(e); break; }
            }
        }

        /// <summary>Lanza un evento por id (también para pruebas).</summary>
        public bool StartEvent(string id)
        {
            var e = GameManager.I ? GameManager.I.config.lifeEvents.Find(x => x.id == id) : null;
            if (e != null) StartEvent(e);
            return e != null;
        }

        void StartEvent(LifeEventDef e)
        {
            ActiveEvent = e;
            eventTimer = e.duration;
            meteorTimer = 0.5f;
            if (e.frenzyMult > 0) GameManager.I.StartFrenzy(e.frenzyMult, e.duration);
            if (e.goldenChance >= 1f && MonolithSpawner.I)
                foreach (var m in MonolithSpawner.I.Active) m.MakeGolden();
            AudioFX.I?.Play(Sfx.Golden, 0.9f, 0.8f);
            Hud.I?.Banner(e.displayName.ToUpperInvariant());
            Hud.I?.ShowToast($"EVENTO: {e.displayName} — {e.description} ({e.duration:0} s)", ToastKind.Gold);
        }

        // ================================================================= Contratos
        void UpdateContracts(GameManager gm)
        {
            var S = gm.S;
            // Cadena de volcánicos: estallidos encadenados con menos de 'window' s entre sí
            if (lastVolatile < 0 || S.runVolatile < lastVolatile) lastVolatile = S.runVolatile;
            chainTimer -= Time.deltaTime;
            if (S.runVolatile > lastVolatile)
            {
                if (chainTimer <= 0) chain = 0;
                chain += S.runVolatile - lastVolatile;
                lastVolatile = S.runVolatile;
                chainTimer = 1.2f;
            }
            // Ráfaga de poderes
            if (lastPowers < 0 || S.powersUsed < lastPowers) lastPowers = S.powersUsed;
            while (S.powersUsed > lastPowers) { powerTimes.Enqueue(Time.time); lastPowers++; }
            bestCombo = Mathf.Max(bestCombo, gm.Combo);
            bestChain = Mathf.Max(bestChain, chain);
            while (powerTimes.Count > 0 && Time.time - powerTimes.Peek() > 10f) powerTimes.Dequeue();
            bestBurst = Mathf.Max(bestBurst, powerTimes.Count);
            if (S.lifeStatues == 0) { bestCombo = gm.Combo; bestChain = bestBurst = 0; } // vida nueva

            if (!gm.ContractsOpen) return;
            foreach (var id in S.contracts)
            {
                if (S.contractsDone.Contains(id)) continue;
                var c = gm.config.GetContract(id);
                if (c != null && Progress(c) >= c.target) { gm.CompleteContract(c); break; } // la lista cambia
            }
        }

        public int Progress(ContractDef c)
        {
            var gm = GameManager.I;
            if (!gm) return 0;
            switch (c.kind)
            {
                case ContractKind.VolatileChain: return chainTimer > 0 || c.window <= 0 ? chain : 0;
                case ContractKind.Combo: return bestCombo;
                case ContractKind.PowerBurst:
                    while (powerTimes.Count > 0 && Time.time - powerTimes.Peek() > c.window) powerTimes.Dequeue();
                    return powerTimes.Count;
                case ContractKind.Golden: return gm.S.lifeGolden;
                case ContractKind.Boss: return gm.S.lifeBosses;
                case ContractKind.Statues: return gm.S.lifeStatues;
            }
            return 0;
        }

        int BestOf(ContractDef c) => c.kind == ContractKind.VolatileChain ? bestChain : c.kind == ContractKind.PowerBurst ? bestBurst : Progress(c);

        /// <summary>Encargo activo (fijo bajo la barra de galería hasta cumplirse o caducar).</summary>
        public string EncargoText()
        {
            if (Encargo == null) return "";
            return $"ENCARGO: {string.Format(Encargo.description, Encargo.target)}   {Mathf.Min(EncargoProgress, Encargo.target)}/{Encargo.target} · {Mathf.CeilToInt(EncargoTimeLeft)} s";
        }

        /// <summary>Texto de la HUD: mutador activo + contratos de la vida.</summary>
        public string PanelText()
        {
            var gm = GameManager.I;
            if (!gm) return "";
            var sb = new System.Text.StringBuilder();
            string mu = gm.MutatorStatus();
            if (mu.Length > 0) sb.Append("MUTADOR  ").AppendLine(mu);
            if (gm.ContractsOpen)
                foreach (var id in gm.S.contracts)
                {
                    var c = gm.config.GetContract(id);
                    if (c == null) continue;
                    bool done = gm.S.contractsDone.Contains(id);
                    sb.Append(done ? "✔ " : "◇ ").Append(gm.ContractText(c));
                    if (!done && c.target > 1)
                    {
                        sb.Append($"  ({Mathf.Min(Progress(c), c.target)}/{c.target})");
                        // Ventana: el contador vuelve a 0 si pasan 'window' s sin sumar; el récord indica lo cerca que estuviste
                        if (c.window > 0) sb.Append($" · récord {Mathf.Min(BestOf(c), c.target)} · se reinicia en {c.window:0.#} s");
                    }
                    sb.AppendLine();
                }
            return sb.ToString().TrimEnd();
        }
    }
}
