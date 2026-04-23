using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartNav.Data;
using SmartNav.Interfaces;
using SmartNav.Models;
using SmartNav.Services;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SmartNav.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPasswordService _passwordService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public UserController(
            AppDbContext context,
            IPasswordService passwordService,
            IEmailService emailService,
            IConfiguration configuration
        )
        {
            _context = context;
            _passwordService = passwordService;
            _emailService = emailService;
            _configuration = configuration;
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

                var resultData = new { user.Id, user.UserName, user.Name, user.Email, user.Surname, user.Phone, user.IsVerified };

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

                var resultData = new { user.Id, user.UserName, user.Name, user.Email, user.Surname, user.Phone, user.RoleId, user.AvatarId };
                return Ok(new ApiResponse<object> { Status = "success", Message = "User found", Data = resultData });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string> { Status = "error", Message = ex.Message, Data = null });
            }
        }

        [HttpPost("UpdateUserDetails")]
        public async Task<IActionResult> UpdateUserDetails([FromBody] JsonElement request)
        {
            try
            {
                var user = await _context.Users.FindAsync(request.GetProperty("id").GetInt32());
                if (user == null) return NotFound(new ApiResponse<object> { Status = "User error", Message = "User not found", Data = null });

                if (request.TryGetProperty("userName", out var nameEl) && nameEl.ValueKind != JsonValueKind.Null)
                {
                    string newName = nameEl.GetString();
                    if (!string.IsNullOrEmpty(newName)) user.UserName = newName;
                }

                if (request.TryGetProperty("avatarId", out var avatarEl))
                {
                    user.AvatarId = avatarEl.GetInt32();
                }

                if (request.TryGetProperty("preferenceId", out var prefEl))
                {
                    user.PreferenceId = prefEl.GetInt32();
                }

                await _context.SaveChangesAsync();

                var resultData = new { user.Id, user.UserName, user.AvatarId, user.PreferenceId };
                return Ok(new ApiResponse<object> { Status = "success", Message = "Update successful", Data = resultData });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string> { Status = "error", Message = ex.Message, Data = null });
            }
        }

        [HttpPost("forgotPasswordSendEmail")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Email))
                {
                    return BadRequest(new ApiResponse<object> { Status = "User error", Message = "Email is required", Data = null });
                }

                var normalizedEmail = request.Email.Trim().ToLower();
                var user = await _context.Users.FirstOrDefaultAsync(u =>
                    u.Email != null && u.Email.ToLower() == normalizedEmail
                );
                if (user == null || string.IsNullOrWhiteSpace(user.Email))
                {
                    return NotFound(new ApiResponse<object> { Status = "User error", Message = "Email not found", Data = null });
                }

                var temporaryPassword = GenerateTemporaryPassword(10);
                var previousHashedPassword = user.Password;
                user.Password = _passwordService.HashPassword(temporaryPassword);
                await _context.SaveChangesAsync();

                try
                {
                    await _emailService.SendForgotPasswordEmail(user.Email, temporaryPassword);
                }
                catch
                {
                    user.Password = previousHashedPassword;
                    await _context.SaveChangesAsync();
                    throw;
                }

                return Ok(new ApiResponse<object>
                {
                    Status = "success",
                    Message = "A temporary password has been sent to your email",
                    Data = null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string> { Status = "error", Message = ex.Message, Data = null });
            }
        }

        [HttpPost("forgotPasswordSendResetLink")]
        public async Task<IActionResult> ForgotPasswordSendResetLink([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Email))
                {
                    return BadRequest(new ApiResponse<object> { Status = "User error", Message = "Email is required", Data = null });
                }

                var normalizedEmail = request.Email.Trim().ToLower();
                var user = await _context.Users.FirstOrDefaultAsync(u =>
                    u.Email != null && u.Email.ToLower() == normalizedEmail
                );
                if (user == null || string.IsNullOrWhiteSpace(user.Email))
                {
                    return NotFound(new ApiResponse<object> { Status = "User error", Message = "Email not found", Data = null });
                }

                var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
                var token = GenerateResetPasswordToken(user, expiresAt);
                var frontendBaseUrl = GetFrontendBaseUrl();
                var resetUrl = $"{frontendBaseUrl}/reset-password?token={Uri.EscapeDataString(token)}";

                await _emailService.SendResetPasswordLinkEmail(user.Email, resetUrl);

                return Ok(new ApiResponse<object>
                {
                    Status = "success",
                    Message = "A reset password link has been sent to your email",
                    Data = null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string> { Status = "error", Message = ex.Message, Data = null });
            }
        }

        [HttpPost("resetPassword")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
                {
                    return BadRequest(new ApiResponse<object> { Status = "User error", Message = "Token and password are required", Data = null });
                }

                if (request.NewPassword.Length < 8)
                {
                    return BadRequest(new ApiResponse<object> { Status = "User error", Message = "Password must have at least 8 characters", Data = null });
                }

                var user = await ValidateResetPasswordTokenAsync(request.Token.Trim());
                if (user == null)
                {
                    return BadRequest(new ApiResponse<object> { Status = "User error", Message = "Reset link is invalid or has expired", Data = null });
                }

                user.Password = _passwordService.HashPassword(request.NewPassword.Trim());
                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<object>
                {
                    Status = "success",
                    Message = "Password has been reset successfully",
                    Data = null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string> { Status = "error", Message = ex.Message, Data = null });
            }
        }

        [HttpPost("changePassword")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                if (
                    request == null ||
                    request.UserId <= 0 ||
                    string.IsNullOrWhiteSpace(request.CurrentPassword) ||
                    string.IsNullOrWhiteSpace(request.NewPassword)
                )
                {
                    return BadRequest(new ApiResponse<object> { Status = "User error", Message = "Invalid change password request", Data = null });
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId);
                if (user == null || string.IsNullOrWhiteSpace(user.Password))
                {
                    return NotFound(new ApiResponse<object> { Status = "User error", Message = "User not found", Data = null });
                }

                if (!_passwordService.VerifyPassword(request.CurrentPassword, user.Password))
                {
                    return Ok(new ApiResponse<object> { Status = "User error", Message = "Current password is not correct", Data = null });
                }

                if (request.NewPassword.Length < 8)
                {
                    return BadRequest(new ApiResponse<object> { Status = "User error", Message = "Password must have at least 8 characters", Data = null });
                }

                user.Password = _passwordService.HashPassword(request.NewPassword);
                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<object>
                {
                    Status = "success",
                    Message = "Password updated successfully",
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

        private string GetFrontendBaseUrl()
        {
            var configuredUrl = _configuration["AppUrls:FrontendBaseUrl"];
            return string.IsNullOrWhiteSpace(configuredUrl)
                ? "http://localhost:4200"
                : configuredUrl.Trim().TrimEnd('/');
        }

        private string GenerateResetPasswordToken(User user, DateTimeOffset expiresAt)
        {
            var payload = $"{user.Id}:{expiresAt.ToUnixTimeSeconds()}";
            var payloadBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
            var signature = ComputeResetTokenSignature(payloadBase64, user);
            return $"{payloadBase64}.{signature}";
        }

        private async Task<User?> ValidateResetPasswordTokenAsync(string token)
        {
            var segments = token.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length != 2)
            {
                return null;
            }

            string payloadRaw;
            try
            {
                payloadRaw = Encoding.UTF8.GetString(Base64UrlDecode(segments[0]));
            }
            catch
            {
                return null;
            }

            var payloadParts = payloadRaw.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (payloadParts.Length != 2)
            {
                return null;
            }

            if (!int.TryParse(payloadParts[0], out var userId) || userId <= 0)
            {
                return null;
            }

            if (!long.TryParse(payloadParts[1], out var expiresAtUnix))
            {
                return null;
            }

            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (expiresAtUnix < nowUnix)
            {
                return null;
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null || string.IsNullOrWhiteSpace(user.Email) || string.IsNullOrWhiteSpace(user.Password))
            {
                return null;
            }

            var expectedSignature = ComputeResetTokenSignature(segments[0], user);
            var providedBytes = Encoding.UTF8.GetBytes(segments[1]);
            var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);

            if (providedBytes.Length != expectedBytes.Length)
            {
                return null;
            }

            if (!CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes))
            {
                return null;
            }

            return user;
        }

        private string ComputeResetTokenSignature(string payloadBase64, User user)
        {
            var secret = GetResetPasswordSecret();
            var data = $"{payloadBase64}:{user.Email}:{user.Password}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Base64UrlEncode(signatureBytes);
        }

        private string GetResetPasswordSecret()
        {
            var secret = _configuration["SecuritySettings:PasswordResetSecret"];
            if (string.IsNullOrWhiteSpace(secret))
            {
                secret = _configuration["EmailSettings:Password"];
            }

            if (string.IsNullOrWhiteSpace(secret))
            {
                secret = "SmartNav-Dev-Reset-Secret-Change-Me";
            }

            return secret;
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }

        private static byte[] Base64UrlDecode(string text)
        {
            var normalized = text
                .Replace("-", "+")
                .Replace("_", "/");

            switch (normalized.Length % 4)
            {
                case 2:
                    normalized += "==";
                    break;
                case 3:
                    normalized += "=";
                    break;
            }

            return Convert.FromBase64String(normalized);
        }

        private static string GenerateTemporaryPassword(int length)
        {
            const string allowedChars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
            var result = new char[length];
            var randomBytes = RandomNumberGenerator.GetBytes(length);

            for (var i = 0; i < length; i++)
            {
                result[i] = allowedChars[randomBytes[i] % allowedChars.Length];
            }

            return new string(result);
        }

    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class ForgotPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordRequest
    {
        public string Token { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class ChangePasswordRequest
    {
        public int UserId { get; set; }
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
