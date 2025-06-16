using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Z_TRIP.Models;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Z_TRIP.Exceptions;


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
        public ActionResult<BookingResponse> GetById(int id)
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

        [HttpPost]
        [Authorize]
        public IActionResult Create([FromBody] BookingCreateRequest request)
        {
            try
            {
                // Validasi input
                if (request == null)
                    throw new ValidationException("Data booking tidak boleh kosong");

                // Parse tanggal dari format YYYYMMDD
                var startDate = ParseYYYYMMDD(request.StartDate, "startDate");
                var endDate = ParseYYYYMMDD(request.EndDate, "endDate");

                // Tambahkan 1 hari dikurangi 1 detik agar mencakup seluruh hari terakhir
                endDate = endDate.AddDays(1).AddSeconds(-1);

                if (startDate >= endDate)
                    throw new ValidationException("Waktu mulai harus sebelum waktu selesai");

                // Validasi tanggal pemesanan
                if (startDate < DateTime.Today)
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
                var vehicleUnit = vehicleUnitsCtx.GetVehicleUnitById(request.VehicleUnitId);
                if (vehicleUnit == null)
                    throw new ResourceNotFoundException($"Unit kendaraan dengan ID {request.VehicleUnitId} tidak ditemukan");

                // Cek ketersediaan (konflik jadwal)
                var bookingCtx = new BookingContext(_constr);
                var conflictingBookings = bookingCtx.GetBookingsByVehicleUnitAndDateRange(
                    request.VehicleUnitId, startDate, endDate);

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
                var durationDays = Math.Ceiling((endDate - startDate).TotalDays);
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
                    VehicleUnitId = request.VehicleUnitId,
                    StartDatetime = startDate,
                    EndDatetime = endDate,
                    StatusNote = request.Note,
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
                            StartDate = FormatYYYYMMDD(startDate),
                            EndDate = FormatYYYYMMDD(endDate.Date),
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
                                StartDate = FormatYYYYMMDD(startDate),
                                EndDate = FormatYYYYMMDD(endDate.Date),
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
            var days = (end - start).TotalDays;
            // Jika kurang dari 1 hari, tetap hitung sebagai 1 hari
            if (days < 1) days = 1;
            return pricePerDay * (decimal)Math.Ceiling(days);
        }

        [HttpPatch("{id}/reject")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult RejectBooking(int id, [FromBody] RejectBookingRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Reason))
                    return BadRequest(new { message = "Alasan penolakan harus diisi" });

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
                bookingCtx.UpdateStatusNote(id, $"Booking ditolak: {request.Reason}");

                // Perbarui status transaksi jika diminta oleh admin
                var updatedPaymentStatus = "";
                if (booking.TransactionId.HasValue && !string.IsNullOrEmpty(request.PaymentStatus))
                {
                    // Validasi status pembayaran yang diminta
                    if (!Enum.TryParse<payment_status_enum>(request.PaymentStatus, true, out var paymentStatus))
                        return BadRequest(new { message = "Status pembayaran tidak valid" });

                    var txnCtx = new TransaksiContext(_constr);
                    var transaksi = txnCtx.GetTransaksiById(booking.TransactionId.Value);

                    if (transaksi != null)
                    {
                        // Pemeriksaan logis status pembayaran
                        if (paymentStatus == payment_status_enum.paid && transaksi.PaymentImage == null)
                        {
                            return BadRequest(new
                            {
                                message = "Tidak dapat mengubah status menjadi 'paid' karena belum ada bukti pembayaran"
                            });
                        }

                        // Update status transaksi
                        txnCtx.UpdateTransaksiStatus(booking.TransactionId.Value, paymentStatus.ToString());
                        updatedPaymentStatus = paymentStatus.ToString();

                        // Tambahkan catatan status pembayaran ke booking
                        string paymentNote = GetPaymentStatusNote(paymentStatus);
                        bookingCtx.UpdateStatusNote(id, $"Booking ditolak: {request.Reason}. {paymentNote}");
                    }
                }

                return Ok(new
                {
                    message = "Booking berhasil ditolak",
                    bookingId = id,
                    status = "rejected",
                    reason = request.Reason,
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

        // Helper method untuk memberikan instruksi selanjutnya
        public class RejectBookingRequest
        {
            public string Reason { get; set; } = string.Empty;

            // Tambahkan field untuk status pembayaran
            // Null berarti tidak mengubah status pembayaran
            public string? PaymentStatus { get; set; }
        }

        public class BookingResponse
        {
            public int Id { get; set; }
            public string StartDate { get; set; } = string.Empty; // Format YYYYMMDD
            public string EndDate { get; set; } = string.Empty;   // Format YYYYMMDD
            public string Status { get; set; } = string.Empty;
            public string? StatusNote { get; set; }
            // ... other properties
        }

        public class BookingCreateRequest
        {
            public int VehicleUnitId { get; set; }
            public string StartDate { get; set; } = string.Empty; // Format YYYYMMDD
            public string EndDate { get; set; } = string.Empty;   // Format YYYYMMDD
            public string? Note { get; set; }
        }

        public class BookingUpdateRequest
        {
            public int VehicleUnitId { get; set; }
            public string StartDate { get; set; } = string.Empty; // Format YYYYMMDD
            public string EndDate { get; set; } = string.Empty;   // Format YYYYMMDD
            public string? Status { get; set; }
            public string? Note { get; set; }
        }

        private DateTime ParseYYYYMMDD(string dateStr, string paramName)
        {
            if (string.IsNullOrEmpty(dateStr))
            {
                throw new ValidationException($"Parameter {paramName} tidak boleh kosong");
            }

            const string FORMAT = "yyyyMMdd";

            if (dateStr.Length != 8 || !DateTime.TryParseExact(
                dateStr,
                FORMAT,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out DateTime result))
            {
                throw new ValidationException($"Format {paramName} harus {FORMAT}");
            }

            return result;
        }
    }
}