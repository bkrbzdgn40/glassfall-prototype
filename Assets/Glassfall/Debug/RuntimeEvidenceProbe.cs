using Glassfall.Gameplay.Combat;
using Glassfall.Gameplay.Droplet;
using Glassfall.Gameplay.Rewards;
using Glassfall.Gameplay.Run;
using UnityEngine;

#if UNITY_EDITOR || DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.Profiling;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
#endif

namespace Glassfall.Diagnostics
{
    [DisallowMultipleComponent]
    public sealed class RuntimeEvidenceProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEBUG
        private const int DefaultFrameWindow = 600;
        private const string EvidenceFolderName = "M25_5E_Evidence";
        private const string AutomatedSmokeArgument =
            "-glassfall-evidence-smoke";
        private const float AutomatedSmokeRunSeconds = 2f;

        [Flags]
        public enum DeveloperControlState
        {
            None = 0,
            ToggleOverlay = 1 << 0,
            CaptureSnapshot = 1 << 1,
            ReloadScene = 1 << 2
        }

        [Header("Authoritative Runtime Source")]
        [SerializeField] private MicroRunController runController;

        private DropletSplitSystem _splitSystem;
        private SplitLinkCombat _linkCombat;
        private MidRunRewardController _rewardController;

        [Header("Capture")]
        [SerializeField] private bool enableBinaryProfilerLogInDevelopmentBuild = true;
        [SerializeField] private bool captureRunTransitions = true;
        [SerializeField] private int rollingFrameWindow = DefaultFrameWindow;

        [Header("Developer Controls")]
        [SerializeField] private bool showOverlayAtStartup;

        private static string _processCaptureId;
        private static int _sceneRunCounter;

        private readonly List<EvidenceSnapshot> _snapshots =
            new List<EvidenceSnapshot>(32);

        private float[] _frameMilliseconds;
        private int _frameSampleCount;
        private int _frameWriteIndex;
        private bool _showOverlay;
        private bool _binaryProfilerStarted;
        private bool _terminalSnapshotCaptured;
        private bool _isQuitting;
        private bool _sceneReloadRequested;
        private bool _automatedSmokeEnabled;
        private float _automatedSmokeActionAt;
        private bool _qualityReportWritten;

        private MicroRunPhase _lastPhase;
        private int _lastEncounter;
        private int _lastWorldSegmentIndex;
        private long _initialTotalAllocatedBytes;
        private long _initialMonoUsedBytes;
        private int _initialGc0;
        private int _initialGc1;
        private int _initialGc2;

        private ProfilerRecorder _mainThreadRecorder;
        private ProfilerRecorder _gcAllocatedInFrameRecorder;

        private string _sessionDirectory;
        private string _binaryProfilerPath;
        private int _sceneRunNumber;
        private RunQualityTelemetrySession _qualitySession;

        public string SessionDirectory => _sessionDirectory;
        public int SceneRunNumber => _sceneRunNumber;

        public void Configure(MicroRunController run)
        {
            runController = run;
        }

        public static float CalculatePercentileMilliseconds(
            float[] samples,
            int sampleCount,
            float percentile01)
        {
            if (samples == null || samples.Length == 0 || sampleCount <= 0)
                return 0f;

            int count = Mathf.Clamp(sampleCount, 1, samples.Length);
            float[] copy = new float[count];
            Array.Copy(samples, copy, count);
            Array.Sort(copy);

            float p = Mathf.Clamp01(percentile01);
            int index = Mathf.Clamp(
                Mathf.CeilToInt((count - 1) * p),
                0,
                count - 1);
            return copy[index];
        }

        public static DeveloperControlState ResolveDeveloperControls(
            bool overlayPressed,
            bool snapshotPressed,
            bool reloadPressed)
        {
            DeveloperControlState state = DeveloperControlState.None;
            if (overlayPressed)
                state |= DeveloperControlState.ToggleOverlay;
            if (snapshotPressed)
                state |= DeveloperControlState.CaptureSnapshot;
            if (reloadPressed)
                state |= DeveloperControlState.ReloadScene;
            return state;
        }

        public static int CalculateNextSceneRunNumber(int currentRunNumber)
        {
            return Mathf.Max(0, currentRunNumber) + 1;
        }

        public static bool IsQualityTelemetryEnabledFor(
            bool isEditor,
            bool isDebugBuild)
        {
            return isEditor || isDebugBuild;
        }

