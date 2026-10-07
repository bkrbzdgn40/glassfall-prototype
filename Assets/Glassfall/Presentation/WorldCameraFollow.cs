using Glassfall.Gameplay.Droplet;
using UnityEngine;

namespace Glassfall.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class WorldCameraFollow : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private DropletSplitSystem splitSystem;
        [SerializeField] private Collider2D worldBounds;
        [SerializeField] private Vector2 focusOffset;

        [Min(0.01f)]
        [SerializeField] private float followSharpness = 7f;

        [Min(0f)]
        [SerializeField] private float splitMidpointBias = 1f;

        public void Configure(
            DropletSplitSystem playerSplitSystem,
            Collider2D bounds,
            float sharpness = 7f,
            Vector2? offset = null,
            float midpointBias = 1f)
        {
            splitSystem = playerSplitSystem;
            worldBounds = bounds;
            followSharpness = Mathf.Max(0.01f, sharpness);
            focusOffset = offset ?? Vector2.zero;
            splitMidpointBias = Mathf.Max(0f, midpointBias);

            CacheDependencies();
            SnapToTarget();
        }

        private void Awake()
        {
            CacheDependencies();
            SnapToTarget();
        }

        private void OnEnable()
        {
            CacheDependencies();
            SnapToTarget();
        }

        private void LateUpdate()
        {
            CacheDependencies();
            if (targetCamera == null)
                return;

            Vector3 desired = ResolveDesiredPosition();
            float blend = 1f - Mathf.Exp(-followSharpness * Mathf.Max(0f, Time.unscaledDeltaTime));
            Vector3 current = transform.position;
            transform.position = Vector3.Lerp(current, desired, blend);
        }

        private void CacheDependencies()
        {
            if (targetCamera == null)
                targetCamera = GetComponent<Camera>();

            if (splitSystem == null)
                splitSystem = FindAnyObjectByType<DropletSplitSystem>();

            if (worldBounds == null)
            {
                GameObject area = GameObject.Find("NeurtalSpawnArea");
                if (area == null)
                    area = GameObject.Find("NeutralSpawnArea");
                if (area != null)
                    worldBounds = area.GetComponent<Collider2D>();
            }
        }

        private void SnapToTarget()
        {
            if (targetCamera == null)
                return;

            transform.position = ResolveDesiredPosition();
        }

        private Vector3 ResolveDesiredPosition()
        {
            Vector3 current = transform.position;
            Vector2 focus = ResolveFocusPoint();
            Vector3 desired = new Vector3(
                focus.x + focusOffset.x,
                focus.y + focusOffset.y,
                current.z);
            return ClampToWorldBounds(desired);
        }

        private Vector2 ResolveFocusPoint()
        {
            if (splitSystem == null || splitSystem.PrimaryController == null)
                return transform.position;

            Vector2 primary = splitSystem.PrimaryController.transform.position;
            if (!splitSystem.IsSplit || splitSystem.MirrorController == null)
                return primary;

            Vector2 mirror = splitSystem.MirrorController.transform.position;
            Vector2 midpoint = (primary + mirror) * 0.5f;
            float bias = Mathf.Clamp01(splitMidpointBias);
            return Vector2.Lerp(primary, midpoint, bias);
        }

        private Vector3 ClampToWorldBounds(Vector3 desired)
        {
            if (worldBounds == null || targetCamera == null || !targetCamera.orthographic)
                return desired;

            Bounds bounds = worldBounds.bounds;
            float halfHeight = targetCamera.orthographicSize;
            float halfWidth = halfHeight * targetCamera.aspect;

            float minX = bounds.min.x + halfWidth;
            float maxX = bounds.max.x - halfWidth;
            float minY = bounds.min.y + halfHeight;
            float maxY = bounds.max.y - halfHeight;

            if (minX > maxX)
                desired.x = bounds.center.x;
            else
                desired.x = Mathf.Clamp(desired.x, minX, maxX);

            if (minY > maxY)
                desired.y = bounds.center.y;
            else
                desired.y = Mathf.Clamp(desired.y, minY, maxY);

            return desired;
        }

        private void OnValidate()
        {
            followSharpness = Mathf.Max(0.01f, followSharpness);
            splitMidpointBias = Mathf.Max(0f, splitMidpointBias);
        }
    }
}
