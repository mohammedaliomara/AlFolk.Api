using AlFolk.Api.Data;
using AlFolk.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlFolk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api")]
public class GamesController : ControllerBase
{
    private readonly AppDbContext _context;

    public GamesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("games")]
    public async Task<IActionResult> GetGames()
    {
        var games = await _context.Games
            .Where(g => g.IsActive)
            .OrderBy(g => g.Name)
            .Select(g => new
            {
                id          = g.Id,
                name        = g.Name,
                skillKey    = g.SkillKey,
                description = g.Description,
                thumbnail   = g.ThumbnailUrl
            })
            .ToListAsync();

        return Ok(new { success = true, data = games });
    }

    [HttpGet("games/{gameKey}")]
    public async Task<IActionResult> GetGameDetails(string gameKey)
    {
        var game = await _context.Games.FirstOrDefaultAsync(g => g.Name.Equals(gameKey, StringComparison.OrdinalIgnoreCase));
        if (game == null)
        {
            return NotFound(new { success = false, message = "اللعبة غير موجودة" });
        }
        return Ok(new { success = true, data = game });
    }

    [HttpGet("skills")]
    public IActionResult GetSkillsList()
    {
        var games = new[]
        {
            // Colors skill → 4 games
            new { key = "colors",  gameName = "خربش الصور",    label = "تمييز الألوان",    description = "لعبة خربشة الصور لاكتشاف الأشكال المخفية وتطوير التمييز البصري للألوان." },
            new { key = "colors",  gameName = "ارسم ولون",     label = "تمييز الألوان",    description = "لعبة رسم وتلوين حر لتعزيز الإبداع الفني وتنمية مهارات تنسيق الألوان." },
            new { key = "colors",  gameName = "لون الصوره",    label = "تمييز الألوان",    description = "لعبة تلوين الرسومات الجاهزة لتنمية مهارات فرز الألوان والتلوين الدقيق." },
            new { key = "colors",  gameName = "الألوان",        label = "تمييز الألوان",    description = "لعبة تعليمية لتعلم الألوان وتمييزها وتطوير مهارات التعرف عليها." },
            // Language/Auditory skill → 3 games
            new { key = "letters", gameName = "اصوات حيوانات", label = "الوعي اللغوي",    description = "لعبة التعرف على أصوات الحيوانات المختلفة لتطوير الوعي السمعي واللغوي." },
            new { key = "letters", gameName = "بيانو",          label = "الوعي اللغوي",    description = "لعبة عزف البيانو التفاعلية لتنمية الحس الموسيقي والتمييز السمعي للنغمات." },
            new { key = "letters", gameName = "اكسليفون",       label = "الوعي اللغوي",    description = "لعبة عزف الأكسليفون لتطوير التنسيق الحركي البصري والتمييز السمعي." },
            // Memory skill → 1 game
            new { key = "memory",  gameName = "الذاكره",        label = "الذاكرة العاملة", description = "لعبة كروت الذاكرة لتقوية مهارات التذكر والمطابقة الثنائية للعناصر." },
            // Shapes skill → 2 games
            new { key = "shapes",  gameName = "الشكل و ظله",   label = "الإدراك البصري",  description = "لعبة مطابقة الأشكال بظلالها لتعزيز قوة الملاحظة والإدراك البصري الفراغي." },
            new { key = "shapes",  gameName = "ركب الصور",      label = "الإدراك البصري",  description = "لعبة تركيب قطع الصور (البازل) لتطوير مهارات حل المشكلات والتنسيق البصري." }
        };
        return Ok(new { success = true, data = games });
    }

    [HttpPost("skills/progress")]
    public async Task<IActionResult> UpdateSkillProgress([FromBody] UpdateProgressRequest request)
    {
        var child = await _context.Children.Include(c => c.Skills).FirstOrDefaultAsync(c => c.Id == request.ChildId);
        if (child == null)
        {
            return NotFound(new { success = false, message = "الطفل غير موجود" });
        }

        var skill = child.Skills.FirstOrDefault(s => s.SkillKey.Equals(request.SkillKey, StringComparison.OrdinalIgnoreCase));
        if (skill == null)
        {
            skill = new ChildSkill
            {
                ChildId = child.Id,
                SkillKey = request.SkillKey,
                Points = 0,
                Progress = Math.Min(100.0, request.ProgressIncrement)
            };
            child.Skills.Add(skill);
        }
        else
        {
            skill.Progress = Math.Min(100.0, skill.Progress + request.ProgressIncrement);
        }

        await _context.SaveChangesAsync();
        return Ok(new { success = true, message = "تم تحديث مستوى تقدم المهارة بنجاح", currentProgress = skill.Progress });
    }
}

public record UpdateProgressRequest(Guid ChildId, string SkillKey, double ProgressIncrement);
