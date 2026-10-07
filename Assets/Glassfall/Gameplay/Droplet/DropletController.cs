using Glassfall.Input;
using Glassfall.Gameplay.Wetness;
using Glassfall.Gameplay.Surface;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Glassfall.Gameplay.Droplet
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(UnityInputAdapter))]
    public class DropletController : MonoBehaviour
    {
        public Vector2 CurrentVelocity =>
            _body != null
                ? _body.linearVelocity
                : Vector2.zero;

        public float MaxSpeed => _movement != null
            ? _movement.GetEffectiveMaxSpeed(
                SurfaceSpeedMultiplier * WetnessSpeedMultiplier *
                FormSpeedMultiplier)
            : maxSpeed * CalculateSpeedModifier(mass) *
              FormSpeedMultiplier *
              SurfaceSpeedMultiplier * WetnessSpeedMultiplier;

        public float BaseMaxSpeed => maxSpeed;
        public float EffectiveMaxSpeed => MaxSpeed;
        public float CurrentSpeed => CurrentVelocity.magnitude;
        public float MovementInputPolarity => _movementInputPolarity;
        public bool IsSplitForm => _isSplitForm;
        public float CurrentMass => mass;
        public float MassRadius => CalculateMassRadius(mass);
        public float CurrentRadius => MassRadius;
        public float MergedSpeedMultiplier => mergedSpeedMultiplier;
        public float MassScale => MassRadius / Mathf.Max(baseRadius, 0.0001f);
        public float CurrentWetness => _wetnessSensor != null
            ? _wetnessSensor.CurrentWetness
            : 0f;
        public float WetnessSpeedMultiplier => _wetnessSensor != null
            ? _wetnessSensor.SpeedMultiplier
            : 1f;
        public float WetnessDragMultiplier => _wetnessSensor != null
            ? _wetnessSensor.DragMultiplier
            : 1f;
        public SurfaceType CurrentSurface => _surfaceSensor != null
            ? _surfaceSensor.CurrentSurface
            : SurfaceType.NormalGlass;
        public float SurfaceAccelerationMultiplier => _surfaceSensor != null
            ? _surfaceSensor.CurrentModifiers.Acceleration
            : 1f;
        public float SurfaceSpeedMultiplier => _surfaceSensor != null
            ? _surfaceSensor.CurrentModifiers.Speed
            : 1f;
        public float SurfaceDragMultiplier => _surfaceSensor != null
            ? _surfaceSensor.CurrentModifiers.Drag
            : 1f;
        public float SurfaceResponseMultiplier => _surfaceSensor != null
            ? _surfaceSensor.CurrentModifiers.Response
            : 1f;
        public float FinalAcceleration => _movement != null
            ? _movement.GetEffectiveAcceleration(
                SurfaceAccelerationMultiplier)
            : acceleration * CalculateAccelerationModifier(mass);
        public float FinalActiveDrag => _movement != null
            ? _movement.GetEffectiveActiveDrag(
                SurfaceDragMultiplier * WetnessDragMultiplier)
            : activeDrag * SurfaceDragMultiplier * WetnessDragMultiplier;
        public float FinalCoastDrag => _movement != null
            ? _movement.GetEffectiveCoastDrag(
                SurfaceDragMultiplier * WetnessDragMultiplier)
            : coastDrag * SurfaceDragMultiplier * WetnessDragMultiplier;

        [Header("Movement")]
        [SerializeField] private float acceleration = 10f;
        [SerializeField] private float maxSpeed = 5f;
        [SerializeField] private float activeDrag = 0.75f;

        [Header("World Travel Tuning")]
        [Range(0.75f, 1f)]
        [SerializeField] private float mergedSpeedMultiplier = 1f;

        [FormerlySerializedAs("drag")]
        [SerializeField] private float coastDrag = 3.5f;

        [Header("Mass")]
        [Min(0.01f)]
        [SerializeField] private float mass = 10f;

        [Min(0.01f)]
        [SerializeField] private float baseRadius = 0.45f;

        [Min(0.01f)]
        [SerializeField] private float referenceMass = 10f;

        [Tooltip("Mass test values cycled by F2/F3 in the Editor or Development Build.")]
        [SerializeField] private float[] testMasses = { 5f, 10f, 20f, 40f };

        [Header("Mass Response")]
        [SerializeField] private float accelerationExponent = -0.25f;
        [SerializeField] private float speedExponent = -0.10f;
        [SerializeField] private float responseExponent = -0.20f;

        private Rigidbody2D _body;
        private CircleCollider2D _circle;
        private UnityInputAdapter _input;
        private DropletWetnessSensor _wetnessSensor;
        private DropletSurfaceSensor _surfaceSensor;
        private DropletSplitSystem _splitSystem;
        private DropletMovement _movement;
        private PhysicsMaterial2D _runtimeMaterial;
        private Vector2 _spawnPosition;
        private float _baseColliderRadius;
        private float _baseWorldColliderRadius;
        private int _testMassIndex;
        private float _movementInputPolarity = 1f;
        private bool _isSplitForm;
        private bool _acceptOneShotCommands = true;
        private bool _massHotkeysEnabled = true;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _circle = GetComponent<CircleCollider2D>();
            _input = GetComponent<UnityInputAdapter>();
            _wetnessSensor = GetComponent<DropletWetnessSensor>();
            _surfaceSensor = GetComponent<DropletSurfaceSensor>();
            _splitSystem = GetComponent<DropletSplitSystem>();

            ConfigureBody();
            ConfigureColliderMaterial();
            CacheColliderRadius();
            _testMassIndex = FindClosestTestMassIndex(mass);
            ApplyMass(mass);
            _spawnPosition = _body.position;
        }

        private void FixedUpdate()
        {
            if (!EnsureRuntimeDependencies())
                return;

            PlayerCommand command = _input.ConsumeCommand();

            if (_acceptOneShotCommands &&
                _splitSystem != null &&
                _splitSystem.ProcessCommand(command))
            {
                return;
            }

            if (_acceptOneShotCommands &&
                _splitSystem == null &&
                command.ResetPressed)
            {
                ResetToSpawn();
                return;
            }

            float dt = Time.fixedDeltaTime;

            _wetnessSensor?.Refresh(_body.position);

            // Unity'nin bir onceki fizik adiminda temaslara gore cozdugu
            // gercek hizi temel al. Boylece duvara dogru hiz birikmez ve
            // teget bilesen duvar boyunca kaymaya devam eder.
            Vector2 nextVelocity = _movement.Step(
                _body.linearVelocity,
                command.MoveDirection * _movementInputPolarity,
                dt,
                SurfaceSpeedMultiplier * WetnessSpeedMultiplier *
                FormSpeedMultiplier,
                SurfaceDragMultiplier * WetnessDragMultiplier,
                SurfaceAccelerationMultiplier,
                SurfaceResponseMultiplier);

            _body.linearVelocity = nextVelocity;
        }

        private bool EnsureRuntimeDependencies()
        {
            if (_body == null)
                _body = GetComponent<Rigidbody2D>();
            if (_circle == null)
                _circle = GetComponent<CircleCollider2D>();
            if (_input == null)
                _input = GetComponent<UnityInputAdapter>();
            if (_wetnessSensor == null)
                _wetnessSensor = GetComponent<DropletWetnessSensor>();
            if (_surfaceSensor == null)
                _surfaceSensor = GetComponent<DropletSurfaceSensor>();
            if (_splitSystem == null)
                _splitSystem = GetComponent<DropletSplitSystem>();

            if (_movement == null && _body != null && _circle != null)
            {
                if (_baseColliderRadius <= 0f)
                    CacheColliderRadius();

                _movement = new DropletMovement(
                    acceleration,
                    maxSpeed,
                    activeDrag,
                    coastDrag,
                    CalculateAccelerationModifier(mass),
                    CalculateSpeedModifier(mass),
                    CalculateResponseModifier(mass));
            }

            return _body != null &&
                   _input != null &&
                   _movement != null;
        }

        private void Update()
        {
            HandleMassHotkeys();
        }

        public void SetMass(float value)
        {
            ApplyMass(value);
        }

        public float GetRadiusForMass(float value)
        {
            return CalculateMassRadius(value);
        }

        public void ConfigureSplitRole(
            float inputPolarity,
            bool acceptsOneShotCommands,
            bool massHotkeysEnabled,
            bool isSplitForm = false)
        {
            _movementInputPolarity = inputPolarity < 0f ? -1f : 1f;
            _acceptOneShotCommands = acceptsOneShotCommands;
            _massHotkeysEnabled = massHotkeysEnabled;
            _isSplitForm = isSplitForm;
            _input?.ClearQueuedCommands();
        }

        public void ConfigureMergedMovement(float speedMultiplier)
        {
            mergedSpeedMultiplier = Mathf.Clamp(speedMultiplier, 0.75f, 1f);
        }

        public void ResetToSpawn()
        {
            ResetDroplet();
        }

        private void ApplyMass(float value)
        {
            mass = Mathf.Max(0.01f, value);

            if (_body == null || _circle == null)
                return;

            _body.mass = mass;
            _circle.radius = _baseColliderRadius *
                (CalculateMassRadius(mass) /
                 Mathf.Max(_baseWorldColliderRadius, 0.0001f));

            _movement = new DropletMovement(
                acceleration,
                maxSpeed,
                activeDrag,
                coastDrag,
                CalculateAccelerationModifier(mass),
                CalculateSpeedModifier(mass),
                CalculateResponseModifier(mass));
        }

        private void CacheColliderRadius()
        {
            _baseColliderRadius = _circle.radius;
            Vector3 scale = transform.lossyScale;
            float maxScale = Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Abs(scale.y));
            _baseWorldColliderRadius = _baseColliderRadius *
                Mathf.Max(maxScale, 0.0001f);
        }

        private float CalculateMassRadius(float value)
        {
            return Mathf.Max(baseRadius, 0.0001f) *
                Mathf.Sqrt(
                    Mathf.Max(value, 0.01f) /
                    Mathf.Max(referenceMass, 0.01f));
        }

        private float CalculateAccelerationModifier(float value)
        {
            return Mathf.Pow(
                Mathf.Max(value, 0.01f) / Mathf.Max(referenceMass, 0.01f),
                accelerationExponent);
        }

        private float CalculateSpeedModifier(float value)
        {
            return Mathf.Pow(
                Mathf.Max(value, 0.01f) / Mathf.Max(referenceMass, 0.01f),
                speedExponent);
        }

        private float CalculateResponseModifier(float value)
        {
            return Mathf.Pow(
                Mathf.Max(value, 0.01f) / Mathf.Max(referenceMass, 0.01f),
                responseExponent);
        }

        private float FormSpeedMultiplier => _isSplitForm
            ? 1f
            : Mathf.Clamp(mergedSpeedMultiplier, 0.75f, 1f);

        private void HandleMassHotkeys()
        {
            if (!_massHotkeysEnabled)
                return;

            if (_splitSystem != null && _splitSystem.IsSplit)
                return;

            if (!Application.isEditor && !Debug.isDebugBuild)
                return;

            if (testMasses == null || testMasses.Length == 0)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            int direction = 0;
            if (keyboard.f2Key.wasPressedThisFrame)
                direction = -1;
            else if (keyboard.f3Key.wasPressedThisFrame)
                direction = 1;

            if (direction == 0)
                return;

            _testMassIndex = Mathf.Clamp(
                _testMassIndex + direction,
                0,
                testMasses.Length - 1);
            ApplyMass(testMasses[_testMassIndex]);
        }

        private int FindClosestTestMassIndex(float value)
        {
            if (testMasses == null || testMasses.Length == 0)
                return 0;

            int closestIndex = 0;
            float closestDistance = Mathf.Abs(testMasses[0] - value);

            for (int i = 1; i < testMasses.Length; i++)
            {
                float distance = Mathf.Abs(testMasses[i] - value);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }

        private void ResetDroplet()
        {
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            _body.position = _spawnPosition;
            _body.rotation = 0f;
            _body.WakeUp();
        }

        private void ConfigureBody()
        {
            // Hareket kodu hizi belirler; Unity yalnizca temas/cozunurluk
            // tarafini yonetir. Bu ayarlar sahnedeki eski Kinematic kurulumu
            // calisma aninda guvenli bir Dynamic govdeye donusturur.
            _body.bodyType = RigidbodyType2D.Dynamic;
            _body.gravityScale = 0f;
            _body.linearDamping = 0f;
            _body.angularDamping = 0f;
            _body.freezeRotation = true;
            _body.collisionDetectionMode =
                CollisionDetectionMode2D.Continuous;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        private void ConfigureColliderMaterial()
        {
            // A shallow diagonal contact must keep its tangent velocity.
            // If no authored material exists, prevent default friction from
            // cancelling that component while the player holds a direction.
            if (_circle.sharedMaterial != null)
                return;

            _runtimeMaterial = new PhysicsMaterial2D(
                "Droplet Runtime Frictionless");
            _runtimeMaterial.friction = 0f;
            _runtimeMaterial.bounciness = 0f;
            _circle.sharedMaterial = _runtimeMaterial;
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
                Destroy(_runtimeMaterial);
        }
    }
}
