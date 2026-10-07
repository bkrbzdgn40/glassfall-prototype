using Glassfall.Gameplay.Rewards;
using Glassfall.Gameplay.Run;
using UnityEngine;

namespace Glassfall.Presentation
{
    [DisallowMultipleComponent]
    public sealed class MidRunRewardOverlay : MonoBehaviour
    {
        [SerializeField] private MicroRunController runController;
        [SerializeField] private MidRunRewardController rewardController;

        private GUIStyle _headerStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _cardStyle;
        private GUIStyle _keyStyle;
        private GUIStyle _categoryStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _effectStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _detailStyle;

        [Header("Reward Presentation")]
        [Min(0.01f)]
        [SerializeField] private float entryFadeSeconds = 0.22f;

        private float _entryElapsed;
        private bool _wasVisible;
        private float _lastPresentationUpdateTime = -1f;

        public void Configure(
            MicroRunController run,
            MidRunRewardController rewards)
        {
            runController = run;
            rewardController = rewards;
        }

        public void ConfigurePresentation(float fadeSeconds = 0.22f)
        {
            entryFadeSeconds = Mathf.Max(0.01f, fadeSeconds);
        }

        private void Awake()
        {
            if (runController == null)
                runController = GetComponent<MicroRunController>();
            if (rewardController == null)
                rewardController =
                    GetComponent<MidRunRewardController>();
            if (rewardController == null)
                rewardController =
                    FindAnyObjectByType<MidRunRewardController>();
        }

        private void OnGUI()
        {
            bool visible = runController != null &&
                rewardController != null &&
                runController.Phase == MicroRunPhase.RewardChoice &&
                rewardController.IsChoosing;
            if (!visible)
            {
                _entryElapsed = 0f;
                _wasVisible = false;
                _lastPresentationUpdateTime = -1f;
                return;
            }

            if (!_wasVisible)
            {
                _entryElapsed = 0f;
                _wasVisible = true;
            }

            float now = Time.unscaledTime;
            float delta = _lastPresentationUpdateTime < 0f
                ? 0f
                : Mathf.Max(0f, now - _lastPresentationUpdateTime);
            _lastPresentationUpdateTime = now;
            _entryElapsed = Mathf.Min(entryFadeSeconds, _entryElapsed + delta);

            if (runController == null ||
                rewardController == null ||
                runController.Phase != MicroRunPhase.RewardChoice ||
                !rewardController.IsChoosing)
            {
                return;
            }

            EnsureStyles();
            float entryAlpha = Mathf.Clamp01(
                _entryElapsed / Mathf.Max(entryFadeSeconds, 0.01f));
            Color previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, entryAlpha);
            DrawRewardChoice(rewardController.ActiveRewardStep);
            GUI.color = previousColor;
        }

        private void DrawRewardChoice(int rewardStep)
        {
            Color previousColor = GUI.color;
            float entryAlpha = Mathf.Clamp01(
                _entryElapsed / Mathf.Max(entryFadeSeconds, 0.01f));
            GUI.color = new Color(0f, 0f, 0f, 0.58f * entryAlpha);
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture);
            GUI.color = previousColor;

            float availableWidth = Mathf.Max(300f, Screen.width - 36f);
            float totalWidth = Mathf.Min(1160f, availableWidth);
            const float gap = 14f;
            float cardWidth = (totalWidth - gap * 2f) / 3f;
            float cardHeight = Mathf.Clamp(
                Screen.height * 0.43f,
                270f,
                340f);

            float left = (Screen.width - totalWidth) * 0.5f;
            float top = Mathf.Max(
                28f,
                (Screen.height - cardHeight) * 0.5f - 58f);

            GUI.Label(
                new Rect(left, top, totalWidth, 46f),
                $"CHOOSE A REWARD · STEP {rewardStep}",
                _headerStyle);

            GUI.Label(
                new Rect(left, top + 44f, totalWidth, 26f),
                rewardController.IsGameplayFrozen
                    ? "GAMEPLAY PAUSED · Click a card or press 1 / 2 / 3"
                    : "Click a card or press 1 / 2 / 3",
                _hintStyle);

            float cardsTop = top + 82f;

