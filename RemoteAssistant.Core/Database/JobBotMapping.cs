namespace RemoteAssistant.Core.Database;

public class JobBotMapping
{
    public int TelegramBotId { get; set; }
    public TelegramBot TelegramBot { get; set; } = null!;

    public int JobTemplateId { get; set; }
    public JobTemplate JobTemplate { get; set; } = null!;
}
