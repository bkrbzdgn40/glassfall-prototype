using Unity.Profiling;
using UnityEngine;

namespace Glassfall.Gameplay.Wetness
{
    public sealed class WetnessGrid
    {
        private static readonly ProfilerMarker DecayMarker =
            new ProfilerMarker("Glassfall.WetnessGrid.Decay");

        private readonly int _width;
        private readonly int _height;
        private readonly Vector2 _worldSize;
        private readonly Vector2 _worldCenter;
        private readonly float[] _values;

        public WetnessGrid(int width, int height, Vector2 worldSize, Vector2 worldCenter)
        {
            _width = Mathf.Max(1, width);
            _height = Mathf.Max(1, height);
            _worldSize = new Vector2(Mathf.Max(worldSize.x, 0.0001f), Mathf.Max(worldSize.y, 0.0001f));
            _worldCenter = worldCenter;
            _values = new float[_width * _height];
        }

        public int Width => _width;
        public int Height => _height;
        public Vector2 WorldSize => _worldSize;
        public Vector2 WorldCenter => _worldCenter;

        public float Sample(Vector2 worldPosition)
        {
            if (!TryGetGridPosition(worldPosition, out Vector2 gridPosition))
                return 0f;

            int x0 = Mathf.FloorToInt(gridPosition.x);
            int y0 = Mathf.FloorToInt(gridPosition.y);
            int x1 = Mathf.Min(x0 + 1, _width - 1);
            int y1 = Mathf.Min(y0 + 1, _height - 1);
            float tx = gridPosition.x - x0;
            float ty = gridPosition.y - y0;
            float lower = Mathf.Lerp(GetValue(x0, y0), GetValue(x1, y0), tx);
            float upper = Mathf.Lerp(GetValue(x0, y1), GetValue(x1, y1), tx);
            return Mathf.Clamp01(Mathf.Lerp(lower, upper, ty));
        }

        public void Deposit(Vector2 worldPosition, float radius, float amount)
        {
            if (amount <= 0f || radius <= 0f)
                return;

            Vector2 minimum = _worldCenter - _worldSize * 0.5f;
            float cellWidth = _worldSize.x / _width;
            float cellHeight = _worldSize.y / _height;
            int minX = Mathf.Max(0, Mathf.FloorToInt((worldPosition.x - radius - minimum.x) / cellWidth));
            int maxX = Mathf.Min(_width - 1, Mathf.CeilToInt((worldPosition.x + radius - minimum.x) / cellWidth));
            int minY = Mathf.Max(0, Mathf.FloorToInt((worldPosition.y - radius - minimum.y) / cellHeight));
            int maxY = Mathf.Min(_height - 1, Mathf.CeilToInt((worldPosition.y + radius - minimum.y) / cellHeight));
            float radiusSquared = radius * radius;

            for (int y = minY; y <= maxY; y++)
            {
                float cellCenterY = minimum.y + (y + 0.5f) * cellHeight;
                for (int x = minX; x <= maxX; x++)
                {
                    float cellCenterX = minimum.x + (x + 0.5f) * cellWidth;
                    float dx = cellCenterX - worldPosition.x;
                    float dy = cellCenterY - worldPosition.y;
                    if (dx * dx + dy * dy > radiusSquared)
                        continue;

                    int index = y * _width + x;
                    _values[index] = Mathf.Clamp01(_values[index] + amount);
                }
            }
        }

        public void Decay(float decayRate, float deltaTime)
        {
            if (decayRate <= 0f || deltaTime <= 0f)
                return;

            using (DecayMarker.Auto())
            {
                for (int i = 0; i < _values.Length; i++)
                {
                    _values[i] = WetTrailLifecycleRules.DecayValue(
                        _values[i],
                        decayRate,
                        deltaTime);
                }
            }
        }

        public void Clear()
        {
            System.Array.Clear(_values, 0, _values.Length);
        }

        public float GetCell(int x, int y)
        {
            if (x < 0 || x >= _width || y < 0 || y >= _height)
                return 0f;
            return _values[y * _width + x];
        }

        public void CopyTo(Color32[] colors, Color32 wetColor)
        {
            if (colors == null || colors.Length < _values.Length)
                return;

            for (int i = 0; i < _values.Length; i++)
            {
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(_values[i]) * 255f);
                colors[i] = new Color32(wetColor.r, wetColor.g, wetColor.b, alpha);
            }
        }

        private bool TryGetGridPosition(Vector2 worldPosition, out Vector2 gridPosition)
        {
            Vector2 minimum = _worldCenter - _worldSize * 0.5f;
            Vector2 maximum = _worldCenter + _worldSize * 0.5f;
            if (worldPosition.x < minimum.x || worldPosition.x > maximum.x ||
                worldPosition.y < minimum.y || worldPosition.y > maximum.y)
            {
                gridPosition = Vector2.zero;
                return false;
            }

            float normalizedX = Mathf.InverseLerp(minimum.x, maximum.x, worldPosition.x);
            float normalizedY = Mathf.InverseLerp(minimum.y, maximum.y, worldPosition.y);
            gridPosition = new Vector2(normalizedX * (_width - 1), normalizedY * (_height - 1));
            return true;
        }

        private float GetValue(int x, int y)
        {
            return _values[y * _width + x];
        }
    }
}
