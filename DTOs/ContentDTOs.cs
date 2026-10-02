namespace AlFolk.Api.DTOs;

public record PlaylistListDto(
    Guid Id,
    string Title,
    string? Description,
    int VideoCount,
    List<VideoDto> Videos
);

public record PlaylistGroupDto(
    List<PlaylistListDto> Kids,
    List<PlaylistListDto> Parents
);

public record CreatePlaylistRequest(
    string Title,
    string? Description,
    string TargetAudience // "kids" or "parents"
);

public record PlaylistDetailDto(
    Guid Id,
    string Title,
    string? Description,
    string TargetAudience,
    List<VideoDto> Videos
);

public record VideoDto(
    Guid Id,
    string Title,
    string Link,
    string? Description,
    string? Duration,
    int SortOrder,
    long ViewCount
);

public record CreateVideoRequest(
    string Title,
    string Link,
    string? Description,
    string? Duration,
    int SortOrder
);
