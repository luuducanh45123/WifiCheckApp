using System;
using System.Collections.Generic;

namespace WifiCheckApp_API.Models;

public partial class Location
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public string? Description { get; set; }
}
