using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartNav.Data;
using SmartNav.Models;
using SmartNav.Services;

namespace SmartNav.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPasswordService _passwordService;

        public UserController(AppDbContext context, IPasswordService passwordService)
        {
            _context = context;
            _passwordService = passwordService;
        }

        [HttpPost("CreateUser")]
        public async Task<IActionResult> CreateUser([FromBody] User user) 
        {
            try
            {
                user.Password = _passwordService.HashPassword(user.Password);
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                var resultData = new
                {
                    user.Id,
                    user.UserName,
                    user.Name,
                    user.Surname,
                    user.Phone
                };

                return Ok(new ApiResponse<object>
                {
                    Status = 200,
                    Message = "User created successfully",
                    Data = resultData
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>
                {
                    Status = 500,
                    Message = $"Error creating user: {ex.Message}",
                    Data = null
                });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == request.UserName);

                if (user == null || !_passwordService.VerifyPassword(request.Password, user.Password))
                {
                    return Unauthorized(new ApiResponse<object> { Status = 401, Message = "Invalid credentials", Data = null });
                }

                var resultData = new { user.Id, user.UserName, user.Name, user.Surname, user.Phone };

                return Ok(new ApiResponse<object>
                {
                    Status = 200,
                    Message = "Login successful",
                    Data = resultData
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string> { Status = 500, Message = ex.Message, Data = null });
            }
        }

        [HttpGet("GetUser/{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null) return NotFound(new ApiResponse<object> { Status = 404, Message = "User not found", Data = null });

                var resultData = new { user.Id, user.UserName, user.Name, user.Surname, user.Phone };
                return Ok(new ApiResponse<object> { Status = 200, Message = "User found", Data = resultData });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string> { Status = 500, Message = ex.Message, Data = null });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUserDetails(int id, [FromBody] User updatedUser)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null) return NotFound(new ApiResponse<object> { Status = 404, Message = "User not found", Data = null });

                user.Name = updatedUser.Name;
                user.Surname = updatedUser.Surname;
                user.Email = updatedUser.Email;
                user.Phone = updatedUser.Phone;

                await _context.SaveChangesAsync();

                var resultData = new { user.Id, user.UserName, user.Name, user.Surname, user.Phone };
                return Ok(new ApiResponse<object> { Status = 200, Message = "Update successful", Data = resultData });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string> { Status = 500, Message = ex.Message, Data = null });
            }
        }

        [HttpPost("forgotPasswordSendEmail")]
        public async Task<IActionResult> ForgotPassword([FromQuery] string email)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null) return NotFound(new ApiResponse<object> { Status = 404, Message = "Email not found", Data = null });

                // Email logic goes here...

                return Ok(new ApiResponse<object>
                {
                    Status = 200,
                    Message = "Recovery link sent",
                    Data = null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string> { Status = 500, Message = ex.Message, Data = null });
            }
        }

    }

    public class LoginRequest
    {
        public string UserName { get; set; }
        public string Password { get; set; }
    }
}
