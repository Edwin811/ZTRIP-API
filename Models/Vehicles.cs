using System;

namespace Z_TRIP.Models
{
    public enum vehicle_category_enum
    {
        mobil,
        motor
    }
    public class Vehicle
    {
        public int Id { get; set; }
        public string Merk { get; set; } = string.Empty;
        public vehicle_category_enum Category { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Capacity { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}