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
public class UsageController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAnalyticsService _analyticsService;

    public UsageController(AppDbContext context, IAnalyticsService analyticsService)
    {
        _context = context;
        _analyticsService = analyticsService;
    }

    [HttpGet("children")]
    public async Task<IActionResult> GetChildrenUsage([FromQuery] int page = 1, [FromQuery] int pageSize = 8)
    {
        var allUsageResponse = await _analyticsService.GetUsageTimeAsync(1, int.MaxValue, "usageHours", "desc");
        var allUsage = allUsageResponse.Data;

        var colors = new[] { "#BC6C25", "#3A86C8", "#8338EC", "#FF006E", "#2A9D8F" };

        var topFive = allUsage.Take(5).Select((c, idx) => new
        {
            id = c.Id,
            fullName = c.Name,
            parentName = c.ParentName,
            totalUsageHours = c.UsageHours,
            totalPoints = c.TotalScore,
            rank = idx + 1,
            color = colors[idx % colors.Length]
        }).ToList();

        var othersData = allUsage.Skip(5).Skip((page - 1) * pageSize).Take(pageSize).Select((c, idx) => new
        {
            id = c.Id,
            fullName = c.Name,
            parentName = c.ParentName,
            totalUsageHours = c.UsageHours,
            totalPoints = c.TotalScore,
            rank = idx + 6 + ((page - 1) * pageSize)
        }).ToList();

        var othersCount = Math.Max(0, allUsage.Count - 5);

        return Ok(new
        {
            success = true,
            data = new
            {
                topFive = topFive,
                others = new
                {
                    items = othersData,
                    pagination = new PaginationMetadata(page, pageSize, othersCount, (int)Math.Ceiling(othersCount / (double)pageSize))
                }
            }
        });
    }

    [HttpGet("children/export")]
    public async Task<IActionResult> ExportUsage()
    {
        var allUsageResponse = await _analyticsService.GetUsageTimeAsync(1, int.MaxValue, "usageHours", "desc");
        var allUsage = allUsageResponse.Data;

        var csvBuilder = new System.Text.StringBuilder();
        csvBuilder.AppendLine("معرف الطفل,اسم الطفل الكامل,اسم ولي الأمر,إجمالي ساعات الاستخدام,إجمالي النقاط,دقائق الاستخدام المقدرة");

        foreach (var c in allUsage)
        {
            csvBuilder.AppendLine($"{c.Id},{c.Name},{c.ParentName},{c.UsageHours},{c.TotalScore},{c.EstimatedMinutes}");
        }

        var csvBytes = System.Text.Encoding.Unicode.GetPreamble()
            .Concat(System.Text.Encoding.Unicode.GetBytes(csvBuilder.ToString()))
            .ToArray();
        return File(csvBytes, "text/csv; charset=utf-16", "usage_export.csv");
    }

    [HttpPost("log")]
    public async Task<IActionResult> LogUsage([FromBody] LogUsageRequest request)
    {
        var child = await _context.Children.Include(c => c.Skills).FirstOrDefaultAsync(c => c.Id == request.ChildId);
        if (child == null)
        {
            return NotFound(new { success = false, message = "الطفل غير موجود" });
        }

        // 1. Update TotalUsageHours (UsageHours)
        child.UsageHours += request.SessionDurationMinutes / 60.0;

        // 2. Update game points in ChildSkills
        var normalizedGameKey = ArabicNormalization.NormalizeGameName(request.GameKey);
        var mappedSkillKey = ArabicNormalization.GetSkillKeyFromGameName(normalizedGameKey);
        var skill = child.Skills.FirstOrDefault(s => s.SkillKey.Equals(mappedSkillKey, StringComparison.OrdinalIgnoreCase));
        if (skill == null)
        {
            skill = new ChildSkill
            {
                ChildId = child.Id,
                SkillKey = mappedSkillKey,
                Points = request.PointsEarned,
                Progress = 10.0
            };
            child.Skills.Add(skill);
        }
        else
        {
            skill.Points += request.PointsEarned;
            skill.Progress = Math.Min(100.0, skill.Progress + (request.PointsEarned * 0.1));
        }

        // 3. Save GameSession for history
        var game = await _context.Games.FirstOrDefaultAsync(g => g.Name.Equals(normalizedGameKey, StringComparison.OrdinalIgnoreCase));
        var gameId = game?.Id ?? Guid.Empty;
        
        if (gameId == Guid.Empty)
        {
            // Fallback game if not found
            var firstGame = await _context.Games.FirstOrDefaultAsync();
            if (firstGame != null)
            {
                gameId = firstGame.Id;
            }
        }

        var session = new GameSession
        {
            ChildId = child.Id,
            GameId = gameId,
            Score = request.PointsEarned,
            DurationSeconds = request.SessionDurationMinutes * 60,
            PlayedAt = DateTime.UtcNow
        };
        _context.GameSessions.Add(session);

        // 4. Log in ActivityLog
        var log = new ActivityLog
        {
            ChildId = child.Id,
            ActionType = "PlayGame",
            EntityType = "Game",
            ReferenceId = gameId,
            CreatedAt = DateTime.UtcNow,
            MetadataJson = $"{{\"gameKey\":\"{normalizedGameKey}\",\"pointsEarned\":{request.PointsEarned},\"durationMinutes\":{request.SessionDurationMinutes}}}"
        };
        _context.ActivityLogs.Add(log);

        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = "تم تسجيل الجلسة وتحديث النقاط والمهارات بنجاح" });
    }
}
