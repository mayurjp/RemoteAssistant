using Microsoft.EntityFrameworkCore;
using RemoteAssistant.Core.Database;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure EF Core with SQL Server Express
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<SchedulerDbContext>(options =>
    options.UseSqlServer(connectionString, b => b.MigrationsAssembly("RemoteAssistant.WebApi")));

// Configure CORS for Angular Admin UI
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Configure HttpClient for OAuth token exchange
builder.Services.AddHttpClient();

var app = builder.Build();

// Enable CORS
app.UseCors("AllowAngular");

// Map controller routes
app.MapControllers();

// Automatically ensure the database and tables are created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
    try
    {
        context.Database.EnsureCreated();
        Console.WriteLine("Database and tables verified/created successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error verifying/creating database: {ex.Message}");
    }
}

app.Run();
