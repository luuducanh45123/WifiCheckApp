using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#if ANDROID
using Android.Net.Wifi;
using Android.Content;
using Android.App;
#endif

namespace WifiCheckApp
{
    public class WifiService
    {
        private readonly IConnectivity _connectivity;

        public WifiService(IConnectivity connectivity)
        {
            _connectivity = connectivity;
        }

        // Trả về MAC address (string) nếu kết nối đúng wifi, ngược lại trả về null hoặc string.Empty
        public async Task<string> GetMacAddressAsync(string targetSsid, string targetGateway)
        {
            if (_connectivity.NetworkAccess != NetworkAccess.Internet)
            {
                return null;
            }

#if ANDROID
            return await GetMacAddressAndroidAsync(targetSsid, targetGateway);
#elif WINDOWS
            return await GetMacAddressWindowsAsync(targetSsid, targetGateway);
#else
            return null;
#endif
        }

#if ANDROID
        private async Task<string> GetMacAddressAndroidAsync(string targetSsid, string targetGateway)
        {
            try
            {
                var allowedBSSIDs = new List<string>
                {
                    "30:4f:75:39:cb:d1",
                    "30:4f:75:39:cb:d0"
                }.Select(b => b.ToLower()).ToList();

                var wifiManager = Android.App.Application.Context.GetSystemService(Context.WifiService) as WifiManager;
                if (wifiManager != null && wifiManager.ConnectionInfo != null)
                {
                    string currentSsid = wifiManager.ConnectionInfo.SSID?.Replace("\"", "");
                    string bssid = wifiManager.ConnectionInfo.BSSID?.ToLower();

                    if (currentSsid != targetSsid)
                    {
                        return null;
                    }

                    if (!allowedBSSIDs.Contains(bssid))
                    {
                        return null;
                    }

                    string gatewayIp = GetGatewayIP();
                    if (gatewayIp != targetGateway)
                    {
                        return null;
                    }

                    // Trả về BSSID làm MAC address
                    return bssid;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting MAC on Android: {ex.Message}");
            }

            return null;
        }

        private string GetGatewayIP()
        {
            try
            {
                var wifiManager = (WifiManager)Android.App.Application.Context.GetSystemService(Context.WifiService);
                var dhcpInfo = wifiManager?.DhcpInfo;

                if (dhcpInfo != null)
                {
                    int gateway = dhcpInfo.Gateway;
                    return Android.Text.Format.Formatter.FormatIpAddress(gateway);
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
#endif

#if WINDOWS
        private async Task<string> GetMacAddressWindowsAsync(string targetSsid, string targetGateway)
        {
            try
            {
                // Kiểm tra wifi name
                var wifiInfo = await RunCommandAsync("netsh wlan show interfaces");
                if (!wifiInfo.Contains(targetSsid))
                {
                    return null;
                }

                // Kiểm tra gateway
                var gatewayInfo = await RunCommandAsync("ipconfig");
                if (!gatewayInfo.Contains(targetGateway))
                {
                    return null;
                }

                // Lấy MAC address từ netsh
                // Ví dụ tìm dòng chứa "Physical address" hoặc "BSSID"
                var lines = wifiInfo.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Trim().StartsWith("Physical address", StringComparison.OrdinalIgnoreCase) ||
                        line.Trim().StartsWith("BSSID", StringComparison.OrdinalIgnoreCase))
                    {
                        // Dòng dạng: Physical address    : xx-xx-xx-xx-xx-xx
                        var parts = line.Split(':');
                        if (parts.Length == 2)
                        {
                            var mac = parts[1].Trim().ToLower().Replace("-", ":");
                            return mac;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting MAC on Windows: {ex.Message}");
            }

            return null;
        }

        private async Task<string> RunCommandAsync(string command)
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.Arguments = $"/c {command}";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return output;
        }
#endif
    }
}
