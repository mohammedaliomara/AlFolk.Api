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
public class LeaderboardController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAnalyticsService _analyticsService;

    public LeaderboardController(AppDbContext context, IAnalyticsService analyticsService)
    {
        _context = context;
        _analyticsService = analyticsService;
    }

    [HttpGet]
    public async Task<IActionResult> GetLeaderboard([FromQuery] int page = 1, [FromQuery] int pageSize = 5)
    {
        var topKidsData = await _analyticsService.GetTopKidsAsync(page, pageSize);

        var topThreeMapped = new List<object>();
        foreach (var kid in topKidsData.Top3)
        {
            var child = await _context.Children.Include(c => c.Parent).FirstOrDefaultAsync(c => c.Id == kid.Id);
            topThreeMapped.Add(new
            {
                rank = kid.Rank,
                id = kid.Id,
                fullName = kid.Name,
                totalPoints = kid.TotalScore,
                topGame = kid.TopGame,
                parentName = child != null ? (child.Parent.FirstName + " " + child.Parent.LastName) : "N/A"
            });
        }

        var othersMappedList = new List<object>();
        foreach (var kid in topKidsData.Others.Data)
        {
            var child = await _context.Children.Include(c => c.Parent).FirstOrDefaultAsync(c => c.Id == kid.Id);
            othersMappedList.Add(new
            {
                rank = kid.Rank,
                id = kid.Id,
                fullName = kid.Name,
                totalPoints = kid.TotalScore,
                topGame = kid.TopGame,
                parentName = child != null ? (child.Parent.FirstName + " " + child.Parent.LastName) : "N/A"
            });
        }

        var gameHeroesMapped = topKidsData.GameHeroes.Select(gh => new
        {
            gameKey = gh.SkillKey,
            gameLabel = gh.SkillLabel,
            topChildName = gh.HeroName,
            points = gh.TopScore
        }).ToList();

        var averageUsageHours = await _analyticsService.GetAverageUsageHoursAsync();
        var averageUsageMinutes = (int)(averageUsageHours * 60);

        return Ok(new
        {
            success = true,
            data = new
            {
                topThree = topThreeMapped,
                others = new
                {
                    items = othersMappedList,
                    pagination = topKidsData.Others.Pagination
                },
                gameHeroes = gameHeroesMapped,
                averageUsageHours = averageUsageHours,
                averageUsageMinutes = averageUsageMinutes
            }
        });
    }
}
