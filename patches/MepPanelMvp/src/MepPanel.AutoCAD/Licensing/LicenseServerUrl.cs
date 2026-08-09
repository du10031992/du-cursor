using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;

namespace MepPanel.AutoCAD.Licensing
{
    internal static class LicenseServerUrl
    {
        private static readonly string[] FallbackUrls =
        {
            "http://localhost:5268/",
            "https://localhost:7024/",
            "http://127.0.0.1:5268/"
        };

        public static string ResolvePrimary()
        {
            string fromConfig = TryReadConfigUrl();
            if (!string.IsNullOrWhiteSpace(fromConfig))
            {
                return Normalize(fromConfig);
            }

            return FallbackUrls[0];
        }

        public static string[] AllCandidates()
        {
            var list = new System.Collections.Generic.List<string>();
            string fromConfig = TryReadConfigUrl();
            if (!string.IsNullOrWhiteSpace(fromConfig))
            {
                list.Add(Normalize(fromConfig));
                // Client production da cau hinh Server trung tam:
                // khong duoc fallback ve localhost, tranh may client tin mot server
                // khac tren chinh may do khi Server trung tam dang mat ket noi.
                return list.ToArray();
            }

            foreach (string url in FallbackUrls)
            {
                string norm = Normalize(url);
                if (!list.Exists(x => string.Equals(x, norm, StringComparison.OrdinalIgnoreCase)))
                {
                    list.Add(norm);
                }
            }

            return list.ToArray();
        }

        private static string TryReadConfigUrl()
        {
            try
            {
                string baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrWhiteSpace(baseDir))
                {
                    return null;
                }

                string path = Path.Combine(baseDir, "MepPanel.config.json");
                if (!File.Exists(path))
                {
                    return null;
                }

                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    var serializer = new DataContractJsonSerializer(typeof(LicenseConfigDto));
                    var dto = serializer.ReadObject(stream) as LicenseConfigDto;
                    return dto?.LicenseServerUrl;
                }
            }
            catch
            {
                return null;
            }
        }

        private static string Normalize(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return FallbackUrls[0];
            }

            string trimmed = url.Trim();
            if (!trimmed.EndsWith("/", StringComparison.Ordinal))
            {
                trimmed += "/";
            }

            return trimmed;
        }

        [System.Runtime.Serialization.DataContract]
        private class LicenseConfigDto
        {
            [System.Runtime.Serialization.DataMember(Name = "licenseServerUrl")]
            public string LicenseServerUrl { get; set; }
        }
    }
}
