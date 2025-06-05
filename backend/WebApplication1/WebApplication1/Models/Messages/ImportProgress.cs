using System;
using System.Collections.Generic;

namespace WebApplication1.Models.Messages
{
    public class ImportProgress
    {
        public required string ImportId { get; set; }
        public ImportStatus Status { get; set; }
        public int TotalMessages { get; set; }
        public int ProcessedMessages { get; set; }
        public int SuccessfulImports { get; set; }
        public int FailedImports { get; set; }
        public int ProgressPercentage { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan? EstimatedTimeRemaining { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, int> ImportErrors { get; set; } = new Dictionary<string, int>();
        public List<string> FailedMessageIds { get; set; } = new List<string>();
        public string? SourceFormat { get; set; }
        public long TotalFileSize { get; set; }
        public long ProcessedFileSize { get; set; }
        public string? CurrentOperation { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        public ImportProgress()
        {
            Status = ImportStatus.Pending;
            StartTime = DateTime.UtcNow;
        }

        public static ImportProgress CreateNew()
        {
            return new ImportProgress
            {
                ImportId = Guid.NewGuid().ToString(),
                Status = ImportStatus.Pending,
                StartTime = DateTime.UtcNow
            };
        }

        public static ImportProgress CreateWithId(string importId)
        {
            return new ImportProgress
            {
                ImportId = importId,
                Status = ImportStatus.Pending,
                StartTime = DateTime.UtcNow
            };
        }
    }
} 