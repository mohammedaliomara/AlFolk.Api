namespace AlFolk.Api.DTOs;

public record DashboardMetrics(
    int TotalChildren,
    int TotalParents,
    int AvgProgress,
    int TotalScore,
    double AvgUsageHours
);

public record WeeklyActivityDto(
    string Day,
    int Kids,
    int Parents
);

public record DashboardResponse(
    DashboardMetrics Metrics,
    List<WeeklyActivityDto> WeeklyActivity
);

public record TopKidDto(
    Guid Id,
    string Name,
    int TotalScore,
    string TopGame,
    int Rank
);

public record GameHeroDto(
    string SkillKey,
    string SkillLabel,
    string HeroName,
    Guid HeroId,
    int TopScore
);

public record TopKidsResponse(
    List<TopKidDto> Top3,
    List<GameHeroDto> GameHeroes,
    PaginatedResponse<TopKidDto> Others
);

public record UsageTimeDto(
    Guid Id,
    string Name,
    string ParentName,
    double UsageHours,
    int TotalScore,
    int EstimatedMinutes
);

public record LogUsageRequest(
    Guid ChildId,
    int SessionDurationMinutes,
    string GameKey,
    int PointsEarned
);
