using Glassfall.Gameplay.Builds;
using Glassfall.Gameplay.Combat;
using UnityEngine;

namespace Glassfall.Presentation
{
    [DisallowMultipleComponent]
    public sealed class MicroBuildChoiceOverlay : MonoBehaviour
    {
        [SerializeField] private MicroBuildChoiceController controller;

        [Min(0.5f)]
        [SerializeField] private float selectionFeedbackSeconds = 2.5f;

        private GUIStyle _headerStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _cardStyle;
        private GUIStyle _keyStyle;
        private GUIStyle _cardTitleStyle;
        private GUIStyle _cardBodyStyle;
        private GUIStyle _feedbackStyle;
        private GUIStyle _statusTitleStyle;
        private GUIStyle _statusBodyStyle;

        public void Configure(MicroBuildChoiceController source)
        {
            controller = source;
        }

        private void Awake()
        {
            if (controller == null)
                controller = GetComponent<MicroBuildChoiceController>();
        }

        private void OnGUI()
        {
            if (controller == null)
                return;

            EnsureStyles();

            if (!controller.HasChoice)
            {
                DrawChoiceOverlay();
                return;
            }

            DrawPersistentBuildStatus();

            if (Time.unscaledTime <=
                controller.SelectedAtUnscaledTime + selectionFeedbackSeconds)
            {
                DrawSelectionFeedback();
            }
        }

        private void DrawChoiceOverlay()
        {
            const float sideMargin = 36f;
            const float cardGap = 18f;
            const float headerHeight = 78f;
            const float footerHeight = 42f;

            float availableWidth = Mathf.Max(
                720f,
                Screen.width - sideMargin * 2f);

            float totalWidth = Mathf.Min(1180f, availableWidth);
            float cardWidth = (totalWidth - cardGap * 2f) / 3f;

            float desiredCardHeight = Mathf.Clamp(
                Screen.height * 0.50f,
                280f,
                390f);

            float totalHeight =
                headerHeight + desiredCardHeight + footerHeight;

            float left = (Screen.width - totalWidth) * 0.5f;
            float top = (Screen.height - totalHeight) * 0.5f;

            GUI.Label(
                new Rect(left, top, totalWidth, 48f),
                "CHOOSE A BUILD",
                _headerStyle);

            GUI.Label(
                new Rect(left, top + 46f, totalWidth, 28f),
                "Click a card or press 1 / 2 / 3",
                _hintStyle);

            float cardsTop = top + headerHeight;

            DrawBuildCard(
                new Rect(
                    left,
                    cardsTop,
                    cardWidth,
                    desiredCardHeight),
                MicroBuildChoice.ConductiveFocus,
                "1",
                "CONDUCTIVE FOCUS",
                "Wet targets conduct earlier\nand hit harder.",
                "Best for route planning and\nwet-trail setup.",
                new Color(0.18f, 1f, 0.86f, 1f));

            DrawBuildCard(
                new Rect(
                    left + cardWidth + cardGap,
                    cardsTop,
                    cardWidth,
                    desiredCardHeight),
                MicroBuildChoice.BroadArc,
                "2",
                "BROAD ARC",
                "Wider effective link,\nlower base damage.",
                "More forgiving aim with\na clear DPS tradeoff.",
                new Color(0.35f, 0.78f, 1f, 1f));

            DrawBuildCard(
                new Rect(
                    left + (cardWidth + cardGap) * 2f,
                    cardsTop,
                    cardWidth,
                    desiredCardHeight),
                MicroBuildChoice.Capacitor,
                "3",
                "CAPACITOR",
                "Stay split longer to\ncharge link damage.",
                "Rewards commitment and\nlonger split control.",
                new Color(1f, 0.82f, 0.28f, 1f));

            GUI.Label(
                new Rect(
                    left,
                    cardsTop + desiredCardHeight + 10f,
                    totalWidth,
                    30f),
                "One build per run. This choice stays locked in.",
                _hintStyle);
        }

        private void DrawBuildCard(
            Rect rect,
            MicroBuildChoice choice,
            string key,
            string title,
            string description,
            string playstyle,
            Color accent)
        {
            if (GUI.Button(rect, GUIContent.none, _cardStyle))
                controller.TryChoose(choice);

            Color previousColor = GUI.color;
            GUI.color = accent;
            GUI.DrawTexture(
                new Rect(rect.x, rect.y, rect.width, 5f),
                Texture2D.whiteTexture);
            GUI.color = previousColor;

            const float horizontalPadding = 22f;

            _keyStyle.normal.textColor = accent;
            GUI.Label(
                new Rect(
                    rect.x + horizontalPadding,
                    rect.y + 20f,
                    rect.width - horizontalPadding * 2f,
                    48f),
                "[" + key + "]",
                _keyStyle);

            GUI.Label(
                new Rect(
                    rect.x + horizontalPadding,
                    rect.y + 72f,
                    rect.width - horizontalPadding * 2f,
                    64f),
                title,
                _cardTitleStyle);

            GUI.Label(
                new Rect(
                    rect.x + horizontalPadding,
                    rect.y + 150f,
                    rect.width - horizontalPadding * 2f,
                    82f),
                description,
                _cardBodyStyle);

            GUI.Label(
                new Rect(
                    rect.x + horizontalPadding,
                    rect.y + rect.height - 88f,
                    rect.width - horizontalPadding * 2f,
                    64f),
                playstyle,
                _cardBodyStyle);
        }

