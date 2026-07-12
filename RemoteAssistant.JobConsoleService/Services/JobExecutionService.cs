using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RemoteAssistant.Core.Database;

namespace RemoteAssistant.JobConsoleService.Services;

public class JobExecutionService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobExecutionService> _logger;

    public JobExecutionService(IServiceProvider serviceProvider, ILogger<JobExecutionService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Job Execution Service starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();

                var pendingJobRequests = await context.JobRequests
                    .Where(j => j.Status == "Pending")
                    .OrderBy(j => j.CreatedAt)
                    .Take(5)
                    .ToListAsync(stoppingToken);

                var executors = scope.ServiceProvider.GetServices<IJobExecutor>();

                foreach (var job in pendingJobRequests)
                {
                    try
                    {
                        var executor = executors.FirstOrDefault(e => e.JobType == job.JobType);

                        if (executor == null)
                        {
                            _logger.LogWarning("No executor for JobType {JobType} (Job {JobId})", job.JobType, job.Id);
                            job.Status = "Failed";
                            job.Result = $"No executor registered for job type: {job.JobType}";
                            job.CompletedAt = DateTime.UtcNow;
                            continue;
                        }

                        _logger.LogInformation("Executing job {JobId} ({JobType}) for Telegram Bot {TelegramBotId}", job.Id, job.JobType, job.TelegramBotId);

                        var result = await executor.ExecuteAsync(job, stoppingToken);

                        job.Status = "Completed";
                        job.Result = result;
                        job.CompletedAt = DateTime.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Job {JobId} failed", job.Id);
                        job.Status = "Failed";
                        job.Result = ex.Message;
                        job.CompletedAt = DateTime.UtcNow;
                    }
                }

                if (pendingJobRequests.Count > 0)
                {
                    await context.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in job execution loop");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        _logger.LogInformation("Job Execution Service stopped.");
    }
}
