using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RemoteAssistant.Core.Database;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<SchedulerDbContext>(options =>
    options.UseSqlServer(connectionString, b => b.MigrationsAssembly("RemoteAssistant.WebApi")));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddHttpClient();

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(jwtKey))
{
    jwtKey = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + Convert.ToBase64String(Guid.NewGuid().ToByteArray());
    builder.Configuration["Jwt:Key"] = jwtKey;
    Console.WriteLine("WARNING: Jwt:Key not configured. Generated random key for this session.");
    Console.WriteLine("Set 'Jwt:Key' in appsettings.json for persistent keys across restarts.");
}
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "RemoteAssistant";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "RemoteAssistant-AdminUI";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseCors("AllowAngular");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

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
