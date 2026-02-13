using System;

namespace Mindflow_Web_API.Models
{
    /// <summary>
    /// Represents a refresh token used to obtain new access tokens.
    /// </summary>
    public class RefreshToken : EntityBase
    {
        public Guid UserId { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public bool IsRevoked { get; set; } = false;
        public DateTime? RevokedAt { get; set; }
        public string? ReplacedByToken { get; set; } // Token rotation: track which token replaced this one
    }
}
