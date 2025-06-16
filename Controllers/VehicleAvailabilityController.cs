using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Z_TRIP.Models;
using Z_TRIP.Exceptions;
using Z_TRIP.Models.Contexts;


namespace Z_TRIP.Controllers
{
    [ApiController]
    [Route("api/vehicle-availability")]
    public class VehicleAvailabilityController : ControllerBase
    {
        private readonly string _constr;

        public VehicleAvailabilityController(IConfiguration config)
        {
            _constr = config.GetConnectionString("koneksi")!;
        }

        // Helper method untuk validasi dan parse format YYYYMMDD
        private DateTime ParseYYYYMMDD(string? dateStr, DateTime defaultDate, string paramName)
        {
            if (string.IsNullOrEmpty(dateStr))
            {
                return defaultDate;
            }

            // Add validation that string contains only digits
            if (!dateStr.All(char.IsDigit))
            {
                throw new ValidationException($"Parameter {paramName} harus berisi angka saja");
            }

            if (dateStr.Length != 8 || !DateTime.TryParseExact(
                dateStr,
                "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out DateTime result))
            {
                throw new ValidationException($"Format {paramName} harus YYYYMMDD");
            }

            // Add validation for reasonable date range (e.g., not in distant past or future)
            if (result.Year < 2020 || result.Year > 2050)
            {
                throw new ValidationException($"{paramName} berada di luar rentang tahun yang valid (2020-2050)");
            }

            return result;
        }

        // Helper method untuk format tanggal ke YYYYMMDD
        private string FormatYYYYMMDD(DateTime date)
        {
            // Handle edge case of DateTime.MinValue
            if (date == DateTime.MinValue)
                return "00000000";

            return date.ToString("yyyyMMdd");
        }

        // GET api/vehicle-availability/code/{unitCode}
        [HttpGet("code/{unitCode}")]
        public IActionResult GetVehicleAvailabilityByCode(
            string unitCode,
            [FromQuery] string? startDate, // Format: YYYYMMDD
            [FromQuery] string? endDate)   // Format: YYYYMMDD
        {
            try
            {
                // Add validation for unitCode
                if (string.IsNullOrWhiteSpace(unitCode))
                {
                    throw new ValidationException("Kode unit kendaraan tidak boleh kosong");
                }

                // Validate maximum length
                if (unitCode.Length > 50) // Adjust limit as needed
                {
                    throw new ValidationException("Kode unit kendaraan terlalu panjang");
                }

                // Validasi unitCode
                var unitCtx = new VehicleUnitsContext(_constr);
                var unit = unitCtx.GetVehicleUnitByCode(unitCode); // Perlu tambah method ini di VehicleUnitsContext

                if (unit == null)
                    throw new ResourceNotFoundException($"Unit kendaraan dengan kode {unitCode} tidak ditemukan");

                var bookingCtx = new BookingContext(_constr);

                // Parse dan validasi tanggal
                DateTime start, end;

                // Parse startDate
                if (string.IsNullOrEmpty(startDate))
                {
                    start = DateTime.Today;
                }
                else
                {
                    if (startDate.Length != 8 || !DateTime.TryParseExact(
                        startDate,
                        "yyyyMMdd",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out start))
                    {
                        throw new ValidationException("Format startDate harus YYYYMMDD");
                    }
                }

                // Parse endDate
                if (string.IsNullOrEmpty(endDate))
                {
                    end = DateTime.Today.AddDays(30);
                }
                else
                {
                    if (endDate.Length != 8 || !DateTime.TryParseExact(
                        endDate,
                        "yyyyMMdd",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out end))
                    {
                        throw new ValidationException("Format endDate harus YYYYMMDD");
                    }
                    // Tambahkan 1 hari dan kurangi 1 detik untuk mencakup seluruh hari terakhir
                    end = end.AddDays(1).AddSeconds(-1);
                }

                // Validasi rentang tanggal
                if (end < start)
                    throw new ValidationException($"Tanggal akhir ({FormatYYYYMMDD(end)}) harus setelah tanggal awal ({FormatYYYYMMDD(start)})");

                var daysDifference = (end - start).TotalDays;
                if (daysDifference > 90)
                    throw new ValidationException($"Rentang tanggal ({daysDifference:0} hari) melebihi batas maksimal 90 hari");

                // Add minimum range validation
                if (daysDifference < 0.5) // At least half a day
                    throw new ValidationException("Rentang tanggal terlalu pendek, minimal 1 hari");

                // Ambil semua booking yang overlap dengan rentang tanggal
                var bookings = bookingCtx.GetBookingsByVehicleUnitAndDateRange(unit.Id, start, end);

                // Dapatkan informasi vehicle (model kendaraan) untuk unit ini
                var vehicleCtx = new VehicleContext(_constr);
                var vehicle = vehicleCtx.GetVehicleById(unit.VehicleId);

                if (vehicle == null)
                    throw new ResourceNotFoundException($"Informasi kendaraan untuk unit {unitCode} tidak ditemukan");

                // Format hasil untuk response
                var unavailablePeriods = bookings
                    .Where(b => b.Status != booking_status_enum.rejected &&
                              b.Status != booking_status_enum.done)
                    .Select(b => new
                    {
                        BookingId = b.Id,
                        StartDate = FormatYYYYMMDD(b.StartDatetime),
                        EndDate = FormatYYYYMMDD(b.EndDatetime),
                        Status = b.Status.ToString()
                    })
                    .ToList();

                return Ok(new
                {
                    UnitCode = unit.Code,
                    VehicleInfo = new
                    {
                        vehicle.Id,
                        vehicle.Name,
                        vehicle.Merk,
                        Category = vehicle.Category.ToString(),
                        vehicle.Description,
                        vehicle.Capacity
                    },
                    UnitInfo = new
                    {
                        unit.Id,
                        unit.PricePerDay,
                        unit.Description,
                        HasImage = unit.VehicleImage != null && unit.VehicleImage.Length > 0
                    },
                    RequestedRange = new
                    {
                        StartDate = FormatYYYYMMDD(start),
                        EndDate = FormatYYYYMMDD(end)
                    },
                    UnavailablePeriods = unavailablePeriods,
                    IsAvailable = !unavailablePeriods.Any()
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

        // Endpoint lama tetap ada tapi dengan format tanggal yang diupdate
        [HttpGet("{vehicleUnitId:int}")]
        public IActionResult GetVehicleAvailability(
            int vehicleUnitId,
            [FromQuery] string? startDate,
            [FromQuery] string? endDate)
        {
            try
            {
                // Add validation for vehicleUnitId
                if (vehicleUnitId <= 0)
                {
                    throw new ValidationException("ID unit kendaraan tidak valid");
                }

                // Validasi vehicleUnitId
                var unitCtx = new VehicleUnitsContext(_constr);
                var unit = unitCtx.GetVehicleUnitById(vehicleUnitId);

                if (unit == null)
                    throw new ResourceNotFoundException($"Unit kendaraan dengan ID {vehicleUnitId} tidak ditemukan");

                // Parse dan validasi tanggal - sama seperti method di atas
                DateTime start, end;

                // Parse startDate
                if (string.IsNullOrEmpty(startDate))
                {
                    start = DateTime.Today;
                }
                else
                {
                    if (startDate.Length != 8 || !DateTime.TryParseExact(
                        startDate,
                        "yyyyMMdd",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out start))
                    {
                        throw new ValidationException("Format startDate harus YYYYMMDD");
                    }
                }

                // Parse endDate
                if (string.IsNullOrEmpty(endDate))
                {
                    end = DateTime.Today.AddDays(30);
                }
                else
                {
                    if (endDate.Length != 8 || !DateTime.TryParseExact(
                        endDate,
                        "yyyyMMdd",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out end))
                    {
                        throw new ValidationException("Format endDate harus YYYYMMDD");
                    }
                    end = end.AddDays(1).AddSeconds(-1);
                }

                // Validasi rentang tanggal
                if (end < start)
                    throw new ValidationException($"Tanggal akhir ({FormatYYYYMMDD(end)}) harus setelah tanggal awal ({FormatYYYYMMDD(start)})");

                var daysDifference = (end - start).TotalDays;
                if (daysDifference > 90)
                    throw new ValidationException($"Rentang tanggal ({daysDifference:0} hari) melebihi batas maksimal 90 hari");

                // Add minimum range validation
                if (daysDifference < 0.5) // At least half a day
                    throw new ValidationException("Rentang tanggal terlalu pendek, minimal 1 hari");

                // Sisanya sama seperti implementasi sebelumnya...
                var bookingCtx = new BookingContext(_constr);
                var bookings = bookingCtx.GetBookingsByVehicleUnitAndDateRange(vehicleUnitId, start, end);

                var unavailablePeriods = bookings
                    .Where(b => b.Status != booking_status_enum.rejected &&
                              b.Status != booking_status_enum.done)
                    .Select(b => new
                    {
                        BookingId = b.Id,
                        StartDate = FormatYYYYMMDD(b.StartDatetime),
                        EndDate = FormatYYYYMMDD(b.EndDatetime),
                        Status = b.Status.ToString()
                    })
                    .ToList();

                return Ok(new
                {
                    VehicleUnitId = vehicleUnitId,
                    UnitCode = unit.Code,
                    RequestedRange = new
                    {
                        StartDate = FormatYYYYMMDD(start),
                        EndDate = FormatYYYYMMDD(end)
                    },
                    UnavailablePeriods = unavailablePeriods,
                    IsAvailable = !unavailablePeriods.Any()
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

        // Untuk admin, mendapatkan semua unit kendaraan yang tersedia di rentang waktu tertentu
        [HttpGet("available")]
        [Authorize]
        public IActionResult GetAvailableVehicles(
            [FromQuery] string? startDate,
            [FromQuery] string? endDate,
            [FromQuery] string? category)
        {
            try
            {
                // Check authentication (already handled by [Authorize], but add user ID validation)
                var userIdClaim = User.FindFirst("userId")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId) || userId <= 0)
                {
                    return Unauthorized(new { message = "User tidak valid" });
                }

                // Add validation for category if provided
                if (!string.IsNullOrEmpty(category))
                {
                    // Check if it's a valid enum value
                    if (!Enum.TryParse<vehicle_category_enum>(category, true, out _))
                    {
                        // List valid categories for better error message
                        var validCategories = string.Join(", ", Enum.GetNames(typeof(vehicle_category_enum)));
                        throw new ValidationException($"Kategori kendaraan tidak valid. Nilai yang diizinkan: {validCategories}");
                    }
                }

                // Parse tanggal menggunakan helper method yang sudah ada
                var start = ParseYYYYMMDD(startDate, DateTime.Today, "startDate");
                var end = ParseYYYYMMDD(endDate, DateTime.Today.AddDays(30), "endDate");

                // Tambahkan 1 hari dan kurangi 1 detik untuk end date agar mencakup seluruh hari
                end = end.AddDays(1).AddSeconds(-1);

                // Validasi rentang tanggal
                if (end < start)
                    throw new ValidationException($"Tanggal akhir ({FormatYYYYMMDD(end)}) harus setelah tanggal awal ({FormatYYYYMMDD(start)})");

                var daysDifference = (end - start).TotalDays;
                if (daysDifference > 90)
                    throw new ValidationException($"Rentang tanggal ({daysDifference:0} hari) melebihi batas maksimal 90 hari");

                // Add minimum range validation
                if (daysDifference < 0.5) // At least half a day
                    throw new ValidationException("Rentang tanggal terlalu pendek, minimal 1 hari");

                var bookingCtx = new BookingContext(_constr);
                var vehicleCtx = new VehicleContext(_constr);
                var vehicleUnitsCtx = new VehicleUnitsContext(_constr);

                // Ambil semua unit kendaraan
                var allVehicleUnits = vehicleUnitsCtx.GetAllVehicleUnits();

                // Filter berdasarkan category jika ada
                if (!string.IsNullOrEmpty(category))
                {
                    // Ambil ID kendaraan berdasarkan kategori
                    var vehicles = vehicleCtx.FilterVehicles(
                        Enum.TryParse<vehicle_category_enum>(category, true, out var catEnum) ? catEnum : null,
                        null, null, null, null, null
                    );
                    var vehicleIds = vehicles.Select(v => v.Id).ToList();

                    // Filter unit kendaraan berdasarkan vehicle ID
                    allVehicleUnits = allVehicleUnits.Where(u => vehicleIds.Contains(u.VehicleId)).ToList();
                }

                // Ambil semua booking yang overlap dengan rentang tanggal
                var activeBookings = bookingCtx.GetActiveBookingsByDateRange(start, end);

                // Group booking berdasarkan vehicle_unit_id
                var bookedUnitIds = activeBookings
                    .Select(b => b.VehicleUnitId)
                    .Distinct()
                    .ToList();

                // Filter unit yang tersedia (tidak ada booking aktif)
                var availableUnits = allVehicleUnits
                    .Where(u => !bookedUnitIds.Contains(u.Id))
                    .ToList();

                // Gabungkan dengan data kendaraan lengkap
                var result = new List<object>();
                foreach (var unit in availableUnits)
                {
                    var vehicle = vehicleCtx.GetVehicleById(unit.VehicleId);
                    if (vehicle != null)
                    {
                        result.Add(new
                        {
                            UnitId = unit.Id,
                            UnitCode = unit.Code,
                            PricePerDay = unit.PricePerDay,
                            UnitDescription = unit.Description,
                            HasImage = unit.VehicleImage != null && unit.VehicleImage.Length > 0,
                            Vehicle = new
                            {
                                vehicle.Id,
                                vehicle.Merk,
                                Category = vehicle.Category.ToString(),
                                vehicle.Name,
                                vehicle.Description,
                                vehicle.Capacity
                            }
                        });
                    }
                }

                return Ok(new
                {
                    RequestedRange = new
                    {
                        StartDate = FormatYYYYMMDD(start),
                        EndDate = FormatYYYYMMDD(end.Date) // Hilangkan bagian waktu
                    },
                    AvailableUnits = result,
                    Count = result.Count
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
    }
}