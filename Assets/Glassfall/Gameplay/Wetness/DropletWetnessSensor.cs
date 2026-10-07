using UnityEngine;

namespace Glassfall.Gameplay.Wetness
{
    public sealed class DropletWetnessSensor : MonoBehaviour
    {
        [SerializeField] private WetnessField wetnessField;

        public float CurrentWetness { get; private set; }
        public float SpeedMultiplier => Mathf.Lerp(1f, 1.2f, CurrentWetness);
        public float DragMultiplier => Mathf.Lerp(1f, 0.8f, CurrentWetness);
        public WetnessField Field => wetnessField;

        public void Refresh(Vector2 worldPosition)
        {
            CurrentWetness = wetnessField != null ? wetnessField.SampleWorld(worldPosition) : 0f;
        }
    }
}