        private void Awake()
        {
            if (runController == null)
                runController = GetComponent<MicroRunController>();
            if (runController == null)
                runController = FindAnyObjectByType<MicroRunController>();

            rollingFrameWindow = Mathf.Clamp(
                rollingFrameWindow,
                120,
                3600);
            _frameMilliseconds = new float[rollingFrameWindow];

            EnsureProcessCaptureId();
            _sceneRunCounter = CalculateNextSceneRunNumber(_sceneRunCounter);
            _sceneRunNumber = _sceneRunCounter;
            _sessionDirectory = Path.Combine(
                Application.persistentDataPath,
                EvidenceFolderName,
                _processCaptureId);
            Directory.CreateDirectory(_sessionDirectory);
            _qualitySession = new RunQualityTelemetrySession(
                _processCaptureId + "-run-" +
                _sceneRunNumber.ToString(
                    "00",
                    CultureInfo.InvariantCulture));
            ResolveQualitySources();
            SubscribeQualitySources();

            _showOverlay = showOverlayAtStartup;
            _automatedSmokeEnabled = HasCommandLineArgument(
                AutomatedSmokeArgument);
            _automatedSmokeActionAt =
                Time.unscaledTime + AutomatedSmokeRunSeconds;
            CaptureMemoryBaseline();
            CaptureRunBaseline();
            StartRecorders();
            StartBinaryProfilerLogIfEligible();
            CaptureSnapshot("scene_start");
        }

        private void Update()
        {
            SampleFrameTime();

            DeveloperControlState controls = ReadDeveloperControls();
            if ((controls & DeveloperControlState.ToggleOverlay) != 0)
                _showOverlay = !_showOverlay;

            if ((controls & DeveloperControlState.CaptureSnapshot) != 0)
            {
                CaptureSnapshot("manual");
                WriteReport();
            }

            if ((controls & DeveloperControlState.ReloadScene) != 0)
            {
                ReloadActiveScene();
                return;
            }

            if (captureRunTransitions)
                CaptureTransitionsIfNeeded();

            UpdateRunQualityTelemetry();

            if (runController != null &&
                (runController.IsComplete || runController.IsFailed) &&
                !_terminalSnapshotCaptured)
            {
                _terminalSnapshotCaptured = true;
                CaptureSnapshot(
                    runController.IsComplete
                        ? "run_complete"
                        : "run_failed");
                WriteReport();
                FinalizeRunQualityTelemetry(
                    runController.IsComplete ? "Complete" : "Failed");
            }

            if (_automatedSmokeEnabled &&
                Time.unscaledTime >= _automatedSmokeActionAt)
            {
                if (_sceneRunNumber < 3)
                {
                    ReloadActiveScene();
                }
                else
                {
                    CaptureSnapshot("automated_smoke_complete");
                    WriteReport();
                    StopBinaryProfilerLog();
                    Application.Quit();
                }
            }
        }

