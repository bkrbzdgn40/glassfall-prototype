using System;
using UnityEngine;

namespace Glassfall.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFeedbackTarget : MonoBehaviour
    {
        [Header("Collect")]
        [Min(0f)]
        [SerializeField] private float collectAmplitude = 0.025f;

        [Min(0.01f)]
        [SerializeField] private float collectDuration = 0.08f;

        [Header("Split")]
        [Min(0f)]
        [SerializeField] private float splitAmplitude = 0.045f;

        [Min(0.01f)]
        [SerializeField] private float splitDuration = 0.13f;

        [Header("Re-Merge")]
        [Min(0f)]
        [SerializeField] private float remergeAmplitude = 0.060f;

        [Min(0.01f)]
        [SerializeField] private float remergeDuration = 0.16f;

        [Header("Safety")]
        [Min(0f)]
        [SerializeField] private float maxCombinedAmplitude = 0.075f;

        private Camera _camera;
        private System.Random _random;
        private Vector3 _basePosition;
        private Vector3 _appliedOffset;
        private Vector2 _direction = Vector2.right;
        private float _activeAmplitude;
        private float _activeDuration;
        private float _elapsed;

        private void Awake()
        {
            CacheCamera();
            _random = new System.Random(GetHashCode());

            if (_camera != null)
                _basePosition = _camera.transform.position;
        }

        private void LateUpdate()
        {
            if (_camera == null)
                return;

            // Remove only the offset that this component applied last frame.
            // Any camera motion authored by another system before LateUpdate
            // therefore becomes the new canonical position instead of drifting.
            Vector3 currentPosition = _camera.transform.position;
            _basePosition = currentPosition - _appliedOffset;

            Vector2 offset = EvaluateOffset(Time.deltaTime);
            _appliedOffset = new Vector3(offset.x, offset.y, 0f);
            _camera.transform.position = _basePosition + _appliedOffset;
        }

        public void PlayCollect(float consumedMass)
        {
            float amplitude = CameraFeedbackMath.ComputeCollectAmplitude(
                collectAmplitude,
                consumedMass,
                maxCombinedAmplitude);
            StartImpulse(amplitude, collectDuration);
        }

        public void PlaySplit()
        {
            StartImpulse(splitAmplitude, splitDuration);
        }

        public void PlayReMerge()
        {
            StartImpulse(remergeAmplitude, remergeDuration);
        }

        private void StartImpulse(float amplitude, float duration)
        {
            if (_camera == null)
                return;

            float clampedAmplitude = CameraFeedbackMath.ClampAmplitude(
                amplitude,
                maxCombinedAmplitude);
            if (clampedAmplitude <= 0f)
                return;

            // One bounded presentation state owns the camera offset. A new
            // event restarts that state instead of spawning stacked routines
            // or accumulating unbounded shake strength.
            _activeAmplitude = clampedAmplitude;
            _activeDuration = Mathf.Max(duration, 0.01f);
            _elapsed = 0f;
            _direction = NextDirection();
        }

        private Vector2 EvaluateOffset(float deltaTime)
        {
            if (_activeAmplitude <= 0f || _activeDuration <= 0f)
            {
                _appliedOffset = Vector3.zero;
                return Vector2.zero;
            }

            _elapsed += Mathf.Max(deltaTime, 0f);
            float normalizedTime = Mathf.Clamp01(
                _elapsed / Mathf.Max(_activeDuration, 0.01f));
            float envelope = CameraFeedbackMath.EvaluateEnvelope(
                normalizedTime);
            Vector2 offset = _direction * (_activeAmplitude * envelope);

            if (normalizedTime >= 1f)
            {
                _activeAmplitude = 0f;
                _activeDuration = 0f;
                _elapsed = 0f;
                return Vector2.zero;
            }

            return offset;
        }

        private Vector2 NextDirection()
        {
            if (_random == null)
                return Vector2.right;

            double angle = _random.NextDouble() * Math.PI * 2.0;
            return new Vector2(
                (float)Math.Cos(angle),
                (float)Math.Sin(angle));
        }

        private void CacheCamera()
        {
            if (_camera == null)
                _camera = GetComponent<Camera>();
        }

        private void OnDisable()
        {
            RestoreCanonicalPosition();
        }

        private void OnDestroy()
        {
            RestoreCanonicalPosition();
        }

        private void RestoreCanonicalPosition()
        {
            if (_camera == null)
                return;

            _camera.transform.position -= _appliedOffset;
            _appliedOffset = Vector3.zero;
            _basePosition = _camera.transform.position;
            _activeAmplitude = 0f;
            _activeDuration = 0f;
            _elapsed = 0f;
        }

        private void OnValidate()
        {
            collectAmplitude = Mathf.Max(0f, collectAmplitude);
            collectDuration = Mathf.Max(0.01f, collectDuration);
            splitAmplitude = Mathf.Max(0f, splitAmplitude);
            splitDuration = Mathf.Max(0.01f, splitDuration);
            remergeAmplitude = Mathf.Max(0f, remergeAmplitude);
            remergeDuration = Mathf.Max(0.01f, remergeDuration);
            maxCombinedAmplitude = Mathf.Max(0f, maxCombinedAmplitude);
        }
    }

    internal static class CameraFeedbackMath
    {
        private const float CollectMassExponent = 0.12f;
        private const float CollectMassScaleMin = 0.80f;
        private const float CollectMassScaleMax = 1.35f;
        private const float AttackFraction = 0.15f;
        private const float OscillationHalfTurns = 2.5f;

        public static float ComputeCollectAmplitude(
            float baseAmplitude,
            float consumedMass,
            float maxAmplitude)
        {
            float massScale = Mathf.Clamp(
                Mathf.Pow(Mathf.Max(consumedMass, 0.01f), CollectMassExponent),
                CollectMassScaleMin,
                CollectMassScaleMax);
            return ClampAmplitude(
                Mathf.Max(baseAmplitude, 0f) * massScale,
                maxAmplitude);
        }

        public static float ClampAmplitude(
            float amplitude,
            float maxAmplitude)
        {
            return Mathf.Clamp(
                Mathf.Max(amplitude, 0f),
                0f,
                Mathf.Max(maxAmplitude, 0f));
        }

        public static float EvaluateEnvelope(float normalizedTime)
        {
            float t = Mathf.Clamp01(normalizedTime);
            float attack = SmoothStep01(
                Mathf.Clamp01(t / AttackFraction));
            float decay = 1f - SmoothStep01(t);
            float oscillation = Mathf.Sin(
                t * Mathf.PI * OscillationHalfTurns);
            return attack * decay * oscillation;
        }

        private static float SmoothStep01(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }
    }
}
