using System;
using System.ComponentModel.DataAnnotations;

namespace Mindflow_Web_API.Models
{
    public class GoogleCalendarConnection : EntityBase
    {
        public Guid UserId { get; set; }

        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        // Encrypted tokens
        [MaxLength(2048)]
        public string EncryptedAccessToken { get; set; } = string.Empty;

        [MaxLength(2048)]
        public string EncryptedRefreshToken { get; set; } = string.Empty;

        public DateTime ExpiresAtUtc { get; set; }

        public DateTime ConnectedAtUtc { get; set; }

        public DateTime? LastSyncAtUtc { get; set; }

        public bool IsConnected { get; set; } = true;
    }
}


