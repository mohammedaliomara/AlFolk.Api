using System.Security.Claims;
using AlFolk.Api.Data;
using AlFolk.Api.Models;

namespace AlFolk.Api.Services;

public interface IActivityLogger
{
    Task LogAsync(string action, string entityType, Guid entityId, string? details = null);
}

public class ActivityLogger : IActivityLogger
{
    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ActivityLogger(AppDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(string action, string entityType, Guid entityId, string? details = null)
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId)) return;

        var log = new ActivityLog
        {
            UserId = userId,
            ActionType = action,
            EntityType = entityType,
            ReferenceId = entityId,
            MetadataJson = details != null ? $"{{\"details\":\"{details}\"}}" : null
        };
        _context.ActivityLogs.Add(log);
        await _context.SaveChangesAsync();
    }
}
