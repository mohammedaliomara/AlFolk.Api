using AlFolk.Api.Data;
using AlFolk.Api.DTOs;
using AlFolk.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AlFolk.Api.Services;

public interface IParentService
{
    Task<PaginatedResponse<ParentListDto>> GetParentsAsync(string? search, int page, int pageSize, string sortBy, string sortDir);
    Task<ParentDetailDto?> GetParentByIdAsync(Guid id);
    Task<ParentDetailDto> CreateParentAsync(CreateParentRequest request);
    Task<bool> UpdateParentAsync(Guid id, UpdateParentRequest request);
    Task<bool> DeleteParentAsync(Guid id);
    Task<List<ParentListDto>> GetAllForReportAsync();
}

public class ParentService : IParentService
{
    private readonly AppDbContext _context;
    private readonly IActivityLogger _logger;

    public ParentService(AppDbContext context, IActivityLogger logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PaginatedResponse<ParentListDto>> GetParentsAsync(string? search, int page, int pageSize, string sortBy, string sortDir)
    {
        var query = _context.Users.Where(u => u.Role == "Parent").AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            var normalizedSearch = ArabicNormalization.Normalize(search);
            query = query.Where(u => u.FirstName.Contains(search) || u.LastName.Contains(search) || u.Phone!.Contains(search));
        }

        // Sorting
        query = sortDir.ToLower() == "asc" 
            ? query.OrderBy(u => EF.Property<object>(u, sortBy)) 
            : query.OrderByDescending(u => EF.Property<object>(u, sortBy));

        var total = await query.CountAsync();
        var data = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new ParentListDto(
                u.Id,
                u.FirstName + " " + u.LastName,
                u.Phone ?? "",
                u.Email,
                u.NationalId,
                u.LoginCount,
                u.Children.Count,
                u.Children.Select(c => c.Name).ToList(),
                _context.VideoViews.Count(vv =>
                    vv.UserId == u.Id ||
                    (vv.ChildId != null && u.Children.Select(c => c.Id).Contains(vv.ChildId.Value))),
                u.Children.Sum(c => (int?)c.Skills.Sum(s => s.Points)) ?? 0,
                u.CreatedAt
            ))
            .ToListAsync();

