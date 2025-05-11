using System.ComponentModel;

namespace WebApplication1.Models.Users
{
    public enum FriendRequestStatus
    {
        [Description("Beklemede")]
        Pending,    // İstek beklemede

        [Description("Kabul Edildi")]
        Accepted,   // İstek kabul edildi

        [Description("Reddedildi")]
        Rejected,   // İstek reddedildi

        [Description("Engellendi")]
        Blocked     // İstek engellendi
    }
} 