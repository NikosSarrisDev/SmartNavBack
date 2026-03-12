using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartNav.Data;
using SmartNav.Interfaces;
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
        private readonly IEmailService _emailService;

        public UserController(AppDbContext context, IPasswordService passwordService, IEmailService emailService)
        {
            _context = context;
            _passwordService = passwordService;
            _emailService = emailService;
        }

        [HttpPost("CreateUser")]
        public async Task<IActionResult> CreateUser([FromBody] User user)
        {
            try
            {
                bool userExists = await _context.Users
                .AnyAsync(u => u.UserName == user.UserName);

                if (userExists)
                {
                    return Ok(new ApiResponse<object>
                    {
                        Status = "error",
                        Message = "Username already exists",
                        Data = null
                    });
                }

                user.VerificationToken = Guid.NewGuid().ToString();
                user.Password = _passwordService.HashPassword(user.Password);
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                //Sending the email after creation of User
                await _emailService.SendVerificationEmail(user.Email, user.VerificationToken);

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
                    Status = "success",
                    Message = "User created successfully",
                    Data = resultData
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>
                {
                    Status = "error",
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
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

                if (user == null || !_passwordService.VerifyPassword(request.Password, user.Password))
                {
                    return Ok(new ApiResponse<object> { Status = "User error", Message = "Invalid credentials", Data = null });
                }

                if (!user.IsVerified)
                {
                    return Ok(new ApiResponse<object> { Status = "User error", Message = "User exists but his email is not verified", Data = null });
                }

                var resultData = new { user.Id, user.UserName, user.Name, user.Surname, user.Phone, user.IsVerified };

                return Ok(new ApiResponse<object>
                {
                    Status = "success",
                    Message = "Login successful",
                    Data = resultData
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string> 
                {
                    Status = "error",
                    Message = ex.Message,
                    Data = null 
                });
            }
        }

        [HttpGet("GetUser/{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null) return NotFound(new ApiResponse<object> { Status = "User error", Message = "User not found", Data = null });

                var resultData = new { user.Id, user.UserName, user.Name, user.Surname, user.Phone };
                return Ok(new ApiResponse<object> { Status = "success", Message = "User found", Data = resultData });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string> { Status = "error", Message = ex.Message, Data = null });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUserDetails(int id, [FromBody] User updatedUser)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null) return NotFound(new ApiResponse<object> { Status = "User error", Message = "User not found", Data = null });

                user.Name = updatedUser.Name;
                user.Surname = updatedUser.Surname;
                user.Email = updatedUser.Email;
                user.Phone = updatedUser.Phone;

                await _context.SaveChangesAsync();

                var resultData = new { user.Id, user.UserName, user.Name, user.Surname, user.Phone };
                return Ok(new ApiResponse<object> { Status = "success", Message = "Update successful", Data = resultData });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string> { Status = "error", Message = ex.Message, Data = null });
            }
        }

        [HttpPost("forgotPasswordSendEmail")]
        public async Task<IActionResult> ForgotPassword([FromQuery] string email)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null) return NotFound(new ApiResponse<object> { Status = "User error", Message = "Email not found", Data = null });

                // Email logic goes here...

                return Ok(new ApiResponse<object>
                {
                    Status = "success",
                    Message = "Recovery link sent",
                    Data = null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string> { Status = "error", Message = ex.Message, Data = null });
            }
        }

        [HttpGet("VerifyEmail")]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.VerificationToken == token);

            if (user == null)
            {
                return Content(GetHtmlResponse("Invalid Link", "This verification link is invalid or has expired.", "#e74c3c"), "text/html");
            }

            user.IsVerified = true;
            user.VerificationToken = null;

            await _context.SaveChangesAsync();

            return Content(GetHtmlResponse("Success!", "Your email has been verified. You can now close this window and log in to the app.", "#2ecc71"), "text/html");
        }

        private static string GetHtmlResponse(string title, string message, string color)
        {
            return $@"
            <html>
                <body style='font-family: Arial, sans-serif; display: flex; justify-content: center; align-items: center; height: 100vh; margin: 0; background-color: #f4f7f6;'>
                    <div style='text-align: center; padding: 50px; background: white; border-radius: 10px; box-shadow: 0 4px 15px rgba(0,0,0,0.1); max-width: 400px;'>
                        <h1 style='color: {color};'>{title}</h1>
                        <p style='color: #333; font-size: 18px;'>{message}</p>
                    </div>
                </body>
            </html>";
        }

    }

    public class LoginRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }
}
