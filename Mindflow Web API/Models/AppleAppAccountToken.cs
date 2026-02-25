using System;

namespace Mindflow_Web_API.Models
{
    /// <summary>
    /// Maps an Apple appAccountToken (GUID) to a specific user.
    /// Issued by our API before purchase and later used by webhooks to resolve userId.
    /// </summary>
    public class AppleAppAccountToken : EntityBase
    {
        public Guid UserId { get; set; }
        public Guid AppAccountToken { get; set; }
        public bool IsActive { get; set; } = true;
    }
}

