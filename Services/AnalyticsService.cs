using AlFolk.Api.Data;
using AlFolk.Api.DTOs;
using AlFolk.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AlFolk.Api.Services;

public interface IAnalyticsService
{
    Task<DashboardResponse> GetDashboardDataAsync();
    Task<TopKidsResponse> GetTopKidsAsync(int page, int pageSize);
    Task<PaginatedResponse<UsageTimeDto>> GetUsageTimeAsync(int page, int pageSize, string sortBy, string sortDir);
    Task<List<UsageTimeDto>> GetUsageTimeReportAsync();
    Task<double> GetAverageUsageHoursAsync();
}

public class AnalyticsService : IAnalyticsService
{
    private readonly AppDbContext _context;
    private readonly IChildService _childService;

    public AnalyticsService(AppDbContext context, IChildService childService)
    {
        _context = context;
        _childService = childService;
    }

    public async Task<DashboardResponse> GetDashboardDataAsync()
    {
        var totalChildren = await _context.Children.CountAsync();
        var totalParents = await _context.Users.CountAsync(u => u.Role == "Parent");
        
        var children = await _context.Children.Include(c => c.Skills).ToListAsync();
        var allSkills = children.SelectMany(c => c.Skills).ToList();

        var totalScore = allSkills.Sum(s => s.Points);
        var avgProgress = (int)(allSkills.Any() ? allSkills.Average(s => s.Progress) : 0);
        
        var gameSessionsDurations = await _context.GameSessions
            .GroupBy(gs => gs.ChildId)
            .Select(g => new { ChildId = g.Key, Duration = g.Sum(gs => gs.DurationSeconds) })
            .ToDictionaryAsync(x => x.ChildId, x => x.Duration);

        var videoViewsDurations = await _context.VideoViews
            .Where(vv => vv.ChildId != null)
            .GroupBy(vv => vv.ChildId)
            .Select(g => new { ChildId = g.Key!.Value, Duration = g.Sum(vv => vv.DurationWatchedSeconds) })
            .ToDictionaryAsync(x => x.ChildId, x => x.Duration);

        var totalUsageHours = 0.0;
        foreach (var c in children)
        {
            gameSessionsDurations.TryGetValue(c.Id, out var gsDur);
            videoViewsDurations.TryGetValue(c.Id, out var vvDur);
            var calcHours = (gsDur + vvDur) / 3600.0;
            totalUsageHours += calcHours > 0 ? calcHours : c.UsageHours;
        }

        // Return total hours directly because the frontend expects "ساعات الأطفال" (Total Children Hours)
        var finalUsageHours = totalUsageHours;

        var activityData = new List<WeeklyActivityDto>();
        var daysCount = 7;
        var now = DateTime.UtcNow.Date;

        for (int i = daysCount - 1; i >= 0; i--)
        {
            var date = now.AddDays(-i);
            
            // 1. Calculate distinct active kids on this date from logs, game sessions, and video views
            var kidsActive = await _context.ActivityLogs
                .Where(l => l.CreatedAt.Date == date && l.ChildId != null)
                .Select(l => l.ChildId)
                .Concat(
                    _context.GameSessions
                        .Where(gs => gs.PlayedAt.Date == date)
                        .Select(gs => (Guid?)gs.ChildId)
                )
                .Concat(
                    _context.VideoViews
                        .Where(vv => vv.ChildId != null && vv.WatchedAt.Date == date)
                        .Select(vv => vv.ChildId)
                )
                .Distinct()
                .CountAsync();

            // 2. Calculate distinct active parents on this date from logs and video views
            var parentsActive = await _context.ActivityLogs
                .Where(l => l.CreatedAt.Date == date && l.UserId != null && _context.Users.Any(u => u.Id == l.UserId && u.Role == "Parent"))
                .Select(l => l.UserId)
                .Concat(
                    _context.VideoViews
                        .Where(vv => vv.UserId != null && vv.WatchedAt.Date == date && _context.Users.Any(u => u.Id == vv.UserId && u.Role == "Parent"))
                        .Select(vv => vv.UserId)
                )
                .Distinct()
                .CountAsync();

            // 3. Since kids play under parent's account, if kids are active, the parent is also active
            if (parentsActive < kidsActive)
            {
                parentsActive = kidsActive;
            }

            activityData.Add(new WeeklyActivityDto(
                date.ToString("dddd", new System.Globalization.CultureInfo("ar-EG")),
                kidsActive,
                parentsActive
            ));
        }

        return new DashboardResponse(
            new DashboardMetrics(totalChildren, totalParents, avgProgress, totalScore, Math.Round(finalUsageHours, 2)),
            activityData
        );
    }

