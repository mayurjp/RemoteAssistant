using Microsoft.EntityFrameworkCore;

namespace RemoteAssistant.Core.Database;

public class SchedulerDbContext : DbContext
{
    public SchedulerDbContext(DbContextOptions<SchedulerDbContext> options) : base(options)
    {
    }

    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<OAuthProvider> OAuthProviders => Set<OAuthProvider>();
    public DbSet<TelegramBot> TelegramBots => Set<TelegramBot>();
    public DbSet<BotRegistration> BotRegistrations => Set<BotRegistration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<BotRegistration>()
            .HasIndex(r => new { r.TelegramId, r.BotId })
            .IsUnique();

        modelBuilder.Entity<BotRegistration>()
            .HasOne(r => r.Bot)
            .WithMany()
            .HasForeignKey(r => r.BotId);
    }
}
