using Glassfall.Gameplay.Droplet;
using Glassfall.Gameplay.Run;
using UnityEngine;

namespace Glassfall.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DropletController))]
    public sealed class DropletPixelArtPresentation : MonoBehaviour
    {
        [SerializeField] private DropletController controller;
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Sprite mergedSprite;
        [SerializeField] private Sprite splitPrimarySprite;
        [SerializeField] private Sprite splitMirrorSprite;
        [SerializeField] private ContaminationSystem contaminationSystem;

        [Header("Contamination Readability")]
        [SerializeField] private Color contaminationTint =
            new Color(1f, 0.42f, 0.18f, 1f);

        [Range(0f, 1f)]
        [SerializeField] private float taintedTintStrength = 0.22f;

        [Range(0f, 1f)]
        [SerializeField] private float corruptedTintStrength = 0.48f;

        [Range(0f, 1f)]
        [SerializeField] private float criticalTintStrength = 0.72f;

        private Sprite _lastAppliedSprite;

        private void Awake()
        {
            CacheDependencies();
            RefreshNow();
        }

        private void LateUpdate()
        {
            RefreshNow();
        }

        public void Configure(
            SpriteRenderer renderer,
            Sprite merged,
            Sprite splitPrimary,
            Sprite splitMirror)
        {
            controller = GetComponent<DropletController>();
            targetRenderer = renderer;
            mergedSprite = merged;
            splitPrimarySprite = splitPrimary;
            splitMirrorSprite = splitMirror;
            _lastAppliedSprite = null;
            RefreshNow();
        }

        public void ConfigureContamination(
            ContaminationSystem source,
            float taintedStrength = 0.22f,
            float corruptedStrength = 0.48f,
            float criticalStrength = 0.72f)
        {
            contaminationSystem = source;
            taintedTintStrength = Mathf.Clamp01(taintedStrength);
            corruptedTintStrength = Mathf.Clamp01(corruptedStrength);
            criticalTintStrength = Mathf.Clamp01(criticalStrength);
            ApplyContaminationVisual();
        }

        public void RefreshNow()
        {
            CacheDependencies();
            if (controller == null || targetRenderer == null)
                return;

            Sprite desired = ResolveSprite();
            if (desired != null && desired != _lastAppliedSprite)
            {
                targetRenderer.sprite = desired;
                _lastAppliedSprite = desired;
            }

            ApplyContaminationVisual();
        }

        private Sprite ResolveSprite()
        {
            if (!controller.IsSplitForm)
                return mergedSprite;

            return controller.MovementInputPolarity < 0f
                ? splitMirrorSprite
                : splitPrimarySprite;
        }

        private void CacheDependencies()
        {
            if (controller == null)
                controller = GetComponent<DropletController>();

            if (targetRenderer != null)
                return;

            Transform visualRoot = transform.Find("VisualRoot");
            if (visualRoot != null)
                targetRenderer = visualRoot.GetComponent<SpriteRenderer>();
        }

        private void ApplyContaminationVisual()
        {
            if (targetRenderer == null)
                return;

            if (contaminationSystem == null)
                contaminationSystem = FindAnyObjectByType<ContaminationSystem>();

            if (contaminationSystem == null)
            {
                targetRenderer.color = Color.white;
                return;
            }

            float strength = contaminationSystem.State switch
            {
                ContaminationState.Tainted => taintedTintStrength,
                ContaminationState.Corrupted => corruptedTintStrength,
                ContaminationState.Critical => criticalTintStrength,
                ContaminationState.Contaminated =>
                    Mathf.Min(0.88f, criticalTintStrength + 0.12f),
                _ => 0f
            };

            if (contaminationSystem.State == ContaminationState.Corrupted)
            {
                strength += Mathf.Sin(Time.unscaledTime * 10f) * 0.025f;
            }
            else if (contaminationSystem.State == ContaminationState.Critical)
            {
                strength += Mathf.Sin(Time.unscaledTime * 18f) * 0.065f;
            }

            Color stateColor = Color.Lerp(
                Color.white,
                contaminationTint,
                Mathf.Clamp01(strength));
            float hitFlash = contaminationSystem.HitFlash01;
            if (hitFlash > 0f)
            {
                stateColor = Color.Lerp(
                    stateColor,
                    new Color(1f, 0.52f, 0.20f, 1f),
                    0.58f * hitFlash);
            }

            float cleansePulse = contaminationSystem.CleansePulse01;
            if (cleansePulse > 0f)
            {
                float pulseStrength = Mathf.SmoothStep(
                    0f,
                    1f,
                    cleansePulse);
                stateColor = Color.Lerp(
                    stateColor,
                    new Color(0.68f, 1f, 1f, 1f),
                    0.72f * pulseStrength);
            }

            targetRenderer.color = stateColor;
        }

        private void OnValidate()
        {
            taintedTintStrength = Mathf.Clamp01(taintedTintStrength);
            corruptedTintStrength = Mathf.Clamp01(corruptedTintStrength);
            criticalTintStrength = Mathf.Clamp01(criticalTintStrength);
        }
    }
}
