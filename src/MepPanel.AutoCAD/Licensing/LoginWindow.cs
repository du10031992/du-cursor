using System;
using System.Windows;
using System.Windows.Controls;

namespace MepPanel.AutoCAD.Licensing
{
    public class LoginWindow : Window
    {
        private readonly LicenseApiClient _apiClient;
        private readonly string _autoCadVersion;
        private readonly string _pluginVersion;

        private readonly TextBox _phoneNumberTextBox;
        private readonly TextBox _otpTextBox;
        private readonly Button _requestOtpButton;
        private readonly Button _loginButton;
        private readonly TextBlock _statusTextBlock;

        public string AuthenticatedPhoneNumber { get; private set; }

        public CheckLicenseResponse LicenseInfo { get; private set; }

        public LoginWindow(
            LicenseApiClient apiClient,
            string autoCadVersion,
            string pluginVersion)
        {
            if (apiClient == null)
            {
                throw new ArgumentNullException(nameof(apiClient));
            }

            _apiClient = apiClient;
            _autoCadVersion = autoCadVersion ?? string.Empty;
            _pluginVersion = pluginVersion ?? string.Empty;

            Title = "Đăng nhập giấy phép";
            Width = 420;
            Height = 320;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;

            var root = new Grid { Margin = new Thickness(20) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var phoneLabel = new TextBlock
            {
                Text = "Số điện thoại (do Admin cấp)",
                Margin = new Thickness(0, 0, 0, 6)
            };
            Grid.SetRow(phoneLabel, 0);
            root.Children.Add(phoneLabel);

            _phoneNumberTextBox = new TextBox
            {
                Height = 28,
                Margin = new Thickness(0, 0, 0, 12)
            };
            Grid.SetRow(_phoneNumberTextBox, 1);
            root.Children.Add(_phoneNumberTextBox);

            var otpLabel = new TextBlock
            {
                Text = "Mã OTP",
                Margin = new Thickness(0, 0, 0, 6)
            };
            Grid.SetRow(otpLabel, 2);
            root.Children.Add(otpLabel);

            var otpRow = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            _requestOtpButton = new Button
            {
                Content = "Gửi mã OTP",
                Width = 110,
                Height = 28,
                Margin = new Thickness(8, 0, 0, 0)
            };
            DockPanel.SetDock(_requestOtpButton, Dock.Right);
            _requestOtpButton.Click += RequestOtpButton_Click;

            _otpTextBox = new TextBox { Height = 28 };
            otpRow.Children.Add(_requestOtpButton);
            otpRow.Children.Add(_otpTextBox);
            Grid.SetRow(otpRow, 3);
            root.Children.Add(otpRow);

            var bottom = new DockPanel { LastChildFill = true };
            _loginButton = new Button
            {
                Content = "Đăng nhập",
                Height = 34
            };
            DockPanel.SetDock(_loginButton, Dock.Bottom);
            _loginButton.Click += LoginButton_Click;

            _statusTextBlock = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 0, 12),
                Foreground = System.Windows.Media.Brushes.DimGray
            };

            bottom.Children.Add(_loginButton);
            bottom.Children.Add(_statusTextBlock);
            Grid.SetRow(bottom, 4);
            root.Children.Add(bottom);

            Content = root;
        }

        private async void RequestOtpButton_Click(object sender, RoutedEventArgs e)
        {
            string phoneNumber = _phoneNumberTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                MessageBox.Show("Vui lòng nhập số điện thoại.", "MepPanel");
                return;
            }

            try
            {
                SetBusy(true);
                _statusTextBlock.Text = "Đang gửi mã OTP...";

                RequestOtpResponse response =
                    await _apiClient.RequestOtpAsync(phoneNumber);

                _statusTextBlock.Text = response != null
                    ? response.Message
                    : "Đã gửi yêu cầu OTP.";

                if (response != null && !string.IsNullOrWhiteSpace(response.TestOtp))
                {
                    _otpTextBox.Text = response.TestOtp;
                    _statusTextBlock.Text +=
                        Environment.NewLine + "OTP thử nghiệm: " + response.TestOtp;
                }
            }
            catch (Exception ex)
            {
                _statusTextBlock.Text = "Không gửi được OTP.";
                MessageBox.Show(ex.Message, "Lỗi gửi OTP");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string phoneNumber = _phoneNumberTextBox.Text.Trim();
            string otp = _otpTextBox.Text.Trim();
            LicenseSession.Clear();

            if (string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(otp))
            {
                MessageBox.Show("Vui lòng nhập số điện thoại và OTP.", "MepPanel");
                return;
            }

            try
            {
                SetBusy(true);
                _statusTextBlock.Text = "Đang xác thực OTP...";

                VerifyOtpResponse verifyResponse =
                    await _apiClient.VerifyOtpAsync(phoneNumber, otp);

                if (verifyResponse == null || !verifyResponse.Authenticated ||
                    string.IsNullOrWhiteSpace(verifyResponse.AccessToken))
                {
                    _statusTextBlock.Text = verifyResponse != null &&
                        !string.IsNullOrWhiteSpace(verifyResponse.Message)
                            ? verifyResponse.Message
                            : "Mã OTP không hợp lệ.";
                    return;
                }

                _apiClient.SetAccessToken(verifyResponse.AccessToken);
                _statusTextBlock.Text = "Đang kích hoạt thiết bị...";

                ActivateDeviceResponse activateResponse =
                    await _apiClient.ActivateDeviceAsync(
                        phoneNumber,
                        _autoCadVersion,
                        _pluginVersion);

                if (activateResponse == null || !activateResponse.Activated)
                {
                    _statusTextBlock.Text = activateResponse != null &&
                        !string.IsNullOrWhiteSpace(activateResponse.Message)
                            ? activateResponse.Message
                            : "Không kích hoạt được thiết bị. Có thể Admin chưa mở chuyển máy.";
                    return;
                }

                _statusTextBlock.Text = "Đang kiểm tra giấy phép...";

                CheckLicenseResponse checkResponse =
                    await _apiClient.CheckLicenseAsync(phoneNumber, _pluginVersion);

                if (checkResponse == null || !checkResponse.Valid)
                {
                    _statusTextBlock.Text = checkResponse != null &&
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

                _statusTextBlock.Text = "Đăng nhập thành công.";
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                LicenseSession.Clear();
                _statusTextBlock.Text = "Đăng nhập không thành công.";
                MessageBox.Show(ex.Message, "Lỗi đăng nhập");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool isBusy)
        {
            _phoneNumberTextBox.IsEnabled = !isBusy;
            _otpTextBox.IsEnabled = !isBusy;
            _requestOtpButton.IsEnabled = !isBusy;
            _loginButton.IsEnabled = !isBusy;
        }
    }
}
