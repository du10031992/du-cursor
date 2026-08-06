using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace MepPanel.AutoCAD.Licensing
{
    [DataContract]
    internal sealed class LicenseConfigFile
    {
        [DataMember(Name = "licenseServerUrl")]
        public string LicenseServerUrl { get; set; }

        [DataMember(Name = "devMode")]
        public bool DevMode { get; set; }
    }

    /// <summary>
    /// Doc MepPanel.config.json cạnh plugin (uu tien hon gia tri mac dinh).
    /// </summary>
    public static class LicenseConfig
    {
        private const string ConfigFileName = "MepPanel.config.json";
        private const string DefaultServerUrl = "https://localhost:7024/";

        private static string _cachedUrl;
        private static bool _devMode;
        private static bool _loaded;

        public static string LicenseServerBaseUrl
        {
            get
            {
                EnsureLoaded();
                return _cachedUrl ?? DefaultServerUrl;
            }
        }

        /// <summary>
        /// true = bo qua license server, mo tat ca chuc nang (dev/test tren may local).
        /// </summary>
        public static bool DevMode
        {
            get
            {
                EnsureLoaded();
                return _devMode;
            }
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            _cachedUrl = DefaultServerUrl;
            _devMode = false;

            try
            {
                string pluginDir = Path.GetDirectoryName(typeof(LicenseConfig).Assembly.Location);
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
                var serializer = new DataContractJsonSerializer(typeof(LicenseConfigFile));
                using (var stream = new MemoryStream(jsonBytes))
                {
                    var config = (LicenseConfigFile)serializer.ReadObject(stream);
                    if (config != null)
                    {
                        if (!string.IsNullOrWhiteSpace(config.LicenseServerUrl))
                        {
                            _cachedUrl = config.LicenseServerUrl.Trim();
                            if (!_cachedUrl.EndsWith("/", StringComparison.Ordinal))
                            {
                                _cachedUrl += "/";
                            }
                        }

                        _devMode = config.DevMode;
                    }
                }
            }
            catch
            {
                _cachedUrl = DefaultServerUrl;
                _devMode = false;
            }
        }
    }
}
