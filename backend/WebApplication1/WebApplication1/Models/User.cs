using System;
using System.Collections.Generic;

namespace WebApplication1.Models
{
    public class User
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<ChatRoom> ChatRooms { get; set; } = new();
        public List<User> BlockedUsers { get; set; } = new();
        public List<User> Friends { get; set; } = new();

        public void JoinChatRoom(string chatRoomId)
        {
            var room = ChatService.GetChatRoomById(chatRoomId);
            room?.AddParticipant(this);
        }

        public void LeaveChatRoom(string chatRoomId)
        {
            var room = ChatService.GetChatRoomById(chatRoomId);
            room?.RemoveParticipant(this);
        }

        public void UpdateProfile(string name, string email)
        {
            Name = name;
            Email = email;
        }

        public void BlockUser(string userId)
        {
            var user = UserService.GetUserById(userId);
            if (user != null && !BlockedUsers.Contains(user))
                BlockedUsers.Add(user);
        }

        public void UnblockUser(string userId)
        {
            BlockedUsers.RemoveAll(u => u.Id == userId);
        }
    }
} 