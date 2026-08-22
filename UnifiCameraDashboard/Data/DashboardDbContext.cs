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
    public DbSet<StoredEvent> Events { get; set; } = null!;
    public DbSet<StoredPushSubscription> PushSubscriptions { get; set; } = null!;

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

        // Event configuration
        modelBuilder.Entity<StoredEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UnifiEventId).IsUnique();
            entity.HasIndex(e => e.CameraUnifiId);
            entity.HasIndex(e => e.Start);
            entity.Property(e => e.UnifiEventId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.CameraUnifiId).HasMaxLength(64);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(64);
            entity.Property(e => e.SmartDetectTypes).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ThumbnailPath).HasMaxLength(500);
            entity.Property(e => e.YoloLabels).IsRequired().HasMaxLength(200);
        });

        // Push subscription configuration
        modelBuilder.Entity<StoredPushSubscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Endpoint).IsUnique();
            entity.Property(e => e.Endpoint).IsRequired().HasMaxLength(500);
            entity.Property(e => e.P256dh).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Auth).IsRequired().HasMaxLength(100);
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

public class StoredEvent
{
    public int Id { get; set; }
    public string UnifiEventId { get; set; } = string.Empty;
    public string? CameraUnifiId { get; set; }
    public string Type { get; set; } = string.Empty;
    // Comma-separated (person,vehicle,package,animal,face,licensePlate,...) - simple over
    // normalized since it's a small closed-ish set of UniFi-defined tags, read far more than written.
    public string SmartDetectTypes { get; set; } = string.Empty;
    public int? Score { get; set; }
    public DateTime Start { get; set; }
    public DateTime? End { get; set; }
    public string? ThumbnailPath { get; set; }
    // Comma-separated COCO labels from YOLO classification (e.g. "person,car"), same convention
    // as SmartDetectTypes. Empty until classified (see YoloClassifiedAt); stays empty if the
    // model found nothing above its confidence threshold - that's a real, meaningful result
    // (the "filter rate" signal), not the same as "not yet classified".
    public string YoloLabels { get; set; } = string.Empty;
    public DateTime? YoloClassifiedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// One browser/device's Web Push registration. Endpoint is the push service's per-subscription
/// URL - effectively its identity, since a browser gets a new one if it re-subscribes.
/// </summary>
public class StoredPushSubscription
{
    public int Id { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
