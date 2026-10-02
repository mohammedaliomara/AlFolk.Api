using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlFolk.Api.Models;

public abstract class BaseEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
}

public class User : BaseEntity
{
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;
    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? NationalId { get; set; }

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    [Required, MaxLength(50)]
    public string Role { get; set; } = "Parent"; // Admin / Parent / Teacher

    public bool IsEmailVerified { get; set; } = false;

    public int LoginCount { get; set; } = 0;
    public int FailedLoginAttempts { get; set; } = 0;

    public bool MustChangePassword { get; set; } = false;

    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginAt { get; set; }

    public string? FcmToken { get; set; }

    public virtual ICollection<Child> Children { get; set; } = new List<Child>();
}

public class Child : BaseEntity
{
    public Guid ParentId { get; set; }
    [ForeignKey("ParentId")]
    public virtual User Parent { get; set; } = null!;

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public string? NationalId { get; set; }

    [Range(0, 100)]
    public int Age { get; set; }

    [Required, MaxLength(20)]
    public string Gender { get; set; } = "Male"; // Male / Female

    public string? EducationLevel { get; set; }

    public string? ImageUrl { get; set; }

    public double UsageHours { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    public virtual ICollection<ChildSkill> Skills { get; set; } = new List<ChildSkill>();
    public virtual ICollection<GameSession> GameSessions { get; set; } = new List<GameSession>();
    public virtual ICollection<Medication> Medications { get; set; } = new List<Medication>();
    public virtual ICollection<Report> Reports { get; set; } = new List<Report>();
}

public class Game : BaseEntity
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ThumbnailUrl { get; set; }

    // Skill category this game belongs to: colors / letters / memory / shapes / numbers
    [MaxLength(50)]
    public string? SkillKey { get; set; }

    public bool IsActive { get; set; } = true;
}

public class GameSession : BaseEntity
{
    public Guid ChildId { get; set; }
    [ForeignKey("ChildId")]
    public virtual Child Child { get; set; } = null!;

    public Guid GameId { get; set; }
    [ForeignKey("GameId")]
    public virtual Game Game { get; set; } = null!;

    public int Score { get; set; } = 0;

    public int DurationSeconds { get; set; } = 0;

    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;

    public string? MetadataJson { get; set; }
}

public class ChildSkill : BaseEntity
{
    public Guid ChildId { get; set; }
    [ForeignKey("ChildId")]
    public virtual Child Child { get; set; } = null!;

    [Required, MaxLength(100)]
    public string SkillKey { get; set; } = string.Empty; // Memory / Numbers / Colors / Language

    public int Level { get; set; } = 1;

    public int Points { get; set; } = 0;

    public double Progress { get; set; } = 0;

    public string? Notes { get; set; }
}

public class Playlist : BaseEntity
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    [Required, MaxLength(50)]
    public string TargetAudience { get; set; } = "Kids"; // Kids / Parents / Teachers

    public bool IsActive { get; set; } = true;

    public virtual ICollection<Video> Videos { get; set; } = new List<Video>();
}

public class Video : BaseEntity
{
    public Guid? PlaylistId { get; set; }
    [ForeignKey("PlaylistId")]
    public virtual Playlist? Playlist { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public string VideoUrl { get; set; } = string.Empty;

    public string? ThumbnailUrl { get; set; }

    public string? Category { get; set; }

    public int DurationSeconds { get; set; } = 0;

    public int SortOrder { get; set; } = 0;

    public long ViewCount { get; set; } = 0;

    public bool IsActive { get; set; } = true;
}

public class VideoView : BaseEntity
{
    public Guid? UserId { get; set; }
    [ForeignKey("UserId")]
    public virtual User? User { get; set; }

    public Guid? ChildId { get; set; }
    [ForeignKey("ChildId")]
    public virtual Child? Child { get; set; }

    public Guid VideoId { get; set; }
    [ForeignKey("VideoId")]
    public virtual Video Video { get; set; } = null!;

    public DateTime WatchedAt { get; set; } = DateTime.UtcNow;

    public int DurationWatchedSeconds { get; set; } = 0;

    public string? DeviceType { get; set; }
}

public class DailyActivity : BaseEntity
{
    public Guid ChildId { get; set; }
    [ForeignKey("ChildId")]
    public virtual Child Child { get; set; } = null!;

    [Required, MaxLength(200)]
    public string ActivityName { get; set; } = string.Empty;

    public DateTime ActivityDate { get; set; }

    public bool IsCompleted { get; set; } = false;

    public string? MetadataJson { get; set; }
}

public class Medication : BaseEntity
{
    public Guid ChildId { get; set; }
    [ForeignKey("ChildId")]
    public virtual Child Child { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Dosage { get; set; }

    public string? ScheduleJson { get; set; }

    public int Frequency { get; set; } = 0;

    public string? Notes { get; set; }
}

public class Media : BaseEntity
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string FileUrl { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string FileType { get; set; } = "Image"; // Image / Video / Audio

    public Guid? UploaderId { get; set; }
    [ForeignKey("UploaderId")]
    public virtual User? Uploader { get; set; }
}

public class ActivityLog : BaseEntity
{
    public Guid? UserId { get; set; }
    [ForeignKey("UserId")]
    public virtual User? User { get; set; }

    public Guid? ChildId { get; set; }
    [ForeignKey("ChildId")]
    public virtual Child? Child { get; set; }

    [Required, MaxLength(100)]
    public string ActionType { get; set; } = string.Empty; // WatchVideo / PlayGame / Login

    public string? EntityType { get; set; } // Video / Game / Child

    public Guid? ReferenceId { get; set; }

    public string? MetadataJson { get; set; }
}

public class AuditLog : BaseEntity
{
    public Guid UserId { get; set; }
    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;

    [Required, MaxLength(50)]
    public string Role { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string ActionType { get; set; } = string.Empty; // Login / Delete / Update

    public string? IpAddress { get; set; }

    public string? DeviceInfo { get; set; }

    public string? MetadataJson { get; set; }
}

public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Message { get; set; } = string.Empty;

    public string? Type { get; set; } // Reminder / Warning / Info

    public bool IsRead { get; set; } = false;

    public string? TargetUrl { get; set; }
}

public class DeviceToken : BaseEntity
{
    public Guid UserId { get; set; }
    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;

    [Required]
    public string Token { get; set; } = string.Empty;

    public string? DeviceType { get; set; } // Android / IOS / Web
}

public class PasswordResetToken : BaseEntity
{
    public Guid UserId { get; set; }
    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;

    [Required]
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; } = false;
}

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;

    [Required]
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public bool IsRevoked { get; set; } = false;
}

public class Report : BaseEntity
{
    public Guid ChildId { get; set; }
    [ForeignKey("ChildId")]
    public virtual Child Child { get; set; } = null!;

    public Guid? CreatedBy { get; set; }
    [ForeignKey("CreatedBy")]
    public virtual User? Creator { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ReportFileUrl { get; set; }
}

public class ChatMessage : BaseEntity
{
    public Guid SenderId { get; set; }
    [ForeignKey("SenderId")]
    public virtual User Sender { get; set; } = null!;

    public Guid ReceiverId { get; set; }
    [ForeignKey("ReceiverId")]
    public virtual User Receiver { get; set; } = null!;

    [Required]
    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; } = false;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

public class AppSetting : BaseEntity
{
    [Required, MaxLength(150)]
    public string SettingKey { get; set; } = string.Empty;

    public string? SettingValue { get; set; }
}
