using Microsoft.EntityFrameworkCore;
using AlFolk.Api.Models;

namespace AlFolk.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Child> Children { get; set; }
    public DbSet<Game> Games { get; set; }
    public DbSet<GameSession> GameSessions { get; set; }
    public DbSet<ChildSkill> ChildSkills { get; set; }
    public DbSet<Playlist> Playlists { get; set; }
    public DbSet<Video> Videos { get; set; }
    public DbSet<VideoView> VideoViews { get; set; }
    public DbSet<DailyActivity> DailyActivities { get; set; }
    public DbSet<Medication> Medications { get; set; }
    public DbSet<Media> Media { get; set; }
    public DbSet<ActivityLog> ActivityLogs { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<DeviceToken> DeviceTokens { get; set; }
    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<Report> Reports { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<AppSetting> AppSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Global query filter for Soft Delete
        modelBuilder.Entity<User>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Child>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Playlist>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Video>().HasQueryFilter(e => !e.IsDeleted);

        // Unique indexes
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique().HasFilter("[Email] IS NOT NULL");
        modelBuilder.Entity<User>().HasIndex(u => u.Phone).IsUnique().HasFilter("[Phone] IS NOT NULL");
        modelBuilder.Entity<User>().HasIndex(u => u.NationalId).IsUnique().HasFilter("[NationalId] IS NOT NULL");
        modelBuilder.Entity<Child>().HasIndex(c => c.NationalId).IsUnique().HasFilter("[NationalId] IS NOT NULL");
        modelBuilder.Entity<AppSetting>().HasIndex(s => s.SettingKey).IsUnique();

        // Column types
        modelBuilder.Entity<DailyActivity>().Property(a => a.ActivityDate).HasColumnType("date");

        // Relationships
        modelBuilder.Entity<Child>()
            .HasOne(c => c.Parent)
            .WithMany(u => u.Children)
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GameSession>()
            .HasOne(gs => gs.Child)
            .WithMany(c => c.GameSessions)
            .HasForeignKey(gs => gs.ChildId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChildSkill>()
            .HasOne(cs => cs.Child)
            .WithMany(c => c.Skills)
            .HasForeignKey(cs => cs.ChildId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Video>()
            .HasOne(v => v.Playlist)
            .WithMany(p => p.Videos)
            .HasForeignKey(v => v.PlaylistId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Medication>()
            .HasOne(m => m.Child)
            .WithMany(c => c.Medications)
            .HasForeignKey(m => m.ChildId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Report>()
            .HasOne(r => r.Child)
            .WithMany(c => c.Reports)
            .HasForeignKey(r => r.ChildId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatMessage>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ChatMessage>()
            .HasOne(m => m.Receiver)
            .WithMany()
            .HasForeignKey(m => m.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);

        // Additional relationships
        modelBuilder.Entity<VideoView>()
            .HasOne(vv => vv.Video)
            .WithMany()
            .HasForeignKey(vv => vv.VideoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DailyActivity>()
            .HasOne(da => da.Child)
            .WithMany()
            .HasForeignKey(da => da.ChildId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ActivityLog>()
            .HasOne(al => al.User)
            .WithMany()
            .HasForeignKey(al => al.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AuditLog>()
            .HasOne(al => al.User)
            .WithMany()
            .HasForeignKey(al => al.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DeviceToken>()
            .HasOne(dt => dt.User)
            .WithMany()
            .HasForeignKey(dt => dt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PasswordResetToken>()
            .HasOne(prt => prt.User)
            .WithMany()
            .HasForeignKey(prt => prt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RefreshToken>()
            .HasOne(rt => rt.User)
            .WithMany()
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is BaseEntity && (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var entry in entries)
        {
            var entity = (BaseEntity)entry.Entity;
            entity.UpdatedAt = DateTime.UtcNow;
            if (entry.State == EntityState.Added)
            {
                entity.CreatedAt = DateTime.UtcNow;
            }
        }
    }
}
