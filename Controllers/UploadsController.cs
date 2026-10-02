using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AlFolk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UploadsController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    public UploadsController(IWebHostEnvironment env)
    {
        _env = env;
    }

    [HttpPost("image")]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { success = false, message = "لم يتم تحديد أي ملف" });
        }

        var uploadsDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "images");
        if (!Directory.Exists(uploadsDir))
        {
            Directory.CreateDirectory(uploadsDir);
        }

        var uniqueName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
        var filePath = Path.Combine(uploadsDir, uniqueName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var fileUrl = $"/uploads/images/{uniqueName}";
        return Ok(new { success = true, url = fileUrl, fileName = uniqueName });
    }

    [HttpPost("video-thumbnail")]
    public async Task<IActionResult> UploadThumbnail(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { success = false, message = "لم يتم تحديد أي ملف" });
        }

        var uploadsDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "thumbnails");
        if (!Directory.Exists(uploadsDir))
        {
            Directory.CreateDirectory(uploadsDir);
        }

        var uniqueName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
        var filePath = Path.Combine(uploadsDir, uniqueName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var fileUrl = $"/uploads/thumbnails/{uniqueName}";
        return Ok(new { success = true, url = fileUrl, fileName = uniqueName });
    }

    [HttpDelete("{fileName}")]
    public IActionResult DeleteFile(string fileName)
    {
        var folders = new[] { "images", "thumbnails" };
        foreach (var folder in folders)
        {
            var filePath = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", folder, fileName);
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
                return Ok(new { success = true, message = "تم حذف الملف بنجاح" });
            }
        }

        return NotFound(new { success = false, message = "الملف غير موجود" });
    }
}
