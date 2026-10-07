using UnityEngine;

namespace Glassfall.Gameplay.Wetness
{
    public sealed class WetnessField : MonoBehaviour
    {
        [Header("Grid")]
        [Min(1)] [SerializeField] private int gridWidth = 128;
        [Min(1)] [SerializeField] private int gridHeight = 128;
        [SerializeField] private Vector2 worldSize = new Vector2(24f, 14f);
        [SerializeField] private Vector2 worldCenter = Vector2.zero;

        [Header("Gameplay")]
        [Min(0f)] [SerializeField] private float decayRate = 0.08f;

        private WetnessGrid _grid;
        private float _runtimeDecayMultiplier = 1f;

        public WetnessGrid Grid => _grid;
        public int GridWidth => _grid != null ? _grid.Width : gridWidth;
        public int GridHeight => _grid != null ? _grid.Height : gridHeight;
        public int GridResolution => GridWidth * GridHeight;
        public Vector2 WorldSize => _grid != null ? _grid.WorldSize : worldSize;
        public Vector2 WorldCenter => _grid != null ? _grid.WorldCenter : worldCenter;
        public float BaseDecayRate => decayRate;
        public float DecayRate => decayRate * _runtimeDecayMultiplier;

        private void Awake()
        {
            ResetRuntimeRewardTuning();
            Initialize();
        }

        private void FixedUpdate()
        {
            _grid?.Decay(DecayRate, Time.fixedDeltaTime);
        }

        public float SampleWorld(Vector2 worldPosition)
        {
            return _grid != null ? _grid.Sample(worldPosition) : 0f;
        }

        public void DepositWorld(Vector2 worldPosition, float radius, float amount)
        {
            _grid?.Deposit(worldPosition, radius, amount);
        }

        public void Clear()
        {
            _grid?.Clear();
        }

        public void ConfigureLifecycle(float newDecayRate)
        {
            decayRate = Mathf.Max(0f, newDecayRate);
            ResetRuntimeRewardTuning();
        }

        public void ConfigureWorldBounds(Collider2D bounds)
        {
            if (bounds == null)
                return;

            Bounds worldBounds = bounds.bounds;
            worldSize = new Vector2(
                Mathf.Max(0.01f, worldBounds.size.x),
                Mathf.Max(0.01f, worldBounds.size.y));
            worldCenter = worldBounds.center;

            if (Application.isPlaying)
                Initialize();
        }

        public void ResetRuntimeRewardTuning()
        {
            _runtimeDecayMultiplier = 1f;
        }

        public void ApplySlowEvaporationReward(float decayMultiplier = 0.75f)
        {
            _runtimeDecayMultiplier = Mathf.Clamp01(decayMultiplier);
        }

        public float GetCell(int x, int y)
        {
            return _grid != null ? _grid.GetCell(x, y) : 0f;
        }

        public void CopyTo(Color32[] colors, Color32 wetColor)
        {
            _grid?.CopyTo(colors, wetColor);
        }

        private void Initialize()
        {
            _grid = new WetnessGrid(gridWidth, gridHeight, worldSize, worldCenter);
        }
    }
}
