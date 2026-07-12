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
            .HasIndex(r => new { r.TelegramId, r.TelegramBotId })
            .IsUnique();

        modelBuilder.Entity<UserMembership>()
            .HasOne(r => r.TelegramBot)
            .WithMany()
            .HasForeignKey(r => r.TelegramBotId);

        modelBuilder.Entity<RegistrationRequest>()
            .HasOne(r => r.TelegramBot)
            .WithMany()
            .HasForeignKey(r => r.TelegramBotId);

        modelBuilder.Entity<JobRequest>()
            .HasOne(j => j.TelegramBot)
            .WithMany()
            .HasForeignKey(j => j.TelegramBotId);

        modelBuilder.Entity<JobBotMapping>()
            .HasKey(bj => new { bj.TelegramBotId, bj.JobTemplateId });

        modelBuilder.Entity<JobBotMapping>()
            .HasOne(bj => bj.TelegramBot)
            .WithMany()
            .HasForeignKey(bj => bj.TelegramBotId);

        modelBuilder.Entity<JobBotMapping>()
            .HasOne(bj => bj.JobTemplate)
            .WithMany(jt => jt.JobBotMappings)
            .HasForeignKey(bj => bj.JobTemplateId);

        modelBuilder.Entity<UserNotification>()
            .HasOne(n => n.TelegramBot)
            .WithMany()
            .HasForeignKey(n => n.TelegramBotId);
    }
}
