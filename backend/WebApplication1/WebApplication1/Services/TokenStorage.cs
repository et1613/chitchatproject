namespace WebApplication1.Services
{
    public class TokenStorage
    {
        private Dictionary<string, string> Tokens = new();

        public void StoreToken(string userId, string token) { }
        public bool ValidateToken(string token) { return true; }
        public void RevokeToken(string userId) { }
    }
} 