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
            var utcNow = DateTime.UtcNow;
            var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(utcNow, vnTimeZone);
        }
    }
}
