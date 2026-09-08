using System.Collections.Generic;
using Hollowmere.Core;
using Hollowmere.Gen;
using Hollowmere.Interact;
using Hollowmere.Player;
using UnityEngine;

namespace Hollowmere.Horror
{
    public enum EntityState
    {
        Patrol,
        Investigate,
        Chase,
        Search
    }

    /// <summary>
    /// The stalker. A four-state machine over grid A*.
    ///
    /// The design rule throughout: the entity should always be *findable* by the player.
    /// It telegraphs with sound before it arrives, it loses you if you break line of
    /// sight and stay quiet, and it never teleports. An enemy that cheats is not scary,
    /// it is just unfair, and players stop engaging with the systems.
    /// </summary>
    public class Entity : MonoBehaviour
    {
        public static Entity Instance { get; private set; }

        [Header("Movement")]
        public float patrolSpeed = 1.7f;
        public float investigateSpeed = 2.4f;
        public float chaseSpeed = 4.3f;
        public float turnSpeed = 8f;

        [Header("Vision")]
        public float viewAngle = 110f;
        public float viewDistanceLit = 26f;
        public float viewDistanceDark = 7.5f;
        public float flashlightVisibilityBonus = 14f;

        [Header("Behaviour")]
        public float killDistance = 1.35f;
        public float loseSightGrace = 3.5f;
        public float searchDuration = 12f;

        public EntityState State { get; private set; } = EntityState.Patrol;
        public bool IsChasing => State == EntityState.Chase;
        public Vector3 LastKnownPlayerPosition { get; private set; }

        LevelGrid _grid;
        List<Vector3> _patrolPoints;
        readonly List<Vector3> _path = new List<Vector3>();
        int _pathIndex;

        float _repathTimer;
        float _lostSightTimer;
        float _searchTimer;
        Vector3 _target;

        AudioSource _growl;
        Transform _eyes;

        // --- proportions -----------------------------------------------------
        // Long arms and a small head on a tall body read as wrong at a glance, which
        // is the whole job of a silhouette seen at the edge of a torch beam.

        const float TotalHeight = 2.30f;
        const float HeadSize    = 0.45f;
        const float BodyWidth   = 0.75f;
        const float BodyHeight  = 1.15f;
        const float BodyDepth   = 0.40f;
        const float ArmThick    = 0.16f;
        const float ArmLength   = 1.00f;

        // Derived: the head sits straight on the body with no neck, blocky-style.
        const float BodyBaseY = TotalHeight - HeadSize - BodyHeight;  // 0.70
        const float BodyY     = BodyBaseY + BodyHeight * 0.5f;        // 1.275
        const float ShoulderY = BodyBaseY + BodyHeight;               // 1.85
        const float HeadY     = ShoulderY + HeadSize * 0.5f;          // 2.075
        const float ArmX      = BodyWidth * 0.5f + ArmThick * 0.5f;   // 0.455

        // --- construction ----------------------------------------------------

        public static Entity Spawn(LevelData level)
        {
            var go = new GameObject("Entity");
            go.transform.position = level.EntitySpawn;

            // One trigger collider on the root rather than one per limb: it never shoves
            // the player through a wall, and the kill is decided by distance anyway.
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.height = TotalHeight;
            capsule.radius = 0.40f;
            capsule.center = new Vector3(0f, TotalHeight * 0.5f, 0f);
            capsule.isTrigger = true;

            var skin = ProceduralMaterials.Flat(new Color(0.045f, 0.04f, 0.05f), 0.05f);

            // Body: one big rectangle, unpivoted - it does not move independently.
            Part(go.transform, "Body", new Vector3(0f, BodyY, 0f),
                new Vector3(BodyWidth, BodyHeight, BodyDepth), skin);

            // Head: a square, on its own pivot so it can turn to track the player
            // without shearing the mesh (a scaled parent would scale its children).
            var headPivot = Pivot(go.transform, "HeadPivot", new Vector3(0f, HeadY, 0f));
            Part(headPivot, "HeadMesh", Vector3.zero, Vector3.one * HeadSize, skin);

            var eyeGlow = ProceduralMaterials.Emissive(new Color(1f, 0.15f, 0.1f), 4f);
            float eyeZ = HeadSize * 0.5f + 0.01f;
            Part(headPivot, "EyeLeft", new Vector3(-0.11f, 0.03f, eyeZ), new Vector3(0.07f, 0.035f, 0.02f), eyeGlow);
            Part(headPivot, "EyeRight", new Vector3(0.11f, 0.03f, eyeZ), new Vector3(0.07f, 0.035f, 0.02f), eyeGlow);

            // Arms: small rectangles, pivoted at the shoulder so a rotation swings them
            // from the top rather than spinning them about their middle.
            var armL = Pivot(go.transform, "ArmLeftPivot", new Vector3(-ArmX, ShoulderY, 0f));
            Part(armL, "ArmLeftMesh", new Vector3(0f, -ArmLength * 0.5f, 0f),
                new Vector3(ArmThick, ArmLength, ArmThick), skin);

            var armR = Pivot(go.transform, "ArmRightPivot", new Vector3(ArmX, ShoulderY, 0f));
            Part(armR, "ArmRightMesh", new Vector3(0f, -ArmLength * 0.5f, 0f),
                new Vector3(ArmThick, ArmLength, ArmThick), skin);

            var entity = go.AddComponent<Entity>();
            entity._grid = level.Grid;
            entity._patrolPoints = new List<Vector3>(level.PatrolPoints);
            entity._eyes = headPivot;   // sightlines originate from the head
            return entity;
        }

