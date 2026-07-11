using Microsoft.EntityFrameworkCore;
using RemoteAssistant.Core.Database;
using RemoteAssistant.TelegramConsoleService.Services;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<SchedulerDbContext>(options =>
    options.UseSqlServer(connectionString), ServiceLifetime.Transient);

builder.Services.AddHttpClient();
builder.Services.AddHostedService<TelegramBotService>();

var host = builder.Build();
host.Run();
