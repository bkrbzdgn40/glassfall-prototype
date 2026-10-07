using Glassfall.Gameplay.Builds;
using Glassfall.Gameplay.Combat;
using UnityEngine;

namespace Glassfall.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    [RequireComponent(typeof(ArcHunter))]
    public sealed class ArcHunterVisual : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MicroBuildChoiceController buildController;
        [SerializeField] private SplitLinkCombat combat;
        [SerializeField] private SpriteRenderer authoredSprite;

        [Range(12, 64)]
        [SerializeField] private int segments = 28;

        [Min(0.01f)]
        [SerializeField] private float ringWidth = 0.10f;

        [SerializeField] private Color idleColor =
            new Color(1f, 0.44f, 0.35f, 0.95f);

        [SerializeField] private Color conductiveColor =
            new Color(0.25f, 0.95f, 1f, 1f);

        [SerializeField] private Color hitColor =
            new Color(0.45f, 0.95f, 1f, 1f);

        [SerializeField] private Color conductiveHitColor =
            new Color(0.92f, 1f, 1f, 1f);

        [SerializeField] private Color capacitorHitColor =
            new Color(1f, 0.84f, 0.30f, 1f);

        [SerializeField] private Color telegraphColor =
            new Color(1f, 0.82f, 0.22f, 1f);

        [SerializeField] private Color commitColor =
            new Color(1f, 0.96f, 0.75f, 1f);

        [SerializeField] private Color defeatedColor =
            new Color(0.5f, 0.75f, 0.85f, 0.22f);

        [Header("Combat Readability")]
        [Range(0f, 1f)]
        [SerializeField] private float ringAlphaMultiplier = 0.58f;

        [SerializeField] private Color hitFlashColor =
            new Color(0.85f, 1f, 1f, 1f);

        [Range(1f, 1.5f)]
        [SerializeField] private float hitPunchScale = 1.12f;

        [Min(0.01f)]
        [SerializeField] private float hitPunchDuration = 0.13f;

        [Header("Health Readability")]
        [SerializeField] private Color depletedHealthTint =
            new Color(0.42f, 0.58f, 0.62f, 1f);
        [Range(0f, 1f)]
        [SerializeField] private float healthDepletionTintStrength = 0.50f;
        [Range(0.2f, 1f)]
        [SerializeField] private float minimumHealthAlpha = 0.68f;

        [Header("Conduction Output Readability")]
        [SerializeField] private bool showConductionOutput = true;
        [Min(0.01f)]
        [SerializeField] private float outputSmoothTime = 0.22f;
        [Min(0f)]
        [SerializeField] private float outputHoldSeconds = 0.15f;
        [Min(0.01f)]
        [SerializeField] private float outputFadeSeconds = 0.70f;
        [SerializeField] private Vector2 outputScreenOffset =
            new Vector2(0f, -48f);

        [SerializeField] private int ringSortingOrder = 4;
        [SerializeField] private int spriteSortingOrder = 5;

        private ArcHunter _hunter;
        private LineRenderer _line;
        private Material _runtimeMaterial;
        private Vector3 _spriteBaseScale;
        private Color _spriteBaseColor = Color.white;
        private float _hitPunchRemaining;
        private bool _wasTakingLinkDamage;
        private bool _spriteStateCaptured;
        private float _smoothedOutputPerSecond;
        private float _outputSmoothVelocity;
        private float _outputAlpha;
        private float _lastOutputContactTime = -100f;
        private GUIStyle _outputStyle;
        private GUIStyle _outputShadowStyle;

        public void ConfigureReadability(
            MicroBuildChoiceController controller,
            SplitLinkCombat combatSource)
        {
            buildController = controller;
            combat = combatSource;
        }

        public void ConfigureCombatReadability(
            SpriteRenderer sprite,
            float newRingWidth = 0.065f,
            float newRingAlphaMultiplier = 0.58f,
            int newRingSortingOrder = 4,
            int newSpriteSortingOrder = 5)
        {
            authoredSprite = sprite;
            ringWidth = Mathf.Max(0.01f, newRingWidth);
            ringAlphaMultiplier = Mathf.Clamp01(newRingAlphaMultiplier);
            ringSortingOrder = newRingSortingOrder;
            spriteSortingOrder = newSpriteSortingOrder;
            ApplyLayering();
        }

        public void ConfigureHealthReadability(
            Color depletionTint,
            float tintStrength = 0.50f,
            float minimumAlpha = 0.68f)
        {
            depletedHealthTint = depletionTint;
            healthDepletionTintStrength = Mathf.Clamp01(tintStrength);
            minimumHealthAlpha = Mathf.Clamp(minimumAlpha, 0.2f, 1f);
        }

        public void ConfigureConductionOutputReadability(
            bool enabled = true,
            float smoothTime = 0.22f,
            float holdSeconds = 0.15f,
            float fadeSeconds = 0.70f)
        {
            showConductionOutput = enabled;
            outputSmoothTime = Mathf.Max(0.01f, smoothTime);
            outputHoldSeconds = Mathf.Max(0f, holdSeconds);
            outputFadeSeconds = Mathf.Max(0.01f, fadeSeconds);
        }

        private void Awake()
        {
            _hunter = GetComponent<ArcHunter>();
            _line = GetComponent<LineRenderer>();

            if (buildController == null)
                buildController = FindAnyObjectByType<MicroBuildChoiceController>();
            if (combat == null)
                combat = FindAnyObjectByType<SplitLinkCombat>();

            CacheAuthoredSprite();
            ConfigureRenderer();
            RebuildCircle();
        }

        private void Update()
        {
            if (_hunter == null || _line == null)
                return;

            if (_hunter.IsDefeated)
            {
                _smoothedOutputPerSecond = 0f;
                _outputSmoothVelocity = 0f;
                _outputAlpha = 0f;
                _line.startColor = defeatedColor;
                _line.endColor = defeatedColor;
                _line.startWidth = ringWidth;
                _line.endWidth = ringWidth;
                transform.localScale = Vector3.one * 0.75f;
                RestoreAuthoredSprite();
                _wasTakingLinkDamage = false;
                return;
            }

            bool takingLinkDamage = _hunter.IsTakingLinkDamage;
            if (takingLinkDamage && !_wasTakingLinkDamage)
                _hitPunchRemaining = hitPunchDuration;
            _wasTakingLinkDamage = takingLinkDamage;
            UpdateHitFeedback();
            UpdateConductionOutputFeedback();

            float conductivity = _hunter.Conductivity01;
            MicroBuildChoice selectedBuild =
                buildController != null
                    ? buildController.SelectedBuild
                    : MicroBuildChoice.None;

            Color idleStateColor = Color.Lerp(
                idleColor,
                conductiveColor,
                conductivity);

            Color hitStateColor = Color.Lerp(
                hitColor,
                conductiveHitColor,
                conductivity);

            float readabilityWidthMultiplier = 1f;

            ArcHunterCombatState combatState = _hunter.CombatState;
            float telegraph01 = _hunter.Telegraph01;
            if (combatState == ArcHunterCombatState.Telegraph)
            {
                idleStateColor = Color.Lerp(
                    idleStateColor,
                    telegraphColor,
                    0.35f + telegraph01 * 0.55f);
                readabilityWidthMultiplier += 0.20f +
                    telegraph01 * 0.30f;
            }
            else if (combatState == ArcHunterCombatState.CommitAttack)
            {
                idleStateColor = Color.Lerp(
                    idleStateColor,
                    commitColor,
                    0.75f);
                readabilityWidthMultiplier += 0.35f;
            }
            else if (combatState == ArcHunterCombatState.Recover)
            {
                idleStateColor = Color.Lerp(
                    idleStateColor,
                    conductiveColor,
                    0.25f);
                readabilityWidthMultiplier *= 0.82f;
            }

            if (selectedBuild == MicroBuildChoice.ConductiveFocus)
            {
                float emphasis = Mathf.Clamp01(conductivity);
                idleStateColor = Color.Lerp(
                    idleStateColor,
                    conductiveHitColor,
                    emphasis * 0.35f);
                hitStateColor = Color.Lerp(
                    hitStateColor,
                    Color.white,
                    emphasis * 0.25f);
                readabilityWidthMultiplier += emphasis * 0.30f;
            }
            else if (selectedBuild == MicroBuildChoice.Capacitor &&
                     _hunter.IsTakingLinkDamage)
            {
                float charge =
                    combat != null ? combat.CapacitorCharge01 : 0f;
                hitStateColor = Color.Lerp(
                    hitStateColor,
                    capacitorHitColor,
                    charge);
                readabilityWidthMultiplier += charge * 0.20f;
            }

            Color color = takingLinkDamage
                ? hitStateColor
                : idleStateColor;
            color.a *= ringAlphaMultiplier;

            _line.startColor = color;
            _line.endColor = color;
            _line.startWidth = ringWidth * readabilityWidthMultiplier;
            _line.endWidth = ringWidth * readabilityWidthMultiplier;

            float conductivePulse = 1f +
                Mathf.Sin(Time.time * 10f) *
                0.025f *
                conductivity;

            if (selectedBuild == MicroBuildChoice.ConductiveFocus)
            {
                conductivePulse +=
                    Mathf.Sin(Time.time * 15f) *
                    0.018f *
                    conductivity;
            }

            float hitPulse = takingLinkDamage
                ? 1f + Mathf.Sin(Time.time * 24f) * 0.06f
                : 1f;

            float combatPulse = 1f;
            if (combatState == ArcHunterCombatState.Telegraph)
            {
                combatPulse += Mathf.Sin(Time.time * 20f) * 0.035f +
                    0.035f * telegraph01;
            }
            else if (combatState == ArcHunterCombatState.CommitAttack)
            {
                combatPulse = 1.10f;
            }
            else if (combatState == ArcHunterCombatState.Recover)
            {
                combatPulse = 0.94f;
            }

            transform.localScale =
                Vector3.one * conductivePulse * hitPulse * combatPulse;

            if (_hitPunchRemaining <= 0f)
                ApplyCombatStateSpriteFeedback(combatState, telegraph01);
        }

        private void ConfigureRenderer()
        {
            _line.useWorldSpace = false;
            _line.loop = true;
            _line.startWidth = ringWidth;
            _line.endWidth = ringWidth;
            _line.numCornerVertices = 2;
            _line.sortingOrder = ringSortingOrder;

            Shader shader = Shader.Find(
                "Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                _runtimeMaterial = new Material(shader)
                {
                    name = "ArcHunterVisual_Runtime"
                };
                _line.material = _runtimeMaterial;
            }

            ApplyLayering();
        }

        private void CacheAuthoredSprite()
        {
            if (authoredSprite == null)
            {
                Transform child = transform.Find("PixelSprite");
                if (child != null)
                    authoredSprite = child.GetComponent<SpriteRenderer>();
            }

            if (authoredSprite == null || _spriteStateCaptured)
                return;

            _spriteBaseScale = authoredSprite.transform.localScale;
            _spriteBaseColor = authoredSprite.color;
            _spriteStateCaptured = true;
        }

        private void ApplyLayering()
        {
            if (_line == null)
                _line = GetComponent<LineRenderer>();
            if (_line != null)
                _line.sortingOrder = ringSortingOrder;

            if (authoredSprite != null)
                authoredSprite.sortingOrder = spriteSortingOrder;
        }

        private void UpdateHitFeedback()
        {
            CacheAuthoredSprite();
            if (!_spriteStateCaptured)
                return;

            if (_hitPunchRemaining <= 0f)
            {
                RestoreAuthoredSprite();
                return;
            }

            _hitPunchRemaining = Mathf.Max(
                0f,
                _hitPunchRemaining - Time.deltaTime);
            float normalized = 1f -
                _hitPunchRemaining / Mathf.Max(hitPunchDuration, 0.01f);
            float envelope = Mathf.Sin(Mathf.Clamp01(normalized) * Mathf.PI);
            float scale = Mathf.Lerp(1f, hitPunchScale, envelope);
            authoredSprite.transform.localScale = _spriteBaseScale * scale;
            authoredSprite.color = Color.Lerp(
                GetHealthAdjustedSpriteColor(),
                hitFlashColor,
                envelope);
        }

        private void RestoreAuthoredSprite()
        {
            if (!_spriteStateCaptured || authoredSprite == null)
                return;

            authoredSprite.transform.localScale = _spriteBaseScale;
            authoredSprite.color = GetHealthAdjustedSpriteColor();
        }

        private void ApplyCombatStateSpriteFeedback(
            ArcHunterCombatState combatState,
            float telegraph01)
        {
            if (!_spriteStateCaptured || authoredSprite == null)
                return;

            float scale = 1f;
            Color healthAdjustedColor = GetHealthAdjustedSpriteColor();
            Color color = healthAdjustedColor;
            if (combatState == ArcHunterCombatState.Telegraph)
            {
                scale = 1f + 0.04f * telegraph01 +
                    Mathf.Sin(Time.time * 20f) * 0.025f;
                color = Color.Lerp(
                    healthAdjustedColor,
                    telegraphColor,
                    0.18f + telegraph01 * 0.34f);
            }
            else if (combatState == ArcHunterCombatState.CommitAttack)
            {
                scale = 1.10f;
                color = Color.Lerp(
                    healthAdjustedColor,
                    commitColor,
                    0.42f);
            }
            else if (combatState == ArcHunterCombatState.Recover)
            {
                scale = 0.95f;
                color = Color.Lerp(
                    healthAdjustedColor,
                    conductiveColor,
                    0.18f);
            }

            authoredSprite.transform.localScale = _spriteBaseScale * scale;
            authoredSprite.color = color;
        }


        private Color GetHealthAdjustedSpriteColor()
        {
            if (!_spriteStateCaptured || _hunter == null)
                return _spriteBaseColor;

            float damage01 = 1f - Mathf.Clamp01(_hunter.Health01);
            Color depleted = depletedHealthTint;
            depleted.a = _spriteBaseColor.a;
            Color color = Color.Lerp(
                _spriteBaseColor,
                depleted,
                damage01 * healthDepletionTintStrength);
            color.a = _spriteBaseColor.a * Mathf.Lerp(
                1f,
                minimumHealthAlpha,
                damage01);
            return color;
        }

        private void UpdateConductionOutputFeedback()
        {
            if (!showConductionOutput || combat == null || _hunter == null)
            {
                _smoothedOutputPerSecond = 0f;
                _outputSmoothVelocity = 0f;
                _outputAlpha = 0f;
                return;
            }

            float rawOutput = Mathf.Max(
                0f,
                combat.CurrentAppliedOutputPerSecond);
            float deltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);
            bool hasContact = combat.IsApplyingLinkDamage;

            _smoothedOutputPerSecond = Mathf.SmoothDamp(
                _smoothedOutputPerSecond,
                hasContact ? rawOutput : 0f,
                ref _outputSmoothVelocity,
                Mathf.Max(0.01f, outputSmoothTime),
                Mathf.Infinity,
                deltaTime);

            if (hasContact)
            {
                _lastOutputContactTime = Time.unscaledTime;
                _outputAlpha = Mathf.MoveTowards(
                    _outputAlpha,
                    1f,
                    deltaTime / Mathf.Max(0.01f, outputSmoothTime));
                return;
            }

            if (Time.unscaledTime - _lastOutputContactTime <=
                Mathf.Max(0f, outputHoldSeconds))
            {
                return;
            }

            _outputAlpha = Mathf.MoveTowards(
                _outputAlpha,
                0f,
                deltaTime / Mathf.Max(0.01f, outputFadeSeconds));
        }

        private void OnGUI()
        {
            if (!showConductionOutput ||
                _hunter == null ||
                _hunter.IsDefeated ||
                _outputAlpha <= 0.01f)
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
                return;

            Vector3 screen = camera.WorldToScreenPoint(transform.position);
            if (screen.z <= 0f)
                return;

            EnsureOutputStyles();
            const float width = 190f;
            const float height = 26f;
            float x = screen.x - width * 0.5f + outputScreenOffset.x;
            float y = Screen.height - screen.y - height * 0.5f +
                outputScreenOffset.y;
            Rect rect = new Rect(x, y, width, height);
            string label = $"CONDUCTION {_smoothedOutputPerSecond:0.0}/s";

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.72f * _outputAlpha);
            GUI.Label(
                new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height),
                label,
                _outputShadowStyle);
            GUI.color = new Color(0.82f, 1f, 1f, _outputAlpha);
            GUI.Label(rect, label, _outputStyle);
            GUI.color = previous;
        }

        private void EnsureOutputStyles()
        {
            if (_outputStyle != null)
                return;

            _outputStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _outputStyle.normal.textColor = Color.white;

            _outputShadowStyle = new GUIStyle(_outputStyle);
            _outputShadowStyle.normal.textColor = Color.black;
        }

        private void RebuildCircle()
        {
            int count = Mathf.Max(12, segments);
            _line.positionCount = count;
            float radius = _hunter != null ? _hunter.InteractionRadius : 0.32f;

            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                _line.SetPosition(
                    i,
                    new Vector3(
                        Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle) * radius,
                        0f));
            }
        }

        private void OnDestroy()
        {
            RestoreAuthoredSprite();
            if (_runtimeMaterial != null)
                Destroy(_runtimeMaterial);
        }

        private void OnValidate()
        {
            segments = Mathf.Clamp(segments, 12, 64);
            ringWidth = Mathf.Max(0.01f, ringWidth);
            ringAlphaMultiplier = Mathf.Clamp01(ringAlphaMultiplier);
            hitPunchScale = Mathf.Clamp(hitPunchScale, 1f, 1.5f);
            hitPunchDuration = Mathf.Max(0.01f, hitPunchDuration);
            healthDepletionTintStrength = Mathf.Clamp01(
                healthDepletionTintStrength);
            minimumHealthAlpha = Mathf.Clamp(
                minimumHealthAlpha,
                0.2f,
                1f);
            outputSmoothTime = Mathf.Max(0.01f, outputSmoothTime);
            outputHoldSeconds = Mathf.Max(0f, outputHoldSeconds);
            outputFadeSeconds = Mathf.Max(0.01f, outputFadeSeconds);
            ApplyLayering();
        }
    }
}
