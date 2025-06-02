namespace WebApplication1.Models.Messages
{
    public enum ImportStatus
    {
        NotFound,
        Pending,
        InProgress,
        Completed,
        CompletedWithErrors,
        Failed,
        Cancelled
    }
} 