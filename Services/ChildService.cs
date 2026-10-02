using System.Text.Json;
using AlFolk.Api.Data;
using AlFolk.Api.DTOs;
using AlFolk.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AlFolk.Api.Services;

public interface IChildService
{
    Task<PaginatedResponse<ChildListDto>> GetChildrenAsync(string? search, int page, int pageSize, string skillFilter, string sortBy, string sortDir);
    Task<ChildDetailDto?> GetChildByIdAsync(Guid id);
    Task<ChildDetailDto> CreateChildAsync(CreateChildRequest request);
    Task<bool> UpdateChildAsync(Guid id, UpdateChildRequest request);
    Task<bool> DeleteChildAsync(Guid id);
    Task<List<ChildListDto>> GetAllForReportAsync();
    Task<ScoreUpdateResponse?> UpdateScoreAsync(Guid childId, UpdateScoreRequest request);
    Task<int> GetTotalScoreAsync(Guid childId);
}

public class ChildService : IChildService
{
    private readonly AppDbContext _context;
    private readonly IActivityLogger _logger;

    public ChildService(AppDbContext context, IActivityLogger logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PaginatedResponse<ChildListDto>> GetChildrenAsync(string? search, int page, int pageSize, string skillFilter, string sortBy, string sortDir)
    {
        var allChildren = await _context.Children
            .Include(c => c.Parent)
            .Include(c => c.Skills)
            .Include(c => c.GameSessions).ThenInclude(gs => gs.Game)
            .ToListAsync();

        await EnsureChildrenHaveSkillsAndPointsAsync(allChildren);

        var gameSessionsDurations = await _context.GameSessions
            .GroupBy(gs => gs.ChildId)
            .Select(g => new { ChildId = g.Key, Duration = g.Sum(gs => gs.DurationSeconds) })
            .ToDictionaryAsync(x => x.ChildId, x => x.Duration);

        var videoViewsDurations = await _context.VideoViews
            .Where(vv => vv.ChildId != null)
            .GroupBy(vv => vv.ChildId)
            .Select(g => new { ChildId = g.Key!.Value, Duration = g.Sum(vv => vv.DurationWatchedSeconds) })
            .ToDictionaryAsync(x => x.ChildId, x => x.Duration);

        var enrichedChildren = allChildren.Select(c =>
        {
            gameSessionsDurations.TryGetValue(c.Id, out var gsDur);
            videoViewsDurations.TryGetValue(c.Id, out var vvDur);
            var calcHours = (gsDur + vvDur) / 3600.0;
            var usageHours = calcHours > 0 ? Math.Round(calcHours, 2) : Math.Round(c.UsageHours, 2);

            var skillsDict = ArabicNormalization.GetSkillsWithGameNames(c.Skills, c.GameSessions);
            var totalScore = skillsDict.Values.Sum(s => s.Pts);
            var avgProgress = (int)(skillsDict.Values.Any() ? skillsDict.Values.Average(s => s.Prg) : 0);

            return new
            {
                Child = c,
                TotalScore = totalScore,
                AvgProgress = avgProgress,
                UsageHours = usageHours,
                SkillsDict = skillsDict
            };
        })
        .OrderByDescending(x => x.TotalScore)
        .Select((x, index) => new { x.Child, x.TotalScore, x.AvgProgress, x.UsageHours, x.SkillsDict, Rank = index + 1 })
        .ToList();

        var query = enrichedChildren.AsEnumerable();

        if (!string.IsNullOrEmpty(search))
        {
            var normalizedSearch = ArabicNormalization.Normalize(search);
            query = query.Where(x => 
                ArabicNormalization.Normalize(x.Child.Name).Contains(normalizedSearch) || 
                ArabicNormalization.Normalize(x.Child.Parent.FirstName + " " + x.Child.Parent.LastName).Contains(normalizedSearch) || 
                (x.Child.Parent.Phone != null && x.Child.Parent.Phone.Contains(search)));
        }

        // Sorting
        if (sortBy == "totalScore")
            query = sortDir == "asc" ? query.OrderBy(x => x.TotalScore) : query.OrderByDescending(x => x.TotalScore);
        else if (sortBy == "name")
            query = sortDir == "asc" ? query.OrderBy(x => x.Child.Name) : query.OrderByDescending(x => x.Child.Name);
        else 
        {
            var lookupKey = sortBy;
            if (sortBy.Equals("letters", StringComparison.OrdinalIgnoreCase))
            {
                lookupKey = "Language";
            }
            query = sortDir == "asc" 
                ? query.OrderBy(x => {
                    var skill = x.Child.Skills.FirstOrDefault(s => s.SkillKey.Equals(lookupKey, StringComparison.OrdinalIgnoreCase));
                    return skill != null ? skill.Points : 0;
                }) 
                : query.OrderByDescending(x => {
                    var skill = x.Child.Skills.FirstOrDefault(s => s.SkillKey.Equals(lookupKey, StringComparison.OrdinalIgnoreCase));
                    return skill != null ? skill.Points : 0;
                });
        }

        var total = query.Count();
        var data = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ChildListDto(
                x.Child.Id,
                x.Child.Name,
                x.Child.ParentId,
                x.Child.Parent.FirstName + " " + x.Child.Parent.LastName,
                x.Child.Parent.Phone ?? "",
                x.Child.Age,
                x.UsageHours,
                x.TotalScore,
                x.AvgProgress,
                x.Rank,
                x.SkillsDict
            ))
            .ToList();

        return new PaginatedResponse<ChildListDto>(data, new PaginationMetadata(page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize)));
    }

    public async Task<ChildDetailDto?> GetChildByIdAsync(Guid id)
    {
        var child = await _context.Children
            .IgnoreQueryFilters()
            .Include(c => c.Parent)
            .Include(c => c.Skills)
            .Include(c => c.Medications)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (child == null) return null;

        var allChildren = await _context.Children.IgnoreQueryFilters().Include(c => c.Skills).ToListAsync();
        var rank = allChildren.OrderByDescending(c => c.Skills.Sum(s => s.Points)).ToList().FindIndex(c => c.Id == id) + 1;

        var gameSessionsDuration = await _context.GameSessions.Where(gs => gs.ChildId == id).SumAsync(gs => gs.DurationSeconds);
        var videoViewsDuration = await _context.VideoViews.Where(vv => vv.ChildId == id).SumAsync(vv => vv.DurationWatchedSeconds);
        var calculatedUsageHours = (gameSessionsDuration + videoViewsDuration) / 3600.0;
        var usageHours = calculatedUsageHours > 0 ? Math.Round(calculatedUsageHours, 2) : Math.Round(child.UsageHours, 2);

        var meds = child.Medications.Select(m => new MedicationDto(m.Id, m.Name, m.Dosage, m.ScheduleJson, m.Frequency, m.Notes)).ToList();

        var watchedVideos = await _context.VideoViews
            .Include(vv => vv.Video)
            .Where(vv => vv.ChildId == id)
            .Select(vv => new WatchedVideoDto(
                vv.VideoId,
                vv.Video.Title,
                vv.Video.ThumbnailUrl,
                vv.Video.Category,
                vv.Video.DurationSeconds,
                vv.DurationWatchedSeconds,
                vv.WatchedAt
            ))
            .ToListAsync();

        var gameSessions = await _context.GameSessions.Include(gs => gs.Game).Where(gs => gs.ChildId == id).ToListAsync();
        string bestGame = "-";
        var nonScoringGames = new[] { "خربش الصور", "ارسم ولون" };
        var validSessions = gameSessions.Where(gs => gs.Game != null && !nonScoringGames.Contains(gs.Game.Name)).ToList();
        
        if (validSessions.Any())
        {
            bestGame = validSessions
                .GroupBy(gs => ArabicNormalization.NormalizeGameName(gs.Game.Name))
                .Select(g => new { GameName = g.Key, TotalScore = g.Sum(gs => gs.Score) })
                .OrderByDescending(g => g.TotalScore)
                .FirstOrDefault()?.GameName ?? "-";
        }
        else if (child.Skills.Any(s => s.Points > 0))
        {
            var topSkill = child.Skills.OrderByDescending(s => s.Points).FirstOrDefault();
            if (topSkill != null)
            {
                // Map skill key to canonical Arabic game name
                var key = topSkill.SkillKey.Equals("Language", StringComparison.OrdinalIgnoreCase)
                    ? "letters" : topSkill.SkillKey.ToLower();
                bestGame = key switch
                {
                    "colors"  => "ارسم ولون",
                    "letters" => "اصوات حيوانات",
                    "shapes"  => "الشكل و ظله",
                    "memory"  => "الذاكره",
                    _         => "-"
                };
            }
        }

        var skillsDict = ArabicNormalization.GetSkillsWithGameNames(child.Skills, gameSessions);
        var totalScore = skillsDict.Values.Sum(s => s.Pts);
        var avgProgress = (int)(skillsDict.Values.Any() ? skillsDict.Values.Average(s => s.Prg) : 0);

        return new ChildDetailDto(
            child.Id,
            child.Name,
            child.ParentId,
            child.Parent?.FirstName + " " + child.Parent?.LastName ?? "N/A",
            child.Parent?.Phone ?? "N/A",
            child.Age,
            usageHours,
            meds,
            rank,
            totalScore,
            avgProgress,
            skillsDict,
            watchedVideos,
            child.NationalId,
            child.Gender,
            bestGame
        );
    }

    public async Task<ChildDetailDto> CreateChildAsync(CreateChildRequest request)
    {
        var parentExists = await _context.Users.AnyAsync(u => u.Id == request.ParentId && u.Role == "Parent");
        if (!parentExists)
        {
            throw new ArgumentException("عفواً، ولي الأمر المحدد غير موجود في النظام. برجاء التحقق من كود ولي الأمر.");
        }

        var nationalId = NormalizeInput(request.NationalId);
        if (nationalId != null)
        {
            var nationalIdExists = await _context.Children
                .IgnoreQueryFilters()
                .AnyAsync(c => c.NationalId == nationalId);
            if (nationalIdExists)
            {
                throw new ArgumentException("عفواً، الرقم القومي المدخل مسجل بالفعل لطفل آخر.");
            }
        }

        var child = new Child
        {
            Name = request.Name,
            ParentId = request.ParentId,
            Age = request.Age,
            Gender = request.Gender,
            NationalId = nationalId
        };

        var skillKeys = new[] { "Memory", "Colors", "Language", "Shapes" };
        foreach (var sk in skillKeys)
        {
            child.Skills.Add(new ChildSkill { SkillKey = sk, Points = 0, Progress = 0 });
        }

        _context.Children.Add(child);
        await _context.SaveChangesAsync();

        await _logger.LogAsync("CREATE", "Child", child.Id, $"Created child {child.Name}");

        return (await GetChildByIdAsync(child.Id))!;
    }

    public async Task<bool> UpdateChildAsync(Guid id, UpdateChildRequest request)
    {
        var child = await _context.Children.Include(c => c.Skills).Include(c => c.Medications).FirstOrDefaultAsync(c => c.Id == id);
        if (child == null) return false;

        var parentExists = await _context.Users.AnyAsync(u => u.Id == request.ParentId && u.Role == "Parent");
        if (!parentExists)
        {
            throw new ArgumentException("عفواً، ولي الأمر المحدد غير موجود في النظام. برجاء التحقق من كود ولي الأمر.");
        }

        var nationalId = NormalizeInput(request.NationalId);
        if (nationalId != null)
        {
            var nationalIdExists = await _context.Children
                .IgnoreQueryFilters()
                .AnyAsync(c => c.NationalId == nationalId && c.Id != id);
            if (nationalIdExists)
            {
                throw new ArgumentException("عفواً، الرقم القومي المدخل مسجل بالفعل لطفل آخر.");
            }
        }

        child.Name = request.Name;
        child.ParentId = request.ParentId;
        child.Age = request.Age;
        child.Gender = request.Gender;
        child.NationalId = nationalId;

        await _context.SaveChangesAsync();
        await _logger.LogAsync("UPDATE", "Child", child.Id, $"Updated child {child.Name}");
        
        return true;
    }

    public async Task<bool> DeleteChildAsync(Guid id)
    {
        var child = await _context.Children.FindAsync(id);
        if (child == null) return false;

        child.IsDeleted = true;
        child.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _logger.LogAsync("DELETE", "Child", child.Id, $"Deleted child {child.Name}");
        
        return true;
    }

    public async Task<List<ChildListDto>> GetAllForReportAsync()
    {
        var response = await GetChildrenAsync(null, 1, int.MaxValue, "all", "totalScore", "desc");
        return response.Data;
    }

    public async Task<ScoreUpdateResponse?> UpdateScoreAsync(Guid childId, UpdateScoreRequest request)
    {
        var child = await _context.Children.Include(c => c.Skills).FirstOrDefaultAsync(c => c.Id == childId);
        if (child == null) return null;

        var nonScoringGames = new[] { "خربش الصور", "ارسم ولون", "scribble", "animalcoloring", "drawandcolor", "coloring", "doodle" };
        if (nonScoringGames.Contains(request.SkillKey.ToLower().Trim()))
        {
            return new ScoreUpdateResponse(0, 0, child.Skills.Sum(s => s.Points));
        }

        var mappedSkillKey = ArabicNormalization.GetSkillKeyFromGameName(request.SkillKey);
        var normalizedGameName = ArabicNormalization.NormalizeGameName(request.SkillKey);
        
        var skill = child.Skills.FirstOrDefault(s => s.SkillKey.Equals(mappedSkillKey, StringComparison.OrdinalIgnoreCase));
        int oldPoints = 0;
        if (skill == null)
        {
            skill = new ChildSkill
            {
                ChildId = child.Id,
                SkillKey = mappedSkillKey,
                Points = request.PointsToAdd,
                Progress = request.NewProgress
            };
            child.Skills.Add(skill);
        }
        else
        {
            oldPoints = skill.Points;
            skill.Points += request.PointsToAdd;
            skill.Progress = request.NewProgress;
        }

        // Find or create Game for the GameSession
        var game = await _context.Games.FirstOrDefaultAsync(g => g.Name == normalizedGameName);
        if (game == null)
        {
            game = new Game { Name = normalizedGameName };
            _context.Games.Add(game);
            await _context.SaveChangesAsync();
        }

        // Create GameSession to ensure points appear in dashboard game columns instantly
        var session = new GameSession
        {
            ChildId = child.Id,
            GameId = game.Id,
            Score = request.PointsToAdd,
            DurationSeconds = 0, // No duration provided in this endpoint
            PlayedAt = DateTime.UtcNow
        };
        _context.GameSessions.Add(session);
        
        await _context.SaveChangesAsync();
        
        int newTotalScore = child.Skills.Sum(s => s.Points);
        
        await _logger.LogAsync("UPDATE_SCORE", "Child", child.Id, $"Updated score for {request.SkillKey} ({mappedSkillKey}): {oldPoints} -> {skill.Points} (Total: {newTotalScore})");
        
        return new ScoreUpdateResponse(oldPoints, skill.Points, newTotalScore);
    }

    public async Task<int> GetTotalScoreAsync(Guid childId)
    {
        var child = await _context.Children.Include(c => c.Skills).Include(c => c.GameSessions).ThenInclude(gs => gs.Game).FirstOrDefaultAsync(c => c.Id == childId);
        if (child == null) return 0;

        var skillsDict = ArabicNormalization.GetSkillsWithGameNames(child.Skills, child.GameSessions);
        return skillsDict.Values.Sum(s => s.Pts);
    }

    private async Task EnsureChildrenHaveSkillsAndPointsAsync(List<Child> children)
    {
        bool changed = false;

        foreach (var c in children)
        {
            if (!c.Skills.Any())
            {
                var skillKeys = new[] { "Memory", "Colors", "Language", "Shapes" };
                foreach (var sk in skillKeys)
                {
                    var newSkill = new ChildSkill
                    {
                        Id = Guid.NewGuid(),
                        ChildId = c.Id,
                        SkillKey = sk,
                        Points = 0,
                        Progress = 0
                    };
                    _context.ChildSkills.Add(newSkill);
                    c.Skills.Add(newSkill);
                }
                changed = true;
            }
        }

        if (changed)
        {
            await _context.SaveChangesAsync();
        }
    }

    private string? NormalizeInput(string? val)
    {
        if (string.IsNullOrWhiteSpace(val) || val.Equals("string", StringComparison.OrdinalIgnoreCase))
            return null;
        return val.Trim();
    }
}
