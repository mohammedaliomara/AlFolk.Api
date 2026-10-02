using AlFolk.Api.Data;
using AlFolk.Api.DTOs;
using AlFolk.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlFolk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ChildrenController : ControllerBase
{
    private readonly IChildService _childService;
    private readonly AppDbContext _context;

    public ChildrenController(IChildService childService, AppDbContext context)
    {
        _childService = childService;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetChildren(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string skillFilter = "all",
        [FromQuery] string sortBy = "totalScore",
        [FromQuery] string sortDir = "desc")
    {
        var response = await _childService.GetChildrenAsync(search, page, pageSize, skillFilter, sortBy, sortDir);
        return Ok(response);
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportChildren(
        [FromQuery] string? search,
        [FromQuery] string skillFilter = "all",
        [FromQuery] string sortBy = "totalScore",
        [FromQuery] string sortDir = "desc")
    {
        var response = await _childService.GetChildrenAsync(search, 1, int.MaxValue, skillFilter, sortBy, sortDir);
        var children = response.Data;

        var csvBuilder = new System.Text.StringBuilder();
        csvBuilder.AppendLine("معرف الطفل,الاسم الكامل,العمر,معرف ولي الأمر,اسم ولي الأمر,رقم هاتف ولي الأمر,إجمالي النقاط,ساعات الاستخدام");

        foreach (var child in children)
        {
            csvBuilder.AppendLine($"{child.Id},{child.Name},{child.Age},{child.ParentId},{child.ParentName},{child.ParentPhone},{child.TotalScore},{child.UsageHours}");
        }

        var csvBytes = System.Text.Encoding.Unicode.GetPreamble()
            .Concat(System.Text.Encoding.Unicode.GetBytes(csvBuilder.ToString()))
            .ToArray();
        return File(csvBytes, "text/csv; charset=utf-16", "children_export.csv");
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetChild(Guid id)
    {
        var child = await _childService.GetChildByIdAsync(id);
        if (child == null) return NotFound();
        return Ok(child);
    }

    [HttpPost]
    public async Task<IActionResult> CreateChild([FromBody] CreateChildRequest request)
    {
        try
        {
            var child = await _childService.CreateChildAsync(request);
            return CreatedAtAction(nameof(GetChild), new { id = child.Id }, child);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateChild(Guid id, [FromBody] UpdateChildRequest request)
    {
        try
        {
            var success = await _childService.UpdateChildAsync(id, request);
            if (!success) return NotFound();
            return Ok(new { message = "Child updated successfully" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteChild(Guid id)
    {
        var success = await _childService.DeleteChildAsync(id);
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpPost("{id}/score")]
    public async Task<IActionResult> UpdateScore(Guid id, [FromBody] UpdateScoreRequest request)
    {
        var result = await _childService.UpdateScoreAsync(id, request);
        if (result == null) return BadRequest(new { message = "Failed to update score. Check child ID or SkillKey." });
        return Ok(result);
    }

    [HttpGet("{id}/total-score")]
    public async Task<IActionResult> GetTotalScore(Guid id)
    {
        var totalScore = await _childService.GetTotalScoreAsync(id);
        return Ok(new { childId = id, totalScore });
    }

    [HttpGet("top")]
    public async Task<IActionResult> GetTopChildren([FromQuery] int limit = 10)
    {
        var response = await _childService.GetChildrenAsync(null, 1, limit, "all", "totalScore", "desc");

        var data = response.Data.Select(c =>
        {
            // All 10 Arabic game names in display order
            var allGameNames = new[]
            {
                "خربش الصور", "ارسم ولون", "لون الصوره", "الألوان",
                "اصوات حيوانات", "بيانو", "اكسليفون",
                "الذاكره", "الشكل و ظله", "ركب الصور"
            };

            var allGames = allGameNames.Select(name => new
            {
                gameName = name,
                points   = c.Skills.TryGetValue(name, out var g) ? g.Pts : 0,
                progress = c.Skills.TryGetValue(name, out var gp) ? gp.Prg : 0
            }).ToList<object>();

            var topGameName = allGames.Count > 0
                ? ((dynamic)allGames.OrderByDescending(g => ((dynamic)g).points).First()).gameName
                : "-";

            return new
            {
                rank       = c.Rank,
                id         = c.Id,
                name       = c.Name,
                totalScore = c.TotalScore,
                usageHours = c.UsageHours,
                parentName = c.ParentName,
                topGame    = (string)topGameName,
                games      = allGames
            };
        }).ToList();

        return Ok(new { success = true, total = response.Pagination.Total, data });
    }

    [HttpGet("{id}/skills")]
    public async Task<IActionResult> GetChildSkills(Guid id)
    {
        var child = await _context.Children
            .Include(c => c.Skills)
            .Include(c => c.GameSessions).ThenInclude(gs => gs.Game)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (child == null)
        {
            return NotFound(new { success = false, message = "الطفل غير موجود" });
        }

        // Returns all 9 Arabic game names with their points/progress
        var allGames = ArabicNormalization.GetAllGamesFlat(child.Skills, child.GameSessions);

        return Ok(new { success = true, data = allGames });
    }
}
