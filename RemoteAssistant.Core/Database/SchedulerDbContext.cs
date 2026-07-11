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
    public DbSet<UserMembership> UserMemberships => Set<UserMembership>();
    public DbSet<RegistrationRequest> RegistrationRequests => Set<RegistrationRequest>();
    public DbSet<JobRequest> JobRequests => Set<JobRequest>();
    public DbSet<JobTemplate> JobTemplates => Set<JobTemplate>();
    public DbSet<JobBotMapping> JobBotMappings => Set<JobBotMapping>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserMembership>()
            .HasIndex(r => new { r.TelegramId, r.BotId })
            .IsUnique();

        modelBuilder.Entity<UserMembership>()
            .HasOne(r => r.Bot)
            .WithMany()
            .HasForeignKey(r => r.BotId);

        modelBuilder.Entity<RegistrationRequest>()
            .HasOne(r => r.Bot)
            .WithMany()
            .HasForeignKey(r => r.BotId);

        modelBuilder.Entity<JobRequest>()
            .HasOne(j => j.Bot)
            .WithMany()
            .HasForeignKey(j => j.BotId);

        modelBuilder.Entity<JobBotMapping>()
            .HasKey(bj => new { bj.BotId, bj.JobTemplateId });

        modelBuilder.Entity<JobBotMapping>()
            .HasOne(bj => bj.Bot)
            .WithMany()
            .HasForeignKey(bj => bj.BotId);

        modelBuilder.Entity<JobBotMapping>()
            .HasOne(bj => bj.JobTemplate)
            .WithMany(jt => jt.JobBotMappings)
            .HasForeignKey(bj => bj.JobTemplateId);

        modelBuilder.Entity<UserNotification>()
            .HasOne(n => n.Bot)
            .WithMany()
            .HasForeignKey(n => n.BotId);
    }
}
