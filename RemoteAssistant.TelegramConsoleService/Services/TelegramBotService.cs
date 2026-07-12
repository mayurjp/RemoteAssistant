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

namespace RemoteAssistant.TelegramConsoleService.Services;

public class TelegramBotService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TelegramBotService> _logger;

    private string? _activeToken;
    private int? _activeTelegramBotId;
    private string _activeTelegramBotName = "Telegram Bot";
    private TelegramBotClient? _telegramBotClient;
    private CancellationTokenSource? _telegramBotCts;

    public TelegramBotService(IServiceProvider serviceProvider, ILogger<TelegramBotService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Telegram Bot background service starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                string? dbToken = null;
                int? dbTelegramBotId = null;

                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
                    var activeTelegramBot = await context.TelegramBots
                        .OrderByDescending(b => b.CreatedAt)
                        .FirstOrDefaultAsync(stoppingToken);
                    dbToken = activeTelegramBot?.Token;
                    dbTelegramBotId = activeTelegramBot?.Id;
                    _activeTelegramBotName = activeTelegramBot?.Name ?? "Telegram Bot";
                }

                if (string.IsNullOrEmpty(dbToken) || dbTelegramBotId == null)
                {
                    _logger.LogWarning("No active Telegram Bot found. Configure via the web UI.");
                    StopTelegramBot();
                }
                else if (dbToken != _activeToken)
                {
                    _logger.LogInformation("Switching to Telegram Bot ID {TelegramBotId}...", dbTelegramBotId);
                    StopTelegramBot();

                    _activeToken = dbToken;
                    _activeTelegramBotId = dbTelegramBotId;
                    _telegramBotCts = new CancellationTokenSource();
                    _telegramBotClient = new TelegramBotClient(_activeToken);

                    _telegramBotClient.StartReceiving(
                        updateHandler: HandleUpdateAsync,
                        pollingErrorHandler: HandleErrorAsync,
                        receiverOptions: new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() },
                        cancellationToken: _telegramBotCts.Token
                    );

                    var me = await _telegramBotClient.GetMeAsync(cancellationToken: _telegramBotCts.Token);
                    _logger.LogInformation("Telegram Bot @{Username} (ID {TelegramBotId}) is online.", me.Username, _activeTelegramBotId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Telegram Bot service loop");
            }

            if (_telegramBotClient != null && _activeTelegramBotId != null)
            {
                await ProcessNotificationsAsync(stoppingToken);
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }

        StopTelegramBot();
    }

    private async Task ProcessNotificationsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();

            var notifications = await context.UserNotifications
                .Where(n => !n.Sent && n.TelegramBotId == _activeTelegramBotId)
                .OrderBy(n => n.CreatedAt)
                .Take(20)
                .ToListAsync(ct);

            foreach (var n in notifications)
            {
                try
                {
                    await _telegramBotClient!.SendTextMessageAsync(n.TelegramId, n.Message, cancellationToken: ct);
                    n.Sent = true;
                    n.SentAt = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send notification to {TelegramId}", n.TelegramId);
                }
            }

            if (notifications.Count > 0)
            {
                await context.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing notifications");
        }
    }

    private void StopTelegramBot()
    {
        _telegramBotCts?.Cancel();
        _telegramBotCts?.Dispose();
        _telegramBotCts = null;
        _telegramBotClient = null;
        _activeToken = null;
        _activeTelegramBotId = null;
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
    {
        if (update.Message is not { } message) return;
        if (message.Text is not { } messageText) return;
        if (_activeTelegramBotId == null) return;

        var parts = messageText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        var command = parts[0].ToLower();
        _logger.LogInformation("Msg '{Text}' from {ChatId} to Telegram Bot {TelegramBotId}", messageText, message.Chat.Id, _activeTelegramBotId);

        try
        {
            switch (command)
            {
                case "/start":
                case "/help":
                    await botClient.SendTextMessageAsync(message.Chat.Id,
                        "Welcome to *" + _activeTelegramBotName + "*.\n\n" +
                        "`/register` — Request access to this bot\n" +
                        "`/unregister` — Unregister from this bot\n" +
                        "`/status` — Check your registration status\n" +
                        "`/jobs` — List available job commands\n" +
                        "`/kitedata` — Fetch OHLC candle data",
                        parseMode: ParseMode.Markdown, cancellationToken: ct);
                    break;

                case "/register":
                    await HandleRegisterAsync(botClient, message, ct);
                    break;

                case "/unregister":
                    await HandleUnregisterAsync(botClient, message, ct);
                    break;

                case "/status":
                    await HandleStatusAsync(botClient, message, ct);
                    break;

                case "/jobs":
                    await HandleJobsListAsync(botClient, message, ct);
                    break;

                case "/kitedata":
                    await HandleRunJobAsync(botClient, message, "KiteDataLoad", ct);
                    break;

                default:
                    await botClient.SendTextMessageAsync(message.Chat.Id,
                        "Unknown command. Use `/help` to see available commands.",
                        parseMode: ParseMode.Markdown, cancellationToken: ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message from {ChatId}", message.Chat.Id);
        }
    }

    private async Task HandleRegisterAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        if (_activeTelegramBotId == null) return;

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        var telegramBotId = _activeTelegramBotId.Value;

        var alreadyRegistered = await context.UserMemberships
            .AnyAsync(r => r.TelegramId == message.Chat.Id && r.TelegramBotId == telegramBotId, ct);

        if (alreadyRegistered)
        {
            await botClient.SendTextMessageAsync(message.Chat.Id, "You are already registered to this bot.", cancellationToken: ct);
            return;
        }

        var pending = await context.RegistrationRequests
            .FirstOrDefaultAsync(r => r.TelegramId == message.Chat.Id && r.TelegramBotId == telegramBotId && r.Status == "Pending", ct);

        if (pending != null)
        {
            await botClient.SendTextMessageAsync(message.Chat.Id, "Your registration request is pending approval. You will be notified once approved.", cancellationToken: ct);
            return;
        }

        context.RegistrationRequests.Add(new RegistrationRequest
        {
            TelegramId = message.Chat.Id,
            TelegramBotId = telegramBotId,
            Status = "Pending",
            RequestedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(ct);

        await botClient.SendTextMessageAsync(message.Chat.Id, "Your registration request has been submitted for approval. You will be notified once reviewed.", cancellationToken: ct);
    }

    private async Task HandleUnregisterAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        if (_activeTelegramBotId == null) return;

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        var telegramBotId = _activeTelegramBotId.Value;

        var reg = await context.UserMemberships
            .FirstOrDefaultAsync(r => r.TelegramId == message.Chat.Id && r.TelegramBotId == telegramBotId, ct);

        if (reg == null)
        {
            await botClient.SendTextMessageAsync(message.Chat.Id, "You are not registered to this bot.", cancellationToken: ct);
            return;
        }

        context.UserMemberships.Remove(reg);
        await context.SaveChangesAsync(ct);

        await botClient.SendTextMessageAsync(message.Chat.Id, "You have been unregistered from this bot.", cancellationToken: ct);
    }

    private async Task HandleStatusAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        if (_activeTelegramBotId == null) return;

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        var telegramBotId = _activeTelegramBotId.Value;

        var reg = await context.UserMemberships
            .FirstOrDefaultAsync(r => r.TelegramId == message.Chat.Id && r.TelegramBotId == telegramBotId, ct);

        if (reg != null)
        {
            await botClient.SendTextMessageAsync(message.Chat.Id, "Status: Registered since " + reg.RegisteredAt.ToString("g") + ".", cancellationToken: ct);
            return;
        }

        var pending = await context.RegistrationRequests
            .OrderByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync(r => r.TelegramId == message.Chat.Id && r.TelegramBotId == telegramBotId, ct);

        if (pending != null)
        {
            var statusText = pending.Status switch
            {
                "Pending" => "Status: Pending approval (requested " + pending.RequestedAt.ToString("g") + ").",
                "Approved" => "Status: Approved. If you cannot use commands, send `/register` to reactivate.",
                "Rejected" => "Status: Registration was rejected on " + (pending.ReviewedAt?.ToString("g") ?? "unknown date") + ". Send `/register` to submit a new request.",
                _ => "Status: " + pending.Status + "."
            };
            await botClient.SendTextMessageAsync(message.Chat.Id, statusText, cancellationToken: ct);
            return;
        }

        await botClient.SendTextMessageAsync(message.Chat.Id, "Status: Not registered. Send `/register` to request access.", cancellationToken: ct);
    }

    private async Task HandleJobsListAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        if (_activeTelegramBotId == null) return;

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        var telegramBotId = _activeTelegramBotId.Value;

        var isRegistered = await context.UserMemberships
            .AnyAsync(r => r.TelegramId == message.Chat.Id && r.TelegramBotId == telegramBotId, ct);

        if (!isRegistered)
        {
            await botClient.SendTextMessageAsync(message.Chat.Id, "You must be registered to see jobs. Send `/register` first.", cancellationToken: ct);
            return;
        }

        var jobTypes = await context.JobBotMappings
            .Where(bj => bj.TelegramBotId == telegramBotId)
            .Select(bj => bj.JobTemplate.JobType)
            .ToListAsync(ct);

        if (jobTypes.Count == 0)
        {
            await botClient.SendTextMessageAsync(message.Chat.Id, "No jobs are assigned to this bot.", cancellationToken: ct);
            return;
        }

        var lines = jobTypes.Select(jt => $"• `/{jt.ToLower()}`");
        await botClient.SendTextMessageAsync(message.Chat.Id,
            "Available job commands:\n\n" + string.Join("\n", lines),
            parseMode: ParseMode.Markdown, cancellationToken: ct);
    }

    private async Task HandleRunJobAsync(ITelegramBotClient botClient, Message message, string jobType, CancellationToken ct)
    {
        if (_activeTelegramBotId == null) return;

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        var telegramBotId = _activeTelegramBotId.Value;

        var isRegistered = await context.UserMemberships
            .AnyAsync(r => r.TelegramId == message.Chat.Id && r.TelegramBotId == telegramBotId, ct);

        if (!isRegistered)
        {
            await botClient.SendTextMessageAsync(message.Chat.Id, "You must be registered to run jobs. Send `/register` first.", cancellationToken: ct);
            return;
        }

        var isAssigned = await context.JobBotMappings
            .AnyAsync(bj => bj.TelegramBotId == telegramBotId && bj.JobTemplate.JobType == jobType, ct);

        if (!isAssigned)
        {
            await botClient.SendTextMessageAsync(message.Chat.Id, $"Job `{jobType}` is not available for this bot. Use `/jobs` to see available jobs.", parseMode: ParseMode.Markdown, cancellationToken: ct);
            return;
        }

        context.JobRequests.Add(new JobRequest
        {
            TelegramBotId = telegramBotId,
            JobType = jobType,
            TelegramId = message.Chat.Id,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(ct);

        await botClient.SendTextMessageAsync(message.Chat.Id, $"Job queued: *{jobType}*. You will be notified when complete.", parseMode: ParseMode.Markdown, cancellationToken: ct);
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception, "Telegram Bot error");
        return Task.CompletedTask;
    }
}
