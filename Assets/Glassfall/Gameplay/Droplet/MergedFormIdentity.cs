using Glassfall.Gameplay.Combat;
using Unity.Profiling;
using UnityEngine;

namespace Glassfall.Gameplay.Droplet
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DropletController))]
    public sealed class MergedFormIdentity : MonoBehaviour
    {
        private const float MinimumPositiveValue = 0.01f;

        private static readonly ProfilerMarker AttractionQueryMarker =
            new ProfilerMarker("Glassfall.Merged.AttractionPhysicsQuery");

        [Header("References")]
        [SerializeField] private ArcHunter hunter;

        [Header("Neutral Attraction")]
        [Tooltip("How far beyond the merged droplet surface neutral droplets can be attracted.")]
        [Min(0f)]
        [SerializeField] private float attractionRadiusFromSurface = 1.25f;

        [Min(0f)]
        [SerializeField] private float maximumAttractionSpeed = 1.6f;

        [Min(0.1f)]
        [SerializeField] private float attractionFalloffExponent = 1.25f;

        [Header("Merged Body Push")]
        [Min(0f)]
        [SerializeField] private float hunterContactPadding = 0.06f;

        [Min(0f)]
        [SerializeField] private float baseBodyShoveSpeed = 3.2f;

        [Min(MinimumPositiveValue)]
        [SerializeField] private float shoveReferenceMass = 10f;

        [Min(0f)]
        [SerializeField] private float shoveMassExponent = 0.35f;

        [Min(1f)]
        [SerializeField] private float maximumShoveMultiplier = 1.75f;

        [Header("Runtime Mirrors")]
        [SerializeField] private bool debugMergedAbilitiesActive;
        [SerializeField] private int debugAttractedNeutralCount;
        [SerializeField] private bool debugBodyShoveActive;
        [SerializeField] private float debugBodyShoveSpeed;

        private DropletController _controller;
        private float _runtimeAttractionRadiusMultiplier = 1f;
        private float _runtimeBodyShoveMultiplier = 1f;

        public bool MergedAbilitiesActive =>
            _controller != null &&
            CanUseMergedAbilities(_controller.IsSplitForm);

        public float AttractionRadiusFromSurface =>
            attractionRadiusFromSurface * _runtimeAttractionRadiusMultiplier;
        public float MaximumAttractionSpeed => maximumAttractionSpeed;
        public float BaseBodyShoveSpeed =>
            baseBodyShoveSpeed * _runtimeBodyShoveMultiplier;

        private void Awake()
        {
            _controller = GetComponent<DropletController>();
            ResetRuntimeRewardTuning();
            UpdateDiagnostics(false, 0, false, 0f);
        }

        private void FixedUpdate()
        {
            if (_controller == null)
                _controller = GetComponent<DropletController>();

            if (_controller == null || !MergedAbilitiesActive)
            {
                UpdateDiagnostics(false, 0, false, 0f);
                return;
            }

            int attractedCount = AttractNeutralDroplets(Time.fixedDeltaTime);
            bool pushedHunter = TryPushHunter(
                Time.fixedDeltaTime,
                out float shoveSpeed);

            UpdateDiagnostics(
                true,
                attractedCount,
                pushedHunter,
                shoveSpeed);
        }

        public void Configure(
            ArcHunter targetHunter,
            float attractionRadius = 1.25f,
            float attractionSpeed = 1.6f,
            float bodyShoveSpeed = 3.2f,
            float bodyShoveMassExponent = 0.35f,
            float bodyShoveMaxMultiplier = 1.75f)
        {
            hunter = targetHunter;
            attractionRadiusFromSurface = Mathf.Max(0f, attractionRadius);
            maximumAttractionSpeed = Mathf.Max(0f, attractionSpeed);
            baseBodyShoveSpeed = Mathf.Max(0f, bodyShoveSpeed);
            shoveMassExponent = Mathf.Max(0f, bodyShoveMassExponent);
            maximumShoveMultiplier = Mathf.Max(1f, bodyShoveMaxMultiplier);
            ResetRuntimeRewardTuning();
        }

        public void ResetRuntimeRewardTuning()
        {
            _runtimeAttractionRadiusMultiplier = 1f;
            _runtimeBodyShoveMultiplier = 1f;
        }

        public void ApplyGravitationalDropReward(float radiusMultiplier = 1.35f)
        {
            _runtimeAttractionRadiusMultiplier =
                Mathf.Max(0f, radiusMultiplier);
        }

        public void ApplyDenseCoreReward(float shoveMultiplier = 1.25f)
        {
            _runtimeBodyShoveMultiplier =
                Mathf.Max(0f, shoveMultiplier);
        }

        public static bool CanUseMergedAbilities(bool isSplitForm)
        {
            return !isSplitForm;
        }

        public static float CalculateAttractionSpeed(
            float distanceFromSurface,
            float attractionRadius,
            float maximumSpeed,
            float falloffExponent = 1.25f)
        {
            float radius = Mathf.Max(0f, attractionRadius);
            float maxSpeed = Mathf.Max(0f, maximumSpeed);
            if (radius <= 0f || maxSpeed <= 0f)
                return 0f;

            float distance = Mathf.Max(0f, distanceFromSurface);
            if (distance >= radius)
                return 0f;

            float closeness01 = 1f - Mathf.Clamp01(distance / radius);
            float exponent = Mathf.Max(0.1f, falloffExponent);
            return maxSpeed * Mathf.Pow(closeness01, exponent);
        }

        public static float CalculateBodyShoveSpeed(
            float currentMass,
            float referenceMass,
            float baseSpeed,
            float massExponent,
            float maximumMultiplier)
        {
            float safeReferenceMass = Mathf.Max(
                MinimumPositiveValue,
                referenceMass);
            float safeMass = Mathf.Max(
                MinimumPositiveValue,
                currentMass);
            float ratio = safeMass / safeReferenceMass;
            float multiplier = Mathf.Pow(
                ratio,
                Mathf.Max(0f, massExponent));
            multiplier = Mathf.Min(
                Mathf.Max(0f, multiplier),
                Mathf.Max(1f, maximumMultiplier));

            return Mathf.Max(0f, baseSpeed) * multiplier;
        }

        private int AttractNeutralDroplets(float deltaTime)
        {
            float bodyRadius = Mathf.Max(0f, _controller.CurrentRadius);
            float runtimeAttractionRadius = AttractionRadiusFromSurface;
            float queryRadius = bodyRadius + runtimeAttractionRadius;
            if (queryRadius <= bodyRadius || maximumAttractionSpeed <= 0f)
                return 0;

            using (AttractionQueryMarker.Auto())
            {
                Vector2 center = transform.position;
                Collider2D[] overlaps = Physics2D.OverlapCircleAll(
                    center,
                    queryRadius);

                int attractedCount = 0;
                for (int i = 0; i < overlaps.Length; i++)
                {
                    NeutralDroplet neutral =
                        overlaps[i].GetComponentInParent<NeutralDroplet>();
                    if (neutral == null || neutral.IsConsumed)
                        continue;

                    Vector2 neutralPosition = neutral.transform.position;
                    float centerDistance = Vector2.Distance(
                        center,
                        neutralPosition);
                    float distanceFromSurface = Mathf.Max(
                        0f,
                        centerDistance - bodyRadius);
                    float pullSpeed = CalculateAttractionSpeed(
                        distanceFromSurface,
                        runtimeAttractionRadius,
                        maximumAttractionSpeed,
                        attractionFalloffExponent);

                    if (pullSpeed <= 0f)
                        continue;

                    Vector2 nextPosition = Vector2.MoveTowards(
                        neutralPosition,
                        center,
                        pullSpeed * Mathf.Max(0f, deltaTime));
                    neutral.transform.position = nextPosition;
                    attractedCount++;
                }

                return attractedCount;
            }
        }

        private bool TryPushHunter(
            float deltaTime,
            out float shoveSpeed)
        {
            shoveSpeed = 0f;
            if (hunter == null || hunter.IsDefeated || baseBodyShoveSpeed <= 0f)
                return false;

            Vector2 dropletPosition = transform.position;
            Vector2 hunterPosition = hunter.transform.position;
            Vector2 fromDroplet = hunterPosition - dropletPosition;
            float distance = fromDroplet.magnitude;
            float contactDistance =
                Mathf.Max(0f, _controller.CurrentRadius) +
                Mathf.Max(0f, hunter.InteractionRadius) +
                hunterContactPadding;

            if (distance > contactDistance)
                return false;

            Vector2 pushDirection;
            if (distance > 0.0001f)
            {
                pushDirection = fromDroplet / distance;
            }
            else if (_controller.CurrentVelocity.sqrMagnitude > 0.0001f)
            {
                pushDirection = _controller.CurrentVelocity.normalized;
            }
            else
            {
                pushDirection = Vector2.right;
            }

            shoveSpeed = CalculateBodyShoveSpeed(
                _controller.CurrentMass,
                shoveReferenceMass,
                BaseBodyShoveSpeed,
                shoveMassExponent,
                maximumShoveMultiplier);

            hunter.ApplyBodyDisplacement(
                pushDirection *
                shoveSpeed *
                Mathf.Max(0f, deltaTime));
            return shoveSpeed > 0f;
        }

        private void UpdateDiagnostics(
            bool active,
            int attractedCount,
            bool bodyShoveActive,
            float shoveSpeed)
        {
            debugMergedAbilitiesActive = active;
            debugAttractedNeutralCount = attractedCount;
            debugBodyShoveActive = bodyShoveActive;
            debugBodyShoveSpeed = shoveSpeed;
        }

        private void OnValidate()
        {
            attractionRadiusFromSurface = Mathf.Max(
                0f,
                attractionRadiusFromSurface);
            maximumAttractionSpeed = Mathf.Max(
                0f,
                maximumAttractionSpeed);
            attractionFalloffExponent = Mathf.Max(
                0.1f,
                attractionFalloffExponent);
            hunterContactPadding = Mathf.Max(0f, hunterContactPadding);
            baseBodyShoveSpeed = Mathf.Max(0f, baseBodyShoveSpeed);
            shoveReferenceMass = Mathf.Max(
                MinimumPositiveValue,
                shoveReferenceMass);
            shoveMassExponent = Mathf.Max(0f, shoveMassExponent);
            maximumShoveMultiplier = Mathf.Max(1f, maximumShoveMultiplier);
        }
    }
}
