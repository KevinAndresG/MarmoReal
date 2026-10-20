using UnityEngine;
using UnityEngine.UIElements;

namespace Marmoreal
{
    /// <summary>HUD de Reliquias (S5): equipar, ranuras y Fragmentos de Eternidad (S9: cada hito de Pulido da 1 Punto de Reliquia que gastas a mano; devolver una reliquia recupera el punto). Layout en Hud.uxml (reliquias).</summary>
    public partial class Hud
    {
        VisualElement reliquiasOverlay;
        Label reliquiasInfo;
        ScrollView reliquiasList;
        Button reliquiasBtn, reliquiasClose, slotBtn;
        bool reliquiasOpen;
        string builtRelics;

        void BindRelics()
        {
            Q(out reliquiasOverlay, "reliquias"); Q(out reliquiasInfo, "reliquias-info"); Q(out reliquiasList, "reliquias-list");
            Q(out reliquiasBtn, "reliquias-btn"); Q(out reliquiasClose, "reliquias-close"); Q(out slotBtn, "slot-btn");
            if (reliquiasBtn != null) reliquiasBtn.clicked += ToggleRelics;
            if (reliquiasClose != null) reliquiasClose.clicked += ToggleRelics;
            if (slotBtn != null) slotBtn.clicked += () =>
            {
                if (GameManager.I.BuyRelicSlot()) AudioFX.I?.Play(Sfx.Buy, 0.5f);
                else ShowToast("Fragmentos de Eternidad insuficientes", ToastKind.Info);
                builtRelics = null;
            };
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

        void RefreshRelics()
        {
            var gm = GameManager.I;
            if (!gm || reliquiasList == null) return;
            var S = gm.S;
            var cfg = gm.config;
            reliquiasInfo.text = $"Ranuras {S.relicsEquipped.Count}/{gm.RelicSlots}  ·  Consagraciones {S.consecrations}  ·  Fragmentos {Num.F(S.eternity)}  ·  PUNTOS DE RELIQUIA: {S.relicTokens}  ·  siguiente en {gm.PrestigesToRelic} Pulido(s)";
            int cost = gm.NextSlotCost;
            slotBtn.text = cost < 0 ? "RANURAS AL MÁXIMO" : $"RANURA EXTRA  ({cost} Fragmentos)";
            slotBtn.SetEnabled(cost >= 0 && S.eternity >= cost);

            string sig = S.relicTokens + "#" + string.Join(",", S.relics) + "|" + string.Join(",", S.relicsEquipped) + "|" + gm.RelicSlots;
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
                text.Add(Lbl("logro-desc", r.description));
                row.Add(text);
                var right = new VisualElement();
                right.AddToClassList("logro-right");
                string id = r.id;
                if (have)
                {
                    right.Add(Lbl("logro-progress", eq ? "EQUIPADA" : "EQUIPAR"));
                    var back = new Button(() => { gm.RefundRelic(id); AudioFX.I?.Play(Sfx.Buy, 0.4f); builtRelics = null; }) { text = "DEVOLVER (+1 punto)" };
                    back.AddToClassList("btn"); back.AddToClassList("relic-btn");
                    right.Add(back);
                    row.RegisterCallback<ClickEvent>(e => { if (e.target is Button) return; gm.ToggleRelic(id); AudioFX.I?.Play(Sfx.Buy, 0.4f); builtRelics = null; });
                }
                else
                {
                    var unlock = new Button(() => { if (gm.UnlockRelic(id)) AudioFX.I?.Play(Sfx.Buy, 0.5f); builtRelics = null; }) { text = "DESBLOQUEAR (1 punto)" };
                    unlock.AddToClassList("btn"); unlock.AddToClassList("relic-btn"); unlock.AddToClassList("buy");
                    unlock.SetEnabled(S.relicTokens > 0);
                    right.Add(unlock);
                }
                row.Add(right);
                reliquiasList.Add(row);
            }
        }
    }
}
