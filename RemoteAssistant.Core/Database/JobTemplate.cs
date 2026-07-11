using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RemoteAssistant.Core.Database;

public class JobTemplate
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string JobType { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<JobBotMapping> JobBotMappings { get; set; } = new List<JobBotMapping>();
}
