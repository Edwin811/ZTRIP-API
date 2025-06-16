// File: Models/Transaksi.cs
using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Z_TRIP.Models
{
    public enum payment_method_enum
    {
        QRIS
    }

    public enum payment_status_enum
    {
        unpaid,  // Status awal - belum ada bukti pembayaran
        pending, // Bukti pembayaran sudah diupload, menunggu verifikasi
        paid     // Pembayaran sudah diverifikasi
    }

    public class Transaksi
    {
        public int Id { get; set; }
        public payment_method_enum Method { get; set; } = payment_method_enum.QRIS;
        public byte[]? PaymentImage { get; set; }
        public payment_status_enum PaymentStatus { get; set; } = payment_status_enum.pending;
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class TransaksiRequest
    {
        [Required(ErrorMessage = "Method pembayaran harus diisi")]
        public payment_method_enum Method { get; set; } = payment_method_enum.QRIS;

        [Required(ErrorMessage = "Amount harus diisi")]
        [Range(0, double.MaxValue, ErrorMessage = "Amount tidak boleh negatif")]
        public decimal Amount { get; set; }

        public IFormFile? PaymentImage { get; set; }
    }
}