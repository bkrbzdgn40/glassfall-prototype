using Glassfall.Gameplay.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Glassfall.Gameplay.Builds
{
    [DisallowMultipleComponent]
    public sealed class MicroBuildChoiceController : MonoBehaviour
    {
        [SerializeField] private SplitLinkCombat combat;

        private MicroBuildChoice _selectedBuild;
        private float _selectedAtUnscaledTime = -1f;

        public SplitLinkCombat Combat => combat;
        public MicroBuildChoice SelectedBuild => _selectedBuild;
        public bool HasChoice => _selectedBuild != MicroBuildChoice.None;
        public float SelectedAtUnscaledTime => _selectedAtUnscaledTime;

        public void Configure(SplitLinkCombat source)
        {
            combat = source;
        }

        private void Awake()
        {
            if (combat == null)
                combat = FindAnyObjectByType<SplitLinkCombat>();

            combat?.ResetRuntimeBuildTuning();
        }

        private void Update()
        {
            if (HasChoice || Keyboard.current == null)
                return;

            if (Keyboard.current.digit1Key.wasPressedThisFrame ||
                Keyboard.current.numpad1Key.wasPressedThisFrame)
            {
                TryChoose(MicroBuildChoice.ConductiveFocus);
            }
            else if (Keyboard.current.digit2Key.wasPressedThisFrame ||
                Keyboard.current.numpad2Key.wasPressedThisFrame)
            {
                TryChoose(MicroBuildChoice.BroadArc);
            }
            else if (Keyboard.current.digit3Key.wasPressedThisFrame ||
                Keyboard.current.numpad3Key.wasPressedThisFrame)
            {
                TryChoose(MicroBuildChoice.Capacitor);
            }
        }

        public bool TryChoose(MicroBuildChoice choice)
        {
            if (HasChoice || choice == MicroBuildChoice.None || combat == null)
                return false;

            switch (choice)
            {
                case MicroBuildChoice.ConductiveFocus:
                    combat.ApplyConductiveFocusTuning();
                    break;
                case MicroBuildChoice.BroadArc:
                    combat.ApplyBroadArcTuning();
                    break;
                case MicroBuildChoice.Capacitor:
                    combat.ConfigureCapacitor();
                    break;
                default:
                    return false;
            }

            _selectedBuild = choice;
            _selectedAtUnscaledTime = Time.unscaledTime;
            return true;
        }

        public void ResetChoiceForSession()
        {
            _selectedBuild = MicroBuildChoice.None;
            _selectedAtUnscaledTime = -1f;
            combat?.ResetRuntimeBuildTuning();
        }
    }
}
