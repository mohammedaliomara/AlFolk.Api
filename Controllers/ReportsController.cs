using AlFolk.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFolk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IParentService _parentService;
    private readonly IChildService _childService;
    private readonly IAnalyticsService _analyticsService;

    public ReportsController(IParentService parentService, IChildService childService, IAnalyticsService analyticsService)
    {
        _parentService = parentService;
        _childService = childService;
        _analyticsService = analyticsService;
    }

    [HttpGet("parents")]
    public async Task<IActionResult> GetParentsReport()
    {
        var data = await _parentService.GetAllForReportAsync();
        return Ok(data);
    }

    [HttpGet("children")]
    public async Task<IActionResult> GetChildrenReport()
    {
        var data = await _childService.GetAllForReportAsync();
        return Ok(data);
    }

    [HttpGet("usage-time")]
    public async Task<IActionResult> GetUsageTimeReport()
    {
        var data = await _analyticsService.GetUsageTimeReportAsync();
        return Ok(data);
    }
}
