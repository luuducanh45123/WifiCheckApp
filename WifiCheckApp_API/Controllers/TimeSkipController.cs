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
        public async Task<IActionResult> CheckIn([FromBody] Checkin_model dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Email == dto.Email && e.IsActive == true);
            if (employee == null)
                return NotFound($"Employee with email {dto.Email} not found or inactive.");

            var device = await _context.Devices
                .FirstOrDefaultAsync(d => d.MacAddress == dto.DeviceMac && d.IsActive == true);
            if (device == null)
            {
                device = new Device
                {
                    MacAddress = dto.DeviceMac,
                    IsActive = true
                };
                _context.Devices.Add(device);
                await _context.SaveChangesAsync();
            }

            var checkInTime = dto.CheckIn;
            var today = DateOnly.FromDateTime(checkInTime);
            bool isMorning = checkInTime.TimeOfDay < TimeSpan.FromHours(12);

            var attendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EmployeeId == employee.EmployeeId && a.WorkDate == today);

            if (attendance == null)
            {
                attendance = new Attendance
                {
                    EmployeeId = employee.EmployeeId,
                    DeviceId = device.DeviceId,
                    WorkDate = today,
                    Notes = dto.Notes,
                    CheckIn = checkInTime, // Ban đầu: thời gian sớm nhất
                    MorningCheckIn = isMorning ? checkInTime : null,
                    AfternoonCheckIn = !isMorning ? checkInTime : null
                };

                _context.Attendances.Add(attendance);
            }
            else
            {
                // Không tạo mới – chỉ cập nhật
                if (isMorning)
                {
                    if (attendance.MorningCheckIn != null)
                        return BadRequest("Đã check-in ca sáng hôm nay.");

                    attendance.MorningCheckIn = checkInTime;
                }
                else
                {
                    if (attendance.AfternoonCheckIn != null)
                        return BadRequest("Đã check-in ca chiều hôm nay.");

                    attendance.AfternoonCheckIn = checkInTime;
                }

                // Gán check-in là thời gian sớm nhất (nếu chưa có hoặc mới phát sinh)
                if (attendance.CheckIn == null || checkInTime < attendance.CheckIn)
                    attendance.CheckIn = checkInTime;

                // Gán check-out là thời gian muộn nhất
                if (attendance.CheckOut == null || checkInTime > attendance.CheckOut)
                    attendance.CheckOut = checkInTime;

                attendance.DeviceId = device.DeviceId;
                attendance.Notes = dto.Notes;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Check-in thành công.",
                ca = isMorning ? "Sáng" : "Chiều",
                checkInTime = checkInTime,
                attendanceId = attendance.AttendanceId
            });
        }



        [HttpPost("checkout")]
        public async Task<IActionResult> CheckOut([FromBody] Checkout_model dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Email == dto.Email && e.IsActive == true);
            if (employee == null)
                return NotFound($"Employee with email {dto.Email} not found or inactive.");

            var device = await _context.Devices
                .FirstOrDefaultAsync(d => d.MacAddress == dto.DeviceMac && d.IsActive == true);
            if (device == null)
            {
                device = new Device
                {
                    MacAddress = dto.DeviceMac,
                    IsActive = true
                };
                _context.Devices.Add(device);
                await _context.SaveChangesAsync();
            }

            var today = DateOnly.FromDateTime(DateTime.Now);

            var existingAttendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EmployeeId == employee.EmployeeId && a.WorkDate == today);

            if (existingAttendance == null)
                return BadRequest("Chưa check-in hôm nay. Không thể check-out.");

            //if (existingAttendance.CheckOut != null)
            //    return BadRequest("Đã check-out rồi.");

            if (existingAttendance.CheckIn == null)
                return BadRequest("CheckIn time not found. Cannot proceed with CheckOut.");

            if (dto.Checkout == null)
                return BadRequest("CheckOut time is required.");

            existingAttendance.CheckOut = dto.Checkout.Value;
            existingAttendance.DeviceId = device.DeviceId;
            existingAttendance.Notes = "CheckOut";

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Check-out thành công.",
                attendanceId = existingAttendance.AttendanceId,
                checkInTime = existingAttendance.CheckIn,
                checkOutTime = existingAttendance.CheckOut
            });
        }

        [HttpGet("attendance/monthly")]
        public async Task<IActionResult> GetMonthlyAttendance(int employeeId, int month, int year)
        {
            var totalDays = DateTime.DaysInMonth(year, month);
            var startDate = new DateOnly(year, month, 1);
            var endDate = new DateOnly(year, month, totalDays);

            // Ngày hiện tại
            var today = DateOnly.FromDateTime(DateTime.Now);

            // Lấy dữ liệu chấm công trong khoảng
            var attendances = await _context.Attendances
                .Where(a => a.EmployeeId == employeeId && a.WorkDate >= startDate && a.WorkDate <= endDate)
                .ToListAsync();

            var result = new List<Daily_attendance_model>();

            for (int day = 1; day <= totalDays; day++)
            {
                var date = new DateOnly(year, month, day);

                // Nếu ngày trong tương lai -> bỏ qua
                if (date > today)
                    continue;

                var dayOfWeek = date.DayOfWeek; // Sunday = 0, Monday = 1, ..., Saturday = 6

                // Tính tuần trong tháng (tuần bắt đầu từ thứ Hai)
                // Tuần 1: ngày 1-7, tuần 2: 8-14, tuần 3: 15-21, ...
                int weekOfMonth = ((day - 1) / 7) + 1;

                // Nếu Chủ Nhật hoặc Thứ Bảy của tuần 1 hoặc tuần 3 -> để trống (không hiển thị vắng mặt)
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

                var attendance = attendances.FirstOrDefault(a => a.WorkDate == date);

                var dto = new Daily_attendance_model
                {
                    Day = day,
                    MorningCheckIn = attendance?.MorningCheckIn?.ToString("HH:mm") ?? "Vắng",
                    MorningCheckOut = attendance?.MorningCheckOut?.ToString("HH:mm") ?? "Vắng",
                    AfternoonCheckIn = attendance?.AfternoonCheckIn?.ToString("HH:mm") ?? "Vắng",
                    AfternoonCheckOut = attendance?.AfternoonCheckOut?.ToString("HH:mm") ?? "Vắng"
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

    }
}