    public async Task<TopKidsResponse> GetTopKidsAsync(int page, int pageSize)
    {
        var allChildrenResponse = await _childService.GetChildrenAsync(null, 1, int.MaxValue, "all", "totalScore", "desc");
        var allChildren = allChildrenResponse.Data;

        var allSessions = await _context.GameSessions
            .Include(gs => gs.Game)
            .ToListAsync();

        var top3 = allChildren.Take(3).Select(c => new TopKidDto(
            c.Id, c.Name, c.TotalScore, GetTopGameForChild(c.Id, c.Skills, allSessions), c.Rank
        )).ToList();

        var nonScoringGames = new[] { "خربش الصور", "ارسم ولون" };
        var scoringGames = await _context.Games.Where(g => !nonScoringGames.Contains(g.Name)).ToListAsync();
        var gameHeroes = new List<GameHeroDto>();

        foreach (var game in scoringGames)
        {
            var topChildFromSessions = allSessions
                .Where(gs => gs.GameId == game.Id)
                .GroupBy(gs => gs.ChildId)
                .Select(g => new { ChildId = g.Key, TotalScore = g.Sum(gs => gs.Score) })
                .OrderByDescending(x => x.TotalScore)
                .FirstOrDefault();

            if (topChildFromSessions != null)
            {
                var child = allChildren.FirstOrDefault(c => c.Id == topChildFromSessions.ChildId);
                gameHeroes.Add(new GameHeroDto(
                    game.Name,
                    game.Name,
                    child?.Name ?? "-",
                    topChildFromSessions.ChildId,
                    topChildFromSessions.TotalScore
                ));
            }
            else
            {
                // Skills dict now uses Arabic game names as keys
                var topForGame = allChildren
                    .OrderByDescending(c => c.Skills.ContainsKey(game.Name) ? c.Skills[game.Name].Pts : 0)
                    .FirstOrDefault();

                gameHeroes.Add(new GameHeroDto(
                    game.Name,
                    game.Name,
                    topForGame?.Name ?? "-",
                    topForGame?.Id ?? Guid.Empty,
                    topForGame != null && topForGame.Skills.ContainsKey(game.Name) ? topForGame.Skills[game.Name].Pts : 0
                ));
            }
        }

        var othersData = allChildren.Skip(3).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new TopKidDto(c.Id, c.Name, c.TotalScore, GetTopGameForChild(c.Id, c.Skills, allSessions), c.Rank)).ToList();

        var others = new PaginatedResponse<TopKidDto>(
            othersData, 
            new PaginationMetadata(page, pageSize, Math.Max(0, allChildren.Count - 3), (int)Math.Ceiling(Math.Max(0, allChildren.Count - 3) / (double)pageSize))
        );

