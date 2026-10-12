using Glassfall.Gameplay.Combat;
using Glassfall.Gameplay.Droplet;
using Glassfall.Gameplay.Encounter;
using Glassfall.Gameplay.Run;
using UnityEngine;

namespace Glassfall.Presentation
{
    [DisallowMultipleComponent]
    public sealed class WorldNavigationOverlay : MonoBehaviour
    {
        private const int ReadabilityVersion = 1;

        [Header("References")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private MicroRunController runController;
        [SerializeField] private DropletSplitSystem splitSystem;
        [SerializeField] private ArcHunter arcHunter;
        [SerializeField] private ResonanceNode resonanceNode;
        [SerializeField] private Transform[] routeTargets;
        [Min(0)]
        [SerializeField] private int routeWorldSegmentIndex;

        [Header("Indicator Readability")]
        [Min(24f)]
        [SerializeField] private float indicatorWidth = 104f;
        [Min(24f)]
        [SerializeField] private float indicatorHeight = 38f;
        [Min(8f)]
        [SerializeField] private float edgePadding = 58f;
        [Min(0f)]
        [SerializeField] private float indicatorSpacing = 46f;
        [Range(11, 20)]
        [SerializeField] private int indicatorFontSize = 15;
        [Range(0.5f, 1f)]
        [SerializeField] private float backgroundAlpha = 0.82f;
        [SerializeField] private int readabilityVersion = ReadabilityVersion;

        private GUIStyle _indicatorStyle;
        private GUIStyle _indicatorBoxStyle;
        private GUIStyle _indicatorShadowStyle;

        public void Configure(
            Camera camera,
            MicroRunController run,
            DropletSplitSystem playerSplit,
            ArcHunter hunter,
            ResonanceNode node,
            Transform[] targets)
        {
            targetCamera = camera;
            runController = run;
            splitSystem = playerSplit;
            arcHunter = hunter;
            resonanceNode = node;
            ConfigureWorldRoute(0, targets);
        }

        public void ConfigureWorldRoute(
            int worldSegmentIndex,
            Transform[] targets)
        {
            routeWorldSegmentIndex = Mathf.Max(0, worldSegmentIndex);
            routeTargets = targets != null
                ? (Transform[])targets.Clone()
                : null;
        }

        public void ConfigureReadability(
            float width = 104f,
            float height = 38f,
            float padding = 58f,
            float spacing = 46f,
            int fontSize = 15,
            float panelAlpha = 0.82f)
        {
            indicatorWidth = Mathf.Max(24f, width);
            indicatorHeight = Mathf.Max(24f, height);
            edgePadding = Mathf.Max(8f, padding);
            indicatorSpacing = Mathf.Max(0f, spacing);
            indicatorFontSize = Mathf.Clamp(fontSize, 11, 20);
            backgroundAlpha = Mathf.Clamp(panelAlpha, 0.5f, 1f);
            readabilityVersion = ReadabilityVersion;
            _indicatorStyle = null;
            _indicatorBoxStyle = null;
            _indicatorShadowStyle = null;
        }

        private void Awake()
        {
            if (readabilityVersion < ReadabilityVersion)
            {
                ConfigureReadability();
                readabilityVersion = ReadabilityVersion;
            }
            ResolveReferences();
        }

        private void OnGUI()
        {
            if (Event.current == null ||
                Event.current.type != EventType.Repaint)
            {
                return;
            }

            ResolveReferences();
            if (targetCamera == null ||
                runController == null ||
                runController.Phase == MicroRunPhase.ChooseBuild ||
                runController.Phase == MicroRunPhase.Complete ||
                runController.Phase == MicroRunPhase.Failed ||
                runController.IsFailureBeat)
            {
                return;
            }

            EnsureStyles();

            int indicatorIndex = 0;
            DrawPlayerIndicators(ref indicatorIndex);

            if (runController.Phase == MicroRunPhase.Encounter &&
                arcHunter != null &&
                arcHunter.gameObject.activeInHierarchy)
            {
                DrawWorldIndicator(
                    arcHunter.transform.position,
                    "ENEMY",
                    new Color(1f, 0.60f, 0.34f, 0.98f),
                    ref indicatorIndex);
                return;
            }

            if (TryGetObjectivePosition(out Vector2 objectivePosition))
            {
                DrawWorldIndicator(
                    objectivePosition,
                    GetObjectiveLabel(),
                    new Color(0.82f, 1f, 1f, 0.98f),
                    ref indicatorIndex);
            }
        }

        private void ResolveReferences()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
            if (runController == null)
                runController = FindAnyObjectByType<MicroRunController>();
            if (splitSystem == null)
                splitSystem = FindAnyObjectByType<DropletSplitSystem>();
            if (arcHunter == null)
                arcHunter = FindAnyObjectByType<ArcHunter>();
            if (resonanceNode == null)
                resonanceNode = FindAnyObjectByType<ResonanceNode>();
        }

