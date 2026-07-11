using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RemoteAssistant.Core.Database;

namespace JobBackGroundService.Services;

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

                var pendingJobs = await context.Jobs
                    .Where(j => j.Status == "Pending")
                    .OrderBy(j => j.CreatedAt)
                    .Take(5)
                    .ToListAsync(stoppingToken);

                foreach (var job in pendingJobs)
                {
                    _logger.LogInformation("Executing job {JobId} for bot {BotId}", job.Id, job.BotId);
                    job.Status = "Completed";
                    job.CompletedAt = DateTime.UtcNow;
                    // TODO: Actual job execution logic
                }

                if (pendingJobs.Count > 0)
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
