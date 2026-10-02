namespace AlFolk.Api.DTOs;

public record GameSessionRequest(
    Guid ChildId,
    string GameName,
    int Score,
    int DurationSeconds
);

public record GameSessionResponse(
    Guid ChildId,
    string GameName,
    int PointsAdded,
    int NewSkillScore,
    int NewTotalScore
);