            MidRunReward first =
                rewardController.GetActiveChoice(0);
            MidRunReward second =
                rewardController.GetActiveChoice(1);
            MidRunReward third =
                rewardController.GetActiveChoice(2);

            DrawCard(
                new Rect(left, cardsTop, cardWidth, cardHeight),
                first,
                "1");

            DrawCard(
                new Rect(
                    left + cardWidth + gap,
                    cardsTop,
                    cardWidth,
                    cardHeight),
                second,
                "2");

            DrawCard(
                new Rect(
                    left + (cardWidth + gap) * 2f,
                    cardsTop,
                    cardWidth,
                    cardHeight),
                third,
                "3");
        }

        private void DrawCard(
            Rect rect,
            MidRunReward reward,
            string key)
        {
            if (GUI.Button(rect, GUIContent.none, _cardStyle))
                rewardController.TryChoose(reward);

            Color accent = GetAccent(reward);
            Color previousColor = GUI.color;
            GUI.color = accent;
            GUI.DrawTexture(
                new Rect(rect.x, rect.y, rect.width, 5f),
                Texture2D.whiteTexture);
            GUI.color = previousColor;

            const float padding = 18f;

            _keyStyle.normal.textColor = accent;
            GUI.Label(
                new Rect(
                    rect.x + padding,
                    rect.y + 14f,
                    rect.width - padding * 2f,
                    40f),
                $"[{key}]",
                _keyStyle);

            GUI.Label(
                new Rect(
                    rect.x + padding,
                    rect.y + 60f,
                    rect.width - padding * 2f,
                    54f),
                GetTitle(reward),
                _titleStyle);

            GUI.Label(
                new Rect(
                    rect.x + padding,
                    rect.y + 40f,
                    rect.width - padding * 2f,
                    18f),
                GetCategoryLabel(reward),
                _categoryStyle);

            GUI.Label(
                new Rect(
                    rect.x + padding,
                    rect.y + 126f,
                    rect.width - padding * 2f,
                    78f),
                GetEffect(reward),
                _effectStyle);

            GUI.Label(
                new Rect(
                    rect.x + padding,
                    rect.y + rect.height - 88f,
                    rect.width - padding * 2f,
                    68f),
                GetDetail(reward),
                _detailStyle);
        }

        private void EnsureStyles()
        {
            if (_headerStyle != null)
                return;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _headerStyle.normal.textColor = Color.white;

            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter
            };
            _hintStyle.normal.textColor =
                new Color(0.82f, 0.90f, 1f, 1f);

            _cardStyle = new GUIStyle(GUI.skin.button);

            _keyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _categoryStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false
            };
            _categoryStyle.normal.textColor =
                new Color(0.68f, 0.82f, 0.94f, 1f);

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            _titleStyle.normal.textColor = Color.white;

            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            _bodyStyle.normal.textColor =
                new Color(0.94f, 0.97f, 1f, 1f);

            _effectStyle = new GUIStyle(_bodyStyle)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold
            };

            _detailStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            _detailStyle.normal.textColor =
                new Color(0.75f, 0.84f, 0.94f, 1f);
        }

        private void OnValidate()
        {
            entryFadeSeconds = Mathf.Max(0.01f, entryFadeSeconds);
        }

        private static string GetTitle(MidRunReward reward)
        {
            switch (reward)
            {
                case MidRunReward.EfficientDivision:
                    return "EFFICIENT DIVISION";
                case MidRunReward.AbsorbentCore:
                    return "ABSORBENT CORE";
                case MidRunReward.StableLink:
                    return "STABLE LINK";
                case MidRunReward.ConductiveResidue:
                    return "CONDUCTIVE RESIDUE";
                case MidRunReward.GravitationalDrop:
                    return "GRAVITATIONAL DROP";
                case MidRunReward.DenseCore:
                    return "DENSE CORE";
                case MidRunReward.HighCohesion:
                    return "HIGH COHESION";
                case MidRunReward.ElasticMembrane:
                    return "ELASTIC MEMBRANE";
                case MidRunReward.SlowEvaporation:
                    return "SLOW EVAPORATION";
                case MidRunReward.PurifyingResonance:
                    return "PURIFYING RESONANCE";
                default:
                    return "NO OFFER";
            }
        }

        private static string GetEffect(MidRunReward reward)
        {
            switch (reward)
            {
                case MidRunReward.EfficientDivision:
                    return "Split cost\n10%  →  7%";
                case MidRunReward.AbsorbentCore:
                    return "Merged collection\n100%  →  125%";
                case MidRunReward.StableLink:
                    return "Longer central\nlink damage segment";
                case MidRunReward.ConductiveResidue:
                    return "Wet conductivity\nstarts earlier";
                case MidRunReward.GravitationalDrop:
                    return "Merged attraction\nradius +35%";
                case MidRunReward.DenseCore:
                    return "Merged body shove\n+25%";
                case MidRunReward.HighCohesion:
                    return "Merged mass shear\n-25%";
                case MidRunReward.ElasticMembrane:
                    return "Stable link range\n+20%";
                case MidRunReward.SlowEvaporation:
                    return "Wet trail decay\n-25%";
                case MidRunReward.PurifyingResonance:
                    return "Recovery cleanse\n+15%";
                default:
                    return "No compatible reward available.";
            }
        }

        private static string GetDetail(MidRunReward reward)
        {
            switch (reward)
            {
                case MidRunReward.EfficientDivision:
                    return "Preserve more mass when you commit to split form.";
                case MidRunReward.AbsorbentCore:
                    return "Harvest neutral droplets more efficiently while merged.";
                case MidRunReward.StableLink:
                    return "More usable center link without removing endpoint exclusion.";
                case MidRunReward.ConductiveResidue:
                    return "Threshold 0.15 → 0.10. Conductive Focus remains stronger.";
                case MidRunReward.GravitationalDrop:
                    return "Pull neutral and recovery mass from farther away while merged.";
                case MidRunReward.DenseCore:
                    return "Reposition enemies more strongly before splitting for execution.";
                case MidRunReward.HighCohesion:
                    return "Merged form keeps more of its body when enemy pressure lands.";
                case MidRunReward.ElasticMembrane:
                    return "Hold full link efficiency across a wider separation band.";
                case MidRunReward.SlowEvaporation:
                    return "Prepared wet routes stay useful and conductive for longer.";
                case MidRunReward.PurifyingResonance:
                    return "Recovery purification removes a little more contamination without changing failure thresholds.";
                default:
                    return string.Empty;
            }
        }

        private static string GetCategoryLabel(MidRunReward reward)
        {
            switch (MidRunRewardRules.GetCategory(reward))
            {
                case MidRunRewardCategory.LinkSplit:
                    return "LINK / SPLIT";
                case MidRunRewardCategory.MergedMass:
                    return "MERGED / MASS";
                case MidRunRewardCategory.WetnessConductivity:
                    return "WETNESS / CONDUCTIVITY";
                case MidRunRewardCategory.RecoveryResonance:
                    return "RECOVERY / RESONANCE";
                default:
                    return "";
            }
        }

        private static Color GetAccent(MidRunReward reward)
        {
            switch (reward)
            {
                case MidRunReward.EfficientDivision:
                    return new Color(0.72f, 0.92f, 1f, 1f);
                case MidRunReward.AbsorbentCore:
                    return new Color(0.35f, 1f, 0.68f, 1f);
                case MidRunReward.StableLink:
                    return new Color(0.42f, 0.78f, 1f, 1f);
                case MidRunReward.ConductiveResidue:
                    return new Color(0.20f, 1f, 0.90f, 1f);
                case MidRunReward.GravitationalDrop:
                    return new Color(0.42f, 0.96f, 0.72f, 1f);
                case MidRunReward.DenseCore:
                    return new Color(1f, 0.72f, 0.34f, 1f);
                case MidRunReward.HighCohesion:
                    return new Color(0.96f, 0.58f, 0.78f, 1f);
                case MidRunReward.ElasticMembrane:
                    return new Color(0.54f, 0.78f, 1f, 1f);
                case MidRunReward.SlowEvaporation:
                    return new Color(0.30f, 0.90f, 1f, 1f);
                case MidRunReward.PurifyingResonance:
                    return new Color(0.78f, 0.90f, 1f, 1f);
                default:
                    return Color.white;
            }
        }
    }
}
