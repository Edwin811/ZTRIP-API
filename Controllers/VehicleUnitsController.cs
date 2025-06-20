using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Z_TRIP.Models;
using System.IO;
using System.Threading.Tasks;
using Z_TRIP.Exceptions;
using System.ComponentModel.DataAnnotations;  // Untuk validasi
using System.Text.Json.Serialization; // Untuk [JsonIgnore]
using Z_TRIP.Models.Contexts;

namespace Z_TRIP.Controllers
{
    public class VehicleUnitRequest
    {
        [Required(ErrorMessage = "Code tidak boleh kosong")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "VehicleId harus diisi")]
        public int VehicleId { get; set; }

        [Required(ErrorMessage = "PricePerDay harus diisi")]
        [Range(0, double.MaxValue, ErrorMessage = "PricePerDay tidak boleh negatif")]
        public decimal PricePerDay { get; set; }

        public string? Description { get; set; }

        [JsonIgnore] // <-- ini akan menyembunyikan dari dokumentasi Swagger dan serialisasi/deserialisasi JSON
        public IFormFile? Image { get; set; }
    }

    [Route("api/vehicle-units")]
    [ApiController]
    public class VehicleUnitsController : ControllerBase
    {
        private readonly string _constr;

        public VehicleUnitsController(IConfiguration config)
        {
            _constr = config.GetConnectionString("koneksi")!;
        }

        // GET api/vehicle-units
        [HttpGet]
        public IActionResult GetAll()
        {
            try
            {
                var context = new VehicleUnitsContext(_constr);
                var units = context.GetAllVehicleUnits();
                var result = units.Select(u => new
                {
                    u.Id,
                    u.Code,
                    u.VehicleId,
                    u.PricePerDay,
                    u.Description,
                    HasImage = u.VehicleImage != null && u.VehicleImage.Length > 0,
                    u.CreatedAt,
                    u.UpdatedAt
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // GET api/vehicle-units/{id}
        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            try
            {
                // Add validation for ID parameter
                if (id <= 0)
                    return BadRequest(new { message = "ID unit kendaraan harus lebih dari 0" });

                var context = new VehicleUnitsContext(_constr);
                var unit = context.GetVehicleUnitById(id);

                if (unit == null)
                    throw new ResourceNotFoundException($"Unit kendaraan dengan ID {id} tidak ditemukan");

                // Tidak perlu mengirim binary data langsung
                var result = new
                {
                    unit.Id,
                    unit.Code,
                    unit.VehicleId,
                    unit.PricePerDay,
                    unit.Description,
                    HasImage = unit.VehicleImage != null && unit.VehicleImage.Length > 0,
                    unit.CreatedAt,
                    unit.UpdatedAt
                };

                return Ok(result);
            }
            catch (ResourceNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                throw; // Middleware global akan menangani
            }
        }

        // GET api/vehicle-units/image/{id}
        [HttpGet("image/{id}")]
        public IActionResult GetImage(int id)
        {
            try
            {
                // Add validation for ID parameter
                if (id <= 0)
                    return BadRequest(new { message = "ID unit kendaraan harus lebih dari 0" });

                var context = new VehicleUnitsContext(_constr);
                var unit = context.GetVehicleUnitById(id);

                if (unit == null || unit.VehicleImage == null || unit.VehicleImage.Length == 0)
                    return NotFound(new { message = "Gambar kendaraan tidak ditemukan" });

                return File(unit.VehicleImage, "image/jpeg");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // POST api/vehicle-units - Perbarui untuk menggunakan VehicleUnitRequest
        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Add([FromForm] VehicleUnitRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest(new { message = "Data unit kendaraan tidak boleh kosong" });

                if (string.IsNullOrEmpty(request.Code))
                    return BadRequest(new { message = "Code tidak boleh kosong" });

                // Add validation for code format
                if (request.Code.Length > 50)
                    return BadRequest(new { message = "Code terlalu panjang, maksimal 50 karakter" });

                // Add validation for VehicleId
                if (request.VehicleId <= 0)
                    return BadRequest(new { message = "ID kendaraan tidak valid" });

                // Add validation for PricePerDay
                if (request.PricePerDay <= 0)
                    return BadRequest(new { message = "Harga per hari harus lebih dari 0" });

                // Add validation for reasonable price
                if (request.PricePerDay > 10000000) // 10 million (adjust as needed)
                    return BadRequest(new { message = "Harga per hari terlalu besar" });

                // Add validation for description length if provided
                if (!string.IsNullOrEmpty(request.Description) && request.Description.Length > 500)
                    return BadRequest(new { message = "Deskripsi terlalu panjang, maksimal 500 karakter" });

                // Check if vehicle exists
                var vehicleCtx = new VehicleContext(_constr);
                var vehicle = vehicleCtx.GetVehicleById(request.VehicleId);
                if (vehicle == null)
                    return BadRequest(new { message = $"Kendaraan dengan ID {request.VehicleId} tidak ditemukan" });

                var unit = new VehicleUnit
                {
                    Code = request.Code,
                    VehicleId = request.VehicleId,
                    PricePerDay = request.PricePerDay,
                    Description = request.Description
                };

                // Handle image upload if provided
                if (request.Image != null)
                {
                    // Validasi tipe file
                    var allowedTypes = new[] { "image/jpeg", "image/png", "image/jpg" };
                    if (!allowedTypes.Contains(request.Image.ContentType))
                        return BadRequest(new { message = "Tipe file tidak didukung, gunakan JPG atau PNG" });

                    // Validasi ukuran file (maks 5MB)
                    if (request.Image.Length > 5 * 1024 * 1024)
                        return BadRequest(new { message = "Ukuran file tidak boleh lebih dari 5MB" });

                    // Convert image to byte array
                    using var ms = new MemoryStream();
                    await request.Image.CopyToAsync(ms);
                    unit.VehicleImage = ms.ToArray();
                }

                var context = new VehicleUnitsContext(_constr);
                int id = context.AddVehicleUnit(unit);

                if (id > 0)
                {
                    return CreatedAtAction(nameof(GetById), new { id }, new
                    {
                        id,
                        message = "Vehicle unit berhasil ditambahkan",
                        hasImage = unit.VehicleImage != null
                    });
                }

                return StatusCode(500, new { message = "Gagal menambahkan vehicle unit" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // PUT api/vehicle-units/{id} - Perbarui untuk menggunakan VehicleUnitRequest
        [HttpPut("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult Update(int id, [FromBody] VehicleUnitRequest request)
        {
            try
            {
                // Add validation for ID parameter
                if (id <= 0)
                    return BadRequest(new { message = "ID unit kendaraan harus lebih dari 0" });

                if (request == null)
                    return BadRequest(new { message = "Data unit kendaraan tidak boleh kosong" });

                if (string.IsNullOrEmpty(request.Code))
                    return BadRequest(new { message = "Code tidak boleh kosong" });

                // Add validation for code format
                if (request.Code.Length > 50)
                    return BadRequest(new { message = "Code terlalu panjang, maksimal 50 karakter" });

                // Add validation for VehicleId
                if (request.VehicleId <= 0)
                    return BadRequest(new { message = "ID kendaraan tidak valid" });

                // Add validation for PricePerDay
                if (request.PricePerDay <= 0)
                    return BadRequest(new { message = "Harga per hari harus lebih dari 0" });

                // Add validation for reasonable price
                if (request.PricePerDay > 10000000) // 10 million (adjust as needed)
                    return BadRequest(new { message = "Harga per hari terlalu besar" });

                // Add validation for description length if provided
                if (!string.IsNullOrEmpty(request.Description) && request.Description.Length > 500)
                    return BadRequest(new { message = "Deskripsi terlalu panjang, maksimal 500 karakter" });

                // Check if vehicle exists
                var vehicleCtx = new VehicleContext(_constr);
                var vehicle = vehicleCtx.GetVehicleById(request.VehicleId);
                if (vehicle == null)
                    return BadRequest(new { message = $"Kendaraan dengan ID {request.VehicleId} tidak ditemukan" });

                // Check if the unit exists before updating
                var context = new VehicleUnitsContext(_constr);
                var existingUnit = context.GetVehicleUnitById(id);
                if (existingUnit == null)
                    return NotFound(new { message = $"Unit kendaraan dengan ID {id} tidak ditemukan" });

                var unit = new VehicleUnit
                {
                    Id = id,
                    Code = request.Code,
                    VehicleId = request.VehicleId,
                    PricePerDay = request.PricePerDay,
                    Description = request.Description
                };

                // Tidak perlu mengubah/mengisi VehicleImage di sini!
                bool updated = context.UpdateVehicleUnit(unit);

                if (updated)
                {
                    return Ok(new { message = "Vehicle unit berhasil diperbarui" });
                }

                return NotFound(new { message = "Vehicle unit tidak ditemukan" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // DELETE api/vehicle-units/{id}
        [HttpDelete("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult Delete(int id)
        {
            try
            {
                // Add validation for ID parameter
                if (id <= 0)
                    return BadRequest(new { message = "ID unit kendaraan harus lebih dari 0" });

                // Check if the unit exists before deleting
                var context = new VehicleUnitsContext(_constr);
                var existingUnit = context.GetVehicleUnitById(id);
                if (existingUnit == null)
                    return NotFound(new { message = $"Unit kendaraan dengan ID {id} tidak ditemukan" });

                // Check for active bookings using a try-catch to handle potential missing method
                try
                {
                    var bookingCtx = new BookingContext(_constr);

                    // Try to get all bookings first, then filter by vehicle unit ID manually
                    var allBookings = bookingCtx.GetAllBookings();

                    // Filter manually
                    var activeBookings = allBookings
                        .Where(b => b.VehicleUnitId == id) // Filter by VehicleUnitId
                        .Where(b => b.Status == booking_status_enum.pending ||
                                    b.Status == booking_status_enum.approved ||
                                    b.Status == booking_status_enum.on_going)
                        .ToList();

                    if (activeBookings.Count > 0)
                        return BadRequest(new
                        {
                            message = "Tidak dapat menghapus unit kendaraan karena masih memiliki booking aktif",
                            activeBookingCount = activeBookings.Count
                        });
                }
                catch (Exception ex)
                {
                    // Log the error but continue with deletion
                    Console.WriteLine($"Warning: Could not check for active bookings: {ex.Message}");

                    // Optionally inform the admin
                    return BadRequest(new
                    {
                        message = "Tidak dapat memeriksa booking aktif. Silakan periksa secara manual sebelum menghapus.",
                        error = "Metode GetBookingsByVehicleUnitId tidak tersedia di BookingContext"
                    });
                }

                bool deleted = context.DeleteVehicleUnit(id);

                if (deleted)
                {
                    return Ok(new { message = "Vehicle unit berhasil dihapus" });
                }

                return NotFound(new { message = "Vehicle unit tidak ditemukan" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // POST api/vehicle-units/upload-image/{id}
        [HttpPost("upload-image/{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> UploadImage(int id, IFormFile file)
        {
            try
            {
                // Validasi parameter
                if (id <= 0)
                    return BadRequest(new { message = "ID kendaraan tidak valid" });

                // Validasi file
                if (file == null || file.Length == 0)
                    return BadRequest(new { message = "File tidak boleh kosong" });

                // Validasi format file
                var allowedTypes = new[] { "image/jpeg", "image/png", "image/jpg" };
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (string.IsNullOrEmpty(extension) ||
                    !new[] { ".jpg", ".jpeg", ".png" }.Contains(extension))
                    return BadRequest(new { message = "Format file harus jpg, jpeg, atau png" });

                if (!allowedTypes.Contains(file.ContentType))
                    return BadRequest(new { message = "Tipe file tidak valid" });

                if (file.Length > 5 * 1024 * 1024)
                    return BadRequest(new { message = "Ukuran file tidak boleh lebih dari 5MB" });

                // Periksa apakah unit ada
                var context = new VehicleUnitsContext(_constr);
                var unit = context.GetVehicleUnitById(id);

                if (unit == null)
                    return NotFound(new { message = "Unit kendaraan tidak ditemukan" });

                // Konversi file ke byte array
                byte[] imageData;
                using (var ms = new MemoryStream())
                {
                    await file.CopyToAsync(ms);
                    imageData = ms.ToArray();
                }

                // Simpan gambar ke database
                if (context.UpdateVehicleImage(id, imageData))
                {
                    return Ok(new { message = "Gambar berhasil diunggah" });
                }

                return StatusCode(500, new { message = "Gagal mengunggah gambar" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // DELETE api/vehicle-units/image/{id}
        [HttpDelete("image/{id}")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult DeleteImage(int id)
        {
            try
            {
                // Add validation for ID parameter
                if (id <= 0)
                    return BadRequest(new { message = "ID unit kendaraan harus lebih dari 0" });

                var context = new VehicleUnitsContext(_constr);
                var unit = context.GetVehicleUnitById(id);

                if (unit == null)
                    return NotFound(new { message = $"Unit kendaraan dengan ID {id} tidak ditemukan" });

                if (unit.VehicleImage == null || unit.VehicleImage.Length == 0)
                    return BadRequest(new { message = "Tidak ada gambar untuk dihapus" });

                // Update dengan null untuk menghapus gambar
                if (context.UpdateVehicleImage(id, null))
                    return Ok(new { message = "Gambar kendaraan berhasil dihapus" });

                return StatusCode(500, new { message = "Gagal menghapus gambar" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }
    }
}
