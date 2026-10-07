using Glassfall.Gameplay.Droplet;
using UnityEngine;

namespace Glassfall.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class DropletAudioFeedback : MonoBehaviour
    {
        [Header("Audio Clips")]
        [SerializeField] private AudioClip collectClip;
        [SerializeField] private AudioClip splitClip;
        [SerializeField] private AudioClip remergeClip;

        [Header("Volume")]
        [Range(0f, 1f)]
        [SerializeField] private float collectVolume = 0.36f;

        [Range(0f, 1f)]
        [SerializeField] private float splitVolume = 0.48f;

        [Range(0f, 1f)]
        [SerializeField] private float remergeVolume = 0.54f;

        [Header("Collect Pitch")]
        [Range(0.5f, 1.5f)]
        [SerializeField] private float collectPitchMin = 0.92f;

        [Range(0.5f, 1.5f)]
        [SerializeField] private float collectPitchMax = 1.08f;

        private AudioSource _audioSource;
        private DropletMergeSystem _mergeSystem;
        private DropletSplitSystem _splitSystem;
        private float _authoredPitch = 1f;

        private void Awake()
        {
            CacheSources();
            ConfigureAudioSource();
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

            if (_audioSource != null)
                _audioSource.pitch = _authoredPitch;
        }

        private void HandleCollect(DropletMergeResult result)
        {
            float pitch = GetCollectPitch(result.ConsumedMass);
            PlayOneShot(collectClip, collectVolume, pitch);
        }

        private void HandleSplit(SplitResult result)
        {
            PlayOneShot(splitClip, splitVolume, _authoredPitch);
        }

        private void HandleReMerge(ReMergeResult result)
        {
            PlayOneShot(remergeClip, remergeVolume, _authoredPitch);
        }

        private void PlayOneShot(
            AudioClip clip,
            float volume,
            float pitch)
        {
            if (_audioSource == null || clip == null || volume <= 0f)
                return;

            _audioSource.pitch = Mathf.Clamp(pitch, 0.5f, 1.5f);
            _audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private float GetCollectPitch(float mass)
        {
            float low = Mathf.Min(collectPitchMin, collectPitchMax);
            float high = Mathf.Max(collectPitchMin, collectPitchMax);
            float massPitch = Mathf.Pow(
                Mathf.Max(mass, 0.01f),
                -0.08f);
            return Mathf.Clamp(massPitch, low, high);
        }

        private void CacheSources()
        {
            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();
            if (_mergeSystem == null)
                _mergeSystem = GetComponent<DropletMergeSystem>();
            if (_splitSystem == null)
                _splitSystem = GetComponent<DropletSplitSystem>();
        }

        private void ConfigureAudioSource()
        {
            if (_audioSource == null)
                return;

            _authoredPitch = Mathf.Clamp(_audioSource.pitch, 0.5f, 1.5f);
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.spatialBlend = 0f;
            _audioSource.dopplerLevel = 0f;
        }

        private void OnValidate()
        {
            collectVolume = Mathf.Clamp01(collectVolume);
            splitVolume = Mathf.Clamp01(splitVolume);
            remergeVolume = Mathf.Clamp01(remergeVolume);

            collectPitchMin = Mathf.Clamp(collectPitchMin, 0.5f, 1.5f);
            collectPitchMax = Mathf.Clamp(collectPitchMax, 0.5f, 1.5f);
        }
    }
}
