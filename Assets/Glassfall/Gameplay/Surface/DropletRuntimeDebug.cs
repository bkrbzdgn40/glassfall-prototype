using Glassfall.Gameplay.Droplet;
using Glassfall.Gameplay.Wetness;
using UnityEngine;

namespace Glassfall.Gameplay.Surface
{
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(DropletController))]
    public sealed class DropletRuntimeDebug : MonoBehaviour
    {
        [Header("Runtime Mirrors (observation only)")]
        [Tooltip("Runtime mirror only. Changing this field does not affect gameplay.")]
        [SerializeField] private float currentMass;
        [SerializeField] private float currentRadius;
        [SerializeField] private float currentSpeed;
        [SerializeField] private Vector2 currentVelocity;

        [Header("Movement")]
        [SerializeField] private float baseMaxSpeed;
        [SerializeField] private float effectiveMaxSpeed;
        [SerializeField] private float finalAcceleration;
        [SerializeField] private float finalActiveDrag;
        [SerializeField] private float finalCoastDrag;

        [Header("Wetness")]
        [SerializeField] private float currentWetness;
        [SerializeField] private float wetnessSpeedMultiplier;
        [SerializeField] private float wetnessDragMultiplier;

        [Header("Surface")]
        [SerializeField] private SurfaceType currentSurface;
        [SerializeField] private float surfaceAccelerationMultiplier;
        [SerializeField] private float surfaceSpeedMultiplier;
        [SerializeField] private float surfaceDragMultiplier;
        [SerializeField] private float surfaceResponseMultiplier;

        [Header("Trail")]
        [SerializeField] private int trailDepositCount;

        private DropletController _controller;
        private DropletTrailDepositor _trailDepositor;

        private void Awake()
        {
            _controller = GetComponent<DropletController>();
            _trailDepositor = GetComponent<DropletTrailDepositor>();
        }

        private void Update()
        {
            if (_controller == null)
                return;

            currentMass = _controller.CurrentMass;
            currentRadius = _controller.CurrentRadius;
            currentSpeed = _controller.CurrentSpeed;
            currentVelocity = _controller.CurrentVelocity;
            baseMaxSpeed = _controller.BaseMaxSpeed;
            effectiveMaxSpeed = _controller.EffectiveMaxSpeed;
            finalAcceleration = _controller.FinalAcceleration;
            finalActiveDrag = _controller.FinalActiveDrag;
            finalCoastDrag = _controller.FinalCoastDrag;
            currentWetness = _controller.CurrentWetness;
            wetnessSpeedMultiplier = _controller.WetnessSpeedMultiplier;
            wetnessDragMultiplier = _controller.WetnessDragMultiplier;
            currentSurface = _controller.CurrentSurface;
            surfaceAccelerationMultiplier =
                _controller.SurfaceAccelerationMultiplier;
            surfaceSpeedMultiplier =
                _controller.SurfaceSpeedMultiplier;
            surfaceDragMultiplier =
                _controller.SurfaceDragMultiplier;
            surfaceResponseMultiplier =
                _controller.SurfaceResponseMultiplier;
            trailDepositCount = _trailDepositor != null
                ? _trailDepositor.DepositCount
                : 0;
        }
    }
}
