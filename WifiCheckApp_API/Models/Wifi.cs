using System;
using System.Collections.Generic;

namespace WifiCheckApp_API.Models;

public partial class Wifi
{
    public int Id { get; set; }

    public string Ssid { get; set; } = null!;

    public string? Bssid { get; set; }
}
