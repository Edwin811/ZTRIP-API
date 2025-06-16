// File: Models/VehicleUnit.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace Z_TRIP.Models
{
    public class VehicleUnit
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public int VehicleId { get; set; }
        public decimal PricePerDay { get; set; }

        // Tambahkan JsonIgnore untuk mencegah serialisasi/deserialisasi dari JSON
        [JsonIgnore]
        public byte[]? VehicleImage { get; set; }

        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class VehicleUnitRequest
    {
        [Required(ErrorMessage = "Code tidak boleh kosong")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "VehicleId harus diisi")]
        public int VehicleId { get; set; }

        [Required(ErrorMessage = "PricePerDay harus diisi")]
        [Range(0, double.MaxValue, ErrorMessage = "PricePerDay tidak boleh negatif")]
        public decimal PricePerDay { get; set; }

        public string? Description { get; set; }

        public IFormFile? Image { get; set; }
    }
}
