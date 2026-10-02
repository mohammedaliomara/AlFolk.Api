using AlFolk.Api.Data;
using AlFolk.Api.DTOs;
using AlFolk.Api.Models;
using AlFolk.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlFolk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GameSessionsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IActivityLogger _logger;

    public GameSessionsController(AppDbContext context, IActivityLogger logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateSession([FromBody] GameSessionRequest request)
    {
        var child = await _context.Children.Include(c => c.Skills).FirstOrDefaultAsync(c => c.Id == request.ChildId);
        if (child == null) return NotFound(new { message = "Child not found" });

        var normalizedGameName = ArabicNormalization.NormalizeGameName(request.GameName);

        // Find or create Game
        var game = await _context.Games.FirstOrDefaultAsync(g => g.Name == normalizedGameName);
        if (game == null)
        {
            game = new Game { Name = normalizedGameName };
            _context.Games.Add(game);
            await _context.SaveChangesAsync();
        }

        // Create GameSession record
        var session = new GameSession
        {
            ChildId = child.Id,
            GameId = game.Id,
            Score = request.Score,
            DurationSeconds = request.DurationSeconds,
            PlayedAt = DateTime.UtcNow
        };
        _context.GameSessions.Add(session);

        // Update Skill Points
        var mappedSkillKey = ArabicNormalization.GetSkillKeyFromGameName(normalizedGameName);
        var skill = child.Skills.FirstOrDefault(s => s.SkillKey.Equals(mappedSkillKey, StringComparison.OrdinalIgnoreCase));
        int oldPoints = 0;
        if (skill == null)
        {
            skill = new ChildSkill
            {
                ChildId = child.Id,
                SkillKey = mappedSkillKey,
                Points = request.Score,
                Progress = 10.0
            };
            child.Skills.Add(skill);
        }
        else
        {
            oldPoints = skill.Points;
            skill.Points += request.Score;
            skill.Progress = Math.Min(100.0, skill.Progress + (request.Score * 0.1));
        }
        
        await _context.SaveChangesAsync();

        int newTotalScore = child.Skills.Sum(s => s.Points);

        await _logger.LogAsync("GAME_SESSION", "Child", child.Id, 
            $"Finished session for {request.GameName} ({mappedSkillKey}): +{request.Score} pts (Total: {newTotalScore})");

        return Ok(new GameSessionResponse(
            child.Id,
            request.GameName,
            request.Score,
            skill.Points,
            newTotalScore
        ));
    }
}
