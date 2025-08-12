using System;

namespace Mindflow_Web_API.Models
{
    public class PaymentCard : EntityBase
    {
        public Guid UserId { get; set; }
        public string CardNumber { get; set; } = string.Empty; // Last 4 digits only for security
        public string CardholderName { get; set; } = string.Empty;
        public string ExpiryMonth { get; set; } = string.Empty; // e.g., "12"
        public string ExpiryYear { get; set; } = string.Empty; // e.g., "25"
        public string CardType { get; set; } = string.Empty; // "MasterCard", "Visa", etc.
        public bool IsDefault { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public string? LastFourDigits { get; set; } // For display purposes
        
        // Navigation properties
        public User User { get; set; } = null!;
    }
}
