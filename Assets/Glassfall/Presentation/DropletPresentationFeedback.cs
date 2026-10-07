using Glassfall.Gameplay.Combat;
using Glassfall.Gameplay.Droplet;
using UnityEngine;

namespace Glassfall.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DropletVisual))]
    public sealed class DropletPresentationFeedback : MonoBehaviour
    {
        [Header("Collect")]
        [Min(1f)]
        [SerializeField] private float collectPulseScale = 1.10f;

        [Min(0.01f)]
        [SerializeField] private float collectPulseDuration = 0.15f;

        [Header("Split")]
        [Range(0.5f, 1f)]
        [SerializeField] private float splitCompressionScale = 0.94f;

        [Range(1f, 2f)]
        [SerializeField] private float splitExpansionScale = 1.08f;

        [Min(0.01f)]
        [SerializeField] private float splitPulseDuration = 0.22f;

        [Header("Re-Merge")]
        [Range(0.5f, 1f)]
        [SerializeField] private float remergeCompressionScale = 0.93f;

        [Range(1f, 2f)]
        [SerializeField] private float remergeOvershootScale = 1.12f;

        [Min(0.01f)]
        [SerializeField] private float remergePulseDuration = 0.25f;

        [Header("Mass Shear")]
        [Range(0.5f, 1f)]
        [SerializeField] private float shearCompressionScale = 0.82f;

        [Range(1f, 1.5f)]
        [SerializeField] private float shearReboundScale = 1.06f;

        [Min(0.01f)]
        [SerializeField] private float shearPulseDuration = 0.18f;

        private DropletVisual _visual;
        private DropletController _controller;
        private DropletSplitSystem _splitSystem;
        private DropletMergeSystem _mergeSystem;
        private MassShearSystem _massShearSystem;
        private DropletPulseKind _pulseKind;
        private float _pulseDuration;
        private float _pulseElapsed;
        private bool _pulseActive;

        private void Awake()
        {
            CacheSources();
        }

        private void OnEnable()
        {
            CacheSources();

            if (_splitSystem != null)
            {
                _splitSystem.SplitPerformed += HandleSplit;
                _splitSystem.ReMergePerformed += HandleReMerge;
            }

            if (_mergeSystem != null)
                _mergeSystem.DropletMerged += HandleCollect;

            if (_massShearSystem != null)
                _massShearSystem.MassSheared += HandleMassSheared;
        }

        private void OnDisable()
        {
            if (_splitSystem != null)
            {
                _splitSystem.SplitPerformed -= HandleSplit;
                _splitSystem.ReMergePerformed -= HandleReMerge;
            }

            if (_mergeSystem != null)
                _mergeSystem.DropletMerged -= HandleCollect;

            if (_massShearSystem != null)
                _massShearSystem.MassSheared -= HandleMassSheared;

            StopPulse();
        }

        private void Update()
        {
            if (!_pulseActive || _visual == null)
                return;

            _pulseElapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(
                _pulseElapsed / Mathf.Max(_pulseDuration, 0.01f));

            _visual.SetPresentationScaleMultiplier(
                PresentationPulseMath.Evaluate(
                    (int)_pulseKind,
                    normalizedTime,
                    collectPulseScale,
                    splitCompressionScale,
                    splitExpansionScale,
                    remergeCompressionScale,
                    remergeOvershootScale,
                    shearCompressionScale,
                    shearReboundScale));

            if (normalizedTime >= 1f)
                StopPulse();
        }

        private void HandleCollect(DropletMergeResult result)
        {
            StartPulse(
                DropletPulseKind.Collect,
                collectPulseDuration);
        }

        private void HandleSplit(SplitResult result)
        {
            StartPulse(
                DropletPulseKind.Split,
                splitPulseDuration);

            DropletController mirror = _splitSystem != null
                ? _splitSystem.MirrorController
                : null;
            DropletPresentationFeedback mirrorFeedback = mirror != null
                ? mirror.GetComponent<DropletPresentationFeedback>()
                : null;
            mirrorFeedback?.PlaySplitPulse();
        }

        private void HandleReMerge(ReMergeResult result)
        {
            StartPulse(
                DropletPulseKind.ReMerge,
                remergePulseDuration);
        }

        private void HandleMassSheared(MassShearResult result)
        {
            if (result.Target != _controller)
                return;

            StartPulse(
                DropletPulseKind.MassShear,
                shearPulseDuration);
        }

        public void ConfigureCombatReadability(
            float compressionScale = 0.82f,
            float reboundScale = 1.06f,
            float pulseDuration = 0.18f)
        {
            shearCompressionScale = Mathf.Clamp(
                compressionScale,
                0.5f,
                1f);
            shearReboundScale = Mathf.Clamp(
                reboundScale,
                1f,
                1.5f);
            shearPulseDuration = Mathf.Max(0.01f, pulseDuration);
        }

        private void PlaySplitPulse()
        {
            StartPulse(
                DropletPulseKind.Split,
                splitPulseDuration);
        }

        private void StartPulse(
            DropletPulseKind kind,
            float duration)
        {
            if (_visual == null)
                return;

            _pulseKind = kind;
            _pulseDuration = Mathf.Max(duration, 0.01f);
            _pulseElapsed = 0f;
            _pulseActive = true;
            _visual.SetPresentationScaleMultiplier(1f);
        }

        internal void ResetForClone()
        {
            StopPulse();
        }

        private void StopPulse()
        {
            _pulseActive = false;
            _pulseElapsed = 0f;
            _pulseDuration = 0f;
            _visual?.SetPresentationScaleMultiplier(1f);
        }

        private void CacheSources()
        {
            if (_visual == null)
                _visual = GetComponent<DropletVisual>();
            if (_controller == null)
                _controller = GetComponent<DropletController>();
            if (_splitSystem == null)
                _splitSystem = GetComponent<DropletSplitSystem>();
            if (_mergeSystem == null)
                _mergeSystem = GetComponent<DropletMergeSystem>();
            if (_massShearSystem == null)
                _massShearSystem = FindAnyObjectByType<MassShearSystem>();
        }

        private void OnValidate()
        {
            collectPulseScale = Mathf.Max(1f, collectPulseScale);
            collectPulseDuration = Mathf.Max(0.01f, collectPulseDuration);
            splitCompressionScale = Mathf.Clamp(
                splitCompressionScale,
                0.5f,
                1f);
            splitExpansionScale = Mathf.Max(1f, splitExpansionScale);
            splitPulseDuration = Mathf.Max(0.01f, splitPulseDuration);
            remergeCompressionScale = Mathf.Clamp(
                remergeCompressionScale,
                0.5f,
                1f);
            remergeOvershootScale = Mathf.Max(1f, remergeOvershootScale);
            remergePulseDuration = Mathf.Max(0.01f, remergePulseDuration);
            shearCompressionScale = Mathf.Clamp(
                shearCompressionScale,
                0.5f,
                1f);
            shearReboundScale = Mathf.Clamp(
                shearReboundScale,
                1f,
                1.5f);
            shearPulseDuration = Mathf.Max(0.01f, shearPulseDuration);
        }
    }

    internal enum DropletPulseKind
    {
        Collect = 0,
        Split = 1,
        ReMerge = 2,
        MassShear = 3
    }

    internal static class PresentationPulseMath
    {
        public static float Evaluate(
            int pulseKind,
            float normalizedTime,
            float collectPeak,
            float splitCompression,
            float splitExpansion,
            float remergeCompression,
            float remergeOvershoot,
            float shearCompression,
            float shearRebound)
        {
            float t = Mathf.Clamp01(normalizedTime);

            if (pulseKind == (int)DropletPulseKind.Collect)
            {
                return Mathf.Lerp(
                    1f,
                    Mathf.Max(1f, collectPeak),
                    Mathf.Sin(t * Mathf.PI));
            }

            if (pulseKind == (int)DropletPulseKind.MassShear)
            {
                if (t < 0.35f)
                {
                    return Mathf.Lerp(
                        1f,
                        Mathf.Clamp01(shearCompression),
                        SmoothStep01(t / 0.35f));
                }

                if (t < 0.70f)
                {
                    return Mathf.Lerp(
                        Mathf.Clamp01(shearCompression),
                        Mathf.Max(1f, shearRebound),
                        SmoothStep01((t - 0.35f) / 0.35f));
                }

                return Mathf.Lerp(
                    Mathf.Max(1f, shearRebound),
                    1f,
                    SmoothStep01((t - 0.70f) / 0.30f));
            }

            float compression = pulseKind == (int)DropletPulseKind.ReMerge
                ? Mathf.Clamp01(remergeCompression)
                : Mathf.Clamp01(splitCompression);
            float expansion = pulseKind == (int)DropletPulseKind.ReMerge
                ? Mathf.Max(1f, remergeOvershoot)
                : Mathf.Max(1f, splitExpansion);

            if (t < 0.30f)
            {
                return Mathf.Lerp(
                    1f,
                    compression,
                    SmoothStep01(t / 0.30f));
            }

            if (t < 0.65f)
            {
                return Mathf.Lerp(
                    compression,
                    expansion,
                    SmoothStep01((t - 0.30f) / 0.35f));
            }

            return Mathf.Lerp(
                expansion,
                1f,
                SmoothStep01((t - 0.65f) / 0.35f));
        }

        private static float SmoothStep01(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }
    }
}
