using AlFolk.Api.Data;
using AlFolk.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlFolk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ActivityLogController : ControllerBase
{
    private readonly AppDbContext _context;

    public ActivityLogController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] Guid? userId,
        [FromQuery] Guid? childId,
        [FromQuery] string? action,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _context.ActivityLogs.AsQueryable();

        if (userId.HasValue)
        {
            query = query.Where(l => l.UserId == userId.Value);
        }

        if (childId.HasValue)
        {
            query = query.Where(l => l.ChildId == childId.Value);
        }

        if (!string.IsNullOrEmpty(action))
        {
            query = query.Where(l => l.ActionType.Contains(action));
        }

        if (startDate.HasValue)
        {
            query = query.Where(l => l.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(l => l.CreatedAt <= endDate.Value);
        }

        var queryOrdered = query.OrderByDescending(l => l.CreatedAt);
        var total = await queryOrdered.CountAsync();
        var data = await queryOrdered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PaginatedResponse<Models.ActivityLog>(data, new PaginationMetadata(page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize))));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetLogDetail(Guid id)
    {
        var log = await _context.ActivityLogs.FindAsync(id);
        if (log == null) return NotFound(new { success = false, message = "السجل غير موجود" });
        return Ok(new { success = true, data = log });
    }
}