        return new PaginatedResponse<ParentListDto>(data, new PaginationMetadata(page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize)));
    }

    public async Task<ParentDetailDto?> GetParentByIdAsync(Guid id)
    {
        var u = await _context.Users
            .Include(u => u.Children).ThenInclude(c => c.Skills)
            .Include(u => u.Children).ThenInclude(c => c.GameSessions).ThenInclude(gs => gs.Game)
            .FirstOrDefaultAsync(u => u.Id == id && u.Role == "Parent");

        if (u == null) return null;

        var watchedVideos = await _context.VideoViews
            .Include(vv => vv.Video)
            .Where(vv => vv.UserId == u.Id)
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

        var childIds = u.Children.Select(c => c.Id).ToList();
        var gameSessionsDuration = await _context.GameSessions
            .Where(gs => childIds.Contains(gs.ChildId))
            .SumAsync(gs => gs.DurationSeconds);
        var videoViewsDuration = await _context.VideoViews
            .Where(vv => vv.ChildId != null && childIds.Contains(vv.ChildId.Value))
            .SumAsync(vv => vv.DurationWatchedSeconds);
        
        var calculatedHours = (gameSessionsDuration + videoViewsDuration) / 3600.0;
        var totalUsageHours = calculatedHours > 0 ? calculatedHours : u.Children.Sum(c => c.UsageHours);
        totalUsageHours = Math.Round(totalUsageHours, 2);

        var totalChildrenScore = u.Children.Sum(c => 
            ArabicNormalization.GetSkillsWithGameNames(c.Skills, c.GameSessions).Values.Sum(s => s.Pts));

        return new ParentDetailDto(
            u.Id,
            u.FirstName + " " + u.LastName,
            u.Phone ?? "",
            u.Email,
            u.NationalId,
            u.LoginCount,
            watchedVideos,
            u.Children.Select(c => new ChildSummaryDto(
                c.Id,
                c.Name,
                c.Age,
                ArabicNormalization.GetSkillsWithGameNames(c.Skills, c.GameSessions).Values.Sum(s => s.Pts),
                (int)(ArabicNormalization.GetSkillsWithGameNames(c.Skills, c.GameSessions).Values.Any() 
                    ? ArabicNormalization.GetSkillsWithGameNames(c.Skills, c.GameSessions).Values.Average(s => s.Prg) : 0)
            )).ToList(),
            totalUsageHours,
            totalChildrenScore
        );
    }

    public async Task<ParentDetailDto> CreateParentAsync(CreateParentRequest request)
    {
        var email = NormalizeInput(request.Email);
        var phone = NormalizeInput(request.Phone);
        var nationalId = NormalizeInput(request.NationalId);

        if (email != null && await _context.Users.AnyAsync(u => u.Email == email))
        {
            throw new InvalidOperationException("البريد الإلكتروني مستخدم بالفعل من قبل مستخدم آخر.");
        }

        if (phone != null && await _context.Users.AnyAsync(u => u.Phone == phone))
        {
            throw new InvalidOperationException("رقم الهاتف مستخدم بالفعل من قبل مستخدم آخر.");
        }

        if (nationalId != null && await _context.Users.AnyAsync(u => u.NationalId == nationalId))
        {
            throw new InvalidOperationException("الرقم القومي مستخدم بالفعل من قبل مستخدم آخر.");
        }

        var names = request.Name.Split(' ', 2);
        var user = new User
        {
            FirstName = names[0],
            LastName = names.Length > 1 ? names[1] : "",
            Phone = phone,
            Email = email,
            NationalId = nationalId,
            Role = "Parent",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(!string.IsNullOrEmpty(request.Password) && !request.Password.Equals("string", StringComparison.OrdinalIgnoreCase) ? request.Password : "123456")
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await _logger.LogAsync("CREATE", "Parent", user.Id, $"Created parent {user.FirstName} {user.LastName}");

        return (await GetParentByIdAsync(user.Id))!;
    }

    public async Task<bool> UpdateParentAsync(Guid id, UpdateParentRequest request)
    {
        var user = await _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Children)
            .FirstOrDefaultAsync(u => u.Id == id && u.Role == "Parent");

        if (user == null) return false;

        var email = NormalizeInput(request.Email);
        var phone = NormalizeInput(request.Phone);
        var nationalId = NormalizeInput(request.NationalId);

        if (email != null && await _context.Users.AnyAsync(u => u.Email == email && u.Id != id))
        {
            throw new InvalidOperationException("البريد الإلكتروني مستخدم بالفعل من قبل مستخدم آخر.");
        }

        if (phone != null && await _context.Users.AnyAsync(u => u.Phone == phone && u.Id != id))
        {
            throw new InvalidOperationException("رقم الهاتف مستخدم بالفعل من قبل مستخدم آخر.");
        }

        if (nationalId != null && await _context.Users.AnyAsync(u => u.NationalId == nationalId && u.Id != id))
        {
            throw new InvalidOperationException("الرقم القومي مستخدم بالفعل من قبل مستخدم آخر.");
        }

        var names = request.Name.Split(' ', 2);
        user.IsDeleted = false;
        user.DeletedAt = null;
        user.FirstName = names[0];
        user.LastName = names.Length > 1 ? names[1] : "";
        user.Phone = phone;
        user.Email = email;
        user.NationalId = nationalId;

        await _context.SaveChangesAsync();
        await _logger.LogAsync("UPDATE", "Parent", user.Id, $"Updated/Restored parent {user.FirstName} {user.LastName}");
        
        return true;
    }

    private string? NormalizeInput(string? val)
    {
        if (string.IsNullOrWhiteSpace(val) || val.Equals("string", StringComparison.OrdinalIgnoreCase))
            return null;
        return val.Trim();
    }

    public async Task<bool> DeleteParentAsync(Guid id)
    {
        var user = await _context.Users.Include(u => u.Children).FirstOrDefaultAsync(u => u.Id == id && u.Role == "Parent");
        if (user == null) return false;

        var now = DateTime.UtcNow;
        user.IsDeleted = true;
        user.DeletedAt = now;
        
        foreach (var child in user.Children)
        {
            child.IsDeleted = true;
            child.DeletedAt = now;
        }

        await _context.SaveChangesAsync();
        await _logger.LogAsync("DELETE", "Parent", user.Id, $"Deleted parent {user.FirstName} {user.LastName}");
        
        return true;
    }

    public async Task<List<ParentListDto>> GetAllForReportAsync()
    {
        return await _context.Users
            .Where(u => u.Role == "Parent")
            .Select(u => new ParentListDto(
                u.Id,
                u.FirstName + " " + u.LastName,
                u.Phone ?? "",
                u.Email,
                u.NationalId,
                u.LoginCount,
                u.Children.Count,
                u.Children.Select(c => c.Name).ToList(),
                _context.VideoViews.Count(vv =>
                    vv.UserId == u.Id ||
                    (vv.ChildId != null && u.Children.Select(c => c.Id).Contains(vv.ChildId.Value))),
                u.Children.Sum(c => (int?)c.Skills.Sum(s => s.Points)) ?? 0,
                u.CreatedAt
            ))
            .ToListAsync();
    }
}

