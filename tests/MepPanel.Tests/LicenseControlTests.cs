using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace MepPanel.Tests;

public class LicenseControlTests : IClassFixture<LicenseWebAppFactory>
{
    private readonly LicenseWebAppFactory _factory;

    public LicenseControlTests(LicenseWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Admin_Can_Block_User_And_Deny_Login()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Admin-ApiKey", "MEP-PANEL-ADMIN-TEST-2026");

        var created = await client.PostAsJsonAsync("/api/admin/users", new
        {
            phoneNumber = "0911000001",
            displayName = "Tester Block",
            maxDevices = 1,
            features = new[] { "MEPDB", "MEPHVAC" }
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        using var createdDoc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var userId = createdDoc.RootElement.GetProperty("id").GetInt32();

        var block = await client.PatchAsJsonAsync(
            $"/api/admin/users/{userId}/status",
            new { status = "Blocked" });
        Assert.Equal(HttpStatusCode.OK, block.StatusCode);

        var anon = _factory.CreateClient();
        var otp = await anon.PostAsJsonAsync("/api/Auth/request-otp", new
        {
            phoneNumber = "0911000001"
        });
        Assert.Equal(HttpStatusCode.Forbidden, otp.StatusCode);
    }

    [Fact]
    public async Task One_Phone_One_Device_Requires_Admin_Release_To_Switch()
    {
        var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.Add("X-Admin-ApiKey", "MEP-PANEL-ADMIN-TEST-2026");

        var phone = "0911000002";
        var create = await admin.PostAsJsonAsync("/api/admin/users", new
        {
            phoneNumber = phone,
            displayName = "Tester Device",
            maxDevices = 1,
            features = new[] { "MEPDB" }
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        using var createDoc = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var userId = createDoc.RootElement.GetProperty("id").GetInt32();

        var userClient = _factory.CreateClient();
        var token = await LoginAsync(userClient, phone);

        userClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var activateA = await userClient.PostAsJsonAsync("/api/Devices/activate", new
        {
            phoneNumber = phone,
            deviceKey = "device-key-A",
            deviceName = "PC-A",
            autoCadVersion = "2021",
            pluginVersion = "1.0.0"
        });
        Assert.Equal(HttpStatusCode.OK, activateA.StatusCode);

        var activateB = await userClient.PostAsJsonAsync("/api/Devices/activate", new
        {
            phoneNumber = phone,
            deviceKey = "device-key-B",
            deviceName = "PC-B",
            autoCadVersion = "2021",
            pluginVersion = "1.0.0"
        });
        Assert.Equal(HttpStatusCode.Forbidden, activateB.StatusCode);

        var release = await admin.PostAsync($"/api/admin/users/{userId}/release-device", null);
        Assert.Equal(HttpStatusCode.OK, release.StatusCode);

        var activateB2 = await userClient.PostAsJsonAsync("/api/Devices/activate", new
        {
            phoneNumber = phone,
            deviceKey = "device-key-B",
            deviceName = "PC-B",
            autoCadVersion = "2021",
            pluginVersion = "1.0.0"
        });
        Assert.Equal(HttpStatusCode.OK, activateB2.StatusCode);
    }

    [Fact]
    public async Task Admin_Overview_And_Put_Toggle_Feature_Work()
    {
        var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.Add("X-Admin-ApiKey", "MEP-PANEL-ADMIN-TEST-2026");

        var phone = "0911000004";
        var create = await admin.PostAsJsonAsync("/api/Admin/users", new
        {
            phoneNumber = phone,
            displayName = "Overview User",
            maxDevices = 1,
            features = new[] { "MEPDB", "MEPHVAC" }
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        using var createDoc = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var userId = createDoc.RootElement.GetProperty("id").GetInt32();

        var overview = await admin.GetAsync("/api/Admin/overview");
        Assert.Equal(HttpStatusCode.OK, overview.StatusCode);

        var disableHvac = await admin.PutAsJsonAsync(
            $"/api/Admin/users/{userId}/features/MEPHVAC",
            new { enabled = false });
        Assert.Equal(HttpStatusCode.OK, disableHvac.StatusCode);

        using var featureDoc = JsonDocument.Parse(await disableHvac.Content.ReadAsStringAsync());
        var features = featureDoc.RootElement.GetProperty("features")
            .EnumerateArray()
            .Select(x => x.GetString())
            .ToArray();
        Assert.Contains("MEPDB", features);
        Assert.DoesNotContain("MEPHVAC", features);

        var blockDeviceUser = await admin.PutAsJsonAsync(
            $"/api/Admin/users/{userId}/status",
            new { status = "Blocked" });
        Assert.Equal(HttpStatusCode.OK, blockDeviceUser.StatusCode);
    }

    [Fact]
    public async Task Admin_Can_Lock_Plugin_Features()
    {
        var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.Add("X-Admin-ApiKey", "MEP-PANEL-ADMIN-TEST-2026");

        var phone = "0911000003";
        var create = await admin.PostAsJsonAsync("/api/admin/users", new
        {
            phoneNumber = phone,
            displayName = "Tester Features",
            maxDevices = 1,
            features = new[] { "MEPDB", "MEPHVAC" }
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        using var createDoc = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var userId = createDoc.RootElement.GetProperty("id").GetInt32();

        var setFeatures = await admin.PatchAsJsonAsync(
            $"/api/admin/users/{userId}/features",
            new { features = new[] { "MEPDB" } });
        Assert.Equal(HttpStatusCode.OK, setFeatures.StatusCode);

        var userClient = _factory.CreateClient();
        var token = await LoginAsync(userClient, phone);
        userClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var activate = await userClient.PostAsJsonAsync("/api/Devices/activate", new
        {
            phoneNumber = phone,
            deviceKey = "device-key-feature",
            deviceName = "PC-Feature",
            autoCadVersion = "2021",
            pluginVersion = "1.0.0"
        });
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode);

        var check = await userClient.PostAsJsonAsync("/api/Devices/check", new
        {
            phoneNumber = phone,
            deviceKey = "device-key-feature",
            pluginVersion = "1.0.0"
        });
        Assert.Equal(HttpStatusCode.OK, check.StatusCode);

        using var checkDoc = JsonDocument.Parse(await check.Content.ReadAsStringAsync());
        Assert.True(checkDoc.RootElement.GetProperty("valid").GetBoolean());

        var features = checkDoc.RootElement.GetProperty("features")
            .EnumerateArray()
            .Select(x => x.GetString())
            .ToArray();

        Assert.Contains("MEPDB", features);
        Assert.DoesNotContain("MEPHVAC", features);
    }

    [Fact]
    public async Task Disable_MEPDB_Strips_All_SubFeatures()
    {
        var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.Add("X-Admin-ApiKey", "MEP-PANEL-ADMIN-TEST-2026");

        var phone = "0911000005";
        var create = await admin.PostAsJsonAsync("/api/admin/users", new
        {
            phoneNumber = phone,
            displayName = "Entry Rule User",
            maxDevices = 1,
            features = new[] { "MEPDB", "MEPHVAC", "MEPDBCABINET2D", "MEPDBDRAW" }
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        using var createDoc = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var userId = createDoc.RootElement.GetProperty("id").GetInt32();

        var disableEntry = await admin.PutAsJsonAsync(
            $"/api/Admin/users/{userId}/features/MEPDB",
            new { enabled = false });
        Assert.Equal(HttpStatusCode.OK, disableEntry.StatusCode);

        using var featureDoc = JsonDocument.Parse(await disableEntry.Content.ReadAsStringAsync());
        var features = featureDoc.RootElement.GetProperty("features")
            .EnumerateArray()
            .Select(x => x.GetString())
            .ToArray();

        Assert.Empty(features);
    }

    [Theory]
    [InlineData("MEPDBDRAW")]
    [InlineData("MEPHVAC")]
    [InlineData("MEPDBWATER")]
    [InlineData("MEPDBSMOKE")]
    [InlineData("MEPSELAYER")]
    [InlineData("MEPDBCONFIG")]
    [InlineData("MEPDBEXPORT")]
    [InlineData("MEPDBCABINET2D")]
    [InlineData("MEPDBUPDATE")]
    [InlineData("MEPDBEXCEL")]
    [InlineData("MEPDBCABINETVIEWS")]
    [InlineData("MEPDBPOWER")]
    [InlineData("MEPDB3P4W")]
    public async Task Toggle_SubFeature_Reflects_In_Device_Check(string subFeature)
    {
        var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.Add("X-Admin-ApiKey", "MEP-PANEL-ADMIN-TEST-2026");

        var suffix = Math.Abs(StringComparer.Ordinal.GetHashCode(subFeature)) % 100000000;
        var phone = $"09{suffix:D8}";
        var create = await admin.PostAsJsonAsync("/api/admin/users", new
        {
            phoneNumber = phone,
            displayName = $"Toggle {subFeature}",
            maxDevices = 1,
            features = new[] { "MEPDB", subFeature }
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        using var createDoc = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var userId = createDoc.RootElement.GetProperty("id").GetInt32();

        var disable = await admin.PutAsJsonAsync(
            $"/api/Admin/users/{userId}/features/{subFeature}",
            new { enabled = false });
        Assert.Equal(HttpStatusCode.OK, disable.StatusCode);

        var userClient = _factory.CreateClient();
        var token = await LoginAsync(userClient, phone);
        userClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var activate = await userClient.PostAsJsonAsync("/api/Devices/activate", new
        {
            phoneNumber = phone,
            deviceKey = $"device-{subFeature}",
            deviceName = "PC-SubFeature",
            autoCadVersion = "2021",
            pluginVersion = "1.0.0"
        });
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode);

        var check = await userClient.PostAsJsonAsync("/api/Devices/check", new
        {
            phoneNumber = phone,
            deviceKey = $"device-{subFeature}",
            pluginVersion = "1.0.0"
        });
        Assert.Equal(HttpStatusCode.OK, check.StatusCode);

        using var checkDoc = JsonDocument.Parse(await check.Content.ReadAsStringAsync());
        var features = checkDoc.RootElement.GetProperty("features")
            .EnumerateArray()
            .Select(x => x.GetString())
            .ToArray();

        Assert.Contains("MEPDB", features);
        Assert.DoesNotContain(subFeature, features);
    }

    private static async Task<string> LoginAsync(HttpClient client, string phone)
    {
        var otp = await client.PostAsJsonAsync("/api/Auth/request-otp", new { phoneNumber = phone });
        Assert.Equal(HttpStatusCode.OK, otp.StatusCode);

        var verify = await client.PostAsJsonAsync("/api/Auth/verify-otp", new
        {
            phoneNumber = phone,
            otp = "123456"
        });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);

        using var doc = JsonDocument.Parse(await verify.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("accessToken").GetString()
            ?? throw new InvalidOperationException("Missing accessToken");
    }
}
