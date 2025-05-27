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

namespace WebApplication1.Services
{
    public interface IUserService
    {
        Task<User> GetUserByIdAsync(string id);
        Task<User> GetUserByEmailAsync(string email);
        Task<User> GetUserByUsernameAsync(string username);
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
        Task<UserSettings> GetUserSettingsAsync(string userId);
        Task<bool> UpdateUserPreferencesAsync(string userId, UserPreferences preferences);
        Task<UserPreferences> GetUserPreferencesAsync(string userId);
        Task<bool> ValidateUserCredentialsAsync(string email, string password);
        Task<bool> IsEmailUniqueAsync(string email);
        Task<bool> IsUsernameUniqueAsync(string username);
        Task<bool> UpdateLastSeenAsync(string userId);
        Task<IEnumerable<UserActivity>> GetUserActivitiesAsync(string userId, int limit = 10);
    }

    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserService> _logger;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IEmailService _emailService;
        private readonly ISecurityService _securityService;

        public UserService(
            ApplicationDbContext context,
            ILogger<UserService> logger,
            IPasswordHasher<User> passwordHasher,
            IEmailService emailService,
            ISecurityService securityService)
        {
            _context = context;
            _logger = logger;
            _passwordHasher = passwordHasher;
            _emailService = emailService;
            _securityService = securityService;
        }

        public async Task<User> GetUserByIdAsync(string id)
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

        public async Task<User> GetUserByEmailAsync(string email)
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

        public async Task<User> GetUserByUsernameAsync(string username)
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
                    .Where(u => u.UserName.Contains(searchTerm) || 
                               u.Email.Contains(searchTerm) || 
                               u.DisplayName.Contains(searchTerm))
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
                    UserSettings = new UserSettings(),
                    UserPreferences = new UserPreferences()
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
                var user = await GetUserByIdAsync(id);
                if (user == null)
                    return false;

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

                var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
                if (result != PasswordVerificationResult.Success)
                    return false;

                user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
                await _context.SaveChangesAsync();

                // Send password change notification email
                await _emailService.SendPasswordChangeNotificationAsync(user.Email);

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
                var user = await GetUserByIdAsync(userId);
                var friend = await GetUserByIdAsync(friendId);

                if (user == null || friend == null)
                    return false;

                if (user.Friends.Contains(friend))
                    return true; // Already friends

                user.Friends.Add(friend);
                await _context.SaveChangesAsync();

                // Send friend request notification
                await _emailService.SendFriendRequestNotificationAsync(friend.Email, user.DisplayName);

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
                var user = await GetUserByIdAsync(userId);
                return user?.Friends ?? new List<User>();
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
                var user = await GetUserByIdAsync(userId);
                var blockedUser = await GetUserByIdAsync(blockedUserId);

                if (user == null || blockedUser == null)
                    return false;

                var blockedUserEntity = new BlockedUser
                {
                    BlockerUserId = userId,
                    BlockedUserId = blockedUserId,
                    BlockedAt = DateTime.UtcNow
                };

                _context.BlockedUsers.Add(blockedUserEntity);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error blocking user: {UserId} -> {BlockedUserId}", userId, blockedUserId);
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

        public async Task<UserSettings> GetUserSettingsAsync(string userId)
        {
            try
            {
                var user = await GetUserByIdAsync(userId);
                return user?.UserSettings;
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

        public async Task<UserPreferences> GetUserPreferencesAsync(string userId)
        {
            try
            {
                var user = await GetUserByIdAsync(userId);
                return user?.UserPreferences;
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
    }

    public class UserCreateDto
    {
        public string UserName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string DisplayName { get; set; }
    }

    public class UserUpdateDto
    {
        public string DisplayName { get; set; }
        public string Bio { get; set; }
        public string ProfilePictureUrl { get; set; }
    }
} 