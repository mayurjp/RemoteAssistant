using Microsoft.EntityFrameworkCore;
using RemoteAssistant.Core.Database;
using RemoteAssistant.JobConsoleService.Services;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<SchedulerDbContext>(options =>
    options.UseSqlServer(connectionString), ServiceLifetime.Transient);

builder.Services.AddHttpClient();

builder.Services.AddSingleton<IJobExecutor, KiteDataLoadJob>();
builder.Services.AddHostedService<JobExecutionService>();

var host = builder.Build();
host.Run();
