using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace MepPanel.AutoCAD.Licensing
{
    public sealed class LicenseApiClient
    {
        private readonly HttpClient _httpClient;

        public LicenseApiClient(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("BaseUrl không hợp lệ.", nameof(baseUrl));
            }

            // Cho phép HTTPS self-signed khi test local.
            ServicePointManager.ServerCertificateValidationCallback =
                (sender, certificate, chain, errors) => true;

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        public void SetAccessToken(string accessToken)
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken.Trim());
            }
        }

        public Task<RequestOtpResponse> RequestOtpAsync(string phoneNumber)
        {
            return PostAsync<RequestOtpResponse>(
                "api/Auth/request-otp",
                new PhoneRequest { PhoneNumber = phoneNumber });
        }

        public Task<VerifyOtpResponse> VerifyOtpAsync(string phoneNumber, string otp)
        {
            return PostAsync<VerifyOtpResponse>(
                "api/Auth/verify-otp",
                new VerifyOtpRequestBody
                {
                    PhoneNumber = phoneNumber,
                    Otp = otp
                });
        }

        public Task<ActivateDeviceResponse> ActivateDeviceAsync(
            string phoneNumber,
            string autoCadVersion,
            string pluginVersion)
        {
            return PostAsync<ActivateDeviceResponse>(
                "api/Devices/activate",
                new ActivateDeviceRequestBody
                {
                    PhoneNumber = phoneNumber,
                    DeviceKey = DeviceIdentity.GetDeviceKey(),
                    DeviceName = DeviceIdentity.GetDeviceName(),
                    AutoCadVersion = autoCadVersion ?? string.Empty,
                    PluginVersion = pluginVersion ?? string.Empty
                });
        }

        public Task<CheckLicenseResponse> CheckLicenseAsync(
            string phoneNumber,
            string pluginVersion)
        {
            return PostAsync<CheckLicenseResponse>(
                "api/Devices/check",
                new CheckDeviceRequestBody
                {
                    PhoneNumber = phoneNumber,
                    DeviceKey = DeviceIdentity.GetDeviceKey(),
                    PluginVersion = pluginVersion ?? string.Empty
                });
        }

        private async Task<T> PostAsync<T>(string relativeUrl, object body)
        {
            string json = Serialize(body);

            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (HttpResponseMessage response = await _httpClient.PostAsync(relativeUrl, content))
            {
                string responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    string message = TryReadMessage(responseText)
                        ?? ((int)response.StatusCode + " " + response.ReasonPhrase);

                    throw new InvalidOperationException(message);
                }

                return Deserialize<T>(responseText);
            }
        }

        private static string Serialize(object value)
        {
            var serializer = new DataContractJsonSerializer(value.GetType());
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static T Deserialize<T>(string json)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json ?? "{}")))
            {
                return (T)serializer.ReadObject(stream);
            }
        }

        private static string TryReadMessage(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                var serializer = new DataContractJsonSerializer(typeof(MessageEnvelope));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    var envelope = (MessageEnvelope)serializer.ReadObject(stream);
                    return envelope != null ? envelope.Message : null;
                }
            }
            catch
            {
                return json.Length > 300 ? json.Substring(0, 300) : json;
            }
        }

        [DataContract]
        private class PhoneRequest
        {
            [DataMember(Name = "phoneNumber")]
            public string PhoneNumber { get; set; }
        }

        [DataContract]
        private class VerifyOtpRequestBody
        {
            [DataMember(Name = "phoneNumber")]
            public string PhoneNumber { get; set; }

            [DataMember(Name = "otp")]
            public string Otp { get; set; }
        }

        [DataContract]
        private class ActivateDeviceRequestBody
        {
            [DataMember(Name = "phoneNumber")]
            public string PhoneNumber { get; set; }

            [DataMember(Name = "deviceKey")]
            public string DeviceKey { get; set; }

            [DataMember(Name = "deviceName")]
            public string DeviceName { get; set; }

            [DataMember(Name = "autoCadVersion")]
            public string AutoCadVersion { get; set; }

            [DataMember(Name = "pluginVersion")]
            public string PluginVersion { get; set; }
        }

        [DataContract]
        private class CheckDeviceRequestBody
        {
            [DataMember(Name = "phoneNumber")]
            public string PhoneNumber { get; set; }

            [DataMember(Name = "deviceKey")]
            public string DeviceKey { get; set; }

            [DataMember(Name = "pluginVersion")]
            public string PluginVersion { get; set; }
        }

        [DataContract]
        private class MessageEnvelope
        {
            [DataMember(Name = "message")]
            public string Message { get; set; }
        }
    }
}
