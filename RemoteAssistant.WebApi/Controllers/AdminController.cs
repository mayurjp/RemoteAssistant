using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RemoteAssistant.Core.Database;

namespace RemoteAssistant.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly SchedulerDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        SchedulerDbContext context, 
        IHttpClientFactory httpClientFactory, 
        IConfiguration configuration,
        ILogger<AdminController> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet("config")]
    public async Task<IActionResult> GetConfigStatus()
    {
        var settings = await _context.SystemSettings.ToListAsync();

        var clientId = settings.FirstOrDefault(s => s.Key == "GoogleClientId")?.Value 
                       ?? _configuration["Google:ClientId"];
        var clientSecret = settings.FirstOrDefault(s => s.Key == "GoogleClientSecret")?.Value 
                            ?? _configuration["Google:ClientSecret"];
        var refreshToken = settings.FirstOrDefault(s => s.Key == "GoogleRefreshToken")?.Value;
        var botToken = settings.FirstOrDefault(s => s.Key == "TelegramBotToken")?.Value;
        var adminEmail = settings.FirstOrDefault(s => s.Key == "GoogleAdminEmail")?.Value;

        return Ok(new
        {
            HasGoogleClientId = !string.IsNullOrEmpty(clientId),
            HasGoogleClientSecret = !string.IsNullOrEmpty(clientSecret),
            HasGoogleRefreshToken = !string.IsNullOrEmpty(refreshToken),
            HasTelegramBotToken = !string.IsNullOrEmpty(botToken),
            GoogleAdminEmail = adminEmail,
            GoogleClientId = clientId
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

    [AllowAnonymous]
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

    [AllowAnonymous]
    [HttpGet("auth/google-login")]
    public async Task<IActionResult> GoogleLogin([FromQuery] string mode = "login")
    {
        var settings = await _context.SystemSettings.ToListAsync();
        var clientId = settings.FirstOrDefault(s => s.Key == "GoogleClientId")?.Value 
                       ?? _configuration["Google:ClientId"];

        if (string.IsNullOrEmpty(clientId))
        {
            return BadRequest("Google OAuth credentials are not configured.");
        }

        var callbackUrl = $"{Request.Scheme}://{Request.Host}/api/admin/auth/callback";
        var scopes = mode == "gmail" 
            ? "openid email https://www.googleapis.com/auth/gmail.send" 
            : "openid email profile";
        var accessType = mode == "gmail" ? "offline" : "offline";

        var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth" +
                      $"?client_id={Uri.EscapeDataString(clientId)}" +
                      $"&redirect_uri={Uri.EscapeDataString(callbackUrl)}" +
                      $"&response_type=code" +
                      $"&scope={Uri.EscapeDataString(scopes)}" +
                      $"&access_type={accessType}" +
                      $"&prompt=consent" +
                      $"&state={Uri.EscapeDataString(mode)}";

        return Redirect(authUrl);
    }

    [AllowAnonymous]
    [HttpGet("auth/callback")]
    public async Task<IActionResult> GoogleCallback([FromQuery] string code, [FromQuery] string state, [FromQuery] string? error)
    {
        var frontendBase = _configuration["Frontend:BaseUrl"] ?? "http://localhost:4200";

        if (!string.IsNullOrEmpty(error))
        {
            return Redirect($"{frontendBase}/login?error={Uri.EscapeDataString(error)}");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return Redirect($"{frontendBase}/login?error=no_code");
        }

        var settings = await _context.SystemSettings.ToListAsync();
        var clientId = settings.FirstOrDefault(s => s.Key == "GoogleClientId")?.Value 
                       ?? _configuration["Google:ClientId"];
        var clientSecret = settings.FirstOrDefault(s => s.Key == "GoogleClientSecret")?.Value 
                            ?? _configuration["Google:ClientSecret"];

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            return Redirect($"{frontendBase}/login?error=not_configured");
        }

        var callbackUrl = $"{Request.Scheme}://{Request.Host}/api/admin/auth/callback";
        var (success, tokenError, idToken, refreshToken, email) = await ExchangeGoogleCodeAsync(
            code, clientId, clientSecret, callbackUrl);

        if (!success)
        {
            _logger.LogError("Google callback failed: {Error}", tokenError);
            return Redirect($"{frontendBase}/login?error={Uri.EscapeDataString(tokenError ?? "exchange_failed")}");
        }

        if (state == "gmail")
        {
            if (!string.IsNullOrEmpty(refreshToken))
            {
                await SaveSettingAsync("GoogleRefreshToken", refreshToken);
            }
            else
            {
                var existing = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "GoogleRefreshToken");
                if (existing == null || string.IsNullOrEmpty(existing.Value))
                {
                    return Redirect($"{frontendBase}/setup?gmail=error&reason=no_refresh_token");
                }
            }

            if (!string.IsNullOrEmpty(email))
            {
                await SaveSettingAsync("GoogleAdminEmail", email);
            }

            return Redirect($"{frontendBase}/setup?gmail=success");
        }

        if (string.IsNullOrEmpty(email))
        {
            return Redirect($"{frontendBase}/login?error=no_email");
        }

        var allowedEmail = _configuration["Admin:AllowedEmail"];
        if (!string.IsNullOrEmpty(allowedEmail) && !email.Equals(allowedEmail, StringComparison.OrdinalIgnoreCase))
        {
            return Redirect($"{frontendBase}/login?error=access_denied");
        }

        var jwtKey = _configuration["Jwt:Key"] ?? "RemoteAssistant-SuperSecret-Key-2024-MinLength32Chars!";
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? "RemoteAssistant";
        var jwtAudience = _configuration["Jwt:Audience"] ?? "RemoteAssistant-AdminUI";

        var claims = new[]
        {
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, email),
            new Claim(JwtRegisteredClaimNames.Sub, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds
        );

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        Response.Cookies.Append("auth_token", jwt, new CookieOptions
        {
            HttpOnly = false,
            Secure = false,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddHours(8)
        });
        Response.Cookies.Append("auth_email", email, new CookieOptions
        {
            HttpOnly = false,
            Secure = false,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddHours(8)
        });

        return Redirect($"{frontendBase}/dashboard");
    }

    [HttpGet("auth/status")]
    public IActionResult AuthStatus()
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value
                    ?? User.FindFirst("sub")?.Value
                    ?? "unknown";

        return Ok(new
        {
            Authenticated = true,
            Email = email
        });
    }

    [AllowAnonymous]
    [HttpPost("auth/logout")]
    public IActionResult Logout()
    {
        return Ok(new { Message = "Logged out successfully." });
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _context.Users
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        return Ok(users);
    }

    private async Task<(bool Success, string? Error, string? IdToken, string? RefreshToken, string? Email)>
        ExchangeGoogleCodeAsync(string code, string clientId, string clientSecret, string redirectUri)
    {
        var client = _httpClientFactory.CreateClient();
        var requestData = new Dictionary<string, string>
        {
            { "code", code },
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "redirect_uri", redirectUri },
            { "grant_type", "authorization_code" }
        };

        try
        {
            var response = await client.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(requestData));
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Google Token Exchange failed: {Response}", responseString);
                return (false, $"Failed to exchange token with Google: {responseString}", null, null, null);
            }

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;

            string? idToken = null;
            if (root.TryGetProperty("id_token", out var idTokenProp))
            {
                idToken = idTokenProp.GetString();
            }

            string? refreshToken = null;
            if (root.TryGetProperty("refresh_token", out var refreshProp))
            {
                refreshToken = refreshProp.GetString();
            }

            string? email = !string.IsNullOrEmpty(idToken) ? ParseEmailFromIdToken(idToken) : null;

            return (true, null, idToken, refreshToken, email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Google OAuth token exchange");
            return (false, $"Internal error during token exchange: {ex.Message}", null, null, null);
        }
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
                payload = payload.Replace('-', '+').Replace('_', '/');
                var jsonBytes = Convert.FromBase64String(payload);
                var json = Encoding.UTF8.GetString(jsonBytes);
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


