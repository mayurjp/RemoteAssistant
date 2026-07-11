using System;
using System.ComponentModel.DataAnnotations;

namespace RemoteAssistant.Core.Database;

public class JobRequest
{
    [Key]
    public int Id { get; set; }

    public int BotId { get; set; }

    [MaxLength(100)]
    public string JobType { get; set; } = string.Empty;

    public long TelegramId { get; set; }

    [MaxLength(2000)]
    public string? Parameters { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    [MaxLength(4000)]
    public string? Result { get; set; }

    public TelegramBot Bot { get; set; } = null!;
}
