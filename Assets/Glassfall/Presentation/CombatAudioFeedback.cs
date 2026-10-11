using Glassfall.Gameplay.Combat;
using Glassfall.Gameplay.Rewards;
using Glassfall.Gameplay.Run;
using UnityEngine;

namespace Glassfall.Presentation
{
    [DisallowMultipleComponent]
    public sealed class CombatAudioFeedback : MonoBehaviour
    {
        [Header("Authoritative Runtime Sources")]
        [SerializeField] private SplitLinkCombat linkCombat;
        [SerializeField] private ArcHunter arcHunter;
        [SerializeField] private ContaminationSystem contaminationSystem;
        [SerializeField] private MidRunRewardController rewardController;
        [SerializeField] private MicroRunController runController;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource gameplayOneShotSource;
        [SerializeField] private AudioSource hunterOneShotSource;
        [SerializeField] private AudioSource conductionLoopSource;

        [Header("Provisional Clips")]
        [SerializeField] private AudioClip conductionLoopClip;
        [SerializeField] private AudioClip hunterTelegraphClip;
        [SerializeField] private AudioClip hunterCommitClip;
        [SerializeField] private AudioClip contaminationHitClip;
        [SerializeField] private AudioClip cleanseClip;
        [SerializeField] private AudioClip encounterClearClip;
        [SerializeField] private AudioClip rewardSelectClip;
        [SerializeField] private AudioClip windowStabilizedClip;

        [Header("Mix")]
        [Range(0f, 1f)]
        [SerializeField] private float conductionVolume = 0.16f;
        [Range(0f, 1f)]
        [SerializeField] private float telegraphVolume = 0.30f;
        [Range(0f, 1f)]
        [SerializeField] private float commitVolume = 0.36f;
        [Range(0f, 1f)]
        [SerializeField] private float contaminationHitVolume = 0.34f;
        [Range(0f, 1f)]
        [SerializeField] private float cleanseVolume = 0.24f;
        [Range(0f, 1f)]
        [SerializeField] private float encounterClearVolume = 0.28f;
        [Range(0f, 1f)]
        [SerializeField] private float rewardSelectVolume = 0.26f;
        [Range(0f, 1f)]
        [SerializeField] private float windowStabilizedVolume = 0.42f;

        [Header("Conduction Response")]
        [Min(0.01f)]
        [SerializeField] private float conductionFadeInSeconds = 0.09f;
        [Min(0.01f)]
        [SerializeField] private float conductionFadeOutSeconds = 0.18f;
        [Range(0.5f, 1.5f)]
        [SerializeField] private float conductionPitchLow = 0.94f;
        [Range(0.5f, 1.5f)]
        [SerializeField] private float conductionPitchHigh = 1.12f;

        [Header("One-shot Debounce")]
        [Min(0f)]
        [SerializeField] private float contaminationMinInterval = 0.08f;
        [Min(0f)]
        [SerializeField] private float cleanseMinInterval = 0.12f;

        [Header("Runtime Mirrors")]
        [SerializeField] private float debugConductionEnvelope;
        [SerializeField] private float debugConductionVolume;
        [SerializeField] private float debugConductionPitch = 1f;
        [SerializeField] private bool debugConductionActive;

        private ArcHunterCombatState _lastHunterState;
        private float _lastContaminationHitFlash01;
        private float _lastCleansePulse01;
        private float _lastRewardSelectionTime = -1f;
        private bool _wasEncounterCompletionBeat;
        private bool _wasWorldCompletionBeat;
        private bool _wasRunComplete;
        private float _lastContaminationSoundTime = -100f;
        private float _lastCleanseSoundTime = -100f;
        private float _conductionEnvelope;

        public void ConfigureReferences(
            SplitLinkCombat combat,
            ArcHunter hunter,
            ContaminationSystem contamination,
            MidRunRewardController rewards,
            MicroRunController run)
        {
            linkCombat = combat;
            arcHunter = hunter;
            contaminationSystem = contamination;
            rewardController = rewards;
            runController = run;
        }

