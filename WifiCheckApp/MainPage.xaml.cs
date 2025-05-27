using System.Text;

namespace WifiCheckApp
{
    public partial class MainPage : ContentPage
    {
        private readonly string _targetWifiName = "THE";
        private readonly string _targetGateway = "192.168.1.1";
        private readonly IConnectivity _connectivity;
        private readonly WifiService _wifiService;
        private readonly EmailService _emailService;
        private Timer _wifiCheckTimer;

        public MainPage(IConnectivity connectivity, WifiService wifiService, EmailService emailService)
        {
            InitializeComponent();
            _connectivity = connectivity;
            _wifiService = wifiService;
            _emailService = emailService;
            RefreshPanel.IsVisible = true;

            // Start a timer to check WiFi status periodically
            _wifiCheckTimer = new Timer(CheckWifiStatus, null, 0, 5000); // Check every 5 seconds

            // Check if we have a saved email
            CheckSavedEmail();

            // Show current time
            Device.StartTimer(TimeSpan.FromSeconds(1), () =>
            {
                TimeLabel.Text = DateTime.Now.ToString("HH:mm:ss");
                return true;
            });
        }

        private void CheckWifiStatus(object state)
        {
            // Need to run on UI thread
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                string deviceMac = await _wifiService.GetMacAddressAsync(_targetWifiName, _targetGateway);

                bool isConnected = !string.IsNullOrEmpty(deviceMac);

                if (isConnected)
                    // If connected to target WiFi, check if we have email
                {
                    string savedEmail = await _emailService.GetSavedEmail();

                    RefreshPanel.IsVisible = false;
                    WifiStatusLabel.Text = $"Đã kết nối đến wifi công ty";
                    WifiStatusLabel.TextColor = Colors.Green;

                    // Make sure EmailLabel is visible and displays the saved email
                    EmailLabel.IsVisible = true;
                    EmailLabel.Text = savedEmail ?? string.Empty;

                    if (string.IsNullOrEmpty(savedEmail))
                    {
                        // Show email input if no email is saved
                        EmailFrame.IsVisible = true;
                        ButtonsPanel.IsVisible = false;
                    }
                    else
                    {
                        // Show buttons if we have the email
                        EmailFrame.IsVisible = false;
                        ButtonsPanel.IsVisible = true;
                    }
                }
                else
                {
                    // Always make sure RefreshPanel is visible when not connected
                    RefreshPanel.IsVisible = true;

                    // Keep EmailLabel visible if it contains text
                    if (!string.IsNullOrEmpty(EmailLabel.Text))
                    {
                        EmailLabel.IsVisible = true;
                    }
                    else
                    {
                        EmailLabel.IsVisible = false;
                    }

                    WifiStatusLabel.Text = $"Không kết nối đến wifi công ty";
                    WifiStatusLabel.TextColor = Colors.Red;

                    // Don't hide EmailFrame if user is currently entering email
                    // But do hide ButtonsPanel since actions require WiFi
                    ButtonsPanel.IsVisible = false;

                    // Show notification
                    //await ShowWifiNotification();
                }
            });
        }

        private async void CheckSavedEmail()
        {
            string savedEmail = await _emailService.GetSavedEmail();
            if (!string.IsNullOrEmpty(savedEmail))
            {
                EmailEntry.Text = savedEmail;
                EmailLabel.Text = savedEmail;
                EmailLabel.IsVisible = true;
            }
        }

        private async Task ShowWifiNotification()
        {
            const string key = "NotificationShown";

            if (!Preferences.ContainsKey(key) || !Preferences.Get(key, false))
            {
                await App.Current.MainPage.DisplayAlert(
                    "Cảnh báo kết nối",
                    $"Vui lòng kết nối đến mạng WiFi {_targetWifiName}",
                    "OK");

                Preferences.Set(key, true);
            }
        }

        private async void OnSaveEmailClicked(object sender, EventArgs e)
        {
            string email = EmailEntry.Text?.Trim();

            if (string.IsNullOrEmpty(email) || !IsValidEmail(email))
            {
                await DisplayAlert("Lỗi", "Vui lòng nhập email hợp lệ", "OK");
                return;
            }

            await _emailService.SaveEmail(email);

            // Update UI - make sure to update EmailLabel with the new email
            EmailLabel.Text = email;
            EmailLabel.IsVisible = true;
            EmailFrame.IsVisible = false;
            ButtonsPanel.IsVisible = true;
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private async void OnCheckInClicked(object sender, EventArgs e)
        {
            try
            {
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                };

                string email = EmailLabel.Text?.Trim();

                if (string.IsNullOrEmpty(email))
                {
                    await DisplayAlert("Lỗi", "Email không được để trống.", "OK");
                    return;
                }

                // ✅ Kiểm tra email có tồn tại trong database không
                var verifyUrl = $"https://attendanceapihost.azurewebsites.net/api/TimeSkip/verify-email?email={email}";
                var verifyHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                };

                using (var verifyClient = new HttpClient(verifyHandler))
                {
                    try
                    {
                        var verifyResponse = await verifyClient.GetAsync(verifyUrl);
                        if (!verifyResponse.IsSuccessStatusCode)
                        {
                            await DisplayAlert("Lỗi", "Email không tồn tại trong hệ thống, không thể check-in.", "OK");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        await DisplayAlert("Lỗi", $"Không thể xác minh email: {ex.Message}", "OK");
                        return;
                    }
                }


                DateTime now = DateTime.Now;
                string todayKey = $"CheckInDone_{now:yyyyMMdd}";

                // Kiểm tra xem hôm nay đã check-in chưa (lưu trong Preferences)
                bool hasCheckedInToday = Preferences.Get(todayKey, false);

                if (hasCheckedInToday)
                {
                    await DisplayAlert("Thông báo", "Bạn đã check-in trong ngày hôm nay, không thể check-in lần thứ 2.", "OK");
                    return;
                }

                
                string deviceMac = await _wifiService.GetMacAddressAsync(_targetWifiName, _targetGateway);

                if (string.IsNullOrEmpty(email))
                {
                    await DisplayAlert("Lỗi", "Email không được để trống.", "OK");
                    return;
                }
                if (string.IsNullOrEmpty(deviceMac))
                {
                    await DisplayAlert("Lỗi", "Không lấy được MAC thiết bị, vui lòng kết nối WiFi công ty.", "OK");
                    return;
                }

                var checkinData = new
                {
                    Email = email,
                    DeviceMac = deviceMac,
                    CheckIn = now,
                    Notes = "CheckIn"
                };

                var json = System.Text.Json.JsonSerializer.Serialize(checkinData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                string apiUrl = "https://attendanceapihost.azurewebsites.net/api/TimeSkip/checkin";

                using (var httpClient = new HttpClient(handler))
                {
                    var response = await httpClient.PostAsync(apiUrl, content);

                    if (response.IsSuccessStatusCode)
                    {
                        // Lưu lại trạng thái đã check-in ngày hôm nay
                        Preferences.Set(todayKey, true);

                        string timestamp = now.ToString("HH:mm:ss dd/MM/yyyy");
                        await DisplayAlert("Thành công", $"Check-in thành công lúc: {timestamp}", "OK");
                    }
                    else
                    {
                        string error = await response.Content.ReadAsStringAsync();
                        await DisplayAlert("Lỗi", $"Không thể check-in: {error}", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", $"Có lỗi xảy ra: {ex.Message}", "OK");
            }
        }
        private async void OnCheckOutClicked(object sender, EventArgs e)

        {
            try
            {
                DateTime now = DateTime.Now;
                string todayKeyCheckIn = $"CheckInDone_{now:yyyyMMdd}";
                string todayKeyCheckOut = $"LastCheckOut_{now:yyyyMMdd}";

                // Kiểm tra đã checkin trong ngày chưa
                bool hasCheckedInToday = Preferences.Get(todayKeyCheckIn, false);
                if (!hasCheckedInToday)
                {
                    await DisplayAlert("Lỗi", "Bạn chưa check-in trong ngày hôm nay, vui lòng check-in trước khi checkout.", "OK");
                    return;
                }

                // Kiểm tra thời gian hiện tại, không cho checkout sau 23:59:59
                if (now.TimeOfDay > new TimeSpan(23, 59, 59))
                {
                    await DisplayAlert("Lỗi", "Đã quá giờ checkout trong ngày. Vui lòng check-in lại vào ngày mai.", "OK");
                    return;
                }

                string email = EmailLabel.Text?.Trim();
                string deviceMac = await _wifiService.GetMacAddressAsync(_targetWifiName, _targetGateway);

                if (string.IsNullOrEmpty(email))
                {
                    await DisplayAlert("Lỗi", "Email không được để trống.", "OK");
                    return;
                }
                if (string.IsNullOrEmpty(deviceMac))
                {
                    await DisplayAlert("Lỗi", "Không lấy được MAC thiết bị, vui lòng kết nối WiFi công ty.", "OK");
                    return;
                }

                var checkoutData = new
                {
                    Email = email,
                    DeviceMac = deviceMac,
                    CheckOut = now,
                    Notes = "CheckOut"
                };

                var json = System.Text.Json.JsonSerializer.Serialize(checkoutData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                string apiUrl = "https://attendanceapihost.azurewebsites.net/api/TimeSkip/checkout";

                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                };

                using (var httpClient = new HttpClient(handler))
                {
                    var response = await httpClient.PostAsync(apiUrl, content);

                    if (response.IsSuccessStatusCode)
                    {
                        // Lưu lại thời gian checkout cuối cùng trong ngày
                        Preferences.Set(todayKeyCheckOut, now.ToString("HH:mm:ss"));

                        string timestamp = now.ToString("HH:mm:ss dd/MM/yyyy");
                        await DisplayAlert("Logout", $"Checkout thành công lúc: {timestamp}", "OK");
                    }
                    else
                    {
                        string error = await response.Content.ReadAsStringAsync();
                        await DisplayAlert("Lỗi", $"Không thể checkout: {error}", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", $"Có lỗi xảy ra: {ex.Message}", "OK");
            }
        }


        private void OnRefreshClicked(object sender, EventArgs e)
        {
            CheckWifiStatus(null);
        }

        private async Task RequestLocationPermissionAsync()
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

            if (status != PermissionStatus.Granted)
            {
                var result = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

                //if (result == PermissionStatus.Granted)
                //{
                //    await DisplayAlert("Thành công", "Đã cấp quyền truy cập vị trí.", "OK");
                //}
                //else
                //{
                //    await DisplayAlert("Từ chối", "Bạn chưa cấp quyền truy cập vị trí.", "OK");
                //}
            }
        }

        protected override async void OnAppearing()
        {
            try
            {
                base.OnAppearing();
                await RequestLocationPermissionAsync();
            }
            catch (Exception e)
            {
                throw; // TODO handle exception
            }
        }
    }
}