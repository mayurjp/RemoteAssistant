using RemoteAssistant.Core.Database;

namespace JobBackGroundService.Services;

public interface IJobExecutor
{
    string JobType { get; }
    Task<string> ExecuteAsync(JobRequest job, CancellationToken ct);
}
