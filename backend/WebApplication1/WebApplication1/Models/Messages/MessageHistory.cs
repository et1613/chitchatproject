using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.RegularExpressions;
using WebApplication1.Models.Enums;
using WebApplication1.Models.Users;

namespace WebApplication1.Models.Messages
{
    public class MessageHistory
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string MessageId { get; set; } = string.Empty;

        [ForeignKey("MessageId")]
        public required virtual Message Message { get; set; }

        [Required]
        public required string OldContent { get; set; }

        public string? NewContent { get; set; }

        [Required]
        public DateTime EditedAt { get; set; }

        [Required]
        public required string EditedByUserId { get; set; }

        [ForeignKey("EditedByUserId")]
        public required virtual User EditedByUser { get; set; }

        [Required]
        public EditType EditType { get; set; }

        public string? EditReason { get; set; }

        public string? ChangeDescription { get; set; }

        [NotMapped]
        public bool HasContentChanged =>
            !string.IsNullOrEmpty(NewContent) && !string.Equals(OldContent, NewContent, StringComparison.Ordinal);
        public string GetContentDiff()
        {
            if (string.IsNullOrEmpty(NewContent))
                return OldContent;

            var oldWords = Regex.Split(OldContent, @"\s+");
            var newWords = Regex.Split(NewContent, @"\s+");

            var diff = new List<string>();
            var maxLength = Math.Max(oldWords.Length, newWords.Length);

            for (int i = 0; i < maxLength; i++)
            {
                if (i >= oldWords.Length)
                {
                    diff.Add($"+{newWords[i]}");
                }
                else if (i >= newWords.Length)
                {
                    diff.Add($"-{oldWords[i]}");
                }
                else if (oldWords[i] != newWords[i])
                {
                    diff.Add($"-{oldWords[i]}");
                    diff.Add($"+{newWords[i]}");
                }
                else
                {
                    diff.Add(oldWords[i]);
                }
            }

            return string.Join(" ", diff);
        }

        public static IEnumerable<MessageHistory> GetEditHistoryByType(IEnumerable<MessageHistory> history, EditType editType)
        {
            return history.Where(h => h.EditType == editType).OrderByDescending(h => h.EditedAt);
        }

        public static IEnumerable<MessageHistory> GetEditHistoryByUser(IEnumerable<MessageHistory> history, string userId)
        {
            return history.Where(h => h.EditedByUserId == userId).OrderByDescending(h => h.EditedAt);
        }

        public static IEnumerable<MessageHistory> GetEditHistoryByDateRange(IEnumerable<MessageHistory> history, DateTime startDate, DateTime endDate)
        {
            return history.Where(h => h.EditedAt >= startDate && h.EditedAt <= endDate).OrderByDescending(h => h.EditedAt);
        }

        public override string ToString()
        {
            return $"[{EditType}] {EditedAt:g} - {ChangeDescription ?? "Düzenleme yapıldı"}";
        }
    }
}
