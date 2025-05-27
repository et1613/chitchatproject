using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.Chat;
using WebApplication1.Models.Messages;
using WebApplication1.Models.Notifications;
using WebApplication1.Models.Users;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebApplication1.Services;

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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User configurations
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.UserName)
                .IsUnique();

            // UserFriends configurations
            modelBuilder.Entity<User>()
                .HasMany(u => u.Friends)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "UserFriends",
                    j => j.HasOne<User>().WithMany().HasForeignKey("FriendId"),
                    j => j.HasOne<User>().WithMany().HasForeignKey("UserId"));

            // BlockedUser configurations
            modelBuilder.Entity<BlockedUser>()
                .HasOne(b => b.BlockerUser)
                .WithMany(u => u.BlockedUsers)
                .HasForeignKey(b => b.BlockerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<BlockedUser>()
                .HasOne(b => b.BlockedUserEntity)
                .WithMany(u => u.BlockedByUsers)
                .HasForeignKey(b => b.BlockedUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Message configurations
            modelBuilder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany(u => u.Messages)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            // ChatRoom configurations
            modelBuilder.Entity<ChatRoom>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(c => c.AdminId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ChatRoom>()
                .HasMany(c => c.Participants)
                .WithMany(u => u.ChatRooms)
                .UsingEntity<Dictionary<string, object>>(
                    "ChatRoomParticipants",
                    j => j.HasOne<User>().WithMany().HasForeignKey("UserId"),
                    j => j.HasOne<ChatRoom>().WithMany().HasForeignKey("ChatRoomId"));

            // FriendRequest configurations
            modelBuilder.Entity<FriendRequest>()
                .HasOne(f => f.Sender)
                .WithMany()
                .HasForeignKey(f => f.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FriendRequest>()
                .HasOne(f => f.Receiver)
                .WithMany()
                .HasForeignKey(f => f.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            // Notification configurations
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

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