        return new TopKidsResponse(top3, gameHeroes, others);
    }

    private string GetTopGameForChild(Guid childId, Dictionary<string, SkillDto> skills, List<GameSession> allSessions)
    {
        var nonScoringGames = new[] { "خربش الصور", "ارسم ولون" };

        var childSessions = allSessions.Where(gs => gs.ChildId == childId && gs.Game != null && !nonScoringGames.Contains(gs.Game.Name)).ToList();
        if (childSessions.Any())
        {
            return childSessions
                .GroupBy(gs => ArabicNormalization.NormalizeGameName(gs.Game.Name))
                .Select(g => new { GameName = g.Key, TotalScore = g.Sum(gs => gs.Score) })
                .OrderByDescending(g => g.TotalScore)
                .FirstOrDefault()?.GameName ?? "-";
        }

        if (skills == null || !skills.Any()) return "-";

        // Skills dict now contains Arabic game name keys — return the top one directly
        var topGame = skills
            .Where(s => !nonScoringGames.Contains(s.Key))
            .OrderByDescending(s => s.Value?.Pts ?? 0)
            .FirstOrDefault();

        return string.IsNullOrEmpty(topGame.Key) ? "-" : topGame.Key;
    }

    public async Task<PaginatedResponse<UsageTimeDto>> GetUsageTimeAsync(int page, int pageSize, string sortBy, string sortDir)
    {
        var allChildren = await _context.Children
            .Include(c => c.Parent)
            .Include(c => c.Skills)
            .ToListAsync();

        var gameSessionsDurations = await _context.GameSessions
            .GroupBy(gs => gs.ChildId)
            .Select(g => new { ChildId = g.Key, Duration = g.Sum(gs => gs.DurationSeconds) })
            .ToDictionaryAsync(x => x.ChildId, x => x.Duration);

        var videoViewsDurations = await _context.VideoViews
            .Where(vv => vv.ChildId != null)
            .GroupBy(vv => vv.ChildId)
            .Select(g => new { ChildId = g.Key!.Value, Duration = g.Sum(vv => vv.DurationWatchedSeconds) })
            .ToDictionaryAsync(x => x.ChildId, x => x.Duration);

        var enriched = allChildren.Select(c =>
        {
            gameSessionsDurations.TryGetValue(c.Id, out var gsDur);
            videoViewsDurations.TryGetValue(c.Id, out var vvDur);
            var calcHours = (gsDur + vvDur) / 3600.0;
            var usageHours = calcHours > 0 ? Math.Round(calcHours, 2) : Math.Round(c.UsageHours, 2);

            int trueTotalMinutes = (int)Math.Round(usageHours * 60);
            int trueHours = trueTotalMinutes / 60;
            int trueMins = trueTotalMinutes % 60;
            
            // HACK: The frontend developer swapped Hours and Minutes when rendering the string.
            // To fix it from the backend without touching the frontend code, we send a fake total minutes value 
            // where (fake / 60) gives the true minutes, and (fake % 60) gives the true hours.
            // Note: This will make the tooltip show the fake minutes, but the text "س د" will be correct.
            int swappedFakeMinutes = (trueMins * 60) + trueHours;

            return new UsageTimeDto(
                c.Id,
                c.Name,
                c.Parent.FirstName + " " + c.Parent.LastName,
                usageHours,
                c.Skills.Sum(s => s.Points),
                swappedFakeMinutes
            );
        }).ToList();

        var query = enriched.AsEnumerable();

        if (sortBy == "usageHours")
            query = sortDir == "asc" ? query.OrderBy(x => x.UsageHours) : query.OrderByDescending(x => x.UsageHours);
        else if (sortBy == "totalScore")
            query = sortDir == "asc" ? query.OrderBy(x => x.TotalScore) : query.OrderByDescending(x => x.TotalScore);
        else
            query = query.OrderByDescending(x => x.Id);

        var total = query.Count();
        var data = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PaginatedResponse<UsageTimeDto>(data, new PaginationMetadata(page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize)));
    }

    public async Task<List<UsageTimeDto>> GetUsageTimeReportAsync()
    {
        var response = await GetUsageTimeAsync(1, int.MaxValue, "usageHours", "desc");
        return response.Data;
    }

    public async Task<double> GetAverageUsageHoursAsync()
    {
        var gameSessionsDurations = await _context.GameSessions
            .GroupBy(gs => gs.ChildId)
            .Select(g => new { ChildId = g.Key, Duration = g.Sum(gs => gs.DurationSeconds) })
            .ToDictionaryAsync(x => x.ChildId, x => x.Duration);

        var videoViewsDurations = await _context.VideoViews
            .Where(vv => vv.ChildId != null)
            .GroupBy(vv => vv.ChildId)
            .Select(g => new { ChildId = g.Key!.Value, Duration = g.Sum(vv => vv.DurationWatchedSeconds) })
            .ToDictionaryAsync(x => x.ChildId, x => x.Duration);

        var children = await _context.Children.ToListAsync();
        var totalUsageHours = 0.0;
        foreach (var c in children)
        {
            gameSessionsDurations.TryGetValue(c.Id, out var gsDur);
            videoViewsDurations.TryGetValue(c.Id, out var vvDur);
            var calcHours = (gsDur + vvDur) / 3600.0;
            totalUsageHours += calcHours > 0 ? calcHours : c.UsageHours;
        }

        var avgUsageHours = children.Any() ? totalUsageHours / children.Count : 0.0;
        return Math.Round(avgUsageHours, 2);
    }

}
