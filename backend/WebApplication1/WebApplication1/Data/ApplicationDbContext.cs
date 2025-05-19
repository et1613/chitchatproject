using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.Chat;
using WebApplication1.Models.Messages;
using WebApplication1.Models.Notifications;
using WebApplication1.Models.Users;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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
        public DbSet<Attachment> Attachments { get; set; }
        public DbSet<MessageHistory> MessageHistories { get; set; }
        public DbSet<DeletedMessage> DeletedMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User configurations  
            modelBuilder.Entity<User>()
                .HasMany(u => u.Friends)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "UserFriends",
                    j => j.HasOne<User>().WithMany().HasForeignKey("FriendId"),
                    j => j.HasOne<User>().WithMany().HasForeignKey("UserId"));

            modelBuilder.Entity<User>()
                .HasMany(u => u.BlockedUsers)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "UserBlockedUsers",
                    j => j.HasOne<User>().WithMany().HasForeignKey("BlockedUserId"),
                    j => j.HasOne<User>().WithMany().HasForeignKey("UserId"));

            // Message configurations  
            modelBuilder.Entity<Message>()
                .HasOne<User>()
                .WithMany()
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
        }
    }
} 