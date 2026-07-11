using RemoteAssistant.Core.Database;

namespace RemoteAssistant.JobConsoleService.Services;

public interface IJobExecutor
{
    string JobType { get; }
    Task<string> ExecuteAsync(JobRequest job, CancellationToken ct);
}
