using System.Data;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WifiCheckApp_API.Models;
using WifiCheckApp_API.ViewModels;

namespace WifiCheckApp_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenController : ControllerBase
    {
        private readonly TimeLapsContext _context;
        private readonly IConfiguration _configuration;

        public AuthenController(TimeLapsContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        private string GenarateJwtToken(string username, string role)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var fullname = _context.Users
                .Where(u => u.Username == username)
                .Select(u => u.Employee != null ? u.Employee.FullName : "")
                .FirstOrDefault() ?? "";

            var userId = _context.Users
                .Where(u => u.Username == username)
                .Select(u => u.UserId)
                .FirstOrDefault();

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role),
                new Claim("FullName", fullname),
                new Claim("UserId", userId.ToString())
            };

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(jwtSettings["ExpireMinutes"])),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] Login_model_request request)
        {
            // 1. Lấy user từ DB (include Role + Employee)
            var user = _context.Users
                .Include(u => u.Role)
                .Include(u => u.Employee)
                .Where(u => u.Username == request.Username && u.IsActive == true)
                .FirstOrDefault();

            if (user == null)
                return Unauthorized("Tài khoản không tồn tại hoặc đã bị khóa.");

            // 2. So sánh password (giả sử bạn lưu plain text. Nếu có mã hóa bcrypt, bạn cần verify)
            if (user.Password != request.Password)
                return Unauthorized("Mật khẩu không đúng.");

            // 3. Lấy role name
            var roleNameFromDb = user.Role?.RoleName ?? "Customer";

            var roleName = roleNameFromDb.ToLower() switch
            {
                "Admin" => "Admin",
                "Customer" => "Customer",
                _ => roleNameFromDb.ToLower()
            };

            // 4. Lấy full name, nếu null lấy username luôn
            var fullname = user.Employee?.FullName ?? user.Username;

            // 4. Tạo token
            var token = GenarateJwtToken(user.Username, roleName);

            //var employeeId = user.Employee?.EmployeeId ?? user.UserId;
            return Ok(new
            {
                Token = token,
                Username = user.Username,
                Role = roleName,
                FullName = fullname,
                EmployeeId = user.Employee?.EmployeeId,
                UserId = user.UserId
            });
        }
        private static string GetRoleName(string roleNameFromDb)
        {
            if (string.IsNullOrEmpty(roleNameFromDb))
                return "Customer";

            return roleNameFromDb.ToLowerInvariant() switch
            {
                "admin" => "Admin",
                "customer" => "Customer",
                _ => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(roleNameFromDb.ToLowerInvariant())
            };
        }


        [HttpPost("loginApp")]
        public async Task<IActionResult> LoginApp([FromBody] Login_model_request request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("Username và Password không được để trống.");
            }

            try
            {
                var user = await _context.Users.AsNoTracking()
                    .Include(u => u.Role)
                    .Include(u => u.Employee)
                    .FirstOrDefaultAsync(u => u.IsActive == true && u.Username == request.Username || u.Employee.Email == request.Username);

                if (user == null)
                {
                    return Unauthorized("Tài khoản không tồn tại hoặc đã bị khóa.");
                }

                if (request.Password != user.Password)
                {
                    return Unauthorized("Mật khẩu không đúng.");
                }

                // Tối ưu role mapping
                var roleName = GetRoleName(user.Role?.RoleName);
                var fullName = user.Employee?.FullName ?? user.Username;

                // Return response
                return Ok(new LoginResponse
                {
                    Username = user.Username,
                    Email = user.Employee?.Email ?? string.Empty,
                    Role = roleName,
                    FullName = fullName,
                    EmployeeId = user.Employee?.EmployeeId,
                    UserId = user.UserId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Đã xảy ra lỗi trong quá trình đăng nhập.");
            }
        }

        // Updated method using BCrypt for password verification
        //private bool VerifyPassword(string plainPassword, string hashedPassword)
        //{
        //    try
        //    {
        //        return BCrypt.Net.BCrypt.Verify(plainPassword, hashedPassword);
        //    }
        //    catch (Exception)
        //    {
        //        return false;
        //    }
        //}

        //// Updated method using BCrypt for password hashing
        //private string HashPassword(string password)
        //{
        //    // Generate salt and hash password with BCrypt
        //    // WorkFactor 12 provides good security balance between performance and security
        //    return BCrypt.Net.BCrypt.HashPassword(password, 12);
        //}

        [HttpGet("GetUserInfo")]
        public async Task<IActionResult> GetUserInfo(int employeeId)
        {
            if (employeeId <= 0)
            {
                return BadRequest("EmployeeId không hợp lệ.");
            }
            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Employee)
                .FirstOrDefaultAsync(u => u.EmployeeId == employeeId && u.IsActive == true);
            if (user == null)
            {
                return NotFound("Không tìm thấy người dùng với EmployeeId này.");
            }
            var roleName = GetRoleName(user.Role?.RoleName);
            return Ok(new EmployeeModel
            {
                EmployeeId = user.EmployeeId,
                Email = user.Employee?.Email,
                DateOfBirth = user.Employee?.DateOfBirth,
                Department = user.Employee?.Department,
                FullName = user.Employee?.FullName,
                UserName = user.Username,
                Gender = user.Employee?.Gender,
                HireDate = user.Employee?.HireDate,
                IsActive = user.Employee?.IsActive,
                Phone = user.Employee?.Phone,
                Position = user.Employee?.Position,
                Role = roleName
            });
        }
    }
}
