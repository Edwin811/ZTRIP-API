using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Z_TRIP.Helpers;
using Z_TRIP.Models;
using System.Linq;

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

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromForm] RegisterRequest request)
        {
            try
            {
                // Validasi input
                if (string.IsNullOrEmpty(request.Email) ||
                    string.IsNullOrEmpty(request.Name) ||
                    string.IsNullOrEmpty(request.Password))
                {
                    return BadRequest(new { message = "Email, nama, dan password wajib diisi" });
                }

                // Cek apakah email sudah terdaftar
                var ctx = new UsersContext(_constr);
                if (ctx.GetUserByEmail(request.Email) != null)
                {
                    return BadRequest(new { message = "Email sudah terdaftar" });
                }

                // Proses KTP jika ada
                byte[]? ktpImageData = null;
                if (request.KtpImage != null && request.KtpImage.Length > 0)
                {
                    // Validasi tipe file
                    var allowedTypes = new[] { "image/jpeg", "image/png", "image/jpg" };
                    if (!allowedTypes.Contains(request.KtpImage.ContentType))
                        return BadRequest(new { message = "Tipe file KTP tidak didukung, gunakan JPG atau PNG" });

                    // Validasi ukuran file (maks 5MB)
                    if (request.KtpImage.Length > 5 * 1024 * 1024)
                        return BadRequest(new { message = "Ukuran file KTP tidak boleh lebih dari 5MB" });

                    // Konversi file ke byte array
                    using var ms = new MemoryStream();
                    await request.KtpImage.CopyToAsync(ms);
                    ktpImageData = ms.ToArray();
                }

                // Proses SIM jika ada
                byte[]? simImageData = null;
                if (request.SimImage != null && request.SimImage.Length > 0)
                {
                    // Validasi tipe file
                    var allowedTypes = new[] { "image/jpeg", "image/png", "image/jpg" };
                    if (!allowedTypes.Contains(request.SimImage.ContentType))
                        return BadRequest(new { message = "Tipe file SIM tidak didukung, gunakan JPG atau PNG" });

                    // Validasi ukuran file (maks 5MB)
                    if (request.SimImage.Length > 5 * 1024 * 1024)
                        return BadRequest(new { message = "Ukuran file SIM tidak boleh lebih dari 5MB" });

                    // Konversi file ke byte array
                    using var ms = new MemoryStream();
                    await request.SimImage.CopyToAsync(ms);
                    simImageData = ms.ToArray();
                }

                // Proses foto profil jika ada
                byte[]? profileImageData = null;
                if (request.ProfileImage != null && request.ProfileImage.Length > 0)
                {
                    // Validasi tipe file
                    var allowedTypes = new[] { "image/jpeg", "image/png", "image/jpg" };
                    if (!allowedTypes.Contains(request.ProfileImage.ContentType))
                        return BadRequest(new { message = "Tipe file profil tidak didukung, gunakan JPG atau PNG" });

                    // Validasi ukuran file (maks 5MB)
                    if (request.ProfileImage.Length > 5 * 1024 * 1024)
                        return BadRequest(new { message = "Ukuran file profil tidak boleh lebih dari 5MB" });

                    // Konversi file ke byte array
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
                    KtpImage = ktpImageData,
                    SimImage = simImageData,
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
                        hasKtp = ktpImageData != null,
                        hasSim = simImageData != null
                    });
                }
                else
                {
                    return StatusCode(500, new { message = "Gagal melakukan registrasi" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] Login login)
        {
            try
            {
                // Debug logs
                Console.WriteLine($"Login attempt: email='{login.Email}', password='{new string('*', login.Password?.Length ?? 0)}'");

                // Trim whitespace
                login.Email = login.Email?.Trim();
                login.Password = login.Password?.Trim();

                if (string.IsNullOrEmpty(login.Email) || string.IsNullOrEmpty(login.Password))
                {
                    return BadRequest(new { message = "Email dan password wajib diisi" });
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
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

    }

    public class RegisterRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public IFormFile? ProfileImage { get; set; } // Diganti dari string? Profile menjadi IFormFile? ProfileImage
        public IFormFile? KtpImage { get; set; }
        public IFormFile? SimImage { get; set; }
    }
}
