namespace AlFolk.Api.DTOs;

public record ParentListDto(
    Guid Id,
    string Name,
    string Phone,
    string? Email,
    string? NationalId,
    int LoginCount,
    int ChildrenCount,
    List<string> ChildrenNames,
    int WatchedVideosCount,
    int TotalChildrenScore,
    DateTime CreatedAt
);

public record CreateParentRequest(
    string Name,
    string Phone,
    string? Email,
    string? NationalId,
    string? Password
);

public record UpdateParentRequest(
    string Name,
    string Phone,
    string? Email,
    string? NationalId
);

public record ParentDetailDto(
    Guid Id,
    string Name,
    string Phone,
    string? Email,
    string? NationalId,
    int LoginCount,
    List<WatchedVideoDto> WatchedVideos,
    List<ChildSummaryDto> Children,
    double TotalUsageHours,
    int TotalChildrenScore
);

public record WatchedVideoDto(
    Guid Id,
    string Title,
    string? ThumbnailUrl,
    string? Category,
    int DurationSeconds,
    int DurationWatchedSeconds,
    DateTime WatchedAt
);

public record ChildSummaryDto(Guid Id, string Name, int Age, int TotalScore, int AvgProgress);

public record PaginationMetadata(int Page, int PageSize, int Total, int TotalPages);

public record PaginatedResponse<T>(List<T> Data, PaginationMetadata Pagination);
