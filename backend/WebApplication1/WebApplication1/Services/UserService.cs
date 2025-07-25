using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebApplication1.Data;
using WebApplication1.Models.Users;
using WebApplication1.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using WebApplication1.Models.Enums;
using WebApplication1.Repositories;
using WebApplication1.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace WebApplication1.Services
{
    public interface IUserService
    {
        Task<User?> GetUserByIdAsync(string id);
        Task<User?> GetUserByEmailAsync(string email);
        Task<User?> GetUserByUsernameAsync(string username);
        Task<IEnumerable<User>> GetAllUsersAsync();
        Task<IEnumerable<User>> SearchUsersAsync(string searchTerm);
        Task<User> CreateUserAsync(UserCreateDto userDto);
        Task<User> UpdateUserAsync(string id, UserUpdateDto userDto);
        Task<bool> DeleteUserAsync(string id);
        Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword);
        Task<bool> UpdateProfilePictureAsync(string userId, string imageUrl);
        Task<bool> UpdateUserStatusAsync(string userId, UserStatus status);
        Task<bool> AddFriendAsync(string userId, string friendId);
        Task<bool> RemoveFriendAsync(string userId, string friendId);
        Task<IEnumerable<User>> GetFriendsAsync(string userId);
        Task<bool> BlockUserAsync(string userId, string blockedUserId);
        Task<bool> UnblockUserAsync(string userId, string blockedUserId);
        Task<IEnumerable<User>> GetBlockedUsersAsync(string userId);
        Task<bool> UpdateUserSettingsAsync(string userId, UserSettings settings);
        Task<UserSettings?> GetUserSettingsAsync(string userId);
        Task<bool> UpdateUserPreferencesAsync(string userId, UserPreferences preferences);
        Task<UserPreferences?> GetUserPreferencesAsync(string userId);
        Task<bool> ValidateUserCredentialsAsync(string email, string password);
        Task<bool> IsEmailUniqueAsync(string email);
        Task<bool> IsUsernameUniqueAsync(string username);
        Task<bool> UpdateLastSeenAsync(string userId);
        Task<IEnumerable<UserActivity>> GetUserActivitiesAsync(string userId, int limit = 10);
        Task SendFriendRequestNotificationAsync(string toEmail, string fromUserName);
        Task SendPasswordChangeNotificationAsync(string toEmail, string userName);
        Task<bool> SendFriendRequestAsync(string senderId, string receiverId, string? message = null);
        Task<List<FriendRequest>> GetReceivedFriendRequestsAsync(string userId);
        Task<bool> AcceptFriendRequestAsync(string requestId, string userId);
        Task<bool> RejectFriendRequestAsync(string requestId, string userId, string? reason = null);
        Task UpdateOnlineStatusAsync(string userId, bool isOnline);
    }

    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserService> _logger;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IEmailService _emailService;
        private readonly ISecurityService _securityService;
        private readonly IConfiguration _configuration;
        private readonly IUserRepository _userRepository;
        private readonly IHubContext<ChatHub> _hubContext;

        public UserService(
            ApplicationDbContext context,
            ILogger<UserService> logger,
            IPasswordHasher<User> passwordHasher,
            IEmailService emailService,
            ISecurityService securityService,
            IConfiguration configuration,
            IUserRepository userRepository,
            IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _logger = logger;
            _passwordHasher = passwordHasher;
            _emailService = emailService;
            _securityService = securityService;
            _configuration = configuration;
            _userRepository = userRepository;
            _hubContext = hubContext;
        }

        public async Task<User?> GetUserByIdAsync(string id)
        {
            try
            {
                return await _context.Users
                    .Include(u => u.Friends)
                    .Include(u => u.BlockedUsers)
                    .Include(u => u.UserSettings)
                    .Include(u => u.UserPreferences)
                    .FirstOrDefaultAsync(u => u.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by ID: {UserId}", id);
                throw;
            }
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            try
            {
                return await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by email: {Email}", email);
                throw;
            }
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            try
            {
                return await _context.Users
                    .FirstOrDefaultAsync(u => u.UserName == username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by username: {Username}", username);
                throw;
            }
        }

        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            try
            {
                return await _context.Users
                    .Include(u => u.UserSettings)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all users");
                throw;
            }
        }

        public async Task<IEnumerable<User>> SearchUsersAsync(string searchTerm)
        {
            try
            {
                return await _context.Users
                    .Where(u => EF.Property<string>(u, "DisplayName").Contains(searchTerm) ||
                                u.Email.Contains(searchTerm) ||
                                u.UserName.Contains(searchTerm))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching users with term: {SearchTerm}", searchTerm);
                throw;
            }
        }

        public async Task<User> CreateUserAsync(UserCreateDto userDto)
        {
            try
            {
                if (!await IsEmailUniqueAsync(userDto.Email))
                    throw new InvalidOperationException("Email already exists");

                if (!await IsUsernameUniqueAsync(userDto.UserName))
                    throw new InvalidOperationException("Username already exists");

                var user = new User
                {
                    UserName = userDto.UserName,
                    Email = userDto.Email,
                    DisplayName = userDto.DisplayName,
                    CreatedAt = DateTime.UtcNow,
                    Status = UserStatus.Offline,
                    IsOnline = false,
                    LastSeen = DateTime.UtcNow,
                    IsActive = true,
                    IsVerified = false,
                    Role = UserRole.Member
                };

                // Initialize UserSettings with required members
                user.UserSettings = new UserSettings
                {
                    User = user,
                    UserId = user.Id,
                    TimeZone = "UTC",
                    Language = "en",
                    Theme = "light",
                    NotificationsEnabled = true,
                    EmailNotificationsEnabled = true,
                    PushNotificationsEnabled = true,
                    SoundEnabled = true,
                    RememberMe = true,
                    SessionTimeout = 30, // minutes
                    ShowOnlineStatus = true,
                    ShowLastSeen = true,
                    ShowReadReceipts = true,
                    ShowTypingIndicator = true,
                    AutoSaveDrafts = true,
                    DraftAutoSaveInterval = 5, // minutes
                    EnableMessageSearch = true,
                    EnableFileSharing = true,
                    MaxFileSize = 10 * 1024 * 1024, // 10MB
                    AllowedFileTypes = new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf", ".doc", ".docx", ".txt" },
                    EnableVoiceMessages = true,
                    EnableVideoCalls = true,
                    EnableScreenSharing = true,
                    EnableLocationSharing = false,
                    EnableContactSync = true,
                    EnableCalendarSync = true,
                    EnableTaskSync = true,
                    EnableNoteSync = true,
                    EnableCloudBackup = false,
                    BackupFrequency = 24, // hours
                    LastBackup = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Initialize UserPreferences with required members
                user.UserPreferences = new UserPreferences
                {
                    User = user,
                    UserId = user.Id,
                    DisplayName = userDto.DisplayName,
                    IsPhoneNumberPublic = false,
                    IsEmailPublic = false,
                    IsLocationPublic = false,
                    IsOnlineStatusPublic = true,
                    IsLastSeenPublic = true,
                    IsReadReceiptsPublic = true,
                    IsTypingIndicatorPublic = true,
                    IsProfilePicturePublic = true,
                    IsBioPublic = true,
                    IsWebsitePublic = false,
                    IsActivityStatusPublic = true,
                    IsFriendListPublic = true,
                    IsGroupListPublic = true,
                    IsMessageHistoryPublic = false,
                    IsMediaGalleryPublic = true,
                    IsTaggedPhotosPublic = true,
                    IsCheckInsPublic = false,
                    IsEventsPublic = true,
                    IsNotesPublic = false,
                    IsTasksPublic = false,
                    IsCalendarPublic = false,
                    IsContactListPublic = false,
                    IsDeviceListPublic = false,
                    IsLoginHistoryPublic = false,
                    IsSecuritySettingsPublic = false,
                    IsNotificationSettingsPublic = false,
                    IsPrivacySettingsPublic = false,
                    IsBlockedUsersListPublic = false,
                    IsMutedUsersListPublic = false,
                    IsRestrictedUsersListPublic = false,
                    IsReportedUsersListPublic = false,
                    IsDeletedMessagesListPublic = false,
                    IsArchivedMessagesListPublic = false,
                    IsStarredMessagesListPublic = false,
                    IsPinnedMessagesListPublic = false,
                    IsSavedItemsListPublic = false,
                    IsRecentSearchesListPublic = false,
                    IsRecentContactsListPublic = false,
                    IsRecentGroupsListPublic = false,
                    IsRecentFilesListPublic = false,
                    IsRecentLinksListPublic = false,
                    IsRecentLocationsListPublic = false,
                    IsRecentEventsListPublic = false,
                    IsRecentNotesListPublic = false,
                    IsRecentTasksListPublic = false,
                    IsRecentCalendarItemsListPublic = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                user.PasswordHash = _passwordHasher.HashPassword(user, userDto.Password);

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Send welcome email
                await _emailService.SendWelcomeEmailAsync(user.Email, user.DisplayName);

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user: {Email}", userDto.Email);
                throw;
            }
        }

        public async Task<User> UpdateUserAsync(string id, UserUpdateDto userDto)
        {
            try
            {
                var user = await GetUserByIdAsync(id);
                if (user == null)
                    throw new InvalidOperationException("User not found");

                if (!string.IsNullOrEmpty(userDto.DisplayName))
                    user.DisplayName = userDto.DisplayName;

                if (!string.IsNullOrEmpty(userDto.Bio))
                    user.Bio = userDto.Bio;

                if (!string.IsNullOrEmpty(userDto.ProfilePictureUrl))
                    user.ProfilePictureUrl = userDto.ProfilePictureUrl;

                await _context.SaveChangesAsync();
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user: {UserId}", id);
                throw;
            }
        }

        public async Task<bool> DeleteUserAsync(string id)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.Friends)
                    .Include(u => u.ChatRooms)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                    return false;

                // Remove from friends lists
                var friends = await _context.Users
                    .Where(u => u.Friends.Any(f => f.Id == id))
                    .ToListAsync();
                foreach (var friend in friends)
                {
                    friend.Friends.Remove(user);
                }

                // Remove friend requests
                var friendRequests = await _context.FriendRequests
                    .Where(fr => fr.SenderId == id || fr.ReceiverId == id)
                    .ToListAsync();
                _context.FriendRequests.RemoveRange(friendRequests);

                // Remove blocked users
                var blockedUsers = await _context.BlockedUsers
                    .Where(bu => bu.BlockerUserId == id || bu.BlockedUserId == id)
                    .ToListAsync();
                _context.BlockedUsers.RemoveRange(blockedUsers);

                // Remove notifications
                var notifications = await _context.Notifications
                    .Where(n => n.UserId == id)
                    .ToListAsync();
                _context.Notifications.RemoveRange(notifications);

                // Remove user activities
                var userActivities = await _context.UserActivities
                    .Where(ua => ua.UserId == id)
                    .ToListAsync();
                _context.UserActivities.RemoveRange(userActivities);

                // Remove refresh tokens
                var refreshTokens = await _context.StoredTokens
                    .Where(rt => rt.UserId == id)
                    .ToListAsync();
                _context.StoredTokens.RemoveRange(refreshTokens);
                
                // Remove from chat rooms and handle admin role
                var chatRooms = await _context.ChatRooms
                    .Include(cr => cr.Participants)
                    .Where(cr => cr.Participants.Any(p => p.Id == id) || cr.AdminId == id)
                    .ToListAsync();

                foreach (var chatRoom in chatRooms)
                {
                    if (chatRoom.AdminId == id)
                    {
                        // If user is the admin, either delete the room or re-assign admin
                        // For simplicity, we delete the chat room if the admin is deleted
                        // A more robust solution might re-assign the admin
                         _context.ChatRooms.Remove(chatRoom);
                    }
                    else
                    {
                        // If user is a participant, just remove them
                        var participant = chatRoom.Participants.FirstOrDefault(p => p.Id == id);
                        if (participant != null)
                        {
                            chatRoom.Participants.Remove(participant);
                        }
                    }
                }
                
                // Anonymize or delete messages
                var messages = await _context.Messages
                    .Where(m => m.SenderId == id)
                    .ToListAsync();
                //Option 1: Delete messages
                _context.Messages.RemoveRange(messages);
                // Option 2: Anonymize messages (requires a nullable SenderId or a placeholder "deleted" user)
                // foreach (var message in messages)
                // {
                //     message.SenderId = null; // This requires SenderId to be nullable
                // }


                // Finally, remove the user
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user: {UserId}", id);
                throw;
            }
        }

        public async Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
        {
            try
            {
                var user = await GetUserByIdAsync(userId);
                if (user == null)
                    return false;

                if (string.IsNullOrEmpty(user.PasswordHash))
                {
                    _logger.LogWarning("User {UserId} has no password hash set", userId);
                    return false;
                }

                var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
                if (result != PasswordVerificationResult.Success)
                    return false;

                user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
                await _context.SaveChangesAsync();

                // Send password change notification email
                if (!string.IsNullOrEmpty(user.Email) && !string.IsNullOrEmpty(user.DisplayName))
                {
                    await _emailService.SendPasswordChangeNotificationAsync(user.Email, user.DisplayName);
                }
                else
                {
                    _logger.LogWarning("Could not send password change notification for user {UserId} - missing email or display name", userId);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user: {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> UpdateProfilePictureAsync(string userId, string imageUrl)
        {
            try
            {
                var user = await GetUserByIdAsync(userId);
                if (user == null)
                    return false;

                user.ProfilePictureUrl = imageUrl;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile picture for user: {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> UpdateUserStatusAsync(string userId, UserStatus status)
        {
            try
            {
                var user = await GetUserByIdAsync(userId);
                if (user == null)
                    return false;

                user.Status = status;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user status: {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> AddFriendAsync(string userId, string friendId)
        {
            try
            {
                // Prevent self-friendship
                if (userId == friendId)
                    return false;

                var user = await GetUserByIdAsync(userId);
                var friend = await GetUserByIdAsync(friendId);

                if (user == null || friend == null)
                    return false;

                // Check if already friends
                if (user.Friends.Any(f => f.Id == friendId))
                    return false;

                // Check if blocked
                if (user.BlockedUsers.Any(b => b.BlockedUserId == friendId) ||
                    friend.BlockedUsers.Any(b => b.BlockedUserId == userId))
                    return false;

                user.Friends.Add(friend);
                await _context.SaveChangesAsync();

                // Send friend request notification
                if (!string.IsNullOrEmpty(friend.Email) && !string.IsNullOrEmpty(user.DisplayName))
                {
                    await _emailService.SendFriendRequestNotificationAsync(friend.Email, user.DisplayName);
                }
                else
                {
                    _logger.LogWarning("Could not send friend request notification - missing email or display name for user {UserId}", userId);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding friend: {UserId} -> {FriendId}", userId, friendId);
                throw;
            }
        }

        public async Task<bool> RemoveFriendAsync(string userId, string friendId)
        {
            try
            {
                var user = await GetUserByIdAsync(userId);
                var friend = await GetUserByIdAsync(friendId);

                if (user == null || friend == null)
                    return false;

                user.Friends.Remove(friend);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing friend: {UserId} -> {FriendId}", userId, friendId);
                throw;
            }
        }

        public async Task<IEnumerable<User>> GetFriendsAsync(string userId)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.Friends)
                    .Include(u => u.BlockedUsers)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    return new List<User>();
                }

                var blockedUserIds = user.BlockedUsers.Select(b => b.BlockedUserId).ToHashSet();

                var friends = user.Friends
                    .Where(f => f.Id != userId && !blockedUserIds.Contains(f.Id))
                    .ToList();

                return friends;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting friends for user: {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> BlockUserAsync(string userId, string blockedUserId)
        {
            try
            {
                // Check if already blocked
                if (await _context.BlockedUsers.AnyAsync(b => b.BlockerUserId == userId && b.BlockedUserId == blockedUserId))
                    return false;

                // Get both users
                var blockerUser = await _context.Users.FindAsync(userId);
                var blockedUser = await _context.Users.FindAsync(blockedUserId);

                if (blockerUser == null || blockedUser == null)
                    return false;

                var blockedUserEntity = new BlockedUser
                {
                    BlockerUserId = userId,
                    BlockedUserId = blockedUserId,
                    BlockedAt = DateTime.UtcNow,
                    BlockerUser = blockerUser,
                    BlockedUserEntity = blockedUser
                };

                _context.BlockedUsers.Add(blockedUserEntity);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error blocking user: {UserId} blocked {BlockedUserId}", userId, blockedUserId);
                throw;
            }
        }

        public async Task<bool> UnblockUserAsync(string userId, string blockedUserId)
        {
            try
            {
                var blockedUser = await _context.BlockedUsers
                    .FirstOrDefaultAsync(b => b.BlockerUserId == userId && b.BlockedUserId == blockedUserId);

                if (blockedUser == null)
                    return false;

                _context.BlockedUsers.Remove(blockedUser);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unblocking user: {UserId} -> {BlockedUserId}", userId, blockedUserId);
                throw;
            }
        }

        public async Task<IEnumerable<User>> GetBlockedUsersAsync(string userId)
        {
            try
            {
                return await _context.BlockedUsers
                    .Where(b => b.BlockerUserId == userId)
                    .Select(b => b.BlockedUserEntity)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting blocked users for user: {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> UpdateUserSettingsAsync(string userId, UserSettings settings)
        {
            try
            {
                var user = await GetUserByIdAsync(userId);
                if (user == null)
                    return false;

                user.UserSettings = settings;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user settings: {UserId}", userId);
                throw;
            }
        }

        public async Task<UserSettings?> GetUserSettingsAsync(string userId)
        {
            try
            {
                var user = await GetUserByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", userId);
                    return null;
                }

                return user.UserSettings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user settings: {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> UpdateUserPreferencesAsync(string userId, UserPreferences preferences)
        {
            try
            {
                var user = await GetUserByIdAsync(userId);
                if (user == null)
                    return false;

                user.UserPreferences = preferences;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user preferences: {UserId}", userId);
                throw;
            }
        }

        public async Task<UserPreferences?> GetUserPreferencesAsync(string userId)
        {
            try
            {
                var user = await GetUserByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", userId);
                    return null;
                }

                return user.UserPreferences;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user preferences: {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> ValidateUserCredentialsAsync(string email, string password)
        {
            try
            {
                var user = await GetUserByEmailAsync(email);
                if (user == null)
                    return false;

                if (string.IsNullOrEmpty(user.PasswordHash))
                {
                    _logger.LogWarning("User {Email} has no password hash set", email);
                    return false;
                }

                var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
                return result == PasswordVerificationResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating user credentials: {Email}", email);
                throw;
            }
        }

        public async Task<bool> IsEmailUniqueAsync(string email)
        {
            try
            {
                return !await _context.Users.AnyAsync(u => u.Email == email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking email uniqueness: {Email}", email);
                throw;
            }
        }

        public async Task<bool> IsUsernameUniqueAsync(string username)
        {
            try
            {
                return !await _context.Users.AnyAsync(u => u.UserName == username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking username uniqueness: {Username}", username);
                throw;
            }
        }

        public async Task<bool> UpdateLastSeenAsync(string userId)
        {
            try
            {
                var user = await GetUserByIdAsync(userId);
                if (user == null)
                    return false;

                user.LastSeen = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating last seen for user: {UserId}", userId);
                throw;
            }
        }

        public async Task<IEnumerable<UserActivity>> GetUserActivitiesAsync(string userId, int limit = 10)
        {
            try
            {
                return await _context.UserActivities
                    .Where(a => a.UserId == userId)
                    .OrderByDescending(a => a.Timestamp)
                    .Take(limit)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user activities: {UserId}", userId);
                throw;
            }
        }

        public async Task SendFriendRequestNotificationAsync(string toEmail, string fromUserName)
        {
            try
            {
                var subject = "New Friend Request";
                var body = $@"
                    <h2>New Friend Request</h2>
                    <p>Hello,</p>
                    <p>{fromUserName} has sent you a friend request.</p>
                    <p>Click the link below to view and respond to the request:</p>
                    <p><a href='{_configuration["AppSettings:BaseUrl"]}/friends/requests'>View Friend Request</a></p>
                    <p>Best regards,<br>Your App Team</p>";

                await _emailService.SendEmailAsync(toEmail, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending friend request notification to {Email}", toEmail);
                throw;
            }
        }

        public async Task SendPasswordChangeNotificationAsync(string toEmail, string userName)
        {
            try
            {
                var subject = "Password Changed";
                var body = $@"
                    <h2>Password Changed</h2>
                    <p>Hello {userName},</p>
                    <p>Your password has been successfully changed.</p>
                    <p>If you did not make this change, please contact support immediately.</p>
                    <p>Best regards,<br>Your App Team</p>";

                await _emailService.SendEmailAsync(toEmail, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending password change notification to {Email}", toEmail);
                throw;
            }
        }

        // FRIEND REQUEST SYSTEM
        public async Task<bool> SendFriendRequestAsync(string senderId, string receiverId, string? message = null)
        {
            _logger.LogInformation($"[FRIEND-REQUEST] senderId={senderId}, receiverId={receiverId}");
            var receiverUser = await GetUserByIdAsync(receiverId);
            if (receiverUser != null)
                _logger.LogInformation($"[FRIEND-REQUEST] receiverUser: id={receiverUser.Id}, email={receiverUser.Email}");
            if (senderId == receiverId)
                return false;

            var sender = await GetUserByIdAsync(senderId);
            var receiver = receiverUser;
            if (sender == null || receiver == null)
                return false;

            // Zaten bekleyen bir istek var mı?
            if (_context.FriendRequests.Any(fr => fr.SenderId == senderId && fr.ReceiverId == receiverId && fr.Status == Models.Users.FriendRequestStatus.Pending))
                return false;

            // Zaten arkadaşlar mı?
            if (sender.Friends.Any(f => f.Id == receiverId))
                return false;

            var request = new Models.Users.FriendRequest
            {
                SenderId = senderId,
                Sender = sender,
                ReceiverId = receiverId,
                Receiver = receiver,
                Status = Models.Users.FriendRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                Message = message ?? string.Empty
            };
            _context.FriendRequests.Add(request);
            await _context.SaveChangesAsync();
            // Bildirim veya e-posta gönderilebilir
            return true;
        }

        public async Task<List<Models.Users.FriendRequest>> GetReceivedFriendRequestsAsync(string userId)
        {
            return await _context.FriendRequests
                .Include(fr => fr.Sender)
                .Where(fr => fr.ReceiverId == userId && fr.Status == Models.Users.FriendRequestStatus.Pending)
                .ToListAsync();
        }

        public async Task<bool> AcceptFriendRequestAsync(string requestId, string userId)
        {
            var request = await _context.FriendRequests.Include(fr => fr.Sender).Include(fr => fr.Receiver).FirstOrDefaultAsync(fr => fr.Id == requestId);
            if (request == null || request.ReceiverId != userId || request.Status != Models.Users.FriendRequestStatus.Pending)
                return false;
            request.Accept();
            // Karşılıklı arkadaş ekle
            request.Sender.Friends.Add(request.Receiver);
            request.Receiver.Friends.Add(request.Sender);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RejectFriendRequestAsync(string requestId, string userId, string? reason = null)
        {
            var request = await _context.FriendRequests.FirstOrDefaultAsync(fr => fr.Id == requestId);
            if (request == null || request.ReceiverId != userId || request.Status != Models.Users.FriendRequestStatus.Pending)
                return false;
            request.Reject(reason);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task UpdateOnlineStatusAsync(string userId, bool isOnline)
        {
            var user = await GetUserByIdAsync(userId);
            if (user != null)
            {
                user.IsOnline = isOnline;
                user.Status = isOnline ? UserStatus.Online : UserStatus.Offline;
                user.LastSeen = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
    }

    public class UserCreateDto
    {
        public required string UserName { get; set; }
        public required string Email { get; set; }
        public required string Password { get; set; }
        public required string DisplayName { get; set; }
    }

    public class UserUpdateDto
    {
        public required string DisplayName { get; set; }
        public string? Bio { get; set; }
        public string? ProfilePictureUrl { get; set; }
    }
} 