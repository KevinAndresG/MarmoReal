using UnityEngine;
using UnityEngine.InputSystem;

namespace Marmoreal
{
    /// <summary>
    /// Cámara orbital que sigue a la canica. Clic derecho + arrastrar o Q/E para girar,
    /// rueda para zoom. V bloquea la cámara (tercera persona: cursor oculto, el ratón gira siempre).
    /// Incluye temblor (trauma²) y "kick" de FOV en la Embestida.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        static CameraRig _i;
        /// <summary>Singleton resistente a recargas de dominio (se re-busca si se pierde).</summary>
        public static CameraRig I { get { if (!_i) _i = FindFirstObjectByType<CameraRig>(); return _i; } private set => _i = value; }

        public Transform target;
        public float distance = 13f;
        [Tooltip("Distancia extra temporal (p. ej. el poder Coloso)")]
        [System.NonSerialized] public float extraDistance;
        float extraSmoothed;
        public float minDistance = 6f, maxDistance = 26f;
        public float yaw = 0f;
        public float pitch = 32f;
        public float minPitch = 10f, maxPitch = 70f;
        public float followSharpness = 10f;
        public float lookAhead = 0.35f;
        public float mouseSensitivity = 0.15f;
        public float keyRotateSpeed = 110f;
        public float baseFov = 55f;
        [Tooltip("Vista del título: órbita lenta alrededor del santuario")]
        public bool attractMode = true;
        [System.NonSerialized] public bool locked; // tercera persona (tecla V; la C abre la Colección)

        Camera cam;
        Vector3 focus;
        float trauma;
        float fovKick;
        float targetDistance;
        float blend = 1f;
        Vector3 blendFromPos;
        Quaternion blendFromRot;

        /// <summary>Sale del modo título con una transición suave hasta la canica.</summary>
        public void BeginPlay()
        {
            if (!attractMode) return;
            attractMode = false;
            blend = 0f;
            blendFromPos = transform.position;
            blendFromRot = transform.rotation;
            if (target) { focus = target.position; yaw = 0f; }
        }

        void Awake()
        {
            I = this;
            cam = GetComponent<Camera>();
            targetDistance = distance;
            if (target) focus = target.position;
        }

        public void Shake(float amount) => trauma = Mathf.Clamp01(trauma + amount);
        public void Kick(float fov) => fovKick = Mathf.Max(fovKick, fov);

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            float sens = GameManager.I ? GameManager.I.S.cameraSensitivity : 1f;
            bool blocked = Hud.I && Hud.I.BlocksGameplay;

            if (attractMode)
            {
                yaw += dt * 6f;
                Vector3 c = new Vector3(0, 2f, 0);
                Quaternion r0 = Quaternion.Euler(22f, yaw, 0);
                transform.SetPositionAndRotation(c - r0 * Vector3.forward * 34f + Vector3.up * 4f, r0);
                cam.fieldOfView = baseFov;
                if (target) focus = target.position;
                return;
            }

            var kb = Keyboard.current;
            if (!blocked && kb != null && kb.vKey.wasPressedThisFrame) locked = !locked;
            // Los menús abiertos sueltan el cursor; al cerrarlos vuelve a bloquearse
            bool cursorLocked = locked && !blocked;
            Cursor.lockState = cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !cursorLocked;

            if (!blocked)
            {
                var m = Mouse.current;
                if (m != null)
                {
                    if (locked || m.rightButton.isPressed)
                    {
                        Vector2 d = m.delta.ReadValue();
                        yaw += d.x * mouseSensitivity * sens;
                        pitch -= d.y * mouseSensitivity * sens;
                    }
                    float scroll = m.scroll.ReadValue().y;
                    if (Mathf.Abs(scroll) > 0.01f && (locked || !(Hud.I && Hud.I.PointerOverUI)))
                        targetDistance = Mathf.Clamp(targetDistance - Mathf.Sign(scroll) * 1.5f, minDistance, maxDistance);
                }
                var k = Keyboard.current;
                if (k != null)
                {
                    if (k.qKey.isPressed) yaw -= keyRotateSpeed * dt * sens;
                    if (k.eKey.isPressed) yaw += keyRotateSpeed * dt * sens;
                }
                var g = Gamepad.current;
                if (g != null)
                {
                    Vector2 rs = g.rightStick.ReadValue();
                    yaw += rs.x * keyRotateSpeed * dt * sens;
                    pitch -= rs.y * keyRotateSpeed * 0.6f * dt * sens;
                }
            }
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            distance = Mathf.Lerp(distance, targetDistance, 1f - Mathf.Exp(-dt * 8f));

            if (target)
            {
                Vector3 vel = PlayerMarble.I ? PlayerMarble.I.Velocity : Vector3.zero;
                vel.y = 0;
                Vector3 desired = target.position + vel * lookAhead + Vector3.up * 0.8f;
                focus = Vector3.Lerp(focus, desired, 1f - Mathf.Exp(-dt * followSharpness));
            }

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0);
            extraSmoothed = Mathf.Lerp(extraSmoothed, extraDistance, 1f - Mathf.Exp(-dt * 3f));
            Vector3 pos = focus - rot * Vector3.forward * (distance + extraSmoothed);
            float minY = ArenaShape.Height(pos) + 0.6f;
            if (pos.y < minY) pos.y = minY;

            // Temblor
            trauma = Mathf.Max(0, trauma - dt * 1.6f);
            float sh = trauma * trauma;
            float t = Time.unscaledTime * 28f;
            Vector3 offs = new Vector3(Mathf.PerlinNoise(t, 0.1f) - 0.5f, Mathf.PerlinNoise(0.3f, t) - 0.5f, 0) * sh * 0.6f;
            Quaternion roll = Quaternion.Euler(0, 0, (Mathf.PerlinNoise(t, t) - 0.5f) * sh * 3f);

            Vector3 finalPos = pos + rot * offs;
            Quaternion finalRot = Quaternion.LookRotation(focus - pos) * roll;
            if (blend < 1f)
            {
                blend = Mathf.Min(1f, blend + dt / 1.4f);
                float e = blend * blend * (3f - 2f * blend);
                finalPos = Vector3.Lerp(blendFromPos, finalPos, e);
                finalRot = Quaternion.Slerp(blendFromRot, finalRot, e);
            }
            transform.SetPositionAndRotation(finalPos, finalRot);

            fovKick = Mathf.Lerp(fovKick, 0, 1f - Mathf.Exp(-dt * 4f));
            float speedFov = PlayerMarble.I && GameManager.I ? Mathf.Clamp01(PlayerMarble.I.Speed / GameManager.I.MaxSpeed - 0.5f) * 6f : 0f;
            cam.fieldOfView = baseFov + fovKick + speedFov;
        }
    }
}
