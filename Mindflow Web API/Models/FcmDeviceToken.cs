using System;

namespace Mindflow_Web_API.Models
{
    /// <summary>
    /// Represents an FCM device token stored for a user/device combination.
    /// This is kept simple for now and can be expanded later (e.g., app version, device model).
    /// </summary>
    public class FcmDeviceToken : EntityBase
    {
        public Guid UserId { get; set; }

        /// <summary>
        /// FCM registration token for the device.
        /// </summary>
        public string DeviceToken { get; set; } = string.Empty;

        /// <summary>
        /// Platform indicator such as \"android\" or \"ios\".
        /// </summary>
        public string Platform { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}


