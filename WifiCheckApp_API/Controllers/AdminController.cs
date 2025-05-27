using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WifiCheckApp_API.Models;
using WifiCheckApp_API.ViewModels;

namespace WifiCheckApp_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly TimeLapsContext _context;

        public AdminController(TimeLapsContext context)
        {
            _context = context;
        }

        [HttpPut("{employeeId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateAttendance(int employeeId, [FromBody] Attendance_update_model dto)
        {
            var attendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.WorkDate == DateOnly.FromDateTime(dto.Date));

            if (attendance == null)
                return NotFound("Không tìm thấy bản ghi chấm công.");

            var changes = new List<AttendanceHistory>();
            var performedBy = int.Parse(User.FindFirst("userId")?.Value ?? "0");

            void TrackChange(string field, object? oldValue, object? newValue)
            {
                if (!Equals(oldValue, newValue))
                {
                    changes.Add(new AttendanceHistory
                    {
                        AttendanceId = attendance.AttendanceId,
                        ActionType = "Update",
                        PerformedBy = performedBy,
                        PerformedAt = DateTime.Now,
                        OldValue = $"{field}: {oldValue}",
                        NewValue = $"{field}: {newValue}"
                    });
                }
            }

            TrackChange("MorningCheckIn", attendance.MorningCheckIn, dto.MorningCheckIn);
            TrackChange("MorningCheckOut", attendance.MorningCheckOut, dto.MorningCheckOut);
            TrackChange("AfternoonCheckIn", attendance.AfternoonCheckIn, dto.AfternoonCheckIn);
            TrackChange("AfternoonCheckOut", attendance.AfternoonCheckOut, dto.AfternoonCheckOut);

            // Cập nhật
            attendance.MorningCheckIn = dto.MorningCheckIn;
            attendance.MorningCheckOut = dto.MorningCheckOut;
            attendance.AfternoonCheckIn = dto.AfternoonCheckIn;
            attendance.AfternoonCheckOut = dto.AfternoonCheckOut;

            _context.AttendanceHistories.AddRange(changes);
            await _context.SaveChangesAsync();

            return Ok("Cập nhật thành công.");
        }
    }
}
