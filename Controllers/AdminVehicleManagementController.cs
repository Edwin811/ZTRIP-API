using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Z_TRIP.Models;
using Z_TRIP.Exceptions;
using System.Globalization;
using Z_TRIP.Models.Contexts;
using System.Text.Json;

namespace Z_TRIP.Controllers
{
    [ApiController]
    [Route("api/admin/vehicle-management")]
    [Authorize(Policy = "AdminOnly")]
    public class AdminVehicleManagementController : ControllerBase
    {
        private const string DATE_FORMAT = "yyyyMMdd";
        private readonly string _constr;

        public AdminVehicleManagementController(IConfiguration config)
        {
            _constr = config.GetConnectionString("koneksi")!;
        }

        private DateTime ParseYYYYMMDD(string? dateStr, string paramName)
        {
            if (string.IsNullOrEmpty(dateStr))
            {
                return DateTime.Today;
            }

            if (dateStr.Length != 8 || !DateTime.TryParseExact(
                dateStr,
                DATE_FORMAT,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime result))
            {
                throw new ValidationException($"Format {paramName} harus {DATE_FORMAT}");
            }
            return result;
        }

        private string FormatToYYYYMMDD(DateTime date)
        {
            return date.ToString(DATE_FORMAT);
        }

        // GET: api/admin/vehicle-management/schedule/{vehicleUnitId}
        [HttpGet("schedule/{vehicleUnitId}")]
        public IActionResult GetVehicleSchedule(int vehicleUnitId, [FromQuery] string? startDate, [FromQuery] string? endDate)
        {
            try
            {
                // Validate vehicle unit ID
                if (vehicleUnitId <= 0)
                {
                    return BadRequest(new { message = "ID unit kendaraan tidak valid" });
                }

                var start = ParseYYYYMMDD(startDate, "startDate");
                var end = ParseYYYYMMDD(endDate, "endDate");

                // Validasi vehicle unit
                var unitCtx = new VehicleUnitsContext(_constr);
                var unit = unitCtx.GetVehicleUnitById(vehicleUnitId);

                if (unit == null)
                    throw new ResourceNotFoundException($"Unit kendaraan dengan ID {vehicleUnitId} tidak ditemukan");

                // Dapatkan informasi vehicle (model kendaraan) untuk unit ini
                var vehicleCtx = new VehicleContext(_constr);
                var vehicle = vehicleCtx.GetVehicleById(unit.VehicleId);

                if (vehicle == null)
                    throw new ResourceNotFoundException($"Informasi kendaraan untuk unit {vehicleUnitId} tidak ditemukan");

                // Tentukan rentang waktu default jika tidak disediakan
                DateTime startDateTime = start.Date;
                DateTime endDateTime = end.Date.AddDays(1).AddSeconds(-1);

                if (endDateTime < startDateTime)
                    throw new ValidationException("Tanggal akhir harus setelah tanggal awal");

                if ((endDateTime.Date - startDateTime.Date).TotalDays > 90)
                    throw new ValidationException("Rentang tanggal maksimal 90 hari");

                // Ambil semua booking untuk unit ini dalam rentang tanggal
                var bookingCtx = new BookingContext(_constr);
                var bookings = bookingCtx.GetBookingsByVehicleUnitAndDateRange(vehicleUnitId, startDateTime, endDateTime);

                // Format bookings untuk response
                var bookingsList = bookings.Select(b =>
                {
                    // Dapatkan informasi user yang booking jika bukan booking dari admin
                    string bookedBy = "Admin (Blocked)";
                    if (!string.IsNullOrEmpty(b.StatusNote) && b.StatusNote.Contains("BLOCKED_BY_ADMIN"))
                    {
                        bookedBy = "Admin (Blocked)";
                    }
                    else
                    {
                        var userCtx = new UsersContext(_constr);
                        var user = userCtx.GetUserById(b.UserId);
                        bookedBy = user?.Name ?? "Unknown User";
                    }

                    return new
                    {
                        b.Id,
                        StartDate = FormatToYYYYMMDD(b.StartDatetime),
                        EndDate = FormatToYYYYMMDD(b.EndDatetime),
                        Status = b.Status.ToString(),
                        b.StatusNote,
                        BookedBy = bookedBy,
                        IsAdminBlocked = b.StatusNote?.Contains("BLOCKED_BY_ADMIN") == true
                    };
                }).ToList();

                return Ok(new
                {
                    VehicleUnitId = vehicleUnitId,
                    UnitCode = unit.Code,
                    UnitPricePerDay = unit.PricePerDay,
                    UnitImage = unit.VehicleImage != null ? Convert.ToBase64String(unit.VehicleImage) : null,
                    VehicleInfo = new
                    {
                        vehicle.Id,
                        vehicle.Name,
                        vehicle.Merk,
                        Category = vehicle.Category.ToString(),
                        vehicle.Description,
                        vehicle.Capacity
                    },
                    DateRange = new
                    {
                        StartDate = FormatToYYYYMMDD(start),
                        EndDate = FormatToYYYYMMDD(end)
                    },
                    Bookings = bookingsList
                });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
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

        // POST: api/admin/vehicle-management/block-schedule
        [HttpPost("block-schedule")]
        public IActionResult BlockSchedule([FromBody] JsonElement requestBody)
        {
            try
            {
                // Check if request body is empty/null
                if (requestBody.ValueKind == JsonValueKind.Undefined ||
                    requestBody.ValueKind == JsonValueKind.Null)
                {
                    return BadRequest(new { message = "Request body tidak boleh kosong" });
                }

                // Parse the JsonElement instead of using ScheduleBlockRequest class
                if (!requestBody.TryGetProperty("vehicleUnitIds", out JsonElement vehicleUnitIdsElement) ||
                    vehicleUnitIdsElement.ValueKind != JsonValueKind.Array)
                {
                    throw new ValidationException("vehicleUnitIds harus berupa array");
                }

                // Extract vehicle unit IDs
                var vehicleUnitIds = new List<int>();
                foreach (JsonElement element in vehicleUnitIdsElement.EnumerateArray())
                {
                    if (element.TryGetInt32(out int unitId))
                    {
                        vehicleUnitIds.Add(unitId);
                    }
                }

                if (vehicleUnitIds.Count == 0)
                {
                    throw new ValidationException("Setidaknya satu ID unit kendaraan harus disediakan");
                }

                // Extract start date
                string startDate = string.Empty;
                if (requestBody.TryGetProperty("startDate", out JsonElement startDateElement) &&
                    startDateElement.ValueKind == JsonValueKind.String)
                {
                    startDate = startDateElement.GetString() ?? string.Empty;
                }
                else
                {
                    throw new ValidationException("StartDate diperlukan (format: YYYYMMDD)");
                }

                // Extract end date
                string endDate = string.Empty;
                if (requestBody.TryGetProperty("endDate", out JsonElement endDateElement) &&
                    endDateElement.ValueKind == JsonValueKind.String)
                {
                    endDate = endDateElement.GetString() ?? string.Empty;
                }
                else
                {
                    throw new ValidationException("EndDate diperlukan (format: YYYYMMDD)");
                }

                // Extract note (optional)
                string note = "Kendaraan sedang dalam perbaikan"; // Default note
                if (requestBody.TryGetProperty("note", out JsonElement noteElement) &&
                    noteElement.ValueKind == JsonValueKind.String)
                {
                    note = noteElement.GetString() ?? note;
                }

                // Parse the dates
                var startDateTime = ParseYYYYMMDD(startDate, "StartDate").Date;
                var endDateTime = ParseYYYYMMDD(endDate, "EndDate").Date.AddDays(1).AddSeconds(-1);

                if (startDateTime >= endDateTime)
                    throw new ValidationException("Tanggal mulai harus sebelum tanggal selesai");

                // Dapatkan ID admin dari token
                var adminId = int.Parse(User.FindFirst("userId")?.Value ?? "0");
                if (adminId == 0)
                    return Unauthorized(new { message = "User tidak valid" });

                // Verifikasi admin
                var userCtx = new UsersContext(_constr);
                var admin = userCtx.GetUserById(adminId);

                if (admin == null || !admin.Role)
                    return Forbid();

                // Inisialisasi context yang dibutuhkan
                var bookingCtx = new BookingContext(_constr);
                var unitCtx = new VehicleUnitsContext(_constr);
                var txnCtx = new TransaksiContext(_constr);

                var results = new List<object>();
                var blockedUnitIds = new List<int>();
                var failedUnitIds = new List<int>();

                // Proses setiap unit kendaraan
                foreach (var unitId in vehicleUnitIds)
                {
                    // Validasi unit kendaraan
                    var unit = unitCtx.GetVehicleUnitById(unitId);
                    if (unit == null)
                    {
                        failedUnitIds.Add(unitId);
                        results.Add(new { UnitId = unitId, Status = "Failed", Message = "Unit tidak ditemukan" });
                        continue;
                    }

                    // Periksa apakah ada konflik jadwal
                    var conflictingBookings = bookingCtx.GetBookingsByVehicleUnitAndDateRange(
                        unitId, startDateTime, endDateTime);

                    var activeConflicts = conflictingBookings
                        .Where(b => b.Status != booking_status_enum.rejected && b.Status != booking_status_enum.done)
                        .ToList();

                    if (activeConflicts.Any())
                    {
                        failedUnitIds.Add(unitId);
                        results.Add(new
                        {
                            UnitId = unitId,
                            Status = "Failed",
                            Message = "Terdapat jadwal yang konflik",
                            Conflicts = activeConflicts.Select(b => new
                            {
                                b.Id,
                                StartDate = FormatToYYYYMMDD(b.StartDatetime),
                                EndDate = FormatToYYYYMMDD(b.EndDatetime),
                                Status = b.Status.ToString()
                            }).ToList()
                        });
                        continue;
                    }

                    // Buat transaksi dummy untuk booking block
                    var transaction = new Transaksi
                    {
                        Method = payment_method_enum.QRIS,
                        PaymentStatus = payment_status_enum.paid, // Tidak perlu pembayaran untuk block
                        Amount = 0 // Block tidak perlu pembayaran
                    };

                    var txnId = txnCtx.AddTransaksi(transaction);

                    if (txnId <= 0)
                    {
                        failedUnitIds.Add(unitId);
                        results.Add(new
                        {
                            UnitId = unitId,
                            Status = "Failed",
                            Message = "Gagal membuat transaksi"
                        });
                        continue;
                    }

                    // Buat booking block
                    var booking = new Booking
                    {
                        UserId = adminId, // Admin yang memblokir
                        VehicleUnitId = unitId,
                        StartDatetime = startDateTime, // Gunakan tanggal awal hari
                        EndDatetime = endDateTime,     // Gunakan tanggal akhir hari
                        Status = booking_status_enum.approved, // Langsung approved karena admin yang membuat
                        StatusNote = $"BLOCKED_BY_ADMIN: {note}", // Tambahkan note
                        TransactionId = txnId
                    };

                    var createdBooking = bookingCtx.CreateBooking(booking);

                    if (createdBooking != null)
                    {
                        blockedUnitIds.Add(unitId);
                        results.Add(new
                        {
                            UnitId = unitId,
                            BookingId = createdBooking.Id,
                            Status = "Success",
                            Message = "Jadwal berhasil diblokir",
                            BlockedPeriod = new
                            {
                                StartDate = FormatToYYYYMMDD(startDateTime),
                                EndDate = FormatToYYYYMMDD(endDateTime)
                            }
                        });
                    }
                    else
                    {
                        failedUnitIds.Add(unitId);
                        results.Add(new
                        {
                            UnitId = unitId,
                            Status = "Failed",
                            Message = "Gagal membuat booking block"
                        });

                        // Hapus transaksi jika booking gagal
                        txnCtx.DeleteTransaksi(txnId);
                    }
                }

                return Ok(new
                {
                    message = "Proses pemblokiran jadwal selesai",
                    success_count = blockedUnitIds.Count,
                    failed_count = failedUnitIds.Count,
                    details = results
                });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // DELETE: api/admin/vehicle-management/unblock-schedule/{bookingId}
        [HttpDelete("unblock-schedule/{bookingId}")]
        public IActionResult UnblockSchedule(int bookingId)
        {
            try
            {
                // Validate booking ID
                if (bookingId <= 0)
                {
                    return BadRequest(new { message = "ID booking tidak valid" });
                }

                var bookingCtx = new BookingContext(_constr);
                var booking = bookingCtx.GetBookingById(bookingId);

                if (booking == null)
                    throw new ResourceNotFoundException("Jadwal yang diblokir tidak ditemukan");

                // Verifikasi bahwa ini adalah booking yang diblokir oleh admin
                if (booking.StatusNote == null || !booking.StatusNote.Contains("BLOCKED_BY_ADMIN"))
                    throw new ValidationException("Booking ini bukan blokir jadwal oleh admin");

                // Hapus booking dan transaksi terkait
                bool deleted = bookingCtx.DeleteBooking(bookingId);

                if (deleted && booking.TransactionId.HasValue)
                {
                    var txnCtx = new TransaksiContext(_constr);
                    txnCtx.DeleteTransaksi(booking.TransactionId.Value);
                }

                return deleted
                    ? Ok(new { message = "Jadwal berhasil dibuka kembali" })
                    : StatusCode(500, new { message = "Gagal membuka jadwal" });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
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

        // PUT: api/admin/vehicle-management/update-schedule/{bookingId}
        [HttpPut("update-schedule/{bookingId}")]
        public IActionResult UpdateBlockedSchedule(int bookingId, [FromBody] JsonElement requestBody)
        {
            try
            {
                // Validate booking ID
                if (bookingId <= 0)
                {
                    return BadRequest(new { message = "ID booking tidak valid" });
                }

                // Check if request body is empty
                if (requestBody.ValueKind == JsonValueKind.Undefined ||
                    requestBody.ValueKind == JsonValueKind.Null)
                {
                    return BadRequest(new { message = "Request body tidak boleh kosong" });
                }

                // Parse request body manually instead of using UpdateScheduleRequest
                DateTime startDate;
                DateTime endDate;
                string? note = null;

                // Extract StartDate
                if (!requestBody.TryGetProperty("startDate", out JsonElement startDateElement) ||
                    startDateElement.ValueKind != JsonValueKind.String)
                {
                    throw new ValidationException("StartDate diperlukan");
                }

                if (!DateTime.TryParse(startDateElement.GetString(), out startDate))
                {
                    throw new ValidationException("Format StartDate tidak valid");
                }

                // Extract EndDate
                if (!requestBody.TryGetProperty("endDate", out JsonElement endDateElement) ||
                    endDateElement.ValueKind != JsonValueKind.String)
                {
                    throw new ValidationException("EndDate diperlukan");
                }

                if (!DateTime.TryParse(endDateElement.GetString(), out endDate))
                {
                    throw new ValidationException("Format EndDate tidak valid");
                }

                // Extract Note (optional)
                if (requestBody.TryGetProperty("note", out JsonElement noteElement) &&
                    noteElement.ValueKind == JsonValueKind.String)
                {
                    note = noteElement.GetString();
                }
                else
                {
                    note = "Kendaraan sedang dalam perbaikan"; // Default note
                }

                // Standarisasi tanggal tanpa jam
                startDate = startDate.Date;
                endDate = endDate.Date.AddDays(1).AddSeconds(-1); // Hingga akhir hari

                if (startDate >= endDate)
                    throw new ValidationException("Tanggal mulai harus sebelum tanggal selesai");

                var bookingCtx = new BookingContext(_constr);
                var booking = bookingCtx.GetBookingById(bookingId);

                if (booking == null)
                    throw new ResourceNotFoundException("Jadwal tidak ditemukan");

                // Verifikasi bahwa ini adalah booking yang diblokir oleh admin
                bool isBlockedByAdmin = booking.StatusNote?.Contains("BLOCKED_BY_ADMIN") == true;

                if (!isBlockedByAdmin && !User.IsInRole("Admin"))
                    throw new ValidationException("Hanya jadwal yang diblokir oleh admin yang dapat diupdate melalui endpoint ini");

                // Periksa konflik dengan jadwal lain (selain booking ini sendiri)
                var conflictingBookings = bookingCtx.GetBookingsByVehicleUnitAndDateRange(
                    booking.VehicleUnitId, startDate, endDate);

                var activeConflicts = conflictingBookings
                    .Where(b => b.Id != bookingId &&
                                b.Status != booking_status_enum.rejected &&
                                b.Status != booking_status_enum.done)
                    .ToList();

                if (activeConflicts.Any())
                    throw new ValidationException("Terdapat jadwal lain yang konflik dengan jadwal baru");

                // Update jadwal
                booking.StartDatetime = startDate;
                booking.EndDatetime = endDate;

                if (isBlockedByAdmin)
                {
                    // Jika ini adalah blocked schedule, update note
                    booking.StatusNote = $"BLOCKED_BY_ADMIN: {note}";
                }
                else if (note != null)
                {
                    // Jika booking normal, tambahkan note tanpa menghapus informasi sebelumnya
                    booking.StatusNote = note;
                }

                bool updated = bookingCtx.UpdateBooking(bookingId, booking);

                return updated
                    ? Ok(new
                    {
                        message = "Jadwal berhasil diperbarui",
                        BlockedPeriod = new
                        {
                            StartDate = FormatToYYYYMMDD(startDate),
                            EndDate = FormatToYYYYMMDD(endDate)
                        }
                    })
                    : StatusCode(500, new { message = "Gagal memperbarui jadwal" });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
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

        // GET: api/admin/vehicle-management/blocked-schedules
        [HttpGet("blocked-schedules")]
        public IActionResult GetBlockedSchedules([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null, [FromQuery] int? vehicleUnitId = null)
        {
            try
            {
                // Add parameter validation
                if (startDate.HasValue && endDate.HasValue && startDate > endDate)
                {
                    return BadRequest(new { message = "Tanggal mulai tidak boleh setelah tanggal selesai" });
                }

                if (vehicleUnitId.HasValue && vehicleUnitId <= 0)
                {
                    return BadRequest(new { message = "ID unit kendaraan harus lebih besar dari 0" });
                }

                // Rest of your existing code remains unchanged
                var bookingCtx = new BookingContext(_constr);
                var unitCtx = new VehicleUnitsContext(_constr);
                var vehicleCtx = new VehicleContext(_constr);

                // Semua parameter opsional, tidak perlu validasi tanggal
                var blockedSchedules = bookingCtx.GetBlockedSchedules(startDate, endDate, vehicleUnitId);

                var result = blockedSchedules.Select(b =>
                {
                    // Get unit info
                    var unit = unitCtx.GetVehicleUnitById(b.VehicleUnitId);
                    var vehicleName = "Unknown";
                    var vehicleMerk = "Unknown";

                    if (unit != null)
                    {
                        var vehicle = vehicleCtx.GetVehicleById(unit.VehicleId);
                        if (vehicle != null)
                        {
                            vehicleName = vehicle.Name;
                            vehicleMerk = vehicle.Merk;
                        }
                    }

                    return new
                    {
                        BlockId = b.Id,
                        VehicleUnitId = b.VehicleUnitId,
                        VehicleUnitCode = unit?.Code,
                        VehicleName = vehicleName,
                        VehicleMerk = vehicleMerk,
                        StartDate = FormatToYYYYMMDD(b.StartDatetime),
                        EndDate = FormatToYYYYMMDD(b.EndDatetime),
                        Note = b.StatusNote?.Replace("BLOCKED_BY_ADMIN: ", ""),
                        CreatedAt = FormatToYYYYMMDD(b.CreatedAt),
                        UpdatedAt = FormatToYYYYMMDD(b.UpdatedAt)
                    };
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // GET: api/admin/vehicle-management/all-blocked-schedules
        [HttpGet("all-blocked-schedules")]
        public IActionResult GetAllBlockedSchedules([FromQuery] int? vehicleUnitId = null)
        {
            try
            {
                // Validate vehicle unit ID if provided
                if (vehicleUnitId.HasValue && vehicleUnitId <= 0)
                {
                    return BadRequest(new { message = "ID unit kendaraan harus lebih besar dari 0" });
                }

                var bookingCtx = new BookingContext(_constr);
                var unitCtx = new VehicleUnitsContext(_constr);
                var vehicleCtx = new VehicleContext(_constr);

                // Dapatkan semua jadwal yang diblokir tanpa batasan tanggal
                var blockedSchedules = bookingCtx.GetAllBlockedSchedules(vehicleUnitId);

                // Kumpulkan informasi unit kendaraan untuk menampilkan detail lengkap
                var result = blockedSchedules.Select(b =>
                {
                    // Get unit info
                    var unit = unitCtx.GetVehicleUnitById(b.VehicleUnitId);
                    var vehicleName = "Unknown";
                    var vehicleMerk = "Unknown";

                    if (unit != null)
                    {
                        var vehicle = vehicleCtx.GetVehicleById(unit.VehicleId);
                        if (vehicle != null)
                        {
                            vehicleName = vehicle.Name;
                            vehicleMerk = vehicle.Merk;
                        }
                    }

                    return new
                    {
                        BlockId = b.Id,
                        VehicleUnitId = b.VehicleUnitId,
                        VehicleUnitCode = unit?.Code,
                        VehicleName = vehicleName,
                        VehicleMerk = vehicleMerk,
                        StartDate = FormatToYYYYMMDD(b.StartDatetime),
                        EndDate = FormatToYYYYMMDD(b.EndDatetime),
                        Note = b.StatusNote?.Replace("BLOCKED_BY_ADMIN: ", ""),
                        CreatedAt = FormatToYYYYMMDD(b.CreatedAt),
                        UpdatedAt = FormatToYYYYMMDD(b.UpdatedAt)
                    };
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // GET: api/admin/vehicle-management/all-blocked-dates
        [HttpGet("all-blocked-dates")]
        public IActionResult GetAllBlockedDates([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
        {
            try
            {
                // Add parameter validation
                if (startDate.HasValue && endDate.HasValue && startDate > endDate)
                {
                    return BadRequest(new { message = "Tanggal mulai tidak boleh setelah tanggal selesai" });
                }

                // Rest of your existing code remains unchanged
                var bookingCtx = new BookingContext(_constr);

                // Parameter opsional, tidak perlu validasi tanggal
                var blockedDates = bookingCtx.GetAllBlockedDates(startDate, endDate);

                // Group berdasarkan tanggal untuk mengetahui semua unit yang diblokir pada tanggal tertentu
                var groupedByDate = blockedDates
                    .GroupBy(item => new
                    {
                        Date = item.Date.ToString("yyyyMMdd")
                    })
                    .Select(group => new
                    {
                        Date = group.Key.Date,
                        VehicleUnits = group.Select(b => new
                        {
                            VehicleUnitId = b.VehicleUnitId,
                            VehicleUnitCode = b.VehicleUnitCode,
                            VehicleName = b.VehicleName,
                            VehicleMerk = b.VehicleMerk,
                            Note = b.Note,
                            BookingId = b.BookingId
                        }).ToList(),
                        BlockedCount = group.Count()
                    })
                    .OrderBy(g => g.Date)
                    .ToList();

                return Ok(groupedByDate);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }
    }

    public static class ValidationExtensions
    {
        public static bool IsValidDateFormat(this string date)
        {
            return date.Length == 8 && DateTime.TryParseExact(
                date,
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _);
        }
    }
}