using Microsoft.EntityFrameworkCore;

namespace UnifiCameraDashboard.Data;

public class DashboardDbContext : DbContext
{
    public DashboardDbContext(DbContextOptions<DashboardDbContext> options)
   : base(options)
    {
    }

    public DbSet<StoredCamera> Cameras { get; set; } = null!;
    public DbSet<AppSettings> Settings { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Camera configuration
        modelBuilder.Entity<StoredCamera>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UnifiId).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.SnapshotUrl).IsRequired().HasMaxLength(500);
            entity.Property(e => e.RtspUrl).HasMaxLength(500);
        });

        // Settings configuration
        modelBuilder.Entity<AppSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Key).IsUnique();
            entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Value).HasMaxLength(1000);
        });
    }
}

// Entities
public class StoredCamera
{
    public int Id { get; set; }
    public string UnifiId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SnapshotUrl { get; set; } = string.Empty;
    public string RtspUrl { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string FirmwareVersion { get; set; } = string.Empty;
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public int GridOrder { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class AppSettings
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool IsEncrypted { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
