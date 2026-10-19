using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Marmoreal
{
    /// <summary>
    /// HUD completa en UI Toolkit. El layout vive en Assets/Marmoreal/UI/Hud.uxml (+ Hud.uss)
    /// y es editable en UI Builder; este script solo rellena datos y anima.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class Hud : MonoBehaviour
    {
        static Hud _i;
        /// <summary>Singleton resistente a recargas de dominio (se re-busca si se pierde).</summary>
        public static Hud I { get { if (!_i) _i = FindFirstObjectByType<Hud>(); return _i; } private set => _i = value; }

        public Camera worldCamera;
        public CameraRig cameraRig;
        public bool showTitle = true;
        [Tooltip("Fuentes del sistema (opcional). Si no existen se usa la del tema.")]
        public string bodyFontFamily = "Segoe UI";
        public string titleFontFamily = "Georgia";
        public string symbolFontFamily = "Segoe UI Symbol";
        public int floaterPool = 56;

        static readonly string[] MarbleNames = { "Carrara", "Rosa Portogallo", "Verde Alpi", "Nero Marquina", "Calacatta Oro" };

        UIDocument doc;
        VisualElement hudLayer;
        VisualElement root, floaterLayer, galleryFill, resFill, resBar, dashFill, toasts, treeOverlay, treeCanvas, modal, title, shop;
        VisualElement powersBar, icorRow, logrosOverlay;
        Label dust, dustRate, essence, statLine, galleryTitle, galleryText, combo, treeEssence, tiName, tiDesc, tiEffect, modalTitle, modalBody, press;
        Label icor, buffs, banner, treeTitle, logrosFame, musaMarker, contractsLbl;
        VisualElement modalCards, modalBox;
        bool holdToasts;
        readonly List<(string, ToastKind)> pendingToasts = new List<(string, ToastKind)>();
        ScrollView shopList, logrosList;
        Button treeBtn, prestigeBtn, qualityBtn, muteBtn, resetBtn, tiBuy, modalOk, modalCancel, modalAlt, treeClose;
        Button altarBtn, logrosBtn, logrosClose;

        /// <summary>Árbol mostrado en el overlay: Esencia = Árbol del Escultor, Ícor = Altar de los Dioses.</summary>
        Currency treeCurrency = Currency.Essence;
        Currency builtTreeCurrency = (Currency)(-1);

        class PowerSlot { public Button el; public VisualElement cd; public Label time; public int index; }
        readonly List<PowerSlot> powerSlots = new List<PowerSlot>();

        class LogroRow { public AchievementDef def; public VisualElement el, fill; public Label progress; }
        readonly List<LogroRow> logroRows = new List<LogroRow>();
        int bannerSeq;

        class Row { public UpgradeDef def; public Button el; public Label lvl, cost, effect, key; }
        readonly List<Row> rows = new List<Row>();
        readonly Dictionary<string, Button> nodes = new Dictionary<string, Button>();
        string selectedNode;
        System.Action modalAction;

        class Floater { public Label l; public Vector3 world, vel; public float age, life; public bool on; }
        readonly List<Floater> floaters = new List<Floater>();
        int floaterNext;

        bool titleShown, modalOpen, treeOpen, logrosOpen, hitStopping;
        float shopTimer, pulseCooldown;
        double lastDust = -1, lastEss = -1, lastRate = -1, lastIcor = -1;
        int lastCombo = -1;
        int tutorialStep;

        public bool BlocksGameplay => titleShown || modalOpen || treeOpen || logrosOpen;
        public bool PointerOverUI { get; private set; }
        public string DebugState => $"bound={root != null} sameRoot={root == doc.rootVisualElement} rows={rows.Count} shopChildren={shopList?.childCount} shopPanel={shopList?.panel != null} title={titleShown} gm={GameManager.I != null} t={Time.timeSinceLevelLoad:0} gal={GameManager.I?.S.galleryLevel} dust={GameManager.I?.S.dust:0} statues={GameManager.I?.S.totalStatues} dmg={GameManager.I?.ImpactDamage:0.0} hp={GameManager.I?.MonolithHP(false, false):0} max={GameManager.I?.MaxMonoliths} sp=[{MonolithSpawner.I?.DebugState}] shards={ShardSystem.I?.ActiveShards} {BiomeManager.I?.DebugState} mut={GameManager.I?.S.mutatorId} boss={(MonolithSpawner.I && MonolithSpawner.I.Boss ? MonolithSpawner.I.Boss.BossPhase + "/" + MonolithSpawner.I.Boss.HPLeft.ToString("0") : "-")} {LifeDirector.I?.DebugState}";

        void Awake()
        {
            I = this;
            doc = GetComponent<UIDocument>();
        }

        /// <summary>
        /// Enlaza con el árbol actual del UIDocument. El UIDocument puede reconstruir su árbol
        /// (al activarse, o por live-reload desde UI Builder), así que se comprueba cada frame.
        /// </summary>
        bool EnsureBound()
        {
            var r = doc.rootVisualElement;
            if (r == null || r.Q("shop-list") == null) return false;
            if (r == root && shopList != null && shopList.panel != null && rows.Count > 0) return true;
            Bind(r);
            if (GameManager.I)
            {
                BuildShop();
                builtTreeCurrency = (Currency)(-1);
                BuildTree();
                BuildPowers();
                BuildLogros();
                RefreshSettingsButtons();
                RefreshShopNow();
            }
            title.EnableInClassList("hidden", !titleShown);
            hudLayer.EnableInClassList("hidden", titleShown);
            lastDust = lastEss = lastRate = lastIcor = -1; lastCombo = -1;
            return true;
        }

        void Bind(VisualElement r)
        {
            root = r;
            Q(out floaterLayer, "floaters"); Q(out galleryFill, "gallery-fill"); Q(out resFill, "res-fill"); Q(out resBar, "res-bar");
            Q(out dashFill, "dash-fill"); Q(out toasts, "toasts"); Q(out treeOverlay, "tree"); Q(out treeCanvas, "tree-canvas");
            Q(out modal, "modal"); Q(out title, "title"); Q(out shop, "shop"); Q(out hudLayer, "hud");
            Q(out dust, "dust"); Q(out dustRate, "dust-rate"); Q(out essence, "essence"); Q(out statLine, "stat-line");
            Q(out galleryTitle, "gallery-title"); Q(out galleryText, "gallery-text"); Q(out combo, "combo");
            Q(out treeEssence, "tree-essence"); Q(out tiName, "ti-name"); Q(out tiDesc, "ti-desc"); Q(out tiEffect, "ti-effect");
            Q(out modalTitle, "modal-title"); Q(out modalBody, "modal-body"); Q(out press, "press");
            Q(out shopList, "shop-list");
            Q(out powersBar, "powers"); Q(out icorRow, "icor-row"); Q(out logrosOverlay, "logros");
            Q(out icor, "icor"); Q(out buffs, "buffs"); Q(out banner, "banner"); Q(out treeTitle, "tree-title"); Q(out logrosFame, "logros-fame");
            Q(out logrosList, "logros-list");
            Q(out altarBtn, "altar-btn"); Q(out logrosBtn, "logros-btn"); Q(out logrosClose, "logros-close");
            Q(out treeBtn, "tree-btn"); Q(out prestigeBtn, "prestige-btn"); Q(out qualityBtn, "quality-btn"); Q(out muteBtn, "mute-btn");
            Q(out resetBtn, "reset-btn"); Q(out tiBuy, "ti-buy"); Q(out modalOk, "modal-ok"); Q(out modalCancel, "modal-cancel"); Q(out modalAlt, "modal-alt"); Q(out treeClose, "tree-close");
            Q(out contractsLbl, "contracts"); Q(out modalCards, "modal-cards"); modalBox = root.Q(className: "modal-box");

            treeBtn.clicked += () => ToggleTree(Currency.Essence);
            altarBtn.clicked += () => ToggleTree(Currency.Icor);
            treeClose.clicked += CloseTree;
            logrosBtn.clicked += ToggleLogros;
            logrosClose.clicked += ToggleLogros;
            prestigeBtn.clicked += AskPrestige;
            qualityBtn.clicked += CycleQuality;
            muteBtn.clicked += ToggleMute;
            resetBtn.clicked += AskReset;
            tiBuy.clicked += BuySelectedNode;
            modalOk.clicked += () => { var a = modalAction; CloseModal(); a?.Invoke(); };
            modalCancel.clicked += CloseModal;
            if (modalAlt != null) modalAlt.clicked += () => { var gm = GameManager.I; BiomeManager.I?.CycleChoice(gm.S.prestigeCount + 1); AudioFX.I?.Play(Sfx.Buy, 0.4f); AskPrestige(); };
            treeCanvas.generateVisualContent += DrawTreeLines;
            treeCanvas.RegisterCallback<GeometryChangedEvent>(_ => LayoutTree());

            ApplyFonts();
            BuildFloaters();
            musaMarker = new Label("✧ MUSA") { pickingMode = PickingMode.Ignore };
            musaMarker.AddToClassList("musa-marker");
            musaMarker.style.display = DisplayStyle.None;
            floaterLayer.Add(musaMarker);
        }

        void Start()
        {
            var gm = GameManager.I;
            titleShown = showTitle;
            EnsureBound();
            Subscribe(gm);

            if (gm.S.quality >= 0 && gm.S.quality < QualitySettings.names.Length) QualitySettings.SetQualityLevel(gm.S.quality, true);
            AudioFX.I?.SetMuted(gm.S.muted);
            RefreshSettingsButtons();
            tutorialStep = gm.S.tutorialDone ? 99 : 0;

            title.EnableInClassList("hidden", !showTitle);
            hudLayer?.EnableInClassList("hidden", showTitle);
            if (!showTitle) BeginPlay();
            else StartCoroutine(BlinkPress());

            if (gm.PendingOffline.dust > 0) ShowOffline(gm.PendingOffline.dust, gm.PendingOffline.secs);
            RefreshShopNow();
        }

        [System.NonSerialized] GameManager subscribed;

        /// <summary>Suscribe a los eventos del GameManager (se repite si una recarga de dominio los borra).</summary>
        void Subscribe(GameManager gm)
        {
            if (!gm || subscribed == gm) return;
            gm.GalleryUp += OnGalleryUp;
            gm.Toast += ShowToast;
            gm.Prestiged += OnPrestiged;
            gm.StatsChanged += RefreshShopNow;
            gm.OfflineEarned += ShowOffline;
            gm.AchievementUnlocked += OnAchievement;
            subscribed = gm;
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
            var gm = subscribed;
            if (!gm) return;
            gm.GalleryUp -= OnGalleryUp;
            gm.Toast -= ShowToast;
            gm.Prestiged -= OnPrestiged;
            gm.StatsChanged -= RefreshShopNow;
            gm.OfflineEarned -= ShowOffline;
            gm.AchievementUnlocked -= OnAchievement;
        }

        void Q<T>(out T el, string name) where T : VisualElement
        {
            el = root.Q<T>(name);
            if (el == null) Debug.LogWarning($"[Marmoreal] Falta el elemento '{name}' en Hud.uxml");
        }

        // ================================================================= Fuentes
        static bool fontsLoaded;
        static FontAsset sym, body, head;

        // DontSave: si Unity destruyera la fuente al salir de Play, el panel repinta con su material
        // destruido (MissingReferenceException). Se reutiliza por nombre entre sesiones para no acumularlas.
        static FontAsset LoadFont(string family)
        {
            if (string.IsNullOrEmpty(family)) return null;
            string id = "Marmoreal_" + family;
            foreach (var f in Resources.FindObjectsOfTypeAll<FontAsset>())
                if (f && f.name == id) return f;
            var fa = FontAsset.CreateFontAsset(family, "Regular");
            if (!fa) return null;
            fa.name = id;
            fa.hideFlags = HideFlags.DontSave;
            if (fa.material) fa.material.hideFlags = HideFlags.DontSave;
            if (fa.atlasTextures != null)
                foreach (var t in fa.atlasTextures) if (t) t.hideFlags = HideFlags.DontSave;
            return fa;
        }

        void ApplyFonts()
        {
            try
            {
                if (!fontsLoaded)
                {
                    fontsLoaded = true;
                    sym = LoadFont(symbolFontFamily);
                    body = LoadFont(bodyFontFamily);
                    head = LoadFont(titleFontFamily);
                }
                if (body)
                {
                    if (sym) body.fallbackFontAssetTable = new List<FontAsset> { sym };
                    root.style.unityFontDefinition = FontDefinition.FromSDFFont(body);
                }
                if (head)
                {
                    if (sym) head.fallbackFontAssetTable = new List<FontAsset> { sym };
                    foreach (var cls in new[] { "title", "gallery-title", "modal-title", "tree-title", "subtitle", "panel-title", "banner" })
                        root.Query<Label>(className: cls).ForEach(l => l.style.unityFontDefinition = FontDefinition.FromSDFFont(head));
                }
            }
            catch (System.Exception e) { Debug.Log("[Marmoreal] Fuentes del sistema no disponibles, usando la del tema: " + e.Message); }
        }

        // ================================================================= Bucle
        float dbgTimer;
        void Update()
        {
#if UNITY_EDITOR
            dbgTimer -= Time.unscaledDeltaTime;
            if (dbgTimer <= 0) { dbgTimer = 1f; System.IO.File.WriteAllText("Temp/hud_state.txt", $"{Time.frameCount} {DebugState}"); }
#endif
            var gm = GameManager.I;
            if (!gm || !EnsureBound()) return;
            Subscribe(gm);

            if (titleShown)
            {
                bool any = (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                        || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                        || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
                if (any) HideTitle();
            }
            else HandleKeys(gm);

            UpdatePointer();
            UpdateStats(gm);
            UpdateFloaters();
            UpdatePowers(gm);
            UpdateMusaMarker();

            shopTimer -= Time.unscaledDeltaTime;
            pulseCooldown -= Time.unscaledDeltaTime;
            if (shopTimer <= 0) { shopTimer = 0.15f; RefreshContracts(); RefreshShop(); if (treeOpen) RefreshTree(); if (logrosOpen) RefreshLogros(); }
        }

        void HandleKeys(GameManager gm)
        {
            var k = Keyboard.current;
            if (k == null) return;
            if (k.escapeKey.wasPressedThisFrame) { if (modalOpen) CloseModal(); else if (treeOpen) CloseTree(); else if (logrosOpen) ToggleLogros(); }
            if (modalOpen) { if (k.enterKey.wasPressedThisFrame) { var a = modalAction; CloseModal(); a?.Invoke(); } return; }
            if (k.tKey.wasPressedThisFrame || k.tabKey.wasPressedThisFrame) ToggleTree(Currency.Essence);
            if (k.gKey.wasPressedThisFrame) ToggleTree(Currency.Icor);
            if (k.lKey.wasPressedThisFrame) ToggleLogros();
            if (treeOpen || logrosOpen) return;
            if (k.pKey.wasPressedThisFrame) AskPrestige();
            if (k.mKey.wasPressedThisFrame) ToggleMute();
            // Compra rápida del Taller: Ctrl + 1-9 (los números solos lanzan poderes)
            if (!(k.leftCtrlKey.isPressed || k.rightCtrlKey.isPressed)) return;
            var digits = new[] { k.digit1Key, k.digit2Key, k.digit3Key, k.digit4Key, k.digit5Key, k.digit6Key, k.digit7Key, k.digit8Key, k.digit9Key };
            int vis = 0;
            foreach (var r in rows)
            {
                if (r.el.ClassListContains("hidden")) continue;
                if (vis < digits.Length && digits[vis].wasPressedThisFrame) Buy(r);
                vis++;
            }
        }

        void UpdatePointer()
        {
            PointerOverUI = false;
            var m = Mouse.current;
            if (m == null || root.panel == null) return;
            Vector2 sp = m.position.ReadValue();
            sp.y = Screen.height - sp.y;
            var pp = RuntimePanelUtils.ScreenToPanel(root.panel, sp);
            var picked = root.panel.Pick(pp);
            PointerOverUI = picked != null && picked != root;
        }

        void UpdateStats(GameManager gm)
        {
            var s = gm.S;
            if (s.dust != lastDust) { lastDust = s.dust; dust.text = Num.F(s.dust); }
            if (s.essence != lastEss) { lastEss = s.essence; essence.text = Num.F(s.essence); }
            if (s.icor != lastIcor) { lastIcor = s.icor; icor.text = Num.F(s.icor); }
            icorRow.EnableInClassList("hidden", s.lifetimeIcor <= 0);
            UpdateBuffs(gm);
            if (System.Math.Abs(gm.DustPerSecond - lastRate) > 0.001)
            {
                lastRate = gm.DustPerSecond;
                dustRate.text = "+" + Num.F(gm.DustPerSecond) + " / s";
            }

            int need = gm.StatuesNeeded;
            galleryTitle.text = "GALERÍA " + Num.Roman(s.galleryLevel + 1);
            galleryText.text = $"{s.statuesThisGallery} / {need} estatuas liberadas";
            galleryFill.style.width = Length.Percent(100f * s.statuesThisGallery / Mathf.Max(1, need));

            float res = gm.Resonance;
            resFill.style.width = Length.Percent(res * 100f);
            resFill.EnableInClassList("full", res >= 1f);
            if (res >= 1f && tutorialStep == 2) { tutorialStep = 3; ShowToast("¡Resonancia llena! Pulsa F (o clic) para liberar la onda", ToastKind.Info); }

            dashFill.style.width = Length.Percent((PlayerMarble.I ? PlayerMarble.I.DashReady01 : 1f) * 100f);

            if (gm.Combo != lastCombo)
            {
                lastCombo = gm.Combo;
                if (gm.Combo >= 3)
                {
                    combo.text = $"COMBO ×{gm.ComboMult:0.00}   ({gm.Combo})";
                    combo.style.opacity = 1;
                    combo.AddToClassList("pop");
                    combo.schedule.Execute(() => combo.RemoveFromClassList("pop")).StartingIn(90);
                }
                else combo.style.opacity = 0;
            }

            statLine.text = $"Daño {Num.F(gm.ImpactDamage)} · Vel. {gm.MaxSpeed:0} · Imán {gm.MagnetRadius:0}m";
        }

        // ================================================================= Tienda
        void BuildShop()
        {
            shopList.Clear();
            rows.Clear();
            foreach (var d in GameManager.I.config.upgrades)
            {
                if (d.currency != Currency.Dust) continue;
                var row = new Row { def = d, el = new Button() };
                row.el.AddToClassList("shop-row");
                if (d.requires != null && d.requires.Length > 0 && GameManager.I.config.GetPower(d.requires[0]) != null) row.el.AddToClassList("power-up"); // mejora de un poder del Altar
                row.el.Add(Lbl("glyph", d.glyph));
                var text = new VisualElement(); text.AddToClassList("shop-text");
                text.Add(Lbl("shop-name", d.displayName));
                text.Add(Lbl("shop-desc", d.description));
                row.effect = Lbl("shop-effect", "");
                text.Add(row.effect);
                row.el.Add(text);
                var right = new VisualElement(); right.AddToClassList("shop-right");
                row.lvl = Lbl("level", "");
                row.cost = Lbl("cost", "");
                row.key = Lbl("key", "");
                right.Add(row.lvl); right.Add(row.cost); right.Add(row.key);
                row.el.Add(right);
                var captured = row;
                row.el.clicked += () => Buy(captured);
                shopList.Add(row.el);
                rows.Add(row);
            }
        }

        static Label Lbl(string cls, string text)
        {
            var l = new Label(text);
            l.AddToClassList(cls);
            return l;
        }

        void Buy(Row r)
        {
            var gm = GameManager.I;
            if (gm.TryBuy(r.def))
            {
                r.el.AddToClassList("bought");
                r.el.schedule.Execute(() => r.el.RemoveFromClassList("bought")).StartingIn(140);
                RefreshShopNow();
            }
            else AudioFX.I?.Play(Sfx.Deny, 0.6f);
        }

        void RefreshShopNow() { RefreshShop(); RefreshTree(); RefreshPrestige(); }

        void RefreshShop()
        {
            var gm = GameManager.I;
            if (!gm) return;
            int vis = 0;
            foreach (var r in rows)
            {
                var d = r.def;
                bool unlocked = gm.IsUnlocked(d);
                r.el.EnableInClassList("hidden", !unlocked);
                if (!unlocked) continue;
                vis++;
                int lvl = gm.Level(d.id);
                bool maxed = gm.IsMaxed(d);
                int cap = gm.MaxLevelOf(d);
                r.lvl.text = cap < 999 ? $"Nv {lvl}/{cap}" : $"Nv {lvl}";
                r.cost.text = maxed ? "MÁX" : Num.F(gm.CostOf(d));
                r.key.text = vis <= 9 ? $"[^{vis}]" : "";
                r.effect.text = maxed ? FormatEffect(d, lvl) : FormatEffect(d, lvl) + "  →  " + FormatEffect(d, lvl + 1);
                r.el.EnableInClassList("affordable", gm.CanAfford(d));
                r.el.EnableInClassList("maxed", maxed);
            }
            RefreshPrestige();
        }

        static readonly HashSet<string> PercentIds = new HashSet<string> { "fortuna", "paciencia", "fiebre", "rebote", "vinculo" };

        public static string FormatEffect(UpgradeDef d, int level)
        {
            if (d.maxLevel == 1 && d.currency != Currency.Dust) return level > 0 ? "Desbloqueado" : "Bloqueado";
            float v = d.Value(level);
            string s = PercentIds.Contains(d.id) ? Num.Pct(v)
                     : v >= 1000 ? Num.F(v)
                     : Mathf.Abs(v - Mathf.Round(v)) < 0.01f ? Mathf.Round(v).ToString("0")
                     : v.ToString(v < 10 ? "0.00" : "0.0");
            return string.Format(d.effectFormat, s);
        }

        void RefreshPrestige()
        {
            var gm = GameManager.I;
            if (!gm || prestigeBtn == null) return;
            bool can = gm.CanPrestige;
            prestigeBtn.EnableInClassList("ready", can);
            prestigeBtn.text = can
                ? $"PULIR LA CANICA  +{Num.F(gm.PrestigeGain)} ESENCIA  [P]"
                : $"PULIR — requiere Galería {Num.Roman(gm.config.prestigeMinGallery + 1)}";
            treeBtn.text = gm.S.lifetimeEssence > 0 ? "ÁRBOL DEL ESCULTOR  [T]" : "ÁRBOL DEL ESCULTOR (bloqueado)  [T]";
            if (altarBtn != null) altarBtn.text = gm.S.lifetimeIcor > 0 ? "ALTAR  [G]" : "ALTAR (bloqueado)  [G]";
            if (logrosBtn != null) logrosBtn.text = $"MECENAZGOS {gm.S.achievements.Count}/{gm.config.achievements.Count}  [L]";
        }

        // ================================================================= Árbol
        static string CurName(Currency c) => c == Currency.Essence ? "ESENCIA" : c == Currency.Icor ? "ÍCOR" : "POLVO";
        static string CurGlyph(Currency c) => c == Currency.Essence ? "◆" : c == Currency.Icor ? "✶" : "";

        void BuildTree()
        {
            if (builtTreeCurrency == treeCurrency && nodes.Count > 0) return;
            builtTreeCurrency = treeCurrency;
            treeCanvas.Clear();
            nodes.Clear();
            selectedNode = null;
            bool altar = treeCurrency == Currency.Icor;
            treeOverlay.EnableInClassList("altar", altar);
            if (treeTitle != null) treeTitle.text = altar ? "ALTAR DE LOS DIOSES" : "ÁRBOL DEL ESCULTOR";
            tiDesc.text = altar
                ? "Elige un nodo. El Ícor cae de estatuas, bloques dorados, Musas, Mecenazgos y al Pulir. Nunca se pierde."
                : "Elige un nodo. La Esencia se gana al Pulir la canica.";
            foreach (var d in GameManager.I.config.upgrades)
            {
                if (d.currency != treeCurrency) continue;
                var b = new Button();
                b.AddToClassList("node");
                if (d.maxLevel == 1) b.AddToClassList("mechanic");
                if (d.branch == 3) b.AddToClassList("capstone");
                var g = Lbl("node-glyph", d.glyph);
                var lv = Lbl("node-level", "");
                lv.name = "lv";
                b.Add(g); b.Add(lv);
                string id = d.id;
                b.clicked += () => SelectNode(id);
                treeCanvas.Add(b);
                nodes[d.id] = b;
            }
            LayoutTree();
        }

        Vector2 NodeCenter(UpgradeDef d)
        {
            float w = treeCanvas.resolvedStyle.width, h = treeCanvas.resolvedStyle.height;
            if (float.IsNaN(w) || w <= 0) w = 1200; if (float.IsNaN(h) || h <= 0) h = 600;
            float sx = Mathf.Min(180f, w / 7f);
            float sy = (h - 120f) / 4.4f;
            return new Vector2(w * 0.5f + d.treePos.x * sx, 50f + d.treePos.y * sy);
        }

        void LayoutTree()
        {
            var gm = GameManager.I;
            if (!gm) return;
            foreach (var d in gm.config.upgrades)
            {
                if (!nodes.TryGetValue(d.id, out var b)) continue;
                var c = NodeCenter(d);
                b.style.left = c.x; b.style.top = c.y;
            }
            treeCanvas.MarkDirtyRepaint();
        }

        void DrawTreeLines(MeshGenerationContext ctx)
        {
            var gm = GameManager.I;
            if (!gm) return;
            var p = ctx.painter2D;
            p.lineWidth = 3f;
            p.lineCap = LineCap.Round;
            foreach (var d in gm.config.upgrades)
            {
                if (d.currency != treeCurrency || d.requires == null) continue;
                foreach (var r in d.requires)
                {
                    var rd = gm.config.Get(r);
                    if (rd == null) continue;
                    bool lit = gm.Has(r) && gm.Has(d.id);
                    bool avail = gm.Has(r);
                    p.strokeColor = lit ? new Color(0.91f, 0.78f, 0.48f, 0.9f) : avail ? new Color(0.72f, 0.65f, 1f, 0.55f) : new Color(1, 1, 1, 0.12f);
                    p.BeginPath();
                    p.MoveTo(NodeCenter(rd));
                    p.LineTo(NodeCenter(d));
                    p.Stroke();
                }
            }
        }

        void RefreshTree()
        {
            var gm = GameManager.I;
            if (!gm) return;
            foreach (var d in gm.config.upgrades)
            {
                if (!nodes.TryGetValue(d.id, out var b)) continue;
                int lvl = gm.Level(d.id);
                bool unlocked = gm.IsUnlocked(d);
                b.EnableInClassList("available", unlocked);
                b.EnableInClassList("affordable", gm.CanAfford(d));
                b.EnableInClassList("owned", lvl > 0);
                b.EnableInClassList("maxed", gm.IsMaxed(d));
                b.EnableInClassList("selected", d.id == selectedNode);
                b.Q<Label>("lv").text = !unlocked ? "" : d.maxLevel == 1 ? (lvl > 0 ? "✓" : Num.F(gm.CostOf(d)) + " " + CurGlyph(d.currency)) : $"{lvl}/{d.maxLevel}";
            }
            if (treeEssence != null) treeEssence.text = Num.F(gm.Balance(treeCurrency)) + " " + (treeCurrency == Currency.Icor ? "Ícor" : "Esencia");
            UpdateNodeInfo();
            treeCanvas.MarkDirtyRepaint();
        }

        void SelectNode(string id)
        {
            selectedNode = id;
            AudioFX.I?.Play(Sfx.Tink, 0.25f, 1.5f);
            RefreshTree();
        }

        void UpdateNodeInfo()
        {
            var gm = GameManager.I;
            var d = selectedNode != null ? gm.config.Get(selectedNode) : null;
            if (d == null || d.currency != treeCurrency) { tiName.text = ""; tiEffect.text = ""; tiBuy.style.display = DisplayStyle.None; return; }
            int lvl = gm.Level(d.id);
            tiName.text = $"{d.glyph}  {d.displayName}";
            tiDesc.text = d.description;
            bool maxed = gm.IsMaxed(d);
            bool power = gm.config.GetPower(d.id) != null;
            tiEffect.text = d.maxLevel == 1 ? (power ? (lvl > 0 ? $"Poder desbloqueado · tecla [{PowerKey(d.id)}]" : $"Poder nuevo · tecla [{PowerKey(d.id)}]") : lvl > 0 ? "Mecánica desbloqueada" : "Mecánica nueva")
                : maxed ? "Actual: " + FormatEffect(d, lvl) + " (máx.)" : $"Actual: {FormatEffect(d, lvl)}   →   {FormatEffect(d, lvl + 1)}";
            tiBuy.style.display = DisplayStyle.Flex;
            if (!gm.IsUnlocked(d))
            {
                tiBuy.text = "REQUIERE: " + string.Join(", ", System.Array.ConvertAll(d.requires, r => gm.config.Get(r)?.displayName ?? r));
                tiBuy.EnableInClassList("disabled", true);
            }
            else if (maxed) { tiBuy.text = "COMPLETO"; tiBuy.EnableInClassList("disabled", true); }
            else
            {
                tiBuy.text = $"{(lvl > 0 ? "MEJORAR" : "DESBLOQUEAR")}  ·  {Num.F(gm.CostOf(d))} {CurName(d.currency)}";
                tiBuy.EnableInClassList("disabled", !gm.CanAfford(d));
            }
        }

        void BuySelectedNode()
        {
            var gm = GameManager.I;
            var d = selectedNode != null ? gm.config.Get(selectedNode) : null;
            if (d == null) return;
            if (gm.TryBuy(d))
            {
                AudioFX.I?.Play(Sfx.GalleryUp, 0.5f, 1.2f);
                PlayerMarble.I?.ApplyLook();
                if (gm.config.GetPower(d.id) != null)
                {
                    ShowToast($"PODER desbloqueado: {d.displayName} — pulsa [{PowerKey(d.id)}] y mejóralo en el TALLER con polvo", ToastKind.Divine);
                    Banner($"{d.glyph}  {d.displayName.ToUpperInvariant()}");
                }
                else if (d.maxLevel == 1) ShowToast($"Mecánica desbloqueada: {d.displayName}", ToastKind.Prestige);
            }
            else AudioFX.I?.Play(Sfx.Deny, 0.6f);
            RefreshShopNow();
        }

        string PowerKey(string id)
        {
            var ps = GameManager.I.config.powers;
            for (int i = 0; i < ps.Count; i++) if (ps[i].id == id) return (i + 1).ToString();
            return "?";
        }

        /// <summary>Abre/cierra el árbol de esa moneda (T = Escultor, G = Altar). Si hay otro abierto, cambia.</summary>
        public void ToggleTree(Currency c)
        {
            if (modalOpen || titleShown) return;
            var gm = GameManager.I;
            if (treeOpen && treeCurrency == c) { CloseTree(); return; }
            if (c == Currency.Essence && gm.S.lifetimeEssence <= 0)
            {
                ShowToast($"El árbol se abre al Pulir la canica por primera vez (Galería {Num.Roman(gm.config.prestigeMinGallery + 1)})", ToastKind.Prestige);
                return;
            }
            if (c == Currency.Icor && gm.S.lifetimeIcor <= 0)
            {
                ShowToast("El ALTAR se abre con tu primer Ícor: estatuas, bloques dorados, Musas y Mecenazgos", ToastKind.Divine);
                return;
            }
            if (logrosOpen) ToggleLogros();
            treeCurrency = c;
            BuildTree();
            treeOpen = true;
            treeOverlay.RemoveFromClassList("hidden");
            AudioFX.I?.Play(Sfx.Tink, 0.3f, c == Currency.Icor ? 0.9f : 1.2f);
            LayoutTree();
            RefreshTree();
        }

        public void ToggleTree() => ToggleTree(Currency.Essence);

        void CloseTree()
        {
            treeOpen = false;
            treeOverlay.AddToClassList("hidden");
        }

        // ================================================================= Poderes
        void BuildPowers()
        {
            if (powersBar == null) return;
            powersBar.Clear();
            powerSlots.Clear();
            var list = GameManager.I.config.powers;
            for (int i = 0; i < list.Count; i++)
            {
                var d = list[i];
                var slot = new PowerSlot { el = new Button(), index = i };
                slot.el.AddToClassList("power-slot");
                slot.el.tooltip = d.displayName;
                slot.el.Add(Lbl("power-glyph", d.glyph));
                slot.cd = new VisualElement { pickingMode = PickingMode.Ignore };
                slot.cd.AddToClassList("power-cd");
                slot.el.Add(slot.cd);
                slot.time = Lbl("power-time", "");
                slot.time.pickingMode = PickingMode.Ignore;
                slot.el.Add(slot.time);
                var key = Lbl("power-key", (i + 1).ToString());
                key.pickingMode = PickingMode.Ignore;
                slot.el.Add(key);
                int idx = i;
                slot.el.clicked += () => PowerSystem.I?.TryCast(idx);
                powersBar.Add(slot.el);
                powerSlots.Add(slot);
            }
        }

        void UpdatePowers(GameManager gm)
        {
            if (powersBar == null) return;
            var ps = PowerSystem.I;
            bool show = gm.S.lifetimeIcor > 0 && ps != null;
            powersBar.EnableInClassList("hidden", !show);
            if (!show) return;
            foreach (var s in powerSlots)
            {
                if (s.index >= ps.Count) continue;
                bool unlocked = ps.Unlocked(s.index);
                float left = unlocked ? ps.CooldownLeft(s.index) : 0f;
                s.el.EnableInClassList("locked", !unlocked);
                s.el.EnableInClassList("ready", unlocked && left <= 0f);
                float frac = unlocked ? 1f - ps.Ready01(s.index) : 1f;
                s.cd.style.height = Length.Percent(frac * 100f);
                s.time.text = unlocked && left > 0f ? Mathf.CeilToInt(left).ToString() : "";
            }
        }

        void RefreshContracts()
        {
            if (contractsLbl == null) return;
            string t = LifeDirector.I ? LifeDirector.I.PanelText() : "";
            contractsLbl.text = t;
            contractsLbl.EnableInClassList("hidden", t.Length == 0);
        }

        void UpdateBuffs(GameManager gm)
        {
            if (buffs == null) return;
            string t = "";
            if (gm.FrenzyTimeLeft > 0) t += $"FRENESÍ ×{gm.FrenzyMult:0.#}  {gm.FrenzyTimeLeft:0}s";
            if (gm.FuryActive) t += (t.Length > 0 ? "     " : "") + $"FURIA: CRÍTICOS + EMBESTIDA LIBRE  {gm.FuryTimeLeft:0}s";
            if (gm.OverflowTimeLeft > 0) t += (t.Length > 0 ? "     " : "") + $"ONDA INFINITA [F]  {gm.OverflowTimeLeft:0}s";
            if (AllySquad.I && AllySquad.I.LegionTimeLeft > 0) t += (t.Length > 0 ? "     " : "") + $"LEGIÓN  {AllySquad.I.LegionTimeLeft:0}s";
            if (gm.colossusMult > 1f) t += (t.Length > 0 ? "     " : "") + $"COLOSO ×{gm.colossusMult:0} daño";
            var ld = LifeDirector.I;
            if (ld && ld.ActiveEvent != null) t += (t.Length > 0 ? "     " : "") + $"{ld.ActiveEvent.displayName.ToUpperInvariant()}  {ld.EventTimeLeft:0}s";
            var boss = MonolithSpawner.I ? MonolithSpawner.I.Boss : null;
            if (boss && boss.HP > 0) t += (t.Length > 0 ? "     " : "") + $"COLOSO · FASE {Num.Roman(boss.BossPhase + 1)} · {boss.HPLeft / boss.HP * 100:0}%";
            buffs.text = t;
            buffs.EnableInClassList("hidden", t.Length == 0);
        }

        /// <summary>Rótulo grande centrado y animado (poderes, bendiciones, mecenazgos).</summary>
        public void Banner(string text)
        {
            if (banner == null) return;
            int id = ++bannerSeq;
            banner.text = text;
            banner.RemoveFromClassList("out");
            banner.AddToClassList("show");
            toasts?.AddToClassList("dim"); // los toasts no tapan el rótulo
            banner.schedule.Execute(() =>
            {
                if (id != bannerSeq) return;
                banner.RemoveFromClassList("show");
                banner.AddToClassList("out");
                toasts?.RemoveFromClassList("dim");
            }).StartingIn(1300);
        }

        /// <summary>Indicador sobre la Musa activa (pegado al borde si está fuera de pantalla).</summary>
        void UpdateMusaMarker()
        {
            if (musaMarker == null) return;
            var ms = MusaSpawner.I;
            var cam = worldCamera ? worldCamera : Camera.main;
            if (!ms || !ms.MusaActive || !cam || root.panel == null || BlocksGameplay) { musaMarker.style.display = DisplayStyle.None; return; }
            Vector3 vp = cam.WorldToViewportPoint(ms.MusaPosition + Vector3.up * 1.8f);
            bool behind = vp.z < 0;
            if (behind) { vp.x = vp.x < 0.5f ? 1f : 0f; vp.y = 0.5f; }
            bool off = behind || vp.x < 0.04f || vp.x > 0.96f || vp.y < 0.08f || vp.y > 0.92f;
            vp.x = Mathf.Clamp(vp.x, 0.04f, 0.96f);
            vp.y = Mathf.Clamp(vp.y, 0.08f, 0.92f);
            var size = root.panel.visualTree.layout.size;
            musaMarker.style.display = DisplayStyle.Flex;
            musaMarker.style.left = vp.x * size.x;
            musaMarker.style.top = (1f - vp.y) * size.y;
            musaMarker.text = off ? (vp.x < 0.5f ? "◀ ✧ MUSA" : "✧ MUSA ▶") : "✧ MUSA";
            musaMarker.style.opacity = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 6f);
        }

        // ================================================================= Mecenazgos
        void BuildLogros()
        {
            if (logrosList == null) return;
            logrosList.Clear();
            logroRows.Clear();
            foreach (var a in GameManager.I.config.achievements)
            {
                var row = new LogroRow { def = a, el = new VisualElement() };
                row.el.AddToClassList("logro-row");
                var text = new VisualElement();
                text.AddToClassList("logro-text");
                text.Add(Lbl("logro-name", a.displayName));
                text.Add(Lbl("logro-desc", a.description));
                var bar = new VisualElement();
                bar.AddToClassList("bar");
                bar.AddToClassList("logro-bar");
                row.fill = new VisualElement();
                row.fill.AddToClassList("bar-fill");
                row.fill.AddToClassList("divine");
                bar.Add(row.fill);
                text.Add(bar);
                row.el.Add(text);
                var right = new VisualElement();
                right.AddToClassList("logro-right");
                right.Add(Lbl("logro-reward", $"+{a.rewardIcor} ✶"));
                row.progress = Lbl("logro-progress", "");
                right.Add(row.progress);
                row.el.Add(right);
                logrosList.Add(row.el);
                logroRows.Add(row);
            }
        }

        void RefreshLogros()
        {
            var gm = GameManager.I;
            if (!gm || logrosFame == null) return;
            foreach (var r in logroRows)
            {
                bool done = gm.HasAchievement(r.def.id);
                double v = gm.StatValue(r.def.stat);
                // El polvo crece exponencialmente: su barra avanza en escala logarítmica
                double k = done ? 1 : r.def.stat == "dust"
                    ? System.Math.Log10(System.Math.Max(1, v)) / System.Math.Log10(System.Math.Max(10, r.def.threshold))
                    : v / System.Math.Max(1, r.def.threshold);
                r.fill.style.width = Length.Percent((float)System.Math.Min(1, k) * 100f);
                r.progress.text = done ? "✓" : $"{Num.F(System.Math.Min(v, r.def.threshold))} / {Num.F(r.def.threshold)}";
                r.el.EnableInClassList("done", done);
            }
            int n = gm.S.achievements.Count;
            logrosFame.text = $"FAMA  +{(gm.FameMult - 1f) * 100f:0}% polvo   ·   {n} / {gm.config.achievements.Count} mecenazgos";
        }

        void ToggleLogros()
        {
            if (modalOpen || titleShown) return;
            if (treeOpen) CloseTree();
            logrosOpen = !logrosOpen;
            logrosOverlay.EnableInClassList("hidden", !logrosOpen);
            if (logrosOpen) RefreshLogros();
        }

        void OnAchievement(AchievementDef a)
        {
            if (logrosOpen) RefreshLogros();
            RefreshPrestige();
            Banner($"♕  {a.displayName.ToUpperInvariant()}");
            if (logrosBtn == null) return;
            logrosBtn.AddToClassList("flash");
            logrosBtn.schedule.Execute(() => logrosBtn.RemoveFromClassList("flash")).StartingIn(900);
        }

        // ================================================================= Prestigio / modales
        public void AskPrestige()
        {
            var gm = GameManager.I;
            if (!gm.CanPrestige)
            {
                ShowToast($"Llega a la Galería {Num.Roman(gm.config.prestigeMinGallery + 1)} para poder Pulir", ToastKind.Prestige);
                AudioFX.I?.Play(Sfx.Deny, 0.6f);
                return;
            }
            string next = MarbleNames[Mathf.Min(gm.S.prestigeCount + 1, MarbleNames.Length - 1)];
            var bm = BiomeManager.I;
            var room = bm ? bm.NextFor(gm.S.prestigeCount + 1) : null;
            bool choose = room && bm.CanChoose(gm.S.prestigeCount + 1);
            string sala = room ? $"\n\nSala siguiente: {room.displayName}{(choose ? "  (elige con CAMBIAR SALA)" : "")}\n{room.tagline}" : "";
            OpenModal("PULIR LA CANICA",
                $"Reinicias el Polvo, el Taller y las Galerías.\nConservas la Esencia, el Árbol, el Ícor y el Altar.\n\n" +
                $"Ícor por pulir: +{Num.F(gm.config.icorPrestigeBase + System.Math.Floor(gm.PrestigeGain / gm.config.icorPrestigeDivisor))}\n" +
                $"Ganarás  +{Num.F(gm.PrestigeGain)} ESENCIA   (una galería más: +{Num.F(gm.PrestigeGainNext)})\n" +
                $"Tu canica renacerá en mármol {next}." + sala +
                (gm.MutatorStatus().Length > 0 ? $"\n\nMutador: {gm.MutatorStatus()}" + (gm.MutatorBonus(gm.PrestigeGain) > 0 ? $"  (+{Num.F(gm.MutatorBonus(gm.PrestigeGain))} Esencia)" : "") : ""),
                "PULIR", () => { holdToasts = true; gm.DoPrestige(); holdToasts = false; });
            if (modalAlt != null) modalAlt.style.display = choose ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void AskReset()
        {
            OpenModal("BORRAR PARTIDA", "Se perderá TODO el progreso, incluidos la Esencia, el Ícor, los árboles y los Mecenazgos.\n¿Seguro?", "BORRAR", () =>
            {
                GameManager.I.ResetSave();
                ShowToast("Partida borrada. El mármol vuelve a estar virgen.", ToastKind.Info);
                tutorialStep = 0;
            });
        }

        void ShowOffline(double dustEarned, double secs)
        {
            var gm = GameManager.I;
            OpenModal("MIENTRAS NO ESTABAS",
                $"Tus {gm.AcolyteCount} acólitos siguieron tallando durante {Num.Time(secs)}.\n\n+{Num.F(dustEarned)} POLVO DE MÁRMOL",
                "RECOGER", null, false);
            gm.PendingOffline = (0, 0);
        }

        void OpenModal(string t, string body, string ok, System.Action action, bool cancel = true)
        {
            modalTitle.text = t;
            modalBody.text = body;
            modalOk.text = ok;
            modalCancel.style.display = cancel ? DisplayStyle.Flex : DisplayStyle.None;
            if (modalAlt != null) modalAlt.style.display = DisplayStyle.None;
            modalCards?.AddToClassList("hidden");
            modalBox?.RemoveFromClassList("wide");
            modalAction = action;
            modalOpen = true;
            modal.RemoveFromClassList("hidden");
        }

        void CloseModal()
        {
            modalOpen = false;
            modalAction = null;
            modal.AddToClassList("hidden");
            if (pendingToasts.Count > 0) root.schedule.Execute(FlushToasts).StartingIn(1600); // tras el rótulo de la sala
        }

        void FlushToasts()
        {
            if (modalOpen) return;
            var list = new List<(string, ToastKind)>(pendingToasts);
            pendingToasts.Clear();
            foreach (var (t, k) in list) ShowToast(t, k);
        }

        /// <summary>Cartas de mutador en el modal de Pulido (elige 1 de 3; por defecto la segura).</summary>
        void ShowMutatorCards()
        {
            var gm = GameManager.I;
            if (modalCards == null) return;
            modalCards.Clear();
            bool any = gm.S.mutatorOffer.Count > 1;
            modalCards.EnableInClassList("hidden", !any);
            modalBox?.EnableInClassList("wide", any);
            foreach (var id in gm.S.mutatorOffer)
            {
                var mu = gm.config.GetMutator(id);
                if (mu == null) continue;
                var b = new Button();
                b.AddToClassList("btn");
                b.AddToClassList("mut-card");
                foreach (var l in new[] { Lbl("mut-name", mu.displayName), Lbl("mut-desc", mu.description),
                    Lbl("mut-goal", mu.safe ? "Carta segura" : $"Sube {mu.goalGalleries} galerías: +{mu.essenceBonus * 100:0}% Esencia") })
                { l.pickingMode = PickingMode.Ignore; b.Add(l); }
                b.EnableInClassList("selected", gm.S.mutatorId == id);
                string cid = id;
                b.clicked += () =>
                {
                    gm.ChooseMutator(cid);
                    AudioFX.I?.Play(Sfx.Buy, 0.4f);
                    foreach (var ch in modalCards.Children()) ch.RemoveFromClassList("selected");
                    b.AddToClassList("selected");
                };
                modalCards.Add(b);
            }
        }

        void OnPrestiged()
        {
            RefreshShopNow();
            var rep = GameManager.I.PrestigeReport;
            if (!string.IsNullOrEmpty(rep))
            {
                bool cards = GameManager.I.S.mutatorOffer.Count > 1;
                OpenModal("CANICA PULIDA", rep + (cards ? "\n\nELIGE UN MUTADOR PARA ESTA VIDA" : ""), "SEGUIR", null, false);
                ShowMutatorCards();
            }
            lastCombo = -1;
            galleryTitle.AddToClassList("up");
            galleryTitle.schedule.Execute(() => galleryTitle.RemoveFromClassList("up")).StartingIn(350);
        }

        void OnGalleryUp(int lvl)
        {
            galleryTitle.AddToClassList("up");
            galleryTitle.schedule.Execute(() => galleryTitle.RemoveFromClassList("up")).StartingIn(350);
            CameraRig.I?.Shake(0.3f);
            if (tutorialStep == 3 || tutorialStep == 2)
            {
                tutorialStep = 99;
                GameManager.I.S.tutorialDone = true;
            }
        }

        // ================================================================= Título / tutorial
        IEnumerator BlinkPress()
        {
            while (titleShown)
            {
                press.ToggleInClassList("dim");
                yield return new WaitForSecondsRealtime(0.9f);
            }
        }

        /// <summary>Salta la pantalla de título (también usado por herramientas de prueba).</summary>
        public void SkipTitle() { if (titleShown) HideTitle(); }

        void HideTitle()
        {
            titleShown = false;
            title.AddToClassList("out");
            hudLayer?.RemoveFromClassList("hidden");
            title.schedule.Execute(() => title.AddToClassList("hidden")).StartingIn(800);
            BeginPlay();
        }

        void BeginPlay()
        {
            cameraRig?.BeginPlay();
            if (tutorialStep == 0)
            {
                tutorialStep = 1;
                ShowToast("Rueda con WASD y choca contra un bloque: la VELOCIDAD es daño", ToastKind.Info);
                StartCoroutine(DelayedToast(6f, "Baja por las laderas del cuenco para ganar inercia", 1));
                StartCoroutine(DelayedToast(13f, "ESPACIO salta · pulsa otra vez en el aire para caer en PICADO", 1));
            }
        }

        IEnumerator DelayedToast(float t, string text, int requiredStep)
        {
            yield return new WaitForSecondsRealtime(t);
            if (tutorialStep == requiredStep) ShowToast(text, ToastKind.Info);
        }

        // ================================================================= Juice
        public void ShowToast(string text, ToastKind kind)
        {
            if (toasts == null) return;
            if (modalOpen || holdToasts) // se muestran al cerrar el modal (no se apilan sobre él ni sobre el rótulo)
            {
                pendingToasts.Add((text, kind));
                if (pendingToasts.Count > 3) pendingToasts.RemoveAt(0);
                return;
            }
            while (toasts.childCount >= 3) toasts.RemoveAt(0);
            var l = Lbl("toast", text);
            if (kind == ToastKind.Gold) l.AddToClassList("gold");
            if (kind == ToastKind.Gallery) l.AddToClassList("gallery");
            if (kind == ToastKind.Prestige) l.AddToClassList("prestige");
            if (kind == ToastKind.Divine) l.AddToClassList("divine");
            toasts.Add(l);
            l.schedule.Execute(() => l.AddToClassList("out")).StartingIn(kind == ToastKind.Info ? 5200 : 3800);
            l.schedule.Execute(() => l.RemoveFromHierarchy()).StartingIn(kind == ToastKind.Info ? 5700 : 4300);
        }

        public void PulseDust()
        {
            if (dust == null || pulseCooldown > 0) return;
            pulseCooldown = 0.09f;
            dust.AddToClassList("pulse");
            dust.schedule.Execute(() => dust.RemoveFromClassList("pulse")).StartingIn(70);
        }

        public void FlashResonance()
        {
            if (resBar == null) return;
            resBar.AddToClassList("flash");
            resBar.schedule.Execute(() => resBar.RemoveFromClassList("flash")).StartingIn(220);
            AudioFX.I?.Play(Sfx.Deny, 0.4f);
        }

        public void HitStop(float seconds)
        {
            if (seconds <= 0.005f || hitStopping) return;
            StartCoroutine(HitStopRoutine(seconds));
        }

        IEnumerator HitStopRoutine(float seconds)
        {
            hitStopping = true;
            Time.timeScale = 0.08f;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = 1f;
            hitStopping = false;
        }

        public void StatueFreed(Vector3 pos, double reward, bool golden)
        {
            Spawn(pos + Vector3.up * 1.5f, golden ? $"¡OBRA DORADA!\n+{Num.F(reward)}" : $"OBRA MAESTRA\n+{Num.F(reward)}", golden ? "golden" : "statue", 2.2f, Vector3.up * 1.2f);
            if (tutorialStep == 1)
            {
                tutorialStep = 2;
                ShowToast("¡Estatua liberada! Gasta el polvo en el TALLER (derecha · Ctrl + 1-9)", ToastKind.Info);
            }
        }

        public void DamageNumber(Vector3 pos, float dmg, bool crit, HitSource src)
        {
            string cls = crit ? "crit" : (src == HitSource.Acolyte || src == HitSource.Ally) ? "acolyte" : (src == HitSource.Shockwave || src == HitSource.Seismic) ? "wave" : src == HitSource.Power ? "power" : null;
            string text = crit ? Num.F(dmg) + "!" : Num.F(dmg);
            Vector3 v = new Vector3(Random.Range(-1f, 1f), 2.5f, Random.Range(-1f, 1f));
            Spawn(pos, text, cls, crit ? 1.1f : 0.8f, v);
        }

        void BuildFloaters()
        {
            floaterLayer.Clear();
            floaters.Clear();
            for (int i = 0; i < floaterPool; i++)
            {
                var l = new Label { pickingMode = PickingMode.Ignore };
                l.AddToClassList("floater");
                l.style.display = DisplayStyle.None;
                floaterLayer.Add(l);
                floaters.Add(new Floater { l = l });
            }
        }

        void Spawn(Vector3 world, string text, string cls, float life, Vector3 vel)
        {
            if (floaters.Count == 0) return;
            var f = floaters[floaterNext];
            floaterNext = (floaterNext + 1) % floaters.Count;
            f.world = world; f.vel = vel; f.age = 0; f.life = life; f.on = true;
            f.l.text = text;
            f.l.ClearClassList();
            f.l.AddToClassList("floater");
            if (cls != null) f.l.AddToClassList(cls);
            f.l.style.display = DisplayStyle.Flex;
            f.l.BringToFront();
        }

        void UpdateFloaters()
        {
            var cam = worldCamera ? worldCamera : Camera.main;
            if (!cam || root.panel == null) return;
            float dt = Time.unscaledDeltaTime;
            foreach (var f in floaters)
            {
                if (!f.on) continue;
                f.age += dt;
                if (f.age >= f.life) { f.on = false; f.l.style.display = DisplayStyle.None; continue; }
                f.vel.y -= 3f * dt;
                f.world += f.vel * dt;
                Vector3 vp = cam.WorldToViewportPoint(f.world);
                if (vp.z < 0) { f.l.style.display = DisplayStyle.None; continue; }
                f.l.style.display = DisplayStyle.Flex;
                Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, f.world, cam);
                if (shop != null && shop.worldBound.Contains(p)) { f.l.style.display = DisplayStyle.None; continue; } // no asomar tras el Taller
                f.l.style.left = p.x;
                f.l.style.top = p.y;
                float k = f.age / f.life;
                f.l.style.opacity = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
                float sc = k < 0.12f ? Mathf.Lerp(0.4f, 1.15f, k / 0.12f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((k - 0.12f) * 4f));
                f.l.style.scale = new Scale(new Vector3(sc, sc, 1));
            }
        }

        // ================================================================= Ajustes
        void CycleQuality()
        {
            var gm = GameManager.I;
            int n = QualitySettings.names.Length;
            int q = (QualitySettings.GetQualityLevel() + 1) % n;
            QualitySettings.SetQualityLevel(q, true);
            gm.S.quality = q;
            RefreshSettingsButtons();
        }

        void ToggleMute()
        {
            var gm = GameManager.I;
            gm.S.muted = !gm.S.muted;
            AudioFX.I?.SetMuted(gm.S.muted);
            RefreshSettingsButtons();
        }

        void RefreshSettingsButtons()
        {
            qualityBtn.text = "Calidad: " + QualitySettings.names[QualitySettings.GetQualityLevel()];
            muteBtn.text = GameManager.I.S.muted ? "Sonido: No" : "Sonido: Sí";
        }
    }
}
