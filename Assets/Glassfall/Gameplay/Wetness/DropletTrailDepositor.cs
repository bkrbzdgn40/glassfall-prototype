using Glassfall.Gameplay.Droplet;
using UnityEngine;

namespace Glassfall.Gameplay.Wetness
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(DropletController))]
    public sealed class DropletTrailDepositor : MonoBehaviour
    {
        [SerializeField] private WetnessField wetnessField;

        [Header("Trail")]
        [Min(0.001f)] [SerializeField] private float trailSpacing = 0.15f;
        [Min(0f)] [SerializeField] private float depositAmount = 0.35f;
        [Min(0f)] [SerializeField] private float trailWidthMultiplier = 0.7f;
        [Min(0.01f)] [SerializeField] private float teleportThreshold = 1f;

        private DropletController _controller;
        private Vector2 _depositAnchor;
        private bool _hasAnchor;

        public int DepositCount { get; private set; }
        public WetnessField Field => wetnessField;

        public void ResetDepositAnchor(Vector2 worldPosition)
        {
            _depositAnchor = worldPosition;
            _hasAnchor = true;
        }

        private void Awake()
        {
            _controller = GetComponent<DropletController>();
        }

        private void FixedUpdate()
        {
            if (wetnessField == null || _controller == null)
                return;

            Vector2 currentPosition = _controller.transform.position;
            if (!_hasAnchor)
            {
                _depositAnchor = currentPosition;
                _hasAnchor = true;
                return;
            }

            Vector2 displacement = currentPosition - _depositAnchor;
            float distance = displacement.magnitude;
            if (distance > teleportThreshold)
            {
                _depositAnchor = currentPosition;
                return;
            }

            if (distance < trailSpacing)
                return;

            Vector2 direction = displacement / distance;
            float radius = _controller.MassRadius * trailWidthMultiplier;
            while (distance >= trailSpacing)
            {
                _depositAnchor += direction * trailSpacing;
                wetnessField.DepositWorld(_depositAnchor, radius, depositAmount);
                DepositCount++;
                displacement = currentPosition - _depositAnchor;
                distance = displacement.magnitude;
            }
        }
    }
}
