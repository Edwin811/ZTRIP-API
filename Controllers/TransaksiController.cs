// File: Controllers/TransaksiController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Z_TRIP.Models;
using System.IO;
using System.Threading.Tasks;
using Z_TRIP.Exceptions;
using Z_TRIP.Models.Contexts;
using System.Text.Json;

namespace Z_TRIP.Controllers
{
    [Route("api/transaksi")]
    [ApiController]
    public class TransaksiController : ControllerBase
    {
        private readonly string _constr;

        public TransaksiController(IConfiguration config)
        {
            _constr = config.GetConnectionString("koneksi")!;
        }

        // GET api/transaksi
        [HttpGet]
        [Authorize]
        public IActionResult GetAll()
        {
            try
            {
                // Ambil user ID dari token
                var userIdClaim = User.FindFirst("userId")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "User tidak valid" });
                }

                bool isAdmin = User.IsInRole("Admin");

                // Inisialisasi context
                var context = new TransaksiContext(_constr);
                var bookingCtx = new BookingContext(_constr);

                List<Transaksi> transaksiList;

                if (isAdmin)
                {
                    // Admin bisa melihat semua transaksi
                    transaksiList = context.GetAllTransaksi();
                }
                else
                {
                    // Customer hanya bisa melihat transaksi miliknya sendiri
                    var bookings = bookingCtx.GetBookingsByUserId(userId);
                    var transactionIds = bookings
                        .Where(b => b.TransactionId.HasValue)
                        .Select(b => b.TransactionId.Value)
                        .ToList();

                    // Filter transaksi berdasarkan ID booking customer
                    transaksiList = transactionIds.Count > 0
                        ? transactionIds.Select(id => context.GetTransaksiById(id))
                           .Where(t => t != null)
                           .Cast<Transaksi>()
                           .ToList()
                        : new List<Transaksi>();
                }

