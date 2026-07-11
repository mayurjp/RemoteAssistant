using Microsoft.EntityFrameworkCore;
using RemoteAssistant.Core.Database;
using RemoteAssistant.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configure EF Core with SQL Server Express
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<SchedulerDbContext>(options =>
    options.UseSqlServer(connectionString), ServiceLifetime.Transient);

builder.Services.AddHttpClient();
builder.Services.AddTransient<GmailSenderService>();
builder.Services.AddHostedService<TelegramBotService>();

var host = builder.Build();
host.Run();
