using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using RemoteAssistant.Core.Database;

namespace RemoteAssistant.Worker.Services;

public class TelegramBotService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TelegramBotService> _logger;

    private string? _activeToken;
    private TelegramBotClient? _botClient;
    private CancellationTokenSource? _botCts;

    public TelegramBotService(IServiceProvider serviceProvider, ILogger<TelegramBotService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Telegram Bot Service starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                string? dbToken = null;

                // Resolve DB context in a transient/scoped way
                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
                    var setting = await context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "TelegramBotToken", stoppingToken);
                    dbToken = setting?.Value;
                }

                if (string.IsNullOrEmpty(dbToken))
                {
                    _logger.LogWarning("Telegram Bot Token is not configured in database yet. Please configure it via Admin UI.");
                    StopBot();
                }
                else if (dbToken != _activeToken)
                {
                    _logger.LogInformation("New/Updated Telegram Bot Token detected. Initializing bot...");
                    StopBot();

                    _activeToken = dbToken;
                    _botCts = new CancellationTokenSource();
                    _botClient = new TelegramBotClient(_activeToken);

                    var receiverOptions = new ReceiverOptions
                    {
                        AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
                    };

                    _botClient.StartReceiving(
                        updateHandler: HandleUpdateAsync,
                        pollingErrorHandler: HandleErrorAsync,
                        receiverOptions: receiverOptions,
                        cancellationToken: _botCts.Token
                    );

                    var me = await _botClient.GetMeAsync(cancellationToken: _botCts.Token);
                    _logger.LogInformation("Telegram Bot @{BotUsername} is up and running.", me.Username);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while checking Telegram Bot configuration or starting the bot.");
            }

            // Check config again every 15 seconds
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }

        StopBot();
        _logger.LogInformation("Telegram Bot Service stopped.");
    }

    private void StopBot()
    {
        if (_botCts != null)
        {
            _botCts.Cancel();
            _botCts.Dispose();
            _botCts = null;
        }
        _botClient = null;
        _activeToken = null;
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { } message) return;
        if (message.Text is not { } messageText) return;

        var parts = messageText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        var command = parts[0].ToLower();

        _logger.LogInformation("Received message '{Text}' from ChatId {ChatId}", messageText, message.Chat.Id);

        try
        {
            switch (command)
            {
                case "/start":
                case "/help":
                    await botClient.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: "Welcome to RemoteAssistant Bot!\n\nTo onboard and register your account, use:\n`/register your-email@example.com`\n\nAfter receiving the OTP email, verify your code using:\n`/verify 123456`",
                        parseMode: ParseMode.Markdown,
                        cancellationToken: cancellationToken
                    );
                    break;

                case "/register":
                    await HandleRegisterCommandAsync(botClient, message, parts, cancellationToken);
                    break;

                case "/verify":
                    await HandleVerifyCommandAsync(botClient, message, parts, cancellationToken);
                    break;

                default:
                    await botClient.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: "Unknown command. Use /register <email> or /verify <otp> or /help.",
                        cancellationToken: cancellationToken
                    );
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message from ChatId {ChatId}", message.Chat.Id);
        }
    }

    private async Task HandleRegisterCommandAsync(ITelegramBotClient botClient, Message message, string[] parts, CancellationToken cancellationToken)
    {
        if (parts.Length < 2)
        {
            await botClient.SendTextMessageAsync(message.Chat.Id, "Usage: `/register your-email@example.com`", parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
            return;
        }

        var email = parts[1].Trim();
        if (!email.Contains("@") || !email.Contains("."))
        {
            await botClient.SendTextMessageAsync(message.Chat.Id, "Please enter a valid email address.", cancellationToken: cancellationToken);
            return;
        }

        // Generate 6-digit OTP
        var otp = Random.Shared.Next(100000, 999999).ToString();

        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
            var user = await context.Users.FirstOrDefaultAsync(u => u.TelegramId == message.Chat.Id, cancellationToken);

            if (user == null)
            {
                user = new RemoteAssistant.Core.Database.User
                {
                    TelegramId = message.Chat.Id,
                    Email = email,
                    IsVerified = false,
                    OtpCode = otp,
                    OtpExpiry = DateTime.UtcNow.AddMinutes(5),
                    CreatedAt = DateTime.UtcNow
                };
                context.Users.Add(user);
            }
            else
            {
                user.Email = email;
                user.IsVerified = false; // reset status for re-verification
                user.OtpCode = otp;
                user.OtpExpiry = DateTime.UtcNow.AddMinutes(5);
                context.Users.Update(user);
            }

            await context.SaveChangesAsync(cancellationToken);

            var mailSender = scope.ServiceProvider.GetRequiredService<GmailSenderService>();
            var emailSent = await mailSender.SendOtpEmailAsync(email, otp);

            if (emailSent)
            {
                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"An OTP code has been sent to `{email}`. Please check your inbox and verify it using:\n`/verify YOUR_OTP`",
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken
                );
            }
            else
            {
                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "⚠️ Failed to send OTP email. Please ensure that the Administrator has completed the Gmail authentication setup in the Admin Portal.",
                    cancellationToken: cancellationToken
                );
            }
        }
    }

    private async Task HandleVerifyCommandAsync(ITelegramBotClient botClient, Message message, string[] parts, CancellationToken cancellationToken)
    {
        if (parts.Length < 2)
        {
            await botClient.SendTextMessageAsync(message.Chat.Id, "Usage: `/verify YOUR_OTP`", parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
            return;
        }

        var otp = parts[1].Trim();

        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
            var user = await context.Users.FirstOrDefaultAsync(u => u.TelegramId == message.Chat.Id, cancellationToken);

            if (user == null)
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, "You have not registered yet. Please use `/register <email>` first.", parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
                return;
            }

            if (user.IsVerified)
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, "You are already verified!", cancellationToken: cancellationToken);
                return;
            }

            if (user.OtpCode != otp)
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, "Invalid OTP code. Please try again.", cancellationToken: cancellationToken);
                return;
            }

            if (user.OtpExpiry < DateTime.UtcNow)
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, "Your OTP code has expired. Please run `/register <email>` again to get a new code.", parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
                return;
            }

            // Mark user as verified
            user.IsVerified = true;
            user.OtpCode = null;
            user.OtpExpiry = null;
            user.VerifiedAt = DateTime.UtcNow;
            context.Users.Update(user);

            await context.SaveChangesAsync(cancellationToken);

            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "✅ Congratulations! Your registration has been verified successfully.",
                cancellationToken: cancellationToken
            );
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Telegram Bot error occurred");
        return Task.CompletedTask;
    }
}
