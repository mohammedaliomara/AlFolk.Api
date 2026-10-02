using AlFolk.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFolk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;

    public AnalyticsController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var data = await _analyticsService.GetDashboardDataAsync();
        return Ok(data);
    }

    [HttpGet("top-kids")]
    public async Task<IActionResult> GetTopKids([FromQuery] int page = 1, [FromQuery] int pageSize = 5)
    {
        var data = await _analyticsService.GetTopKidsAsync(page, pageSize);
        return Ok(data);
    }

    [HttpGet("usage-time")]
    public async Task<IActionResult> GetUsageTime(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 5, 
        [FromQuery] string sortBy = "usageHours", 
        [FromQuery] string sortDir = "desc")
    {
        var data = await _analyticsService.GetUsageTimeAsync(page, pageSize, sortBy, sortDir);
        return Ok(data);
    }

    [HttpGet("average-usage-hours")]
    public async Task<IActionResult> GetAverageUsageHours()
    {
        var avg = await _analyticsService.GetAverageUsageHoursAsync();
        return Ok(new { averageUsageHours = avg });
    }
}
