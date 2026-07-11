using System;
using System.ComponentModel.DataAnnotations;

namespace RemoteAssistant.Core.Database;

public class BotRegistration
{
    [Key]
    public int Id { get; set; }

    public long TelegramId { get; set; }

    public int BotId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public DateTime? UnregisteredAt { get; set; }

    public TelegramBot Bot { get; set; } = null!;
}
