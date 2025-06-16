using System;
using System.ComponentModel.DataAnnotations;

namespace Z_TRIP.Models
{
    public enum booking_status_enum
    {
        pending,
        approved,
        rejected,
        on_going,
        overtime,
        done
    }

    public class Booking
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int VehicleUnitId { get; set; }

        [Required]
        public DateTime StartDatetime { get; set; }

        [Required]
        public DateTime EndDatetime { get; set; }

        public DateTime? RequestDate { get; set; }

        public booking_status_enum Status { get; set; } = booking_status_enum.pending;

        public DateTime StatusUpdatedAt { get; set; }

        public string? StatusNote { get; set; }

        public int? TransactionId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        // Validasi method
        public bool IsValid()
        {
            return StartDatetime < EndDatetime &&
                   StartDatetime >= DateTime.Today &&
                   UserId > 0 &&
                   VehicleUnitId > 0;
        }
    }
}