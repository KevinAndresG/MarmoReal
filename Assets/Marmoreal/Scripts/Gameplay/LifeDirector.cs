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
        int lastVolatile = -1, chain, lastPowers = -1, bestCombo;
        float chainTimer;
        readonly Queue<float> powerTimes = new Queue<float>();

        public string DebugState => $"ev={(ActiveEvent != null ? ActiveEvent.id : "-")} evT={EventTimeLeft:0} next={nextEvent:0} chain={chain} combo={bestCombo} ctr={string.Join(",", GameManager.I ? GameManager.I.S.contractsDone : new List<string>())}";

        void Awake() { I = this; }

        void Update()
        {
            var gm = GameManager.I;
            if (!gm || gm.S == null) return;
            if (Hud.I && Hud.I.BlocksGameplay) return;
            UpdateEvents(gm);
            UpdateContracts(gm);
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
            if (S.lifeStatues == 0) bestCombo = gm.Combo; // vida nueva

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
                    if (!done && c.target > 1) sb.Append($"  ({Mathf.Min(Progress(c), c.target)}/{c.target})");
                    sb.AppendLine();
                }
            return sb.ToString().TrimEnd();
        }
    }
}
