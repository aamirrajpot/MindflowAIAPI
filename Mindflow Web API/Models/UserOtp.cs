using System;

namespace Mindflow_Web_API.Models
{
    public class UserOtp
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public string Otp { get; set; } = string.Empty;
        public DateTimeOffset Expiry { get; set; }
        public bool IsUsed { get; set; } = false;
    }
} 