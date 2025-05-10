using System;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    public class AuthService
    {
        public string Login(string email, string password)
        {
            // Fake check
            return Guid.NewGuid().ToString();
        }

        public User Register(string name, string email, string password)
        {
            return new User { Name = name, Email = email, PasswordHash = password };
        }

        public void Logout(string userId) { }

        public void ResetPassword(string email) { }
    }
} 