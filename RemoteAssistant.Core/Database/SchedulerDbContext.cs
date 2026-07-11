using Microsoft.EntityFrameworkCore;

namespace RemoteAssistant.Core.Database;

public class SchedulerDbContext : DbContext
{
    public SchedulerDbContext(DbContextOptions<SchedulerDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Seed some initial system setting keys if they don't exist
        // (Optional, we can create them dynamically when we save them)
    }
}
