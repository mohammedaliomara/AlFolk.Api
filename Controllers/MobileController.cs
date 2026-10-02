using AlFolk.Api.Data;
using AlFolk.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AlFolk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MobileController : ControllerBase
{
    private readonly AppDbContext _context;

    public MobileController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("home")]
    public async Task<IActionResult> GetParentHome()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
        {
            return Unauthorized();
        }

        var parent = await _context.Users
            .Include(u => u.Children).ThenInclude(c => c.Skills)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (parent == null)
        {
            return NotFound(new { success = false, message = "الحساب غير موجود" });
        }

        var unreadNotificationsCount = await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);

        var recentVideosWatched = await _context.VideoViews
            .Include(vv => vv.Video)
            .Where(vv => vv.UserId == userId)
            .OrderByDescending(vv => vv.WatchedAt)
            .Take(3)
            .Select(vv => new
            {
                videoId = vv.VideoId,
                title = vv.Video.Title,
                watchedAt = vv.WatchedAt
            })
            .ToListAsync();

        var kidsSummaries = parent.Children.Select(c => new
        {
            id = c.Id,
            name = c.Name,
            age = c.Age,
            gender = c.Gender,
            totalPoints = c.Skills.Sum(s => s.Points),
            avgProgress = c.Skills.Any() ? c.Skills.Average(s => s.Progress) : 0.0
        }).ToList();

        return Ok(new
        {
            success = true,
            data = new
            {
                parentName = parent.FirstName + " " + parent.LastName,
                unreadNotifications = unreadNotificationsCount,
                children = kidsSummaries,
                recentVideos = recentVideosWatched
            }
        });
    }

    [HttpGet("daily-tasks")]
    public async Task<IActionResult> GetChildDailyTasks([FromQuery] Guid childId)
    {
        var child = await _context.Children.FindAsync(childId);
        if (child == null)
        {
            return NotFound(new { success = false, message = "الطفل غير موجود" });
        }

        var startOfToday = DateTime.UtcNow.Date;
        var playSessionsToday = await _context.GameSessions
            .Where(gs => gs.ChildId == childId && gs.PlayedAt >= startOfToday)
            .ToListAsync();

        var tasks = new List<object>
        {
            new
            {
                taskId = "task_shapes",
                title = "لعب لعبة مطابقة الأشكال",
                description = "العب لعبة مطابقة الأشكال بظلالها لمدة 5 دقائق لتعزيز قوة الملاحظة.",
                targetGameKey = "shapes",
                isCompleted = playSessionsToday.Any(s => s.PlayedAt >= startOfToday)
            },
            new
            {
                taskId = "task_memory",
                title = "لعب لعبة الذاكرة البصرية",
                description = "حاول مطابقة الصور لتقوية الذاكرة العاملة.",
                targetGameKey = "memory",
                isCompleted = playSessionsToday.Any(s => s.PlayedAt >= startOfToday)
            },
            new
            {
                taskId = "task_video",
                title = "مشاهدة فيديو تفاعلي",
                description = "شاهد أحد الفيديوهات التعليمية المفيدة للأطفال.",
                targetGameKey = "video",
                isCompleted = false
            }
        };

        return Ok(new { success = true, data = tasks });
    }

    [HttpGet("recommendations")]
    public async Task<IActionResult> GetChildRecommendations([FromQuery] Guid childId)
    {
        var child = await _context.Children
            .Include(c => c.Skills)
            .FirstOrDefaultAsync(c => c.Id == childId);

        if (child == null)
        {
            return NotFound(new { success = false, message = "الطفل غير موجود" });
        }

        var lowestSkill = child.Skills
            .OrderBy(s => s.Progress)
            .FirstOrDefault();

        var recommendedGameKey = lowestSkill?.SkillKey ?? "colors";
        var recommendedGameLabel = recommendedGameKey switch
        {
            "colors" => "تمييز الألوان",
            "numbers" => "الألعاب التفاعلية",
            "letters" => "الوعي اللغوي",
            "shapes" => "الإدراك البصري",
            "memory" => "الذاكرة العاملة",
            _ => "الألعاب التفاعلية"
        };

        var game = await _context.Games
            .FirstOrDefaultAsync(g => g.Name.Equals(recommendedGameKey, StringComparison.OrdinalIgnoreCase) || g.Name.Equals(recommendedGameLabel, StringComparison.OrdinalIgnoreCase));

        var playlist = await _context.Playlists
            .Include(p => p.Videos)
            .Where(p => p.TargetAudience.ToLower() == "kids" && !p.IsDeleted)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();

        return Ok(new
        {
            success = true,
            data = new
            {
                focusSkillKey = recommendedGameKey,
                focusSkillLabel = recommendedGameLabel,
                recommendedGame = game ?? new Game { Name = recommendedGameLabel, Description = "لعبة تنمية المهارات الأساسية" },
                recommendedPlaylist = playlist != null ? new
                {
                    id = playlist.Id,
                    title = playlist.Title,
                    description = playlist.Description,
                    videosCount = playlist.Videos.Count
                } : null
            }
        });
    }

    [HttpGet("child-points-by-day")]
    public async Task<IActionResult> GetChildPointsByDay([FromQuery] Guid childId, [FromQuery] DateTime? date)
    {
        var child = await _context.Children.FindAsync(childId);
        if (child == null)
        {
            return NotFound(new { success = false, message = "الطفل غير موجود" });
        }

        if (date.HasValue)
        {
            var targetDate = date.Value.Date;
            var pointsEarned = await _context.GameSessions
                .Where(gs => gs.ChildId == childId && gs.CreatedAt.Date == targetDate)
                .SumAsync(gs => gs.Score);

            return Ok(new
            {
                success = true,
                childId = childId,
                childName = child.Name,
                date = targetDate.ToString("yyyy-MM-dd"),
                pointsEarned = pointsEarned
            });
        }
        else
        {
            var today = DateTime.UtcNow.Date;
            var last7Days = Enumerable.Range(0, 7)
                .Select(i => today.AddDays(-i))
                .ToList();

            var sessions = await _context.GameSessions
                .Where(gs => gs.ChildId == childId && gs.CreatedAt.Date >= today.AddDays(-6))
                .ToListAsync();

            var dailyBreakdown = last7Days.Select(d => new
            {
                date = d.ToString("yyyy-MM-dd"),
                dayOfWeek = d.ToString("dddd", new System.Globalization.CultureInfo("ar-EG")),
                pointsEarned = sessions.Where(gs => gs.CreatedAt.Date == d).Sum(gs => gs.Score)
            }).ToList();

            return Ok(new
            {
                success = true,
                childId = childId,
                childName = child.Name,
                dailyBreakdown = dailyBreakdown
            });
        }
    }
}
