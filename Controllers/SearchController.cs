using AlFolk.Api.Data;
using AlFolk.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlFolk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly ISearchService _searchService;
    private readonly AppDbContext _context;

    public SearchController(ISearchService searchService, AppDbContext context)
    {
        _searchService = searchService;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrEmpty(q) || q.Length < 2)
            return BadRequest(new { error = "invalid_query", message = "يجب إدخال حرفين على الأقل للبحث" });

        var results = await _searchService.GlobalSearchAsync(q);
        return Ok(results);
    }

    [HttpGet("advanced")]
    public async Task<IActionResult> AdvancedSearch([FromQuery] string? q, [FromQuery] string type = "all")
    {
        var result = new Dictionary<string, object>();
        var query = q ?? "";

        if (type == "all" || type.Equals("children", StringComparison.OrdinalIgnoreCase))
        {
            var children = await _context.Children
                .Where(c => c.Name.Contains(query) || (c.NationalId != null && c.NationalId.Contains(query)))
                .Select(c => new { id = c.Id, name = c.Name, age = c.Age, parentId = c.ParentId })
                .ToListAsync();
            result["children"] = children;
        }

        if (type == "all" || type.Equals("parents", StringComparison.OrdinalIgnoreCase))
        {
            var parents = await _context.Users
                .Where(u => u.Role == "Parent" && (u.FirstName.Contains(query) || u.LastName.Contains(query) || (u.Phone != null && u.Phone.Contains(query))))
                .Select(u => new { id = u.Id, name = u.FirstName + " " + u.LastName, phone = u.Phone, email = u.Email })
                .ToListAsync();
            result["parents"] = parents;
        }

        if (type == "all" || type.Equals("games", StringComparison.OrdinalIgnoreCase))
        {
            var games = await _context.Games
                .Where(g => g.Name.Contains(query))
                .ToListAsync();
            result["games"] = games;
        }

        if (type == "all" || type.Equals("videos", StringComparison.OrdinalIgnoreCase))
        {
            var videos = await _context.Videos
                .Where(v => !v.IsDeleted && (v.Title.Contains(query) || (v.Description != null && v.Description.Contains(query))))
                .ToListAsync();
            result["videos"] = videos;
        }

        return Ok(new { success = true, data = result });
    }
}
