using System;

namespace Mindflow_Web_API.Models
{
    public class User : EntityBase
    {
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool EmailConfirmed { get; set; } = false;
        public string PasswordHash { get; set; } = string.Empty;
        public string SecurityStamp { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime? DateOfBirth { get; set; }
        public string? ProfilePic { get; set; }
        public string? Sub { get; set; } // Apple subject identifier

        public static User Create(string userName, string email, string firstName, string lastName, bool emailConfirmed, bool isActive, string passwordHash, string securityStamp, string? sub = null)
        {
            return new User
            {
                UserName = userName,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                EmailConfirmed = emailConfirmed,
                IsActive = isActive,
                PasswordHash = passwordHash,
                SecurityStamp = securityStamp,
                Sub = sub
            };
        }
    }
} 