using Microsoft.AspNetCore.Http;
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

            var today = DateOnly.FromDateTime(DateTime.Now);

            var existingAttendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EmployeeId == employee.EmployeeId && a.WorkDate == today);

            if (existingAttendance != null)
                return BadRequest("Đã check-in hôm nay.");

            if (dto.CheckIn == null)
                return BadRequest("CheckIn time is required.");

            var attendance = new Attendance
            {
                EmployeeId = employee.EmployeeId,
                DeviceId = device.DeviceId,
                CheckIn = dto.CheckIn.Value,
                WorkDate = today,
                Notes = "CheckIn"
            };

            _context.Attendances.Add(attendance);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Check-in thành công.",
                attendanceId = attendance.AttendanceId,
                checkInTime = attendance.CheckIn
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

        [HttpGet("attendances")]
        public async Task<IActionResult> GetAttendances()
        {
            // Bước 1: Lấy dữ liệu (chưa tính Index)
            var attendancesData = await _context.Attendances
                .Include(a => a.Employee)
                .OrderBy(a => a.WorkDate)
                .ThenBy(a => a.Employee.FullName)
                .ToListAsync();

            // Bước 2: Chuyển sang client side xử lý thêm Index
            var attendances = attendancesData
                .Select((a, index) => new
                {
                    Index = index + 1, // số thứ tự bắt đầu từ 1
                    EmployeeFullName = a.Employee.FullName,
                    EmployeeEmail = a.Employee.Email,
                    CheckIn = a.CheckIn,
                    CheckOut = a.CheckOut,
                    WorkDate = a.WorkDate.ToString("yyyy-MM-dd")
                })
                .ToList();

            return Ok(attendances);
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
