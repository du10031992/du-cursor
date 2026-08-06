using MepPanel.LicenseServer.Models;

namespace MepPanel.LicenseServer.Services;

public static class FeatureParser
{
    public static IReadOnlyList<string> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        return raw
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().ToUpperInvariant())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string Join(IEnumerable<string>? features)
    {
        var list = (features ?? Array.Empty<string>())
            .Select(x => x.Trim().ToUpperInvariant())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return string.Join(",", list);
    }

    public static bool HasFeature(string? enabledFeatures, string featureCode)
    {
        if (string.IsNullOrWhiteSpace(featureCode))
        {
            return false;
        }

        var wanted = featureCode.Trim().ToUpperInvariant();
        return Parse(enabledFeatures)
            .Any(x => string.Equals(x, wanted, StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<string> NormalizeKnown(IEnumerable<string>? features)
    {
        var known = new HashSet<string>(
            PluginFeatures.All,
            StringComparer.OrdinalIgnoreCase);

        return Parse(Join(features))
            .Where(known.Contains)
            .ToArray();
    }

    /// <summary>MEPDB bat buoc de dung sub-feature; tat MEPDB thi xoa het sub.</summary>
    public static IReadOnlyList<string> ApplyEntryRules(IEnumerable<string>? features)
    {
        var list = NormalizeKnown(features).ToList();
        var hasEntry = list.Any(x => PluginFeatures.IsEntry(x));

        if (!hasEntry)
        {
            return Array.Empty<string>();
        }

        return list;
    }
}
