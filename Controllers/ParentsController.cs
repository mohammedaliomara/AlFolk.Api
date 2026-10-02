using AlFolk.Api.DTOs;
using AlFolk.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFolk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ParentsController : ControllerBase
{
    private readonly IParentService _parentService;

    public ParentsController(IParentService parentService)
    {
        _parentService = parentService;
    }

    [HttpGet]
    public async Task<IActionResult> GetParents(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string sortBy = "CreatedAt",
        [FromQuery] string sortDir = "desc")
    {
        var response = await _parentService.GetParentsAsync(search, page, pageSize, sortBy, sortDir);
        return Ok(response);
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportParents(
        [FromQuery] string? search,
        [FromQuery] string sortBy = "CreatedAt",
        [FromQuery] string sortDir = "desc")
    {
        var response = await _parentService.GetParentsAsync(search, 1, int.MaxValue, sortBy, sortDir);
        var parents = response.Data;

        var csvBuilder = new System.Text.StringBuilder();
        csvBuilder.AppendLine("معرف ولي الأمر,الاسم الكامل,البريد الإلكتروني,رقم الهاتف,الرقم القومي,عدد الأطفال,عدد مرات تسجيل الدخول,تاريخ الإنشاء");

        foreach (var parent in parents)
        {
            csvBuilder.AppendLine($"{parent.Id},{parent.Name},{parent.Email ?? ""},{parent.Phone},{parent.NationalId ?? ""},{parent.ChildrenCount},{parent.LoginCount},{parent.CreatedAt}");
        }

        var csvBytes = System.Text.Encoding.Unicode.GetPreamble()
            .Concat(System.Text.Encoding.Unicode.GetBytes(csvBuilder.ToString()))
            .ToArray();
        return File(csvBytes, "text/csv; charset=utf-16", "parents_export.csv");
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetParent(Guid id)
    {
        var parent = await _parentService.GetParentByIdAsync(id);
        if (parent == null) return NotFound();
        return Ok(parent);
    }

    [HttpPost]
    public async Task<IActionResult> CreateParent([FromBody] CreateParentRequest request)
    {
        try
        {
            var parent = await _parentService.CreateParentAsync(request);
            return CreatedAtAction(nameof(GetParent), new { id = parent.Id }, parent);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = "validation_error", message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateParent(Guid id, [FromBody] UpdateParentRequest request)
    {
        try
        {
            var success = await _parentService.UpdateParentAsync(id, request);
            if (!success) return NotFound();
            return Ok(new { message = "Parent updated successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = "validation_error", message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteParent(Guid id)
    {
        var success = await _parentService.DeleteParentAsync(id);
        if (!success) return NotFound();
        return NoContent();
    }
}
