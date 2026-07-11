using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RemoteAssistant.Core.Database;

namespace RemoteAssistant.Worker.Services;

public class GmailSenderService
{
    private readonly SchedulerDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GmailSenderService> _logger;

    public GmailSenderService(SchedulerDbContext context, IHttpClientFactory httpClientFactory, ILogger<GmailSenderService> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<bool> SendOtpEmailAsync(string recipientEmail, string otpCode)
    {
        _logger.LogInformation("Attempting to send OTP to {Email}", recipientEmail);

        // Fetch settings from DB
        var settings = await _context.SystemSettings.ToListAsync();
        var clientId = settings.Find(s => s.Key == "GoogleClientId")?.Value;
        var clientSecret = settings.Find(s => s.Key == "GoogleClientSecret")?.Value;
        var refreshToken = settings.Find(s => s.Key == "GoogleRefreshToken")?.Value;
        var adminEmail = settings.Find(s => s.Key == "GoogleAdminEmail")?.Value;

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) ||
            string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(adminEmail))
        {
            _logger.LogError("Gmail configuration is missing in the database. Ensure Gmail OAuth is completed in Admin UI.");
            return false;
        }

        // 1. Get access token from refresh token
        string? accessToken = await RefreshAccessTokenAsync(clientId, clientSecret, refreshToken);
        if (string.IsNullOrEmpty(accessToken))
        {
            _logger.LogError("Failed to refresh Gmail access token.");
            return false;
        }

        // 2. Send email via Gmail REST API
        return await SendRawEmailAsync(accessToken, adminEmail, recipientEmail, otpCode);
    }

    private async Task<string?> RefreshAccessTokenAsync(string clientId, string clientSecret, string refreshToken)
    {
        var client = _httpClientFactory.CreateClient();
        var requestData = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "refresh_token", refreshToken },
            { "grant_type", "refresh_token" }
        };

        try
        {
            var response = await client.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(requestData));
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Google access token refresh failed: {Response}", responseString);
                return null;
            }

            using var doc = JsonDocument.Parse(responseString);
            if (doc.RootElement.TryGetProperty("access_token", out var accessProp))
            {
                return accessProp.GetString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing Google access token");
        }

        return null;
    }

    private async Task<bool> SendRawEmailAsync(string accessToken, string fromEmail, string toEmail, string otpCode)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Construct RFC 822 / MIME message
        var messageBuilder = new StringBuilder();
        messageBuilder.AppendLine($"From: <{fromEmail}>");
        messageBuilder.AppendLine($"To: <{toEmail}>");
        messageBuilder.AppendLine("Subject: RemoteAssistant Registration OTP");
        messageBuilder.AppendLine("MIME-Version: 1.0");
        messageBuilder.AppendLine("Content-Type: text/html; charset=utf-8");
        messageBuilder.AppendLine();
        messageBuilder.AppendLine("<h3>RemoteAssistant Verification</h3>");
        messageBuilder.AppendLine($"<p>Your verification code is: <strong>{otpCode}</strong></p>");
        messageBuilder.AppendLine("<p>This code will expire in 5 minutes.</p>");
        messageBuilder.AppendLine("<hr/>");
        messageBuilder.AppendLine("<p>If you did not request this code, please ignore this email.</p>");

        // Convert message to Base64Url
        var rawMessageBytes = Encoding.UTF8.GetBytes(messageBuilder.ToString());
        var rawMessageBase64Url = Convert.ToBase64String(rawMessageBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var payload = new { raw = rawMessageBase64Url };
        var jsonPayload = JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        try
        {
            var response = await client.PostAsync("https://gmail.googleapis.com/v1/users/me/messages/send", content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("OTP email successfully sent to {Email} via Gmail API.", toEmail);
                return true;
            }
            else
            {
                _logger.LogError("Gmail Send API failed: {Response}", responseString);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email via Gmail REST API");
            return false;
        }
    }
}