        private void ReloadActiveScene()
        {
            _sceneReloadRequested = true;
            CaptureSnapshot("before_scene_reload");
            WriteReport();
            StopBinaryProfilerLog();

            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid())
                SceneManager.LoadScene(active.buildIndex);
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
            FinalizeRunQualityTelemetry("Incomplete");
            CaptureSnapshot("application_quit");
            WriteReport();
            StopBinaryProfilerLog();
        }

        private void OnDestroy()
        {
            if (!_isQuitting && !_sceneReloadRequested)
                FinalizeRunQualityTelemetry("Incomplete");

            if (!_isQuitting && !_sceneReloadRequested)
            {
                CaptureSnapshot("probe_destroyed");
                WriteReport();
            }

            StopBinaryProfilerLog();
            DisposeRecorders();
            UnsubscribeQualitySources();
        }

        private static DeveloperControlState ReadDeveloperControls()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return DeveloperControlState.None;

            return ResolveDeveloperControls(
                keyboard.f8Key.wasPressedThisFrame,
                keyboard.f9Key.wasPressedThisFrame,
                keyboard.f10Key.wasPressedThisFrame);
        }

        private static bool HasCommandLineArgument(string expected)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length; i++)
            {
                if (string.Equals(
                        arguments[i],
                        expected,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void CaptureRunBaseline()
        {
            if (runController == null)
            {
                _lastPhase = MicroRunPhase.ChooseBuild;
                _lastEncounter = 0;
                _lastWorldSegmentIndex = 0;
                return;
            }

            _lastPhase = runController.Phase;
            _lastEncounter = runController.CurrentEncounter;
            _lastWorldSegmentIndex = runController.CurrentWorldSegmentIndex;
        }

        private void CaptureMemoryBaseline()
        {
            _initialTotalAllocatedBytes =
                Profiler.GetTotalAllocatedMemoryLong();
            _initialMonoUsedBytes =
                Profiler.GetMonoUsedSizeLong();
            _initialGc0 = GC.CollectionCount(0);
            _initialGc1 = GC.CollectionCount(1);
            _initialGc2 = GC.CollectionCount(2);
        }

        private void ResolveQualitySources()
        {
            if (_splitSystem == null)
                _splitSystem = FindAnyObjectByType<DropletSplitSystem>();
            if (_linkCombat == null)
                _linkCombat = FindAnyObjectByType<SplitLinkCombat>();
            if (_rewardController == null)
            {
                _rewardController =
                    FindAnyObjectByType<MidRunRewardController>();
            }
        }

        private void SubscribeQualitySources()
        {
            if (_splitSystem != null)
            {
                _splitSystem.SplitPerformed += OnSplitPerformed;
                _splitSystem.ReMergePerformed += OnReMergePerformed;
            }

            if (_rewardController != null)
            {
                _rewardController.RewardChoiceOffered +=
                    OnRewardChoiceOffered;
                _rewardController.RewardSelected += OnRewardSelected;
            }
        }

        private void UnsubscribeQualitySources()
        {
            if (_splitSystem != null)
            {
                _splitSystem.SplitPerformed -= OnSplitPerformed;
                _splitSystem.ReMergePerformed -= OnReMergePerformed;
            }

            if (_rewardController != null)
            {
                _rewardController.RewardChoiceOffered -=
                    OnRewardChoiceOffered;
                _rewardController.RewardSelected -= OnRewardSelected;
            }
        }

        private void UpdateRunQualityTelemetry()
        {
            if (_qualitySession == null || runController == null)
                return;

            double now = Time.realtimeSinceStartupAsDouble;
            if (!_qualitySession.HasStarted)
            {
                if (runController.Phase == MicroRunPhase.ChooseBuild)
                    return;

                _qualitySession.Begin(
                    now,
                    runController.CurrentWorldId,
                    runController.CurrentWorldSegmentIndex);
            }

            _qualitySession.ObserveState(
                now,
                runController.Phase.ToString(),
                runController.CurrentWorldId,
                runController.CurrentWorldSegmentIndex,
                runController.CurrentEncounter,
                runController.CurrentWorldEncounter,
                runController.Contamination01,
                runController.CurrentCombinedMass);

            bool applyingLinkDamage = _linkCombat != null &&
                _linkCombat.IsApplyingLinkDamage;
            bool applyingConduction = applyingLinkDamage &&
                _linkCombat.Conductivity01 > 0.001f;
            _qualitySession.SampleUsage(
                Time.unscaledDeltaTime,
                _splitSystem != null && _splitSystem.IsSplit,
                applyingLinkDamage,
                applyingConduction,
                _linkCombat != null
                    ? _linkCombat.CurrentAppliedOutputPerSecond
                    : 0f);
        }

        private void FinalizeRunQualityTelemetry(string outcome)
        {
            if (_qualitySession == null ||
                !_qualitySession.HasStarted ||
                _qualitySession.IsFinalized)
            {
                return;
            }

            string failurePhase = runController != null &&
                                  runController.IsFailed
                ? runController.FailedFromPhase.ToString()
                : string.Empty;
            _qualitySession.FinalizeRun(
                Time.realtimeSinceStartupAsDouble,
                outcome,
                failurePhase,
                runController != null
                    ? runController.CurrentWorldId
                    : string.Empty,
                runController != null
                    ? runController.CurrentEncounter
                    : 0,
                runController != null
                    ? runController.Contamination01
                    : 0f,
                runController != null
                    ? runController.CurrentCombinedMass
                    : 0f);
            WriteRunQualityReport();
        }

        private void OnSplitPerformed(SplitResult result)
        {
            _qualitySession?.RecordSplitTransition();
        }

        private void OnReMergePerformed(ReMergeResult result)
        {
            _qualitySession?.RecordMergeTransition();
        }

        private void OnRewardChoiceOffered(
            int rewardStep,
            MidRunReward first,
            MidRunReward second,
            MidRunReward third)
        {
            _qualitySession?.RecordRewardOffer(
                rewardStep,
                RewardId(first),
                RewardId(second),
                RewardId(third));
        }

        private void OnRewardSelected(
            int rewardStep,
            MidRunReward selected)
        {
            _qualitySession?.RecordRewardSelection(
                rewardStep,
                RewardId(selected));
        }

        private static string RewardId(MidRunReward reward)
        {
            return reward == MidRunReward.None
                ? string.Empty
                : reward.ToString();
        }

        private void WriteRunQualityReport()
        {
            if (_qualityReportWritten ||
                _qualitySession == null ||
                !_qualitySession.IsFinalized ||
                string.IsNullOrWhiteSpace(_sessionDirectory))
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(_sessionDirectory);
                string qualityPath = Path.Combine(
                    _sessionDirectory,
                    $"run_{_sceneRunNumber:00}_quality.csv");
                WriteTextFileAtomically(
                    qualityPath,
                    _qualitySession.WriteCsv);
                _qualityReportWritten = true;
                Debug.Log(
                    "[Glassfall M27] Run quality telemetry written to: " +
                    qualityPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[Glassfall M27] Run quality telemetry could not be " +
                    "written.\n" + exception);
            }
        }

        private void StartRecorders()
        {
            try
            {
                _mainThreadRecorder = ProfilerRecorder.StartNew(
                    ProfilerCategory.Internal,
                    "Main Thread",
                    15);
            }
            catch
            {
                _mainThreadRecorder = default;
            }

            try
            {
                _gcAllocatedInFrameRecorder = ProfilerRecorder.StartNew(
                    ProfilerCategory.Memory,
                    "GC Allocated In Frame",
                    15);
            }
            catch
            {
                _gcAllocatedInFrameRecorder = default;
            }
        }

        private void DisposeRecorders()
        {
            if (_mainThreadRecorder.Valid)
                _mainThreadRecorder.Dispose();
            if (_gcAllocatedInFrameRecorder.Valid)
                _gcAllocatedInFrameRecorder.Dispose();
        }

        private void StartBinaryProfilerLogIfEligible()
        {
            if (!enableBinaryProfilerLogInDevelopmentBuild ||
                Application.isEditor ||
                !Debug.isDebugBuild)
            {
                return;
            }

            try
            {
                _binaryProfilerPath = Path.Combine(
                    _sessionDirectory,
                    $"run_{_sceneRunNumber:00}_profiler.raw");

                Profiler.logFile = _binaryProfilerPath;
                Profiler.enableBinaryLog = true;
                Profiler.enabled = true;
                _binaryProfilerStarted = true;

                Debug.Log(
                    "[Glassfall M25.5E] Binary profiler capture: " +
                    _binaryProfilerPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[Glassfall M25.5E] Binary profiler capture could not " +
                    "start. Runtime CSV evidence remains available.\n" +
                    exception.Message);
                _binaryProfilerStarted = false;
            }
        }

        private void StopBinaryProfilerLog()
        {
            if (!_binaryProfilerStarted)
                return;

            try
            {
                Profiler.enabled = false;
                Profiler.enableBinaryLog = false;
                Profiler.logFile = string.Empty;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[Glassfall M25.5E] Could not close binary profiler log: " +
                    exception.Message);
            }

            _binaryProfilerStarted = false;
        }

        private void SampleFrameTime()
        {
            if (_frameMilliseconds == null ||
                _frameMilliseconds.Length == 0)
            {
                return;
            }

            float milliseconds = Time.unscaledDeltaTime * 1000f;
            _frameMilliseconds[_frameWriteIndex] = milliseconds;
            _frameWriteIndex =
                (_frameWriteIndex + 1) % _frameMilliseconds.Length;
            _frameSampleCount = Mathf.Min(
                _frameSampleCount + 1,
                _frameMilliseconds.Length);
        }

        private void CaptureTransitionsIfNeeded()
        {
            if (runController == null)
                return;

            MicroRunPhase phase = runController.Phase;
            int encounter = runController.CurrentEncounter;
            int world = runController.CurrentWorldSegmentIndex;

            if (phase == _lastPhase &&
                encounter == _lastEncounter &&
                world == _lastWorldSegmentIndex)
            {
                return;
            }

            string reason =
                $"transition_{_lastPhase}_to_{phase}" +
                $"_w{world + 1}_e{encounter}";
            CaptureSnapshot(reason);

            _lastPhase = phase;
            _lastEncounter = encounter;
            _lastWorldSegmentIndex = world;
        }

        private void CaptureSnapshot(string reason)
        {
            EvidenceSnapshot snapshot = new EvidenceSnapshot
            {
                reason = reason ?? string.Empty,
                realtimeSeconds = Time.realtimeSinceStartupAsDouble,
                runElapsedSeconds = runController != null
                    ? runController.ElapsedSeconds
                    : 0f,
                phase = runController != null
                    ? runController.Phase.ToString()
                    : "NO_RUN",
                worldId = runController != null
                    ? runController.CurrentWorldId
                    : "NO_WORLD",
                worldSegmentIndex = runController != null
                    ? runController.CurrentWorldSegmentIndex
                    : 0,
                encounter = runController != null
                    ? runController.CurrentEncounter
                    : 0,
                worldEncounter = runController != null
                    ? runController.CurrentWorldEncounter
                    : 0,
                contamination01 = runController != null
                    ? runController.Contamination01
                    : 0f,
                frameAverageMs = CalculateAverageFrameMilliseconds(),
                frameP95Ms = CalculatePercentileMilliseconds(
                    GetChronologicalFrameSamples(),
                    _frameSampleCount,
                    0.95f),
                mainThreadMs = ReadRecorderMilliseconds(
                    _mainThreadRecorder),
                gcAllocatedInFrameBytes = ReadRecorderBytes(
                    _gcAllocatedInFrameRecorder),
                totalAllocatedBytes =
                    Profiler.GetTotalAllocatedMemoryLong(),
                totalReservedBytes =
                    Profiler.GetTotalReservedMemoryLong(),
                monoUsedBytes =
                    Profiler.GetMonoUsedSizeLong(),
                monoHeapBytes =
                    Profiler.GetMonoHeapSizeLong(),
                gc0Collections = GC.CollectionCount(0),
                gc1Collections = GC.CollectionCount(1),
                gc2Collections = GC.CollectionCount(2)
            };

            _snapshots.Add(snapshot);
        }

        private float[] GetChronologicalFrameSamples()
        {
            if (_frameMilliseconds == null || _frameSampleCount <= 0)
                return Array.Empty<float>();

            float[] result = new float[_frameSampleCount];
            int start = _frameSampleCount < _frameMilliseconds.Length
                ? 0
                : _frameWriteIndex;

            for (int i = 0; i < _frameSampleCount; i++)
            {
                result[i] = _frameMilliseconds[
                    (start + i) % _frameMilliseconds.Length];
            }

            return result;
        }

        private float CalculateAverageFrameMilliseconds()
        {
            if (_frameMilliseconds == null || _frameSampleCount <= 0)
                return 0f;

            double sum = 0d;
            if (_frameSampleCount < _frameMilliseconds.Length)
            {
                for (int i = 0; i < _frameSampleCount; i++)
                    sum += _frameMilliseconds[i];
            }
            else
            {
                for (int i = 0; i < _frameMilliseconds.Length; i++)
                    sum += _frameMilliseconds[i];
            }

            return (float)(sum / _frameSampleCount);
        }

        private static float ReadRecorderMilliseconds(
            ProfilerRecorder recorder)
        {
            if (!recorder.Valid || recorder.Count <= 0)
                return -1f;

            return (float)(recorder.LastValue / 1_000_000.0);
        }

        private static long ReadRecorderBytes(ProfilerRecorder recorder)
        {
            if (!recorder.Valid || recorder.Count <= 0)
                return -1L;

            return recorder.LastValue;
        }

        private void WriteReport()
        {
            if (string.IsNullOrWhiteSpace(_sessionDirectory))
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(_sessionDirectory);

                string baseName = $"run_{_sceneRunNumber:00}";
                string csvPath = Path.Combine(
                    _sessionDirectory,
                    baseName + "_summary.csv");
                string metadataPath = Path.Combine(
                    _sessionDirectory,
                    baseName + "_metadata.txt");

                WriteTextFileAtomically(csvPath, writer =>
                {
                    writer.WriteLine(
                        "reason,realtime_s,run_elapsed_s,phase,world_id," +
                        "world_segment,encounter,world_encounter," +
                        "contamination_01,frame_avg_ms,frame_p95_ms," +
                        "main_thread_ms,gc_alloc_frame_bytes," +
                        "total_allocated_bytes,total_reserved_bytes," +
                        "mono_used_bytes,mono_heap_bytes,gc0,gc1,gc2");

                    for (int i = 0; i < _snapshots.Count; i++)
                        writer.WriteLine(ToCsv(_snapshots[i]));
                });

                WriteTextFileAtomically(metadataPath, writer =>
                {
                    writer.WriteLine(
                        "Glassfall M25.5E Runtime Evidence");
                    writer.WriteLine(
                        $"process_capture_id={_processCaptureId}");
                    writer.WriteLine(
                        $"scene_run={_sceneRunNumber}");
                    writer.WriteLine(
                        $"unity_version={Application.unityVersion}");
                    writer.WriteLine(
                        $"app_version={Application.version}");
                    writer.WriteLine(
                        $"platform={Application.platform}");
                    writer.WriteLine(
                        $"is_editor={Application.isEditor}");
                    writer.WriteLine(
                        $"is_debug_build={Debug.isDebugBuild}");
                    writer.WriteLine(
                        $"graphics_device={SystemInfo.graphicsDeviceName}");
                    writer.WriteLine(
                        $"graphics_api={SystemInfo.graphicsDeviceType}");
                    writer.WriteLine(
                        $"graphics_memory_mb={SystemInfo.graphicsMemorySize}");
                    writer.WriteLine(
                        $"system_memory_mb={SystemInfo.systemMemorySize}");
                    writer.WriteLine(
                        $"processor={SystemInfo.processorType}");
                    writer.WriteLine(
                        $"processor_count={SystemInfo.processorCount}");
                    writer.WriteLine(
                        $"screen={Screen.width}x{Screen.height}");
                    writer.WriteLine(
                        $"refresh_rate={Screen.currentResolution.refreshRateRatio.value.ToString("F3", CultureInfo.InvariantCulture)}");
                    writer.WriteLine(
                        $"quality_level={QualitySettings.GetQualityLevel()}");
                    writer.WriteLine(
                        $"v_sync_count={QualitySettings.vSyncCount}");
                    writer.WriteLine(
                        $"target_frame_rate={Application.targetFrameRate}");
                    writer.WriteLine(
                        $"initial_total_allocated_bytes={_initialTotalAllocatedBytes}");
                    writer.WriteLine(
                        $"final_total_allocated_bytes={Profiler.GetTotalAllocatedMemoryLong()}");
                    writer.WriteLine(
                        $"initial_mono_used_bytes={_initialMonoUsedBytes}");
                    writer.WriteLine(
                        $"final_mono_used_bytes={Profiler.GetMonoUsedSizeLong()}");
                    writer.WriteLine(
                        $"gc0_delta={GC.CollectionCount(0) - _initialGc0}");
                    writer.WriteLine(
                        $"gc1_delta={GC.CollectionCount(1) - _initialGc1}");
                    writer.WriteLine(
                        $"gc2_delta={GC.CollectionCount(2) - _initialGc2}");
                    writer.WriteLine(
                        $"binary_profiler_requested={enableBinaryProfilerLogInDevelopmentBuild}");
                    writer.WriteLine(
                        $"binary_profiler_path={_binaryProfilerPath ?? string.Empty}");
                });

                Debug.Log(
                    "[Glassfall M25.5E] Runtime evidence written to: " +
                    _sessionDirectory);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[Glassfall M25.5E] Runtime evidence report could not " +
                    "be written.\n" + exception);
            }
        }

        private static void WriteTextFileAtomically(
            string destinationPath,
            Action<StreamWriter> writeContents)
        {
            string temporaryPath = destinationPath + ".tmp";
            try
            {
                using (StreamWriter writer = new StreamWriter(
                    temporaryPath,
                    false))
                {
                    writeContents(writer);
                }

                if (File.Exists(destinationPath))
                    File.Delete(destinationPath);
                File.Move(temporaryPath, destinationPath);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private static string ToCsv(EvidenceSnapshot value)
        {
            return string.Join(
                ",",
                EscapeCsv(value.reason),
                F(value.realtimeSeconds),
                F(value.runElapsedSeconds),
                EscapeCsv(value.phase),
                EscapeCsv(value.worldId),
                value.worldSegmentIndex.ToString(
                    CultureInfo.InvariantCulture),
                value.encounter.ToString(
                    CultureInfo.InvariantCulture),
                value.worldEncounter.ToString(
                    CultureInfo.InvariantCulture),
                F(value.contamination01),
                F(value.frameAverageMs),
                F(value.frameP95Ms),
                F(value.mainThreadMs),
                value.gcAllocatedInFrameBytes.ToString(
                    CultureInfo.InvariantCulture),
                value.totalAllocatedBytes.ToString(
                    CultureInfo.InvariantCulture),
                value.totalReservedBytes.ToString(
                    CultureInfo.InvariantCulture),
                value.monoUsedBytes.ToString(
                    CultureInfo.InvariantCulture),
                value.monoHeapBytes.ToString(
                    CultureInfo.InvariantCulture),
                value.gc0Collections.ToString(
                    CultureInfo.InvariantCulture),
                value.gc1Collections.ToString(
                    CultureInfo.InvariantCulture),
                value.gc2Collections.ToString(
                    CultureInfo.InvariantCulture));
        }

        private static string F(double value)
        {
            return value.ToString(
                "F4",
                CultureInfo.InvariantCulture);
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (!value.Contains(",") &&
                !value.Contains("\"") &&
                !value.Contains("\n"))
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static bool IsDeveloperRuntime()
        {
            return Application.isEditor || Debug.isDebugBuild;
        }

        private static void EnsureProcessCaptureId()
        {
            if (!string.IsNullOrEmpty(_processCaptureId))
                return;

            _processCaptureId =
                DateTime.Now.ToString(
                    "yyyyMMdd_HHmmss",
                    CultureInfo.InvariantCulture);
        }

        private void OnGUI()
        {
            if (!_showOverlay || !IsDeveloperRuntime())
                return;

            float avg = CalculateAverageFrameMilliseconds();
            float p95 = CalculatePercentileMilliseconds(
                GetChronologicalFrameSamples(),
                _frameSampleCount,
                0.95f);
            float mainThread = ReadRecorderMilliseconds(
                _mainThreadRecorder);
            long gcFrame = ReadRecorderBytes(
                _gcAllocatedInFrameRecorder);

            string runState = runController != null
                ? $"{runController.CurrentWorldId} | " +
                  $"{runController.Phase} | " +
                  $"E {runController.CurrentWorldEncounter}/" +
                  $"{runController.CurrentWorldEncounterCount}"
                : "NO RUN SOURCE";

            string text =
                "M25.5E RUNTIME EVIDENCE\n" +
                runState + "\n" +
                $"Frame avg {avg:F2} ms | p95 {p95:F2} ms\n" +
                $"Main Thread {(mainThread >= 0f ? mainThread.ToString("F2") : "n/a")} ms\n" +
                $"GC Alloc/frame {(gcFrame >= 0L ? FormatBytes(gcFrame) : "n/a")}\n" +
                $"Allocated {FormatBytes(Profiler.GetTotalAllocatedMemoryLong())} | " +
                $"Mono {FormatBytes(Profiler.GetMonoUsedSizeLong())}\n" +
                $"Run #{_sceneRunNumber} | F8 hide | F9 snapshot | F10 reload scene\n" +
                "Keep overlay OFF for clean profiler measurements.";

            GUI.Box(new Rect(12f, 12f, 430f, 142f), text);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 0L)
                return "n/a";

            const double megabyte = 1024d * 1024d;
            return (bytes / megabyte).ToString(
                "F2",
                CultureInfo.InvariantCulture) + " MB";
        }

        private void OnValidate()
        {
            rollingFrameWindow = Mathf.Clamp(
                rollingFrameWindow,
                120,
                3600);
        }

        private struct EvidenceSnapshot
        {
            public string reason;
            public double realtimeSeconds;
            public float runElapsedSeconds;
            public string phase;
            public string worldId;
            public int worldSegmentIndex;
            public int encounter;
            public int worldEncounter;
            public float contamination01;
            public float frameAverageMs;
            public float frameP95Ms;
            public float mainThreadMs;
            public long gcAllocatedInFrameBytes;
            public long totalAllocatedBytes;
            public long totalReservedBytes;
            public long monoUsedBytes;
            public long monoHeapBytes;
            public int gc0Collections;
            public int gc1Collections;
            public int gc2Collections;
        }
#else
        public void Configure(MicroRunController run)
        {
        }

        private void Awake()
        {
            enabled = false;
        }
#endif
    }
}
