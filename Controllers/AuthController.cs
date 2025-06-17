using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Z_TRIP.Helpers;
using Z_TRIP.Models;
using System.Linq;
using Z_TRIP.Models.Contexts;

namespace Z_TRIP.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly string _constr;

        public AuthController(IConfiguration config)
        {
            _config = config;
            _constr = config.GetConnectionString("koneksi")!;
        }

        public class RegisterRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public IFormFile? ProfileImage { get; set; }
            public IFormFile? KtpImage { get; set; }
            public IFormFile? SimImage { get; set; }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromForm] RegisterRequest request)
        {
            try
            {
                // Validasi input - expand basic validation
                if (string.IsNullOrEmpty(request.Email))
                {
                    return BadRequest(new { message = "Email tidak boleh kosong" });
                }

                if (string.IsNullOrEmpty(request.Name))
                {
                    return BadRequest(new { message = "Nama tidak boleh kosong" });
                }

                if (string.IsNullOrEmpty(request.Password))
                {
                    return BadRequest(new { message = "Password tidak boleh kosong" });
                }

                // Validate email format
                try
                {
                    var addr = new System.Net.Mail.MailAddress(request.Email);
                    if (addr.Address != request.Email)
                    {
                        return BadRequest(new { message = "Format email tidak valid" });
                    }
                }
                catch
                {
                    return BadRequest(new { message = "Format email tidak valid" });
                }

                // Validate password strength
                if (request.Password.Length < 6)
                {
                    return BadRequest(new { message = "Password minimal 6 karakter" });
                }

                // Cek apakah email sudah terdaftar
                var ctx = new UsersContext(_constr);
                if (ctx.GetUserByEmail(request.Email) != null)
                {
                    return BadRequest(new { message = "Email sudah terdaftar" });
                }

                // Proses gambar secara langsung di controller
                byte[]? profileImageData = null;
                if (request.ProfileImage != null && request.ProfileImage.Length > 0)
                {
                    // Validasi file
                    var allowedTypes = new[] { "image/jpeg", "image/png", "image/jpg" };
                    if (!allowedTypes.Contains(request.ProfileImage.ContentType))
                        return BadRequest(new { message = "Format file profil tidak valid" });

                    // Validasi ukuran file (maks 5MB)
                    if (request.ProfileImage.Length > 5 * 1024 * 1024)
                        return BadRequest(new { message = "Ukuran file profil tidak boleh lebih dari 5MB" });

                    using var ms = new MemoryStream();
                    await request.ProfileImage.CopyToAsync(ms);
                    profileImageData = ms.ToArray();
                }

                // Buat user baru
                var user = new Users
                {
                    Email = request.Email,
                    Name = request.Name,
                    Password = request.Password, // Akan di-hash di RegisterUserWithIdentity
                    Profile = profileImageData, // Sekarang menggunakan byte[] untuk profile
                    KtpImage = null, // KTP diabaikan
                    SimImage = null, // SIM diabaikan
                    Role = false // Default role adalah customer
                };

                // Register user dan ambil ID
                int userId = ctx.RegisterUserWithIdentity(user);

                if (userId > 0)
                {
                    // Berhasil register
                    return CreatedAtAction(nameof(Login), new { }, new
                    {
                        message = "Registrasi berhasil",
                        user_id = userId,
                        hasProfile = profileImageData != null,
                        hasKtp = false, // KTP diabaikan
                        hasSim = false  // SIM diabaikan
                    });
                }
                else
                {
                    return StatusCode(500, new { message = "Gagal melakukan registrasi" });
                }
            }
            catch (Exception ex)
            {
                // Log the full exception details
                Console.WriteLine($"Registration error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                // Return sanitized error message to client
                return StatusCode(500, new { message = "Terjadi kesalahan saat memproses registrasi. Silakan coba lagi nanti." });
            }
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] Login login)
        {
            try
            {
                // Check for null object
                if (login == null)
                {
                    return BadRequest(new { message = "Data login tidak boleh kosong" });
                }

                // Debug logs
                Console.WriteLine($"Login attempt: email='{login.Email}', password='{new string('*', login.Password?.Length ?? 0)}'");

                // Trim whitespace
                login.Email = login.Email?.Trim();
                login.Password = login.Password?.Trim();

                // Detailed validation with specific error messages
                if (string.IsNullOrEmpty(login.Email))
                {
                    return BadRequest(new { message = "Email tidak boleh kosong" });
                }

                if (string.IsNullOrEmpty(login.Password))
                {
                    return BadRequest(new { message = "Password tidak boleh kosong" });
                }

                // Validate email format
                try
                {
                    var addr = new System.Net.Mail.MailAddress(login.Email);
                    if (addr.Address != login.Email)
                    {
                        return BadRequest(new { message = "Format email tidak valid" });
                    }
                }
                catch
                {
                    return BadRequest(new { message = "Format email tidak valid" });
                }

                var ctx = new UsersContext(_constr);
                var user = ctx.GetUserByEmail(login.Email);

                if (user == null)
                {
                    Console.WriteLine($"User not found: '{login.Email}'");
                    return NotFound(new { message = "User tidak ditemukan" });
                }

                Console.WriteLine($"User found: id={user.Id}, email='{user.Email}', role={user.Role}");
                Console.WriteLine($"Stored password hash: {user.Password}");

                bool verified = BCrypt.Net.BCrypt.Verify(login.Password, user.Password);
                Console.WriteLine($"Password verification result: {verified}");

                if (verified)
                {
                    var jwtHelper = new JwtHelper(_config);
                    var token = jwtHelper.GenerateJwtToken(user);

                    return Ok(new
                    {
                        token,
                        user_id = user.Id,
                        name = user.Name,
                        email = user.Email,
                        role = user.Role,
                        has_profile = user.Profile != null && user.Profile.Length > 0,
                        has_ktp = user.KtpImage != null && user.KtpImage.Length > 0,
                        has_sim = user.SimImage != null && user.SimImage.Length > 0,
                        is_verified = user.IsVerified
                    });
                }
                else
                {
                    return Unauthorized(new { message = "Password salah" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                // Return sanitized error message to client
                return StatusCode(500, new { message = "Terjadi kesalahan saat proses login. Silakan coba lagi nanti." });
            }
        }
    }
}
