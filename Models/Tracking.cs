// File: Models/Tracking.cs
using System;

namespace Z_TRIP.Models
{
    public class Tracking
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public string Latitude { get; set; } = null!;
        public string Longitude { get; set; } = null!;
        public DateTime? RecordedAt { get; set; }
    }
}
