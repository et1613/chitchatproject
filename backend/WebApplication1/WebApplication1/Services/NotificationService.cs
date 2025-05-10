namespace WebApplication1.Services
{
    public static class NotificationService
    {
        public static void NotifyUser(string userId, string content)
        {
            // simulate push
            System.Console.WriteLine($"[Notification to {userId}] {content}");
        }
    }
} 