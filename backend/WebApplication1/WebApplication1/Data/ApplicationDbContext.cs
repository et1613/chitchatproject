using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.Chat;
using WebApplication1.Models.Messages;
using WebApplication1.Models.Notifications;
using WebApplication1.Models.Users;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebApplication1.Services;
using WebApplication1.Models.Enums;

namespace WebApplication1.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<ChatRoom> ChatRooms { get; set; }
        public DbSet<FriendRequest> FriendRequests { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<NotificationSettings> NotificationSettings { get; set; }
        public DbSet<Attachment> Attachments { get; set; }
        public DbSet<MessageHistory> MessageHistories { get; set; }
        public DbSet<DeletedMessage> DeletedMessages { get; set; }
        public DbSet<BlockedUser> BlockedUsers { get; set; }
        public DbSet<UserActivity> UserActivities { get; set; }
        public DbSet<StoredToken> StoredTokens { get; set; }
        public DbSet<TokenBlacklist> TokenBlacklist { get; set; }
        public DbSet<UserSettings> UserSettings { get; set; }
        public DbSet<UserPreferences> UserPreferences { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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
            });

            // Message configurations
            modelBuilder.Entity<Message>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.Timestamp).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.IsRead).HasDefaultValue(false);
                entity.Property(e => e.IsEdited).HasDefaultValue(false);

                // Relationships
                entity.HasOne(m => m.Sender)
                    .WithMany()
                    .HasForeignKey(m => m.SenderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.ChatRoom)
                    .WithMany(c => c.Messages)
                    .HasForeignKey(m => m.ChatRoomId)
                    .OnDelete(DeleteBehavior.Cascade);
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
            modelBuilder.Entity<NotificationSettings>()
                .HasOne(ns => ns.User)
                .WithOne(u => u.NotificationSettings)
                .HasForeignKey<NotificationSettings>(ns => ns.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // StoredToken configurations
            modelBuilder.Entity<StoredToken>()
                .HasIndex(t => t.Token)
                .IsUnique();

            modelBuilder.Entity<StoredToken>()
                .HasIndex(t => new { t.UserId, t.TokenType });

            modelBuilder.Entity<StoredToken>()
                .Property(t => t.Metadata)
                .HasColumnType("nvarchar(max)");

            // TokenBlacklist configurations
            modelBuilder.Entity<TokenBlacklist>()
                .HasIndex(t => t.Token)
                .IsUnique();

            modelBuilder.Entity<TokenBlacklist>()
                .HasIndex(t => t.AddedAt);
        }
    }
} 