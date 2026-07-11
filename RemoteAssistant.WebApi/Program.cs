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

        context.Database.ExecuteSqlRaw("""
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'OAuthProviders')
            BEGIN
                CREATE TABLE [OAuthProviders] (
                    [Provider] nvarchar(50) NOT NULL,
                    [ClientId] nvarchar(500) NULL,
                    [ClientSecret] nvarchar(500) NULL,
                    [UpdatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_OAuthProviders] PRIMARY KEY ([Provider])
                );
            END
            """);

        context.Database.ExecuteSqlRaw("""
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TelegramBots')
            BEGIN
                CREATE TABLE [TelegramBots] (
                    [Id] int NOT NULL IDENTITY,
                    [Name] nvarchar(100) NOT NULL,
                    [Description] nvarchar(500) NULL,
                    [Token] nvarchar(500) NOT NULL,
                    [IsActive] bit NOT NULL DEFAULT 1,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_TelegramBots] PRIMARY KEY ([Id])
                );
            END
            """);

        context.Database.ExecuteSqlRaw("""
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'BotRegistrations')
            BEGIN
                CREATE TABLE [BotRegistrations] (
                    [Id] int NOT NULL IDENTITY,
                    [TelegramId] bigint NOT NULL,
                    [BotId] int NOT NULL,
                    [IsActive] bit NOT NULL DEFAULT 1,
                    [RegisteredAt] datetime2 NOT NULL,
                    [UnregisteredAt] datetime2 NULL,
                    CONSTRAINT [PK_BotRegistrations] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_BotRegistrations_TelegramBots_BotId] FOREIGN KEY ([BotId]) REFERENCES [TelegramBots] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [IX_BotRegistrations_TelegramId_BotId] UNIQUE ([TelegramId], [BotId])
                );
            END
            """);

        Console.WriteLine("Database and tables verified/created successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error verifying/creating database: {ex.Message}");
    }
}

app.Run();
