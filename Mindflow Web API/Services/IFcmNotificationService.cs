using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mindflow_Web_API.Services
{
    public interface IFcmNotificationService
    {
        Task<string> SendToDeviceAsync(string deviceToken, string title, string body, IDictionary<string, string>? data = null);

        Task<int> SendToUserAsync(Guid userId, string title, string body, IDictionary<string, string>? data = null);
    }
}


