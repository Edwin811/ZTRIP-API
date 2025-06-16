using System;
using System.Text;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Z_TRIP.Models;
using Z_TRIP.Helpers;
using Z_TRIP.Services;

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
        public async Task<IActionResult> RequestReset([FromBody] PasswordResetRequest request)
        {
            // Log input
            Console.WriteLine($"Password reset requested for email: '{request.Email}'");

            if (string.IsNullOrEmpty(request.Email))
                return BadRequest(new { message = "Email tidak boleh kosong" });

            // Gunakan try-catch untuk konsistensi
            try
            {
                // Trim email untuk menghilangkan whitespace
                request.Email = request.Email.Trim();

                // Cek apakah email ada di database
                var userCtx = new UsersContext(_constr);
                var user = userCtx.GetUserByEmail(request.Email);

                if (user == null)
                {
                    Console.WriteLine($"Email tidak ditemukan di database: '{request.Email}'");
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

        // 2. Verifikasi OTP/Kode (tidak ada perubahan)
        [HttpPost("verify-otp")]
        public IActionResult VerifyOTP([FromBody] VerifyOTPRequest request)
        {
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.OTP))
                return BadRequest(new { message = "Email dan OTP tidak boleh kosong" });

            // Cek apakah email ada di database
            var userCtx = new UsersContext(_constr);
            var user = userCtx.GetUserByEmail(request.Email);

            if (user == null)
                return NotFound(new { message = "Email tidak terdaftar" });

            // Verifikasi OTP
            var resetCtx = new PasswordResetContext(_constr);
            var reset = resetCtx.ValidateToken(user.Id, request.OTP);

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

        // 3. Reset password dengan token (tidak ada perubahan)
        [HttpPost("reset")]
        public IActionResult ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (string.IsNullOrEmpty(request.Email) ||
                string.IsNullOrEmpty(request.ResetToken) ||
                string.IsNullOrEmpty(request.NewPassword))
                return BadRequest(new { message = "Semua field wajib diisi" });

            if (request.NewPassword != request.ConfirmPassword)
                return BadRequest(new { message = "Password dan konfirmasi password tidak sama" });

            // Cek apakah email ada di database
            var userCtx = new UsersContext(_constr);
            var user = userCtx.GetUserByEmail(request.Email);

            if (user == null)
                return NotFound(new { message = "Email tidak terdaftar" });

            // Validasi reset token
            var resetCtx = new PasswordResetContext(_constr);
            var reset = resetCtx.GetResetByToken(request.ResetToken);

            if (reset == null || reset.UserId != user.Id || reset.Used || reset.ExpiresAt < DateTime.UtcNow)
                return BadRequest(new { message = "Token tidak valid atau sudah expired" });

            // Update password
            user.Password = request.NewPassword;
            if (userCtx.UpdatePassword(user.Id, user.Password))
            {
                // Mark token as used
                resetCtx.MarkTokenAsUsed(reset.Id);

                return Ok(new { message = "Password berhasil direset" });
            }

            return StatusCode(500, new { message = "Gagal mengupdate password" });
        }

        // Hanya untuk debugging - hapus di production
        [HttpGet("check-email")]
        public IActionResult CheckEmail(string email)
        {
            if (string.IsNullOrEmpty(email))
                return BadRequest(new { message = "Email wajib diisi" });

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

        // Helper method untuk generate OTP
        private string GenerateOTP()
        {
            Random random = new Random();
            return random.Next(100000, 999999).ToString();
        }
    }

    public class PasswordResetRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class VerifyOTPRequest
    {
        public string Email { get; set; } = string.Empty;
        public string OTP { get; set; } = string.Empty;
    }

    public class ResetPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
        public string ResetToken { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
