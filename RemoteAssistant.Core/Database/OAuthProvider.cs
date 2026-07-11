using System;
using System.ComponentModel.DataAnnotations;

namespace RemoteAssistant.Core.Database;

public class OAuthProvider
{
    [Key]
    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ClientId { get; set; }

    [MaxLength(500)]
    public string? ClientSecret { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
