#if UNITY_EDITOR || DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Glassfall.Diagnostics
{
    public sealed class RunQualityTelemetrySession
    {
        public readonly struct WorldRecord
        {
            public WorldRecord(
                string worldId,
                int worldIndex,
                double durationSeconds)
            {
                WorldId = worldId ?? string.Empty;
                WorldIndex = worldIndex;
                DurationSeconds = Math.Max(0d, durationSeconds);
            }

            public string WorldId { get; }
            public int WorldIndex { get; }
            public double DurationSeconds { get; }
        }

        public readonly struct EncounterRecord
        {
            public EncounterRecord(
                string worldId,
                int worldIndex,
                int globalEncounter,
                int worldEncounter,
                double durationSeconds,
                float contaminationStart,
                float contaminationEnd,
                float massStart,
                float massEnd)
            {
                WorldId = worldId ?? string.Empty;
                WorldIndex = worldIndex;
                GlobalEncounter = globalEncounter;
                WorldEncounter = worldEncounter;
                DurationSeconds = Math.Max(0d, durationSeconds);
                ContaminationStart = contaminationStart;
                ContaminationEnd = contaminationEnd;
                MassStart = massStart;
                MassEnd = massEnd;
            }

            public string WorldId { get; }
            public int WorldIndex { get; }
            public int GlobalEncounter { get; }
            public int WorldEncounter { get; }
            public double DurationSeconds { get; }
            public float ContaminationStart { get; }
            public float ContaminationEnd { get; }
            public float MassStart { get; }
            public float MassEnd { get; }
        }

        public readonly struct RewardRecord
        {
            public RewardRecord(
                int rewardStep,
                string selectedReward,
                string offeredRewards)
            {
                RewardStep = rewardStep;
                SelectedReward = selectedReward ?? string.Empty;
                OfferedRewards = offeredRewards ?? string.Empty;
            }

            public int RewardStep { get; }
            public string SelectedReward { get; }
            public string OfferedRewards { get; }
        }

        private readonly List<WorldRecord> _worlds =
            new List<WorldRecord>(2);
        private readonly List<EncounterRecord> _encounters =
            new List<EncounterRecord>(6);
        private readonly List<RewardRecord> _rewards =
            new List<RewardRecord>(6);

        private double _runStartedAt;
        private double _worldStartedAt;
        private string _activeWorldId = string.Empty;
        private int _activeWorldIndex = -1;
        private double _encounterStartedAt;
        private string _encounterWorldId = string.Empty;
        private int _encounterWorldIndex = -1;
        private int _activeGlobalEncounter;
        private int _activeWorldEncounter;
        private float _encounterContaminationStart;
        private float _encounterMassStart;
        private int _pendingRewardStep;
        private string _pendingRewardOffers = string.Empty;

        public RunQualityTelemetrySession(string runId)
        {
            RunId = string.IsNullOrWhiteSpace(runId)
                ? "local-run"
                : runId.Trim();
        }

        public string RunId { get; }
        public bool HasStarted { get; private set; }
        public bool IsFinalized { get; private set; }
        public string Outcome { get; private set; } = "InProgress";
        public string FailurePhase { get; private set; } = string.Empty;
        public double TotalDurationSeconds { get; private set; }
        public double MergedSeconds { get; private set; }
        public double SplitSeconds { get; private set; }
        public int SplitTransitionCount { get; private set; }
        public int MergeTransitionCount { get; private set; }
        public double LinkDamageActiveSeconds { get; private set; }
        public double ConductionActiveSeconds { get; private set; }
        public double IntegratedAppliedOutput { get; private set; }
        public float FinalContamination { get; private set; }
        public float FinalMass { get; private set; }
        public string FinalWorldId { get; private set; } = string.Empty;
        public int FinalGlobalEncounter { get; private set; }
        public IReadOnlyList<WorldRecord> Worlds => _worlds;
        public IReadOnlyList<EncounterRecord> Encounters => _encounters;
        public IReadOnlyList<RewardRecord> Rewards => _rewards;

        public bool Begin(
            double now,
            string worldId,
            int worldIndex)
        {
            if (HasStarted || IsFinalized)
                return false;

            HasStarted = true;
            _runStartedAt = now;
            BeginWorld(now, worldId, worldIndex);
            return true;
        }

        public void SampleUsage(
            double deltaSeconds,
            bool isSplit,
            bool isApplyingLinkDamage,
            bool isApplyingConductiveOutput,
            float appliedOutputPerSecond)
        {
            if (!HasStarted || IsFinalized)
                return;

            double delta = Math.Max(0d, deltaSeconds);
            if (isSplit)
                SplitSeconds += delta;
            else
                MergedSeconds += delta;

            if (isApplyingLinkDamage)
                LinkDamageActiveSeconds += delta;
            if (isApplyingConductiveOutput)
                ConductionActiveSeconds += delta;

            IntegratedAppliedOutput +=
                Math.Max(0f, appliedOutputPerSecond) * delta;
        }

        public void RecordSplitTransition()
        {
            if (HasStarted && !IsFinalized)
                SplitTransitionCount++;
        }

        public void RecordMergeTransition()
        {
            if (HasStarted && !IsFinalized)
                MergeTransitionCount++;
        }

        public void ObserveState(
            double now,
            string phase,
            string worldId,
            int worldIndex,
            int globalEncounter,
            int worldEncounter,
            float contamination,
            float mass)
        {
            if (!HasStarted || IsFinalized)
                return;

            if (worldIndex != _activeWorldIndex)
            {
                EndEncounter(now, contamination, mass);
                EndWorld(now);
                BeginWorld(now, worldId, worldIndex);
            }

            bool isEncounter = string.Equals(
                phase,
                "Encounter",
                StringComparison.Ordinal);
            if (_activeGlobalEncounter > 0 &&
                (!isEncounter || globalEncounter != _activeGlobalEncounter))
            {
                EndEncounter(now, contamination, mass);
            }

            if (isEncounter &&
                globalEncounter > 0 &&
                _activeGlobalEncounter == 0)
            {
                _encounterStartedAt = now;
                _encounterWorldId = worldId ?? string.Empty;
                _encounterWorldIndex = worldIndex;
                _activeGlobalEncounter = globalEncounter;
                _activeWorldEncounter = worldEncounter;
                _encounterContaminationStart = contamination;
                _encounterMassStart = mass;
            }
        }

        public void RecordRewardOffer(
            int rewardStep,
            string first,
            string second,
            string third)
        {
            if (!HasStarted || IsFinalized || rewardStep < 1)
                return;

            _pendingRewardStep = rewardStep;
            _pendingRewardOffers = JoinOffers(first, second, third);
        }

        public void RecordRewardSelection(
            int rewardStep,
            string selectedReward)
        {
            if (!HasStarted || IsFinalized || rewardStep < 1)
                return;

            string offers = rewardStep == _pendingRewardStep
                ? _pendingRewardOffers
                : string.Empty;
            _rewards.Add(new RewardRecord(
                rewardStep,
                selectedReward,
                offers));
            _pendingRewardStep = 0;
            _pendingRewardOffers = string.Empty;
        }

        public bool FinalizeRun(
            double now,
            string outcome,
            string failurePhase,
            string worldId,
            int globalEncounter,
            float contamination,
            float mass)
        {
            if (!HasStarted || IsFinalized)
                return false;

            EndEncounter(now, contamination, mass);
            EndWorld(now);
            IsFinalized = true;
            Outcome = string.IsNullOrWhiteSpace(outcome)
                ? "Unknown"
                : outcome.Trim();
            FailurePhase = failurePhase ?? string.Empty;
            TotalDurationSeconds = Math.Max(0d, now - _runStartedAt);
            FinalWorldId = worldId ?? string.Empty;
            FinalGlobalEncounter = globalEncounter;
            FinalContamination = contamination;
            FinalMass = mass;
            return true;
        }

        public void WriteCsv(StreamWriter writer)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            writer.WriteLine(
                "record_type,run_id,outcome,world_id,world_index," +
                "global_encounter,world_encounter,duration_s," +
                "contamination_start,contamination_end,mass_start,mass_end," +
                "merged_s,split_s,split_count,merge_count," +
                "link_damage_s,conduction_s,integrated_output," +
                "reward_step,selected_reward,offered_rewards,fail_phase");

            WriteRunRow(writer);
            for (int i = 0; i < _worlds.Count; i++)
                WriteWorldRow(writer, _worlds[i]);
            for (int i = 0; i < _encounters.Count; i++)
                WriteEncounterRow(writer, _encounters[i]);
            for (int i = 0; i < _rewards.Count; i++)
                WriteRewardRow(writer, _rewards[i]);
        }

        private void BeginWorld(double now, string worldId, int worldIndex)
        {
            _worldStartedAt = now;
            _activeWorldId = worldId ?? string.Empty;
            _activeWorldIndex = worldIndex;
        }

        private void EndWorld(double now)
        {
            if (_activeWorldIndex < 0)
                return;

            _worlds.Add(new WorldRecord(
                _activeWorldId,
                _activeWorldIndex,
                now - _worldStartedAt));
            _activeWorldId = string.Empty;
            _activeWorldIndex = -1;
        }

        private void EndEncounter(
            double now,
            float contamination,
            float mass)
        {
            if (_activeGlobalEncounter <= 0)
                return;

            _encounters.Add(new EncounterRecord(
                _encounterWorldId,
                _encounterWorldIndex,
                _activeGlobalEncounter,
                _activeWorldEncounter,
                now - _encounterStartedAt,
                _encounterContaminationStart,
                contamination,
                _encounterMassStart,
                mass));
            _activeGlobalEncounter = 0;
            _activeWorldEncounter = 0;
        }

        private void WriteRunRow(StreamWriter writer)
        {
            WriteRow(
                writer,
                "RUN",
                Outcome,
                FinalWorldId,
                string.Empty,
                FinalGlobalEncounter.ToString(CultureInfo.InvariantCulture),
                string.Empty,
                F(TotalDurationSeconds),
                string.Empty,
                F(FinalContamination),
                string.Empty,
                F(FinalMass),
                F(MergedSeconds),
                F(SplitSeconds),
                SplitTransitionCount.ToString(CultureInfo.InvariantCulture),
                MergeTransitionCount.ToString(CultureInfo.InvariantCulture),
                F(LinkDamageActiveSeconds),
                F(ConductionActiveSeconds),
                F(IntegratedAppliedOutput),
                string.Empty,
                string.Empty,
                string.Empty,
                FailurePhase);
        }

        private void WriteWorldRow(StreamWriter writer, WorldRecord value)
        {
            WriteRow(
                writer,
                "WORLD",
                string.Empty,
                value.WorldId,
                value.WorldIndex.ToString(CultureInfo.InvariantCulture),
                string.Empty,
                string.Empty,
                F(value.DurationSeconds),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);
        }

        private void WriteEncounterRow(
            StreamWriter writer,
            EncounterRecord value)
        {
            WriteRow(
                writer,
                "ENCOUNTER",
                string.Empty,
                value.WorldId,
                value.WorldIndex.ToString(CultureInfo.InvariantCulture),
                value.GlobalEncounter.ToString(CultureInfo.InvariantCulture),
                value.WorldEncounter.ToString(CultureInfo.InvariantCulture),
                F(value.DurationSeconds),
                F(value.ContaminationStart),
                F(value.ContaminationEnd),
                F(value.MassStart),
                F(value.MassEnd),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);
        }

        private void WriteRewardRow(StreamWriter writer, RewardRecord value)
        {
            WriteRow(
                writer,
                "REWARD",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                value.RewardStep.ToString(CultureInfo.InvariantCulture),
                value.SelectedReward,
                value.OfferedRewards,
                string.Empty);
        }

        private void WriteRow(
            StreamWriter writer,
            string recordType,
            string outcome,
            string worldId,
            string worldIndex,
            string globalEncounter,
            string worldEncounter,
            string duration,
            string contaminationStart,
            string contaminationEnd,
            string massStart,
            string massEnd,
            string merged,
            string split,
            string splitCount,
            string mergeCount,
            string linkDamage,
            string conduction,
            string output,
            string rewardStep,
            string selectedReward,
            string offeredRewards,
            string failPhase)
        {
            writer.WriteLine(string.Join(
                ",",
                EscapeCsv(recordType),
                EscapeCsv(RunId),
                EscapeCsv(outcome),
                EscapeCsv(worldId),
                worldIndex,
                globalEncounter,
                worldEncounter,
                duration,
                contaminationStart,
                contaminationEnd,
                massStart,
                massEnd,
                merged,
                split,
                splitCount,
                mergeCount,
                linkDamage,
                conduction,
                output,
                rewardStep,
                EscapeCsv(selectedReward),
                EscapeCsv(offeredRewards),
                EscapeCsv(failPhase)));
        }

        private static string JoinOffers(
            string first,
            string second,
            string third)
        {
            string result = first ?? string.Empty;
            if (!string.IsNullOrEmpty(second))
                result += "|" + second;
            if (!string.IsNullOrEmpty(third))
                result += "|" + third;
            return result;
        }

        private static string F(double value)
        {
            return value.ToString("F4", CultureInfo.InvariantCulture);
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
    }
}
#endif
