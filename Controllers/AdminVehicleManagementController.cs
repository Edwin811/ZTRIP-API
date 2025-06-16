using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Z_TRIP.Models;
using Z_TRIP.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

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
        public IActionResult BlockSchedule([FromBody] ScheduleBlockRequest request)
        {
            try
            {
                var startDate = ParseYYYYMMDD(request.StartDate, "StartDate").Date;
                var endDate = ParseYYYYMMDD(request.EndDate, "EndDate").Date.AddDays(1).AddSeconds(-1);

                if (startDate >= endDate)
                    throw new ValidationException("Tanggal mulai harus sebelum tanggal selesai");

                if (request.VehicleUnitIds == null || !request.VehicleUnitIds.Any())
                    throw new ValidationException("Setidaknya satu ID unit kendaraan harus disediakan");

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
                foreach (var unitId in request.VehicleUnitIds)
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
                        unitId, startDate, endDate);

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
                        StartDatetime = startDate, // Gunakan tanggal awal hari
                        EndDatetime = endDate,     // Gunakan tanggal akhir hari
                        Status = booking_status_enum.approved, // Langsung approved karena admin yang membuat
                        StatusNote = $"BLOCKED_BY_ADMIN: {request.Note ?? "No reason provided"}", // Tanda khusus
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
                                StartDate = FormatToYYYYMMDD(startDate),
                                EndDate = FormatToYYYYMMDD(endDate)
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
        public IActionResult UpdateBlockedSchedule(int bookingId, [FromBody] UpdateScheduleRequest request)
        {
            try
            {
                // Standarisasi tanggal tanpa jam
                var startDate = request.StartDate.Date;
                var endDate = request.EndDate.Date.AddDays(1).AddSeconds(-1); // Hingga akhir hari

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
                    booking.StatusNote = $"BLOCKED_BY_ADMIN: {request.Note ?? "No reason provided"}";
                }
                else if (request.Note != null)
                {
                    // Jika booking normal, tambahkan note tanpa menghapus informasi sebelumnya
                    booking.StatusNote = request.Note;
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
        public IActionResult GetBlockedSchedules([FromQuery] string? startDate, [FromQuery] string? endDate)
        {
            try
            {
                var start = ParseYYYYMMDD(startDate ?? DateTime.Today.ToString(DATE_FORMAT), "startDate");
                var end = ParseYYYYMMDD(endDate ?? DateTime.Today.AddDays(30).ToString(DATE_FORMAT), "endDate");

                if (start >= end)
                    throw new ValidationException("Tanggal mulai harus sebelum tanggal selesai");

                var bookingCtx = new BookingContext(_constr);
                var blockedSchedules = bookingCtx.GetBlockedSchedules(start, end);

                var result = blockedSchedules.Select(b => new
                {
                    BlockId = b.Id,
                    VehicleUnitId = b.VehicleUnitId,
                    StartDate = FormatToYYYYMMDD(b.StartDatetime),
                    EndDate = FormatToYYYYMMDD(b.EndDatetime),
                    Note = b.StatusNote?.Replace("BLOCKED_BY_ADMIN: ", ""),
                    CreatedAt = FormatToYYYYMMDD(b.CreatedAt)
                }).ToList();

                return Ok(result);
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

        // Helper method untuk mendapatkan nama pengguna
        private string GetUserName(int userId)
        {
            var userCtx = new UsersContext(_constr);
            var user = userCtx.GetUserById(userId);
            return user?.Name ?? "Unknown User";
        }
    }

    public class ScheduleBlockRequest
    {
        public List<int> VehicleUnitIds { get; set; } = new List<int>();
        public string StartDate { get; set; } = string.Empty; // Format YYYYMMDD
        public string EndDate { get; set; } = string.Empty;   // Format YYYYMMDD
        public string? Note { get; set; }
    }

    public class UpdateScheduleRequest
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? Note { get; set; }
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