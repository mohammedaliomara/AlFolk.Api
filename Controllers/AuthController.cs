using System.Security.Claims;
using AlFolk.Api.DTOs;
using AlFolk.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFolk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try 
        {
            var response = await _authService.LoginAsync(request);
            if (response == null)
            {
                return Unauthorized(new { error = "invalid_credentials", message = "البريد الإلكتروني أو كلمة المرور غير صحيحة" });
            }
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { 
                error = "server_error", 
                message = "حدث خطأ في الاتصال بقاعدة البيانات",
                details = ex.Message,
                inner = ex.InnerException?.Message
            });
        }
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
        {
            return Unauthorized();
        }

        var user = await _authService.GetMeAsync(userId);
        if (user == null) return NotFound();
        return Ok(new { admin = user }); // Kept 'admin' for front-end compatibility if needed
    }

    [Authorize]
    [HttpPost("logout")]
    [HttpPost("admin/logout")]
    public async Task<IActionResult> Logout([FromBody] TokenRefreshRequest? request)
    {
        if (request != null && !string.IsNullOrEmpty(request.RefreshToken))
        {
            await _authService.RevokeTokenAsync(request.RefreshToken);
        }
        return Ok(new { success = true, message = "تم تسجيل الخروج بنجاح" });
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] TokenRefreshRequest request)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            return BadRequest(new { error = "invalid_request", message = "Refresh token is required" });
        }

        var response = await _authService.RefreshTokenAsync(request.RefreshToken);
        if (response == null)
        {
            return Unauthorized(new { error = "invalid_token", message = "Refresh token is invalid or expired" });
        }

        return Ok(response);
    }

    [Authorize]
    [HttpPut("admin/change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
        {
            return Unauthorized();
        }

        var success = await _authService.ChangePasswordAsync(userId, request);
        if (!success)
        {
            return BadRequest(new { error = "change_password_failed", message = "فشل تغيير كلمة المرور. يرجى التأكد من كلمة المرور الحالية وتطابق كلمة المرور الجديدة." });
        }

        return Ok(new { success = true, message = "تم تغيير كلمة المرور بنجاح" });
    }

    [HttpPost("forgot-password")]
    public IActionResult ForgotPassword()
    {
        return StatusCode(501, new { message = "نسيت كلمة المرور غير مفعلة حالياً" });
    }

    [HttpPost("reset-password")]
    public IActionResult ResetPassword()
    {
        return StatusCode(501, new { message = "إعادة تعيين كلمة المرور غير مفعلة حالياً" });
    }
}
