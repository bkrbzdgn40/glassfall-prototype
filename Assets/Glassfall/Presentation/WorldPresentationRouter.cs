using Glassfall.Gameplay.Run;
using UnityEngine;

namespace Glassfall.Presentation
{
    [DisallowMultipleComponent]
    public sealed class WorldPresentationRouter : MonoBehaviour
    {
        [SerializeField] private MicroRunController runController;
        [SerializeField] private GameObject foggedWindowRoot;
        [SerializeField] private GameObject neonWindowRoot;
        [Min(0)]
        [SerializeField] private int foggedWindowSegmentIndex;
        [Min(0)]
        [SerializeField] private int neonWindowSegmentIndex = 1;

        private int _activeSegmentIndex = -1;

        public int ActiveSegmentIndex => _activeSegmentIndex;
        public bool IsFoggedWindowActive =>
            foggedWindowRoot != null && foggedWindowRoot.activeSelf;
        public bool IsNeonWindowActive =>
            neonWindowRoot != null && neonWindowRoot.activeSelf;

        public void Configure(
            MicroRunController run,
            GameObject foggedRoot,
            GameObject neonRoot,
            int foggedSegmentIndex = 0,
            int neonSegmentIndex = 1)
        {
            runController = run;
            foggedWindowRoot = foggedRoot;
            neonWindowRoot = neonRoot;
            foggedWindowSegmentIndex = Mathf.Max(0, foggedSegmentIndex);
            neonWindowSegmentIndex = Mathf.Max(0, neonSegmentIndex);
            ApplyCurrentWorld(force: true);
        }

        public static int ResolvePresentationSegment(
            int currentSegmentIndex,
            int foggedSegmentIndex = 0,
            int neonSegmentIndex = 1)
        {
            if (currentSegmentIndex == neonSegmentIndex)
                return neonSegmentIndex;
            if (currentSegmentIndex == foggedSegmentIndex)
                return foggedSegmentIndex;
            return -1;
        }

        private void Awake()
        {
            ResolveReferences();
            ApplyCurrentWorld(force: true);
        }

        private void OnEnable()
        {
            ResolveReferences();
            ApplyCurrentWorld(force: true);
        }

        private void Update()
        {
            ApplyCurrentWorld(force: false);
        }

        private void ResolveReferences()
        {
            if (runController == null)
                runController = GetComponent<MicroRunController>();
        }

        private void ApplyCurrentWorld(bool force)
        {
            int currentSegment = runController != null
                ? runController.CurrentWorldSegmentIndex
                : foggedWindowSegmentIndex;
            if (!force && currentSegment == _activeSegmentIndex)
                return;

            int selection = ResolvePresentationSegment(
                currentSegment,
                foggedWindowSegmentIndex,
                neonWindowSegmentIndex);
            bool showFogged = selection == foggedWindowSegmentIndex;
            bool showNeon = selection == neonWindowSegmentIndex;

            if (foggedWindowRoot != null &&
                foggedWindowRoot != neonWindowRoot &&
                foggedWindowRoot.activeSelf != showFogged)
            {
                foggedWindowRoot.SetActive(showFogged);
            }

            if (neonWindowRoot != null &&
                neonWindowRoot.activeSelf != showNeon)
            {
                neonWindowRoot.SetActive(showNeon);
            }

            _activeSegmentIndex = selection;
        }

        private void OnValidate()
        {
            foggedWindowSegmentIndex = Mathf.Max(
                0,
                foggedWindowSegmentIndex);
            neonWindowSegmentIndex = Mathf.Max(0, neonWindowSegmentIndex);
        }
    }
}
