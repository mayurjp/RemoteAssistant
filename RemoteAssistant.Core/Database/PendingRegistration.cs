using System;
using System.ComponentModel.DataAnnotations;

namespace RemoteAssistant.Core.Database;

public class PendingRegistration
{
    [Key]
    public int Id { get; set; }

    public long TelegramId { get; set; }

    public int BotId { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Pending";

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAt { get; set; }

    [MaxLength(100)]
    public string? ReviewedBy { get; set; }

    public TelegramBot Bot { get; set; } = null!;
}
