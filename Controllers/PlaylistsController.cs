using AlFolk.Api.DTOs;
using AlFolk.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFolk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PlaylistsController : ControllerBase
{
    private readonly IContentService _contentService;

    public PlaylistsController(IContentService contentService)
    {
        _contentService = contentService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPlaylists([FromQuery] string? audience = "all")
    {
        var response = await _contentService.GetPlaylistsAsync(audience);
        return Ok(response);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPlaylist(Guid id)
    {
        var playlist = await _contentService.GetPlaylistByIdAsync(id);
        if (playlist == null) return NotFound();
        return Ok(playlist);
    }

    [HttpPost]
    public async Task<IActionResult> CreatePlaylist([FromBody] CreatePlaylistRequest request)
    {
        var playlist = await _contentService.CreatePlaylistAsync(request);
        return CreatedAtAction(nameof(GetPlaylist), new { id = playlist.Id }, playlist);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePlaylist(Guid id, [FromBody] CreatePlaylistRequest request)
    {
        var success = await _contentService.UpdatePlaylistAsync(id, request);
        if (!success) return NotFound();
        return Ok(new { message = "Playlist updated successfully" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePlaylist(Guid id)
    {
        var success = await _contentService.DeletePlaylistAsync(id);
        if (!success) return NotFound();
        return NoContent();
    }

    // Video Management within Playlists
    [HttpPost("{playlistId}/videos")]
    public async Task<IActionResult> AddVideo(Guid playlistId, [FromBody] CreateVideoRequest request)
    {
        var video = await _contentService.AddVideoAsync(playlistId, request);
        return Ok(video);
    }

    [HttpPut("{playlistId}/videos/{videoId}")]
    public async Task<IActionResult> UpdateVideo(Guid playlistId, Guid videoId, [FromBody] CreateVideoRequest request)
    {
        var success = await _contentService.UpdateVideoAsync(playlistId, videoId, request);
        if (!success) return NotFound();
        return Ok(new { message = "Video updated successfully" });
    }

    [HttpDelete("{playlistId}/videos/{videoId}")]
    public async Task<IActionResult> DeleteVideo(Guid playlistId, Guid videoId)
    {
        var success = await _contentService.DeleteVideoAsync(playlistId, videoId);
        if (!success) return NotFound();
        return NoContent();
    }
}
