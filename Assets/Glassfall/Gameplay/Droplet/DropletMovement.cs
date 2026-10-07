using UnityEngine;

namespace Glassfall.Gameplay.Droplet
{
    public class DropletMovement
    {
        private readonly float _acceleration;
        private readonly float _maxSpeed;
        private readonly float _activeDrag;
        private readonly float _coastDrag;
        private readonly float _accelerationModifier;
        private readonly float _speedModifier;
        private readonly float _responseModifier;

        public float MaxSpeed => _maxSpeed * _speedModifier;

        public float GetEffectiveMaxSpeed(float speedMultiplier)
        {
            return MaxSpeed * Mathf.Max(0f, speedMultiplier);
        }

        public float GetEffectiveAcceleration(float accelerationMultiplier)
        {
            return _acceleration *
                _accelerationModifier *
                Mathf.Max(0f, accelerationMultiplier);
        }

        public float GetEffectiveResponse(float responseMultiplier)
        {
            return _responseModifier * Mathf.Max(0f, responseMultiplier);
        }

        public float GetEffectiveActiveDrag(float dragMultiplier)
        {
            return _activeDrag * Mathf.Max(0f, dragMultiplier);
        }

        public float GetEffectiveCoastDrag(float dragMultiplier)
        {
            return _coastDrag * Mathf.Max(0f, dragMultiplier);
        }

        public DropletMovement(
            float acceleration,
            float maxSpeed,
            float activeDrag,
            float coastDrag,
            float accelerationModifier = 1f,
            float speedModifier = 1f,
            float responseModifier = 1f)
        {
            _acceleration = acceleration;
            _maxSpeed = maxSpeed;
            _activeDrag = activeDrag;
            _coastDrag = coastDrag;
            _accelerationModifier = Mathf.Max(0f, accelerationModifier);
            _speedModifier = Mathf.Max(0f, speedModifier);
            _responseModifier = Mathf.Max(0f, responseModifier);
        }

        public Vector2 Step(
            Vector2 currentVelocity,
            Vector2 inputDirection,
            float deltaTime,
            float speedMultiplier = 1f,
            float dragMultiplier = 1f,
            float accelerationMultiplier = 1f,
            float responseMultiplier = 1f)
        {
            if (inputDirection.sqrMagnitude > 1f)
                inputDirection.Normalize();

            bool hasInput = inputDirection.sqrMagnitude > 0.0001f;

            if (hasInput)
            {
                currentVelocity +=
                    inputDirection *
                    GetEffectiveAcceleration(accelerationMultiplier) *
                    GetEffectiveResponse(responseMultiplier) *
                    deltaTime;
            }

            float drag = hasInput
                ? GetEffectiveActiveDrag(dragMultiplier)
                : GetEffectiveCoastDrag(dragMultiplier);

            float dragFactor =
                Mathf.Exp(-drag * deltaTime);

            currentVelocity *= dragFactor;

            float effectiveMaxSpeed =
                GetEffectiveMaxSpeed(speedMultiplier);

            if (currentVelocity.sqrMagnitude >
                effectiveMaxSpeed * effectiveMaxSpeed)
            {
                currentVelocity =
                    currentVelocity.normalized * effectiveMaxSpeed;
            }

            return currentVelocity;
        }
    }
}
