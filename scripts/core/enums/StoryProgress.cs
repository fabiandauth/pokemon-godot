namespace Game.Core;

// Keep story milestones ordered. Gaps deliberately leave room for smaller beats
// without renumbering save data when the story grows.
public enum StoryProgress
{
    WokeUpLate = 0,
    MotherExplainedMissedMeeting = 10,
    MetProfessorPokebart = 20,
    ProfessorSuggestedFlowers = 30,
    FirstPokemonReceived = 40,

    LeftCoveTown = 100,
    ReachedFirstRoute = 110,
    WonFirstTrainerBattle = 120,
    ReachedLakeTown = 130,
    MetLakeTownLeader = 140,
    EarnedFirstBadge = 150,

    EnteredMountainPass = 200,
    FoundMountainCamp = 210,
    HelpedMountainRanger = 220,
    ReachedMountainSummit = 230,
    DefeatedMountainRival = 240,
    EarnedSecondBadge = 250,

    EnteredForestRegion = 300,
    FoundForestShrine = 310,
    MetForestGuardian = 320,
    RestoredForestShrine = 330,
    DefeatedForestThreat = 340,
    EarnedThirdBadge = 350,

    ReachedHarborCity = 400,
    BoardedResearchVessel = 410,
    InvestigatedSeaDisturbance = 420,
    RescuedResearchCrew = 430,
    ReturnedToHarbor = 440,
    EarnedFourthBadge = 450,

    ReachedDesertOutpost = 500,
    CrossedDesert = 510,
    FoundAncientRuins = 520,
    SolvedRuinsMystery = 530,
    StoppedRuinsRaiders = 540,
    EarnedFifthBadge = 550,

    ReachedNorthernCity = 600,
    InvestigatedPowerFailure = 610,
    EnteredPowerPlant = 620,
    RestoredRegionalPower = 630,
    DefeatedNorthernRival = 640,
    EarnedSixthBadge = 650,

    ReachedVolcanoIsland = 700,
    EnteredVolcanoLab = 710,
    RecoveredLostResearch = 720,
    CalmedVolcanoPokemon = 730,
    EscapedVolcano = 740,
    EarnedSeventhBadge = 750,

    ReturnedToCoveTown = 800,
    LearnedPokebartsSecret = 810,
    EnteredFinalHideout = 820,
    DefeatedRivalFinale = 830,
    SavedTheRegion = 840,
    EarnedEighthBadge = 850,

    EnteredPokemonLeague = 900,
    DefeatedEliteOne = 910,
    DefeatedEliteTwo = 920,
    DefeatedEliteThree = 930,
    DefeatedEliteFour = 940,
    BecameChampion = 950,
    StoryComplete = 1000
}

public enum NpcStoryRole
{
    None,
    Mother,
    ProfessorPokebart
}
