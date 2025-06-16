using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Z_TRIP.Models;
using Z_TRIP.Helpers; // Tambahkan using directive untuk SqlDBHelper
using Z_TRIP.Exceptions; // Tambahkan juga untuk exception handling

namespace Z_TRIP.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Policy = "AdminOnly")]
    public class AdminController : ControllerBase
    {
        private readonly string _constr;

        public AdminController(IConfiguration config)
        {
            _constr = config.GetConnectionString("koneksi")!;
        }

        // GET api/admin/customers
        [HttpGet("customers")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult GetAllCustomers()
        {
            try
            {
                var ctx = new UsersContext(_constr);
                var customers = ctx.GetCustomers()
                    .Select(c => new
                    {
                        c.Id,
                        c.Email,
                        c.Name,
                        HasProfile = c.Profile != null && c.Profile.Length > 0, // Baik untuk byte[] maupun string
                        HasKtp = c.KtpImage != null && c.KtpImage.Length > 0,
                        HasSim = c.SimImage != null && c.SimImage.Length > 0,
                        c.IsVerified,
                        c.CreatedAt,
                        c.UpdatedAt
                    })
                    .ToList();

                return Ok(new
                {
                    count = customers.Count,
                    customers
                });
            }
            catch (Exception ex)
            {
                // Global middleware will handle
                throw;
            }
        }

        // GET api/admin/customers/{id}
        [HttpGet("customers/{id}")]
        public IActionResult GetCustomer(int id)
        {
            var context = new UsersContext(_constr);
            var user = context.GetUserById(id);

            if (user == null)
                return NotFound(new { message = "User tidak ditemukan" });

            if (user.Role)
                return BadRequest(new { message = "Endpoint ini hanya untuk data customer" });

            // Do not send password and binary data
            var result = new
            {
                user.Id,
                user.Email,
                user.Name,
                user.Profile,
                HasKtp = user.KtpImage != null && user.KtpImage.Length > 0,
                HasSim = user.SimImage != null && user.SimImage.Length > 0,
                user.CreatedAt,
                user.UpdatedAt
            };

            return Ok(result);
        }

        // GET api/admin/customers/{id}/ktp
        [HttpGet("customers/{id}/ktp")]
        public IActionResult GetCustomerKtp(int id)
        {
            var context = new UsersContext(_constr);
            var user = context.GetUserById(id);

            if (user == null)
                return NotFound(new { message = "User tidak ditemukan" });

            if (user.KtpImage == null || user.KtpImage.Length == 0)
                return NotFound(new { message = "KTP tidak ditemukan" });

            return File(user.KtpImage, "image/jpeg");
        }

        // GET api/admin/customers/{id}/sim
        [HttpGet("customers/{id}/sim")]
        public IActionResult GetCustomerSim(int id)
        {
            var context = new UsersContext(_constr);
            var user = context.GetUserById(id);

            if (user == null)
                return NotFound(new { message = "User tidak ditemukan" });

            if (user.SimImage == null || user.SimImage.Length == 0)
                return NotFound(new { message = "SIM tidak ditemukan" });

            return File(user.SimImage, "image/jpeg");
        }

        // PUT api/admin/customers/{id}/verify
        [HttpPut("customers/{id}/verify")]
        public IActionResult VerifyCustomer(int id)
        {
            try
            {
                var context = new UsersContext(_constr);
                var user = context.GetUserById(id);

                if (user == null)
                    return NotFound(new { message = "User tidak ditemukan" });

                if (user.Role)
                    return BadRequest(new { message = "Endpoint ini hanya untuk verifikasi customer" });

                // Pastikan customer sudah upload dokumen
                if (user.KtpImage == null || user.KtpImage.Length == 0)
                    return BadRequest(new { message = "Customer belum mengupload KTP" });

                if (user.SimImage == null || user.SimImage.Length == 0)
                    return BadRequest(new { message = "Customer belum mengupload SIM" });

                // Update status verifikasi di database menggunakan using pattern
                using (var db = new SqlDBHelper(_constr))
                {
                    const string query = @"
                UPDATE users
                SET is_verified = true,
                    updated_at = CURRENT_TIMESTAMP
                WHERE id = @UserId";

                    using var cmd = db.GetNpgsqlCommand(query);
                    cmd.Parameters.AddWithValue("@UserId", id);

                    int rowsAffected = cmd.ExecuteNonQuery();

                    if (rowsAffected > 0)
                        return Ok(new { message = "Customer berhasil diverifikasi" });
                    else
                        return StatusCode(500, new { message = "Gagal memverifikasi customer" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // GET api/admin/bookings
        [HttpGet("bookings")]
        public IActionResult GetAllBookings()
        {
            var context = new BookingContext(_constr);
            var bookings = context.GetAllBookings();

            return Ok(bookings);
        }

        // GET api/admin/bookings/pending
        [HttpGet("bookings/pending")]
        public IActionResult GetPendingBookings()
        {
            var context = new BookingContext(_constr);
            var bookings = context.GetBookingsByStatus("pending");

            return Ok(bookings);
        }

        // PUT api/admin/bookings/{id}/approve
        [HttpPut("bookings/{id}/approve")]
        public IActionResult ApproveBooking(int id)
        {
            var context = new BookingContext(_constr);
            var booking = context.GetBookingById(id);

            if (booking == null)
                return NotFound(new { message = "Booking tidak ditemukan" });

            if (booking.Status != booking_status_enum.pending)
                return BadRequest(new { message = "Booking sudah diproses sebelumnya" });

            // Update status booking menjadi approved
            if (context.UpdateStatusBooking(id, "approved"))
                return Ok(new { message = "Booking berhasil diapprove" });

            return StatusCode(500, new { message = "Gagal mengupdate status booking" });
        }

        // PUT api/admin/bookings/{id}/reject
        [HttpPut("bookings/{id}/reject")]
        public IActionResult RejectBooking(int id, [FromBody] StatusNoteRequest request)
        {
            var context = new BookingContext(_constr);
            var booking = context.GetBookingById(id);

            if (booking == null)
                return NotFound(new { message = "Booking tidak ditemukan" });

            if (booking.Status != booking_status_enum.pending)
                return BadRequest(new { message = "Booking sudah diproses sebelumnya" });

            // Update status booking menjadi rejected dengan catatan
            booking.Status = booking_status_enum.rejected;
            booking.StatusNote = request.StatusNote;

            if (context.UpdateBooking(id, booking))
                return Ok(new { message = "Booking ditolak" });

            return StatusCode(500, new { message = "Gagal mengupdate status booking" });
        }

        // GET api/admin/active-bookings
        [HttpGet("active-bookings")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult GetActiveBookings()
        {
            try
            {
                var bookingCtx = new BookingContext(_constr);
                var bookings = bookingCtx.GetBookingsByStatus("on_going");

                // Untuk setiap booking yang aktif, dapatkan data user dan kendaraan
                var result = bookings.Select(b =>
                {
                    var user = new UsersContext(_constr).GetUserById(b.UserId);
                    var vehicleUnit = new VehicleUnitsContext(_constr).GetVehicleUnitById(b.VehicleUnitId);
                    var vehicle = vehicleUnit != null ?
                        new VehicleContext(_constr).GetVehicleById(vehicleUnit.VehicleId) : null;

                    // Dapatkan lokasi terbaru jika ada
                    var latestTracking = new TrackingContext(_constr).GetLatestByBookingId(b.Id);

                    return new
                    {
                        BookingId = b.Id,
                        StartDatetime = b.StartDatetime,
                        EndDatetime = b.EndDatetime,
                        Status = b.Status.ToString(),
                        User = user != null ? new
                        {
                            UserId = user.Id,
                            Name = user.Name,
                            Email = user.Email
                        } : null,
                        Vehicle = vehicle != null ? new
                        {
                            Id = vehicle.Id,
                            Name = vehicle.Name,
                            Category = vehicle.Category.ToString(),
                            UnitCode = vehicleUnit?.Code
                        } : null,
                        LatestLocation = latestTracking != null ? new
                        {
                            Latitude = latestTracking.Latitude,
                            Longitude = latestTracking.Longitude,
                            RecordedAt = latestTracking.RecordedAt
                        } : null
                    };
                }).ToList();

                return Ok(new
                {
                    message = "Data booking aktif berhasil diambil",
                    count = result.Count,
                    data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // Endpoint untuk mendapatkan daftar pengguna yang perlu verifikasi
        [HttpGet("customers/verification-needed")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult GetCustomersNeedingVerification()
        {
            try
            {
                var ctx = new UsersContext(_constr);
                var allCustomers = ctx.GetCustomers();

                var customersToVerify = allCustomers
                    .Where(c => !c.Role && !c.IsVerified)
                    .Where(c => c.KtpImage != null && c.KtpImage.Length > 0 &&
                                c.SimImage != null && c.SimImage.Length > 0)
                    .Select(c => new
                    {
                        c.Id,
                        c.Name,
                        c.Email,
                        c.Profile,
                        HasKtp = c.KtpImage != null && c.KtpImage.Length > 0,
                        HasSim = c.SimImage != null && c.SimImage.Length > 0,
                        c.CreatedAt,
                        DocumentsComplete = c.KtpImage != null && c.KtpImage.Length > 0 &&
                                           c.SimImage != null && c.SimImage.Length > 0
                    })
                    .ToList();

                return Ok(new
                {
                    total_count = customersToVerify.Count,
                    customers = customersToVerify
                });
            }
            catch (Exception ex)
            {
                // Global middleware will handle
                throw;
            }
        }
    }

    public class StatusNoteRequest
    {
        public string? StatusNote { get; set; }
    }
}