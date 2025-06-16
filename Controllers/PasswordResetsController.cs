using System;
using System.Text;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Z_TRIP.Models;
using Z_TRIP.Helpers;
using Z_TRIP.Services;
using Z_TRIP.Models.Contexts;
using System.Text.Json;

namespace Z_TRIP.Controllers
{
    [Route("api/password-reset")]
    [ApiController]
    public class PasswordResetsController : ControllerBase
    {
        private readonly string _constr;
        private readonly EmailService _emailService;

        public PasswordResetsController(IConfiguration config, EmailService emailService)
        {
            _constr = config.GetConnectionString("koneksi")!;
            _emailService = emailService;
        }

        // 1. Request reset password dengan email
        [HttpPost("request")]
        public async Task<IActionResult> RequestReset([FromBody] JsonElement requestBody)
        {
            try
            {
                // Extract email dari JSON request
                if (!requestBody.TryGetProperty("email", out JsonElement emailElement) ||
                    emailElement.ValueKind != JsonValueKind.String)
                {
                    return BadRequest(new { message = "Email tidak boleh kosong" });
                }

                string email = emailElement.GetString() ?? string.Empty;

                // Log input
                Console.WriteLine($"Password reset requested for email: '{email}'");

                if (string.IsNullOrEmpty(email))
                    return BadRequest(new { message = "Email tidak boleh kosong" });

                // Trim email untuk menghilangkan whitespace
                email = email.Trim();

                // ADD: Email format validation
                try
                {
                    var addr = new System.Net.Mail.MailAddress(email);
                    if (addr.Address != email)
                        return BadRequest(new { message = "Format email tidak valid" });
                }
                catch
                {
                    return BadRequest(new { message = "Format email tidak valid" });
                }

                // Cek apakah email ada di database
                var userCtx = new UsersContext(_constr);
                var user = userCtx.GetUserByEmail(email);

                if (user == null)
                {
                    Console.WriteLine($"Email tidak ditemukan di database: '{email}'");
                    return NotFound(new { message = "Email tidak terdaftar" });
                }

                Console.WriteLine($"Email ditemukan: '{user.Email}' (ID: {user.Id})");

                // Generate OTP (6 digit)
                var otp = GenerateOTP();

                // Simpan OTP ke database (token akan digunakan sebagai OTP)
                var resetCtx = new PasswordResetContext(_constr);

                // Hapus token lama yang belum digunakan jika ada
                resetCtx.DeleteUnusedTokensByUserId(user.Id);

                var expiresAt = DateTime.UtcNow.AddMinutes(30); // Berlaku 30 menit
                var resetId = resetCtx.CreateToken(user.Id, otp, expiresAt);

                if (resetId <= 0)
                    return StatusCode(500, new { message = "Gagal membuat reset token" });

                try
                {
                    // Kirim OTP via email
                    await _emailService.SendOTP(user.Email, otp);

                    return Ok(new
                    {
                        message = "Kode verifikasi telah dikirim ke email Anda"
                    });
                }
                catch (Exception ex)
                {
                    // Log error dan kembalikan pesan error generic
                    Console.WriteLine($"Error sending email: {ex.Message}");
                    return StatusCode(500, new { message = "Gagal mengirim email verifikasi. Silakan coba lagi nanti." });
                }
            }
            catch (Exception ex)
            {
                // Log error dan kembalikan pesan error generic
                Console.WriteLine($"Error in password reset request: {ex.Message}");
                return StatusCode(500, new { message = "Terjadi kesalahan. Silakan coba lagi nanti." });
            }
        }

        // 2. Verifikasi OTP/Kode
        [HttpPost("verify-otp")]
        public IActionResult VerifyOTP([FromBody] JsonElement requestBody)
        {
            try
            {
                // ADD: Null check for request body
                if (requestBody.ValueKind == JsonValueKind.Undefined || requestBody.ValueKind == JsonValueKind.Null)
                {
                    return BadRequest(new { message = "Data tidak boleh kosong" });
                }

                // Extract email dan OTP dari JSON request
                if (!requestBody.TryGetProperty("email", out JsonElement emailElement) ||
                    emailElement.ValueKind != JsonValueKind.String)
                {
                    return BadRequest(new { message = "Email tidak boleh kosong" });
                }

                if (!requestBody.TryGetProperty("otp", out JsonElement otpElement) ||
                    otpElement.ValueKind != JsonValueKind.String)
                {
                    return BadRequest(new { message = "OTP tidak boleh kosong" });
                }

                string email = emailElement.GetString() ?? string.Empty;
                string otp = otpElement.GetString() ?? string.Empty;

                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(otp))
                    return BadRequest(new { message = "Email dan OTP tidak boleh kosong" });

                // ADD: Email format validation
                try
                {
                    var addr = new System.Net.Mail.MailAddress(email);
                    if (addr.Address != email)
                        return BadRequest(new { message = "Format email tidak valid" });
                }
                catch
                {
                    return BadRequest(new { message = "Format email tidak valid" });
                }

                // ADD: OTP format validation - ensure it's 6 digits
                if (otp.Length != 6 || !otp.All(char.IsDigit))
                    return BadRequest(new { message = "Format OTP tidak valid. Harus 6 digit angka." });

                // Cek apakah email ada di database
                var userCtx = new UsersContext(_constr);
                var user = userCtx.GetUserByEmail(email);

                if (user == null)
                    return NotFound(new { message = "Email tidak terdaftar" });

                // Verifikasi OTP
                var resetCtx = new PasswordResetContext(_constr);
                var reset = resetCtx.ValidateToken(user.Id, otp);

                if (reset == null)
                    return BadRequest(new { message = "OTP tidak valid atau sudah expired" });

                // Generate temporary token untuk reset password
                var tempToken = Guid.NewGuid().ToString("N");
                resetCtx.UpdateToken(reset.Id, tempToken);

                return Ok(new
                {
                    message = "OTP valid",
                    reset_token = tempToken
                });
            }
            catch (Exception ex)
            {
                // Log error dan kembalikan pesan error generic
                Console.WriteLine($"Error in OTP verification: {ex.Message}");
                return StatusCode(500, new { message = "Terjadi kesalahan. Silakan coba lagi nanti." });
            }
        }

