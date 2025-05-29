using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WifiCheckApp_API.Models;
using WifiCheckApp_API.ViewModels;
using System.Collections.Generic;

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

            var today = DateOnly.FromDateTime(dto.CheckIn);

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
                return BadRequest($"Nhân viên đã check-in cho ca {(sessionId == 1 ? "sáng" : "chiều")} hôm nay \nThời gian: {existingCheckin.CheckInTime:HH:mm:ss}");
            }

            var attendance = new Attendance
            {
                EmployeeId = employee.EmployeeId,
                WorkDate = today,
                SessionId = sessionId,
                CheckInTime = dto.CheckIn,
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
                checkInTime = dto.CheckIn,
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

            var today = DateOnly.FromDateTime(DateTime.Now);

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
                return BadRequest($"Nhân viên chưa check-in cho ca {(sessionId == 1 ? "sáng" : "chiều")} hôm nay.\nVui lòng check-in trước khi check-out.");
            }

            // Update bản ghi hiện có với thông tin check-out
            existingAttendance.CheckOutTime = dto.CheckOut;
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

        [HttpGet("attendances/summary-employee")]
        public async Task<IActionResult> GetSummaryEmployee(int employeeId, int month, int year)
        {
            var startDate = new DateOnly(year, month, 1);
            var endDate = new DateOnly(year, month, DateTime.DaysInMonth(year, month));

            var attendances = await _context.Attendances
                .Where(a => a.EmployeeId == employeeId && a.WorkDate >= startDate && a.WorkDate <= endDate)
                .OrderBy(a => a.WorkDate)
                .ThenBy(a => a.SessionId)
                .ToListAsync();

            var attendanceDict = attendances
                .GroupBy(a => a.WorkDate)
                .ToDictionary(g => g.Key, g => g.ToList());

            return Ok(CalculateAttendanceSummary(startDate, endDate, attendanceDict));
        }

        private SummaryModel CalculateAttendanceSummary(DateOnly startDate, DateOnly endDate, Dictionary<DateOnly, List<Attendance>> attendanceDict)
        {
            double totalWorkingDays = 0;
            int totalLateDays = 0, totalLateMinutes = 0, totalEarlyLeaveDays = 0, totalEarlyLeaveMinutes = 0, standardWorkingDays = 0;
            int totalDaysOff = 0, totalDaysOffWithoutReason = 0, totalDaysOffWithReason = 0;
            var dailyDetails = new List<DailyModel>();

            // Helper function for holiday check
            bool IsHoliday(DateOnly date)
            {
                if (date.DayOfWeek == DayOfWeek.Sunday) return true;
                if (date.DayOfWeek == DayOfWeek.Saturday)
                {
                    var firstDayOfMonth = new DateOnly(date.Year, date.Month, 1);
                    var firstSaturday = firstDayOfMonth;
                    while (firstSaturday.DayOfWeek != DayOfWeek.Saturday)
                        firstSaturday = firstSaturday.AddDays(1);
                    if (date < firstSaturday) return false;
                    var weekOfMonth = ((date.DayNumber - firstSaturday.DayNumber) / 7) + 1;
                    return weekOfMonth == 1 || weekOfMonth == 3;
                }
                return false;
            }

            // Helper function to determine leave status
            string GetLeaveStatus(Attendance? morning, Attendance? afternoon)
            {
                // Kiểm tra nghỉ phép có lương đã được duyệt
                bool hasPaidLeave = (morning?.LeaveType == "Paid" && morning?.Status == "Approve") ||
                                   (afternoon?.LeaveType == "Paid" && afternoon?.Status == "Approve");

                if (hasPaidLeave) return "paid";

                // Kiểm tra nghỉ phép không lương hoặc nghỉ phép bị từ chối
                bool hasUnpaidOrRejectedLeave = (morning?.LeaveType == "Unpaid") ||
                                               (afternoon?.LeaveType == "Unpaid") ||
                                               (morning?.LeaveType == "Paid" && morning?.Status == "Reject") ||
                                               (afternoon?.LeaveType == "Paid" && afternoon?.Status == "Reject");

                if (hasUnpaidOrRejectedLeave) return "unpaid";

                return null; // Không có nghỉ phép
            }

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var isHoliday = IsHoliday(date);
                if (isHoliday)
                {
                    var holidayType = date.DayOfWeek == DayOfWeek.Sunday ? "Sunday" : "Saturday (Week 1 or 3)";
                    dailyDetails.Add(new DailyModel
                    {
                        Date = date.ToString("yyyy-MM-dd"),
                        DayOfWeek = date.DayOfWeek.ToString(),
                        IsHoliday = true,
                        HolidayType = holidayType,
                        WorkingDay = 0.0,
                        Status = "weekend"
                    });
                    continue;
                }

                standardWorkingDays++;
                var dayAttendances = attendanceDict.GetValueOrDefault(date, new List<Attendance>());
                var morning = dayAttendances.FirstOrDefault(a => a.SessionId == 1);
                var afternoon = dayAttendances.FirstOrDefault(a => a.SessionId == 2);

                var leaveStatus = GetLeaveStatus(morning, afternoon);
                var dailyModel = CreateDailyModel(date, morning, afternoon, leaveStatus);

                // Cập nhật tổng kết nghỉ phép
                if (!string.IsNullOrEmpty(leaveStatus))
                {
                    totalDaysOff++;
                    if (leaveStatus == "paid")
                        totalDaysOffWithReason++;
                    else
                        totalDaysOffWithoutReason++;
                }
                else if (dailyModel.Status == "absent")
                {
                    totalDaysOff++;
                    totalDaysOffWithoutReason++;
                }

                // Cập nhật tổng kết
                totalWorkingDays += dailyModel.WorkingDay;
                if (dailyModel.LateMinutes > 0)
                {
                    totalLateDays++;
                    totalLateMinutes += dailyModel.LateMinutes;
                }
                if (dailyModel.EarlyLeaveMinutes > 0)
                {
                    totalEarlyLeaveDays++;
                    totalEarlyLeaveMinutes += dailyModel.EarlyLeaveMinutes;
                }

                dailyDetails.Add(dailyModel);
            }

            return new SummaryModel
            {
                Monthly = new MonthlyModel
                {
                    WorkingDays = Math.Round(totalWorkingDays, 2),
                    StandardWorkingDays = standardWorkingDays,
                    AttendanceRate = standardWorkingDays > 0 ? Math.Round((totalWorkingDays / standardWorkingDays) * 100, 2) : 0,
                    LateDaysCount = totalLateDays,
                    LateMinutes = totalLateMinutes,
                    EarlyLeaveDaysCount = totalEarlyLeaveDays,
                    EarlyCheckOutMinutes = totalEarlyLeaveMinutes,
                    TotalDaysInMonth = DateTime.DaysInMonth(startDate.Year, startDate.Month),
                    TotalDaysOff = totalDaysOff,
                    TotalDaysOffWithoutReason = totalDaysOffWithoutReason,
                    TotalDaysOffWithReason = totalDaysOffWithReason
                },
                Daily = dailyDetails
            };
        }

        private DailyModel CreateDailyModel(DateOnly date, Attendance? morning, Attendance? afternoon, string leaveStatus)
        {
            var dailyModel = new DailyModel
            {
                Date = date.ToString("yyyy-MM-dd"),
                DayOfWeek = date.DayOfWeek.ToString(),
                IsHoliday = false,
                MorningCheckIn = morning?.CheckInTime?.ToString("HH:mm"),
                MorningCheckOut = morning?.CheckOutTime?.ToString("HH:mm"),
                AfternoonCheckIn = afternoon?.CheckInTime?.ToString("HH:mm"),
                AfternoonCheckOut = afternoon?.CheckOutTime?.ToString("HH:mm"),
                LeaveStatus = !string.IsNullOrEmpty(leaveStatus) ? (morning?.Status ?? afternoon?.Status) : null
            };

            if (!string.IsNullOrEmpty(leaveStatus))
            {
                // Có nghỉ phép
                dailyModel.WorkingDay = leaveStatus == "paid" ? 1.0 : 0.0;
                dailyModel.LateMinutes = 0;
                dailyModel.EarlyLeaveMinutes = 0;
                dailyModel.Status = leaveStatus;
            }
            else
            {
                // Không có nghỉ phép, tính toán bình thường
                var (workingDay, lateMinutes, earlyLeaveMinutes, isLate, isEarlyLeave) = CalculateDailyWork(morning, afternoon);

                dailyModel.WorkingDay = Math.Round(workingDay, 2);
                dailyModel.LateMinutes = lateMinutes;
                dailyModel.EarlyLeaveMinutes = earlyLeaveMinutes;
                dailyModel.Status = workingDay == 0 ? "absent" :
                                   workingDay == 1.0 ? "ontime" :
                                   workingDay == 0.5 ? "earlyleave" :
                                   workingDay < 1.0 ? "late" : "ontime";
            }

            return dailyModel;
        }

        private (double workingDay, int lateMinutes, int earlyLeaveMinutes, bool isLate, bool isEarlyLeave) CalculateDailyWork(Attendance? morning, Attendance? afternoon)
        {
            double workingDay = 0;
            int lateMinutes = 0, earlyLeaveMinutes = 0;
            bool isLate = false, isEarlyLeave = false;

            // Kiểm tra nghỉ phép có lương đã được duyệt
            bool hasPaidLeave = (morning?.LeaveType == "Paid" && morning?.Status == "Approve") ||
                               (afternoon?.LeaveType == "Paid" && afternoon?.Status == "Approve");

            if (hasPaidLeave)
            {
                return (1.0, 0, 0, false, false); // Tính đủ công, không late/early leave
            }

            // Thời gian làm việc chuẩn (8 giờ = 480 phút)
            const int standardWorkingMinutes = 480;
            int actualWorkingMinutes = 0;

            // Morning session
            if (morning?.CheckInTime != null)
            {
                var checkIn = morning.CheckInTime.Value.TimeOfDay;
                var expectedStart = new TimeSpan(8, 0, 0);
                var morningEnd = new TimeSpan(12, 0, 0); // Giả sử buổi sáng kết thúc 12h

                if (checkIn > expectedStart)
                {
                    lateMinutes = (int)(checkIn - expectedStart).TotalMinutes;
                    isLate = true;
                }

                // Tính thời gian làm việc buổi sáng thực tế
                var effectiveStart = checkIn > expectedStart ? checkIn : expectedStart;
                var morningWorkMinutes = (int)(morningEnd - effectiveStart).TotalMinutes;
                actualWorkingMinutes += Math.Max(0, morningWorkMinutes);

                workingDay += 0.5;
            }
            else if (morning?.LeaveType == "Paid" && morning?.Status == "Approve")
            {
                // Nghỉ phép có lương buổi sáng - tính 0.5 công
                workingDay += 0.5;
                actualWorkingMinutes += 240; // 4 tiếng buổi sáng
            }

            // Afternoon session
            if (afternoon?.CheckOutTime != null)
            {
                var checkOut = afternoon.CheckOutTime.Value.TimeOfDay;
                var expectedEnd = new TimeSpan(17, 30, 0);
                var afternoonStart = new TimeSpan(13, 30, 0); // Giả sử buổi chiều bắt đầu 13h30

                if (checkOut < expectedEnd)
                {
                    earlyLeaveMinutes = (int)(expectedEnd - checkOut).TotalMinutes;
                    isEarlyLeave = true;
                }

                // Tính thời gian làm việc buổi chiều thực tế
                var effectiveEnd = checkOut < expectedEnd ? checkOut : expectedEnd;
                var afternoonWorkMinutes = (int)(effectiveEnd - afternoonStart).TotalMinutes;
                actualWorkingMinutes += Math.Max(0, afternoonWorkMinutes);

                workingDay += 0.5;
            }
            else if (afternoon?.LeaveType == "Paid" && afternoon?.Status == "Approve")
            {
                // Nghỉ phép có lương buổi chiều - tính 0.5 công
                workingDay += 0.5;
                actualWorkingMinutes += 240; // 4 tiếng buổi chiều
            }

            // Special case: morning check-in + afternoon check-out only (ca nguyên ngày)
            if (morning?.CheckInTime != null && afternoon?.CheckOutTime != null &&
                morning?.CheckOutTime == null && afternoon?.CheckInTime == null)
            {
                var checkIn = morning.CheckInTime.Value.TimeOfDay;
                var checkOut = afternoon.CheckOutTime.Value.TimeOfDay;
                var expectedStart = new TimeSpan(8, 0, 0);
                var expectedEnd = new TimeSpan(17, 30, 0);

                // Tính late và early leave
                if (checkIn > expectedStart)
                {
                    lateMinutes = (int)(checkIn - expectedStart).TotalMinutes;
                    isLate = true;
                }

                if (checkOut < expectedEnd)
                {
                    earlyLeaveMinutes = (int)(expectedEnd - checkOut).TotalMinutes;
                    isEarlyLeave = true;
                }

                // Tính thời gian làm việc thực tế (trừ nghỉ trưa 1.5h)
                var effectiveStart = checkIn > expectedStart ? checkIn : expectedStart;
                var effectiveEnd = checkOut < expectedEnd ? checkOut : expectedEnd;
                var totalMinutes = (int)(effectiveEnd - effectiveStart).TotalMinutes;
                actualWorkingMinutes = Math.Max(0, totalMinutes - 90); // Trừ 90 phút nghỉ trưa

                workingDay = 1.0;
            }

            // Tính ngày công dựa trên thời gian làm việc thực tế
            if (actualWorkingMinutes > 0)
            {
                double actualWorkingDay = (double)actualWorkingMinutes / standardWorkingMinutes;
                workingDay = Math.Min(workingDay, actualWorkingDay);
            }

            return (Math.Min(workingDay, 1.0), lateMinutes, earlyLeaveMinutes, isLate, isEarlyLeave);
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
