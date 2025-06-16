using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.IO;
using System.Threading.Tasks;
using Z_TRIP.Models;
using Z_TRIP.Helpers;
using Z_TRIP.Exceptions;


namespace Z_TRIP.Controllers
{
    [ApiController]
    [Route("api/profile")]
    [Authorize]
    public class UserProfileController : ControllerBase
    {
        private readonly string _constr;

        public UserProfileController(IConfiguration config)
        {
            _constr = config.GetConnectionString("koneksi")!;
        }

        [HttpGet]
        public IActionResult GetProfile()
        {
            try
            {
                var userIdClaim = User.FindFirst("userId")?.Value;
                if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
                    return Unauthorized(new { message = "User tidak valid" });

                var ctx = new UsersContext(_constr);
                var user = ctx.GetUserById(userId);

                if (user == null)
                    return NotFound(new { message = "User tidak ditemukan" });

                // Status upload image
                var hasKtp = user.KtpImage != null && user.KtpImage.Length > 0;
                var hasSim = user.SimImage != null && user.SimImage.Length > 0;
                var hasProfile = user.Profile != null && user.Profile.Length > 0;

                // Tampilkan semua data user dan status image (true/false)
                return Ok(new
                {
                    id = user.Id,
                    name = user.Name,
                    email = user.Email,
                    role = user.Role,
                    isVerified = user.IsVerified,
                    createdAt = user.CreatedAt,
                    updatedAt = user.UpdatedAt,
                    // Status image (true/false)
                    hasKtp,
                    hasSim,
                    hasProfile
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // Update profile endpoint
        [HttpPut]
        [Authorize]
        public IActionResult UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            try
            {
                // Ambil user id dari claim
                var userIdClaim = User.FindFirst("userId")?.Value;

                if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
                    return Unauthorized(new { message = "User tidak valid" });

                var ctx = new UsersContext(_constr);
                var user = ctx.GetUserById(userId);

                if (user == null)
                    throw new ResourceNotFoundException("User tidak ditemukan");

                // Update nama saja, karena gambar profile dihandle terpisah
                user.Name = request.Name ?? user.Name;

                // Update di database
                bool success = ctx.UpdateUserName(userId, user.Name);

                if (success)
                    return Ok(new
                    {
                        message = "Nama profil berhasil diperbarui",
                        user = new
                        {
                            user.Id,
                            user.Name,
                            user.Email,
                            is_verified = user.IsVerified, // Tambahkan is_verified di sini
                            HasProfile = user.Profile != null && user.Profile.Length > 0,
                            HasKtp = user.KtpImage != null && user.KtpImage.Length > 0,
                            HasSim = user.SimImage != null && user.SimImage.Length > 0
                        }
                    });

                return StatusCode(500, new { message = "Gagal memperbarui profil" });
            }
            catch (ResourceNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                // Global error handler will handle this
                throw;
            }
        }

        // Class untuk request update profile
        public class UpdateProfileRequest
        {
            public string? Name { get; set; }
        }

        // Perbaikan upload KTP: tambah validasi user verification
        [HttpPost("upload-ktp")]
        public async Task<IActionResult> UploadKtp(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "File tidak boleh kosong" });

            // Validasi tipe file
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/jpg" };
            if (!allowedTypes.Contains(file.ContentType))
                return BadRequest(new { message = "Tipe file tidak didukung, gunakan JPG atau PNG" });

            // Validasi ukuran file (maks 5MB)
            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(new { message = "Ukuran file tidak boleh lebih dari 5MB" });

            try
            {
                // Ambil user id dari claim
                var userIdClaim = User.FindFirst("userId")?.Value;

                if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
                    return Unauthorized(new { message = "User tidak valid" });

                // Cek status verifikasi
                var ctx = new UsersContext(_constr);
                var user = ctx.GetUserById(userId);

                if (user == null)
                    return NotFound(new { message = "User tidak ditemukan" });

                // VALIDASI: Jika sudah terverifikasi, tidak boleh upload ulang
                if (user.IsVerified)
                    return BadRequest(new
                    {
                        message = "Dokumen KTP tidak dapat diubah karena akun Anda sudah diverifikasi",
                        isVerified = true
                    });

                // Konversi file ke byte array
                byte[] imageData;
                using (var ms = new MemoryStream())
                {
                    await file.CopyToAsync(ms);
                    imageData = ms.ToArray();
                }

                // Simpan ke database
                if (ctx.UpdateKtpImage(userId, imageData))
                    return Ok(new
                    {
                        message = "KTP berhasil diupload",
                        is_verified = user.IsVerified // Tambahkan status verifikasi
                    });

                return StatusCode(500, new { message = "Gagal mengupload KTP" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // Perbaikan upload SIM: tambah validasi user verification
        [HttpPost("upload-sim")]
        public async Task<IActionResult> UploadSim(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "File tidak boleh kosong" });

            // Validasi tipe file
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/jpg" };
            if (!allowedTypes.Contains(file.ContentType))
                return BadRequest(new { message = "Tipe file tidak didukung, gunakan JPG atau PNG" });

            // Validasi ukuran file (maks 5MB)
            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(new { message = "Ukuran file tidak boleh lebih dari 5MB" });

            try
            {
                // Ambil user id dari claim
                var userIdClaim = User.FindFirst("userId")?.Value;

                if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
                    return Unauthorized(new { message = "User tidak valid" });

                // Cek status verifikasi
                var ctx = new UsersContext(_constr);
                var user = ctx.GetUserById(userId);

                if (user == null)
                    return NotFound(new { message = "User tidak ditemukan" });

                // VALIDASI: Jika sudah terverifikasi, tidak boleh upload ulang
                if (user.IsVerified)
                    return BadRequest(new
                    {
                        message = "Dokumen SIM tidak dapat diubah karena akun Anda sudah diverifikasi",
                        isVerified = true
                    });

                // Konversi file ke byte array
                byte[] imageData;
                using (var ms = new MemoryStream())
                {
                    await file.CopyToAsync(ms);
                    imageData = ms.ToArray();
                }

                // Simpan ke database
                if (ctx.UpdateSimImage(userId, imageData))
                    return Ok(new
                    {
                        message = "SIM berhasil diupload",
                        is_verified = user.IsVerified // Tambahkan status verifikasi
                    });

                return StatusCode(500, new { message = "Gagal mengupload SIM" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // Upload profile image - konsisten dengan upload KTP/SIM
        [HttpPost("upload-profile")]
        [Authorize]
        public async Task<IActionResult> UploadProfileImage(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest(new { message = "File tidak boleh kosong" });

                // Validasi tipe file
                var allowedTypes = new[] { "image/jpeg", "image/png", "image/jpg" };
                if (!allowedTypes.Contains(file.ContentType))
                    return BadRequest(new { message = "Tipe file tidak didukung, gunakan JPG atau PNG" });

                // Validasi ukuran file (maks 5MB)
                if (file.Length > 5 * 1024 * 1024)
                    return BadRequest(new { message = "Ukuran file tidak boleh lebih dari 5MB" });

                // Ambil user id dari claim
                var userIdClaim = User.FindFirst("userId")?.Value;

                if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
                    return Unauthorized(new { message = "User tidak valid" });

                // Konversi ke byte array
                byte[] imageData;
                using (var ms = new MemoryStream())
                {
                    await file.CopyToAsync(ms);
                    imageData = ms.ToArray();
                }

                // Update di database
                var ctx = new UsersContext(_constr);
                bool success = ctx.UpdateProfileImage(userId, imageData);

                if (success)
                {
                    var user = ctx.GetUserById(userId); // Get updated user info
                    return Ok(new
                    {
                        message = "Foto profil berhasil diupload",
                        is_verified = user?.IsVerified ?? false // Tambahkan status verifikasi
                    });
                }

                return StatusCode(500, new { message = "Gagal menyimpan foto profil" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        [HttpGet("ktp")]
        public IActionResult GetKtpImage()
        {
            // Ambil user id dari claim
            var userIdClaim = User.FindFirst("user_id")?.Value;

            if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized(new { message = "User tidak valid" });

            var ctx = new UsersContext(_constr);
            var user = ctx.GetUserById(userId);

            if (user == null || user.KtpImage == null || user.KtpImage.Length == 0)
                return NotFound(new { message = "KTP tidak ditemukan" });

            return File(user.KtpImage, "image/jpeg");
        }

        [HttpGet("sim")]
        public IActionResult GetSimImage()
        {
            // Ambil user id dari claim
            var userIdClaim = User.FindFirst("user_id")?.Value;

            if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized(new { message = "User tidak valid" });

            var ctx = new UsersContext(_constr);
            var user = ctx.GetUserById(userId);

            if (user == null || user.SimImage == null || user.SimImage.Length == 0)
                return NotFound(new { message = "SIM tidak ditemukan" });

            return File(user.SimImage, "image/jpeg");
        }

        // Get profile image - mirip dengan KTP/SIM
        [HttpGet("profile-image")]
        [Authorize]
        public IActionResult GetProfileImage()
        {
            try
            {
                var userIdClaim = User.FindFirst("userId")?.Value;

                if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
                    return Unauthorized(new { message = "User tidak valid" });

                var ctx = new UsersContext(_constr);
                var user = ctx.GetUserById(userId);

                if (user == null)
                    return NotFound(new { message = "User tidak ditemukan" });

                if (user.Profile == null || user.Profile.Length == 0)
                    return NotFound(new { message = "Foto profil tidak ditemukan" });

                return File(user.Profile, "image/jpeg");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // Method GetVerificationStatus - implementasi lengkap

        [HttpGet("verification-status")]
        [Authorize]
        public IActionResult GetVerificationStatus()
        {
            try
            {
                var userIdClaim = User.FindFirst("userId")?.Value;

                if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
                    return Unauthorized(new { message = "User tidak valid" });

                var ctx = new UsersContext(_constr);
                var user = ctx.GetUserById(userId);

                if (user == null)
                    return NotFound(new { message = "User tidak ditemukan" });

                bool hasKtp = user.KtpImage != null && user.KtpImage.Length > 0;
                bool hasSim = user.SimImage != null && user.SimImage.Length > 0;

                return Ok(new
                {
                    is_verified = user.IsVerified,
                    has_ktp = hasKtp,
                    has_sim = hasSim,
                    documents_submitted = hasKtp && hasSim,
                    can_book = user.IsVerified
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (ResourceNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }
    }
}