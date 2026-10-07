using Glassfall.Gameplay.Droplet;
using UnityEngine;

namespace Glassfall.Presentation
{
    public class DropletVisual : MonoBehaviour
    {
        [SerializeField]
        private DropletController controller;

        [SerializeField]
        private Transform visualRoot;

        [Header("Deformation")]
        [SerializeField]
        private float maxStretch = 0.25f;

        [SerializeField]
        private float deformationResponse = 10f;

        [SerializeField]
        private float rotationResponse = 12f;

        private float _lastAngle;
        private Vector3 _baseVisualScale;
        private Quaternion _baseVisualRotation;
        private DropletPresentationFeedback _presentationFeedback;
        private float _presentationScaleMultiplier = 1f;
        private bool _hasCanonicalState;

        private void Awake()
        {
            CacheCanonicalState();
            _presentationFeedback =
                GetComponent<DropletPresentationFeedback>();

            if (_presentationFeedback == null)
            {
                _presentationFeedback =
                    gameObject.AddComponent<DropletPresentationFeedback>();
            }
        }

        /// <summary>
        /// Restores the authored VisualRoot transform before a runtime clone is made.
        /// This prevents a clone from treating the parent's current deformation as its
        /// own canonical visual state.
        /// </summary>
        public void ResetToCanonicalState()
        {
            if (!CacheCanonicalState())
                return;

            _presentationFeedback?.ResetForClone();
            _presentationScaleMultiplier = 1f;
            _lastAngle = 0f;
            visualRoot.localScale = _baseVisualScale;
            visualRoot.localRotation = _baseVisualRotation;
        }

        /// <summary>
        /// Snaps presentation to the current authoritative mass and velocity after a
        /// runtime state change such as split, re-merge, or reset.
        /// </summary>
        public void ReinitializeFromCurrentState()
        {
            if (controller == null || !CacheCanonicalState())
                return;

            Vector2 velocity = controller.CurrentVelocity;
            float maxSpeed = Mathf.Max(controller.MaxSpeed, 0.001f);
            float normalizedSpeed = Mathf.Clamp01(
                velocity.magnitude / maxSpeed);

            _lastAngle = velocity.sqrMagnitude > 0.001f
                ? Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg
                : 0f;

            visualRoot.localScale = CalculateTargetScale(normalizedSpeed);
            visualRoot.localRotation = CalculateTargetRotation();
        }

        /// <summary>
        /// Applies a presentation-only scale multiplier on top of the canonical
        /// mass and movement deformation calculated by this component.
        /// </summary>
        public void SetPresentationScaleMultiplier(float multiplier)
        {
            _presentationScaleMultiplier = Mathf.Clamp(
                multiplier,
                0.01f,
                3f);
        }

        private void Update()
        {
            if (controller == null || visualRoot == null)
                return;

            Vector2 velocity = controller.CurrentVelocity;

            float maxSpeed =
                Mathf.Max(controller.MaxSpeed, 0.001f);

            float normalizedSpeed =
                Mathf.Clamp01(
                    velocity.magnitude / maxSpeed);

            UpdateScale(normalizedSpeed);
            UpdateRotation(velocity);
        }

        private void UpdateScale(float normalizedSpeed)
        {
            Vector3 targetScale = CalculateTargetScale(normalizedSpeed);

            float t =
                1f -
                Mathf.Exp(
                    -deformationResponse *
                    Time.deltaTime);

            visualRoot.localScale =
                Vector3.Lerp(
                    visualRoot.localScale,
                    targetScale,
                    t);
        }

        private void UpdateRotation(Vector2 velocity)
        {
            if (velocity.sqrMagnitude > 0.001f)
            {
                _lastAngle =
                    Mathf.Atan2(
                        velocity.y,
                        velocity.x)
                    * Mathf.Rad2Deg;
            }

            Quaternion targetRotation = CalculateTargetRotation();

            float t =
                1f -
                Mathf.Exp(
                    -rotationResponse *
                    Time.deltaTime);

            visualRoot.localRotation =
                Quaternion.Lerp(
                    visualRoot.localRotation,
                    targetRotation,
                    t);
        }

        private Vector3 CalculateTargetScale(float normalizedSpeed)
        {
            float stretch = 1f + normalizedSpeed * maxStretch;
            float squash = 1f / stretch;

            return Vector3.Scale(
                _baseVisualScale,
                new Vector3(
                    controller.MassScale * stretch *
                    _presentationScaleMultiplier,
                    controller.MassScale * squash *
                    _presentationScaleMultiplier,
                    1f));
        }

        private Quaternion CalculateTargetRotation()
        {
            return _baseVisualRotation * Quaternion.Euler(
                0f,
                0f,
                _lastAngle);
        }

        private bool CacheCanonicalState()
        {
            if (_hasCanonicalState)
                return true;

            if (visualRoot == null)
                return false;

            _baseVisualScale = visualRoot.localScale;
            _baseVisualRotation = visualRoot.localRotation;
            _hasCanonicalState = true;
            return true;
        }
    }
}
