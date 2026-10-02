using AlFolk.Api.Data;
using AlFolk.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AlFolk.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        await EnsureSchemaAsync(context);

        // Optional: Database cleanup to remove legacy/random test data
        // Let's clear any previously seeded parents, children, and game sessions so they can be re-seeded cleanly.
        var hasRandomData = await context.Children.AnyAsync(c => c.Name.StartsWith("طفل "));
        if (hasRandomData)
        {
            // Delete child skills, medications, game sessions, and children
            context.ChildSkills.RemoveRange(await context.ChildSkills.ToListAsync());
            context.GameSessions.RemoveRange(await context.GameSessions.ToListAsync());
            context.Medications.RemoveRange(await context.Medications.ToListAsync());
            context.VideoViews.RemoveRange(await context.VideoViews.ToListAsync());
            context.Children.RemoveRange(await context.Children.IgnoreQueryFilters().ToListAsync());
            
            // Delete seeded parents
            var seededParents = await context.Users.Where(u => u.Role == "Parent").IgnoreQueryFilters().ToListAsync();
            context.Users.RemoveRange(seededParents);
            
            await context.SaveChangesAsync();
        }

        // 1. Seed Admin
        if (!context.Users.Any(u => u.Role == "Admin"))
        {
            var admin = new User
            {
                FirstName = "المدير",
                LastName = "العام",
                Email = "admin@alfolk.org",
                Role = "Admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                MustChangePassword = true,
                IsActive = true
            };
            context.Users.Add(admin);
            await context.SaveChangesAsync();
        }

        // 2. Seed Playlists
        if (!context.Playlists.Any())
        {
            var kidsPlaylist = new Playlist
            {
                Title = "فيديوهات تعليمية للأطفال",
                Description = "مجموعة من الفيديوهات لتعزيز المهارات الإدراكية",
                TargetAudience = "Kids",
                IsActive = true
            };
            var parentPlaylist = new Playlist
            {
                Title = "إرشادات لأولياء الأمور",
                Description = "نصائح للتعامل مع الأطفال وتطوير مهاراتهم",
                TargetAudience = "Parents",
                IsActive = true
            };
            context.Playlists.AddRange(kidsPlaylist, parentPlaylist);
            await context.SaveChangesAsync();

            // 3. Seed Videos
            var video1 = new Video { PlaylistId = kidsPlaylist.Id, Title = "تعلم الألوان", VideoUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ", DurationSeconds = 600, SortOrder = 1, IsActive = true };
            var video2 = new Video { PlaylistId = parentPlaylist.Id, Title = "كيفية المتابعة", VideoUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ", DurationSeconds = 900, SortOrder = 1, IsActive = true };
            context.Videos.AddRange(video1, video2);
            await context.SaveChangesAsync();
        }

        // 4. Seed Parents and Children
        if (!context.Users.Any(u => u.Role == "Parent"))
        {
            for (int i = 1; i <= 5; i++)
            {
                var parent = new User
                {
                    FirstName = "ولي أمر",
                    LastName = i.ToString(),
                    Phone = $"010{i}2345678",
                    Role = "Parent",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                    LoginCount = i * 10,
                    IsActive = true
                };
                
                var child = new Child
                {
                    Name = $"طفل {i}",
                    Age = 5 + i,
                    UsageHours = i * 5.5,
                    Gender = i % 2 == 0 ? "Female" : "Male",
                    IsActive = true
                };

                child.Medications.Add(new Medication { Name = "فيتامين د", Dosage = "5ml" });

                var skillKeys = new[] { "memory", "colors", "letters", "shapes" };
                foreach (var sk in skillKeys)
                {
                    child.Skills.Add(new ChildSkill { SkillKey = sk, Points = i * 100, Progress = 50 + i * 5 });
                }

                parent.Children.Add(child);
                context.Users.Add(parent);
            }
            await context.SaveChangesAsync();
        }

        // 5. Seed Games — remove legacy English/deleted games first, then ensure Arabic games exist
        var legacyNames = new[]
        {
            // Old English names
            "Memory Match", "Color Sorting", "Number Catch", "الحساب الذهني",
            // New English names coming from mobile that were not yet normalized
            "MemoryMatching", "memoryMatching", "memoryMatch", "MemoryMatch",
            "animalColoring", "AnimalColoring", "animal_coloring", "AnimalColoring",
            "scribble", "Scribble", "scribbles", "Scribbles",
            "xylophone", "Xylophone", "xi", "Xi",
            "colors", "Colors", "colorsGame", "ColorsGame",
            "drawAndColor", "DrawAndColor", "drawing", "Drawing",
            "animalSounds", "AnimalSounds", "animal_sounds",
            "shapeAndShadow", "ShapeAndShadow", "shapes", "Shapes",
            "puzzle", "Puzzle", "jigsaw", "Jigsaw",
            "piano", "Piano",
            "memory", "Memory",
            "coloring", "Coloring",
            "shadow", "Shadow",
            "doodle", "Doodle"
        };
        var legacyGames = context.Games.Where(g => legacyNames.Contains(g.Name)).ToList();
        if (legacyGames.Any())
        {
            // Re-assign game sessions to their normalized Arabic game before deleting
            foreach (var lg in legacyGames)
            {
                var normalizedName = AlFolk.Api.Services.ArabicNormalization.NormalizeGameName(lg.Name);
                var targetGame = context.Games.FirstOrDefault(g => g.Name == normalizedName && g.Id != lg.Id);
                if (targetGame != null)
                {
                    var sessions = context.GameSessions.Where(s => s.GameId == lg.Id).ToList();
                    foreach (var s in sessions) s.GameId = targetGame.Id;
                    var logs = context.ActivityLogs.Where(l => l.EntityType == "Game" && l.ReferenceId == lg.Id).ToList();
                    foreach (var l in logs) l.ReferenceId = targetGame.Id;
                }
            }
            await context.SaveChangesAsync();
            context.Games.RemoveRange(legacyGames);
            await context.SaveChangesAsync();
        }

        var appGames = new List<Game>
        {
            new Game { Name = "\u0627\u0644\u0634\u0643\u0644 \u0648 \u0638\u0644\u0647",   SkillKey = "shapes",  Description = "\u0644\u0639\u0628\u0629 \u0645\u0637\u0627\u0628\u0642\u0629 \u0627\u0644\u0623\u0634\u0643\u0627\u0644 \u0628\u0638\u0644\u0627\u0644\u0647\u0627 \u0644\u062a\u0639\u0632\u064a\u0632 \u0642\u0648\u0629 \u0627\u0644\u0645\u0644\u0627\u062d\u0638\u0629 \u0648\u0627\u0644\u0625\u062f\u0631\u0627\u0643 \u0627\u0644\u0628\u0635\u0631\u064a" },
            new Game { Name = "\u0627\u0635\u0648\u0627\u062a \u062d\u064a\u0648\u0627\u0646\u0627\u062a", SkillKey = "letters", Description = "\u0644\u0639\u0628\u0629 \u062a\u0641\u0627\u0639\u0644\u064a\u0629 \u0644\u0644\u062a\u0639\u0631\u0641 \u0639\u0644\u0649 \u0623\u0635\u0648\u0627\u062a \u0627\u0644\u062d\u064a\u0648\u0627\u0646\u0627\u062a \u0627\u0644\u0645\u062e\u062a\u0644\u0641\u0629 \u0648\u062a\u0637\u0648\u064a\u0631 \u0627\u0644\u0648\u0639\u064a \u0627\u0644\u0633\u0645\u0639\u064a \u0648\u0627\u0644\u0644\u063a\u0648\u064a" },
            new Game { Name = "\u0631\u0643\u0628 \u0627\u0644\u0635\u0648\u0631",     SkillKey = "shapes",  Description = "\u0644\u0639\u0628\u0629 \u062a\u0631\u0643\u064a\u0628 \u0642\u0637\u0639 \u0627\u0644\u0635\u0648\u0631 \u0627\u0644\u0645\u062c\u0632\u0623\u0629 (\u0627\u0644\u0628\u0627\u0632\u0644) \u0644\u062a\u0637\u0648\u064a\u0631 \u0645\u0647\u0627\u0631\u0627\u062a \u062d\u0644 \u0627\u0644\u0645\u0634\u0643\u0644\u0627\u062a \u0648\u0627\u0644\u062a\u0646\u0633\u064a\u0642 \u0627\u0644\u0628\u0635\u0631\u064a" },
            new Game { Name = "\u0627\u0644\u0630\u0627\u0643\u0631\u0647",        SkillKey = "memory",  Description = "\u0644\u0639\u0628\u0629 \u0643\u0631\u0648\u062a \u0627\u0644\u0630\u0627\u0643\u0631\u0629 \u0644\u062a\u0642\u0648\u064a\u0629 \u0645\u0647\u0627\u0631\u0627\u062a \u0627\u0644\u062a\u0630\u0643\u0631 \u0648\u0627\u0644\u0645\u0637\u0627\u0628\u0642\u0629 \u0627\u0644\u062b\u0646\u0627\u0626\u064a\u0629 \u0644\u0644\u0639\u0646\u0627\u0635\u0631" },
            new Game { Name = "\u0628\u064a\u0627\u0646\u0648",          SkillKey = "letters", Description = "\u0644\u0639\u0628\u0629 \u0639\u0632\u0641 \u0627\u0644\u0628\u064a\u0627\u0646\u0648 \u0627\u0644\u062a\u0641\u0627\u0639\u0644\u064a\u0629 \u0644\u062a\u0646\u0645\u064a\u0629 \u0627\u0644\u062d\u0633 \u0627\u0644\u0645\u0648\u0633\u064a\u0642\u064a \u0648\u0627\u0644\u0648\u0639\u064a \u0627\u0644\u0633\u0645\u0639\u064a \u0644\u0644\u0623\u0635\u0648\u0627\u062a \u0648\u0627\u0644\u0646\u063a\u0645\u0627\u062a" },
            new Game { Name = "\u0627\u0643\u0633\u0644\u064a\u0641\u0648\u0646",      SkillKey = "letters", Description = "\u0644\u0639\u0628\u0629 \u0639\u0632\u0641 \u0627\u0644\u0623\u0643\u0633\u0644\u064a\u0641\u0648\u0646 \u0644\u062a\u0637\u0648\u064a\u0631 \u0645\u0647\u0627\u0631\u0627\u062a \u0627\u0644\u062a\u0646\u0633\u064a\u0642 \u0627\u0644\u062d\u0631\u0643\u064a \u0627\u0644\u0628\u0635\u0631\u064a \u0648\u0627\u0644\u062a\u0645\u064a\u064a\u0632 \u0627\u0644\u0633\u0645\u0639\u064a" },
            new Game { Name = "\u062e\u0631\u0628\u0634 \u0627\u0644\u0635\u0648\u0631",    SkillKey = "colors",  Description = "\u0644\u0639\u0628\u0629 \u062e\u0631\u0628\u0634 \u0627\u0644\u0635\u0648\u0631 \u0644\u0627\u0643\u062a\u0634\u0627\u0641 \u0627\u0644\u0623\u0634\u0643\u0627\u0644 \u0627\u0644\u0645\u062e\u0641\u064a\u0629 \u0648\u062a\u0637\u0648\u064a\u0631 \u0627\u0644\u0645\u0647\u0627\u0631\u0627\u062a \u0627\u0644\u0628\u0635\u0631\u064a\u0629 \u0648\u0627\u0644\u062d\u0631\u0643\u064a\u0629 \u0627\u0644\u062f\u0642\u064a\u0642\u0629" },
            new Game { Name = "\u0627\u0631\u0633\u0645 \u0648\u0644\u0648\u0646",     SkillKey = "colors",  Description = "\u0644\u0639\u0628\u0629 \u0631\u0633\u0645 \u0648\u062a\u0644\u0648\u064a\u0646 \u062d\u0631 \u0644\u062a\u0639\u0632\u064a\u0632 \u0627\u0644\u0625\u0628\u062f\u0627\u0639 \u0648\u0627\u0644\u062a\u0639\u0628\u064a\u0631 \u0627\u0644\u0641\u0646\u064a \u0648\u062a\u0646\u0633\u064a\u0642 \u0627\u0644\u0623\u0644\u0648\u0627\u0646" },
            new Game { Name = "\u0644\u0648\u0646 \u0627\u0644\u0635\u0648\u0631\u0647",    SkillKey = "colors",  Description = "\u0644\u0639\u0628\u0629 \u062a\u0644\u0648\u064a\u0646 \u0627\u0644\u0631\u0633\u0648\u0645\u0627\u062a \u0627\u0644\u062c\u0627\u0647\u0632\u0629 \u0644\u0645\u0633\u0627\u0639\u062f\u0629 \u0627\u0644\u0623\u0637\u0641\u0627\u0644 \u0639\u0644\u0649 \u0641\u0631\u0632 \u0627\u0644\u0623\u0644\u0648\u0627\u0646 \u0648\u062a\u0646\u0645\u064a\u0629 \u0645\u0647\u0627\u0631\u0627\u062a \u0627\u0644\u062a\u0644\u0648\u064a\u0646 \u0627\u0644\u062f\u0642\u064a\u0642" },
            new Game { Name = "\u0627\u0644\u0623\u0644\u0648\u0627\u0646",        SkillKey = "colors",  Description = "\u0644\u0639\u0628\u0629 \u062a\u0639\u0644\u064a\u0645\u064a\u0629 \u0644\u062a\u0639\u0644\u0645 \u0627\u0644\u0623\u0644\u0648\u0627\u0646 \u0648\u062a\u0645\u064a\u064a\u0632\u0647\u0627 \u0648\u062a\u0637\u0648\u064a\u0631 \u0645\u0647\u0627\u0631\u0627\u062a \u0627\u0644\u062a\u0639\u0631\u0641 \u0639\u0644\u0649 \u0627\u0644\u0623\u0644\u0648\u0627\u0646 \u0648\u062a\u0633\u0645\u064a\u062a\u0647\u0627" }
        };

        foreach (var g in appGames)
        {
            var existing = context.Games.FirstOrDefault(e => e.Name == g.Name);
            if (existing == null)
            {
                context.Games.Add(g);
            }
            else if (existing.SkillKey != g.SkillKey)
            {
                // Back-fill SkillKey if it was missing
                existing.SkillKey = g.SkillKey;
            }
        }
        await context.SaveChangesAsync();
        await CleanupAndSyncDatabaseAsync(context);
    }

    public static async Task EnsureSchemaAsync(AppDbContext context)
    {
        // Create tables that might not exist in the shared DB
        // Each statement is safe to run multiple times (IF NOT EXISTS)
        await context.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Users')
            CREATE TABLE [Users] (
                [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
                [FirstName] NVARCHAR(100) NOT NULL DEFAULT '',
                [LastName] NVARCHAR(100) NOT NULL DEFAULT '',
                [Email] NVARCHAR(450) NULL,
                [Phone] NVARCHAR(450) NULL,
                [NationalId] NVARCHAR(450) NULL,
                [PasswordHash] NVARCHAR(MAX) NOT NULL DEFAULT '',
                [AvatarUrl] NVARCHAR(MAX) NULL,
                [Role] NVARCHAR(50) NOT NULL DEFAULT 'Parent',
                [IsEmailVerified] BIT NOT NULL DEFAULT 0,
                [LoginCount] INT NOT NULL DEFAULT 0,
                [FailedLoginAttempts] INT NOT NULL DEFAULT 0,
                [MustChangePassword] BIT NOT NULL DEFAULT 0,
                [IsActive] BIT NOT NULL DEFAULT 1,
                [LastLoginAt] DATETIME2 NULL,
                [IsDeleted] BIT NOT NULL DEFAULT 0,
                [DeletedAt] DATETIME2 NULL,
                [UpdatedAt] DATETIME2 NULL,
                [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
            );
        ");

        await context.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Notifications')
            CREATE TABLE [Notifications] (
                [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
                [UserId] UNIQUEIDENTIFIER NOT NULL,
                [Title] NVARCHAR(200) NOT NULL DEFAULT '',
                [Message] NVARCHAR(MAX) NOT NULL DEFAULT '',
                [Type] NVARCHAR(MAX) NULL,
                [IsRead] BIT NOT NULL DEFAULT 0,
                [TargetUrl] NVARCHAR(MAX) NULL,
                [IsDeleted] BIT NOT NULL DEFAULT 0,
                [DeletedAt] DATETIME2 NULL,
                [UpdatedAt] DATETIME2 NULL,
                [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id])
            );
        ");

        await context.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DeviceTokens')
            CREATE TABLE [DeviceTokens] (
                [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
                [UserId] UNIQUEIDENTIFIER NOT NULL,
                [Token] NVARCHAR(MAX) NOT NULL DEFAULT '',
                [DeviceType] NVARCHAR(MAX) NULL,
                [IsDeleted] BIT NOT NULL DEFAULT 0,
                [DeletedAt] DATETIME2 NULL,
                [UpdatedAt] DATETIME2 NULL,
                [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT [PK_DeviceTokens] PRIMARY KEY ([Id])
            );
        ");

        // Add missing columns to existing tables safely
        var tables = new[] { "Users", "Children", "Playlists", "Videos", "Notifications",
                             "Games", "GameSessions", "ChildSkills", "Medications",
                             "VideoViews", "ActivityLogs", "AuditLogs", "DeviceTokens",
                             "PasswordResetTokens", "RefreshTokens", "Reports", "ChatMessages",
                             "AppSettings", "DailyActivities", "Media" };

        foreach (var table in tables)
        {
            await context.Database.ExecuteSqlRawAsync($@"
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{table}')
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{table}' AND COLUMN_NAME = 'IsDeleted')
                        ALTER TABLE [{table}] ADD [IsDeleted] BIT NOT NULL DEFAULT 0;
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{table}' AND COLUMN_NAME = 'DeletedAt')
                        ALTER TABLE [{table}] ADD [DeletedAt] DATETIME2 NULL;
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{table}' AND COLUMN_NAME = 'UpdatedAt')
                        ALTER TABLE [{table}] ADD [UpdatedAt] DATETIME2 NULL;
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{table}' AND COLUMN_NAME = 'CreatedAt')
                        ALTER TABLE [{table}] ADD [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE();
                END
            ");
        }

        // Games.SkillKey
        await context.Database.ExecuteSqlRawAsync(@"
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Games')
            AND NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Games' AND COLUMN_NAME = 'SkillKey')
            BEGIN
                ALTER TABLE [Games] ADD [SkillKey] NVARCHAR(50) NULL;
            END
        ");

        // Specific column fixes for existing tables to prevent runtime errors:
        // ChildSkills.Level and ChildSkills.Notes
        await context.Database.ExecuteSqlRawAsync(@"
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ChildSkills')
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ChildSkills' AND COLUMN_NAME = 'Level')
                    ALTER TABLE [ChildSkills] ADD [Level] INT NOT NULL DEFAULT 1;
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ChildSkills' AND COLUMN_NAME = 'Notes')
                    ALTER TABLE [ChildSkills] ADD [Notes] NVARCHAR(MAX) NULL;
            END
        ");

        // Notifications.TargetUrl
        await context.Database.ExecuteSqlRawAsync(@"
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Notifications')
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Notifications' AND COLUMN_NAME = 'TargetUrl')
                    ALTER TABLE [Notifications] ADD [TargetUrl] NVARCHAR(MAX) NULL;
            END
        ");

        // Children table additions
        await context.Database.ExecuteSqlRawAsync(@"
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Children')
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Children' AND COLUMN_NAME = 'NationalId')
                    ALTER TABLE [Children] ADD [NationalId] NVARCHAR(450) NULL;
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Children' AND COLUMN_NAME = 'Gender')
                    ALTER TABLE [Children] ADD [Gender] NVARCHAR(20) NOT NULL DEFAULT 'Male';
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Children' AND COLUMN_NAME = 'EducationLevel')
                    ALTER TABLE [Children] ADD [EducationLevel] NVARCHAR(MAX) NULL;
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Children' AND COLUMN_NAME = 'ImageUrl')
                    ALTER TABLE [Children] ADD [ImageUrl] NVARCHAR(MAX) NULL;
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Children' AND COLUMN_NAME = 'UsageHours')
                    ALTER TABLE [Children] ADD [UsageHours] FLOAT NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Children' AND COLUMN_NAME = 'IsActive')
                    ALTER TABLE [Children] ADD [IsActive] BIT NOT NULL DEFAULT 1;
            END
        ");

        // Medications table additions
        await context.Database.ExecuteSqlRawAsync(@"
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Medications')
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Medications' AND COLUMN_NAME = 'Dosage')
                    ALTER TABLE [Medications] ADD [Dosage] NVARCHAR(MAX) NULL;
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Medications' AND COLUMN_NAME = 'ScheduleJson')
                    ALTER TABLE [Medications] ADD [ScheduleJson] NVARCHAR(MAX) NULL;
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Medications' AND COLUMN_NAME = 'Frequency')
                    ALTER TABLE [Medications] ADD [Frequency] INT NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Medications' AND COLUMN_NAME = 'Notes')
                    ALTER TABLE [Medications] ADD [Notes] NVARCHAR(MAX) NULL;
            END
        ");
    }

    public static async Task CleanupAndSyncDatabaseAsync(AppDbContext context)
    {
        // 1. Normalize and merge games in the database
        var games = await context.Games.ToListAsync();
        foreach (var g in games)
        {
            var normalizedName = AlFolk.Api.Services.ArabicNormalization.NormalizeGameName(g.Name);
            if (g.Name != normalizedName)
            {
                // We need to rename or merge this game!
                var targetGame = await context.Games.FirstOrDefaultAsync(x => x.Name == normalizedName);
                if (targetGame != null)
                {
                    // Merge: update all game sessions pointing to this game to point to the target game
                    var sessionsToUpdate = await context.GameSessions.Where(s => s.GameId == g.Id).ToListAsync();
                    foreach (var session in sessionsToUpdate)
                    {
                        session.GameId = targetGame.Id;
                    }
                    
                    // Also update activity logs referencing this game
                    var logsToUpdate = await context.ActivityLogs
                        .Where(l => l.EntityType == "Game" && l.ReferenceId == g.Id)
                        .ToListAsync();
                    foreach (var log in logsToUpdate)
                    {
                        log.ReferenceId = targetGame.Id;
                    }
                    
                    context.Games.Remove(g);
                }
                else
                {
                    // Simply rename
                    g.Name = normalizedName;
                    g.SkillKey = AlFolk.Api.Services.ArabicNormalization.GetSkillKeyFromGameName(normalizedName).ToLower();
                }
            }
        }
        await context.SaveChangesAsync();

        // 1.5 Set Score = 0 for all non-scoring drawing games
        var nonScoringNames = new[] { "خربش الصور", "ارسم ولون", "scribble", "animalcoloring", "drawandcolor", "coloring", "doodle" };
        var sessionsToZero = await context.GameSessions
            .Include(gs => gs.Game)
            .Where(gs => gs.Game != null && nonScoringNames.Contains(gs.Game.Name))
            .ToListAsync();

        foreach (var session in sessionsToZero)
        {
            session.Score = 0;
        }
        await context.SaveChangesAsync();

        // 2. Synchronize ChildSkills points with GameSessions sum
        var children = await context.Children
            .Include(c => c.Skills)
            .Include(c => c.GameSessions)
            .ThenInclude(gs => gs.Game)
            .ToListAsync();
            
        foreach (var child in children)
        {
            // Group sessions by normalised lowercase skill key
            var sessionGroups = child.GameSessions
                .Where(gs => gs.Game != null)
                .GroupBy(gs => AlFolk.Api.Services.ArabicNormalization.GetSkillKeyFromGameName(gs.Game.Name),
                         StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key.ToLower(), g => g.Sum(gs => gs.Score), StringComparer.OrdinalIgnoreCase);

            foreach (var skill in child.Skills)
            {
                // Normalise stored key (Language → letters, Numbers → skip)
                var normalizedKey = skill.SkillKey.Equals("Language", StringComparison.OrdinalIgnoreCase)
                    ? "letters"
                    : skill.SkillKey.ToLower();

                // Skip legacy "numbers" skill — no Arabic games map to it
                if (normalizedKey == "numbers") continue;

                sessionGroups.TryGetValue(normalizedKey, out int sessionPoints);

                // Update skill points to match session points exactly!
                if (skill.Points > sessionPoints)
                {
                    var diff = skill.Points - sessionPoints;
                    
                    // Map skill key back to a default game to store the points
                    string targetGameName = normalizedKey switch
                    {
                        "colors" => "الألوان",
                        "shapes" => "الشكل و ظله",
                        "memory" => "الذاكره",
                        "letters" => "اصوات حيوانات",
                        _ => "الذاكره"
                    };

                    var game = await context.Games.FirstOrDefaultAsync(g => g.Name == targetGameName);
                    if (game == null)
                    {
                        game = new Game { Name = targetGameName, SkillKey = normalizedKey };
                        context.Games.Add(game);
                        await context.SaveChangesAsync();
                    }

                    context.GameSessions.Add(new GameSession
                    {
                        ChildId = child.Id,
                        GameId = game.Id,
                        Score = diff,
                        DurationSeconds = 0,
                        PlayedAt = DateTime.UtcNow
                    });
                }
                else if (skill.Points < sessionPoints)
                {
                    skill.Points = sessionPoints;
                }
                
                // progress: base 20% + 0.1 per point, capped at 100
                skill.Progress = Math.Min(100.0, 20.0 + (skill.Points * 0.1));
            }
        }
        await context.SaveChangesAsync();
    }
}