        public void ConfigureAudioSources(
            AudioSource gameplaySource,
            AudioSource hunterSource,
            AudioSource loopSource)
        {
            gameplayOneShotSource = gameplaySource;
            hunterOneShotSource = hunterSource;
            conductionLoopSource = loopSource;
            ConfigureAudioSourceDefaults();
        }

        public void ConfigureClips(
            AudioClip conduction,
            AudioClip telegraph,
            AudioClip commit,
            AudioClip contaminationHit,
            AudioClip cleanse,
            AudioClip encounterClear,
            AudioClip rewardSelect,
            AudioClip windowStabilized)
        {
            conductionLoopClip = conduction;
            hunterTelegraphClip = telegraph;
            hunterCommitClip = commit;
            contaminationHitClip = contaminationHit;
            cleanseClip = cleanse;
            encounterClearClip = encounterClear;
            rewardSelectClip = rewardSelect;
            windowStabilizedClip = windowStabilized;

            if (conductionLoopSource != null)
                conductionLoopSource.clip = conductionLoopClip;
        }

        public void ConfigureMix(
            float newConductionVolume = 0.16f,
            float newTelegraphVolume = 0.30f,
            float newCommitVolume = 0.36f,
            float newContaminationHitVolume = 0.34f,
            float newCleanseVolume = 0.24f,
            float newEncounterClearVolume = 0.28f,
            float newRewardSelectVolume = 0.26f,
            float newWindowStabilizedVolume = 0.42f)
        {
            conductionVolume = Mathf.Clamp01(newConductionVolume);
            telegraphVolume = Mathf.Clamp01(newTelegraphVolume);
            commitVolume = Mathf.Clamp01(newCommitVolume);
            contaminationHitVolume = Mathf.Clamp01(newContaminationHitVolume);
            cleanseVolume = Mathf.Clamp01(newCleanseVolume);
            encounterClearVolume = Mathf.Clamp01(newEncounterClearVolume);
            rewardSelectVolume = Mathf.Clamp01(newRewardSelectVolume);
            windowStabilizedVolume = Mathf.Clamp01(newWindowStabilizedVolume);
        }

        public static float CalculateConductionIntensity(float conductivity01)
        {
            return Mathf.Lerp(
                0.52f,
                1f,
                Mathf.Sqrt(Mathf.Clamp01(conductivity01)));
        }

        public static float CalculateConductionPitch(
            float conductivity01,
            float lowPitch,
            float highPitch)
        {
            float low = Mathf.Min(lowPitch, highPitch);
            float high = Mathf.Max(lowPitch, highPitch);
            return Mathf.Lerp(low, high, Mathf.Clamp01(conductivity01));
        }

        private void Awake()
        {
            ConfigureAudioSourceDefaults();
            CaptureRuntimeBaseline();
        }

        private void OnEnable()
        {
            ConfigureAudioSourceDefaults();
            CaptureRuntimeBaseline();
        }

        private void OnDisable()
        {
            StopConductionLoop();
        }

        private void Update()
        {
            UpdateConductionLoop(Time.unscaledDeltaTime);
            UpdateHunterStateAudio();
            UpdateContaminationAudio();
            UpdateRewardAudio();
            UpdateRunAudio();
            UpdateDiagnostics();
        }

        private void UpdateConductionLoop(float unscaledDeltaTime)
        {
            bool applyingDamage = linkCombat != null &&
                linkCombat.IsApplyingLinkDamage &&
                linkCombat.CurrentAppliedOutputPerSecond > 0.001f;

            float targetEnvelope = applyingDamage ? 1f : 0f;
            float fadeSeconds = applyingDamage
                ? conductionFadeInSeconds
                : conductionFadeOutSeconds;
            _conductionEnvelope = Mathf.MoveTowards(
                _conductionEnvelope,
                targetEnvelope,
                unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds));

            if (conductionLoopSource == null || conductionLoopClip == null)
                return;

            if (conductionLoopSource.clip != conductionLoopClip)
                conductionLoopSource.clip = conductionLoopClip;

            if (_conductionEnvelope > 0.001f && !conductionLoopSource.isPlaying)
                conductionLoopSource.Play();

