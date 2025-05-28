using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WifiCheckApp_API.Models;
using WifiCheckApp_API.ViewModels;

namespace WifiCheckApp_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TimeSkipController : ControllerBase
    {
        private readonly ILogger<TimeSkipController> _logger;
        private readonly TimeLapsContext _context;

        public TimeSkipController(ILogger<TimeSkipController> logger, TimeLapsContext context)
        {
            _logger = logger;
            _context = context;
        }

        [HttpPost("checkin")]
        public async Task<IActionResult> CheckIn([FromBody] CheckinModel dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Email == dto.Email && e.IsActive == true);

            if (employee == null)
                return NotFound($"Không tìm thấy nhân viên với email {dto.Email} hoặc đã bị vô hiệu hóa.");

            var checkInTime = GetDateTimeLocal(dto.CheckIn);
            var today = DateOnly.FromDateTime(checkInTime);

            // Tự động xác định session theo giờ
            int sessionId;
            if (dto.TypeCheck == 1)
                sessionId = 1; // Sáng
            else
                sessionId = 2; // Chiều

            // Kiểm tra xem đã check-in cho session này trong ngày hưa chưa
            var existingCheckin = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EmployeeId == employee.EmployeeId
                                        && a.WorkDate == today
                                        && a.SessionId == sessionId
                                        && a.CheckInTime != null);

            if (existingCheckin != null)
            {
                return BadRequest($"Nhân viên đã check-in cho ca {(sessionId == 1 ? "sáng" : "chiều")} hôm nay lúc {existingCheckin.CheckInTime:HH:mm}.");
            }

            var attendance = new Attendance
            {
                EmployeeId = employee.EmployeeId,
                WorkDate = today,
                SessionId = sessionId,
                CheckInTime = checkInTime,
                Notes = dto.Notes,
                WiFiId = dto.WifiId,
                GpsId = dto.GpsId,
                CheckInStatus = dto.CheckInStatus,
                LateMinutes = dto.LateMinute,
            };

            _context.Attendances.Add(attendance);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Check-in thành công.",
                ca = sessionId == 1 ? "Sáng" : "Chiều",
                checkInTime = checkInTime,
                attendanceId = attendance.AttendanceId
            });
        }

        [HttpPost("checkout")]
        public async Task<IActionResult> CheckOut([FromBody] CheckOutModel dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Email == dto.Email && e.IsActive == true);

            if (employee == null)
                return NotFound($"Employee with email {dto.Email} not found or inactive.");

            var today = DateOnly.FromDateTime(GetDateTimeLocal(DateTime.Now));

            int sessionId;
            if (dto.TypeCheck == 1)
                sessionId = 1; // Sáng
            else
                sessionId = 2; // Chiều

            // Kiểm tra xem đã có bản ghi check-in cho ngày hôm nay và session này chưa
            var existingAttendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EmployeeId == employee.EmployeeId
                                        && a.WorkDate == today
                                        && a.SessionId == sessionId);

            if (existingAttendance == null)
            {
                return BadRequest($"Nhân viên chưa check-in cho ca {(sessionId == 1 ? "sáng" : "chiều")} hôm nay. Vui lòng check-in trước khi check-out.");
            }

            // Kiểm tra xem đã check-out chưa
            if (existingAttendance.CheckOutTime != null)
            {
                return BadRequest($"Nhân viên đã check-out cho ca {(sessionId == 1 ? "sáng" : "chiều")} hôm nay lúc {existingAttendance.CheckOutTime:HH:mm}.");
            }

            // Update bản ghi hiện có với thông tin check-out
            existingAttendance.CheckOutTime = GetDateTimeLocal(dto.CheckOut);
            existingAttendance.Notes = dto.Notes;
            existingAttendance.WiFiId = dto.WifiId;
            existingAttendance.GpsId = dto.GpsId;
            existingAttendance.CheckOutStatus = dto.checkOutStatus;
            existingAttendance.EarlyCheckOutMinutes = dto.EarlyCheckOutMinutes;

            _context.Attendances.Update(existingAttendance);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Check-out thành công.",
                attendanceId = existingAttendance.AttendanceId,
                checkInTime = existingAttendance.CheckInTime,
                checkOutTime = existingAttendance.CheckOutTime
            });
        }

        [HttpGet("attendance/monthly")]
        public async Task<IActionResult> GetMonthlyAttendance(int employeeId, int month, int year)
        {
            var totalDays = DateTime.DaysInMonth(year, month);
            var startDate = new DateOnly(year, month, 1);
            var endDate = new DateOnly(year, month, totalDays);
            var today = DateOnly.FromDateTime(GetDateTimeLocal(DateTime.Now));

            // Lấy toàn bộ dữ liệu chấm công trong tháng
            var attendances = await _context.Attendances
                .Where(a => a.EmployeeId == employeeId && a.WorkDate >= startDate && a.WorkDate <= endDate)
                .ToListAsync();

            var result = new List<Daily_attendance_model>();

            for (int day = 1; day <= totalDays; day++)
            {
                var date = new DateOnly(year, month, day);

                if (date > today)
                    continue;

                var dayOfWeek = date.DayOfWeek;
                int weekOfMonth = ((day - 1) / 7) + 1;

                bool isSunday = dayOfWeek == DayOfWeek.Sunday;
                bool isSpecialSaturday = dayOfWeek == DayOfWeek.Saturday && (weekOfMonth == 1 || weekOfMonth == 3);

                if (isSunday || isSpecialSaturday)
                {
                    result.Add(new Daily_attendance_model
                    {
                        Day = day,
                        MorningCheckIn = "",
                        MorningCheckOut = "",
                        AfternoonCheckIn = "",
                        AfternoonCheckOut = ""
                    });
                    continue;
                }

                var morning = attendances.FirstOrDefault(a => a.WorkDate == date && a.SessionId == 1);
                var afternoon = attendances.FirstOrDefault(a => a.WorkDate == date && a.SessionId == 2);

                var dto = new Daily_attendance_model
                {
                    Day = day,
                    MorningCheckIn = morning?.CheckInTime?.ToString("HH:mm") ?? "Vắng",
                    MorningCheckOut = morning?.CheckOutTime?.ToString("HH:mm") ?? "Vắng",
                    AfternoonCheckIn = afternoon?.CheckInTime?.ToString("HH:mm") ?? "Vắng",
                    AfternoonCheckOut = afternoon?.CheckOutTime?.ToString("HH:mm") ?? "Vắng"
                };

                result.Add(dto);
            }

            return Ok(result);
        }


        [HttpGet("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromQuery] string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("Email is required.");

            var exists = await _context.Employees
                .AnyAsync(e => e.Email == email && e.IsActive == true);

            if (!exists)
                return NotFound("Email chưa được đăng ký trong hệ thống.");

            return Ok("Email hợp lệ.");
        }

        [HttpGet("attendance/summary")]
        public async Task<IActionResult> GetAttendanceSummary(int employeeId, int month, int year)
        {
            //var startDate = new DateOnly(year, month, 1);
            //var endDate = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
            //var attendances = await _context.Attendances
            //    .Where(a => a.EmployeeId == employeeId && a.WorkDate >= startDate && a.WorkDate <= endDate)
            //    .ToListAsync();

            //var summary = new
            //{
            //    TotalDays = endDate.Day,
            //    PresentDays = attendances.Count(a => a.CheckInTime.HasValue),
            //    AbsentDays = endDate.Day - attendances.Count(a => a.CheckInTime.HasValue),
            //    LateCheckIns = attendances.Count(a => a.CheckInStatus == "Late"),
            //    EarlyCheckOuts = attendances.Count(a => a.CheckOutStatus == "Early")
            //};
            return Ok();
        }

        private DateTime GetDateTimeLocal(DateTime dateTime)
        {
            var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(dateTime, vnTimeZone);
        }

        [HttpGet("summary")]
        public IActionResult GetAttendanceSummary([FromQuery] string month)
        {
            if (string.IsNullOrWhiteSpace(month) || month.Length != 7)
                return BadRequest("Tháng không hợp lệ");

            if (!DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate))
                return BadRequest("Tháng không hợp lệ");

            var endDate = startDate.AddMonths(1);

            var summary = _context.Employees
                .Where(e => e.IsActive == true)
                .Select(e => new
                {
                    EmployeeId = e.EmployeeId,
                    FullName = e.FullName,

                    TotalWorkingDays = e.Attendances.Count(a =>
                        a.WorkDate >= DateOnly.FromDateTime(startDate) &&
                        a.WorkDate < DateOnly.FromDateTime(endDate) &&
                        a.CheckInTime != null && a.CheckOutTime != null
                    ),

                    TotalPaidLeaves = e.Attendances.Count(a =>
                        a.WorkDate >= DateOnly.FromDateTime(startDate) &&
                        a.WorkDate < DateOnly.FromDateTime(endDate) &&
                        a.LeaveType == "Paid" &&
                        a.Status == "Approve"
                    ),

                    TotalUnpaidLeaves = e.Attendances.Count(a =>
                        a.WorkDate >= DateOnly.FromDateTime(startDate) &&
                        a.WorkDate < DateOnly.FromDateTime(endDate) &&
                        a.LeaveType == "Unpaid" &&
                        a.Status == "Approve"
                    ),

                    TotalPendingLeaves = e.Attendances.Count(a =>
                        a.WorkDate >= DateOnly.FromDateTime(startDate) &&
                        a.WorkDate < DateOnly.FromDateTime(endDate) &&
                        (a.LeaveType == "Paid" || a.LeaveType == "Unpaid") &&
                        a.Status == "Confirm"
                    ),

                    TotalLateSessions = e.Attendances.Count(a =>
                        a.WorkDate >= DateOnly.FromDateTime(startDate) &&
                        a.WorkDate < DateOnly.FromDateTime(endDate) &&
                        a.LateMinutes.HasValue && a.LateMinutes > 0
                    ),

                    TotalLateMinutes = e.Attendances
                        .Where(a =>
                            a.WorkDate >= DateOnly.FromDateTime(startDate) &&
                            a.WorkDate < DateOnly.FromDateTime(endDate)
                        )
                        .Sum(a => (a.LateMinutes ?? 0) + (a.EarlyCheckOutMinutes ?? 0))
                })
                .ToList();

            var result = summary.Select((x, index) => new
            {
                STT = index + 1,
                x.EmployeeId,
                x.FullName,
                x.TotalWorkingDays,
                x.TotalPaidLeaves,
                x.TotalUnpaidLeaves,
                x.TotalPendingLeaves,
                x.TotalLateSessions,
                x.TotalLateMinutes
            });

            return Ok(result);
        }



        [HttpGet("leave-types")]
        public IActionResult GetLeaveTypes()
        {
            var leaveTypes = _context.Attendances
                .Where(a => !string.IsNullOrEmpty(a.LeaveType))
                .Select(a => a.LeaveType)
                .Distinct()
                .ToList();

            var result = leaveTypes.Select(type => new
            {
                Value = type,
                Text = type == "Paid" ? "Nghỉ phép (có lương)" :
                       type == "Unpaid" ? "Nghỉ không phép" :
                       type
            }).ToList();

            // Nếu muốn hiển thị thêm trên UI (ví dụ cho dropdown lọc trạng thái), có thể thêm:
            result.Add(new
            {
                Value = "Pending",
                Text = "Đơn nghỉ đang chờ duyệt"
            });

            return Ok(result);
        }


        [HttpPost("convert-unpaid")]
        public async Task<IActionResult> ConvertUnpaid([FromBody] Convert_request_model request)
        {
            if (request.EmployeeIds == null || !request.EmployeeIds.Any())
                return BadRequest("Danh sách nhân viên không hợp lệ");

            if (string.IsNullOrWhiteSpace(request.Month) || !DateTime.TryParse($"{request.Month}-01", out var monthStart))
                return BadRequest("Tháng không hợp lệ. Định dạng đúng là yyyy-MM");

            var year = monthStart.Year;
            var month = monthStart.Month;

            var attendances = await _context.Attendances
                .Where(a =>
                    request.EmployeeIds.Contains(a.EmployeeId) &&
                    a.WorkDate.Year == year &&
                    a.WorkDate.Month == month &&
                    a.LeaveType == "Unpaid" &&
                    a.Status == "Confirm")
                .ToListAsync();

            foreach (var record in attendances)
            {
                if (request.IsApproved)
                {
                    record.LeaveType = "Paid";
                    record.Status = "Approve";
                }
                else
                {
                    // Giữ nguyên LeaveType là Unpaid
                    record.Status = "Reject";
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                UpdatedCount = attendances.Count,
                Message = request.IsApproved
                    ? $"{attendances.Count} đơn đã được duyệt thành công."
                    : $"{attendances.Count} đơn đã bị từ chối."
            });
        }

    }
}
