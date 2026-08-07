using MepPanel.LicenseServer.Models;
using MepPanel.LicenseServer.Services;
using Microsoft.EntityFrameworkCore;

namespace MepPanel.LicenseServer.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IConfiguration configuration)
    {
        await db.Database.EnsureCreatedAsync();

        var defaultFeatures = configuration
            .GetSection("LicenseSettings:DefaultFeatures")
            .Get<string[]>()
            ?? PluginFeatures.All;

        var maxDevices = int.TryParse(
            configuration["LicenseSettings:DefaultMaxDevices"],
            out var parsed)
            ? parsed
            : 1;

        if (!await db.Users.AnyAsync())
        {
            var users = new[]
            {
                new
                {
                    Phone = "0900000001",
                    Name = "Nguoi dung thu 01",
                    Features = defaultFeatures
                },
                new
                {
                    Phone = "0900000002",
                    Name = "Nguoi dung thu 02",
                    Features = new[] { PluginFeatures.MepDb }
                }
            };

            foreach (var item in users)
            {
                var user = new User
                {
                    PhoneNumber = item.Phone,
                    DisplayName = item.Name,
                    Role = "User",
                    Status = UserStatuses.Active,
                    CreatedAtUtc = DateTime.UtcNow
                };

                db.Users.Add(user);
                await db.SaveChangesAsync();

                db.Licenses.Add(new License
                {
                    UserId = user.Id,
                    Plan = "Trial",
                    Status = LicenseStatuses.Active,
                    MaxDevices = maxDevices,
                    EnabledFeatures = FeatureParser.Join(item.Features),
                    StartsAtUtc = DateTime.UtcNow,
                    ExpiresAtUtc = DateTime.UtcNow.AddYears(1)
                });
            }

            await db.SaveChangesAsync();
            return;
        }

        // User cu: bo sung MEPDBWATER / MEPDBSMOKE neu thieu (khong ghi de feature khac).
        await EnsureSystemFeaturesAsync(db, new[] { "MEPDBWATER", "MEPDBSMOKE" });
    }

    private static async Task EnsureSystemFeaturesAsync(AppDbContext db, string[] required)
    {
        var licenses = await db.Licenses.Include(l => l.User).ToListAsync();
        var changed = false;

        foreach (var license in licenses)
        {
            if (license.User?.PhoneNumber == "0900000002")
            {
                // user thu 02 giu quyen toi thieu
                continue;
            }

            var current = FeatureParser.Parse(license.EnabledFeatures).ToList();
            var before = current.Count;
            foreach (var code in required)
            {
                if (!current.Any(x => string.Equals(x, code, StringComparison.OrdinalIgnoreCase)))
                {
                    current.Add(code);
                }
            }

            // Chi bo sung khi user da co MEPDB (dang dung plugin)
            if (!current.Any(x => string.Equals(x, "MEPDB", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (current.Count != before)
            {
                license.EnabledFeatures = FeatureParser.Join(current);
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync();
        }
    }
}
