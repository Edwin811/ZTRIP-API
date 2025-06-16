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

        [HttpPost("register")]
        public async Task<IActionResult> Register(
            [FromForm] string email,
            [FromForm] string name,
            [FromForm] string password,
            [FromForm] IFormFile? profileImage = null,
            [FromForm] IFormFile? ktpImage = null,
            [FromForm] IFormFile? simImage = null)
        {
            try
            {
                // Validasi input - expand basic validation
                if (string.IsNullOrEmpty(email))
                {
                    return BadRequest(new { message = "Email tidak boleh kosong" });
                }

                if (string.IsNullOrEmpty(name))
                {
                    return BadRequest(new { message = "Nama tidak boleh kosong" });
                }

                if (string.IsNullOrEmpty(password))
                {
                    return BadRequest(new { message = "Password tidak boleh kosong" });
                }

                // Validate email format
                try
                {
                    var addr = new System.Net.Mail.MailAddress(email);
                    if (addr.Address != email)
                    {
                        return BadRequest(new { message = "Format email tidak valid" });
                    }
                }
                catch
                {
                    return BadRequest(new { message = "Format email tidak valid" });
                }

                // Validate password strength
                if (password.Length < 6)
                {
                    return BadRequest(new { message = "Password minimal 6 karakter" });
                }

                // Cek apakah email sudah terdaftar
                var ctx = new UsersContext(_constr);
                if (ctx.GetUserByEmail(email) != null)
                {
                    return BadRequest(new { message = "Email sudah terdaftar" });
                }

                // Proses KTP jika ada
                byte[]? ktpImageData = null;
                if (ktpImage != null && ktpImage.Length > 0)
                {
                    // Validasi tipe file
                    var allowedTypes = new[] { "image/jpeg", "image/png", "image/jpg" };
                    if (!allowedTypes.Contains(ktpImage.ContentType))
                        return BadRequest(new { message = "Tipe file KTP tidak didukung, gunakan JPG atau PNG" });

                    // Validasi ukuran file (maks 5MB)
                    if (ktpImage.Length > 5 * 1024 * 1024)
                        return BadRequest(new { message = "Ukuran file KTP tidak boleh lebih dari 5MB" });

                    // Konversi file ke byte array
                    using var ms = new MemoryStream();
                    await ktpImage.CopyToAsync(ms);
                    ktpImageData = ms.ToArray();
                }

                // Proses SIM jika ada
                byte[]? simImageData = null;
                if (simImage != null && simImage.Length > 0)
                {
                    // Validasi tipe file
                    var allowedTypes = new[] { "image/jpeg", "image/png", "image/jpg" };
                    if (!allowedTypes.Contains(simImage.ContentType))
                        return BadRequest(new { message = "Tipe file SIM tidak didukung, gunakan JPG atau PNG" });

                    // Validasi ukuran file (maks 5MB)
                    if (simImage.Length > 5 * 1024 * 1024)
                        return BadRequest(new { message = "Ukuran file SIM tidak boleh lebih dari 5MB" });

                    // Konversi file ke byte array
                    using var ms = new MemoryStream();
                    await simImage.CopyToAsync(ms);
                    simImageData = ms.ToArray();
                }

                // Proses foto profil jika ada
                byte[]? profileImageData = null;
                if (profileImage != null && profileImage.Length > 0)
                {
                    // Validasi tipe file
                    var allowedTypes = new[] { "image/jpeg", "image/png", "image/jpg" };
                    if (!allowedTypes.Contains(profileImage.ContentType))
                        return BadRequest(new { message = "Tipe file profil tidak didukung, gunakan JPG atau PNG" });

                    // Validasi ukuran file (maks 5MB)
                    if (profileImage.Length > 5 * 1024 * 1024)
                        return BadRequest(new { message = "Ukuran file profil tidak boleh lebih dari 5MB" });

                    // Konversi file ke byte array
                    using var ms = new MemoryStream();
                    await profileImage.CopyToAsync(ms);
                    profileImageData = ms.ToArray();
                }

                // Buat user baru
                var user = new Users
                {
                    Email = email,
                    Name = name,
                    Password = password, // Akan di-hash di RegisterUserWithIdentity
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
