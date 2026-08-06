using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace MepPanel.Blocks.AutoCAD.Drawing
{
    [DataContract]
    internal sealed class MepPluginConfigFile
    {
        [DataMember(Name = "pipeLibraryDwg")]
        public string PipeLibraryDwg { get; set; }
    }

    /// <summary>Đọc cấu hình plugin (MepPanel.config.json cạnh DLL).</summary>
    internal static class MepPluginConfig
    {
        private const string ConfigFileName = "MepPanel.config.json";
        private const string DefaultTemplateName = "AMC_TEMPLATE_RV29.dwg";

        private static string _pipeLibraryDwg;
        private static bool _loaded;

        public static string PipeLibraryDwg
        {
            get
            {
                EnsureLoaded();
                return _pipeLibraryDwg;
            }
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            _pipeLibraryDwg = ResolveDefaultTemplatePath();

            try
            {
                string pluginDir = Path.GetDirectoryName(typeof(MepPluginConfig).Assembly.Location);
                if (string.IsNullOrWhiteSpace(pluginDir))
                {
                    return;
                }

                string configPath = Path.Combine(pluginDir, ConfigFileName);
                if (!File.Exists(configPath))
                {
                    return;
                }

                byte[] jsonBytes = File.ReadAllBytes(configPath);
                var serializer = new DataContractJsonSerializer(typeof(MepPluginConfigFile));
                using (var stream = new MemoryStream(jsonBytes))
                {
                    var config = (MepPluginConfigFile)serializer.ReadObject(stream);
                    if (config != null && !string.IsNullOrWhiteSpace(config.PipeLibraryDwg))
                    {
                        string configured = config.PipeLibraryDwg.Trim();
                        _pipeLibraryDwg = Path.IsPathRooted(configured)
                            ? configured
                            : Path.GetFullPath(Path.Combine(pluginDir, configured));
                    }
                }
            }
            catch
            {
                _pipeLibraryDwg = ResolveDefaultTemplatePath();
            }
        }

        private static string ResolveDefaultTemplatePath()
        {
            string pluginDir = Path.GetDirectoryName(typeof(MepPluginConfig).Assembly.Location)
                ?? AppDomain.CurrentDomain.BaseDirectory;

            string[] candidates =
            {
                Path.Combine(pluginDir, "samples", "templates", DefaultTemplateName),
                Path.Combine(pluginDir, "..", "..", "samples", "templates", DefaultTemplateName),
                Path.Combine(pluginDir, DefaultTemplateName)
            };

            foreach (string path in candidates)
            {
                string full = Path.GetFullPath(path);
                if (File.Exists(full))
                {
                    return full;
                }
            }

            return Path.Combine(pluginDir, "samples", "templates", DefaultTemplateName);
        }
    }
}
