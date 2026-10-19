using UnityEngine;
using UnityEngine.InputSystem;

namespace Marmoreal
{
    /// <summary>
    /// La canica escultora. El daño de cada choque depende de la velocidad (energía cinética):
    /// rodar por las laderas del cuenco para ganar inercia es parte del juego.
    /// WASD/Flechas/Stick: rodar · Espacio/A: Saltar (en el aire: Golpe en Picado)
    /// Shift/B: Embestida · F/Y/Clic izq.: Onda de Resonancia
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerMarble : MonoBehaviour
    {
        static PlayerMarble _i;
        /// <summary>Singleton resistente a recargas de dominio (se re-busca si se pierde).</summary>
        public static PlayerMarble I { get { if (!_i) _i = FindFirstObjectByType<PlayerMarble>(); return _i; } private set => _i = value; }

        public CameraRig cameraRig;
        public MeshRenderer body;
        [Tooltip("Material de la canica por número de pulidos (0 = Carrara, 1 = Rosa, ...)")]
        public Material[] prestigeMaterials;
        public TrailRenderer trail;
        [System.NonSerialized] string appliedColor;
        public float radius = 0.75f;
        public float acceleration = 26f;
        public float airControl = 0.35f;
        public float dashSpeed = 17f;
        public float dashTime = 0.45f;
        [Header("Salto")]
        public float jumpSpeed = 9f;
        [Tooltip("Velocidad de caída del Golpe en Picado (segundo salto en el aire)")]
        public float slamSpeed = 24f;
        [Tooltip("Margen para saltar justo después de dejar el suelo / pulsar justo antes de tocarlo")]
        public float coyoteTime = 0.12f, jumpBuffer = 0.15f;
        [Tooltip("Gravedad extra al caer (salto de personaje: sube suave, cae rápido)")]
        public float fallGravityMult = 1.8f;
        public LayerMask groundMask = ~0;
        [Header("Límite de la arena")]
        [Tooltip("Radio a partir del cual una pared invisible empuja hacia dentro")]
        public float boundaryRadius = 41f;
        public Vector3 respawnPoint = new Vector3(0, 2, -6);
        [Tooltip("Depuración: la canica juega sola (F9 lo alterna en el Editor / builds de desarrollo)")]
        public bool autopilot;

        public bool Dashing => dashTimer > 0;
        public bool Slamming { get; private set; }
        public bool Grounded => grounded;
        /// <summary>Cargas de Embestida disponibles (mejora Doble Embestida).</summary>
        public int DashChargesLeft => dashCharges;
        public float DashReady01
        {
            get
            {
                var gm = GameManager.I;
                if (!gm) return 1f;
                if (gm.FuryActive) return 1f;
                float partial = dashCharges >= gm.DashCharges ? 0f : 1f - Mathf.Clamp01(dashCooldown / gm.DashCooldown);
                return Mathf.Clamp01((dashCharges + partial) / gm.DashCharges);
            }
        }
        public float Speed => rb ? rb.linearVelocity.magnitude : 0f;
        public Vector3 Velocity => rb ? rb.linearVelocity : Vector3.zero;

        Rigidbody rb;
        Vector2 moveInput;
        Vector3 lastVel;
        float dashTimer, dashCooldown, dashGap;
        int dashCharges = 1;
        bool grounded;
        float impactCooldown;
        float coyoteTimer, jumpBufferTimer, jumpGrace;
        bool canSlam;
        readonly RaycastHit[] groundHits = new RaycastHit[4];

        void Awake()
        {
            I = this;
            rb = GetComponent<Rigidbody>();
            rb.maxAngularVelocity = 80f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        [System.NonSerialized] GameManager subscribed;

        void Start() { if (!cameraRig) cameraRig = CameraRig.I; ApplyLook(); }

        void OnDestroy()
        {
            if (subscribed) subscribed.Prestiged -= OnPrestige;
        }

        void OnPrestige()
        {
            ApplyLook();
            Respawn();
        }

        /// <summary>
        /// Teletransporte seguro: con interpolación hay que mover el Rigidbody (no solo el Transform),
        /// si no la física devuelve la canica a la posición anterior en cada paso.
        /// </summary>
        /// <summary>Impulso externo (lava de la Cantera Volcánica).</summary>
        public void Launch(Vector3 velocity)
        {
            rb.linearVelocity = velocity;
            rb.WakeUp();
        }

        public void Respawn()
        {
            var p = respawnPoint;
            // Con un Coloso/jefe en pie, nunca reaparecer dentro de él: punto fuera de su radio de despeje
            var sp = MonolithSpawner.I;
            var boss = sp ? sp.Boss : null;
            if (boss)
            {
                Vector3 bp = boss.transform.position, d = p - bp; d.y = 0;
                float r = sp.bossClearRadius + 1.5f;
                if (d.sqrMagnitude < r * r)
                {
                    if (d.sqrMagnitude < 0.01f) d = Vector3.back;
                    p = bp + d.normalized * r;
                    p.y = respawnPoint.y + ArenaShape.Height(p);
                }
            }
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = p;
            transform.position = p;
            Physics.SyncTransforms();
            rb.WakeUp();
            dashTimer = 0; Slamming = false;
            AudioFX.I?.Play(Sfx.Dash, 0.4f, 0.7f);
        }

        /// <summary>Canica elegida en la Colección: material + estela + color de la onda (solo cosmético).</summary>
        public void ApplyLook()
        {
            var gm = GameManager.I;
            if (!body || !gm) return;
            appliedColor = gm.S.marbleColor;
            var c = gm.CurrentColor;
            if (c != null && c.material) body.sharedMaterial = c.material;
            else if (prestigeMaterials != null && prestigeMaterials.Length > 0) body.sharedMaterial = prestigeMaterials[0];
            if (c == null) return;
            if (trail) trail.material.SetColor("_UnlitColor", c.trailColor);
            Shockwave.I?.SetTint(c.waveColor);
        }

        void Update()
        {
            if (GameManager.I && subscribed != GameManager.I) { GameManager.I.Prestiged += OnPrestige; subscribed = GameManager.I; ApplyLook(); }
            if (GameManager.I && appliedColor != GameManager.I.S.marbleColor) ApplyLook();
            bool blocked = Hud.I && Hud.I.BlocksGameplay;
            moveInput = Vector2.zero;
            if (!blocked)
            {
                var k = Keyboard.current;
                if (k != null)
                {
                    if (k.wKey.isPressed || k.upArrowKey.isPressed) moveInput.y += 1;
                    if (k.sKey.isPressed || k.downArrowKey.isPressed) moveInput.y -= 1;
                    if (k.dKey.isPressed || k.rightArrowKey.isPressed) moveInput.x += 1;
                    if (k.aKey.isPressed || k.leftArrowKey.isPressed) moveInput.x -= 1;
                    if (k.spaceKey.wasPressedThisFrame) jumpBufferTimer = jumpBuffer;
                    if (k.leftShiftKey.wasPressedThisFrame || k.rightShiftKey.wasPressedThisFrame) TryDash();
                    if (k.fKey.wasPressedThisFrame) Shockwave.I?.TryRelease();
                }
                var g = Gamepad.current;
                if (g != null)
                {
                    moveInput += g.leftStick.ReadValue();
                    if (g.buttonSouth.wasPressedThisFrame) jumpBufferTimer = jumpBuffer;
                    if (g.buttonEast.wasPressedThisFrame || g.rightShoulder.wasPressedThisFrame) TryDash();
                    if (g.buttonNorth.wasPressedThisFrame) Shockwave.I?.TryRelease();
                }
                var m = Mouse.current;
                if (m != null && m.leftButton.wasPressedThisFrame && ((cameraRig && cameraRig.locked) || !(Hud.I && Hud.I.PointerOverUI))) Shockwave.I?.TryRelease();
                moveInput = Vector2.ClampMagnitude(moveInput, 1f);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame) autopilot = !autopilot;
#endif
            if (autopilot && !blocked) Autopilot();

            dashTimer -= Time.deltaTime;
            dashGap -= Time.deltaTime;
            RechargeDash();
            jumpBufferTimer -= Time.deltaTime;
            jumpGrace -= Time.deltaTime;
            impactCooldown -= Time.deltaTime;

            float maxSpeed = GameManager.I ? GameManager.I.MaxSpeed : 11f;
            AudioFX.I?.SetRoll(Speed / maxSpeed, grounded);
            if (trail) trail.emitting = Speed > maxSpeed * 0.75f || Dashing;

            if (rb.position.y < -12f) Respawn();
        }

        void FixedUpdate()
        {
            // Suelo real (cuenco, bloques, estatuas…) con un SphereCast corto hacia abajo
            grounded = false;
            int n = Physics.SphereCastNonAlloc(rb.position + Vector3.up * 0.1f, radius * 0.9f, Vector3.down, groundHits, 0.35f, groundMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (groundHits[i].collider.attachedRigidbody != rb && groundHits[i].normal.y > 0.45f) { grounded = true; break; }
            if (grounded) { coyoteTimer = coyoteTime; canSlam = true; if (Slamming) Slamming = false; }
            else coyoteTimer -= Time.fixedDeltaTime;

            if (jumpBufferTimer > 0)
            {
                if (coyoteTimer > 0) Jump();
                else if (canSlam && rb.position.y > ArenaShape.Height(rb.position) + 1.6f) Slam();
            }

            // Salto de personaje: gravedad extra al caer
            if (!grounded && rb.linearVelocity.y < 0f)
                rb.AddForce(Physics.gravity * (fallGravityMult - 1f), ForceMode.Acceleration);

            KeepInsideArena();

            Vector3 dir = Vector3.zero;
            if (autopilot) dir = autoDir;
            else if (cameraRig)
            {
                Vector3 f = cameraRig.transform.forward; f.y = 0; f.Normalize();
                Vector3 r = cameraRig.transform.right; r.y = 0; r.Normalize();
                dir = f * moveInput.y + r * moveInput.x;
            }
            else dir = new Vector3(moveInput.x, 0, moveInput.y);

            float maxSpeed = GameManager.I ? GameManager.I.MaxSpeed : 11f;
            float accel = acceleration * (grounded ? 1f : airControl) * (0.6f + maxSpeed / 25f);
            rb.AddForce(dir * accel, ForceMode.Acceleration);

            // Rodadura visual coherente: par en la dirección de avance
            if (dir.sqrMagnitude > 0.01f) rb.AddTorque(Vector3.Cross(Vector3.up, dir) * accel * 0.5f, ForceMode.Acceleration);

            // Límite suave de velocidad horizontal (la Embestida y las laderas pueden superarlo un poco)
            Vector3 v = rb.linearVelocity;
            Vector3 hv = new Vector3(v.x, 0, v.z);
            float cap = Dashing ? maxSpeed * 1.9f : maxSpeed * 1.15f;
            if (hv.magnitude > cap)
            {
                Vector3 target = hv.normalized * cap;
                hv = Vector3.Lerp(hv, target, 0.15f);
                rb.linearVelocity = new Vector3(hv.x, v.y, hv.z);
            }

            lastVel = rb.linearVelocity;
        }

        void Jump()
        {
            jumpBufferTimer = 0; coyoteTimer = 0; jumpGrace = 0.15f;
            Vector3 v = rb.linearVelocity;
            rb.linearVelocity = new Vector3(v.x, Mathf.Max(v.y, 0f) + jumpSpeed, v.z);
            AudioFX.I?.Play(Sfx.Dash, 0.35f, 1.6f);
        }

        /// <summary>Golpe en Picado: segundo salto en el aire → cae en vertical. El daño es el normal según la velocidad de caída.</summary>
        void Slam()
        {
            jumpBufferTimer = 0; canSlam = false;
            Slamming = true;
            Vector3 v = rb.linearVelocity;
            rb.linearVelocity = new Vector3(v.x * 0.3f, -slamSpeed, v.z * 0.3f);
            AudioFX.I?.Play(Sfx.Dash, 0.6f, 0.75f);
            cameraRig?.Kick(5f);
        }

        /// <summary>Pared invisible suave en el borde de la terraza: impide caer al vacío.</summary>
        void KeepInsideArena()
        {
            Vector3 p = rb.position;
            Vector3 flat = new Vector3(p.x, 0, p.z);
            float r = flat.magnitude;
            if (r < boundaryRadius || r < 0.01f) return;
            Vector3 outDir = flat / r;
            Vector3 v = rb.linearVelocity;
            float outward = Vector3.Dot(v, outDir);
            if (outward > 0) v -= outDir * outward * 1.6f; // rebota un poco hacia dentro
            rb.linearVelocity = v;
            if (r > boundaryRadius + 0.5f) rb.position = new Vector3(outDir.x * (boundaryRadius + 0.5f), p.y, outDir.z * (boundaryRadius + 0.5f));
        }

        Vector3 autoDir;
        bool autoRetreat;

        void Autopilot()
        {
            var sp = MonolithSpawner.I;
            var m = sp ? sp.Nearest(transform.position, 200f) : null;
            if (!m) { autoDir = (-transform.position).normalized; autoDir.y = 0; return; }
            Vector3 to = m.ClosestPoint(transform.position) - transform.position; to.y = 0;
            float d = to.magnitude;
            // Toma carrerilla: si está muy cerca y lento, se aleja un poco para volver a embestir
            Vector3 hv = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            if (autoRetreat) { autoDir = -to.normalized; if (d > 6.5f) autoRetreat = false; }
            else { autoDir = to.normalized; if (d < radius + 0.6f) autoRetreat = true; }
            if (!autoRetreat && d < 8f && d > 2.5f && Vector3.Dot(hv.normalized, to.normalized) > 0.8f) TryDash();
            if (GameManager.I && GameManager.I.Resonance >= 1f) Shockwave.I?.TryRelease();
            var ms = MusaSpawner.I;
            if (ms && ms.MusaActive) { Vector3 tm = ms.MusaPosition - transform.position; tm.y = 0; autoDir = tm.normalized; autoRetreat = false; }
            var ps = PowerSystem.I;
            if (ps) for (int i = 0; i < ps.Count; i++) if (ps.IsReady(i) && ps.TryCast(i)) break;

            // Compra la mejora más barata (mismo comportamiento que Tools/balance_sim.py)
            autoBuyTimer -= Time.deltaTime;
            var gm = GameManager.I;
            if (autoBuyTimer <= 0 && gm)
            {
                autoBuyTimer = 0.5f;
                UpgradeDef best = null;
                foreach (var u in gm.config.upgrades)
                    if (u.currency == Currency.Dust && gm.IsUnlocked(u) && !gm.IsMaxed(u) && (best == null || gm.CostOf(u) < gm.CostOf(best))) best = u;
                if (best != null) gm.TryBuy(best);
            }
        }

        float autoBuyTimer;

        /// <summary>Recarga las cargas de Embestida de una en una.</summary>
        void RechargeDash()
        {
            var gm = GameManager.I;
            if (!gm) return;
            int max = gm.DashCharges;
            if (dashCharges > max) dashCharges = max;
            if (dashCharges >= max) { dashCooldown = gm.DashCooldown; return; }
            dashCooldown -= Time.deltaTime;
            if (dashCooldown <= 0f) { dashCharges++; dashCooldown = gm.DashCooldown; }
        }

        void TryDash()
        {
            var gm0 = GameManager.I;
            if (!gm0 || dashGap > 0) return;
            if (gm0.Mutator != null && gm0.Mutator.noDash) return; // mutador Furia Ciega
            bool fury = gm0.FuryActive; // Furia Divina: Embestidas sin recarga
            if (!fury && dashCharges <= 0) return;
            // Con la cámara bloqueada la Embestida sale hacia donde se mira; si no, sigue la inercia
            Vector3 dir = cameraRig && cameraRig.locked ? cameraRig.transform.forward : new Vector3(lastVel.x, 0, lastVel.z);
            dir.y = 0;
            if (moveInput.sqrMagnitude > 0.01f && cameraRig)
            {
                Vector3 f = cameraRig.transform.forward; f.y = 0; f.Normalize();
                Vector3 r = cameraRig.transform.right; r.y = 0; r.Normalize();
                dir = f * moveInput.y + r * moveInput.x;
            }
            if (autopilot) dir = autoDir;
            if (dir.sqrMagnitude < 0.01f && cameraRig) { dir = cameraRig.transform.forward; dir.y = 0; }
            dir.Normalize();
            Vector3 v = rb.linearVelocity;
            float keep = Mathf.Max(0, Vector3.Dot(new Vector3(v.x, 0, v.z), dir));
            rb.linearVelocity = dir * Mathf.Max(dashSpeed, keep + dashSpeed * 0.5f) + Vector3.up * Mathf.Max(0, v.y);
            dashTimer = dashTime;
            dashGap = fury ? 0.12f : 0.18f;
            if (!fury) dashCharges--;
            AudioFX.I?.Play(Sfx.Dash, 0.7f, Random.Range(0.95f, 1.1f));
            cameraRig?.Kick(8f);
        }

        /// <summary>Al aterrizar (contacto con normal hacia arriba) se anula el rebote vertical.</summary>
        void CancelLandingBounce(Collision c)
        {
            for (int i = 0; i < c.contactCount; i++)
            {
                if (c.GetContact(i).normal.y < 0.5f) continue;
                Vector3 v = rb.linearVelocity;
                if (v.y > 0f) rb.linearVelocity = new Vector3(v.x, 0f, v.z);
                return;
            }
        }

        void OnCollisionStay(Collision c)
        {
            if (jumpGrace <= 0f) CancelLandingBounce(c);
        }

        void OnCollisionEnter(Collision c)
        {
            if (jumpGrace <= 0f) CancelLandingBounce(c);
            if (!c.collider.TryGetComponent(out Monolith m) || !m.IsTargetable) return;
            var gm = GameManager.I;
            if (!gm) return;

            var cp = c.GetContact(0);
            float approach = Mathf.Abs(Vector3.Dot(lastVel, cp.normal));
            approach = Mathf.Max(approach, c.relativeVelocity.magnitude * 0.6f);
            if (impactCooldown > 0 && approach < 6f) return;
            impactCooldown = 0.08f;

            float dmg = gm.ImpactDamageFor(approach, Dashing, out bool crit);
            if (dmg <= 0) return;
            m.TakeDamage(dmg, cp.point, HitSource.Player, crit);
            gm.RegisterImpact(dmg);

            float k = Mathf.Clamp01(approach / Mathf.Max(1f, gm.MaxSpeed));
            AudioFX.I?.Play(Sfx.Impact, 0.35f + 0.65f * k, Random.Range(0.9f, 1.1f) * (crit ? 0.85f : 1f));
            cameraRig?.Shake(0.08f + 0.25f * k + (crit ? 0.15f : 0f));
            Hud.I?.HitStop(crit ? 0.05f : 0.02f * k);

            if (gm.Has("rebote"))
            {
                Vector3 n = cp.normal; n.y = Mathf.Max(0.05f, n.y * 0.3f);
                rb.AddForce(n.normalized * approach * gm.Value("rebote"), ForceMode.VelocityChange);
            }
            bool slam = Slamming;
            if (slam) { Slamming = false; cameraRig?.Shake(0.35f); }
            if ((Dashing || slam) && gm.Has("sismo"))
                Shockwave.I?.Mini(cp.point, gm.ImpactDamage * gm.Value("sismo"), 3.25f + 0.75f * gm.Level("sismo"));
        }
    }
}