public static class ArabicNormalization
{
    public static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.ToLower()
            .Replace("أ", "ا").Replace("إ", "ا").Replace("آ", "ا")
            .Replace("ة", "ه")
            .Replace("ى", "ي")
            .Trim();
    }

    public static string NormalizeGameName(string gameName)
    {
        if (string.IsNullOrEmpty(gameName)) return gameName;
        
        var normalized = gameName.ToLower().Trim().Replace(" ", "").Replace("_", "").Replace("-", "");
        
        // Xylophone
        if (normalized == "xylophone" || normalized == "xi") return "اكسليفون";
        
        // Colors / Drawing
        if (normalized == "drawandcolor" || normalized == "drawing" ||
            normalized == "animalcoloring" || normalized == "coloring") return "ارسم ولون";
        
        // Scribble → خربش الصور
        if (normalized == "scribble" || normalized == "scribbles" || 
            normalized == "kharbash" || normalized == "doodle") return "خربش الصور";
        
        // Piano
        if (normalized == "piano") return "بيانو";
        
        // Memory
        if (normalized == "memorymatch" || normalized == "memory" || 
            normalized == "memorymatching" || normalized == "matchcards" ||
            normalized == "cardmatch" || normalized == "memorycard") return "الذاكره";
        
        // Puzzle → ركب الصور
        if (normalized == "puzzle" || normalized == "jigsaw" ||
            normalized == "puzzles" || normalized == "rakabelsour") return "ركب الصور";
        
        // Shapes / Shadow
        if (normalized == "shapes" || normalized == "shapesgame" || 
            normalized == "shapeandshadow" || normalized == "shapeshadow" ||
            normalized == "shadow") return "الشكل و ظله";
        
        // Animal Sounds
        if (normalized == "animalsounds" || normalized == "animals" ||
            normalized == "animalsound") return "اصوات حيوانات";
        
        // Color Sorting
        if (normalized == "colorsort" || normalized == "colorsorting" || 
            normalized == "sortcolors") return "لون الصوره";

        // الألوان — standalone colors game (includes تمييز الألوان)
        if (normalized == "colors" || normalized == "colorsgame" || normalized == "color" ||
            normalized == "الالوان" || normalized == "الألوان" || 
            normalized == "تمييزالالوان" || normalized == "تمييزالألوان" ||
            normalized == "alwan" || normalized == "alwaan" ||
            normalized == "colors2" || normalized == "colorsgame2") return "الألوان";
        
        return gameName;
    }


    public static string GetSkillKeyFromGameName(string gameName)
    {
        if (string.IsNullOrEmpty(gameName)) return "memory";
        
        var normalized = Normalize(NormalizeGameName(gameName));
        
        // Colors mapping: "لون الصوره", "ارسم ولون", "خربش الصور"
        if (normalized.Contains("color") || 
            normalized.Contains("تلوين") || 
            normalized.Contains("الوان") || 
            normalized.Contains("رسم") || 
            normalized.Contains("خربش") || 
            normalized.Contains("لون"))
            return "colors";
            
        // Memory mapping: "الذاكره", "Memory Match"
        if (normalized.Contains("memory") || 
            normalized.Contains("ذاكره"))
            return "memory";
            
        // Shapes mapping: "الشكل و ظله", "ركب الصور"
        if (normalized.Contains("ركب") || 
            normalized.Contains("puzzle") ||
            normalized.Contains("shape") || 
            normalized.Contains("اشكال") || 
            normalized.Contains("شكل") || 
            normalized.Contains("ظل"))
            return "shapes";
            
        // Language / Auditory mapping: "اصوات حيوانات", "بيانو", "اكسليفون"
        if (normalized.Contains("letter") || 
            normalized.Contains("لغه") || 
            normalized.Contains("حروف") || 
            normalized.Contains("صوت") || 
            normalized.Contains("اصوات") || 
            normalized.Contains("بيانو") || 
            normalized.Contains("اكسليفون") || 
            normalized.Contains("كلام"))
            return "letters";

        return "memory"; // safe fallback
    }

    // All 10 canonical Arabic game names in display order
    private static readonly (string GameName, string SkillKey)[] AllGames = new[]
    {
        ("خربش الصور",    "colors"),
        ("ارسم ولون",     "colors"),
        ("لون الصوره",    "colors"),
        ("الألوان",        "colors"),
        ("اصوات حيوانات", "letters"),
        ("بيانو",          "letters"),
        ("اكسليفون",      "letters"),
        ("الذاكره",        "memory"),
        ("الشكل و ظله",   "shapes"),
        ("ركب الصور",     "shapes"),
    };

    public static Dictionary<string, SkillDto> GetSkillsWithGameNames(
        ICollection<ChildSkill> childSkills,
        ICollection<GameSession> gameSessions = null)
    {
        // Build a lookup: normalised skillKey → SkillDto (pts/prg from ChildSkills table)
        var skillLookup = new Dictionary<string, SkillDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in childSkills)
        {
            var key = s.SkillKey.Equals("Language", StringComparison.OrdinalIgnoreCase)
                ? "letters"
                : s.SkillKey.ToLower();
            // Merge if duplicates exist (e.g. both "Language" and "letters")
            if (!skillLookup.ContainsKey(key))
                skillLookup[key] = new SkillDto(s.Points, (int)s.Progress);
            else
                skillLookup[key] = new SkillDto(
                    skillLookup[key].Pts + s.Points,
                    Math.Max(skillLookup[key].Prg, (int)s.Progress));
        }

        // Return ONLY Arabic game name keys — one entry per game
        var nonScoringGames = new[] { "خربش الصور", "ارسم ولون" };
        var dict = new Dictionary<string, SkillDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var (gameName, skillKey) in AllGames)
        {
            if (nonScoringGames.Contains(gameName))
            {
                dict[gameName] = new SkillDto(0, 0);
                continue;
            }

            if (gameSessions != null)
            {
                var normalizedTarget = NormalizeGameName(gameName);
                var gameScore = gameSessions
                    .Where(gs => gs.Game != null &&
                                 NormalizeGameName(gs.Game.Name).Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase))
                    .Sum(gs => gs.Score);
                var gameProgress = Math.Min(100, (int)Math.Round((gameScore / 150.0) * 100));
                dict[gameName] = new SkillDto(gameScore, gameProgress);
            }
            else
            {
                // Fallback to parent skill aggregate when no sessions available
                skillLookup.TryGetValue(skillKey, out var fallback);
                dict[gameName] = fallback ?? new SkillDto(0, 0);
            }
        }

        return dict;
    }

    // Returns a flat ordered list of all 9 Arabic games with points/progress
    public static List<object> GetAllGamesFlat(ICollection<ChildSkill> childSkills, ICollection<GameSession> gameSessions = null)
    {
        var skillToGames = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "colors",   new[] { "خربش الصور", "ارسم ولون", "لون الصوره", "الألوان" } },
            { "language", new[] { "اصوات حيوانات", "بيانو", "اكسليفون" } },
            { "letters",  new[] { "اصوات حيوانات", "بيانو", "اكسليفون" } },
            { "memory",   new[] { "الذاكره" } },
            { "shapes",   new[] { "الشكل و ظله", "ركب الصور" } }
        };

        var result = new List<object>();
        var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var s in childSkills)
        {
            var frontendKey = s.SkillKey.Equals("Language", StringComparison.OrdinalIgnoreCase)
                ? "letters"
                : s.SkillKey.ToLower();

            if (!skillToGames.TryGetValue(frontendKey, out var games))
                skillToGames.TryGetValue(s.SkillKey, out games);

            if (games == null) continue;

            foreach (var gameName in games)
            {
                if (!seen.Add(gameName)) continue; // deduplicate (language/letters overlap)
                
                var nonScoringGames = new[] { "خربش الصور", "ارسم ولون" };
                if (nonScoringGames.Contains(gameName))
                {
                    result.Add(new
                    {
                        gameName   = gameName,
                        skillKey   = frontendKey,
                        points     = 0,
                        progress   = 0
                    });
                    continue;
                }

                var normalizedTarget = NormalizeGameName(gameName);
                var gameScore = gameSessions != null
                    ? gameSessions.Where(gs => gs.Game != null && NormalizeGameName(gs.Game.Name).Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase)).Sum(gs => gs.Score)
                    : s.Points; // fallback
                    
                var gameProgress = gameSessions != null
                    ? Math.Min(100, (int)Math.Round((gameScore / 150.0) * 100))
                    : (int)s.Progress;

                result.Add(new
                {
                    gameName   = gameName,
                    skillKey   = frontendKey,
                    points     = gameScore,
                    progress   = gameProgress
                });
            }
        }

        return result;
    }
}
