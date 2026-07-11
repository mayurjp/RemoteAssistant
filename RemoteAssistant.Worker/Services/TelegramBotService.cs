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

                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
                    var activeBot = await context.TelegramBots
                        .FirstOrDefaultAsync(b => b.IsActive, stoppingToken);
                    dbToken = activeBot?.Token;
                }

                if (string.IsNullOrEmpty(dbToken))
                {
                    _logger.LogWarning("No active Telegram Bot found. Please configure a bot via the Admin UI setup page.");
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
                        AllowedUpdates = Array.Empty<UpdateType>()
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
                        text: "Welcome to RemoteAssistant Bot!\n\nTo register, use:\n`/register your-email@example.com`",
                        parseMode: ParseMode.Markdown,
                        cancellationToken: cancellationToken
                    );
                    break;

                case "/register":
                    await HandleRegisterCommandAsync(botClient, message, parts, cancellationToken);
                    break;

                default:
                    await botClient.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: "Unknown command. Use `/register <email>` or `/help`.",
                        parseMode: ParseMode.Markdown,
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
            await botClient.SendTextMessageAsync(
                message.Chat.Id,
                "Usage: `/register your-email@example.com`",
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken
            );
            return;
        }

        var email = parts[1].Trim();
        if (!email.Contains("@") || !email.Contains("."))
        {
            await botClient.SendTextMessageAsync(
                message.Chat.Id,
                "Please enter a valid email address.",
                cancellationToken: cancellationToken
            );
            return;
        }

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
                    IsVerified = true,
                    CreatedAt = DateTime.UtcNow,
                    VerifiedAt = DateTime.UtcNow
                };
                context.Users.Add(user);
            }
            else
            {
                user.Email = email;
                user.IsVerified = true;
                user.VerifiedAt = DateTime.UtcNow;
                context.Users.Update(user);
            }

            await context.SaveChangesAsync(cancellationToken);

            var existingUser = user.IsVerified && user.VerifiedAt != user.CreatedAt
                ? "updated"
                : "registered";

            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: $"✅ Your account has been {existingUser} successfully with email `{email}`.",
                parseMode: ParseMode.Markdown,
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
