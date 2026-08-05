using MepPanel.LicenseServer.Models;
using MepPanel.LicenseServer.Services;
using Microsoft.EntityFrameworkCore;

namespace MepPanel.LicenseServer.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IConfiguration configuration)
    {
        await db.Database.EnsureCreatedAsync();

        if (await db.Users.AnyAsync())
        {
            return;
        }

        var defaultFeatures = configuration
            .GetSection("LicenseSettings:DefaultFeatures")
            .Get<string[]>()
            ?? PluginFeatures.All;

        var maxDevices = int.TryParse(
            configuration["LicenseSettings:DefaultMaxDevices"],
            out var parsed)
            ? parsed
            : 1;

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
    }
}
