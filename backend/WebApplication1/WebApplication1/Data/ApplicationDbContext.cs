using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.Chat;
using WebApplication1.Models.Messages;
using WebApplication1.Models.Notifications;
using WebApplication1.Models.Users;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebApplication1.Services;
using WebApplication1.Models.Enums;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace WebApplication1.Data
{
    public class ApplicationDbContext : IdentityDbContext<User>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public override DbSet<User> Users { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<ChatRoom> ChatRooms { get; set; }
        public DbSet<FriendRequest> FriendRequests { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<NotificationSettings> NotificationSettings { get; set; }
        public DbSet<Attachment> Attachments { get; set; }
        public DbSet<MessageHistory> MessageHistories { get; set; }
        public DbSet<BlockedUser> BlockedUsers { get; set; }
        public DbSet<UserActivity> UserActivities { get; set; }
        public DbSet<StoredToken> StoredTokens { get; set; }
        public DbSet<TokenBlacklist> TokenBlacklist { get; set; }
        public DbSet<UserSettings> UserSettings { get; set; }
        public DbSet<UserPreferences> UserPreferences { get; set; }
        public DbSet<MessageBackup> MessageBackups { get; set; }
        public DbSet<NotificationTemplate> NotificationTemplates { get; set; }
        public DbSet<NotificationGroup> NotificationGroups { get; set; }
        public DbSet<ScheduledNotification> ScheduledNotifications { get; set; }
        public DbSet<NotificationPreferences> NotificationPreferences { get; set; }
        public DbSet<SecurityEvent> SecurityEvents { get; set; }
        public DbSet<BlockedIpAddress> BlockedIpAddresses { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Value converter for Attachment.Metadata
            var dictionaryConverter = new ValueConverter<Dictionary<string, string>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions)null!) ?? new Dictionary<string, string>()
            );

            // Value converter for enum-keyed dictionary (NotificationType, NotificationChannel)
            var notificationTypeBoolDictConverter = new ValueConverter<Dictionary<WebApplication1.Models.Enums.NotificationType, bool>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<Dictionary<WebApplication1.Models.Enums.NotificationType, bool>>(v, (JsonSerializerOptions)null!) ?? new Dictionary<WebApplication1.Models.Enums.NotificationType, bool>()
            );
            var notificationChannelBoolDictConverter = new ValueConverter<Dictionary<WebApplication1.Models.Enums.NotificationChannel, bool>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<Dictionary<WebApplication1.Models.Enums.NotificationChannel, bool>>(v, (JsonSerializerOptions)null!) ?? new Dictionary<WebApplication1.Models.Enums.NotificationChannel, bool>()
            );
            var stringListConverter = new ValueConverter<List<string>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null!) ?? new List<string>()
            );

            // User configurations
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
                entity.Property(e => e.PasswordHash).IsRequired();
                entity.Property(e => e.DisplayName).HasMaxLength(100);
                entity.Property(e => e.Bio).HasMaxLength(500);
                entity.Property(e => e.ProfilePictureUrl).HasMaxLength(500);
                entity.Property(e => e.Status).HasDefaultValue(UserStatus.Offline);
                entity.Property(e => e.Role).HasDefaultValue(UserRole.Member);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.IsVerified).HasDefaultValue(false);

                // Self-referencing many-to-many relationship for Friends
                entity.HasMany(u => u.Friends)
                    .WithMany()
                    .UsingEntity(j => j.ToTable("UserFriends"));

                // One-to-one relationship with UserSettings
                entity.HasOne(u => u.UserSettings)
                    .WithOne()
                    .HasForeignKey<UserSettings>("UserId")
                    .OnDelete(DeleteBehavior.Cascade);

                // One-to-one relationship with UserPreferences
                entity.HasOne(u => u.UserPreferences)
                    .WithOne()
                    .HasForeignKey<UserPreferences>("UserId")
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ChatRoom configurations
            modelBuilder.Entity<ChatRoom>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.IsActive).HasDefaultValue(true);

                // Many-to-many relationship with Users
                entity.HasMany(c => c.Participants)
                    .WithMany(u => u.ChatRooms)
                    .UsingEntity(j => j.ToTable("ChatRoomParticipants"));

                entity.HasOne(c => c.Admin)
                    .WithMany()
                    .HasForeignKey(c => c.AdminId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Message configurations
            modelBuilder.Entity<Message>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.Timestamp).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.IsRead).HasDefaultValue(false);
                entity.Property(e => e.IsEdited).HasDefaultValue(false);
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.DeleteReason).HasMaxLength(500);

                // Relationships
                entity.HasOne(m => m.Sender)
                    .WithMany()
                    .HasForeignKey(m => m.SenderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.ChatRoom)
                    .WithMany(c => c.Messages)
                    .HasForeignKey(m => m.ChatRoomId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(m => m.ReplyToMessage)
                    .WithMany(m => m.Replies)
                    .HasForeignKey(m => m.ReplyToMessageId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Notification configurations
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Type).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.Status).HasDefaultValue(false);

                // Relationships
                entity.HasOne(n => n.User)
                    .WithMany(u => u.Notifications)
                    .HasForeignKey(n => n.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(n => n.Message)
                    .WithMany(m => m.Notifications)
                    .HasForeignKey(n => n.MessageId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // FriendRequest configurations
            modelBuilder.Entity<FriendRequest>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Status).HasDefaultValue(FriendRequestStatus.Pending);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Relationships
                entity.HasOne(f => f.Sender)
                    .WithMany(u => u.SentFriendRequests)
                    .HasForeignKey(f => f.SenderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(f => f.Receiver)
                    .WithMany(u => u.ReceivedFriendRequests)
                    .HasForeignKey(f => f.ReceiverId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // BlockedUser configurations
            modelBuilder.Entity<BlockedUser>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.BlockedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.BlockCount).HasDefaultValue(1);

                // Relationships
                entity.HasOne(b => b.BlockerUser)
                    .WithMany(u => u.BlockedUsers)
                    .HasForeignKey(b => b.BlockerUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(b => b.BlockedUserEntity)
                    .WithMany(u => u.BlockedByUsers)
                    .HasForeignKey(b => b.BlockedUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // UserActivity configurations
            modelBuilder.Entity<UserActivity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ActivityType).IsRequired();
                entity.Property(e => e.Description).IsRequired();
                entity.Property(e => e.Timestamp).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.IsSuccessful).HasDefaultValue(true);

                // Relationship
                entity.HasOne(a => a.User)
                    .WithMany(u => u.Activities)
                    .HasForeignKey(a => a.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // NotificationSettings configurations
            modelBuilder.Entity<NotificationSettings>(entity =>
            {
                entity.HasKey(e => e.UserId);
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.EmailNotifications).HasDefaultValue(true);
                entity.Property(e => e.PushNotifications).HasDefaultValue(true);
                entity.Property(e => e.InAppNotifications).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Relationship
                entity.HasOne(ns => ns.User)
                    .WithOne()
                    .HasForeignKey<NotificationSettings>("UserId")
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Attachment configurations
            modelBuilder.Entity<Attachment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.MessageId).IsRequired();
                entity.Property(e => e.Url).IsRequired();
                entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.FileType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.FileSize).IsRequired();
                entity.Property(e => e.UploadedBy).IsRequired();
                entity.Property(e => e.MimeType).IsRequired();
                entity.Property(e => e.UploadedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.Metadata)
                    .HasConversion(dictionaryConverter)
                    .HasColumnType("jsonb");

                // Relationship
                entity.HasOne(a => a.Message)
                    .WithMany(m => m.Attachments)
                    .HasForeignKey(a => a.MessageId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // MessageHistory configurations
            modelBuilder.Entity<MessageHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.MessageId).IsRequired();
                entity.Property(e => e.OldContent).IsRequired();
                entity.Property(e => e.NewContent).IsRequired();
                entity.Property(e => e.EditedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.Metadata)
                    .HasConversion(dictionaryConverter)
                    .HasColumnType("jsonb");

                // Relationship
                entity.HasOne(mh => mh.Message)
                    .WithMany(m => m.EditHistory)
                    .HasForeignKey(mh => mh.MessageId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // StoredToken configurations
            modelBuilder.Entity<StoredToken>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.TokenType).IsRequired();
                entity.Property(e => e.Token).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.ExpiresAt).IsRequired();
                entity.Property(e => e.IsRevoked).HasDefaultValue(false);
                entity.Property(e => e.RevocationReason).HasMaxLength(500);
                entity.Property(e => e.Metadata).HasColumnType("jsonb");
                entity.Property(e => e.UsageCount).HasDefaultValue(0);
                entity.Property(e => e.LastUsedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Indexes
                entity.HasIndex(t => t.Token);
                entity.HasIndex(t => t.ExpiresAt);
                entity.HasIndex(t => t.UserId);
            });

            // TokenBlacklist configurations
            modelBuilder.Entity<TokenBlacklist>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Token).IsRequired();
                entity.Property(e => e.AddedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Indexes
                entity.HasIndex(t => t.Token);
                entity.HasIndex(t => t.AddedAt);
            });

            // UserSettings configurations
            modelBuilder.Entity<UserSettings>(entity =>
            {
                entity.HasKey(e => e.UserId);
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.Theme).HasDefaultValue("light");
                entity.Property(e => e.Language).HasDefaultValue("en");
                entity.Property(e => e.TimeZone).HasDefaultValue("UTC");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Relationship
                entity.HasOne(us => us.User)
                    .WithOne()
                    .HasForeignKey<UserSettings>("UserId")
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // UserPreferences configurations
            modelBuilder.Entity<UserPreferences>(entity =>
            {
                entity.HasKey(e => e.UserId);
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.DisplayName).IsRequired();
                entity.Property(e => e.IsEmailPublic).HasDefaultValue(true);
                entity.Property(e => e.IsOnlineStatusPublic).HasDefaultValue(true);
                entity.Property(e => e.IsReadReceiptsPublic).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Relationship
                entity.HasOne(up => up.User)
                    .WithOne()
                    .HasForeignKey<UserPreferences>(up => up.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // MessageBackup configurations
            modelBuilder.Entity<MessageBackup>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ChatRoomId).IsRequired();
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.BackupPath).IsRequired();
                entity.Property(e => e.MessageCount).HasDefaultValue(0);
                entity.Property(e => e.BackupSize).HasDefaultValue(0L);
            });

            // NotificationTemplate configurations
            modelBuilder.Entity<NotificationTemplate>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.Type).IsRequired();
                entity.Property(e => e.Parameters).HasColumnType("jsonb");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // NotificationGroup configurations
            modelBuilder.Entity<NotificationGroup>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.UserIds).HasColumnType("jsonb");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // ScheduledNotification configurations
            modelBuilder.Entity<ScheduledNotification>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.Type).IsRequired();
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.ScheduledTime).IsRequired();
                entity.Property(e => e.IsRecurring).HasDefaultValue(false);
                entity.Property(e => e.RecurrencePattern).HasMaxLength(100);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // NotificationPreferences configurations
            modelBuilder.Entity<NotificationPreferences>(entity =>
            {
                entity.HasKey(e => e.UserId);
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.EnabledTypes)
                    .HasConversion(notificationTypeBoolDictConverter)
                    .HasColumnType("jsonb");
                entity.Property(e => e.EnabledChannels)
                    .HasConversion(notificationChannelBoolDictConverter)
                    .HasColumnType("jsonb");
                entity.Property(e => e.BlockedSenders)
                    .HasConversion(stringListConverter)
                    .HasColumnType("jsonb");
                entity.Property(e => e.QuietHoursStart).IsRequired();
                entity.Property(e => e.QuietHoursEnd).IsRequired();

                // Relationship
                entity.HasOne(np => np.User)
                    .WithOne(u => u.NotificationPreferences)
                    .HasForeignKey<NotificationPreferences>(np => np.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // SecurityEvent configurations
            modelBuilder.Entity<SecurityEvent>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.EventType).IsRequired();
                entity.Property(e => e.Description).IsRequired();
                entity.Property(e => e.IpAddress).HasMaxLength(45);
                entity.Property(e => e.Timestamp).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // BlockedIpAddress configurations
            modelBuilder.Entity<BlockedIpAddress>(entity =>
            {
                entity.HasKey(e => e.IpAddress);
                entity.Property(e => e.IpAddress).IsRequired().HasMaxLength(45);
                entity.Property(e => e.Reason).IsRequired();
                entity.Property(e => e.BlockedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.ExpiresAt).IsRequired();

                // Indexes
                entity.HasIndex(b => b.ExpiresAt);
            });

            // RefreshToken configurations
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Token).IsRequired();
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.ExpiryDate).IsRequired();
                entity.Property(e => e.IsValid).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Relationship
                entity.HasOne(rt => rt.User)
                    .WithMany()
                    .HasForeignKey(rt => rt.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Indexes
                entity.HasIndex(rt => rt.Token);
                entity.HasIndex(rt => rt.UserId);
                entity.HasIndex(rt => rt.ExpiryDate);
            });
        }
    }
} 