                // Format response
                var result = transaksiList.Select(t => new
                {
                    t.Id,
                    t.Method,
                    PaymentStatus = t.PaymentStatus.ToString(),
                    t.Amount,
                    HasPaymentImage = t.PaymentImage != null && t.PaymentImage.Length > 0,
                    BookingId = GetBookingIdForTransaction(t.Id, bookingCtx),
                    CanUploadPayment = t.PaymentStatus != payment_status_enum.paid,
                    CreatedAt = t.CreatedAt.ToString("yyyyMMdd"),
                    UpdatedAt = t.UpdatedAt.ToString("yyyyMMdd")
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // GET api/transaksi/{id}
        [HttpGet("{id}")]
        [Authorize]
        public IActionResult GetById(int id)
        {
            try
            {
                // Add validation for ID
                if (id <= 0)
                    return BadRequest(new { message = "ID transaksi tidak valid" });

                var context = new TransaksiContext(_constr);
                var transaksi = context.GetTransaksiById(id);

                if (transaksi == null)
                    return NotFound(new { message = "Transaksi tidak ditemukan" });

                // Batasi customer hanya bisa melihat transaksinya sendiri
                var bookingCtx = new BookingContext(_constr);
                var booking = bookingCtx.GetBookingByTransactionId(id);

                var userIdClaim = User.FindFirst("userId")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                    return Unauthorized(new { message = "User tidak valid" });

                bool isAdmin = User.IsInRole("Admin");

                if (!isAdmin && (booking == null || booking.UserId != userId))
                    return Forbid();

                var result = new
                {
                    transaksi.Id,
                    transaksi.Method,
                    transaksi.PaymentStatus,
                    transaksi.Amount,
                    HasPaymentImage = transaksi.PaymentImage != null && transaksi.PaymentImage.Length > 0,
                    transaksi.CreatedAt,
                    transaksi.UpdatedAt
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // GET api/transaksi/payment-image/{id}
        [HttpGet("payment-image/{id}")]
        [Authorize]
        public IActionResult GetPaymentImage(int id)
        {
            try
            {
                // Add validation for ID
                if (id <= 0)
                    return BadRequest(new { message = "ID transaksi tidak valid" });

                var context = new TransaksiContext(_constr);
                var transaksi = context.GetTransaksiById(id);

                if (transaksi == null || transaksi.PaymentImage == null || transaksi.PaymentImage.Length == 0)
                    return NotFound(new { message = "Bukti pembayaran tidak ditemukan" });

                // Batasi customer hanya bisa melihat transaksinya sendiri
                var bookingCtx = new BookingContext(_constr);
                var booking = bookingCtx.GetBookingByTransactionId(id);

                var userIdClaim = User.FindFirst("userId")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                    return Unauthorized(new { message = "User tidak valid" });

                bool isAdmin = User.IsInRole("Admin");

                if (!isAdmin && (booking == null || booking.UserId != userId))
                    return Forbid();

                return File(transaksi.PaymentImage, "image/jpeg");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // POST api/transaksi/upload-payment/{id}
        [HttpPost("upload-payment/{id}")]
        [Authorize]
        public async Task<IActionResult> UploadPaymentProof(int id, IFormFile file)
        {
            try
            {
                // Add validation for ID
                if (id <= 0)
                    return BadRequest(new { message = "ID transaksi tidak valid" });

                if (file == null || file.Length == 0)
                    return BadRequest(new { message = "File bukti pembayaran tidak boleh kosong" });

                // Validate file has a filename
                if (string.IsNullOrEmpty(file.FileName))
                    return BadRequest(new { message = "Nama file tidak valid" });

                // Validate file extension
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (string.IsNullOrEmpty(extension) || !new[] { ".jpg", ".jpeg", ".png" }.Contains(extension))
                    return BadRequest(new { message = "Format file harus jpg, jpeg, atau png" });

                // Validasi tipe file
                var allowedTypes = new[] { "image/jpeg", "image/png", "image/jpg" };
                if (!allowedTypes.Contains(file.ContentType))
                    return BadRequest(new { message = "Tipe file tidak didukung, gunakan JPG atau PNG" });

                // Validasi ukuran file (maks 5MB)
                if (file.Length > 5 * 1024 * 1024)
                    return BadRequest(new { message = "Ukuran file tidak boleh lebih dari 5MB" });

                // Ambil user ID dari token
                var userIdClaim = User.FindFirst("userId")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "User tidak valid" });
                }

                bool isAdmin = User.IsInRole("Admin");

                // Dapatkan transaksi
                var context = new TransaksiContext(_constr);
                var transaksi = context.GetTransaksiById(id);

                if (transaksi == null)
                    return NotFound(new { message = $"Transaksi dengan ID {id} tidak ditemukan" });

                // Verifikasi kepemilikan transaksi
                var bookingCtx = new BookingContext(_constr);
                var booking = bookingCtx.GetBookingByTransactionId(id);

                if (booking == null)
                    return NotFound(new { message = "Booking terkait transaksi ini tidak ditemukan" });

                // Jika bukan admin, verifikasi kepemilikan
                if (!isAdmin && booking.UserId != userId)
                    return Forbid();

                // Verifikasi status booking tidak rejected
                if (booking.Status == booking_status_enum.rejected)
                {
                    return BadRequest(new
                    {
                        message = "Booking telah ditolak. Bukti pembayaran tidak dapat diupload.",
                        statusNote = booking.StatusNote
                    });
                }

                // Validasi status transaksi - hanya unpaid yang boleh upload baru
                if (transaksi.PaymentStatus == payment_status_enum.paid)
                {
                    return BadRequest(new { message = "Pembayaran sudah diverifikasi" });
                }
                else if (transaksi.PaymentStatus == payment_status_enum.pending)
                {
                    // Jika status pending, mungkin admin menolak pembayaran sebelumnya
                    // Atau customer ingin mengupload bukti baru, tetap izinkan
                    // Namun beri peringatan
                    var warningMessage = "Bukti pembayaran sebelumnya akan diganti dengan yang baru";
                    // Bisa dilanjutkan
                }

                // Konversi file ke byte array
                byte[] imageData;
                using (var ms = new MemoryStream())
                {
                    await file.CopyToAsync(ms);
                    imageData = ms.ToArray();
                }

                // Simpan ke database
                if (!context.UploadPaymentProof(id, imageData))
                    return StatusCode(500, new { message = "Gagal mengupload bukti pembayaran" });

                // Update status menjadi pending untuk diverifikasi admin
                context.UpdateTransaksiStatus(id, payment_status_enum.pending.ToString());

                // Update booking status note
                bookingCtx.UpdateStatusNote(booking.Id,
                    "Bukti pembayaran telah diupload, menunggu verifikasi admin");

                return Ok(new
                {
                    message = "Bukti pembayaran berhasil diupload",
                    status = "pending",
                    nextSteps = new[] { "Bukti pembayaran Anda akan diverifikasi oleh admin" }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // GET api/transaksi/unpaid
        [HttpGet("unpaid")]
        [Authorize]
        public IActionResult GetUnpaidTransaksi()
        {
            try
            {
                // Ambil user ID dari token
                var userIdClaim = User.FindFirst("userId")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "User tidak valid" });
                }

                bool isAdmin = User.IsInRole("Admin");

                // Get bookings for the user
                var bookingCtx = new BookingContext(_constr);
                var bookings = isAdmin
                    ? bookingCtx.GetAllBookings()
                    : bookingCtx.GetBookingsByUserId(userId);

                var context = new TransaksiContext(_constr);
                var result = new List<object>();

                // Get transaction details for each booking
                foreach (var booking in bookings)
                {
                    if (!booking.TransactionId.HasValue)
                        continue; // Skip if no transaction

                    var transaksi = context.GetTransaksiById(booking.TransactionId.Value);
                    if (transaksi == null || transaksi.PaymentStatus == payment_status_enum.paid)
                        continue; // Skip if transaction was paid

                    // Get vehicle details
                    var unit = new VehicleUnitsContext(_constr).GetVehicleUnitById(booking.VehicleUnitId);
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

                    result.Add(new
                    {
                        TransactionId = transaksi.Id,
                        BookingId = booking.Id,
                        Vehicle = $"{vehicleModel} {vehicleName}",
                        UnitCode = unit?.Code ?? "Unknown",
                        BookingPeriod = new
                        {
                            StartDate = FormatYYYYMMDD(booking.StartDatetime),
                            EndDate = FormatYYYYMMDD(booking.EndDatetime)
                        },
                        Amount = transaksi.Amount,
                        Status = transaksi.PaymentStatus.ToString(),
                        PaymentMethod = transaksi.Method.ToString(),
                        HasProofOfPayment = transaksi.PaymentImage != null && transaksi.PaymentImage.Length > 0,
                        CreatedAt = FormatYYYYMMDD(transaksi.CreatedAt)
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // Helper method untuk format tanggal
        private string FormatYYYYMMDD(DateTime date)
        {
            // Add validation to handle DateTime.MinValue
            if (date == DateTime.MinValue)
                return "00000000";

            return date.ToString("yyyyMMdd");
        }

        // Tambahkan method ini ke TransaksiController
        private void SyncBookingStatus(int transactionId, payment_status_enum newStatus)
        {
            try
            {
                // Validate transactionId
                if (transactionId <= 0)
                {
                    Console.WriteLine($"Invalid transaction ID: {transactionId}");
                    return;
                }

                // Cari booking yang terkait dengan transaksi ini
                var bookingCtx = new BookingContext(_constr);
                var booking = bookingCtx.GetBookingByTransactionId(transactionId);

                if (booking == null)
                    return; // Tidak ada booking terkait

                switch (newStatus)
                {
                    case payment_status_enum.paid:
                        // Jika payment diubah menjadi paid, booking menjadi approved
                        if (booking.Status == booking_status_enum.pending)
                        {
                            bookingCtx.UpdateStatusBooking(booking.Id, booking_status_enum.approved.ToString());
                            bookingCtx.UpdateStatusNote(booking.Id, "Pembayaran telah diverifikasi, booking disetujui");
                        }
                        break;

                    case payment_status_enum.pending:
                        // Tidak perlu mengubah status booking pada saat payment menjadi pending
                        // Status booking tetap pending
                        bookingCtx.UpdateStatusNote(booking.Id, "Bukti pembayaran telah diupload, menunggu verifikasi");
                        break;

                    case payment_status_enum.unpaid:
                        // Jika payment dikembalikan ke unpaid (rejected payment), berikan notifikasi
                        if (booking.Status == booking_status_enum.approved)
                        {
                            // Ini kasus khusus - jangan ubah status booking yang sudah approved
                            bookingCtx.UpdateStatusNote(booking.Id,
                                "PERHATIAN: Status pembayaran diubah menjadi unpaid padahal booking sudah approved. " +
                                "Silakan hubungi admin.");
                        }
                        else if (booking.Status == booking_status_enum.pending)
                        {
                            // Normal case: pembayaran ditolak, status booking tetap pending
                            bookingCtx.UpdateStatusNote(booking.Id,
                                "Bukti pembayaran ditolak. Mohon upload ulang bukti pembayaran yang valid.");
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                // Log error tapi jangan gagalkan operasi
                Console.WriteLine($"Error saat sinkronisasi status: {ex.Message}");
            }
        }

        // Helper method untuk mendapatkan booking ID dari transaksi
        private int? GetBookingIdForTransaction(int transactionId, BookingContext bookingCtx)
        {
            var booking = bookingCtx.GetBookingByTransactionId(transactionId);
            return booking?.Id;
        }

        // POST api/transaksi/{id}/approve-payment
        [HttpPost("{id}/approve-payment")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult ApprovePayment(int id)
        {
            try
            {
                // Add validation for ID
                if (id <= 0)
                    return BadRequest(new { message = "ID transaksi tidak valid" });

                var context = new TransaksiContext(_constr);
                var transaksi = context.GetTransaksiById(id);

                if (transaksi == null)
                    return NotFound(new { message = $"Transaksi dengan ID {id} tidak ditemukan" });

                // Cek status transaksi
                if (transaksi.PaymentStatus == payment_status_enum.paid)
                    return BadRequest(new { message = "Pembayaran sudah diverifikasi" });

                if (transaksi.PaymentStatus == payment_status_enum.unpaid)
                    return BadRequest(new { message = "Belum ada bukti pembayaran untuk disetujui" });

                // Update status transaksi ke paid
                context.UpdateTransaksiStatus(id, payment_status_enum.paid.ToString());

                // Update status booking juga
                var bookingCtx = new BookingContext(_constr);
                var booking = bookingCtx.GetBookingByTransactionId(id);

                if (booking == null)
                    return NotFound(new { message = "Booking terkait transaksi ini tidak ditemukan" });

                // Update booking menjadi approved jika masih pending
                if (booking.Status == booking_status_enum.pending)
                {
                    bookingCtx.UpdateStatusBooking(booking.Id, booking_status_enum.approved.ToString());
                    bookingCtx.UpdateStatusNote(booking.Id, "Pembayaran diverifikasi, booking disetujui");
                }

                return Ok(new
                {
                    message = "Pembayaran berhasil diverifikasi",
                    paymentStatus = "paid",
                    bookingId = booking.Id,
                    bookingStatus = booking_status_enum.approved.ToString(),
                    note = "Booking berhasil disetujui setelah verifikasi pembayaran"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // POST api/transaksi/{id}/reject-payment
        [HttpPost("{id}/reject-payment")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult RejectPayment(int id, [FromBody] JsonElement requestBody)
        {
            try
            {
                // Add validation for ID
                if (id <= 0)
                    return BadRequest(new { message = "ID transaksi tidak valid" });

                // Check if request body is null/undefined
                if (requestBody.ValueKind == JsonValueKind.Undefined ||
                    requestBody.ValueKind == JsonValueKind.Null)
                {
                    return BadRequest(new { message = "Data alasan penolakan harus diisi" });
                }

                // Extract reason from JSON request
                if (!requestBody.TryGetProperty("reason", out JsonElement reasonElement) ||
                    reasonElement.ValueKind != JsonValueKind.String)
                {
                    return BadRequest(new { message = "Alasan penolakan harus diisi" });
                }

                string reason = reasonElement.GetString() ?? string.Empty;

                if (string.IsNullOrEmpty(reason))
                    return BadRequest(new { message = "Alasan penolakan harus diisi" });

                // Validate reason length
                if (reason.Length < 5)
                    return BadRequest(new { message = "Alasan penolakan terlalu pendek, minimal 5 karakter" });

                if (reason.Length > 500)
                    return BadRequest(new { message = "Alasan penolakan terlalu panjang, maksimal 500 karakter" });


                var context = new TransaksiContext(_constr);
                var transaksi = context.GetTransaksiById(id);

                if (transaksi == null)
                    return NotFound(new { message = $"Transaksi dengan ID {id} tidak ditemukan" });

                // Cek status transaksi
                if (transaksi.PaymentStatus != payment_status_enum.pending)
                    return BadRequest(new
                    {
                        message = "Hanya pembayaran dengan status pending yang dapat ditolak",
                        currentStatus = transaksi.PaymentStatus.ToString()
                    });

                // Update status transaksi ke unpaid
                context.UpdateTransaksiStatus(id, payment_status_enum.unpaid.ToString());

                // Update status booking juga
                var bookingCtx = new BookingContext(_constr);
                var booking = bookingCtx.GetBookingByTransactionId(id);

                if (booking != null)
                {
                    // Update booking note dengan alasan penolakan
                    bookingCtx.UpdateStatusNote(booking.Id, $"Bukti pembayaran ditolak: {reason}");

                    // Booking status tetap pending, customer perlu upload ulang
                    if (booking.Status != booking_status_enum.pending)
                    {
                        bookingCtx.UpdateStatusBooking(booking.Id, booking_status_enum.pending.ToString());
                    }
                }

                return Ok(new
                {
                    message = "Pembayaran berhasil ditolak",
                    paymentStatus = "unpaid",
                    reason = reason,
                    bookingId = booking?.Id,
                    bookingStatus = booking?.Status.ToString() ?? "unknown",
                    nextSteps = "Customer perlu mengupload ulang bukti pembayaran yang valid"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // PUT api/transaksi/{id}/update-status
        [HttpPut("{id}/update-status")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult UpdateStatus(int id, [FromBody] JsonElement requestBody)
        {
            try
            {
                // Add validation for ID
                if (id <= 0)
                    return BadRequest(new { message = "ID transaksi tidak valid" });

                // Check if request body is null/undefined
                if (requestBody.ValueKind == JsonValueKind.Undefined ||
                    requestBody.ValueKind == JsonValueKind.Null)
                {
                    return BadRequest(new { message = "Data status harus diisi" });
                }

                // Extract status from JSON request
                if (!requestBody.TryGetProperty("status", out JsonElement statusElement) ||
                    statusElement.ValueKind != JsonValueKind.String)
                {
                    return BadRequest(new { message = "Status harus diisi" });
                }

                string status = statusElement.GetString() ?? string.Empty;

                if (string.IsNullOrEmpty(status))
                    return BadRequest(new { message = "Status harus diisi" });

                // List allowed status values for better error messaging
                var allowedStatusValues = string.Join(", ", Enum.GetNames(typeof(payment_status_enum)));

                // Validasi status
                if (!Enum.TryParse<payment_status_enum>(status, true, out var newStatus))
                    return BadRequest(new
                    {
                        message = $"Status pembayaran tidak valid. Nilai yang diizinkan: {allowedStatusValues}"
                    });


                var context = new TransaksiContext(_constr);
                var transaksi = context.GetTransaksiById(id);

                if (transaksi == null)
                    return NotFound(new { message = $"Transaksi dengan ID {id} tidak ditemukan" });

                // Update status transaksi
                if (!context.UpdateTransaksiStatus(id, newStatus.ToString()))
                    return StatusCode(500, new { message = "Gagal mengupdate status transaksi" });

                // Sync booking status based on new payment status
                SyncBookingStatus(id, newStatus);

                return Ok(new
                {
                    message = $"Status transaksi berhasil diubah menjadi {newStatus.ToString()}",
                    transactionId = id,
                    newStatus = newStatus.ToString()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }
    }
}
