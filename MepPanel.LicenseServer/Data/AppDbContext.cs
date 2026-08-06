using MepPanel.LicenseServer.Models;
using Microsoft.EntityFrameworkCore;

namespace MepPanel.LicenseServer.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<License> Licenses => Set<License>();

    public DbSet<Device> Devices => Set<Device>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(x => x.PhoneNumber).IsUnique();
            entity.Property(x => x.PhoneNumber).HasMaxLength(32).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(128);
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Role).HasMaxLength(32).IsRequired();
        });

        modelBuilder.Entity<License>(entity =>
        {
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.Property(x => x.Plan).HasMaxLength(64);
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.EnabledFeatures).HasMaxLength(512).IsRequired();

            entity.HasOne(x => x.User)
                .WithOne(x => x.License)
                .HasForeignKey<License>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasIndex(x => x.DeviceKeyHash);
            entity.HasIndex(x => new { x.UserId, x.Status });
            entity.Property(x => x.DeviceKeyHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.DeviceName).HasMaxLength(128);
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();

            entity.HasOne(x => x.User)
                .WithMany(x => x.Devices)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(x => x.Action).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Detail).HasMaxLength(2000);
        });
    }
}
