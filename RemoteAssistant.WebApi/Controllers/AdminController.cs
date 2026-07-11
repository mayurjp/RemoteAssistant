using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RemoteAssistant.Core.Database;
using System.Text.Json;

namespace RemoteAssistant.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly SchedulerDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AdminController> _logger;

    public AdminController(SchedulerDbContext context, IHttpClientFactory httpClientFactory, ILogger<AdminController> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet("config")]
    public async Task<IActionResult> GetConfigStatus()
    {
        var settings = await _context.SystemSettings.ToListAsync();

        var clientId = settings.FirstOrDefault(s => s.Key == "GoogleClientId")?.Value;
        var clientSecret = settings.FirstOrDefault(s => s.Key == "GoogleClientSecret")?.Value;
        var refreshToken = settings.FirstOrDefault(s => s.Key == "GoogleRefreshToken")?.Value;
        var botToken = settings.FirstOrDefault(s => s.Key == "TelegramBotToken")?.Value;
        var adminEmail = settings.FirstOrDefault(s => s.Key == "GoogleAdminEmail")?.Value;

        return Ok(new
        {
            HasGoogleClientId = !string.IsNullOrEmpty(clientId),
            HasGoogleClientSecret = !string.IsNullOrEmpty(clientSecret),
            HasGoogleRefreshToken = !string.IsNullOrEmpty(refreshToken),
            HasTelegramBotToken = !string.IsNullOrEmpty(botToken),
            GoogleAdminEmail = adminEmail
        });
    }

    [HttpPost("config/telegram")]
    public async Task<IActionResult> SaveTelegramToken([FromBody] TelegramTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest("Telegram Token cannot be empty.");
        }

        await SaveSettingAsync("TelegramBotToken", request.Token.Trim());
        return Ok(new { Message = "Telegram Token saved successfully." });
    }

    [HttpPost("config/google")]
    public async Task<IActionResult> SaveGoogleCredentials([FromBody] GoogleCredentialsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.ClientSecret))
        {
            return BadRequest("Client ID and Client Secret cannot be empty.");
        }

        await SaveSettingAsync("GoogleClientId", request.ClientId.Trim());
        await SaveSettingAsync("GoogleClientSecret", request.ClientSecret.Trim());
        return Ok(new { Message = "Google OAuth credentials saved successfully." });
    }

    [HttpPost("oauth/callback")]
    public async Task<IActionResult> ProcessOAuthCallback([FromBody] OAuthCallbackRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest("Authorization code is required.");
        }

        var clientIdSetting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "GoogleClientId");
        var clientSecretSetting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "GoogleClientSecret");

        if (clientIdSetting == null || string.IsNullOrEmpty(clientIdSetting.Value) ||
            clientSecretSetting == null || string.IsNullOrEmpty(clientSecretSetting.Value))
        {
            return BadRequest("Google Client ID and Client Secret are not configured yet.");
        }

        var client = _httpClientFactory.CreateClient();
        var requestData = new Dictionary<string, string>
        {
            { "code", request.Code },
            { "client_id", clientIdSetting.Value },
            { "client_secret", clientSecretSetting.Value },
            { "redirect_uri", "http://localhost:4200/oauth-callback" },
            { "grant_type", "authorization_code" }
        };

        try
        {
            var response = await client.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(requestData));
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Google Token Exchange failed: {Response}", responseString);
                return BadRequest($"Failed to exchange token with Google: {responseString}");
            }

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;

            if (root.TryGetProperty("refresh_token", out var refreshProp))
            {
                var refreshToken = refreshProp.GetString();
                if (!string.IsNullOrEmpty(refreshToken))
                {
                    await SaveSettingAsync("GoogleRefreshToken", refreshToken);
                }
            }
            else
            {
                // In some cases Google won't return refresh token if prompt=consent is not set on request.
                // We should check if we already have a refresh token saved.
                var existingRefreshToken = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "GoogleRefreshToken");
                if (existingRefreshToken == null || string.IsNullOrEmpty(existingRefreshToken.Value))
                {
                    return BadRequest("OAuth consented, but Google did not return a refresh token. " +
                                      "Ensure offline access is requested and try revoking permissions/logging in again.");
                }
            }

            string? adminEmail = null;
            if (root.TryGetProperty("id_token", out var idTokenProp))
            {
                adminEmail = ParseEmailFromIdToken(idTokenProp.GetString() ?? "");
                if (!string.IsNullOrEmpty(adminEmail))
                {
                    await SaveSettingAsync("GoogleAdminEmail", adminEmail);
                }
            }

            return Ok(new
            {
                Message = "Authentication and Gmail authorization completed successfully.",
                Email = adminEmail
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Google OAuth callback");
            return StatusCode(500, $"Internal error during authentication: {ex.Message}");
        }
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _context.Users
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        return Ok(users);
    }

    private async Task SaveSettingAsync(string key, string value)
    {
        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting == null)
        {
            setting = new SystemSetting { Key = key, Value = value, UpdatedAt = DateTime.UtcNow };
            _context.SystemSettings.Add(setting);
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAt = DateTime.UtcNow;
            _context.SystemSettings.Update(setting);
        }
        await _context.SaveChangesAsync();
    }

    private string? ParseEmailFromIdToken(string idToken)
    {
        try
        {
            var parts = idToken.Split('.');
            if (parts.Length > 1)
            {
                var payload = parts[1];
                int mod = payload.Length % 4;
                if (mod > 0) payload += new string('=', 4 - mod);
                // Base64Url decode helper
                payload = payload.Replace('-', '+').Replace('_', '/');
                var jsonBytes = Convert.FromBase64String(payload);
                var json = System.Text.Encoding.UTF8.GetString(jsonBytes);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("email", out var emailProp))
                {
                    return emailProp.GetString();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse email from Google ID Token");
        }
        return null;
    }
}

public class TelegramTokenRequest
{
    public string Token { get; set; } = string.Empty;
}

public class GoogleCredentialsRequest
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}

public class OAuthCallbackRequest
{
    public string Code { get; set; } = string.Empty;
}
