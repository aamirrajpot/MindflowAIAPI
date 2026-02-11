using System;
using System.Collections.Generic;

namespace Mindflow_Web_API.DTOs
{
    public class RegisterFcmDeviceDto
    {
        public string DeviceToken { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty; // e.g., "android", "ios"
    }

    public class SendTestNotificationDto
    {
        public Guid? UserId { get; set; }
        public string? DeviceToken { get; set; }
        public string Title { get; set; } = "Test notification";
        public string Body { get; set; } = "This is a test notification from Mindflow API.";
        public Dictionary<string, string>? Data { get; set; }
    }

    /// <summary>
    /// Request to delete FCM device token(s) for the current user.
    /// If DeviceToken is null or empty, all tokens for the user are deleted.
    /// </summary>
    public class DeleteFcmDeviceDto
    {
        public string? DeviceToken { get; set; }
    }
}


