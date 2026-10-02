using AlFolk.Api.Data;
using AlFolk.Api.DTOs;
using AlFolk.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AlFolk.Api.Services;

public interface IContentService
{
    Task<PlaylistGroupDto> GetPlaylistsAsync(string? audience);
    Task<PlaylistDetailDto?> GetPlaylistByIdAsync(Guid id);
    Task<PlaylistDetailDto> CreatePlaylistAsync(CreatePlaylistRequest request);
    Task<bool> UpdatePlaylistAsync(Guid id, CreatePlaylistRequest request);
    Task<bool> DeletePlaylistAsync(Guid id);
    
    Task<VideoDto> AddVideoAsync(Guid playlistId, CreateVideoRequest request);
    Task<bool> UpdateVideoAsync(Guid playlistId, Guid videoId, CreateVideoRequest request);
    Task<bool> DeleteVideoAsync(Guid playlistId, Guid videoId);
}

public class ContentService : IContentService
{
    private readonly AppDbContext _context;

    public ContentService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PlaylistGroupDto> GetPlaylistsAsync(string? audience)
    {
        var query = _context.Playlists.Include(p => p.Videos).AsQueryable();

        if (!string.IsNullOrEmpty(audience) && audience.ToLower() != "all")
        {
            query = query.Where(p => p.TargetAudience.ToLower() == audience.ToLower());
        }

        var playlists = await query.ToListAsync();

        return new PlaylistGroupDto(
            playlists.Where(p => p.TargetAudience.ToLower() == "kids")
                .Select(p => new PlaylistListDto(
                    p.Id, p.Title, p.Description, p.Videos.Count,
                    p.Videos.OrderBy(v => v.SortOrder).Select(v => new VideoDto(v.Id, v.Title, v.VideoUrl, v.Description, v.DurationSeconds.ToString(), v.SortOrder, v.ViewCount)).ToList()
                )).ToList(),
            playlists.Where(p => p.TargetAudience.ToLower() == "parents")
                .Select(p => new PlaylistListDto(
                    p.Id, p.Title, p.Description, p.Videos.Count,
                    p.Videos.OrderBy(v => v.SortOrder).Select(v => new VideoDto(v.Id, v.Title, v.VideoUrl, v.Description, v.DurationSeconds.ToString(), v.SortOrder, v.ViewCount)).ToList()
                )).ToList()
        );
    }

    public async Task<PlaylistDetailDto?> GetPlaylistByIdAsync(Guid id)
    {
        var p = await _context.Playlists.Include(p => p.Videos).FirstOrDefaultAsync(p => p.Id == id);
        if (p == null) return null;

        return new PlaylistDetailDto(
            p.Id, p.Title, p.Description, p.TargetAudience,
            p.Videos.OrderBy(v => v.SortOrder).Select(v => new VideoDto(v.Id, v.Title, v.VideoUrl, v.Description, v.DurationSeconds.ToString(), v.SortOrder, v.ViewCount)).ToList()
        );
    }

    public async Task<PlaylistDetailDto> CreatePlaylistAsync(CreatePlaylistRequest request)
    {
        var playlist = new Playlist
        {
            Title = request.Title,
            Description = request.Description,
            TargetAudience = request.TargetAudience
        };
        _context.Playlists.Add(playlist);
        await _context.SaveChangesAsync();
        return (await GetPlaylistByIdAsync(playlist.Id))!;
    }

    public async Task<bool> UpdatePlaylistAsync(Guid id, CreatePlaylistRequest request)
    {
        var p = await _context.Playlists.FindAsync(id);
        if (p == null) return false;

        p.Title = request.Title;
        p.Description = request.Description;
        p.TargetAudience = request.TargetAudience;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeletePlaylistAsync(Guid id)
    {
        var p = await _context.Playlists.FindAsync(id);
        if (p == null) return false;

        p.IsDeleted = true;
        p.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<VideoDto> AddVideoAsync(Guid playlistId, CreateVideoRequest request)
    {
        int.TryParse(request.Duration, out int duration);
        var video = new Video
        {
            PlaylistId = playlistId,
            Title = request.Title,
            VideoUrl = request.Link,
            Description = request.Description,
            DurationSeconds = duration,
            SortOrder = request.SortOrder
        };
        _context.Videos.Add(video);
        await _context.SaveChangesAsync();
        return new VideoDto(video.Id, video.Title, video.VideoUrl, video.Description, video.DurationSeconds.ToString(), video.SortOrder, video.ViewCount);
    }

    public async Task<bool> UpdateVideoAsync(Guid playlistId, Guid videoId, CreateVideoRequest request)
    {
        var v = await _context.Videos.FirstOrDefaultAsync(x => x.Id == videoId && x.PlaylistId == playlistId);
        if (v == null) return false;

        int.TryParse(request.Duration, out int duration);
        v.Title = request.Title;
        v.VideoUrl = request.Link;
        v.Description = request.Description;
        v.DurationSeconds = duration;
        v.SortOrder = request.SortOrder;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteVideoAsync(Guid playlistId, Guid videoId)
    {
        var v = await _context.Videos.FirstOrDefaultAsync(x => x.Id == videoId && x.PlaylistId == playlistId);
        if (v == null) return false;

        v.IsDeleted = true;
        v.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }
}
