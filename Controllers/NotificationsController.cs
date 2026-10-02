using System.Security.Claims;
using AlFolk.Api.Data;
using AlFolk.Api.DTOs;
using AlFolk.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using AlFolk.Api.Services;

namespace AlFolk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IFirebaseMessagingService _firebaseService;

    public NotificationsController(AppDbContext context, IFirebaseMessagingService firebaseService)
    {
        _context = context;
        _firebaseService = firebaseService;
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendNotification([FromBody] SendNotificationRequest request)
    {
        if (string.IsNullOrEmpty(request.Title) || string.IsNullOrEmpty(request.Message))
        {
            return BadRequest(new { success = false, message = "العنوان والرسالة مطلوبان" });
        }

        var recipients = new List<User>();
        if (request.RecipientType.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            recipients = await _context.Users
                .Where(u => u.Role == "Parent" && !u.IsDeleted)
                .ToListAsync();
        }
        else if (request.RecipientType.Equals("specific", StringComparison.OrdinalIgnoreCase))
        {
            if (!request.TargetParentId.HasValue)
            {
                return BadRequest(new { success = false, message = "معرّف ولي الأمر مطلوب للإشعار المحدد" });
            }

            var parent = await _context.Users.FindAsync(request.TargetParentId.Value);
            if (parent == null || parent.IsDeleted)
            {
                return NotFound(new { success = false, message = "ولي الأمر غير موجود" });
            }
            recipients.Add(parent);
        }
        else
        {
            return BadRequest(new { success = false, message = "نوع المستلم غير صالح" });
        }

        if (!recipients.Any())
        {
            return Ok(new { success = true, message = "لا يوجد مستلمون للإشعار", totalRecipients = 0 });
        }

        var notifications = recipients.Select(r => new Notification
        {
            UserId = r.Id,
            Title = request.Title,
            Message = request.Message,
            Type = "Info",
            IsRead = false
        }).ToList();

        _context.Notifications.AddRange(notifications);
        await _context.SaveChangesAsync();

        var fcmSuccessCount = 0;
        var fcmFailureCount = 0;
        var fcmErrors = new List<string>();

        try
        {
            var userIds = recipients.Select(r => r.Id).ToList();
            var result = await _firebaseService.SendNotificationToUsersAsync(userIds, request.Title, request.Message);
            fcmSuccessCount = result.SuccessCount;
            fcmFailureCount = result.FailureCount;
            fcmErrors = result.Errors ?? new List<string>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Firebase Error]: {ex.Message}");
            fcmErrors.Add(ex.Message);
        }

        var firstNotification = notifications.First();

        return Created("", new
        {
            success = true,
            data = new
            {
                id = firstNotification.Id,
                title = firstNotification.Title,
                message = firstNotification.Message,
                recipientType = request.RecipientType,
                sentAt = firstNotification.CreatedAt,
                totalRecipients = recipients.Count,
                fcmSuccessCount = fcmSuccessCount,
                fcmFailureCount = fcmFailureCount,
                fcmErrors = fcmErrors
            }
        });
    }

    [AllowAnonymous]
    [HttpGet("test-firebase")]
    public async Task<IActionResult> TestFirebase()
    {
        try
        {
            if (FirebaseAdmin.FirebaseApp.DefaultInstance == null)
            {
                var credentialsPath = Path.Combine(AppContext.BaseDirectory, "firebase-credentials.json");
                if (!System.IO.File.Exists(credentialsPath))
                {
                    credentialsPath = Path.Combine(Directory.GetCurrentDirectory(), "firebase-credentials.json");
                }

                if (!System.IO.File.Exists(credentialsPath))
                {
                    return Ok(new
                    {
                        success = false,
                        message = $"ملف بيانات الاعتماد غير موجود في المسار: {credentialsPath}"
                    });
                }
            }

            // Check if initialized
            bool initialized = FirebaseAdmin.FirebaseApp.DefaultInstance != null;
            
            return Ok(new
            {
                success = true,
                message = "تمت تهيئة Firebase Admin SDK بنجاح!",
                projectId = FirebaseAdmin.FirebaseApp.DefaultInstance?.Options.ProjectId,
                isInitialized = initialized
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = $"فشل في تهيئة Firebase: {ex.Message}",
                details = ex.ToString()
            });
        }
    }

    /// <summary>
    /// [TEST ONLY] Inserts a fake FCM token for a given parentId then immediately fires a push notification.
    /// Use this to verify the full Firebase pipeline without needing the mobile app.
    /// </summary>
    [HttpPost("test-send-fcm")]
    public async Task<IActionResult> TestSendFcm([FromBody] TestFcmRequest request)
    {
        // 1. Verify the parent exists
        var parent = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.ParentId && u.Role == "Parent");
        if (parent == null)
            return NotFound(new { success = false, message = "الـ parent غير موجود" });

        // 2. Insert the test token into DeviceTokens
        var existingToken = await _context.DeviceTokens
            .FirstOrDefaultAsync(dt => dt.UserId == request.ParentId && dt.Token == request.FcmToken);

        if (existingToken == null)
        {
            _context.DeviceTokens.Add(new AlFolk.Api.Models.DeviceToken
            {
                UserId = request.ParentId,
                Token  = request.FcmToken,
                DeviceType = "Android"
            });
            await _context.SaveChangesAsync();
        }

        // 3. Fire the push notification directly to this token
        var result = await _firebaseService.SendNotificationToUsersAsync(
            new List<Guid> { request.ParentId },
            request.Title ?? "تيست Firebase",
            request.Message ?? "هذا إشعار تجريبي من السيرفر"
        );

        return Ok(new
        {
            success = true,
            message  = $"تم إرسال Push Notification لـ parent: {parent.FirstName} {parent.LastName}",
            parentId = request.ParentId,
            fcmToken = request.FcmToken,
            fcmSuccessCount = result.SuccessCount,
            fcmFailureCount = result.FailureCount,
            fcmErrors = result.Errors
        });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var notifications = await _context.Notifications
            .Include(n => n.User)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        var grouped = notifications.GroupBy(n => new { n.Title, n.Message, n.CreatedAt.Date })
            .Select(g => new
            {
                id = g.First().Id,
                title = g.Key.Title,
                message = g.Key.Message,
                recipientType = g.Count() > 1 ? "all" : "specific",
                targetParentName = g.Count() == 1 ? (g.First().User != null ? (g.First().User.FirstName + " " + g.First().User.LastName) : "N/A") : null,
                sentAt = g.First().CreatedAt,
                readCount = g.Count(n => n.IsRead),
                totalRecipients = g.Count(),
                readPercentage = g.Count() > 0 ? Math.Round((double)g.Count(n => n.IsRead) / g.Count() * 100, 2) : 0
            })
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var totalItems = notifications.GroupBy(n => new { n.Title, n.Message, n.CreatedAt.Date }).Count();

        return Ok(new
        {
            success = true,
            data = new
            {
                items = grouped,
                pagination = new PaginationMetadata(page, pageSize, totalItems, (int)Math.Ceiling(totalItems / (double)pageSize))
            }
        });
    }

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var notification = await _context.Notifications.FindAsync(id);
        if (notification == null)
        {
            return NotFound(new { success = false, message = "الإشعار غير موجود" });
        }

        notification.IsRead = true;
        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = "تم تحديد الإشعار كمقروء" });
    }

    [HttpPost("/api/parents/fcm-token")]
    public async Task<IActionResult> RegisterFcmToken([FromBody] FcmTokenRequest request)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrEmpty(request.FcmToken))
        {
            return BadRequest(new { success = false, message = "FcmToken is required" });
        }

        var existingToken = await _context.DeviceTokens
            .FirstOrDefaultAsync(dt => dt.UserId == userId && dt.Token == request.FcmToken);

        if (existingToken == null)
        {
            var deviceToken = new DeviceToken
            {
                UserId = userId,
                Token = request.FcmToken,
                DeviceType = request.DeviceType ?? "Web"
            };
            _context.DeviceTokens.Add(deviceToken);
        }
        else if (existingToken.DeviceType != request.DeviceType && request.DeviceType != null)
        {
            existingToken.DeviceType = request.DeviceType;
        }

        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = "FCM token registered successfully" });
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
        {
            return Unauthorized();
        }

        var query = _context.Notifications.Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt);
        var totalItems = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return Ok(new
        {
            success = true,
            data = new
            {
                items = items,
                pagination = new PaginationMetadata(page, pageSize, totalItems, (int)Math.Ceiling(totalItems / (double)pageSize))
            }
        });
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
        {
            return Unauthorized();
        }

        var count = await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
        return Ok(new { success = true, unreadCount = count });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
        {
            return Unauthorized();
        }

        var unread = await _context.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
        foreach (var n in unread)
        {
            n.IsRead = true;
        }

        await _context.SaveChangesAsync();
        return Ok(new { success = true, message = "تم تحديد جميع الإشعارات كمقروءة" });
    }

    [HttpDelete("clear-all")]
    public async Task<IActionResult> ClearAllNotifications()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
        {
            return Unauthorized();
        }

        var userNotifications = await _context.Notifications.Where(n => n.UserId == userId).ToListAsync();
        _context.Notifications.RemoveRange(userNotifications);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = "تم حذف جميع الإشعارات بنجاح" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteNotification(Guid id)
    {
        var notification = await _context.Notifications.FindAsync(id);
        if (notification == null)
        {
            return NotFound(new { success = false, message = "الإشعار غير موجود" });
        }

        var date = notification.CreatedAt.Date;
        var relatedNotifications = await _context.Notifications
            .Where(n => n.Title == notification.Title 
                     && n.Message == notification.Message 
                     && n.CreatedAt.Date == date)
            .ToListAsync();

        _context.Notifications.RemoveRange(relatedNotifications);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = $"تم حذف الإشعار ومجموعته بنجاح (العدد: {relatedNotifications.Count})" });
    }
}
