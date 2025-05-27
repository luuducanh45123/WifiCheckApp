using System.Data;
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
    }
}
