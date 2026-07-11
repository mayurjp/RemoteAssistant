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

        // Drop stale tables in reverse dependency order (dependents first)
        var staleTables = new[] { "BotNotifications", "UserNotifications", "RegistrationRequests", "UserMemberships", "TelegramBotRegistrations", "UserRegistrations", "BotRegistrations", "BotJobTemplates", "BotJobAssignments", "PendingRegistrations", "Users", "BotJobs", "JobDefinitions", "Jobs" };
        foreach (var tbl in staleTables)
        {
            try { context.Database.ExecuteSqlRaw($"IF OBJECT_ID('{tbl}', 'U') IS NOT NULL DROP TABLE [{tbl}];"); }
            catch (Exception ex) { Console.WriteLine($"Could not drop {tbl}: {ex.Message}"); }
        }

        context.Database.ExecuteSqlRaw("""
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'UserMemberships')
            BEGIN
                CREATE TABLE [UserMemberships] (
                    [Id] int NOT NULL IDENTITY,
                    [TelegramId] bigint NOT NULL,
                    [BotId] int NOT NULL,
                    [RegisteredAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_UserMemberships] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_UserMemberships_TelegramBots_BotId] FOREIGN KEY ([BotId]) REFERENCES [TelegramBots] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [IX_UserMemberships_TelegramId_BotId] UNIQUE ([TelegramId], [BotId])
                );
            END
            """);

        context.Database.ExecuteSqlRaw("""
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'RegistrationRequests')
            BEGIN
                CREATE TABLE [RegistrationRequests] (
                    [Id] int NOT NULL IDENTITY,
                    [TelegramId] bigint NOT NULL,
                    [BotId] int NOT NULL,
                    [Status] nvarchar(50) NOT NULL DEFAULT 'Pending',
                    [RequestedAt] datetime2 NOT NULL,
                    [ReviewedAt] datetime2 NULL,
                    [ReviewedBy] nvarchar(100) NULL,
                    CONSTRAINT [PK_RegistrationRequests] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_RegistrationRequests_TelegramBots_BotId] FOREIGN KEY ([BotId]) REFERENCES [TelegramBots] ([Id]) ON DELETE CASCADE
                );
            END
            """);

        context.Database.ExecuteSqlRaw("""
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'JobRequests')
            BEGIN
                CREATE TABLE [JobRequests] (
                    [Id] int NOT NULL IDENTITY,
                    [BotId] int NOT NULL,
                    [JobType] nvarchar(100) NOT NULL,
                    [TelegramId] bigint NOT NULL,
                    [Parameters] nvarchar(2000) NULL,
                    [Status] nvarchar(50) NOT NULL DEFAULT 'Pending',
                    [CreatedAt] datetime2 NOT NULL,
                    [CompletedAt] datetime2 NULL,
                    [Result] nvarchar(4000) NULL,
                    CONSTRAINT [PK_JobRequests] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_JobRequests_TelegramBots_BotId] FOREIGN KEY ([BotId]) REFERENCES [TelegramBots] ([Id])
                );
            END
            """);

        context.Database.ExecuteSqlRaw("""
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'JobTemplates')
            BEGIN
                CREATE TABLE [JobTemplates] (
                    [Id] int NOT NULL IDENTITY,
                    [JobType] nvarchar(100) NOT NULL,
                    [Name] nvarchar(200) NOT NULL,
                    [Description] nvarchar(500) NULL,
                    [IsActive] bit NOT NULL DEFAULT 1,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_JobTemplates] PRIMARY KEY ([Id])
                );

                INSERT INTO [JobTemplates] ([JobType], [Name], [Description], [CreatedAt], [UpdatedAt])
                VALUES ('KiteDataLoad', 'Kite Connect Data Load', 'Fetches mock Kite Connect OHLC candle data', GETDATE(), GETDATE());
            END
            """);

        context.Database.ExecuteSqlRaw("""
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'JobBotMappings')
            BEGIN
                CREATE TABLE [JobBotMappings] (
                    [BotId] int NOT NULL,
                    [JobTemplateId] int NOT NULL,
                    CONSTRAINT [PK_JobBotMappings] PRIMARY KEY ([BotId], [JobTemplateId]),
                    CONSTRAINT [FK_JobBotMappings_TelegramBots_BotId] FOREIGN KEY ([BotId]) REFERENCES [TelegramBots] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_JobBotMappings_JobTemplates_JobTemplateId] FOREIGN KEY ([JobTemplateId]) REFERENCES [JobTemplates] ([Id]) ON DELETE CASCADE
                );
            END
            """);

        context.Database.ExecuteSqlRaw("""
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'UserNotifications')
            BEGIN
                CREATE TABLE [UserNotifications] (
                    [Id] int NOT NULL IDENTITY,
                    [BotId] int NOT NULL,
                    [TelegramId] bigint NOT NULL,
                    [Message] nvarchar(2000) NOT NULL,
                    [Sent] bit NOT NULL DEFAULT 0,
                    [CreatedAt] datetime2 NOT NULL,
                    [SentAt] datetime2 NULL,
                    CONSTRAINT [PK_UserNotifications] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_UserNotifications_TelegramBots_BotId] FOREIGN KEY ([BotId]) REFERENCES [TelegramBots] ([Id])
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
