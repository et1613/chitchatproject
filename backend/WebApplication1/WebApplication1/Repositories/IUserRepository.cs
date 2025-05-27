using WebApplication1.Models.Users;

namespace WebApplication1.Repositories
{
    public interface IUserRepository : IRepository<User>
    {
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByUsernameAsync(string username);
        Task<IEnumerable<User>> GetFriendsAsync(string userId);
        Task<IEnumerable<User>> GetBlockedUsersAsync(string userId);
        Task<IEnumerable<User>> GetBlockedByUsersAsync(string userId);
    }
} 