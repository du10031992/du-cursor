using System;
using System.Windows;

namespace MepPanel.AutoCAD.Licensing
{
    public partial class LoginWindow : Window
    {
        private readonly LicenseApiClient _apiClient;
        private readonly string _autoCadVersion;
        private readonly string _pluginVersion;

        public string AuthenticatedPhoneNumber { get; private set; }

        public CheckLicenseResponse LicenseInfo { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
        }

        public LoginWindow(
            LicenseApiClient apiClient,
            string autoCadVersion,
            string pluginVersion)
            : this()
        {
            if (apiClient == null)
            {
                throw new ArgumentNullException(nameof(apiClient));
            }

            _apiClient = apiClient;
            _autoCadVersion = autoCadVersion ?? string.Empty;
            _pluginVersion = pluginVersion ?? string.Empty;
        }

        private async void RequestOtpButton_Click(object sender, RoutedEventArgs e)
        {
            string phoneNumber = PhoneNumberTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                MessageBox.Show("Vui lòng nhập số điện thoại.", "MepPanel");
                return;
            }

            try
            {
                SetBusy(true);
                StatusTextBlock.Text = "Đang gửi mã OTP...";

                RequestOtpResponse response =
                    await _apiClient.RequestOtpAsync(phoneNumber);

                StatusTextBlock.Text = response != null
                    ? response.Message
                    : "Đã gửi yêu cầu OTP.";

                if (response != null && !string.IsNullOrWhiteSpace(response.TestOtp))
                {
                    OtpTextBox.Text = response.TestOtp;
                    StatusTextBlock.Text +=
                        Environment.NewLine + "OTP thử nghiệm: " + response.TestOtp;
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Không gửi được OTP.";
                MessageBox.Show(ex.Message, "Lỗi gửi OTP");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string phoneNumber = PhoneNumberTextBox.Text.Trim();
            string otp = OtpTextBox.Text.Trim();
            LicenseSession.Clear();

            if (string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(otp))
            {
                MessageBox.Show("Vui lòng nhập số điện thoại và OTP.", "MepPanel");
                return;
            }

            try
            {
                SetBusy(true);
                StatusTextBlock.Text = "Đang xác thực OTP...";

                VerifyOtpResponse verifyResponse =
                    await _apiClient.VerifyOtpAsync(phoneNumber, otp);

                if (verifyResponse == null || !verifyResponse.Authenticated ||
                    string.IsNullOrWhiteSpace(verifyResponse.AccessToken))
                {
                    StatusTextBlock.Text = verifyResponse != null &&
                        !string.IsNullOrWhiteSpace(verifyResponse.Message)
                            ? verifyResponse.Message
                            : "Mã OTP không hợp lệ.";
                    return;
                }

                _apiClient.SetAccessToken(verifyResponse.AccessToken);
                StatusTextBlock.Text = "Đang kích hoạt thiết bị...";

                ActivateDeviceResponse activateResponse =
                    await _apiClient.ActivateDeviceAsync(
                        phoneNumber,
                        _autoCadVersion,
                        _pluginVersion);

                if (activateResponse == null || !activateResponse.Activated)
                {
                    StatusTextBlock.Text = activateResponse != null &&
                        !string.IsNullOrWhiteSpace(activateResponse.Message)
                            ? activateResponse.Message
                            : "Không kích hoạt được thiết bị. Có thể Admin chưa mở chuyển máy.";
                    return;
                }

                StatusTextBlock.Text = "Đang kiểm tra giấy phép...";

                CheckLicenseResponse checkResponse =
                    await _apiClient.CheckLicenseAsync(phoneNumber, _pluginVersion);

                if (checkResponse == null || !checkResponse.Valid)
                {
                    StatusTextBlock.Text = checkResponse != null &&
                        !string.IsNullOrWhiteSpace(checkResponse.Message)
                            ? checkResponse.Message
                            : "Giấy phép không hợp lệ.";
                    return;
                }

                LicenseCache.Save(phoneNumber, checkResponse);
                AuthenticatedPhoneNumber = phoneNumber;
                LicenseInfo = checkResponse;

                LicenseSession.Authorize(
                    phoneNumber,
                    checkResponse.DisplayName,
                    verifyResponse.AccessToken,
                    checkResponse.Features,
                    checkResponse.LicensePlan);

                StatusTextBlock.Text = "Đăng nhập thành công.";
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                LicenseSession.Clear();
                StatusTextBlock.Text = "Đăng nhập không thành công.";
                MessageBox.Show(ex.Message, "Lỗi đăng nhập");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool isBusy)
        {
            PhoneNumberTextBox.IsEnabled = !isBusy;
            OtpTextBox.IsEnabled = !isBusy;
            RequestOtpButton.IsEnabled = !isBusy;
            LoginButton.IsEnabled = !isBusy;
        }
    }
}
