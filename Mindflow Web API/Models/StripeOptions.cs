namespace Mindflow_Web_API.Models
{
    public class StripeOptions
    {
        public string SecretKey { get; set; } = string.Empty;
        public string PublishableKey { get; set; } = string.Empty;
        public bool TestMode { get; set; } = true;
    }
}
