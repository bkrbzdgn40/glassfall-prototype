using Glassfall.Gameplay.Droplet;
using UnityEngine;

namespace Glassfall.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NeutralDroplet))]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PickupPixelArtPresentation : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Sprite neutralSprite;
        [SerializeField] private Sprite recoveryFragmentSprite;

        [Header("Recovery Fragment Readability")]
        [SerializeField] private Color recoverySpawnFlashColor =
            new Color(0.62f, 0.95f, 1f, 1f);

        [Min(0.01f)]
        [SerializeField] private float recoverySpawnFlashDuration = 0.22f;

        private Sprite _lastAppliedSprite;
        private bool _wasRecoveryFragment;
        private float _recoveryFlashRemaining;

        private void Awake()
        {
            CacheRenderer();
            RefreshNow();
        }

        private void LateUpdate()
        {
            RefreshNow();
            UpdateRecoverySpawnFeedback();
        }

        public void Configure(
            SpriteRenderer renderer,
            Sprite neutral,
            Sprite recoveryFragment)
        {
            targetRenderer = renderer;
            neutralSprite = neutral;
            recoveryFragmentSprite = recoveryFragment;
            _lastAppliedSprite = null;
            RefreshNow();
        }

        public void ConfigureRecoveryFeedback(
            Color flashColor,
            float flashDuration = 0.22f)
        {
            recoverySpawnFlashColor = flashColor;
            recoverySpawnFlashDuration = Mathf.Max(0.01f, flashDuration);
        }

        public void RefreshNow()
        {
            CacheRenderer();
            if (targetRenderer == null)
                return;

            bool isRecoveryFragment =
                GetComponent<RecoverableMassFragment>() != null;
            if (isRecoveryFragment && !_wasRecoveryFragment)
                _recoveryFlashRemaining = recoverySpawnFlashDuration;
            _wasRecoveryFragment = isRecoveryFragment;

            Sprite desired = isRecoveryFragment
                ? recoveryFragmentSprite
                : neutralSprite;

            if (desired == null || desired == _lastAppliedSprite)
                return;

            targetRenderer.sprite = desired;
            targetRenderer.color = Color.white;
            _lastAppliedSprite = desired;
        }

        private void UpdateRecoverySpawnFeedback()
        {
            if (targetRenderer == null || _recoveryFlashRemaining <= 0f)
                return;

            _recoveryFlashRemaining = Mathf.Max(
                0f,
                _recoveryFlashRemaining - Time.deltaTime);
            float normalized = 1f -
                _recoveryFlashRemaining /
                Mathf.Max(recoverySpawnFlashDuration, 0.01f);
            float envelope = Mathf.Sin(Mathf.Clamp01(normalized) * Mathf.PI);
            targetRenderer.color = Color.Lerp(
                Color.white,
                recoverySpawnFlashColor,
                envelope);

            if (_recoveryFlashRemaining <= 0f)
                targetRenderer.color = Color.white;
        }

        private void OnDisable()
        {
            if (targetRenderer != null)
                targetRenderer.color = Color.white;
        }

        private void OnValidate()
        {
            recoverySpawnFlashDuration = Mathf.Max(
                0.01f,
                recoverySpawnFlashDuration);
        }

        private void CacheRenderer()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponent<SpriteRenderer>();
        }
    }
}
