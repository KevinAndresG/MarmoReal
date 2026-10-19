using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Marmoreal
{
    /// <summary>HUD de campaña (S4): Gran Monumento, Colección de canicas, pantalla de victoria y récords del Modo Infinito.
    /// Layout en Hud.uxml (monument, coleccion); aquí solo se rellena.</summary>
    public partial class Hud
    {
        VisualElement coleccionOverlay;
        Label monumentTitle, monumentHint, coleccionMonument, recordsLbl;
        readonly Label[] pieceLbls = new Label[7];
        ScrollView coleccionList;
        Button coleccionBtn, coleccionClose, infiniteBtn;
        bool coleccionOpen;
        string builtColors;
        float victoryAt = -1f;
        bool cinematic;

        void BindCampaign()
        {
            Q(out coleccionOverlay, "coleccion"); Q(out monumentTitle, "monument-title"); Q(out monumentHint, "monument-hint");
            Q(out coleccionMonument, "coleccion-monument"); Q(out recordsLbl, "records"); Q(out coleccionList, "coleccion-list");
            Q(out coleccionBtn, "coleccion-btn"); Q(out coleccionClose, "coleccion-close"); Q(out infiniteBtn, "infinite-btn");
            for (int i = 0; i < pieceLbls.Length; i++) pieceLbls[i] = root.Q<Label>($"piece-{i}");
            if (coleccionBtn != null) coleccionBtn.clicked += ToggleColeccion;
            if (coleccionClose != null) coleccionClose.clicked += ToggleColeccion;
            if (infiniteBtn != null) infiniteBtn.clicked += () => { GameManager.I.EnterInfinite(); AudioFX.I?.Play(Sfx.Prestige); RefreshColeccion(); };
            builtColors = null;
        }

        // ================================================================= Gran Monumento
        void RefreshMonument()
        {
            var gm = GameManager.I;
            if (!gm || monumentTitle == null) return;
            var S = gm.S;
            var cfg = gm.config;
            int need = gm.PiecesNeeded;
            var trial = gm.TrialBiome;
            for (int i = 0; i < pieceLbls.Length; i++)
            {
                var l = pieceLbls[i];
                if (l == null) continue;
                bool got = S.pieces.Contains(i);
                if (i < cfg.campaign.Count) l.text = cfg.campaign[i].displayName;
                l.EnableInClassList("got", got);
                l.EnableInClassList("trial", trial && trial.campaignStage == i);
                l.EnableInClassList("future", !got && i >= need);
            }
            monumentTitle.text = S.infinite ? $"MODO INFINITO  ·  {gm.Rank.ToUpperInvariant()}  ·  RÉCORD GALERÍA {S.infBestGallery}"
                : S.campaignWon ? "EL GRAN MONUMENTO  ·  COMPLETO"
                : $"EL GRAN MONUMENTO  {S.pieces.Count}/{need}";
            monumentHint.text = trial
                ? (gm.TrialDue ? "¡PRUEBA DE LA SALA! Derriba al jefe" : $"Prueba de {trial.displayName}: Galería {Num.Roman(trial.trialGallery)}")
                : S.campaignWon && !S.infinite ? "Modo Infinito disponible en COLECCIÓN [C]" : "";

            if (S.campaignWon && !S.victoryShown && !modalOpen && !titleShown && !holdToasts && Time.unscaledTime >= victoryAt) ShowVictory();
        }

        void OnTrialPassed(int stage)
        {
            var cfg = GameManager.I.config;
            Banner($"PIEZA DEL MONUMENTO: {(stage < cfg.campaign.Count ? cfg.campaign[stage].displayName.ToUpperInvariant() : "?")}");
            CameraRig.I?.Shake(0.5f);
            Flash(coleccionBtn);
            RefreshMonument();
        }

        void OnCampaignWon() => victoryAt = Time.unscaledTime + 3f; // deja ver el banner de la pieza

        void ShowVictory()
        {
            GameManager.I.S.victoryShown = true;
            StartCoroutine(VictoryCinematic());
        }

        /// <summary>Cinemática corta: la cámara orbita el Gran Monumento (--- MONUMENTO ---) y luego sale el modal. Cualquier tecla la salta.</summary>
        System.Collections.IEnumerator VictoryCinematic()
        {
            var mon = FindFirstObjectByType<Monument>();
            var rig = cameraRig ? cameraRig : CameraRig.I;
            if (mon && rig)
            {
                cinematic = true;
                var b = new Bounds(mon.transform.position, Vector3.one);
                foreach (var r in mon.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                Vector3 c = b.center;
                float dist = b.extents.magnitude * 1.5f + 6f, dur = 8f, t = 0f;
                var cam = rig.transform;
                Vector3 pos0 = cam.position; Quaternion rot0 = cam.rotation;
                rig.enabled = false;
                hudLayer?.AddToClassList("cine");
                Banner("EL GRAN MONUMENTO\n<size=40%>cualquier tecla: saltar</size>", (int)(dur * 1000));
                var fwd = -mon.transform.position; // el frente mira al centro del cuenco
                float a0 = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
                yield return null;
                while (t < dur && !(Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame))
                {
                    t += Time.unscaledDeltaTime;
                    float k = t / dur;
                    var dir = Quaternion.Euler(0, a0 - 70f + 160f * Mathf.SmoothStep(0, 1, k), 0) * Vector3.forward;
                    cam.position = c + dir * dist * Mathf.Lerp(1.15f, 0.9f, k) + Vector3.up * dist * Mathf.Lerp(0.45f, 0.2f, k);
                    cam.LookAt(c + Vector3.up * b.extents.y * 0.2f);
                    yield return null;
                }
                cam.SetPositionAndRotation(pos0, rot0);
                rig.enabled = true;
                hudLayer?.RemoveFromClassList("cine");
                Banner("");
                cinematic = false;
            }
            ShowVictoryModal();
        }

        void ShowVictoryModal()
        {
            var gm = GameManager.I;
            var S = gm.S;
            OpenModal("¡MONUMENTO COMPLETO!",
                "La obra maestra se alza sobre el santuario.\n\n" +
                $"Tiempo de campaña: {Num.Time(S.campaignSeconds)}\n" +
                $"Estatuas liberadas: {Num.F(S.totalStatues)}\n" +
                $"Pulidos: {S.prestigeCount}\n" +
                $"Mejor golpe: {Num.F(S.bestHit)}\n" +
                $"Galería máxima: {Num.Roman(S.maxGallery)}\n\n" +
                "Desbloqueado: MODO INFINITO (salas mezcladas, mutadores más duros, récords y rangos) y la canica Dorada.\n" +
                "Puedes entrar ahora o más tarde desde COLECCIÓN [C].",
                "ENTRAR AL INFINITO", () => gm.EnterInfinite());
            AudioFX.I?.Play(Sfx.Prestige);
            gm.Save();
        }

        // ================================================================= Colección
        void ToggleColeccion()
        {
            if (modalOpen || titleShown || coleccionOverlay == null) return;
            if (treeOpen) CloseTree();
            if (logrosOpen) ToggleLogros();
            coleccionOpen = !coleccionOpen;
            coleccionOverlay.EnableInClassList("hidden", !coleccionOpen);
            if (coleccionOpen) { builtColors = null; RefreshColeccion(); }
        }

        void RefreshColeccion()
        {
            var gm = GameManager.I;
            if (!gm || coleccionList == null) return;
            var S = gm.S;
            var cfg = gm.config;
            string sig = S.marbleColor + "|" + string.Join(",", S.colors) + "|" + (long)S.eternity;
            if (sig != builtColors)
            {
                builtColors = sig;
                coleccionList.Clear();
                foreach (var c in cfg.marbleColors)
                {
                    bool have = S.colors.Contains(c.id);
                    var row = new VisualElement();
                    row.AddToClassList("logro-row");
                    row.EnableInClassList("locked", !have);
                    row.EnableInClassList("done", have);
                    row.EnableInClassList("selected", have && gm.CurrentColor == c);
                    var sw = new VisualElement();
                    sw.AddToClassList("color-swatch");
                    sw.style.backgroundColor = c.trailColor;
                    row.Add(sw);
                    var text = new VisualElement();
                    text.AddToClassList("logro-text");
                    text.Add(Lbl("logro-name", c.displayName));
                    text.Add(Lbl("logro-desc", c.hint));
                    row.Add(text);
                    var right = new VisualElement();
                    right.AddToClassList("logro-right");
                    right.Add(Lbl("logro-progress", !have ? (c.unlock == ColorUnlock.Fragments ? $"COMPRAR {c.threshold} ◈" : "BLOQUEADA") : gm.CurrentColor == c ? "EN USO" : "ELEGIR"));
                    row.Add(right);
                    if (!have && c.unlock == ColorUnlock.Fragments)
                        row.RegisterCallback<ClickEvent>(_ => { if (gm.BuyColor(c)) AudioFX.I?.Play(Sfx.Buy, 0.5f); else ShowToast($"Faltan Fragmentos de Eternidad ({Num.F(S.eternity)}/{c.threshold})", ToastKind.Info); });
                    if (have)
                    {
                        string id = c.id;
                        row.RegisterCallback<ClickEvent>(_ => { gm.SelectColor(id); AudioFX.I?.Play(Sfx.Buy, 0.4f); });
                    }
                    coleccionList.Add(row);
                }
            }

            int need = gm.PiecesNeeded;
            var sb = new StringBuilder($"EL GRAN MONUMENTO  {S.pieces.Count}/{need}   ");
            for (int i = 0; i < cfg.campaign.Count; i++)
                sb.Append(i > 0 ? " · " : "").Append(cfg.campaign[i].displayName).Append(S.pieces.Contains(i) ? " ✓" : i >= need ? " (próx.)" : "");
            if (coleccionMonument != null) coleccionMonument.text = sb.ToString();

            if (recordsLbl != null)
            {
                if (!S.campaignWon) recordsLbl.text = "Completa el Gran Monumento para desbloquear el MODO INFINITO.";
                else
                {
                    var rk = new StringBuilder();
                    for (int i = 0; i < cfg.rankNames.Length && i < cfg.rankGalleries.Length; i++)
                        rk.Append(i > 0 ? " → " : "").Append(cfg.rankNames[i]).Append(' ').Append(cfg.rankGalleries[i]);
                    recordsLbl.text = $"RÉCORDS DEL INFINITO   ·   Rango: {gm.Rank}\n" +
                        $"Galería máx. {S.infBestGallery}   ·   Tiempo {Num.Time(S.infSeconds)}   ·   Mejor golpe {Num.F(S.infBestHit)}   ·   Polvo/s {Num.F(S.infBestDps)}\n" +
                        $"Rangos por galería: {rk}";
                }
            }
            infiniteBtn?.EnableInClassList("hidden", !S.campaignWon || S.infinite);
        }

        static void Flash(Button b)
        {
            if (b == null) return;
            b.AddToClassList("flash");
            b.schedule.Execute(() => b.RemoveFromClassList("flash")).StartingIn(900);
        }
    }
}
