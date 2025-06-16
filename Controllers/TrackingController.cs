using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Z_TRIP.Models;
using System;
using Z_TRIP.Models.Contexts;

namespace Z_TRIP.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrackingController : ControllerBase
    {
        private readonly string _constr;

        public TrackingController(IConfiguration config)
        {
            _constr = config.GetConnectionString("koneksi")!;
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult GetAll()
        {
            try
            {
                var ctx = new TrackingContext(_constr);
                var trackingData = ctx.GetAll();

                return Ok(new
                {
                    message = "Data tracking berhasil diambil",
                    count = trackingData.Count,
                    data = trackingData
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        [HttpGet("by-booking/{bookingId}")]
        [Authorize]
        public IActionResult GetByBooking(int bookingId)
        {
            try
            {
                // Add validation for bookingId
                if (bookingId <= 0)
                    return BadRequest(new { message = "ID booking tidak valid" });

                var userId = int.Parse(User.FindFirst("userId")?.Value ?? "0");
                if (userId == 0)
                    return Unauthorized(new { message = "User tidak valid" });

                // Verifikasi apakah user adalah admin atau pemilik booking
                bool isAdmin = User.IsInRole("Admin");
                if (!isAdmin)
                {
                    var bookingCtx = new BookingContext(_constr);
                    var booking = bookingCtx.GetBookingById(bookingId);
                    if (booking == null)
                        return NotFound(new { message = "Booking tidak ditemukan" });

                    if (booking.UserId != userId)
                        return Forbid();
                }

                var ctx = new TrackingContext(_constr);
                var trackingData = ctx.GetByBookingId(bookingId);

                return Ok(new
                {
                    message = "Data tracking berhasil diambil",
                    count = trackingData.Count,
                    data = trackingData
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        [HttpPost]
        [Authorize]
        public IActionResult Create([FromBody] Tracking tracking)
        {
            try
            {
                // Basic null check - you already have this
                if (tracking == null || tracking.BookingId <= 0 ||
                    string.IsNullOrEmpty(tracking.Latitude) ||
                    string.IsNullOrEmpty(tracking.Longitude))
                {
                    return BadRequest(new { message = "Data tracking tidak lengkap" });
                }

                // Add format validation for coordinates
                if (!IsValidCoordinate(tracking.Latitude, true))
                {
                    return BadRequest(new { message = "Format latitude tidak valid" });
                }

                if (!IsValidCoordinate(tracking.Longitude, false))
                {
                    return BadRequest(new { message = "Format longitude tidak valid" });
                }

                var userId = int.Parse(User.FindFirst("userId")?.Value ?? "0");
                if (userId == 0)
                    return Unauthorized(new { message = "User tidak valid" });

                // Verifikasi apakah booking adalah milik user dan statusnya on_going
                var bookingCtx = new BookingContext(_constr);
                var booking = bookingCtx.GetBookingById(tracking.BookingId);

                if (booking == null)
                    return NotFound(new { message = "Booking tidak ditemukan" });

                if (booking.UserId != userId && !User.IsInRole("Admin"))
                    return Forbid();

                if (booking.Status != booking_status_enum.on_going)
                    return BadRequest(new
                    {
                        message = "Tracking hanya bisa dilakukan untuk booking yang sedang berlangsung",
                        status = booking.Status.ToString()
                    });

                var ctx = new TrackingContext(_constr);
                var status = ctx.Create(tracking);

                if (status == "Berhasil")
                    return Ok(new { message = "Lokasi berhasil dicatat" });
                else
                    return StatusCode(500, new { message = status });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult Delete(int id)
        {
            try
            {
                // Add validation for id
                if (id <= 0)
                    return BadRequest(new { message = "ID tracking tidak valid" });

                var ctx = new TrackingContext(_constr);
                var status = ctx.Delete(id);

                return status == "Berhasil"
                    ? Ok(new { message = "Data tracking berhasil dihapus" })
                    : StatusCode(500, new { message = status });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // Endpoint baru untuk mendapatkan lokasi terbaru dari booking
        [HttpGet("latest/{bookingId}")]
        [Authorize]
        public IActionResult GetLatestTracking(int bookingId)
        {
            try
            {
                // Add validation for bookingId
                if (bookingId <= 0)
                    return BadRequest(new { message = "ID booking tidak valid" });

                var userId = int.Parse(User.FindFirst("userId")?.Value ?? "0");
                if (userId == 0)
                    return Unauthorized(new { message = "User tidak valid" });

                // Verifikasi apakah user adalah admin atau pemilik booking
                bool isAdmin = User.IsInRole("Admin");
                if (!isAdmin)
                {
                    var bookingCtx = new BookingContext(_constr);
                    var booking = bookingCtx.GetBookingById(bookingId);
                    if (booking == null)
                        return NotFound(new { message = "Booking tidak ditemukan" });

                    if (booking.UserId != userId)
                        return Forbid();
                }

                var ctx = new TrackingContext(_constr);
                var latestTracking = ctx.GetLatestByBookingId(bookingId);

                if (latestTracking == null)
                    return NotFound(new { message = "Belum ada data tracking untuk booking ini" });

                return Ok(latestTracking);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // Helper method to validate coordinates
        private bool IsValidCoordinate(string coordinate, bool isLatitude)
        {
            // Check if it can be parsed as double
            if (!double.TryParse(coordinate, out double value))
                return false;

            // Check range
            if (isLatitude) // Latitude range: -90 to 90
                return value >= -90 && value <= 90;
            else // Longitude range: -180 to 180
                return value >= -180 && value <= 180;
        }
    }
}