        private void DrawPersistentBuildStatus()
        {
            const float width = 360f;
            const float height = 82f;
            const float margin = 16f;

            Rect box = new Rect(
                margin,
                margin,
                width,
                height);

            GUI.Box(box, GUIContent.none);

            MicroBuildChoice build = controller.SelectedBuild;
            Color accent = GetBuildAccent(build);
            string status = GetBuildStatus(build, controller.Combat);

            Color previousColor = GUI.color;
            GUI.color = accent;
            GUI.DrawTexture(
                new Rect(box.x, box.y, 6f, box.height),
                Texture2D.whiteTexture);
            GUI.color = previousColor;

            _statusTitleStyle.normal.textColor = accent;
            GUI.Label(
                new Rect(
                    box.x + 18f,
                    box.y + 8f,
                    box.width - 28f,
                    30f),
                GetBuildName(build),
                _statusTitleStyle);

            GUI.Label(
                new Rect(
                    box.x + 18f,
                    box.y + 39f,
                    box.width - 28f,
                    28f),
                status,
                _statusBodyStyle);

            if (build == MicroBuildChoice.Capacitor)
            {
                DrawCapacitorBar(
                    new Rect(
                        box.x + 18f,
                        box.y + box.height - 11f,
                        box.width - 36f,
                        5f),
                    controller.Combat != null
                        ? controller.Combat.CapacitorCharge01
                        : 0f,
                    accent);
            }
        }

        private static string GetBuildStatus(
            MicroBuildChoice build,
            SplitLinkCombat combat)
        {
            switch (build)
            {
                case MicroBuildChoice.ConductiveFocus:
                {
                    float conductivity =
                        combat != null ? combat.Conductivity01 : 0f;
                    return conductivity > 0.01f
                        ? $"TARGET CONDUCTIVE  {conductivity * 100f:0}%"
                        : "SEEK WET TARGET";
                }

                case MicroBuildChoice.BroadArc:
                    return "WIDE DAMAGE BAND ACTIVE";

                case MicroBuildChoice.Capacitor:
                {
                    float charge =
                        combat != null ? combat.CapacitorCharge01 : 0f;
                    return $"LINK CHARGE  {charge * 100f:0}%";
                }

                default:
                    return string.Empty;
            }
        }

        private static void DrawCapacitorBar(
            Rect rect,
            float charge01,
            Color accent)
        {
            Color previousColor = GUI.color;

            GUI.color = new Color(1f, 1f, 1f, 0.16f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            float fill = Mathf.Clamp01(charge01);
            GUI.color = accent;
            GUI.DrawTexture(
                new Rect(
                    rect.x,
                    rect.y,
                    rect.width * fill,
                    rect.height),
                Texture2D.whiteTexture);

            GUI.color = previousColor;
        }

        private void DrawSelectionFeedback()
        {
            float width = Mathf.Min(620f, Screen.width - 40f);

            GUI.Box(
                new Rect(
                    (Screen.width - width) * 0.5f,
                    20f,
                    width,
                    62f),
                GUIContent.none);

            GUI.Label(
                new Rect(
                    (Screen.width - width) * 0.5f + 18f,
                    28f,
                    width - 36f,
                    46f),
                "BUILD: " + GetBuildName(controller.SelectedBuild),
                _feedbackStyle);
        }

        private void EnsureStyles()
        {
            if (_headerStyle != null)
                return;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _headerStyle.normal.textColor = Color.white;

            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                alignment = TextAnchor.MiddleCenter
            };
            _hintStyle.normal.textColor =
                new Color(0.86f, 0.92f, 1f, 1f);

            _cardStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.UpperCenter,
                wordWrap = true
            };

            _keyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _cardTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 23,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            _cardTitleStyle.normal.textColor = Color.white;

            _cardBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            _cardBodyStyle.normal.textColor =
                new Color(0.92f, 0.95f, 1f, 1f);

            _feedbackStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 25,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _feedbackStyle.normal.textColor = Color.white;

            _statusTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            _statusBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft
            };
            _statusBodyStyle.normal.textColor =
                new Color(0.92f, 0.95f, 1f, 1f);
        }

        private static Color GetBuildAccent(MicroBuildChoice choice)
        {
            switch (choice)
            {
                case MicroBuildChoice.ConductiveFocus:
                    return new Color(0.18f, 1f, 0.86f, 1f);
                case MicroBuildChoice.BroadArc:
                    return new Color(0.35f, 0.78f, 1f, 1f);
                case MicroBuildChoice.Capacitor:
                    return new Color(1f, 0.82f, 0.28f, 1f);
                default:
                    return Color.white;
            }
        }

        private static string GetBuildName(MicroBuildChoice choice)
        {
            switch (choice)
            {
                case MicroBuildChoice.ConductiveFocus:
                    return "CONDUCTIVE FOCUS";
                case MicroBuildChoice.BroadArc:
                    return "BROAD ARC";
                case MicroBuildChoice.Capacitor:
                    return "CAPACITOR";
                default:
                    return "NONE";
            }
        }

        private void OnValidate()
        {
            selectionFeedbackSeconds =
                Mathf.Max(0.5f, selectionFeedbackSeconds);
        }
    }
}
