using Glassfall.Gameplay.Builds;
using Glassfall.Gameplay.Combat;
using Glassfall.Gameplay.Link;
using UnityEngine;

namespace Glassfall.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class SplitLinkVisual : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SplitLinkSystem linkSystem;
        [SerializeField] private MicroBuildChoiceController buildController;
        [SerializeField] private SplitLinkCombat combat;
        [SerializeField] private LineRenderer broadArcHalo;

        [Header("Base Link")]
        [Min(0.01f)]
        [SerializeField] private float width = 0.08f;

        [SerializeField] private Color linkColor =
            new Color(0.32f, 0.88f, 1f, 0.85f);

        [Header("Build Readability")]
        [SerializeField] private Color conductiveFocusColor =
            new Color(0.18f, 1f, 0.86f, 0.95f);

        [SerializeField] private Color conductiveActiveColor =
            new Color(0.86f, 1f, 1f, 1f);

        [SerializeField] private Color broadArcColor =
            new Color(0.35f, 0.78f, 1f, 0.95f);

        [SerializeField] private Color broadArcHaloColor =
            new Color(0.20f, 0.72f, 1f, 0.20f);

        [SerializeField] private Color capacitorChargedColor =
            new Color(1f, 0.82f, 0.28f, 1f);

        [Min(0.01f)]
        [SerializeField] private float broadArcMinimumHaloWidth = 0.22f;

        [Header("Surface Tension")]
        [SerializeField] private Color overstretchedColor =
            new Color(1f, 0.46f, 0.30f, 0.98f);

        [Range(0.1f, 1f)]
        [SerializeField] private float overstretchedMinimumWidthMultiplier = 0.45f;

        [Min(0f)]
        [SerializeField] private float overstretchedWobbleAmplitude = 0.08f;

        [Min(0f)]
        [SerializeField] private float overstretchedWobbleSpeed = 18f;

        private LineRenderer _line;
        private Material _runtimeMaterial;
        private Material _runtimeHaloMaterial;

        public void Configure(SplitLinkSystem source)
        {
            linkSystem = source;
        }

        public void ConfigureReadability(
            MicroBuildChoiceController controller,
            SplitLinkCombat combatSource,
            LineRenderer halo)
        {
            buildController = controller;
            combat = combatSource;
            broadArcHalo = halo;

            if (broadArcHalo != null)
                ConfigureHaloRenderer();
        }

        public void ConfigureSurfaceTension(
            float minimumWidthMultiplier = 0.45f,
            float wobbleAmplitude = 0.08f,
            float wobbleSpeed = 18f)
        {
            overstretchedMinimumWidthMultiplier = Mathf.Clamp(
                minimumWidthMultiplier,
                0.1f,
                1f);
            overstretchedWobbleAmplitude = Mathf.Max(0f, wobbleAmplitude);
            overstretchedWobbleSpeed = Mathf.Max(0f, wobbleSpeed);
        }

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();

            if (linkSystem == null)
                linkSystem = GetComponent<SplitLinkSystem>();
            if (buildController == null)
                buildController = FindAnyObjectByType<MicroBuildChoiceController>();
            if (combat == null)
                combat = GetComponent<SplitLinkCombat>();
            if (combat == null)
                combat = FindAnyObjectByType<SplitLinkCombat>();

            if (broadArcHalo == null)
            {
                Transform haloTransform = transform.Find("BroadArcDamageHalo");
                if (haloTransform != null)
                    broadArcHalo = haloTransform.GetComponent<LineRenderer>();
            }

            ConfigureRenderer();
            if (broadArcHalo != null)
                ConfigureHaloRenderer();
        }

        private void LateUpdate()
        {
            if (_line == null)
                return;

            if (linkSystem == null ||
                !linkSystem.TryGetFullSegment(
                    out Vector2 start,
                    out Vector2 end))
            {
                _line.enabled = false;
                SetHaloEnabled(false);
                return;
            }

            MicroBuildChoice selectedBuild =
                buildController != null
                    ? buildController.SelectedBuild
                    : MicroBuildChoice.None;

            _line.enabled = true;
            UpdateMainLinkGeometry(start, end);

            ApplyMainLinkReadability(selectedBuild);
            UpdateBroadArcHalo(selectedBuild);
        }

        private void ApplyMainLinkReadability(MicroBuildChoice selectedBuild)
        {
            Color color = linkColor;
            float currentWidth = width;

            switch (selectedBuild)
            {
                case MicroBuildChoice.ConductiveFocus:
                {
                    float conductivity =
                        combat != null ? combat.Conductivity01 : 0f;
                    float pulse =
                        (0.5f + 0.5f * Mathf.Sin(Time.time * 14f)) *
                        conductivity;

                    color = Color.Lerp(
                        conductiveFocusColor,
                        conductiveActiveColor,
                        Mathf.Clamp01(conductivity * 0.8f + pulse * 0.2f));

                    currentWidth *= 1f + 0.20f * pulse;
                    break;
                }

                case MicroBuildChoice.BroadArc:
                    color = broadArcColor;
                    break;

                case MicroBuildChoice.Capacitor:
                {
                    float charge =
                        combat != null ? combat.CapacitorCharge01 : 0f;
                    float pulse =
                        (0.5f + 0.5f * Mathf.Sin(Time.time * 16f)) *
                        charge;

                    color = Color.Lerp(
                        linkColor,
                        capacitorChargedColor,
                        charge);

                    currentWidth *=
                        1f + charge * 0.35f + pulse * 0.08f;
                    break;
                }
            }

            float tension = combat != null
                ? combat.SurfaceTensionOverstretch01
                : 0f;
            if (tension > 0f)
            {
                float pulse =
                    0.5f + 0.5f * Mathf.Sin(Time.time * 20f);
                color = Color.Lerp(
                    color,
                    overstretchedColor,
                    Mathf.Clamp01(tension * (0.75f + pulse * 0.15f)));
                currentWidth *= Mathf.Lerp(
                    1f,
                    overstretchedMinimumWidthMultiplier,
                    tension);
            }

            _line.startWidth = currentWidth;
            _line.endWidth = currentWidth;
            _line.startColor = color;
            _line.endColor = color;
        }

        private void UpdateMainLinkGeometry(Vector2 start, Vector2 end)
        {
            float tension = combat != null
                ? combat.SurfaceTensionOverstretch01
                : 0f;

            if (tension <= 0.001f || overstretchedWobbleAmplitude <= 0f)
            {
                _line.positionCount = 2;
                _line.SetPosition(0, start);
                _line.SetPosition(1, end);
                return;
            }

            Vector2 delta = end - start;
            Vector2 perpendicular = delta.sqrMagnitude > 0.0001f
                ? new Vector2(-delta.y, delta.x).normalized
                : Vector2.up;
            float wobble = Mathf.Sin(Time.time * overstretchedWobbleSpeed) *
                overstretchedWobbleAmplitude * tension;
            Vector2 midpoint = (start + end) * 0.5f + perpendicular * wobble;

            _line.positionCount = 3;
            _line.SetPosition(0, start);
            _line.SetPosition(1, midpoint);
            _line.SetPosition(2, end);
        }

        private void UpdateBroadArcHalo(MicroBuildChoice selectedBuild)
        {
            if (broadArcHalo == null ||
                selectedBuild != MicroBuildChoice.BroadArc ||
                combat == null ||
                !linkSystem.TryGetTrimmedSegment(
                    combat.EndpointInset,
                    out Vector2 start,
                    out Vector2 end))
            {
                SetHaloEnabled(false);
                return;
            }

            float haloWidth = Mathf.Max(
                broadArcMinimumHaloWidth,
                width + combat.HitPadding);

            float tension = Mathf.Clamp01(combat.SurfaceTensionOverstretch01);
            Color haloColor = broadArcHaloColor;
            haloColor.a *= Mathf.Lerp(1f, 0.35f, tension);

            broadArcHalo.enabled = true;
            broadArcHalo.positionCount = 2;
            broadArcHalo.startWidth = haloWidth;
            broadArcHalo.endWidth = haloWidth;
            broadArcHalo.startColor = haloColor;
            broadArcHalo.endColor = haloColor;
            broadArcHalo.SetPosition(0, start);
            broadArcHalo.SetPosition(1, end);
        }

        private void SetHaloEnabled(bool value)
        {
            if (broadArcHalo != null)
                broadArcHalo.enabled = value;
        }

        private void ConfigureRenderer()
        {
            _line.useWorldSpace = true;
            _line.positionCount = 2;
            _line.startWidth = width;
            _line.endWidth = width;
            _line.startColor = linkColor;
            _line.endColor = linkColor;
            _line.numCapVertices = 4;
            _line.sortingOrder = 6;

            Shader shader = FindLineShader();
            if (shader != null)
            {
                _runtimeMaterial = new Material(shader)
                {
                    name = "SplitLinkVisual_Runtime"
                };
                _line.material = _runtimeMaterial;
            }

            _line.enabled = false;
        }

        private void ConfigureHaloRenderer()
        {
            if (broadArcHalo == null)
                return;

            broadArcHalo.useWorldSpace = true;
            broadArcHalo.positionCount = 2;
            broadArcHalo.startWidth = broadArcMinimumHaloWidth;
            broadArcHalo.endWidth = broadArcMinimumHaloWidth;
            broadArcHalo.startColor = broadArcHaloColor;
            broadArcHalo.endColor = broadArcHaloColor;
            broadArcHalo.numCapVertices = 6;
            broadArcHalo.sortingOrder = 5;

            Shader shader = FindLineShader();
            if (shader != null && broadArcHalo.sharedMaterial == null)
            {
                _runtimeHaloMaterial = new Material(shader)
                {
                    name = "BroadArcHalo_Runtime"
                };
                broadArcHalo.material = _runtimeHaloMaterial;
            }

            broadArcHalo.enabled = false;
        }

        private static Shader FindLineShader()
        {
            Shader shader = Shader.Find(
                "Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            return shader;
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
                Destroy(_runtimeMaterial);
            if (_runtimeHaloMaterial != null)
                Destroy(_runtimeHaloMaterial);
        }

        private void OnValidate()
        {
            width = Mathf.Max(0.01f, width);
            broadArcMinimumHaloWidth =
                Mathf.Max(0.01f, broadArcMinimumHaloWidth);
            overstretchedMinimumWidthMultiplier = Mathf.Clamp(
                overstretchedMinimumWidthMultiplier,
                0.1f,
                1f);
            overstretchedWobbleAmplitude =
                Mathf.Max(0f, overstretchedWobbleAmplitude);
            overstretchedWobbleSpeed =
                Mathf.Max(0f, overstretchedWobbleSpeed);

            if (_line == null)
                _line = GetComponent<LineRenderer>();

            if (_line != null)
            {
                _line.startWidth = width;
                _line.endWidth = width;
                _line.startColor = linkColor;
                _line.endColor = linkColor;
            }
        }
    }
}
