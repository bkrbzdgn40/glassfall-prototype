using Glassfall.Gameplay.Droplet;
using UnityEngine;

namespace Glassfall.Presentation
{
    [DisallowMultipleComponent]
    public sealed class DropletCameraFeedback : MonoBehaviour
    {
        [SerializeField] private CameraFeedbackTarget target;

        private DropletMergeSystem _mergeSystem;
        private DropletSplitSystem _splitSystem;

        private void Awake()
        {
            CacheSources();
        }

        private void OnEnable()
        {
            CacheSources();

            if (_mergeSystem != null)
                _mergeSystem.DropletMerged += HandleCollect;

            if (_splitSystem != null)
            {
                _splitSystem.SplitPerformed += HandleSplit;
                _splitSystem.ReMergePerformed += HandleReMerge;
            }
        }

        private void OnDisable()
        {
            if (_mergeSystem != null)
                _mergeSystem.DropletMerged -= HandleCollect;

            if (_splitSystem != null)
            {
                _splitSystem.SplitPerformed -= HandleSplit;
                _splitSystem.ReMergePerformed -= HandleReMerge;
            }
        }

        private void HandleCollect(DropletMergeResult result)
        {
            target?.PlayCollect(result.ConsumedMass);
        }

        private void HandleSplit(SplitResult result)
        {
            target?.PlaySplit();
        }

        private void HandleReMerge(ReMergeResult result)
        {
            target?.PlayReMerge();
        }

        private void CacheSources()
        {
            if (_mergeSystem == null)
                _mergeSystem = GetComponent<DropletMergeSystem>();
            if (_splitSystem == null)
                _splitSystem = GetComponent<DropletSplitSystem>();

            if (target == null)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                    target = mainCamera.GetComponent<CameraFeedbackTarget>();
            }
        }
    }
}
