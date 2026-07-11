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
    private int? _activeBotId;
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
                int? dbBotId = null;

                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
                    var activeBot = await context.TelegramBots
                        .FirstOrDefaultAsync(b => b.IsActive, stoppingToken);
                    dbToken = activeBot?.Token;
                    dbBotId = activeBot?.Id;
                }

                if (string.IsNullOrEmpty(dbToken) || dbBotId == null)
                {
                    _logger.LogWarning("No active Telegram Bot found. Please configure a bot via the Admin UI setup page.");
                    StopBot();
                }
                else if (dbToken != _activeToken)
                {
                    _logger.LogInformation("Switching to Bot ID {BotId}...", dbBotId);
                    StopBot();

                    _activeToken = dbToken;
                    _activeBotId = dbBotId;
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
                    _logger.LogInformation("Telegram Bot @{BotUsername} (ID {BotId}) is up and running.", me.Username, _activeBotId);
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
        _activeBotId = null;
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { } message) return;
        if (message.Text is not { } messageText) return;
        if (_activeBotId == null) return;

        var parts = messageText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        var command = parts[0].ToLower();

        _logger.LogInformation("Received message '{Text}' from ChatId {ChatId} for BotId {BotId}", messageText, message.Chat.Id, _activeBotId);

        try
        {
            switch (command)
            {
                case "/start":
                case "/help":
                    await botClient.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: "Welcome! You are talking to bot ID " + _activeBotId + ".\n\n" +
                              "Commands:\n" +
                              "`/register` — Register to this bot\n" +
                              "`/unregister` — Unregister from this bot",
                        parseMode: ParseMode.Markdown,
                        cancellationToken: cancellationToken
                    );
                    break;

                case "/register":
                    await HandleRegisterCommandAsync(botClient, message, parts, cancellationToken);
                    break;

                case "/unregister":
                    await HandleUnregisterCommandAsync(botClient, message, cancellationToken);
                    break;

                default:
                    await botClient.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: "Unknown command. Use `/register`, `/unregister`, or `/help`.",
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
        if (_activeBotId == null) return;

        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
            var botId = _activeBotId.Value;

            var existing = await context.BotRegistrations
                .FirstOrDefaultAsync(r => r.TelegramId == message.Chat.Id && r.BotId == botId, cancellationToken);

            if (existing != null)
            {
                if (existing.IsActive)
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, "You are already registered to this bot.", cancellationToken: cancellationToken);
                    return;
                }

                existing.IsActive = true;
                existing.UnregisteredAt = null;
                context.BotRegistrations.Update(existing);
                await context.SaveChangesAsync(cancellationToken);

                await botClient.SendTextMessageAsync(message.Chat.Id, "✅ You have been re-registered to this bot.", cancellationToken: cancellationToken);
            }
            else
            {
                var reg = new BotRegistration
                {
                    TelegramId = message.Chat.Id,
                    BotId = botId,
                    IsActive = true,
                    RegisteredAt = DateTime.UtcNow
                };
                context.BotRegistrations.Add(reg);
                await context.SaveChangesAsync(cancellationToken);

                await botClient.SendTextMessageAsync(message.Chat.Id, "✅ You are now registered to this bot.", cancellationToken: cancellationToken);
            }
        }
    }

    private async Task HandleUnregisterCommandAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        if (_activeBotId == null) return;

        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
            var botId = _activeBotId.Value;

            var existing = await context.BotRegistrations
                .FirstOrDefaultAsync(r => r.TelegramId == message.Chat.Id && r.BotId == botId && r.IsActive, cancellationToken);

            if (existing == null)
            {
                await botClient.SendTextMessageAsync(
                    message.Chat.Id,
                    "You are not registered to this bot. Use `/register <email>` to register.",
                    cancellationToken: cancellationToken
                );
                return;
            }

            existing.IsActive = false;
            existing.UnregisteredAt = DateTime.UtcNow;
            context.BotRegistrations.Update(existing);
            await context.SaveChangesAsync(cancellationToken);

            await botClient.SendTextMessageAsync(
                message.Chat.Id,
                "You have been unregistered from this bot. Use `/register <email>` to re-register.",
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