        /// <summary>Empty transform used as a joint. Stays at scale 1 so children are undistorted.</summary>
        static Transform Pivot(Transform parent, string name, Vector3 localPosition)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            return t;
        }

        /// <summary>A single box limb, positioned in local space so it follows the rig.</summary>
        static GameObject Part(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;

            // The root capsule is the only collider; limb colliders would fight the raycasts.
            Destroy(go.GetComponent<Collider>());
            return go;
        }

        void Awake()
        {
            Instance = this;

            _growl = gameObject.AddComponent<AudioSource>();
            _growl.clip = ProceduralAudio.Growl;
            _growl.loop = true;
            _growl.volume = 0f;
            _growl.spatialBlend = 1f;      // fully 3D so the player can locate it by ear
            _growl.rolloffMode = AudioRolloffMode.Linear;
            _growl.minDistance = 2f;
            _growl.maxDistance = 30f;
            _growl.playOnAwake = false;
            _growl.Play();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start() => PickNewPatrolTarget();

        // --- main loop -------------------------------------------------------

        void Update()
        {
            var player = PlayerController.Instance;
            if (player == null || GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;

            bool canSee = CanSeePlayer(player, out float distanceToPlayer);
            bool canHear = CanHearPlayer(player, distanceToPlayer);

            switch (State)
            {
                case EntityState.Patrol:     TickPatrol(canSee, canHear, player); break;
                case EntityState.Investigate:TickInvestigate(canSee, canHear, player); break;
                case EntityState.Chase:      TickChase(canSee, player); break;
                case EntityState.Search:     TickSearch(canSee, canHear, player); break;
            }

            MoveAlongPath(CurrentSpeed());
            UpdateGrowl(distanceToPlayer);

            if (distanceToPlayer <= killDistance && !IsPlayerSafelyHidden(player))
            {
                GameManager.Instance.OnPlayerCaught();
            }
        }

        float CurrentSpeed()
        {
            switch (State)
            {
                case EntityState.Chase: return chaseSpeed;
                case EntityState.Investigate:
                case EntityState.Search: return investigateSpeed;
                default: return patrolSpeed;
            }
        }

        // --- states ----------------------------------------------------------

        void TickPatrol(bool canSee, bool canHear, PlayerController player)
        {
            if (canSee) { BeginChase(player); return; }
            if (canHear) { BeginInvestigate(player.transform.position); return; }

            if (NoiseSystem.TryGetLoudest(transform.position, out var noise))
            {
                BeginInvestigate(noise);
                return;
            }

            if (_pathIndex >= _path.Count) PickNewPatrolTarget();
        }

        void TickInvestigate(bool canSee, bool canHear, PlayerController player)
        {
            if (canSee) { BeginChase(player); return; }
            if (canHear) { _target = player.transform.position; Repath(_target); return; }

            // Reached the noise and found nothing - sweep the area before giving up.
            if (_pathIndex >= _path.Count) BeginSearch(transform.position);
        }

        void TickChase(bool canSee, PlayerController player)
        {
            if (canSee)
            {
                _lostSightTimer = 0f;
                LastKnownPlayerPosition = player.transform.position;
            }
            else
            {
                _lostSightTimer += Time.deltaTime;
                if (_lostSightTimer >= loseSightGrace)
                {
                    BeginSearch(LastKnownPlayerPosition);
                    return;
                }
            }

            _repathTimer -= Time.deltaTime;
            if (_repathTimer <= 0f)
            {
                _repathTimer = 0.35f;
                Repath(LastKnownPlayerPosition);
            }
        }

        void TickSearch(bool canSee, bool canHear, PlayerController player)
        {
            if (canSee) { BeginChase(player); return; }
            if (canHear) { BeginInvestigate(player.transform.position); return; }

            _searchTimer -= Time.deltaTime;
            if (_searchTimer <= 0f)
            {
                State = EntityState.Patrol;
                PickNewPatrolTarget();
                return;
            }

            // Wander around the last known position rather than standing still.
            if (_pathIndex >= _path.Count)
            {
                var offset = Random.insideUnitCircle * 9f;
                Repath(LastKnownPlayerPosition + new Vector3(offset.x, 0f, offset.y));
            }
        }

        void BeginChase(PlayerController player)
        {
            if (State != EntityState.Chase)
            {
                AudioSource.PlayClipAtPoint(ProceduralAudio.Scream, transform.position, 0.85f);
                GameManager.Instance?.Notify("It has seen you");
            }

            State = EntityState.Chase;
            _lostSightTimer = 0f;
            LastKnownPlayerPosition = player.transform.position;
            Repath(LastKnownPlayerPosition);
        }

        void BeginInvestigate(Vector3 position)
        {
            State = EntityState.Investigate;
            _target = position;
            Repath(position);
        }

        void BeginSearch(Vector3 around)
        {
            State = EntityState.Search;
            _searchTimer = searchDuration;
            LastKnownPlayerPosition = around;
            Repath(around);
        }

        void PickNewPatrolTarget()
        {
            if (_patrolPoints == null || _patrolPoints.Count == 0) return;

            // Bias away from where it already is, so patrols cross the map.
            Vector3 best = _patrolPoints[0];
            float bestScore = -1f;

            for (int i = 0; i < 4; i++)
            {
                var candidate = _patrolPoints[Random.Range(0, _patrolPoints.Count)];
                float score = Vector3.Distance(candidate, transform.position);
                if (score > bestScore) { bestScore = score; best = candidate; }
            }

            _target = best;
            Repath(best);
        }

        // --- senses ----------------------------------------------------------

        bool CanSeePlayer(PlayerController player, out float distance)
        {
            var origin = _eyes != null ? _eyes.position : transform.position + Vector3.up * 1.9f;
            var playerHead = player.transform.position + Vector3.up * 1.4f;
            distance = Vector3.Distance(transform.position, player.transform.position);

            if (IsPlayerSafelyHidden(player)) return false;

            // How far it can see depends on how visible the player has made themselves.
            var status = player.GetComponent<PlayerStatus>();
            float lit = status != null ? status.Illumination : 0f;
            float range = Mathf.Lerp(viewDistanceDark, viewDistanceLit, Mathf.Clamp01(lit * 2f));

            if (Flashlight.Instance != null && Flashlight.Instance.IsOn) range += flashlightVisibilityBonus;
            if (player.IsCrouching) range *= 0.7f;

            if (distance > range) return false;

            var toPlayer = (playerHead - origin).normalized;
            if (Vector3.Angle(transform.forward, toPlayer) > viewAngle * 0.5f) return false;

            // Ignore triggers, or pickups and the entity's own body block the sightline.
            if (Physics.Raycast(origin, toPlayer, out var hit, distance + 1f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.transform.IsChildOf(player.transform)) return false;
            }

            return true;
        }

        bool CanHearPlayer(PlayerController player, float distance) => distance <= player.NoiseRadius;

        /// <summary>
        /// Hiding only works if it did not watch you climb in. Chase state means it
        /// tracked you to the locker, so the locker will not save you.
        /// </summary>
        bool IsPlayerSafelyHidden(PlayerController player)
        {
            if (HidingSpot.Occupied == null) return false;
            return State != EntityState.Chase;
        }

        // --- locomotion ------------------------------------------------------

        void Repath(Vector3 destination)
        {
            if (_grid == null) return;

            _path.Clear();
            _path.AddRange(_grid.FindPath(transform.position, destination));
            _pathIndex = 0;
        }

        void MoveAlongPath(float speed)
        {
            if (_pathIndex >= _path.Count) return;

            var waypoint = _path[_pathIndex];
            waypoint.y = transform.position.y;

            var delta = waypoint - transform.position;
            if (delta.sqrMagnitude < 0.16f)
            {
                _pathIndex++;
                return;
            }

            var direction = delta.normalized;
            transform.position += direction * speed * Time.deltaTime;

            var facing = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.z), Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, facing, turnSpeed * Time.deltaTime);
        }

        void UpdateGrowl(float distanceToPlayer)
        {
            if (_growl == null) return;

            float target = State == EntityState.Chase ? 0.85f : 0.35f;
            _growl.volume = Mathf.Lerp(_growl.volume, target, Time.deltaTime * 2f);
            _growl.pitch = Mathf.Lerp(_growl.pitch, State == EntityState.Chase ? 1.25f : 0.85f, Time.deltaTime * 2f);
        }

        public void ResetForNewRun(LevelData level)
        {
            _grid = level.Grid;
            _patrolPoints = new List<Vector3>(level.PatrolPoints);
            transform.position = level.EntitySpawn;
            State = EntityState.Patrol;
            _path.Clear();
            _pathIndex = 0;
            _lostSightTimer = 0f;
            PickNewPatrolTarget();
        }
    }
}
