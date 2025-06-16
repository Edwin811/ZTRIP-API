using Microsoft.AspNetCore.Mvc;
using Z_TRIP.Models;
using Microsoft.AspNetCore.Authorization;
using Z_TRIP.Exceptions;
using Z_TRIP.Models.Contexts;
using System.Text.Json;

namespace Z_TRIP.Controllers
{
    [ApiController]
    [Route("api/[controller]")]

    public class BookingController : ControllerBase
    {
        private readonly string _constr;
        public BookingController(IConfiguration config) => _constr = config.GetConnectionString("koneksi")!;

        [HttpGet]
        [Authorize]
        public ActionResult<List<Booking>> GetAll()
        {
            try
            {
                // Ambil user ID dari token
                var userIdClaim = User.FindFirst("userId")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "User tidak valid" });
                }

                // Check if user is admin
                bool isAdmin = User.IsInRole("Admin");

                List<Booking> bookings;
                var ctx = new BookingContext(_constr);

                if (isAdmin)
                {
                    // Admin can see all bookings
                    bookings = ctx.GetAllBookings();
                }
                else
                {
                    // Customer can only see their own bookings
                    bookings = ctx.GetBookingsByUserId(userId);
                }

                if (bookings.Count == 0)
                {
                    return NotFound(new
                    {
                        message = isAdmin
                            ? "Tidak ada data booking"
                            : "Anda belum memiliki booking"
                    });
                }

                if (isAdmin)
                {
                    // Admin response dengan detail lengkap
                    var adminResult = bookings.Select(b => new
                    {
                        b.Id,
                        UserId = b.UserId,
                        VehicleUnitId = b.VehicleUnitId,
                        StartDate = FormatYYYYMMDD(b.StartDatetime),
                        EndDate = FormatYYYYMMDD(b.EndDatetime),
                        Status = b.Status.ToString(),
                        b.StatusNote,
                        b.TransactionId,
                        CreatedAt = FormatYYYYMMDD(b.CreatedAt)
                    }).ToList();

                    return Ok(adminResult);
                }
                else
                {
                    // Customer response dengan informasi yang relevan saja
                    var customerResult = bookings.Select(async b =>
                    {
                        // Get vehicle info for better context
                        var unit = new VehicleUnitsContext(_constr).GetVehicleUnitById(b.VehicleUnitId);
                        var vehicleName = "Unknown";
                        var vehicleModel = "Unknown";

                        if (unit != null)
                        {
                            var vehicle = new VehicleContext(_constr).GetVehicleById(unit.VehicleId);
                            if (vehicle != null)
                            {
                                vehicleName = vehicle.Name;
                                vehicleModel = vehicle.Merk;
                            }
                        }

                        // Get transaction info for payment status
                        string paymentStatus = "Not paid";
                        if (b.TransactionId.HasValue)
                        {
                            var transaction = new TransaksiContext(_constr).GetTransaksiById(b.TransactionId.Value);
                            if (transaction != null)
                            {
                                paymentStatus = transaction.PaymentStatus.ToString();
                            }
                        }

                        return new
                        {
                            BookingId = b.Id,
                            Vehicle = $"{vehicleModel} {vehicleName}",
                            UnitCode = unit?.Code ?? "Unknown",
                            Period = new
                            {
                                StartDate = FormatYYYYMMDD(b.StartDatetime),
                                EndDate = FormatYYYYMMDD(b.EndDatetime),
                                DurationDays = Math.Ceiling((b.EndDatetime - b.StartDatetime).TotalDays)
                            },
                            Status = b.Status.ToString(),
                            PaymentStatus = paymentStatus,
                            Price = unit?.PricePerDay ?? 0,
                            TotalPrice = unit != null ? unit.PricePerDay * (decimal)Math.Ceiling((b.EndDatetime - b.StartDatetime).TotalDays) : 0
                        };
                    }).Select(t => t.Result).ToList();

                    return Ok(customerResult);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        private string FormatYYYYMMDD(DateTime date)
        {
            return date.ToString("yyyyMMdd");
        }

        [HttpGet("{id:int}")]
        [Authorize]
        public IActionResult GetById(int id)
        {
            try
            {
                // Add validation for ID parameter
                if (id <= 0)
                {
                    return BadRequest(new { message = "ID booking tidak valid" });
                }

                // Ambil user ID dari token
                var userIdClaim = User.FindFirst("userId")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "User tidak valid" });
                }

