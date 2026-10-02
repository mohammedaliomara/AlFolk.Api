using AlFolk.Api.Data;
using AlFolk.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlFolk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAnalyticsService _analyticsService;

    public DashboardController(AppDbContext context, IAnalyticsService analyticsService)
    {
        _context = context;
        _analyticsService = analyticsService;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var totalChildren = await _context.Children.CountAsync();
        var totalParents = await _context.Users.CountAsync(u => u.Role == "Parent");
        var totalPoints = await _context.ChildSkills.SumAsync(s => s.Points);
        var totalSessions = await _context.GameSessions.CountAsync();

        // Calculate real usage hours from actual game sessions + video views
        var gameSeconds = await _context.GameSessions.SumAsync(gs => (long)gs.DurationSeconds);
        var videoSeconds = await _context.VideoViews
            .Where(vv => vv.ChildId != null)
            .SumAsync(vv => (long)vv.DurationWatchedSeconds);
        var totalUsageHours = Math.Round((gameSeconds + videoSeconds) / 3600.0, 2);

        // Fallback to stored hours if no sessions recorded yet
        if (totalUsageHours == 0)
            totalUsageHours = Math.Round(await _context.Children.SumAsync(c => c.UsageHours), 2);

        return Ok(new
        {
            success = true,
            data = new
            {
                totalChildren = totalChildren,
                totalParents = totalParents,
                totalUsageHours = totalUsageHours,
                totalPoints = totalPoints,
                totalSessions = totalSessions
            }
        });
    }

    [HttpGet("weekly-activity")]
    public async Task<IActionResult> GetWeeklyActivity([FromQuery] int weeks = 1)
    {
        var dashboardData = await _analyticsService.GetDashboardDataAsync();
        var daysCount = 7;
        var now = DateTime.UtcNow.Date;
        var resultList = new List<object>();

        for (int i = daysCount - 1; i >= 0; i--)
        {
            var date = now.AddDays(-i);
            // idx goes 0..6 matching the WeeklyActivity list built in AnalyticsService
            var idx = daysCount - 1 - i;
            if (idx >= 0 && idx < dashboardData.WeeklyActivity.Count)
            {
                var activity = dashboardData.WeeklyActivity[idx];
                resultList.Add(new
                {
                    date = date.ToString("yyyy-MM-dd"),
                    dayName = activity.Day,
                    activeKids = activity.Kids,
                    activeParents = activity.Parents
                });
            }
        }

        return Ok(new
        {
            success = true,
            data = resultList
        });
    }

    [HttpGet("monthly-stats")]
    public async Task<IActionResult> GetMonthlyStats()
    {
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var activeKids = await _context.GameSessions
            .Where(gs => gs.PlayedAt >= thirtyDaysAgo)
            .Select(gs => gs.ChildId)
            .Distinct()
            .CountAsync();

        var activeParents = await _context.Users
            .Where(u => u.Role == "Parent" && u.LastLoginAt >= thirtyDaysAgo)
            .CountAsync();

        var totalUsageLastMonth = await _context.GameSessions
            .Where(gs => gs.PlayedAt >= thirtyDaysAgo)
            .SumAsync(gs => (double)gs.DurationSeconds) / 3600.0;

        return Ok(new
        {
            success = true,
            data = new
            {
                month = DateTime.UtcNow.ToString("MMMM yyyy"),
                activeChildrenThisMonth = activeKids,
                activeParentsThisMonth = activeParents,
                totalUsageHoursThisMonth = Math.Round(totalUsageLastMonth, 2)
            }
        });
    }

    [HttpGet("top-games")]
    public async Task<IActionResult> GetTopGamesStats()
    {
        var games = await _context.Games.ToListAsync();
        var sessions = await _context.GameSessions.ToListAsync();

        var list = games.Select(g => new
        {
            gameKey = g.Name,
            gameName = g.Name,
            totalPlaySessions = sessions.Count(s => s.GameId == g.Id),
            totalPointsEarned = sessions.Where(s => s.GameId == g.Id).Sum(s => s.Score),
            totalPlayTimeMinutes = Math.Round(sessions.Where(s => s.GameId == g.Id).Sum(s => s.DurationSeconds) / 60.0, 2)
        })
        .OrderByDescending(x => x.totalPlaySessions)
        .ToList();

        return Ok(new { success = true, data = list });
    }

    [HttpGet("engagement")]
    public async Task<IActionResult> GetEngagement()
    {
        var totalSessions = await _context.GameSessions.CountAsync();
        var avgSessionSeconds = totalSessions > 0 ? await _context.GameSessions.AverageAsync(s => s.DurationSeconds) : 0.0;
        var totalKids = await _context.Children.CountAsync();
        var avgPointsPerKid = totalKids > 0 ? (await _context.ChildSkills.SumAsync(s => s.Points)) / (double)totalKids : 0.0;

        return Ok(new
        {
            success = true,
            data = new
            {
                averageSessionMinutes = Math.Round(avgSessionSeconds / 60.0, 2),
                averagePointsPerChild = Math.Round(avgPointsPerKid, 2),
                totalPlaySessionsCount = totalSessions
            }
        });
    }

    [HttpGet("revenue")]
    public IActionResult GetRevenue()
    {
        return Ok(new
        {
            success = true,
            data = new
            {
                monthlyRecurringRevenue = 0.0,
                annualRecurringRevenue = 0.0,
                activeSubscriptions = 0,
                trialSubscriptions = 0,
                currency = "EGP"
            }
        });
    }
}
