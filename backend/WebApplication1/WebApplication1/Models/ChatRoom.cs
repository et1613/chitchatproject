using System;
using System.Collections.Generic;
using System.Linq;

namespace WebApplication1.Models
{
    public class ChatRoom
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string AdminId { get; set; }
        public List<User> Participants { get; set; } = new();
        public List<Message> Messages { get; set; } = new();

        public void AddParticipant(User user)
        {
            if (!Participants.Contains(user)) Participants.Add(user);
        }

        public void RemoveParticipant(User user)
        {
            Participants.Remove(user);
        }

        public void SendMessage(string senderId, string content)
        {
            Messages.Add(new Message
            {
                SenderId = senderId,
                ChatRoomId = Id,
                Content = content
            });
        }

        public List<Message> GetVisibleMessagesForUser(string userId)
        {
            return Messages.Where(m => m.IsVisibleToUser(userId)).ToList();
        }
    }
} 