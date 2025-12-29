using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Persistence;

namespace Mindflow_Web_API.Services
{
    public interface IGoogleCalendarService
    {
        Task<string> BuildConnectUrlAsync(Guid userId);
        Task<(bool success, string message)> HandleCallbackAsync(string code, string state, HttpContext httpContext);
        Task<(bool isConnected, string? email, DateTime? lastSyncAt)> GetStatusAsync(Guid userId);
        Task DisconnectAsync(Guid userId);
        Task<(bool success, int syncedEvents, DateTime? lastSyncAt)> SyncAsync(Guid userId);
        Task<List<GoogleCalendarEventDto>> GetEventsAsync(Guid userId, DateTime? startDate = null, DateTime? endDate = null);
    }

    public class GoogleCalendarEventDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string? Location { get; set; }
        public string Source { get; set; } = "Google"; // Always "Google" for Google Calendar events
    }

    public class GoogleCalendarService : IGoogleCalendarService
    {
        private readonly MindflowDbContext _db;
        private readonly ISimpleEncryptionService _encryption;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GoogleCalendarService> _logger;

        public GoogleCalendarService(
            MindflowDbContext db,
            ISimpleEncryptionService encryption,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<GoogleCalendarService> logger)
        {
            _db = db;
            _encryption = encryption;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> BuildConnectUrlAsync(Guid userId)
        {
            var clientId = _configuration["Google:ClientId"]
                           ?? throw new InvalidOperationException("Google:ClientId not configured");
            var redirectUri = _configuration["Google:RedirectUri"]
                              ?? throw new InvalidOperationException("Google:RedirectUri not configured");

            var state = _encryption.Encrypt(userId.ToString());
            // Request calendar.events, tasks, and userinfo.email scopes
            // userinfo.email is needed to get the user's email address
            var scope = Uri.EscapeDataString("https://www.googleapis.com/auth/calendar.events https://www.googleapis.com/auth/tasks https://www.googleapis.com/auth/userinfo.email");

            // Check if user already has a connection - if so, use prompt=select_account instead of prompt=consent
            // This allows re-authentication without forcing consent screen every time
            var existingConnection = await _db.GoogleCalendarConnections.FirstOrDefaultAsync(x => x.UserId == userId && x.IsConnected);
            var prompt = existingConnection != null ? "select_account" : "consent";

            var url =
                $"https://accounts.google.com/o/oauth2/v2/auth?client_id={Uri.EscapeDataString(clientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&response_type=code" +
                $"&scope={scope}" +
                $"&state={Uri.EscapeDataString(state)}" +
                $"&access_type=offline" +
                $"&prompt={prompt}";

            _logger.LogDebug("Generated Google OAuth URL for user {UserId} with prompt={Prompt}", userId, prompt);
            return url;
        }

        public async Task<(bool success, string message)> HandleCallbackAsync(string code, string state, HttpContext httpContext)
        {
            // 1. Verify state (decrypt to userId)
            Guid userId;
            try
            {
                var decrypted = _encryption.Decrypt(state);
                if (!Guid.TryParse(decrypted, out userId))
                {
                    return (false, "Invalid state");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to decrypt Google Calendar state");
                return (false, "Invalid state");
            }

            var clientId = _configuration["Google:ClientId"]
                           ?? throw new InvalidOperationException("Google:ClientId not configured");
            var clientSecret = _configuration["Google:ClientSecret"]
                               ?? throw new InvalidOperationException("Google:ClientSecret not configured");
            var redirectUri = _configuration["Google:RedirectUri"]
                              ?? throw new InvalidOperationException("Google:RedirectUri not configured");

            var httpClient = _httpClientFactory.CreateClient("google-oauth");

            // 2. Exchange code for tokens
            var tokenRequest = new
            {
                code,
                client_id = clientId,
                client_secret = clientSecret,
                redirect_uri = redirectUri,
                grant_type = "authorization_code"
            };

            var tokenResponse = await httpClient.PostAsJsonAsync("https://oauth2.googleapis.com/token", tokenRequest);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                var error = await tokenResponse.Content.ReadAsStringAsync();
                _logger.LogError("Google token exchange failed for user {UserId}: StatusCode={StatusCode}, Error={Error}", userId, tokenResponse.StatusCode, error);
                
                // Try to parse error details for better user feedback
                try
                {
                    using var errorDoc = JsonDocument.Parse(error);
                    if (errorDoc.RootElement.TryGetProperty("error", out var errorElement))
                    {
                        var errorCode = errorElement.GetString();
                        var errorDescription = errorDoc.RootElement.TryGetProperty("error_description", out var descElement) 
                            ? descElement.GetString() 
                            : null;
                        
                        if (errorCode == "invalid_grant")
                        {
                            return (false, "The authorization code has expired or been used. Please try connecting again.");
                        }
                        else if (errorCode == "invalid_client")
                        {
                            return (false, "Google Calendar connection is not properly configured. Please contact support.");
                        }
                        else if (!string.IsNullOrWhiteSpace(errorDescription))
                        {
                            return (false, $"Google connection error: {errorDescription}");
                        }
                    }
                }
                catch
                {
                    // If parsing fails, use generic message
                }
                
                return (false, $"Failed to connect to Google Calendar. Please try again. (Error: {tokenResponse.StatusCode})");
            }

            var tokenResponseContent = await tokenResponse.Content.ReadAsStringAsync();
            _logger.LogDebug("Google token exchange response: {Response}", tokenResponseContent);
            
            using var tokenDoc = JsonDocument.Parse(tokenResponseContent);
            var root = tokenDoc.RootElement;
            
            // Validate access_token exists
            if (!root.TryGetProperty("access_token", out var accessTokenElement))
            {
                _logger.LogError("Google token response missing access_token. Response: {Response}", tokenResponseContent);
                return (false, "Invalid token response from Google");
            }
            
            var accessToken = accessTokenElement.GetString();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogError("Google access_token is empty. Response: {Response}", tokenResponseContent);
                return (false, "Invalid access token from Google");
            }
            
            var refreshToken = root.TryGetProperty("refresh_token", out var rtElement) ? rtElement.GetString() ?? string.Empty : string.Empty;
            var expiresIn = root.TryGetProperty("expires_in", out var expiresElement) ? expiresElement.GetInt32() : 3600;

            _logger.LogDebug("Successfully obtained access token (length: {Length}), expires in {ExpiresIn} seconds, has refresh token: {HasRefreshToken}", 
                accessToken.Length, expiresIn, !string.IsNullOrEmpty(refreshToken));
            
            // Warn if refresh token is missing (can happen if user already granted consent)
            if (string.IsNullOrEmpty(refreshToken))
            {
                _logger.LogWarning("Google did not return a refresh token for user {UserId}. This may happen if consent was already granted. Connection will work but may require re-authentication when token expires.", userId);
            }

            // 3. Get user's Google email
            // Create a new HttpClient for userinfo request to avoid header conflicts
            var userInfoClient = _httpClientFactory.CreateClient("google-oauth");
            var userInfoRequest = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
            userInfoRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            
            _logger.LogDebug("Requesting user info from Google with access token (length: {TokenLength})", accessToken.Length);
            
            var userInfoResponse = await userInfoClient.SendAsync(userInfoRequest);
            if (!userInfoResponse.IsSuccessStatusCode)
            {
                var error = await userInfoResponse.Content.ReadAsStringAsync();
                _logger.LogError("Google userinfo failed: StatusCode={StatusCode}, Error={Error}, AccessTokenLength={TokenLength}", 
                    userInfoResponse.StatusCode, error, accessToken?.Length ?? 0);
                return (false, $"Failed to fetch Google user info: {error}");
            }

            var userInfoContent = await userInfoResponse.Content.ReadAsStringAsync();
            using var userDoc = JsonDocument.Parse(userInfoContent);
            
            if (!userDoc.RootElement.TryGetProperty("email", out var emailElement))
            {
                _logger.LogError("Google userinfo response missing email field. Response: {Response}", userInfoContent);
                return (false, "Failed to retrieve Google account email. Please try again.");
            }
            
            var email = emailElement.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(email))
            {
                _logger.LogError("Google userinfo email is empty. Response: {Response}", userInfoContent);
                return (false, "Failed to retrieve Google account email. Please try again.");
            }
            
            _logger.LogInformation("Successfully retrieved Google account email for user {UserId}: {Email}", userId, email);

            // 4. Store tokens in database (encrypted)
            try
            {
                var encryptedAccess = _encryption.Encrypt(accessToken);
                var encryptedRefresh = string.IsNullOrEmpty(refreshToken) ? string.Empty : _encryption.Encrypt(refreshToken);

                var existing = await _db.GoogleCalendarConnections.FirstOrDefaultAsync(x => x.UserId == userId);
                var nowUtc = DateTime.UtcNow;
                if (existing == null)
                {
                    existing = new GoogleCalendarConnection
                    {
                        UserId = userId,
                        Email = email,
                        EncryptedAccessToken = encryptedAccess,
                        EncryptedRefreshToken = encryptedRefresh,
                        ExpiresAtUtc = nowUtc.AddSeconds(expiresIn),
                        ConnectedAtUtc = nowUtc,
                        LastSyncAtUtc = null,
                        IsConnected = true
                    };
                    _db.GoogleCalendarConnections.Add(existing);
                    _logger.LogInformation("Created new Google Calendar connection for user {UserId} with email {Email}", userId, email);
                }
                else
                {
                    existing.Email = email;
                    existing.EncryptedAccessToken = encryptedAccess;
                    // Only update refresh token if we got a new one (preserve existing if Google didn't return one)
                    if (!string.IsNullOrEmpty(refreshToken))
                    {
                        existing.EncryptedRefreshToken = encryptedRefresh;
                    }
                    existing.ExpiresAtUtc = nowUtc.AddSeconds(expiresIn);
                    existing.ConnectedAtUtc = nowUtc;
                    existing.IsConnected = true;
                    _logger.LogInformation("Updated Google Calendar connection for user {UserId} with email {Email}", userId, email);
                }

                await _db.SaveChangesAsync();
                _logger.LogInformation("Successfully saved Google Calendar connection for user {UserId}", userId);

                return (true, "Connected successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save Google Calendar connection to database for user {UserId}", userId);
                return (false, "Failed to save connection. Please try again.");
            }
        }

        public async Task<(bool isConnected, string? email, DateTime? lastSyncAt)> GetStatusAsync(Guid userId)
        {
            var conn = await _db.GoogleCalendarConnections.FirstOrDefaultAsync(x => x.UserId == userId && x.IsConnected);
            if (conn == null)
                return (false, null, null);

            return (true, conn.Email, conn.LastSyncAtUtc);
        }

        public async Task DisconnectAsync(Guid userId)
        {
            var conn = await _db.GoogleCalendarConnections.FirstOrDefaultAsync(x => x.UserId == userId);
            if (conn == null)
                return;

            conn.IsConnected = false;
            conn.EncryptedAccessToken = string.Empty;
            conn.EncryptedRefreshToken = string.Empty;
            await _db.SaveChangesAsync();
        }

        public async Task<(bool success, int syncedEvents, DateTime? lastSyncAt)> SyncAsync(Guid userId)
        {
            // 1. Get a valid (refreshed if needed) access token
            var accessToken = await GetValidAccessTokenAsync(userId);
            if (string.IsNullOrEmpty(accessToken))
                return (false, 0, null);

            // 2. TODO: Use accessToken to call Google Calendar API and sync events.
            // For now, we just record the sync time.
            var conn = await _db.GoogleCalendarConnections.FirstOrDefaultAsync(x => x.UserId == userId && x.IsConnected);
            if (conn == null)
                return (false, 0, null);

            conn.LastSyncAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return (true, 0, conn.LastSyncAtUtc);
        }

        /// <summary>
        /// Returns a valid Google access token for the user.
        /// If expired and a refresh token is available, it will refresh and update the database.
        /// </summary>
        private async Task<string?> GetValidAccessTokenAsync(Guid userId)
        {
            var conn = await _db.GoogleCalendarConnections.FirstOrDefaultAsync(x => x.UserId == userId && x.IsConnected);
            if (conn == null)
                return null;

            var nowUtc = DateTime.UtcNow;
            // If not expired, return decrypted access token
            if (conn.ExpiresAtUtc > nowUtc.AddMinutes(1))
            {
                return _encryption.Decrypt(conn.EncryptedAccessToken);
            }

            // No refresh token available
            if (string.IsNullOrWhiteSpace(conn.EncryptedRefreshToken))
            {
                _logger.LogWarning("Google access token expired and no refresh token available for user {UserId}", userId);
                return null;
            }

            var clientId = _configuration["Google:ClientId"]
                           ?? throw new InvalidOperationException("Google:ClientId not configured");
            var clientSecret = _configuration["Google:ClientSecret"]
                               ?? throw new InvalidOperationException("Google:ClientSecret not configured");

            var refreshToken = _encryption.Decrypt(conn.EncryptedRefreshToken);
            var httpClient = _httpClientFactory.CreateClient("google-oauth");

            var refreshRequest = new
            {
                client_id = clientId,
                client_secret = clientSecret,
                refresh_token = refreshToken,
                grant_type = "refresh_token"
            };

            var response = await httpClient.PostAsJsonAsync("https://oauth2.googleapis.com/token", refreshRequest);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Google token refresh failed for user {UserId}: {Error}", userId, error);
                return null;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            var newAccessToken = root.GetProperty("access_token").GetString() ?? string.Empty;
            var expiresIn = root.GetProperty("expires_in").GetInt32();

            conn.EncryptedAccessToken = _encryption.Encrypt(newAccessToken);
            conn.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn);
            await _db.SaveChangesAsync();

            return newAccessToken;
        }

        public async Task<List<GoogleCalendarEventDto>> GetEventsAsync(Guid userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var accessToken = await GetValidAccessTokenAsync(userId);
            if (string.IsNullOrEmpty(accessToken))
            {
                return new List<GoogleCalendarEventDto>();
            }

            // Default to today + 30 days if no date range specified
            var timeMin = startDate ?? DateTime.UtcNow.Date;
            var timeMax = endDate ?? timeMin.AddDays(30);

            var httpClient = _httpClientFactory.CreateClient("google-oauth");
            var allItems = new List<GoogleCalendarEventDto>();

            // Fetch Google Calendar Events
            try
            {
                var eventsUrl = $"https://www.googleapis.com/calendar/v3/calendars/primary/events" +
                              $"?timeMin={timeMin:yyyy-MM-ddTHH:mm:ssZ}" +
                              $"&timeMax={timeMax:yyyy-MM-ddTHH:mm:ssZ}" +
                              $"&singleEvents=true" +
                              $"&orderBy=startTime" +
                              $"&maxResults=250";

                var eventsRequest = new HttpRequestMessage(HttpMethod.Get, eventsUrl);
                eventsRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var eventsResponse = await httpClient.SendAsync(eventsRequest);
                if (eventsResponse.IsSuccessStatusCode)
                {
                    using var eventsDoc = JsonDocument.Parse(await eventsResponse.Content.ReadAsStringAsync());
                    var eventsRoot = eventsDoc.RootElement;

                    if (eventsRoot.TryGetProperty("items", out var itemsElement))
                    {
                        foreach (var item in itemsElement.EnumerateArray())
                        {
                            // Skip cancelled events
                            if (item.TryGetProperty("status", out var status) && status.GetString() == "cancelled")
                                continue;

                            var eventDto = new GoogleCalendarEventDto
                            {
                                Id = item.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
                                Title = item.TryGetProperty("summary", out var summary) ? summary.GetString() ?? "No Title" : "No Title",
                                Description = item.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                                Location = item.TryGetProperty("location", out var loc) ? loc.GetString() : null,
                                Source = "Google Calendar"
                            };

                            // Parse start/end times
                            if (item.TryGetProperty("start", out var start))
                            {
                                if (start.TryGetProperty("dateTime", out var dt))
                                {
                                    eventDto.Start = DateTime.Parse(dt.GetString() ?? DateTime.UtcNow.ToString("O")).ToUniversalTime();
                                }
                                else if (start.TryGetProperty("date", out var date))
                                {
                                    eventDto.Start = DateTime.Parse(date.GetString() ?? DateTime.UtcNow.ToString("yyyy-MM-dd")).ToUniversalTime();
                                }
                            }

                            if (item.TryGetProperty("end", out var end))
                            {
                                if (end.TryGetProperty("dateTime", out var dt))
                                {
                                    eventDto.End = DateTime.Parse(dt.GetString() ?? DateTime.UtcNow.ToString("O")).ToUniversalTime();
                                }
                                else if (end.TryGetProperty("date", out var date))
                                {
                                    eventDto.End = DateTime.Parse(date.GetString() ?? DateTime.UtcNow.ToString("yyyy-MM-dd")).ToUniversalTime();
                                }
                            }

                            allItems.Add(eventDto);
                        }
                    }
                }
                else
                {
                    var error = await eventsResponse.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to fetch Google Calendar events for user {UserId}: {Error}", userId, error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Google Calendar events for user {UserId}", userId);
            }

            //// Fetch Google Tasks
            //try
            //{
            //    var tasksUrl = $"https://tasks.googleapis.com/tasks/v1/lists/@default/tasks" +
            //                  $"?showCompleted=false" +
            //                  $"&maxResults=100";

            //    var tasksRequest = new HttpRequestMessage(HttpMethod.Get, tasksUrl);
            //    tasksRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            //    var tasksResponse = await httpClient.SendAsync(tasksRequest);
            //    if (tasksResponse.IsSuccessStatusCode)
            //    {
            //        using var tasksDoc = JsonDocument.Parse(await tasksResponse.Content.ReadAsStringAsync());
            //        var tasksRoot = tasksDoc.RootElement;

            //        if (tasksRoot.TryGetProperty("items", out var tasksItemsElement))
            //        {
            //            foreach (var taskItem in tasksItemsElement.EnumerateArray())
            //            {
            //                // Skip completed tasks
            //                if (taskItem.TryGetProperty("status", out var taskStatus) && taskStatus.GetString() == "completed")
            //                    continue;

            //                // Parse due date if available
            //                DateTime? taskDueDate = null;
            //                if (taskItem.TryGetProperty("due", out var due))
            //                {
            //                    var dueStr = due.GetString();
            //                    if (!string.IsNullOrEmpty(dueStr))
            //                    {
            //                        if (DateTime.TryParse(dueStr, out var parsedDue))
            //                        {
            //                            taskDueDate = parsedDue.ToUniversalTime();
            //                        }
            //                    }
            //                }

            //                // Only include tasks that fall within the date range (or have no due date)
            //                if (taskDueDate.HasValue)
            //                {
            //                    if (taskDueDate.Value < timeMin || taskDueDate.Value > timeMax)
            //                        continue;
            //                }
            //                else
            //                {
            //                    // Tasks without due dates: include if they're not too old (within last 7 days or future)
            //                    var taskUpdated = DateTime.UtcNow;
            //                    if (taskItem.TryGetProperty("updated", out var updated))
            //                    {
            //                        var updatedStr = updated.GetString();
            //                        if (!string.IsNullOrEmpty(updatedStr) && DateTime.TryParse(updatedStr, out var parsedUpdated))
            //                        {
            //                            taskUpdated = parsedUpdated.ToUniversalTime();
            //                        }
            //                    }
                                
            //                    // Only include recent tasks (updated within last 7 days) or tasks without dates
            //                    if (taskUpdated < timeMin.AddDays(-7))
            //                        continue;
            //                }

            //                var taskTitle = taskItem.TryGetProperty("title", out var title) ? title.GetString() ?? "No Title" : "No Title";
            //                var taskNotes = taskItem.TryGetProperty("notes", out var notes) ? notes.GetString() : null;

            //                // For tasks without due dates, use current date/time or updated date
            //                var taskStart = taskDueDate ?? DateTime.UtcNow;
            //                var taskEnd = taskStart.AddMinutes(30); // Default 30-minute duration for tasks

            //                var taskDto = new GoogleCalendarEventDto
            //                {
            //                    Id = taskItem.TryGetProperty("id", out var taskId) ? taskId.GetString() ?? string.Empty : string.Empty,
            //                    Title = taskTitle,
            //                    Description = taskNotes,
            //                    Location = null,
            //                    Start = taskStart,
            //                    End = taskEnd,
            //                    Source = "Google Tasks"
            //                };

            //                allItems.Add(taskDto);
            //            }
            //        }
            //    }
            //    else
            //    {
            //        var error = await tasksResponse.Content.ReadAsStringAsync();
            //        _logger.LogWarning("Failed to fetch Google Tasks for user {UserId}: {Error}", userId, error);
            //    }
            //}
            //catch (Exception ex)
            //{
            //    _logger.LogError(ex, "Error fetching Google Tasks for user {UserId}", userId);
            //}

            // Sort all items by start time
            return allItems.OrderBy(x => x.Start).ToList();
        }
    }
}


