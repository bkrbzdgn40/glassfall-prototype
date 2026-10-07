namespace Glassfall.Gameplay.Run
{
    public enum MicroRunPhase
    {
        ChooseBuild = 0,
        ArmResonance = 1,
        Encounter = 2,
        RewardChoice = 3,
        Intermission = 4,
        Complete = 5,
        Preparation = 6,
        Recovery = 7,
        Failed = 8
    }

    public sealed class MicroRunProgress
    {
        public MicroRunProgress(int encounterCount)
        {
            EncounterCount = encounterCount < 1 ? 1 : encounterCount;
            Phase = MicroRunPhase.ChooseBuild;
            CurrentEncounter = 0;
        }

        public MicroRunPhase Phase { get; private set; }
        public int CurrentEncounter { get; private set; }
        public int EncounterCount { get; }

        public bool SelectBuild()
        {
            if (Phase != MicroRunPhase.ChooseBuild)
                return false;

            Phase = MicroRunPhase.Preparation;
            return true;
        }

        public bool CompletePreparation()
        {
            if (Phase != MicroRunPhase.Preparation)
                return false;

            Phase = MicroRunPhase.ArmResonance;
            return true;
        }

        public bool CompleteResonance()
        {
            if (Phase != MicroRunPhase.ArmResonance)
                return false;

            CurrentEncounter = 1;
            Phase = MicroRunPhase.Encounter;
            return true;
        }

        public bool DefeatEncounter()
        {
            if (Phase != MicroRunPhase.Encounter)
                return false;

            if (CurrentEncounter >= EncounterCount)
            {
                Phase = MicroRunPhase.Complete;
                return true;
            }

            Phase = MicroRunPhase.RewardChoice;
            return true;
        }

        public bool CompleteRewardChoice()
        {
            if (Phase != MicroRunPhase.RewardChoice)
                return false;

            Phase = MicroRunPhase.Recovery;
            return true;
        }

        public bool CompleteRecovery()
        {
            if (Phase != MicroRunPhase.Recovery)
                return false;

            Phase = MicroRunPhase.Intermission;
            return true;
        }

        public bool FinishIntermission()
        {
            if (Phase != MicroRunPhase.Intermission)
                return false;

            CurrentEncounter++;
            Phase = MicroRunPhase.Encounter;
            return true;
        }

        public bool FailRun()
        {
            if (Phase == MicroRunPhase.ChooseBuild ||
                Phase == MicroRunPhase.RewardChoice ||
                Phase == MicroRunPhase.Complete ||
                Phase == MicroRunPhase.Failed)
            {
                return false;
            }

            Phase = MicroRunPhase.Failed;
            return true;
        }
    }
}
