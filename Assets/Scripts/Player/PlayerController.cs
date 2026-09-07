using Hollowmere.Core;
using Hollowmere.Gen;
using UnityEngine;

namespace Hollowmere.Player
{
    /// <summary>
    /// First-person movement. Deliberately has no jump: vertical freedom trivialises
    /// stalker AI and reads as arcadey in a horror context.
    ///
    /// The important output for the rest of the game is <see cref="NoiseRadius"/> - how
    /// far away the entity can hear you right now.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Header("Speeds")]
        public float walkSpeed = 2.6f;
        public float sprintSpeed = 5.2f;
        public float crouchSpeed = 1.3f;
        public float acceleration = 12f;

        [Header("Body")]
        public float standHeight = 1.8f;
        public float crouchHeight = 1.05f;
        public float standEyeHeight = 1.65f;
        public float crouchEyeHeight = 0.95f;
        public float gravity = -18f;

        [Header("Noise (world units the entity can hear)")]
        public float sprintNoise = 22f;
        public float walkNoise = 9f;
        public float crouchNoise = 2.5f;
        public float idleNoise = 1f;

        public Camera Cam { get; private set; }
        public Transform CameraRig { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsSprinting { get; private set; }
        public float CurrentSpeed { get; private set; }
        public float NoiseRadius { get; private set; }
        public bool Frozen { get; set; }

        CharacterController _cc;
        PlayerStatus _status;
        AudioSource _footstepSource;

        Vector3 _velocity;
        Vector3 _horizontalVelocity;
        float _pitch;
        float _bobTimer;
        float _distanceSinceStep;
        float _eyeHeight;
        int _stepVariant;

        // --- construction ----------------------------------------------------

        /// <summary>Builds the whole player rig from code. No prefabs anywhere in this project.</summary>
        public static PlayerController Create(Vector3 spawn)
        {
            var go = new GameObject("Player");
            go.tag = "Player";
            go.transform.position = spawn + Vector3.up * 0.2f;

            var cc = go.AddComponent<CharacterController>();
            cc.radius = 0.35f;
            cc.height = 1.8f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 45f;
            cc.stepOffset = 0.4f;
            cc.skinWidth = 0.03f;

            // PlayerStatus must exist before PlayerController: AddComponent runs Awake
            // immediately, and PlayerController.Awake caches a reference to it.
            go.AddComponent<PlayerStatus>();

            var player = go.AddComponent<PlayerController>();

            var rig = new GameObject("CameraRig").transform;
            rig.SetParent(go.transform, false);
            rig.localPosition = new Vector3(0f, 1.65f, 0f);
            player.CameraRig = rig;

            var camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(rig, false);

            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 120f;
            cam.fieldOfView = GameSettings.Current.fieldOfView;
            camGo.AddComponent<AudioListener>();
            player.Cam = cam;

            go.AddComponent<Interactor>();
            Flashlight.Attach(player);

            return player;
        }

        // --- lifecycle -------------------------------------------------------

        void Awake()
        {
            Instance = this;
            _cc = GetComponent<CharacterController>();
            _status = GetComponent<PlayerStatus>();
            _eyeHeight = standEyeHeight;

            _footstepSource = gameObject.AddComponent<AudioSource>();
            _footstepSource.spatialBlend = 0f;
            _footstepSource.playOnAwake = false;
            _footstepSource.volume = 0.55f;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (Frozen)
            {
                _horizontalVelocity = Vector3.zero;
                CurrentSpeed = 0f;
                NoiseRadius = idleNoise;
                return;
            }

            HandleLook();
            HandleCrouch();
            HandleMovement();
            HandleHeadBob();
            HandleFootsteps();
            UpdateNoise();
        }

        // --- look ------------------------------------------------------------

        void HandleLook()
        {
            var look = PlayerInput.Look * GameSettings.Current.mouseSensitivity;
            if (GameSettings.Current.invertY) look.y = -look.y;

            transform.Rotate(Vector3.up, look.x, Space.Self);

            _pitch = Mathf.Clamp(_pitch - look.y, -85f, 85f);
            CameraRig.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        // --- movement --------------------------------------------------------

        void HandleCrouch()
        {
            bool wantsCrouch = PlayerInput.CrouchHeld;

            // Refuse to stand up under a ceiling, or the controller pops through geometry.
            if (!wantsCrouch && IsCrouching && Physics.SphereCast(
                    transform.position + Vector3.up * 0.4f, _cc.radius * 0.95f,
                    Vector3.up, out _, standHeight - 0.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                wantsCrouch = true;
            }

            IsCrouching = wantsCrouch;

            float targetHeight = IsCrouching ? crouchHeight : standHeight;
            float targetEye = IsCrouching ? crouchEyeHeight : standEyeHeight;

            _cc.height = Mathf.MoveTowards(_cc.height, targetHeight, Time.deltaTime * 6f);
            _cc.center = new Vector3(0f, _cc.height * 0.5f, 0f);
            _eyeHeight = Mathf.MoveTowards(_eyeHeight, targetEye, Time.deltaTime * 6f);
        }

        void HandleMovement()
        {
            var input = PlayerInput.Move;
            if (input.sqrMagnitude > 1f) input.Normalize();

            bool wantsSprint = PlayerInput.SprintHeld && !IsCrouching && input.y > 0.1f;
            IsSprinting = wantsSprint && _status != null && _status.TrySpendStamina(Time.deltaTime);

            float targetSpeed = IsCrouching ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed);
            var wish = (transform.right * input.x + transform.forward * input.y) * targetSpeed;

            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, wish, acceleration * Time.deltaTime);
            CurrentSpeed = _horizontalVelocity.magnitude;

            // Small constant downforce keeps isGrounded stable on the seams between boxes.
            if (_cc.isGrounded && _velocity.y < 0f) _velocity.y = -2f;
            _velocity.y += gravity * Time.deltaTime;

            var motion = _horizontalVelocity + Vector3.up * _velocity.y;
            _cc.Move(motion * Time.deltaTime);

            // FOV nudge on sprint - cheap, and it does a lot for the sense of speed.
            if (Cam != null)
            {
                float targetFov = GameSettings.Current.fieldOfView + (IsSprinting ? 8f : 0f);
                Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, targetFov, Time.deltaTime * 6f);
            }
        }

