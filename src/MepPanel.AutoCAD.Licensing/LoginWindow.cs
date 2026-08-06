using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MepPanel.AutoCAD.Licensing
{
    public class LoginWindow : Form
    {
        private readonly LicenseApiClient _apiClient;
        private readonly string _autoCadVersion;
        private readonly string _pluginVersion;

        private readonly TextBox _phoneNumberTextBox;
        private readonly TextBox _otpTextBox;
        private readonly Button _requestOtpButton;
        private readonly Button _loginButton;
        private readonly Label _statusLabel;

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

            Text = "Đăng nhập giấy phép";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(380, 260);

            var phoneLabel = new Label
            {
                Text = "Số điện thoại (do Admin cấp)",
                Location = new Point(16, 16),
                AutoSize = true
            };

            _phoneNumberTextBox = new TextBox
            {
                Location = new Point(16, 36),
                Width = 348
            };

            var otpLabel = new Label
            {
                Text = "Mã OTP",
                Location = new Point(16, 72),
                AutoSize = true
            };

            _otpTextBox = new TextBox
            {
                Location = new Point(16, 92),
                Width = 220
            };

            _requestOtpButton = new Button
            {
                Text = "Gửi mã OTP",
                Location = new Point(246, 90),
                Width = 118,
                Height = 26
            };
            _requestOtpButton.Click += RequestOtpButton_Click;

            _statusLabel = new Label
            {
                Location = new Point(16, 128),
                Size = new Size(348, 56),
                AutoEllipsis = false
            };

            _loginButton = new Button
            {
                Text = "Đăng nhập",
                Location = new Point(16, 192),
                Width = 348,
                Height = 32
            };
            _loginButton.Click += LoginButton_Click;

            Controls.Add(phoneLabel);
            Controls.Add(_phoneNumberTextBox);
            Controls.Add(otpLabel);
            Controls.Add(_otpTextBox);
            Controls.Add(_requestOtpButton);
            Controls.Add(_statusLabel);
            Controls.Add(_loginButton);
        }

        private void RequestOtpButton_Click(object sender, EventArgs e)
        {
            string phoneNumber = _phoneNumberTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                MessageBox.Show("Vui lòng nhập số điện thoại.", "MepPanel");
                return;
            }

            SetBusy(true);
            _statusLabel.Text = "Đang gửi mã OTP...";

            try
            {
                RequestOtpResponse response = AsyncRunner.Run(
                    () => _apiClient.RequestOtpAsync(phoneNumber));

                _statusLabel.Text = response != null
                    ? response.Message
                    : "Đã gửi yêu cầu OTP.";

                if (response != null && !string.IsNullOrWhiteSpace(response.TestOtp))
                {
                    _otpTextBox.Text = response.TestOtp;
                    _statusLabel.Text += Environment.NewLine + "OTP thử nghiệm: " + response.TestOtp;
                }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Không gửi được OTP.";
                MessageBox.Show(ex.Message, "Lỗi gửi OTP");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void LoginButton_Click(object sender, EventArgs e)
        {
            string phoneNumber = _phoneNumberTextBox.Text.Trim();
            string otp = _otpTextBox.Text.Trim();
            LicenseSession.Clear();

            if (string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(otp))
            {
                MessageBox.Show("Vui lòng nhập số điện thoại và OTP.", "MepPanel");
                return;
            }

            SetBusy(true);
            _statusLabel.Text = "Đang đăng nhập...";

            try
            {
                AsyncRunner.Run(() => CompleteLoginAsync(phoneNumber, otp));
                _statusLabel.Text = "Đăng nhập thành công.";
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                LicenseSession.Clear();
                _statusLabel.Text = "Đăng nhập không thành công.";
                MessageBox.Show(ex.Message, "Lỗi đăng nhập");
                SetBusy(false);
            }
        }

        private async Task CompleteLoginAsync(string phoneNumber, string otp)
        {
            VerifyOtpResponse verifyResponse =
                await _apiClient.VerifyOtpAsync(phoneNumber, otp).ConfigureAwait(false);

            if (verifyResponse == null || !verifyResponse.Authenticated ||
                string.IsNullOrWhiteSpace(verifyResponse.AccessToken))
            {
                throw new InvalidOperationException(
                    verifyResponse != null && !string.IsNullOrWhiteSpace(verifyResponse.Message)
                        ? verifyResponse.Message
                        : "Mã OTP không hợp lệ.");
            }

            _apiClient.SetAccessToken(verifyResponse.AccessToken);

            ActivateDeviceResponse activateResponse =
                await _apiClient.ActivateDeviceAsync(
                    phoneNumber,
                    _autoCadVersion,
                    _pluginVersion).ConfigureAwait(false);

            if (activateResponse == null || !activateResponse.Activated)
            {
                throw new InvalidOperationException(
                    activateResponse != null && !string.IsNullOrWhiteSpace(activateResponse.Message)
                        ? activateResponse.Message
                        : "Không kích hoạt được thiết bị. Có thể Admin chưa mở chuyển máy.");
            }

            CheckLicenseResponse checkResponse =
                await _apiClient.CheckLicenseAsync(phoneNumber, _pluginVersion)
                    .ConfigureAwait(false);

            if (checkResponse == null || !checkResponse.Valid)
            {
                throw new InvalidOperationException(
                    checkResponse != null && !string.IsNullOrWhiteSpace(checkResponse.Message)
                        ? checkResponse.Message
                        : "Giấy phép không hợp lệ.");
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
        }

        private void SetBusy(bool isBusy)
        {
            _phoneNumberTextBox.Enabled = !isBusy;
            _otpTextBox.Enabled = !isBusy;
            _requestOtpButton.Enabled = !isBusy;
            _loginButton.Enabled = !isBusy;
            UseWaitCursor = isBusy;
        }
    }
}
