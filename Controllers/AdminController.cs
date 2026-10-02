using System.Security.Claims;
using AlFolk.Api.DTOs;
using AlFolk.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlFolk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IAuthService _authService;

    public AdminController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
        {
            return Unauthorized();
        }

        var user = await _authService.GetMeAsync(userId);
        if (user == null) return NotFound();
        return Ok(new { success = true, data = user });
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
        {
            return Unauthorized();
        }

        var success = await _authService.UpdateProfileAsync(userId, request);
        
        if (!success)
        {
            return BadRequest(new { error = "update_failed", message = "فشل تحديث البيانات. تأكد من صحة كلمة المرور الحالية أو البريد الإلكتروني." });
        }

        var user = await _authService.GetMeAsync(userId);
        return Ok(new { success = true, data = user, admin = user });
    }

    [HttpDelete("reset-usage-data")]
    public async Task<IActionResult> ResetUsageData([FromServices] AlFolk.Api.Data.AppDbContext context)
    {
        // Require admin rights if necessary (assumed by the Authorize on the controller)
        // This resets fake points and usage data
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(context.Database, "UPDATE Children SET UsageHours = 0;");
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(context.Database, "UPDATE ChildSkills SET Points = 0, Progress = 0;");
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(context.Database, "DELETE FROM GameSessions;");
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(context.Database, "DELETE FROM VideoViews;");
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(context.Database, "DELETE FROM ActivityLogs;");
        
        return Ok(new { success = true, message = "تم تصفير جميع ساعات الاستخدام والنقاط بنجاح!" });
    }

    [HttpPost("simulate-activity")]
    public async Task<IActionResult> SimulateActivity([FromServices] AlFolk.Api.Data.AppDbContext context)
    {
        var random = new Random();
        var now = DateTime.UtcNow.Date;

        // Clear existing log/session data first to avoid compounding 
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(context.Database, "DELETE FROM GameSessions;");
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(context.Database, "DELETE FROM VideoViews;");
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(context.Database, "DELETE FROM ActivityLogs;");

        // Reset child usage hours and skills
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(context.Database, "UPDATE Children SET UsageHours = 0;");
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(context.Database, "UPDATE ChildSkills SET Points = 0, Progress = 20;");

        var parents = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(context.Users.Where(u => u.Role == "Parent"));
        var children = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(context.Children.Include(c => c.Skills));
        var games = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(context.Games);
        var videos = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(context.Videos);

        if (!parents.Any() || !children.Any() || !games.Any())
        {
            return BadRequest(new { success = false, message = "من فضلك تأكد من وجود أولياء أمور، أطفال، وألعاب في قاعدة البيانات أولاً." });
        }

        // Loop over the past 7 days (including today)
        for (int i = 0; i < 7; i++)
        {
            var date = now.AddDays(-i);
            
            // Random number of active parents on this day (e.g. 2 to 5)
            var activeParentsCount = random.Next(2, Math.Min(6, parents.Count + 1));
            var dayParents = parents.OrderBy(x => random.Next()).Take(activeParentsCount).ToList();

            foreach (var parent in dayParents)
            {
                // 1. Seed Login Activity
                var logTime = date.AddHours(random.Next(8, 22)).AddMinutes(random.Next(0, 60));
                context.ActivityLogs.Add(new AlFolk.Api.Models.ActivityLog
                {
                    UserId = parent.Id,
                    ActionType = "Login",
                    EntityType = "User",
                    ReferenceId = parent.Id,
                    CreatedAt = logTime
                });

                // Increment login count
                parent.LoginCount += 1;
                parent.LastLoginAt = logTime;

                // 2. Seed Parent Video View occasionally
                if (videos.Any() && random.Next(0, 2) == 0)
                {
                    var video = videos.FirstOrDefault(v => v.Playlist?.TargetAudience == "Parents") ?? videos.First();
                    context.VideoViews.Add(new AlFolk.Api.Models.VideoView
                    {
                        UserId = parent.Id,
                        VideoId = video.Id,
                        DurationWatchedSeconds = random.Next(120, 600),
                        WatchedAt = logTime.AddMinutes(random.Next(5, 30)),
                        CreatedAt = logTime.AddMinutes(random.Next(5, 30))
                    });
                }
            }

            // Random number of active kids on this day (e.g. 2 to 5)
            var activeKidsCount = random.Next(2, Math.Min(6, children.Count + 1));
            var dayKids = children.OrderBy(x => random.Next()).Take(activeKidsCount).ToList();

            foreach (var child in dayKids)
            {
                // Seed 1 to 3 game play sessions for this kid on this day
                var playSessionsCount = random.Next(1, 4);
                for (int j = 0; j < playSessionsCount; j++)
                {
                    var game = games[random.Next(games.Count)];
                    var score = random.Next(10, 50);
                    var durationMinutes = random.Next(3, 15);
                    var sessionTime = date.AddHours(random.Next(9, 21)).AddMinutes(random.Next(0, 60));

                    // Add Game Session
                    context.GameSessions.Add(new AlFolk.Api.Models.GameSession
                    {
                        ChildId = child.Id,
                        GameId = game.Id,
                        Score = score,
                        DurationSeconds = durationMinutes * 60,
                        PlayedAt = sessionTime,
                        CreatedAt = sessionTime
                    });

                    // Add Activity Log
                    context.ActivityLogs.Add(new AlFolk.Api.Models.ActivityLog
                    {
                        ChildId = child.Id,
                        ActionType = "PlayGame",
                        EntityType = "Game",
                        ReferenceId = game.Id,
                        CreatedAt = sessionTime
                    });

                    // Update Child usage & skill points
                    child.UsageHours += durationMinutes / 60.0;
                    
                    var skillKey = ArabicNormalization.GetSkillKeyFromGameName(game.Name);
                    var skill = child.Skills.FirstOrDefault(s => s.SkillKey.Equals(skillKey, StringComparison.OrdinalIgnoreCase));
                    if (skill != null)
                    {
                        skill.Points += score;
                        skill.Progress = Math.Min(100.0, skill.Progress + (score * 0.1));
                    }
                }

                // Seed Kid Video View occasionally
                if (videos.Any() && random.Next(0, 2) == 0)
                {
                    var video = videos.FirstOrDefault(v => v.Playlist?.TargetAudience == "Kids") ?? videos.First();
                    var viewTime = date.AddHours(random.Next(9, 21)).AddMinutes(random.Next(0, 60));
                    context.VideoViews.Add(new AlFolk.Api.Models.VideoView
                    {
                        ChildId = child.Id,
                        VideoId = video.Id,
                        DurationWatchedSeconds = random.Next(60, 400),
                        WatchedAt = viewTime,
                        CreatedAt = viewTime
                    });
                }
            }
        }

        await context.SaveChangesAsync();
        return Ok(new { success = true, message = "تم محاكاة وتعبئة بيانات النشاط التفاعلي لآخر 7 أيام بالكامل بنجاح! راجع لوحة التحكم الآن." });
    }
}
