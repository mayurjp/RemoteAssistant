using System;
using System.ComponentModel.DataAnnotations;

namespace RemoteAssistant.Core.Database;

public class UserNotification
{
    [Key]
    public int Id { get; set; }

    public int BotId { get; set; }

    public long TelegramId { get; set; }

    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;

    public bool Sent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SentAt { get; set; }

    public TelegramBot Bot { get; set; } = null!;
}