        private void DrawPlayerIndicators(ref int indicatorIndex)
        {
            if (splitSystem == null)
                return;

            if (splitSystem.IsSplit && splitSystem.PrimaryTransform != null)
            {
                DrawWorldIndicator(
                    splitSystem.PrimaryTransform.position,
                    "PRIMARY",
                    new Color(0.34f, 1f, 0.97f, 0.98f),
                    ref indicatorIndex);
                if (splitSystem.MirrorTransform != null)
                {
                    DrawWorldIndicator(
                        splitSystem.MirrorTransform.position,
                        "MIRROR",
                        new Color(0.40f, 0.90f, 0.76f, 0.98f),
                        ref indicatorIndex);
                }
            }
        }

        private bool TryGetObjectivePosition(out Vector2 position)
        {
            position = Vector2.zero;
            switch (runController.Phase)
            {
                case MicroRunPhase.ArmResonance:
                    if (resonanceNode != null &&
                        resonanceNode.gameObject.activeInHierarchy)
                    {
                        position = resonanceNode.transform.position;
                        return true;
                    }
                    return false;

                case MicroRunPhase.Encounter:
                    if (!runController.IsAwaitingEncounterArrival)
                        return false;
                    position = runController.CurrentEncounterTargetPosition;
                    return true;

                case MicroRunPhase.Recovery:
                case MicroRunPhase.Intermission:
                    return TryGetNextRouteTarget(out position);

                default:
                    return false;
            }
        }

        private bool TryGetNextRouteTarget(out Vector2 position)
        {
            if (runController.TryGetNextEncounterTargetPosition(
                    out position))
            {
                return true;
            }

            int nextIndex = runController.CurrentWorldEncounter;
            bool routeMatchesCurrentWorld =
                runController.CurrentWorldSegmentIndex ==
                routeWorldSegmentIndex;
            if (routeMatchesCurrentWorld &&
                routeTargets != null &&
                nextIndex >= 0 &&
                nextIndex < routeTargets.Length &&
                routeTargets[nextIndex] != null)
            {
                position = routeTargets[nextIndex].position;
                return true;
            }

            position = Vector2.zero;
            return false;
        }

        private string GetObjectiveLabel()
        {
            switch (runController.Phase)
            {
                case MicroRunPhase.ArmResonance:
                    return "RESONANCE";
                case MicroRunPhase.Encounter:
                    return "NEXT HUNT";
                default:
                    return "NEXT AREA";
            }
        }

        private void DrawWorldIndicator(
            Vector2 worldPosition,
            string label,
            Color color,
            ref int indicatorIndex)
        {
            if (!TryGetOffscreenPosition(worldPosition, out Vector2 screenPosition))
                return;

            screenPosition.y += indicatorIndex * indicatorSpacing;
            screenPosition = ClampIndicatorToScreen(screenPosition);
            Rect rect = new Rect(
                screenPosition.x - indicatorWidth * 0.5f,
                screenPosition.y - indicatorHeight * 0.5f,
                indicatorWidth,
                indicatorHeight);
            Rect outlineRect = new Rect(
                rect.x - 1f,
                rect.y - 1f,
                rect.width + 2f,
                rect.height + 2f);

            Color previousColor = GUI.color;
            GUI.color = new Color(
                color.r,
                color.g,
                color.b,
                0.34f);
            GUI.Box(outlineRect, GUIContent.none, _indicatorBoxStyle);
            GUI.color = new Color(
                0.01f,
                0.04f,
                0.07f,
                backgroundAlpha);
            GUI.Box(rect, GUIContent.none, _indicatorBoxStyle);
            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.Label(
                new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height),
                GetDirectionGlyph(worldPosition) + "  " + label,
                _indicatorShadowStyle);
            GUI.color = color;
            GUI.Label(
                rect,
                GetDirectionGlyph(worldPosition) + "  " + label,
                _indicatorStyle);
            GUI.color = previousColor;
            indicatorIndex++;
        }