                // Check if user is admin
                bool isAdmin = User.IsInRole("Admin");

                var bookingCtx = new BookingContext(_constr);
                var booking = bookingCtx.GetBookingById(id);

                if (booking == null)
                    return NotFound(new { message = $"Booking dengan ID {id} tidak ditemukan." });

                // Jika bukan admin dan bukan booking miliknya, tolak akses
                if (!isAdmin && booking.UserId != userId)
                    return Forbid();

                // Get vehicle information
                var unit = new VehicleUnitsContext(_constr).GetVehicleUnitById(booking.VehicleUnitId);
                var vehicleInfo = new { Name = "Unknown", Merk = "Unknown", Type = "Unknown", Image = false };
                decimal pricePerDay = 0;

                if (unit != null)
                {
                    pricePerDay = unit.PricePerDay;
                    var vehicle = new VehicleContext(_constr).GetVehicleById(unit.VehicleId);
                    if (vehicle != null)
                    {
                        vehicleInfo = new
                        {
                            Name = vehicle.Name,
                            Merk = vehicle.Merk,
                            Type = vehicle.Category.ToString(),
                            Image = unit.VehicleImage != null && unit.VehicleImage.Length > 0
                        };
                    }
                }

                // Calculate duration and total price
                var durationDays = (decimal)Math.Ceiling((booking.EndDatetime - booking.StartDatetime).TotalDays);
                var totalPrice = pricePerDay * durationDays;

                // Get payment info
                var paymentInfo = new { Status = "Not paid", Method = "Unknown" };
                if (booking.TransactionId.HasValue)
                {
                    var transaction = new TransaksiContext(_constr).GetTransaksiById(booking.TransactionId.Value);
                    if (transaction != null)
                    {
                        paymentInfo = new
                        {
                            Status = transaction.PaymentStatus.ToString(),
                            Method = transaction.Method.ToString()
                        };
                    }
                }

