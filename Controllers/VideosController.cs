using System.Security.Claims;
using AlFolk.Api.Data;
using AlFolk.Api.DTOs;
using AlFolk.Api.Models;
using AlFolk.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlFolk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class VideosController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IContentService _contentService;

    public VideosController(AppDbContext context, IContentService contentService)
    {
        _context = context;
        _contentService = contentService;
    }

    [HttpGet("playlists")]
    public async Task<IActionResult> GetPlaylists([FromQuery] string? audience = "all")
    {
        var response = await _contentService.GetPlaylistsAsync(audience);
        return Ok(response);
    }

    [HttpPost("playlists")]
    public async Task<IActionResult> CreatePlaylist([FromBody] CreatePlaylistRequest request)
    {
        var playlist = await _contentService.CreatePlaylistAsync(request);
        return Ok(playlist);
    }

    [HttpPut("playlists/{id}")]
    public async Task<IActionResult> UpdatePlaylist(Guid id, [FromBody] CreatePlaylistRequest request)
    {
        var success = await _contentService.UpdatePlaylistAsync(id, request);
        if (!success) return NotFound();
        return Ok(new { success = true, message = "Playlist updated successfully" });
    }

    [HttpDelete("playlists/{id}")]
    public async Task<IActionResult> DeletePlaylist(Guid id)
    {
        var success = await _contentService.DeletePlaylistAsync(id);
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpPost("playlists/{playlistId}/videos")]
    public async Task<IActionResult> AddVideo(Guid playlistId, [FromBody] CreateVideoRequest request)
    {
        var video = await _contentService.AddVideoAsync(playlistId, request);
        return Ok(video);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateVideo(Guid id, [FromBody] CreateVideoRequest request)
    {
        var v = await _context.Videos.FindAsync(id);
        if (v == null || v.IsDeleted) return NotFound();

        int.TryParse(request.Duration, out int duration);
        v.Title = request.Title;
        v.VideoUrl = request.Link;
        v.Description = request.Description;
        v.DurationSeconds = duration;
        v.SortOrder = request.SortOrder;

        await _context.SaveChangesAsync();
        return Ok(new { success = true, message = "Video updated successfully" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteVideo(Guid id)
    {
        var v = await _context.Videos.FindAsync(id);
        if (v == null || v.IsDeleted) return NotFound();

        v.IsDeleted = true;
        v.DeletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("playlists/{id}")]
    public async Task<IActionResult> GetPlaylistDetails(Guid id)
    {
        var playlist = await _contentService.GetPlaylistByIdAsync(id);
        if (playlist == null) return NotFound();
        return Ok(playlist);
    }

    [HttpPost("{id}/watch")]
    public async Task<IActionResult> TrackWatch(Guid id)
    {
        var userIdString = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
        {
            return Unauthorized();
        }

        var video = await _context.Videos.FindAsync(id);
        if (video == null || video.IsDeleted) return NotFound();

        video.ViewCount++;

        var view = new VideoView
        {
            UserId = userId,
            VideoId = id,
            WatchedAt = DateTime.UtcNow
        };
        _context.VideoViews.Add(view);

        await _context.SaveChangesAsync();
        return Ok(new { success = true, message = "تم تسجيل مشاهدة الفيديو وزيادة العداد" });
    }

    [HttpGet("recently-watched")]
    public async Task<IActionResult> GetRecentlyWatched([FromQuery] int limit = 5)
    {
        var userIdString = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
        {
            return Unauthorized();
        }

        var recent = await _context.VideoViews
            .Include(vv => vv.Video)
            .Where(vv => vv.UserId == userId)
            .OrderByDescending(vv => vv.WatchedAt)
            .Take(limit)
            .Select(vv => new
            {
                videoId = vv.VideoId,
                videoTitle = vv.Video.Title,
                videoUrl = vv.Video.VideoUrl,
                watchedAt = vv.WatchedAt
            })
            .ToListAsync();

        return Ok(new { success = true, data = recent });
    }
}
