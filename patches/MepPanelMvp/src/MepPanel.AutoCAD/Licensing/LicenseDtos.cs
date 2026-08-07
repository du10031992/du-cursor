using System.Collections.Generic;
using System.Runtime.Serialization;

namespace MepPanel.AutoCAD.Licensing
{
    [DataContract]
    public class RequestOtpResponse
    {
        [DataMember(Name = "message")]
        public string Message { get; set; }

        [DataMember(Name = "testOtp")]
        public string TestOtp { get; set; }
    }

    [DataContract]
    public class VerifyOtpResponse
    {
        [DataMember(Name = "authenticated")]
        public bool Authenticated { get; set; }

        [DataMember(Name = "accessToken")]
        public string AccessToken { get; set; }

        [DataMember(Name = "accessTokenExpiresAtUtc")]
        public string AccessTokenExpiresAtUtc { get; set; }

        [DataMember(Name = "displayName")]
        public string DisplayName { get; set; }

        [DataMember(Name = "phoneNumber")]
        public string PhoneNumber { get; set; }

        [DataMember(Name = "message")]
        public string Message { get; set; }
    }

    [DataContract]
    public class ActivateDeviceResponse
    {
        [DataMember(Name = "activated")]
        public bool Activated { get; set; }

        [DataMember(Name = "message")]
        public string Message { get; set; }

        [DataMember(Name = "deviceId")]
        public int DeviceId { get; set; }

        [DataMember(Name = "features")]
        public List<string> Features { get; set; }
    }

    [DataContract]
    public partial class CheckLicenseResponse
    {
        [DataMember(Name = "valid")]
        public bool Valid { get; set; }

        [DataMember(Name = "message")]
        public string Message { get; set; }

        [DataMember(Name = "displayName")]
        public string DisplayName { get; set; }

        [DataMember(Name = "phoneNumber")]
        public string PhoneNumber { get; set; }

        [DataMember(Name = "licensePlan")]
        public string LicensePlan { get; set; }

        [DataMember(Name = "licenseExpiresAtUtc")]
        public string LicenseExpiresAtUtc { get; set; }

        [DataMember(Name = "offlineUntilUtc")]
        public string OfflineUntilUtc { get; set; }

        // Features nam o CheckLicenseResponse.Features.cs (partial)
    }
}