        void HandleHeadBob()
        {
            float bobAmount = 0f, bobSpeed = 0f;

            if (GameSettings.Current.headBob && CurrentSpeed > 0.15f && _cc.isGrounded)
            {
                bobSpeed = IsSprinting ? 13f : (IsCrouching ? 6f : 8.5f);
                bobAmount = IsSprinting ? 0.075f : (IsCrouching ? 0.02f : 0.04f);
                _bobTimer += Time.deltaTime * bobSpeed;
            }
            else
            {
                _bobTimer = Mathf.MoveTowards(_bobTimer, 0f, Time.deltaTime * 6f);
            }

            float vertical = Mathf.Sin(_bobTimer) * bobAmount;
            float horizontal = Mathf.Cos(_bobTimer * 0.5f) * bobAmount * 0.6f;

            CameraRig.localPosition = Vector3.Lerp(
                CameraRig.localPosition,
                new Vector3(horizontal, _eyeHeight + vertical, 0f),
                Time.deltaTime * 12f);
        }

        void HandleFootsteps()
        {
            if (!_cc.isGrounded || CurrentSpeed < 0.15f)
            {
                _distanceSinceStep = 0f;
                return;
            }

            _distanceSinceStep += CurrentSpeed * Time.deltaTime;
            float stride = IsCrouching ? 1.1f : (IsSprinting ? 2.0f : 1.5f);
            if (_distanceSinceStep < stride) return;

            _distanceSinceStep = 0f;
            _stepVariant = (_stepVariant + 1) % 4;

            _footstepSource.pitch = Random.Range(0.9f, 1.12f);
            _footstepSource.volume = IsCrouching ? 0.18f : (IsSprinting ? 0.7f : 0.45f);
            _footstepSource.PlayOneShot(ProceduralAudio.Footstep(_stepVariant));
        }

        void UpdateNoise()
        {
            float target;
            if (CurrentSpeed < 0.15f) target = idleNoise;
            else if (IsCrouching) target = crouchNoise;
            else if (IsSprinting) target = sprintNoise;
            else target = walkNoise;

            // Holding your breath makes you quieter, at the cost of the breath meter.
            if (_status != null && _status.IsHoldingBreath) target *= 0.35f;

            NoiseRadius = Mathf.Lerp(NoiseRadius, target, Time.deltaTime * 8f);
        }
    }
}