        private bool TryGetOffscreenPosition(
            Vector2 worldPosition,
            out Vector2 screenPosition)
        {
            screenPosition = Vector2.zero;
            if (targetCamera == null)
                return false;

            Vector3 viewport = targetCamera.WorldToViewportPoint(worldPosition);
            bool onScreen = viewport.z > 0f &&
                viewport.x >= 0f && viewport.x <= 1f &&
                viewport.y >= 0f && viewport.y <= 1f;
            if (onScreen)
                return false;

            Vector2 direction = new Vector2(
                viewport.x - 0.5f,
                0.5f - viewport.y);
            if (viewport.z < 0f)
                direction = -direction;
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector2.up;
            direction.Normalize();

            Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
            float halfWidth = Mathf.Max(
                1f,
                center.x - edgePadding - indicatorWidth * 0.5f);
            float halfHeight = Mathf.Max(
                1f,
                center.y - edgePadding - indicatorHeight * 0.5f);
            float scaleX = Mathf.Abs(direction.x) > 0.0001f
                ? halfWidth / Mathf.Abs(direction.x)
                : float.PositiveInfinity;
            float scaleY = Mathf.Abs(direction.y) > 0.0001f
                ? halfHeight / Mathf.Abs(direction.y)
                : float.PositiveInfinity;

            screenPosition = center + direction * Mathf.Min(scaleX, scaleY);
            return true;
        }

        private Vector2 ClampIndicatorToScreen(Vector2 position)
        {
            float halfWidth = indicatorWidth * 0.5f;
            float halfHeight = indicatorHeight * 0.5f;
            position.x = Mathf.Clamp(
                position.x,
                edgePadding + halfWidth,
                Screen.width - edgePadding - halfWidth);
            position.y = Mathf.Clamp(
                position.y,
                edgePadding + halfHeight,
                Screen.height - edgePadding - halfHeight);

            // Keep navigation prompts out of the two persistent HUD cards.
            Rect massCard = new Rect(16f, 108f, 310f, 112f);
            Rect objectiveCard = new Rect(
                Screen.width - 406f,
                16f,
                390f,
                132f);
            Rect indicatorRect = new Rect(
                position.x - halfWidth,
                position.y - halfHeight,
                indicatorWidth,
                indicatorHeight);
            if (indicatorRect.Overlaps(massCard))
                position.y = massCard.yMax + halfHeight + 8f;
            if (indicatorRect.Overlaps(objectiveCard))
                position.y = objectiveCard.yMax + halfHeight + 8f;

            return new Vector2(
                Mathf.Clamp(position.x, halfWidth, Screen.width - halfWidth),
                Mathf.Clamp(position.y, halfHeight, Screen.height - halfHeight));
        }

        private string GetDirectionGlyph(Vector2 worldPosition)
        {
            Vector3 viewport = targetCamera.WorldToViewportPoint(worldPosition);
            Vector2 direction = new Vector2(
                viewport.x - 0.5f,
                viewport.y - 0.5f);
            if (viewport.z < 0f)
                direction = -direction;

            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y) * 1.6f)
                return direction.x > 0f ? "▶" : "◀";
            if (Mathf.Abs(direction.y) > Mathf.Abs(direction.x) * 1.6f)
                return direction.y > 0f ? "▲" : "▼";
            if (direction.x > 0f)
                return direction.y > 0f ? "↗" : "↘";
            return direction.y > 0f ? "↖" : "↙";
        }

        private void EnsureStyles()
        {
            if (_indicatorStyle != null)
                return;

            _indicatorStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = indicatorFontSize,
                fontStyle = FontStyle.Bold,
                richText = false,
                clipping = TextClipping.Clip
            };
            _indicatorShadowStyle = new GUIStyle(_indicatorStyle);
            _indicatorBoxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter
            };
        }

        private void OnValidate()
        {
            indicatorWidth = Mathf.Max(24f, indicatorWidth);
            indicatorHeight = Mathf.Max(24f, indicatorHeight);
            edgePadding = Mathf.Max(8f, edgePadding);
            indicatorSpacing = Mathf.Max(0f, indicatorSpacing);
            indicatorFontSize = Mathf.Clamp(indicatorFontSize, 11, 20);
            backgroundAlpha = Mathf.Clamp(backgroundAlpha, 0.5f, 1f);
            routeWorldSegmentIndex = Mathf.Max(0, routeWorldSegmentIndex);
        }
    }
}
