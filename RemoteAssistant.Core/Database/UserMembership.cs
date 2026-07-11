using System;
using System.ComponentModel.DataAnnotations;

namespace RemoteAssistant.Core.Database;

public class UserMembership
{
    [Key]
    public int Id { get; set; }

    public long TelegramId { get; set; }

    public int BotId { get; set; }

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public TelegramBot Bot { get; set; } = null!;
}
