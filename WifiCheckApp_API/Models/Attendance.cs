using System;
using System.Collections.Generic;

namespace WifiCheckApp_API.Models;

public partial class Attendance
{
    public int AttendanceId { get; set; }

    public int EmployeeId { get; set; }

    public int? DeviceId { get; set; }

    public int? ShiftId { get; set; }

    public DateTime CheckIn { get; set; }

    public DateTime? CheckOut { get; set; }

    public DateOnly WorkDate { get; set; }

    public string? Notes { get; set; }

    public DateTime? MorningCheckIn { get; set; }

    public DateTime? MorningCheckOut { get; set; }

    public DateTime? AfternoonCheckIn { get; set; }

    public DateTime? AfternoonCheckOut { get; set; }

    public virtual ICollection<AttendanceHistory> AttendanceHistories { get; set; } = new List<AttendanceHistory>();

    public virtual Device? Device { get; set; }

    public virtual Employee Employee { get; set; } = null!;
}