            float conductivity01 = linkCombat != null
                ? linkCombat.Conductivity01
                : 0f;
            float intensity = CalculateConductionIntensity(conductivity01);
            conductionLoopSource.volume =
                conductionVolume * intensity * _conductionEnvelope;
            conductionLoopSource.pitch = CalculateConductionPitch(
                conductivity01,
                conductionPitchLow,
                conductionPitchHigh);

            if (!applyingDamage &&
                _conductionEnvelope <= 0.001f &&
                conductionLoopSource.isPlaying)
            {
                conductionLoopSource.Stop();
            }
        }

        private void UpdateHunterStateAudio()
        {
            if (arcHunter == null)
                return;

            ArcHunterCombatState current = arcHunter.CombatState;

            if (!arcHunter.isActiveAndEnabled)
            {
                _lastHunterState = current;
                return;
            }

            if (current == _lastHunterState)
                return;

            if (current == ArcHunterCombatState.Telegraph)
            {
                PlayOneShot(
                    hunterOneShotSource,
                    hunterTelegraphClip,
                    telegraphVolume);
            }
            else if (current == ArcHunterCombatState.CommitAttack)
            {
                PlayOneShot(
                    hunterOneShotSource,
                    hunterCommitClip,
                    commitVolume);
            }

            _lastHunterState = current;
        }

        private void UpdateContaminationAudio()
        {
            if (contaminationSystem == null)
                return;

            float hitFlash = contaminationSystem.HitFlash01;
            float cleansePulse = contaminationSystem.CleansePulse01;
            float now = Time.unscaledTime;

            bool hitRetriggered =
                hitFlash > 0.80f &&
                (hitFlash > _lastContaminationHitFlash01 + 0.05f ||
                 _lastContaminationHitFlash01 <= 0.05f);
            if (hitRetriggered &&
                now - _lastContaminationSoundTime >=
                contaminationMinInterval)
            {
                PlayOneShot(
                    gameplayOneShotSource,
                    contaminationHitClip,
                    contaminationHitVolume);
                _lastContaminationSoundTime = now;
            }

            bool cleanseRetriggered =
                cleansePulse > 0.80f &&
                (cleansePulse > _lastCleansePulse01 + 0.05f ||
                 _lastCleansePulse01 <= 0.05f);
            if (cleanseRetriggered &&
                now - _lastCleanseSoundTime >= cleanseMinInterval)
            {
                PlayOneShot(
                    gameplayOneShotSource,
                    cleanseClip,
                    cleanseVolume);
                _lastCleanseSoundTime = now;
            }

            _lastContaminationHitFlash01 = hitFlash;
            _lastCleansePulse01 = cleansePulse;
        }

        private void UpdateRewardAudio()
        {
            if (rewardController == null)
                return;

            float selectedAt = rewardController.SelectedAtUnscaledTime;
            if (selectedAt >= 0f &&
                (_lastRewardSelectionTime < 0f ||
                 selectedAt > _lastRewardSelectionTime + 0.0001f))
            {
                PlayOneShot(
                    gameplayOneShotSource,
                    rewardSelectClip,
                    rewardSelectVolume);
            }

            _lastRewardSelectionTime = selectedAt;
        }

        private void UpdateRunAudio()
        {
            if (runController == null)
                return;

            bool isComplete = runController.IsComplete;
            bool completionBeat = runController.IsEncounterCompletionBeat;
            bool worldCompletionBeat = runController.IsWorldCompletionBeat;

            if (isComplete && !_wasRunComplete)
            {
                PlayOneShot(
                    gameplayOneShotSource,
                    windowStabilizedClip,
                    windowStabilizedVolume);
            }
            else if (worldCompletionBeat && !_wasWorldCompletionBeat)
            {
                PlayOneShot(
                    gameplayOneShotSource,
                    windowStabilizedClip,
                    windowStabilizedVolume);
            }
            else if (completionBeat && !_wasEncounterCompletionBeat)
            {
                PlayOneShot(
                    gameplayOneShotSource,
                    encounterClearClip,
                    encounterClearVolume);
            }

            _wasRunComplete = isComplete;
            _wasEncounterCompletionBeat = completionBeat;
            _wasWorldCompletionBeat = worldCompletionBeat;
        }

