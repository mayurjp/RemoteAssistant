namespace RemoteAssistant.Core.Database;

public class JobBotMapping
{
    public int BotId { get; set; }
    public TelegramBot Bot { get; set; } = null!;

    public int JobTemplateId { get; set; }
    public JobTemplate JobTemplate { get; set; } = null!;
}