        // 3. Reset password dengan token
        [HttpPost("reset")]
        public IActionResult ResetPassword([FromBody] JsonElement requestBody)
        {
            try
            {
                // ADD: Null check for request body
                if (requestBody.ValueKind == JsonValueKind.Undefined || requestBody.ValueKind == JsonValueKind.Null)
                {
                    return BadRequest(new { message = "Data tidak boleh kosong" });
                }

                // Extract fields dari JSON request
                if (!requestBody.TryGetProperty("email", out JsonElement emailElement) ||
                    emailElement.ValueKind != JsonValueKind.String)
                {
                    return BadRequest(new { message = "Email tidak boleh kosong" });
                }

                if (!requestBody.TryGetProperty("resetToken", out JsonElement resetTokenElement) ||
                    resetTokenElement.ValueKind != JsonValueKind.String)
                {
                    return BadRequest(new { message = "Reset token tidak boleh kosong" });
                }

                if (!requestBody.TryGetProperty("newPassword", out JsonElement newPasswordElement) ||
                    newPasswordElement.ValueKind != JsonValueKind.String)
                {
                    return BadRequest(new { message = "Password baru tidak boleh kosong" });
                }

                if (!requestBody.TryGetProperty("confirmPassword", out JsonElement confirmPasswordElement) ||
                    confirmPasswordElement.ValueKind != JsonValueKind.String)
                {
                    return BadRequest(new { message = "Konfirmasi password tidak boleh kosong" });
                }

                string email = emailElement.GetString() ?? string.Empty;
                string resetToken = resetTokenElement.GetString() ?? string.Empty;
                string newPassword = newPasswordElement.GetString() ?? string.Empty;
                string confirmPassword = confirmPasswordElement.GetString() ?? string.Empty;

                if (string.IsNullOrEmpty(email) ||
                    string.IsNullOrEmpty(resetToken) ||
                    string.IsNullOrEmpty(newPassword))
                    return BadRequest(new { message = "Semua field wajib diisi" });

                // ADD: Email format validation  
                try
                {
                    var addr = new System.Net.Mail.MailAddress(email);
                    if (addr.Address != email)
                        return BadRequest(new { message = "Format email tidak valid" });
                }
                catch
                {
                    return BadRequest(new { message = "Format email tidak valid" });
                }

                // ADD: Password strength validation
                if (newPassword.Length < 6)
                    return BadRequest(new { message = "Password minimal 6 karakter" });

                // Check if password contains both letters and numbers
                if (!newPassword.Any(char.IsLetter) || !newPassword.Any(char.IsDigit))
                    return BadRequest(new { message = "Password harus mengandung huruf dan angka" });

                // ADD: Token format validation - ensure it's not malformed (should be a valid GUID)
                if (resetToken.Length < 10) // Simple length check to avoid obviously invalid tokens
                    return BadRequest(new { message = "Format token tidak valid" });

                // Cek apakah email ada di database
                var userCtx = new UsersContext(_constr);
                var user = userCtx.GetUserByEmail(email);

                if (user == null)
                    return NotFound(new { message = "Email tidak terdaftar" });

                // Validasi reset token
                var resetCtx = new PasswordResetContext(_constr);
                var reset = resetCtx.GetResetByToken(resetToken);

                if (reset == null || reset.UserId != user.Id || reset.Used || reset.ExpiresAt < DateTime.UtcNow)
                    return BadRequest(new { message = "Token tidak valid atau sudah expired" });

                // Update password
                user.Password = newPassword;
                if (userCtx.UpdatePassword(user.Id, user.Password))
                {
                    // Mark token as used
                    resetCtx.MarkTokenAsUsed(reset.Id);

                    return Ok(new { message = "Password berhasil direset" });
                }

                return StatusCode(500, new { message = "Gagal mengupdate password" });
            }
            catch (Exception ex)
            {
                // Log error dan kembalikan pesan error generic
                Console.WriteLine($"Error in password reset: {ex.Message}");
                return StatusCode(500, new { message = "Terjadi kesalahan. Silakan coba lagi nanti." });
            }
        }

        // Hanya untuk debugging - hapus di production
        [HttpGet("check-email")]
        public IActionResult CheckEmail(string email)
        {
            // ADD: Email format validation
            try
            {
                if (string.IsNullOrEmpty(email))
                    return BadRequest(new { message = "Email wajib diisi" });

                var addr = new System.Net.Mail.MailAddress(email);
                if (addr.Address != email)
                    return BadRequest(new { message = "Format email tidak valid" });
            }
            catch
            {
                return BadRequest(new { message = "Format email tidak valid" });
            }

            var userCtx = new UsersContext(_constr);
            var user = userCtx.GetUserByEmail(email);

            if (user != null)
            {
                return Ok(new
                {
                    message = "Email terdaftar",
                    userId = user.Id,
                    email = user.Email
                });
            }

            return NotFound(new { message = "Email tidak terdaftar" });
        }

        private string GenerateOTP()
        {
            // ADD: Use more secure random number generation for OTP
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            // Convert to a 6-digit number
            int value = BitConverter.ToInt32(bytes, 0) & 0x7FFFFFFF; // Make sure it's positive
            return (value % 900000 + 100000).ToString(); // Ensure it's 6 digits
        }
    }
}
