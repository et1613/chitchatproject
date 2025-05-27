using System.Collections.Generic;
using System.Linq;
using WebApplication1.Models;
using WebApplication1.Models.Users;

namespace WebApplication1.Services
{
    public static class UserService
    {
        public static List<User> Users = new();

        public static User GetUserById(string id) => Users.FirstOrDefault(u => u.Id == id);
    }
} 