                if (isAdmin)
                {
                    // Admin dapat melihat detail lengkap
                    return Ok(new
                    {
                        booking.Id,
                        booking.UserId,
                        booking.VehicleUnitId,
                        StartDate = FormatYYYYMMDD(booking.StartDatetime),
                        EndDate = FormatYYYYMMDD(booking.EndDatetime),
                        Status = booking.Status.ToString(),
                        booking.StatusNote,
                        booking.TransactionId,
                        VehicleInfo = vehicleInfo,
                        Payment = paymentInfo,
                        PriceDetails = new
                        {
                            PricePerDay = pricePerDay,
                            DurationDays = durationDays,
                            TotalPrice = totalPrice
                        }
                    });
                }
                else
                {
                    // Customer hanya melihat informasi yang relevan
                    return Ok(new
                    {
                        BookingId = booking.Id,
                        Vehicle = new
                        {
                            Name = $"{vehicleInfo.Merk} {vehicleInfo.Name}",
                            Type = vehicleInfo.Type,
                            UnitCode = unit?.Code,
                            HasImage = vehicleInfo.Image
                        },
                        Schedule = new
                        {
                            StartDate = FormatYYYYMMDD(booking.StartDatetime),
                            EndDate = FormatYYYYMMDD(booking.EndDatetime),
                            DurationDays = durationDays
                        },
                        Status = booking.Status.ToString(),
                        Payment = new
                        {
                            Status = paymentInfo.Status,
                            Method = paymentInfo.Method,
                            PricePerDay = pricePerDay,
                            TotalPrice = totalPrice,
                            TransactionId = booking.TransactionId
                        },
                        StatusNote = booking.Status == booking_status_enum.rejected ? booking.StatusNote : null
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        [HttpGet("status/{status}")]
        [Authorize]
        public ActionResult<List<Booking>> GetByStatus(string status)
        {
            try
            {
                // Add validation for status parameter
                if (string.IsNullOrEmpty(status))
                {
                    return BadRequest(new { message = "Parameter status tidak boleh kosong" });
                }

                // Validate status is a valid enum value
                if (!Enum.TryParse<booking_status_enum>(status, true, out _))
                {
                    return BadRequest(new { message = "Status booking tidak valid" });
                }

                // Ambil user ID dari token
                var userIdClaim = User.FindFirst("userId")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "User tidak valid" });
                }

                // Check if user is admin
                bool isAdmin = User.IsInRole("Admin");

                var bookingCtx = new BookingContext(_constr);
                List<Booking> list;

                if (isAdmin)
                {
                    // Admin dapat melihat semua booking dengan status tertentu
                    list = bookingCtx.GetBookingsByStatus(status);
                }
                else
                {
                    // Customer hanya bisa melihat bookingnya sendiri
                    list = bookingCtx.GetBookingsByStatusAndUserId(status, userId);
                }

                if (list.Count == 0)
                    return NotFound(new { message = $"Tidak ada booking dengan status '{status}'." });

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        [HttpGet("vehicle/{vehicleUnitId:int}")]
        [Authorize]
        public ActionResult<List<Booking>> GetByVehicleUnit(int vehicleUnitId)
        {
            try
            {
                // Add validation for ID parameter
                if (vehicleUnitId <= 0)
                {
                    return BadRequest(new { message = "ID unit kendaraan tidak valid" });
                }

                var userIdClaim = User.FindFirst("userId")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                    return Unauthorized(new { message = "User tidak valid" });

                bool isAdmin = User.IsInRole("Admin");

                var list = new BookingContext(_constr).GetVehicleSchedule(vehicleUnitId);

                // Batasi customer hanya bisa melihat booking miliknya sendiri
                if (!isAdmin)
                    list = list.Where(b => b.UserId == userId).ToList();

                if (list.Count == 0)
                    return NotFound($"Tidak ada jadwal booking untuk vehicle_unit_id {vehicleUnitId}.");
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        [HttpPost]
        [Authorize]
        public IActionResult Create([FromBody] JsonElement requestBody)
        {
            try
            {
                // Validasi input
                if (requestBody.ValueKind == JsonValueKind.Undefined)
                    throw new ValidationException("Data booking tidak boleh kosong");

                // Extract fields from JsonElement
                int vehicleUnitId;
                string startDate, endDate;
                string? note = null;

                // Get vehicleUnitId
                if (!requestBody.TryGetProperty("vehicleUnitId", out JsonElement vehicleUnitIdElement) ||
                    !vehicleUnitIdElement.TryGetInt32(out vehicleUnitId))
                {
                    throw new ValidationException("VehicleUnitId harus disediakan dan merupakan integer");
                }

                // Get startDate
                if (!requestBody.TryGetProperty("startDate", out JsonElement startDateElement) ||
                    startDateElement.ValueKind != JsonValueKind.String)
                {
                    throw new ValidationException("StartDate harus disediakan dan dalam format string");
                }
                startDate = startDateElement.GetString() ?? string.Empty;

                // Get endDate
                if (!requestBody.TryGetProperty("endDate", out JsonElement endDateElement) ||
                    endDateElement.ValueKind != JsonValueKind.String)
                {
                    throw new ValidationException("EndDate harus disediakan dan dalam format string");
                }
                endDate = endDateElement.GetString() ?? string.Empty;

                // Get optional note
                if (requestBody.TryGetProperty("note", out JsonElement noteElement) &&
                    noteElement.ValueKind == JsonValueKind.String)
                {
                    note = noteElement.GetString();
                }

                // Additional validation for startDate length
                if (startDate.Length != 8)
                    throw new ValidationException("Format startDate harus YYYYMMDD");

                // Additional validation for endDate length
                if (endDate.Length != 8)
                    throw new ValidationException("Format endDate harus YYYYMMDD");

                // Additional validation to ensure dates are numeric
                if (!startDate.All(char.IsDigit))
                    throw new ValidationException("startDate harus berisi angka saja");

                if (!endDate.All(char.IsDigit))
                    throw new ValidationException("endDate harus berisi angka saja");

                // Additional validation for vehicle ID
                if (vehicleUnitId <= 0)
                    throw new ValidationException("ID unit kendaraan tidak valid");

                // Parse tanggal dari format YYYYMMDD
                var startDateTime = ParseYYYYMMDD(startDate, "startDate");
                var endDateTime = ParseYYYYMMDD(endDate, "endDate");

                // Tambahkan 1 hari dikurangi 1 detik agar mencakup seluruh hari terakhir
                endDateTime = endDateTime.AddDays(1).AddSeconds(-1);

                if (startDateTime >= endDateTime)
                    throw new ValidationException("Waktu mulai harus sebelum waktu selesai");

                // Validasi tanggal pemesanan
                if (startDateTime < DateTime.Today)
                    throw new ValidationException("Tanggal booking tidak boleh di masa lalu");

                // Ambil user id
                var userId = int.Parse(User.FindFirst("userId")?.Value ?? "0");
                if (userId <= 0)
                    throw new UnauthorizedAccessException("User tidak valid");

                // Cek apakah user sudah diverifikasi
                var userCtx = new UsersContext(_constr);
                var user = userCtx.GetUserById(userId);

                if (user == null)
                    throw new ResourceNotFoundException("User tidak ditemukan");

                bool isAdmin = User.IsInRole("Admin");

                // Cek verifikasi user (kecuali admin)
                if (!isAdmin && !user.IsVerified)
                {
                    return BadRequest(new
                    {
                        message = "Akun Anda belum diverifikasi. Silakan upload KTP dan SIM, kemudian tunggu verifikasi dari admin.",
                        is_verified = false,
                        can_book = false,
                        hasKtp = user.KtpImage != null && user.KtpImage.Length > 0,
                        hasSim = user.SimImage != null && user.SimImage.Length > 0
                    });
                }

                // Pastikan user memilih kendaraan yang valid
                var vehicleUnitsCtx = new VehicleUnitsContext(_constr);
                var vehicleUnit = vehicleUnitsCtx.GetVehicleUnitById(vehicleUnitId);
                if (vehicleUnit == null)
                    throw new ResourceNotFoundException($"Unit kendaraan dengan ID {vehicleUnitId} tidak ditemukan");

                // Cek ketersediaan (konflik jadwal)
                var bookingCtx = new BookingContext(_constr);
                var conflictingBookings = bookingCtx.GetBookingsByVehicleUnitAndDateRange(
                    vehicleUnitId, startDateTime, endDateTime);

                var activeConflicts = conflictingBookings
                    .Where(b => b.Status != booking_status_enum.rejected && b.Status != booking_status_enum.done)
                    .ToList();

                if (activeConflicts.Any())
                {
                    return Conflict(new
                    {
                        message = "Kendaraan tidak tersedia pada waktu yang dipilih",
                        conflicts = activeConflicts.Select(b => new
                        {
                            b.Id,
                            StartDate = FormatYYYYMMDD(b.StartDatetime),
                            EndDate = FormatYYYYMMDD(b.EndDatetime),
                            Status = b.Status.ToString()
                        }).ToList()
                    });
                }

                // Hitung jumlah pembayaran
                var durationDays = Math.Ceiling((endDateTime - startDateTime).TotalDays);
                var totalAmount = vehicleUnit.PricePerDay * (decimal)durationDays;

                // Buat transaksi pembayaran
                var transaction = new Transaksi
                {
                    Method = payment_method_enum.QRIS,
                    PaymentStatus = payment_status_enum.pending,
                    Amount = totalAmount
                };

                var txnCtx = new TransaksiContext(_constr);
                var txnId = txnCtx.AddTransaksi(transaction);

                if (txnId <= 0)
                    throw new Exception("Gagal membuat transaksi pembayaran");

                // Buat objek booking dengan data yang valid
                var booking = new Booking
                {
                    UserId = userId,
                    VehicleUnitId = vehicleUnitId,
                    StartDatetime = startDateTime,
                    EndDatetime = endDateTime,
                    StatusNote = note,
                    TransactionId = txnId,
                    Status = booking_status_enum.pending
                };

                // Buat booking
                var createdBooking = bookingCtx.CreateBooking(booking);

                if (createdBooking == null)
                    throw new Exception("Gagal membuat booking");

                // Dapatkan informasi vehicle untuk response
                var vehicleCtx = new VehicleContext(_constr);
                var vehicle = vehicleCtx.GetVehicleById(vehicleUnit.VehicleId);

                // Response dibedakan berdasarkan role
                if (isAdmin)
                {
                    // Admin mendapatkan detail lengkap
                    return CreatedAtAction(nameof(GetById), new { id = createdBooking.Id }, new
                    {
                        message = "Booking berhasil dibuat oleh admin",
                        booking = new
                        {
                            Id = createdBooking.Id,
                            UserId = createdBooking.UserId,
                            VehicleUnitId = createdBooking.VehicleUnitId,
                            StartDate = FormatYYYYMMDD(startDateTime),
                            EndDate = FormatYYYYMMDD(endDateTime.Date),
                            Status = createdBooking.Status.ToString(),
                            StatusNote = createdBooking.StatusNote,
                            TransactionId = createdBooking.TransactionId,
                            VehicleInfo = new
                            {
                                Id = vehicle?.Id,
                                Name = vehicle?.Name ?? "Unknown",
                                Merk = vehicle?.Merk ?? "Unknown",
                                UnitCode = vehicleUnit.Code,
                                Category = vehicle?.Category.ToString() ?? "Unknown"
                            },
                            Payment = new
                            {
                                TransactionId = txnId,
                                Status = "pending",
                                PricePerDay = vehicleUnit.PricePerDay,
                                DurationDays = durationDays,
                                TotalPrice = totalAmount
                            }
                        }
                    });
                }
                else
                {
                    // Format response untuk customer yang lebih clean dan friendly
                    return CreatedAtAction(nameof(GetById), new { id = createdBooking.Id }, new
                    {
                        message = "Booking berhasil dibuat",
                        booking = new
                        {
                            Id = createdBooking.Id,
                            Vehicle = new
                            {
                                Name = vehicle?.Name ?? "Unknown",
                                Merk = vehicle?.Merk ?? "Unknown",
                                UnitCode = vehicleUnit.Code,
                                HasImage = vehicleUnit.VehicleImage != null && vehicleUnit.VehicleImage.Length > 0
                            },
                            Schedule = new
                            {
                                StartDate = FormatYYYYMMDD(startDateTime),
                                EndDate = FormatYYYYMMDD(endDateTime.Date),
                                DurationDays = durationDays
                            },
                            Payment = new
                            {
                                TransactionId = txnId,
                                Status = "pending",
                                PricePerDay = vehicleUnit.PricePerDay,
                                TotalPrice = totalAmount,
                                Note = "Silakan melakukan pembayaran untuk mengkonfirmasi booking"
                            },
                            Status = createdBooking.Status.ToString(),
                            NextSteps = new List<string>
                            {
                                "Lakukan pembayaran melalui menu transaksi",
                                "Upload bukti pembayaran",
                                "Tunggu konfirmasi admin"
                            }
                        }
                    });
                }
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (ResourceNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // Helper method untuk menghitung jumlah pembayaran
        private decimal CalculateTotalAmount(decimal pricePerDay, DateTime start, DateTime end)
        {
            // Check for invalid price
            if (pricePerDay <= 0)
            {
                throw new ValidationException("Harga per hari harus lebih dari 0");
            }

            // Check for invalid date range
            if (start > end)
            {
                throw new ValidationException("Tanggal mulai tidak boleh setelah tanggal selesai");
            }

            var days = (end - start).TotalDays;
            // Jika kurang dari 1 hari, tetap hitung sebagai 1 hari
            if (days < 1) days = 1;
            return pricePerDay * (decimal)Math.Ceiling(days);
        }

        [HttpPatch("{id}/reject")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult RejectBooking(int id, [FromBody] JsonElement requestBody)
        {
            try
            {
                // Add validation for ID parameter
                if (id <= 0)
                {
                    return BadRequest(new { message = "ID booking tidak valid" });
                }

                // Validate request body isn't empty
                if (requestBody.ValueKind == JsonValueKind.Undefined ||
                    requestBody.ValueKind == JsonValueKind.Null)
                {
                    return BadRequest(new { message = "Data penolakan tidak boleh kosong" });
                }

                // Extract fields from requestBody
                string reason;
                string? paymentStatus = null;

                // Get reason
                if (!requestBody.TryGetProperty("reason", out JsonElement reasonElement) ||
                    reasonElement.ValueKind != JsonValueKind.String)
                {
                    return BadRequest(new { message = "Alasan penolakan harus diisi" });
                }
                reason = reasonElement.GetString() ?? string.Empty;

                if (string.IsNullOrEmpty(reason))
                    return BadRequest(new { message = "Alasan penolakan harus diisi" });

                // Get optional paymentStatus
                if (requestBody.TryGetProperty("paymentStatus", out JsonElement paymentStatusElement) &&
                    paymentStatusElement.ValueKind == JsonValueKind.String)
                {
                    paymentStatus = paymentStatusElement.GetString();
                }

                var bookingCtx = new BookingContext(_constr);
                var booking = bookingCtx.GetBookingById(id);

                if (booking == null)
                    return NotFound(new { message = $"Booking dengan ID {id} tidak ditemukan" });

                // Hanya booking dengan status pending yang bisa direject
                if (booking.Status != booking_status_enum.pending)
                {
                    return BadRequest(new
                    {
                        message = "Hanya booking dengan status pending yang dapat ditolak",
                        currentStatus = booking.Status.ToString()
                    });
                }

                // Update status booking menjadi rejected
                bookingCtx.UpdateStatusBooking(id, booking_status_enum.rejected.ToString());
                bookingCtx.UpdateStatusNote(id, $"Booking ditolak: {reason}");

                // Perbarui status transaksi jika diminta oleh admin
                var updatedPaymentStatus = "";
                if (booking.TransactionId.HasValue && !string.IsNullOrEmpty(paymentStatus))
                {
                    // Validasi status pembayaran yang diminta
                    if (!Enum.TryParse<payment_status_enum>(paymentStatus, true, out var paymentStatusEnum))
                        return BadRequest(new { message = "Status pembayaran tidak valid" });

                    var txnCtx = new TransaksiContext(_constr);
                    var transaksi = txnCtx.GetTransaksiById(booking.TransactionId.Value);

                    if (transaksi != null)
                    {
                        // Pemeriksaan logis status pembayaran
                        if (paymentStatusEnum == payment_status_enum.paid && transaksi.PaymentImage == null)
                        {
                            return BadRequest(new
                            {
                                message = "Tidak dapat mengubah status menjadi 'paid' karena belum ada bukti pembayaran"
                            });
                        }

                        // Update status transaksi
                        txnCtx.UpdateTransaksiStatus(booking.TransactionId.Value, paymentStatusEnum.ToString());
                        updatedPaymentStatus = paymentStatusEnum.ToString();

                        // Tambahkan catatan status pembayaran ke booking
                        string paymentNote = GetPaymentStatusNote(paymentStatusEnum);
                        bookingCtx.UpdateStatusNote(id, $"Booking ditolak: {reason}. {paymentNote}");
                    }
                }

                return Ok(new
                {
                    message = "Booking berhasil ditolak",
                    bookingId = id,
                    status = "rejected",
                    reason = reason,
                    paymentStatus = string.IsNullOrEmpty(updatedPaymentStatus) ?
                        (booking.TransactionId.HasValue ?
                            new TransaksiContext(_constr).GetTransaksiById(booking.TransactionId.Value)?.PaymentStatus.ToString() :
                            "tidak ada transaksi") :
                        updatedPaymentStatus,
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // Helper method untuk menentukan catatan status pembayaran
        private string GetPaymentStatusNote(payment_status_enum paymentStatus)
        {
            return paymentStatus switch
            {
                payment_status_enum.unpaid => "Status pembayaran diatur ke 'unpaid'. Customer perlu mengupload bukti pembayaran yang valid.",
                payment_status_enum.pending => "Status pembayaran diatur ke 'pending'. Menunggu verifikasi bukti pembayaran.",
                payment_status_enum.paid => "Status pembayaran diatur ke 'paid'. Pembayaran telah diverifikasi tetapi booking tetap ditolak.",
                _ => string.Empty
            };
        }

        private DateTime ParseYYYYMMDD(string dateStr, string paramName)
        {
            if (string.IsNullOrEmpty(dateStr))
            {
                throw new ValidationException($"Parameter {paramName} tidak boleh kosong");
            }

            const string FORMAT = "yyyyMMdd";

            // Additional check for length before trying to parse
            if (dateStr.Length != 8)
            {
                throw new ValidationException($"Format {paramName} harus terdiri dari 8 digit dengan format {FORMAT}");
            }

            // Additional check that string contains only digits
            if (!dateStr.All(char.IsDigit))
            {
                throw new ValidationException($"Format {paramName} harus berisi angka saja");
            }

            if (!DateTime.TryParseExact(
                dateStr,
                FORMAT,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out DateTime result))
            {
                throw new ValidationException($"Format {paramName} tidak valid, gunakan format {FORMAT}");
            }

            return result;
        }
    }
}