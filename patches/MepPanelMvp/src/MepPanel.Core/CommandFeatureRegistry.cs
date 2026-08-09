using System;
using System.Collections.Generic;

namespace MepPanel.Core
{
    /// <summary>
    /// Nguon mapping duy nhat: lenh noi bo -> feature duoc Admin cap.
    /// Lenh moi phai dung MepRequiresFeatureAttribute hoac dang ky alias o day.
    /// </summary>
    public static class CommandFeatureRegistry
    {
        private static readonly Dictionary<string, string> Aliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["MEPWATER"] = PluginFeatures.Water,
                ["MEPFIRE"] = PluginFeatures.Smoke,

                ["MEPDBKNOWLEDGE"] = PluginFeatures.Draw,
                ["MEPDEVICEBLOCKS"] = PluginFeatures.Draw,

                ["MEPDBEDIT"] = PluginFeatures.Update,

                ["MEPDBRENDER"] = PluginFeatures.Cabinet2D,
                ["MEPDBUNFOLD"] = PluginFeatures.Cabinet2D,
                ["MEPDBREALWIRING"] = PluginFeatures.Cabinet2D,
                ["MEPDBREALRENDER"] = PluginFeatures.Cabinet2D,
                ["MEPDBCABINET3D"] = PluginFeatures.Cabinet2D,
                ["MEPDBDUPLICATE"] = PluginFeatures.Cabinet2D,

                ["MEPHVACDRAW"] = PluginFeatures.MepHvac,
                ["MEPHVACCONFIG"] = PluginFeatures.MepHvac,
                ["MEPHVACSMOKE"] = PluginFeatures.MepHvac
            };

        public static string Resolve(string commandName)
        {
            if (string.IsNullOrWhiteSpace(commandName))
            {
                return null;
            }

            if (PluginFeatures.IsSubFeature(commandName))
            {
                return commandName;
            }

            string feature;
            if (Aliases.TryGetValue(commandName, out feature))
            {
                return feature;
            }

            if (commandName.StartsWith("MEPHVAC", StringComparison.OrdinalIgnoreCase))
            {
                return PluginFeatures.MepHvac;
            }

            return null;
        }
    }
}
