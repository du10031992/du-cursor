using System;
using System.Collections.Generic;
using System.Linq;

namespace MepPanel.AutoCAD.Licensing
{
    public static class LicenseSession
    {
        private static readonly HashSet<string> FeatureSet =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static bool IsAuthorized { get; private set; }

        public static string PhoneNumber { get; private set; }

        public static string DisplayName { get; private set; }

        public static string AccessToken { get; private set; }

        public static string LicensePlan { get; private set; }

        public static DateTime AuthorizedAtUtc { get; private set; }

        public static IReadOnlyCollection<string> Features
        {
            get { return FeatureSet.ToArray(); }
        }

        public static void Authorize(
            string phoneNumber,
            string displayName,
            string accessToken,
            IEnumerable<string> features = null,
            string licensePlan = null)
        {
            PhoneNumber = phoneNumber ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            AccessToken = accessToken ?? string.Empty;
            LicensePlan = licensePlan ?? string.Empty;
            AuthorizedAtUtc = DateTime.UtcNow;
            IsAuthorized = true;

            FeatureSet.Clear();
            if (features != null)
            {
                foreach (string feature in features)
                {
                    if (!string.IsNullOrWhiteSpace(feature))
                    {
                        FeatureSet.Add(feature.Trim().ToUpperInvariant());
                    }
                }
            }
        }

        public static bool HasFeature(string featureCode)
        {
            if (!IsAuthorized || string.IsNullOrWhiteSpace(featureCode))
            {
                return false;
            }

            return FeatureSet.Contains(featureCode.Trim().ToUpperInvariant());
        }

        public static void Clear()
        {
            IsAuthorized = false;
            PhoneNumber = string.Empty;
            DisplayName = string.Empty;
            AccessToken = string.Empty;
            LicensePlan = string.Empty;
            AuthorizedAtUtc = DateTime.MinValue;
            FeatureSet.Clear();
        }
    }
}
