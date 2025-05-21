using System;
using System.Collections.Generic;

namespace WifiCheckApp_API.Models;

public partial class Attendance
{
    public int AttendanceId { get; set; }

    public int EmployeeId { get; set; }

    public int? DeviceId { get; set; }

    public int ShiftId { get; set; }

    public DateTime CheckIn { get; set; }

    public DateTime? CheckOut { get; set; }

    public DateOnly WorkDate { get; set; }

    public string? Notes { get; set; }

    public virtual Device? Device { get; set; }

    public virtual Employee Employee { get; set; } = null!;
}
