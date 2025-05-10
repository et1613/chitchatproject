using System;
using System.Collections.Generic;
using System.Linq;

namespace WebApplication1.Models
{
    public class FriendRequest
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FromUserId { get; set; }
        public string ToUserId { get; set; }
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    }

    public class FriendRequestService
    {
        private static List<FriendRequest> Requests = new();

        public void SendFriendRequest(string fromUserId, string toUserId)
        {
            if (Requests.Any(r => r.FromUserId == fromUserId && r.ToUserId == toUserId)) return;
            Requests.Add(new FriendRequest { FromUserId = fromUserId, ToUserId = toUserId });
        }

        public void AcceptFriendRequest(string fromUserId, string toUserId)
        {
            var request = Requests.FirstOrDefault(r => r.FromUserId == fromUserId && r.ToUserId == toUserId);
            if (request != null)
            {
                var fromUser = UserService.GetUserById(fromUserId);
                var toUser = UserService.GetUserById(toUserId);
                fromUser?.Friends.Add(toUser);
                toUser?.Friends.Add(fromUser);
                Requests.Remove(request);
            }
        }

        public void RejectFriendRequest(string fromUserId, string toUserId)
        {
            Requests.RemoveAll(r => r.FromUserId == fromUserId && r.ToUserId == toUserId);
        }
    }
} 