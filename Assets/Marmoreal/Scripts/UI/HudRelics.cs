using UnityEngine;
using UnityEngine.UIElements;

namespace Marmoreal
{
    /// <summary>HUD de Reliquias (S5): equipar, ranuras, Fragmentos de Eternidad y Consagrar. Layout en Hud.uxml (reliquias).</summary>
    public partial class Hud
    {
        VisualElement reliquiasOverlay;
        Label reliquiasInfo;
        ScrollView reliquiasList;
        Button reliquiasBtn, reliquiasClose, slotBtn, consagrarBtn;
        bool reliquiasOpen;
        string builtRelics;

        void BindRelics()
        {
            Q(out reliquiasOverlay, "reliquias"); Q(out reliquiasInfo, "reliquias-info"); Q(out reliquiasList, "reliquias-list");
            Q(out reliquiasBtn, "reliquias-btn"); Q(out reliquiasClose, "reliquias-close"); Q(out slotBtn, "slot-btn"); Q(out consagrarBtn, "consagrar-btn");
            if (reliquiasBtn != null) reliquiasBtn.clicked += ToggleRelics;
            if (reliquiasClose != null) reliquiasClose.clicked += ToggleRelics;
            if (slotBtn != null) slotBtn.clicked += () =>
            {
                if (GameManager.I.BuyRelicSlot()) AudioFX.I?.Play(Sfx.Buy, 0.5f);
                else ShowToast("Fragmentos de Eternidad insuficientes", ToastKind.Info);
                builtRelics = null;
            };
            if (consagrarBtn != null) consagrarBtn.clicked += AskConsecrate;
            builtRelics = null;
        }

        void ToggleRelics()
        {
            if (modalOpen || titleShown || reliquiasOverlay == null) return;
            if (treeOpen) CloseTree();
            if (logrosOpen) ToggleLogros();
            reliquiasOpen = !reliquiasOpen;
            reliquiasOverlay.EnableInClassList("hidden", !reliquiasOpen);
            if (reliquiasOpen) { builtRelics = null; RefreshRelics(); }
        }

        void AskConsecrate()
        {
            var gm = GameManager.I;
            if (!gm.CanPrestige) { ShowToast($"Llega a la Galería {Num.Roman(gm.config.prestigeMinGallery + 1)} para poder Consagrar", ToastKind.Prestige); return; }
            if (!gm.CanConsecrate) { ShowToast($"Faltan {gm.config.consecrateMinPrestiges - gm.PrestigesSinceConsecrate} Pulidos para Consagrar", ToastKind.Prestige); return; }
            ToggleRelics();
            var c = gm.config;
            int keepPct = Mathf.RoundToInt(c.consecrateKeepTree * 100);
            OpenModal("CONSAGRAR ≠ PULIR",
                "PULIR reinicia solo el Polvo, el Taller y las Galerías.\nCONSAGRAR es un Pulido más profundo:\n\n" +
                $"PIERDES · tu Esencia ({Num.F(gm.S.essence)})" + (keepPct > 0 ? $" y {100 - keepPct} % de los niveles del Árbol (cada nodo comprado conserva al menos 1)" : " y todo el Árbol del Escultor") + "\n" +
                "CONSERVAS · Ícor, Altar, Reliquias, campaña, colección y récords\n" +
                $"GANAS · {gm.RelicGain} Reliquia(s) al azar · Esencia por Pulido ×{1.0 + c.consecrateEssenceBonus * (gm.S.consecrations + 1):0.##} para siempre" +
                (c.consecrateResetsCostGrowth ? " · el Taller vuelve a su precio de inicio" : "") + "\n\n" +
                (gm.S.pieces.Count < 3 && !gm.S.campaignWon ? "AVISO: se recomienda Consagrar tras la Prueba 3 o la victoria." : "Momento recomendado: ya superaste la Prueba 3."),
                "CONTINUAR", () => OpenModal("¿CONSAGRAR AHORA?",
                    $"Perderás {Num.F(gm.S.essence)} Esencia y parte del Árbol. No se puede deshacer.\n\nPulsa CONSAGRAR con el ratón para confirmar (Intro no sirve aquí).",
                    "CONSAGRAR", () => { holdToasts = true; gm.DoPrestige(true); holdToasts = false; }, clickOnly: true));
        }

        void RefreshRelics()
        {
            var gm = GameManager.I;
            if (!gm || reliquiasList == null) return;
            var S = gm.S;
            var cfg = gm.config;
            reliquiasInfo.text = $"Ranuras {S.relicsEquipped.Count}/{gm.RelicSlots}  ·  Consagraciones {S.consecrations}  ·  Fragmentos {Num.F(S.eternity)}  ·  Pulidos hasta Consagrar: {Mathf.Max(0, cfg.consecrateMinPrestiges - gm.PrestigesSinceConsecrate)}";
            int cost = gm.NextSlotCost;
            slotBtn.text = cost < 0 ? "RANURAS AL MÁXIMO" : $"RANURA EXTRA  ({cost} Fragmentos)";
            slotBtn.SetEnabled(cost >= 0 && S.eternity >= cost);
            consagrarBtn.text = gm.CanConsecrate ? $"CONSAGRAR  (+{gm.RelicGain} reliquia)" : $"CONSAGRAR  (en {cfg.consecrateMinPrestiges - gm.PrestigesSinceConsecrate} Pulidos)";

            string sig = string.Join(",", S.relics) + "|" + string.Join(",", S.relicsEquipped) + "|" + gm.RelicSlots;
            if (sig == builtRelics) return;
            builtRelics = sig;
            reliquiasList.Clear();
            foreach (var r in cfg.relics)
            {
                bool have = S.relics.Contains(r.id), eq = S.relicsEquipped.Contains(r.id);
                var row = new VisualElement();
                row.AddToClassList("logro-row");
                row.EnableInClassList("locked", !have);
                row.EnableInClassList("done", have);
                row.EnableInClassList("equipped", eq);
                row.Add(Lbl("relic-glyph", r.glyph));
                var text = new VisualElement();
                text.AddToClassList("logro-text");
                text.Add(Lbl("logro-name", r.displayName));
                text.Add(Lbl("logro-desc", have ? r.description : "Se consigue al Consagrar"));
                row.Add(text);
                var right = new VisualElement();
                right.AddToClassList("logro-right");
                right.Add(Lbl("logro-progress", !have ? "BLOQUEADA" : eq ? "EQUIPADA" : "EQUIPAR"));
                row.Add(right);
                if (have)
                {
                    string id = r.id;
                    row.RegisterCallback<ClickEvent>(_ => { gm.ToggleRelic(id); AudioFX.I?.Play(Sfx.Buy, 0.4f); builtRelics = null; });
                }
                reliquiasList.Add(row);
            }
        }
    }
}