        private void PlayOneShot(
            AudioSource source,
            AudioClip clip,
            float volume)
        {
            if (source == null || clip == null || volume <= 0f)
                return;

            source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private void ConfigureAudioSourceDefaults()
        {
            ConfigureOneShotSource(gameplayOneShotSource);
            ConfigureOneShotSource(hunterOneShotSource);

            if (conductionLoopSource == null)
                return;

            conductionLoopSource.playOnAwake = false;
            conductionLoopSource.loop = true;
            conductionLoopSource.spatialBlend = 0f;
            conductionLoopSource.dopplerLevel = 0f;
            conductionLoopSource.volume = 0f;
            conductionLoopSource.pitch = 1f;

            if (conductionLoopClip != null)
                conductionLoopSource.clip = conductionLoopClip;
        }

        private static void ConfigureOneShotSource(AudioSource source)
        {
            if (source == null)
                return;

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = 1f;
            source.pitch = 1f;
        }

        private void CaptureRuntimeBaseline()
        {
            _lastHunterState = arcHunter != null
                ? arcHunter.CombatState
                : ArcHunterCombatState.Hunt;
            _lastContaminationHitFlash01 = contaminationSystem != null
                ? contaminationSystem.HitFlash01
                : 0f;
            _lastCleansePulse01 = contaminationSystem != null
                ? contaminationSystem.CleansePulse01
                : 0f;
            _lastRewardSelectionTime = rewardController != null
                ? rewardController.SelectedAtUnscaledTime
                : -1f;
            _wasEncounterCompletionBeat = runController != null &&
                runController.IsEncounterCompletionBeat;
            _wasWorldCompletionBeat = runController != null &&
                runController.IsWorldCompletionBeat;
            _wasRunComplete = runController != null &&
                runController.IsComplete;
            _conductionEnvelope = 0f;
            StopConductionLoop();
            UpdateDiagnostics();
        }

        private void StopConductionLoop()
        {
            _conductionEnvelope = 0f;

            if (conductionLoopSource == null)
                return;

            conductionLoopSource.volume = 0f;
            conductionLoopSource.pitch = 1f;
            if (conductionLoopSource.isPlaying)
                conductionLoopSource.Stop();
        }

        private void UpdateDiagnostics()
        {
            debugConductionEnvelope = _conductionEnvelope;
            debugConductionActive = linkCombat != null &&
                linkCombat.IsApplyingLinkDamage;
            debugConductionVolume = conductionLoopSource != null
                ? conductionLoopSource.volume
                : 0f;
            debugConductionPitch = conductionLoopSource != null
                ? conductionLoopSource.pitch
                : 1f;
        }

        private void OnValidate()
        {
            conductionVolume = Mathf.Clamp01(conductionVolume);
            telegraphVolume = Mathf.Clamp01(telegraphVolume);
            commitVolume = Mathf.Clamp01(commitVolume);
            contaminationHitVolume = Mathf.Clamp01(contaminationHitVolume);
            cleanseVolume = Mathf.Clamp01(cleanseVolume);
            encounterClearVolume = Mathf.Clamp01(encounterClearVolume);
            rewardSelectVolume = Mathf.Clamp01(rewardSelectVolume);
            windowStabilizedVolume = Mathf.Clamp01(windowStabilizedVolume);

            conductionFadeInSeconds = Mathf.Max(
                0.01f,
                conductionFadeInSeconds);
            conductionFadeOutSeconds = Mathf.Max(
                0.01f,
                conductionFadeOutSeconds);
            conductionPitchLow = Mathf.Clamp(
                conductionPitchLow,
                0.5f,
                1.5f);
            conductionPitchHigh = Mathf.Clamp(
                conductionPitchHigh,
                0.5f,
                1.5f);
            contaminationMinInterval = Mathf.Max(
                0f,
                contaminationMinInterval);
            cleanseMinInterval = Mathf.Max(
                0f,
                cleanseMinInterval);
        }
    }
}
