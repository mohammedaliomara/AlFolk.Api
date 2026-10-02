namespace AlFolk.Api.DTOs;

public record ChildListDto(
    Guid Id,
    string Name,
    Guid ParentId,
    string ParentName,
    string ParentPhone,
    int Age,
    double UsageHours,
    int TotalScore,
    int AvgProgress,
    int Rank,
    Dictionary<string, SkillDto> Skills
);

public record SkillDto(int Pts, int Prg);

public record CreateChildRequest(
    string Name,
    int Age,
    string Gender,
    string? NationalId,
    Guid ParentId
);

public record UpdateChildRequest(
    string Name,
    int Age,
    string Gender,
    string? NationalId,
    Guid ParentId
);

public record MedicationDto(
    Guid Id,
    string Name,
    string? Dosage,
    string? ScheduleJson,
    int Frequency,
    string? Notes
);

public record ChildDetailDto(
    Guid Id,
    string Name,
    Guid ParentId,
    string ParentName,
    string ParentPhone,
    int Age,
    double UsageHours,
    List<MedicationDto> Medications,
    int Rank,
    int TotalScore,
    int AvgProgress,
    Dictionary<string, SkillDto> Skills,
    List<WatchedVideoDto> WatchedVideos,
    string? NationalId,
    string Gender,
    string BestGame
);
 
public record UpdateScoreRequest(string SkillKey, int PointsToAdd, int NewProgress);
 
public record ScoreUpdateResponse(int OldSkillPoints, int NewSkillPoints, int NewTotalScore);
