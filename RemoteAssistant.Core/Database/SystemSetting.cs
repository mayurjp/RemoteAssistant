using System;
using System.ComponentModel.DataAnnotations;

namespace RemoteAssistant.Core.Database;

public class SystemSetting
{
    [Key]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
