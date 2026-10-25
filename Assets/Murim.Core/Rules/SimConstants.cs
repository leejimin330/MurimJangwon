namespace Murim.Core.Rules
{
    /// <summary>시뮬레이션 수치 기준값. 밸런스 조정은 여기서만 한다.</summary>
    public static class SimConstants
    {
        // 인물
        public const int StatMin = 1;
        public const int StatMax = 20;
        public const int MaxRealm = 6;
        public const int AdultAge = 16;
        public const int MarriageMinAge = 16;
        public const int FertileMinAge = 16;
        public const int FertileMaxAge = 45;
        public const double MonthlyBirthChance = 0.01;
        /// <summary>세력 인원이 목표 규모 × 이 값을 넘으면 출생 확률이 비례해 줄어든다.</summary>
        public const double CrowdingFactor = 1.5;
        public const double MinBirthMultiplier = 0.1;
        public const double MaxBirthMultiplier = 1.5;
        public const double TraitInheritChance = 0.4;
        public const int MinTraits = 2;
        public const int MaxTraits = 4;

        // 수명: 연간 위험 = BaseHazard × exp(HazardGrowth × (나이 − 경지 × RealmLifespanYears))
        public const double BaseHazard = 0.0001;
        public const double HazardGrowth = 0.1;
        public const int RealmLifespanYears = 8;
        public const double MonthlyAccidentChance = 0.0001;
        public const int OldAgeThreshold = 55;

        // 경지 단순 상승 (1단계 임시 규칙)
        public const int RealmGrowthMinAge = 15;
        public const int RealmGrowthMaxAge = 60;
        public const double RealmGrowthDivisor = 200.0;

        // 호감도·기억
        public const int OpinionMin = -100;
        public const int OpinionMax = 100;
        public const int SameTraitOpinion = 10;
        public const int OppositeTraitOpinion = -10;
        public const int MemoryDecayPercent = 10;
        public const int MemoryMinStrength = 10;
        public const int MemoryInheritThreshold = 50;
        public const int NemesisThreshold = 60;
        public const int BenefactorThreshold = 40;

        // 세력
        public const int AlignmentShiftThreshold = 30;
        public const int RighteousStartChivalry = 50;
        public const int UnorthodoxStartChivalry = -50;
        public const int DemonicStartChivalry = -80;
        public const int ChivalryMin = -100;
        public const int ChivalryMax = 100;
        public const int ClanMarriageBonus = 15;
        public const int SameAlignmentMarriageBonus = 20;
        public const int OpposedAlignmentMarriagePenalty = -40;
        public const int MarriageOpinionGain = 20;
        public const int MarriageAllianceGain = 15;
        public const int MarriageRejectGrudge = 30;
        public const int AllianceThreshold = 30;
        public const int UnaffiliatedMarriageOpinion = 30;

        // 후계
        public const double DesertionChance = 0.25;
        public const int DesertionOpinion = -50;

        // 경제
        public const int InfluenceIncome = 2;
        public const int GuestStipend = 20;
        public const int DebtInterestPercent = 10;
        public const int DebtUnrestThreshold = 3000;
        public const int DebtUnrestOpinion = -10;
        public const int GuestToElderOpinion = 60;
    }
}
