using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models.Users;

namespace WebApplication1.Repositories
{
    public class UserRepository : Repository<User>, IUserRepository
    {
        public UserRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == username);
        }

        public async Task<IEnumerable<User>> GetFriendsAsync(string userId)
        {
            var user = await _dbSet
                .Include(u => u.Friends)
                .FirstOrDefaultAsync(u => u.Id == userId);

            return user?.Friends ?? Enumerable.Empty<User>();
        }

        public async Task<IEnumerable<User>> GetBlockedUsersAsync(string userId)
        {
            return await _context.BlockedUsers
                .Include(b => b.BlockedUserEntity)
                .Where(b => b.BlockerUserId == userId && b.IsCurrentlyBlocked)
                .Select(b => b.BlockedUserEntity)
                .ToListAsync();
        }

        public async Task<IEnumerable<User>> GetBlockedByUsersAsync(string userId)
        {
            return await _context.BlockedUsers
                .Include(b => b.BlockerUser)
                .Where(b => b.BlockedUserId == userId && b.IsCurrentlyBlocked)
                .Select(b => b.BlockerUser)
                .ToListAsync();
        }

        public override async Task<User?> GetByIdAsync(string id)
        {
            return await _context.Users
                .Include(u => u.Friends)
                .Include(u => u.ChatRooms)
                .Include(u => u.SentFriendRequests)
                .Include(u => u.ReceivedFriendRequests)
                .Include(u => u.Notifications)
                .Include(u => u.BlockedUsers)
                .Include(u => u.BlockedByUsers)
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<IEnumerable<User>> SearchUsersAsync(string query)
        {
            return await _context.Users
                .Where(u => u.UserName.Contains(query) || u.Email.Contains(query))
                .ToListAsync();
        }
    }
} 