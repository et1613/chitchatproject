using System.Net.WebSockets;

namespace WebApplication1
{
    public class User
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<ChatRoom> ChatRooms { get; set; } 
        public List<User> BlockedUsers { get; set; } 
        public List<User> Friends { get; set; } 

        public void SendMessage(string receiverId, string content) { }
        public void JoinChatRoom(string chatRoomId) { }
        public void LeaveChatRoom(string chatRoomId) { }
        public void UpdateProfile(string name, string email) { }

        public void SendFriendRequest(string userId) { }

        public void ResponseFriendRequest(string userId) { }

        public void BlockUser(string userId) { }

        public void UnblockUser(string userId) { }
    }

    public enum MessageStatus
    {
        Sent,
        Delivered,
        Read
    }

    public class Message
    {
        public string Id { get; set; }
        public string SenderId { get; set; }
        public string ReceiverId { get; set; }
        public string ChatRoomId { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
        public bool IsEdited { get; set; }
        public List<Attachment> Attachments { get; set; }
        public MessageStatus Status { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime DeletedAt { get; set; }

        public void MarkAsRead() { }
        public void DeleteMessage() { }
        public void EditMessage(string newContent) { }
        public void UpdateStatus(MessageStatus newStatus) { }
        public void DeleteForEveryone() { }
        public void DeleteForUser(string userId) { }
    }

    public class MessageHistory
    {
        public string Id { get; set; }
        public string MessageId { get; set; }
        public string OldContent { get; set; }
        public DateTime EditedAt { get; set; }
        public string EditedByUserId { get; set; } 
        public string ChangeDescription { get; set; } 

        public void SaveOldVersion(string messageId, string oldContent) { }
    }

    public class DeletedMessage
    {
        public string Id { get; set; }
        public string MessageId { get; set; }
        public string DeletedByUserId { get; set; }
        public DateTime DeletedAt { get; set; }

        public void RestoreMessage() { }
    }

    public class ChatRoom
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string AdminId { get; set; }
        public List<User> Participants { get; set; } 
        public List<Message> Messages { get; set; }

        public void AddParticipant(User user) { }
        public void RemoveParticipant(User user) { }
        public void SendMessage(string senderId, string content) { }
    }

    public class Notification
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public string MessageId { get; set; }
        public string Type { get; set; }
        public bool Status { get; set; }
        public DateTime CreatedAt { get; set; }

        public void SendNotification() { }
        public void MarkAsRead() { }
    }

    public class Attachment
    {
        public string Id { get; set; }
        public string MessageId { get; set; }
        public string FileType { get; set; }
        public string FileUrl { get; set; }
        public long FileSize { get; set; }

        public void UploadAttachment() { }
        public void DeleteAttachment() { }
    }

    public class ConnectionManager
    {
        private Dictionary<string, WebSocket> Clients = new();

        public void AddClient(string userId, WebSocket connection) { }
        public void RemoveClient(string userId) { }
        public void SendMessageToClient(string userId, string message) { }
        public void BroadcastMessage(string message) { }
    }

    public class AuthService
    {
        public string Login(string email, string password) { return "Token"; }
        public User Register(string name, string email, string password) { return new User(); }
        public void Logout(string userId) { }
        public void ResetPassword(string email) { }
    }

    public class UserStatus
    {
        public string UserId { get; set; }
        public bool IsOnline { get; set; }
        public DateTime LastSeen { get; set; }

        public void UpdateStatus(bool isOnline) { }
    }

    public class EncryptionService
    {
        public string EncryptMessage(string content, string publicKey) { return "EncryptedData"; }
        public string DecryptMessage(string encryptedContent, string privateKey) { return "DecryptedData"; }
    }

    public class HashingService
    {
        public string GenerateHash(string content) { return "HashValue"; }
        public bool VerifyHash(string content, string hash) { return true; }
    }

    public class DigitalSignatureService
    {
        public string SignMessage(string content, string privateKey) { return "Signature"; }
        public bool VerifySignature(string content, string signature, string publicKey) { return true; }
    }

    public class TokenStorage
    {
        private Dictionary<string, string> Tokens = new();

        public void StoreToken(string userId, string token) { }
        public bool ValidateToken(string token) { return true; }
        public void RevokeToken(string userId) { }
    }
}
